using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Services;

/// <summary>
/// Dựng sáu báo cáo thống kê nhân sự (PRD 10.1 → 10.6) từ dữ liệu Supabase.
///
/// <para>
/// <b>Vì sao tách khỏi controller:</b> bộ số liệu này giờ có hai người dùng — trang
/// Báo cáo &amp; Thống kê, và Trợ lý AI (qua <c>Api/AiDataController</c>). Nếu để nguyên
/// trong controller thì endpoint của Trợ lý AI buộc phải gọi chéo sang controller, hoặc
/// chép lại công thức — chép lại nghĩa là hai bản số liệu sẽ lệch nhau.
/// </para>
///
/// <para>
/// Phạm vi dữ liệu <b>không</b> được quyết định ở đây: lớp gọi phải tự gọi
/// <see cref="ReportPolicy.ResolveScope"/> rồi truyền vào. Nhờ vậy không có đường nào
/// lấy được số liệu ngoài quyền mà bỏ qua bước kiểm tra.
/// </para>
/// </summary>
public sealed class BaoCaoReportService
{
    /// <summary>Thời gian sống của bộ nhớ đệm số liệu báo cáo, tính bằng giây.</summary>
    private const int CacheSeconds = 60;

    /// <summary>Danh sách tab hợp lệ; giá trị khác sẽ rơi về tab mặc định.</summary>
    private static readonly string[] ValidTabs =
        ["bien-dong", "co-cau", "hoc-van", "quy-luong", "chuyen-can", "su-kien"];

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BaoCaoReportService> _logger;

    public BaoCaoReportService(
        AppDbContext context,
        IMemoryCache cache,
        ILogger<BaoCaoReportService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Dựng toàn bộ sáu báo cáo cho kỳ và phạm vi đã chọn. Kết quả được đệm 60 giây theo
    /// khóa gồm đủ tham số, vì các báo cáo này quét lượng lớn dữ liệu chấm công và phiếu lương.
    /// </summary>
    /// <param name="scope">Phạm vi dữ liệu đã được <see cref="ReportPolicy.ResolveScope"/> phê duyệt.</param>
    /// <param name="role">Tên vai trò người đang xem, quyết định có kèm báo cáo quỹ lương hay không.</param>
    public async Task<BaoCaoViewModel> BuildAsync(
        ReportPolicy.BaoCaoScope scope,
        string? role,
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var roleLabel = ResolveRoleLabel(role);

        // ── Chuẩn hóa bộ lọc kỳ ──────────────────────────────────────────────
        var year = nam is >= 2000 and <= 2200 ? nam.Value : today.Year;
        var periodKind = (ky ?? "THANG").ToUpperInvariant() switch
        {
            "QUY" => "QUY",
            "NAM" => "NAM",
            _ => "THANG"
        };

        int? month = periodKind == "THANG" ? Math.Clamp(thang ?? today.Month, 1, 12) : null;
        int? quarter = periodKind == "QUY" ? Math.Clamp(quy ?? ((today.Month - 1) / 3) + 1, 1, 4) : null;

        var (from, to, periodLabel) = ReportPolicy.ResolvePeriodRange(year, quarter, month);

        // Phạm vi phòng ban: Quản lý bị ghép cứng vào bộ phận của mình nên tham số URL vô hiệu.
        int? departmentId = scope.IsWholeHotel
            ? (phongBan is > 0 ? phongBan : null)
            : scope.DepartmentId;

        var cacheKey = string.Join('|',
            "BaoCaoMetrics_V1", role ?? "-", year, month, quarter, departmentId, today.DayNumber);

        if (_cache.TryGetValue(cacheKey, out BaoCaoViewModel? cached) && cached is not null)
        {
            // Tab là trạng thái giao diện, không nằm trong khóa đệm. Trả bản sao thay vì ghi
            // thẳng vào object đệm: mục đệm được chia sẻ giữa các yêu cầu (và giữa trang
            // Báo cáo với cầu dữ liệu của Trợ lý AI), nên sửa tại chỗ sẽ khiến yêu cầu này
            // đổi tab của yêu cầu khác.
            return cached.CloneWithTab(NormalizeTab(tab));
        }

        var model = new BaoCaoViewModel
        {
            KyBaoCao = periodKind,
            Nam = year,
            Thang = month,
            Quy = quarter,
            PhongBanId = departmentId,
            NhanKy = periodLabel,
            TuNgay = from,
            DenNgay = to,
            Tab = NormalizeTab(tab),
            IsWholeHotel = scope.IsWholeHotel,
            RoleLabel = roleLabel,
            CanViewPayrollFund = ReportPolicy.CanViewPayrollFundReport(role),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var phongBans = await _context.PhongBans
                .AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .Select(p => new { p.MaPhongBan, p.TenPhongBan })
                .ToArrayAsync();

            var deptNames = phongBans.ToDictionary(p => p.MaPhongBan, p => p.TenPhongBan);

            model.PhamViLabel = ResolveScopeLabel(scope, departmentId, deptNames);

            // Danh mục phòng ban chỉ hiện khi người dùng thực sự được chọn phạm vi.
            model.PhongBanOptions = scope.IsWholeHotel
                ? [.. phongBans.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    p.TenPhongBan, p.MaPhongBan.ToString(CultureInfo.InvariantCulture)))]
                : [];

            model.NamOptions = BuildYearOptions(year, today.Year);

            var nhanSu = await LoadEmployeesAsync(departmentId);

            model.BienDong = BuildBienDong(nhanSu, from, to, year);
            model.CoCau = BuildCoCau(nhanSu);

            // 10.3 lấy mốc thâm niên tại ngày cuối kỳ báo cáo để số liệu quá khứ vẫn tái lập được.
            model.HocVanThamNien = BuildHocVanThamNien(nhanSu, to);

            model.ChuyenCan = await BuildChuyenCanAsync(from, to, departmentId);

            if (model.CanViewPayrollFund)
            {
                model.QuyLuong = await BuildQuyLuongAsync(from, to, year, departmentId);
            }

            model.SuKien = await BuildSuKienAsync(from, to, scope, departmentId);

            // JSON cho ECharts. Giữ bộ mã hóa mặc định của System.Text.Json: nó thoát ký tự
            // '<' và '>' thành < / >, nên tên phòng ban hay tên sự kiện do người dùng
            // nhập không thể đóng thẻ script và chèn mã. TUYỆT ĐỐI không dùng
            // UnsafeRelaxedJsonEscaping ở đây vì sẽ mở lại đúng lỗ hổng đó.
            model.ChartJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                model.BienDong,
                model.CoCau,
                model.HocVanThamNien,
                model.QuyLuong,
                model.ChuyenCan,
                model.SuKien
            });

            model.IsDatabaseOnline = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi nạp số liệu báo cáo thống kê từ CSDL Supabase.");
            model.IsDatabaseOnline = false;
        }

        // Chỉ đệm khi lấy được dữ liệu: ghim luôn kết quả lỗi sẽ khiến trang trắng suốt 60 giây.
        if (model.IsDatabaseOnline)
        {
            _cache.Set(cacheKey, model, TimeSpan.FromSeconds(CacheSeconds));
        }

        return model;
    }

    // ── 10.1 Biến động nhân sự ─────────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo biến động nhân sự. Số liệu định kỳ (đầu kỳ / cuối kỳ / tuyển mới / thôi việc)
    /// bám theo ngày, còn biểu đồ xu hướng luôn vẽ 12 tháng của năm đang chọn để người dùng có
    /// bối cảnh — chọn một tháng lẻ mà chỉ thấy một cột thì không đọc được xu hướng.
    /// </summary>
    private static BienDongNhanSuReport BuildBienDong(
        IReadOnlyList<NhanSuRow> nhanSu,
        DateOnly from,
        DateOnly to,
        int year)
    {
        var resigned = nhanSu.Where(n => ReportPolicy.IsResigned(n.TrangThai)).ToArray();

        // Quân số tại một mốc ngày: đã vào làm và chưa thôi việc tính đến ngày đó.
        // Ngày thôi việc của một số hồ sơ có thể trống — khi đó chỉ dựa vào trạng thái.
        int HeadcountAt(DateOnly asOf) =>
            nhanSu.Count(n => n.NgayVaoLam <= asOf)
            - resigned.Count(n => !n.NgayThoiViec.HasValue || n.NgayThoiViec.Value <= asOf);

        var dauKy = Math.Max(0, HeadcountAt(from.AddDays(-1)));
        var cuoiKy = Math.Max(0, HeadcountAt(to));
        var tuyenMoi = nhanSu.Count(n => n.NgayVaoLam >= from && n.NgayVaoLam <= to);
        var nghiViec = resigned.Count(n => n.NgayThoiViec.HasValue
                                           && n.NgayThoiViec.Value >= from
                                           && n.NgayThoiViec.Value <= to);

        var report = new BienDongNhanSuReport
        {
            DauKy = dauKy,
            CuoiKy = cuoiKy,
            TuyenMoi = tuyenMoi,
            NghiViec = nghiViec,
            BienDongRong = tuyenMoi - nghiViec,
            TyLeThoiViec = ReportPolicy.TurnoverRate(ReportPolicy.AverageHeadcount(dauKy, cuoiKy), nghiViec)
        };

        var series = new List<BienDongSeriesItem>(12);
        for (var m = 1; m <= 12; m++)
        {
            var monthStart = new DateOnly(year, m, 1);
            var monthEnd = new DateOnly(year, m, DateTime.DaysInMonth(year, m));

            series.Add(new BienDongSeriesItem
            {
                Nhan = $"T{m:00}",
                TuyenMoi = nhanSu.Count(n => n.NgayVaoLam >= monthStart && n.NgayVaoLam <= monthEnd),
                NghiViec = resigned.Count(n => n.NgayThoiViec.HasValue
                                               && n.NgayThoiViec.Value >= monthStart
                                               && n.NgayThoiViec.Value <= monthEnd),
                CuoiKy = Math.Max(0, HeadcountAt(monthEnd))
            });
        }

        report.Series = series;

        report.TheoPhongBan = nhanSu
            .GroupBy(n => n.TenPhongBan)
            .Select(g => new BienDongPhongBanItem
            {
                PhongBan = g.Key,
                QuanSo = Math.Max(0, g.Count(n => n.NgayVaoLam <= to)
                                     - g.Count(n => ReportPolicy.IsResigned(n.TrangThai)
                                                    && (!n.NgayThoiViec.HasValue || n.NgayThoiViec.Value <= to))),
                TuyenMoi = g.Count(n => n.NgayVaoLam >= from && n.NgayVaoLam <= to),
                NghiViec = g.Count(n => ReportPolicy.IsResigned(n.TrangThai)
                                        && n.NgayThoiViec.HasValue
                                        && n.NgayThoiViec.Value >= from
                                        && n.NgayThoiViec.Value <= to)
            })
            .Where(row => row.QuanSo > 0 || row.TuyenMoi > 0 || row.NghiViec > 0)
            .OrderByDescending(row => row.QuanSo)
            .ThenBy(row => row.PhongBan, StringComparer.CurrentCulture)
            .ToList();

        return report;
    }

    // ── 10.2 Cơ cấu nhân lực ───────────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo cơ cấu nhân lực theo phòng ban, trạng thái, giới tính và chức vụ.
    /// Gom nhóm bằng LINQ trên bộ nhớ vì nhãn hiển thị (giới tính, trạng thái) cần dịch sang
    /// tiếng Việt — việc đó không dịch được sang SQL.
    /// </summary>
    private static CoCauNhanLucReport BuildCoCau(IReadOnlyList<NhanSuRow> nhanSu)
    {
        var total = nhanSu.Count;

        return new CoCauNhanLucReport
        {
            TongNhanSu = total,
            DangLam = nhanSu.Count(n => n.TrangThai == "DANG_LAM"),
            NghiPhep = nhanSu.Count(n => n.TrangThai == "NGHI_PHEP"),
            DaThoiViec = nhanSu.Count(n => ReportPolicy.IsResigned(n.TrangThai)),

            TheoPhongBan = [.. nhanSu
                .GroupBy(n => n.TenPhongBan)
                .Select(g => new PhanBoItem
                {
                    Nhan = g.Key,
                    GiaTri = g.Count(),
                    TyLe = PhanTram(g.Count(), total)
                })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoTrangThai = [.. nhanSu
                .GroupBy(n => ResolveStatusLabel(n.TrangThai))
                .Select(g => new PhanBoItem { Nhan = g.Key, GiaTri = g.Count(), TyLe = PhanTram(g.Count(), total) })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoGioiTinh = [.. nhanSu
                .GroupBy(n => ResolveGenderLabel(n.GioiTinh))
                .Select(g => new PhanBoItem { Nhan = g.Key, GiaTri = g.Count(), TyLe = PhanTram(g.Count(), total) })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            // Chỉ lấy tám chức vụ đông nhất: biểu đồ tròn quá nhiều lát cắt thì không đọc được.
            TheoChucVu = [.. nhanSu
                .GroupBy(n => n.TenChucVu)
                .Select(g => new PhanBoItem { Nhan = g.Key, GiaTri = g.Count(), TyLe = PhanTram(g.Count(), total) })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)
                .Take(8)]
        };
    }

    // ── 10.3 Học vấn & thâm niên ──────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo học vấn &amp; thâm niên. Trình độ được chuẩn hóa theo từ khóa
    /// (<see cref="ReportPolicy.NormalizeEducation"/>) vì cột <c>trinh_do</c> là văn bản tự do.
    /// </summary>
    private static HocVanThamNienReport BuildHocVanThamNien(IReadOnlyList<NhanSuRow> nhanSu, DateOnly asOf)
    {
        var total = nhanSu.Count;

        var educationCounts = nhanSu
            .GroupBy(n => ReportPolicy.NormalizeEducation(n.TrinhDo))
            .ToDictionary(g => g.Key, g => g.Count());

        // Duyệt theo thứ tự chuẩn (bậc cao trước, "Khác" cuối) để giữ thứ tự hiển thị ổn định.
        var theoTrinhDo = new List<PhanBoItem>();
        var higherCount = 0;
        foreach (var level in ReportPolicy.EducationOrder)
        {
            var count = educationCounts.GetValueOrDefault(level);
            if (count <= 0)
            {
                continue;
            }

            // Hai bậc đầu (sau đại học, đại học) hợp thành nhóm "từ đại học trở lên".
            if (level == ReportPolicy.EducationLevels[0] || level == ReportPolicy.EducationLevels[1])
            {
                higherCount += count;
            }

            theoTrinhDo.Add(new PhanBoItem { Nhan = level, GiaTri = count, TyLe = PhanTram(count, total) });
        }

        var tenureCounts = nhanSu
            .GroupBy(n => ReportPolicy.TenureBucketLabel(ReportPolicy.TenureMonths(n.NgayVaoLam, asOf)))
            .ToDictionary(g => g.Key, g => g.Count());

        var theoThamNien = ReportPolicy.TenureBucketOrder
            .Select(bucket => new PhanBoItem
            {
                Nhan = bucket,
                GiaTri = tenureCounts.GetValueOrDefault(bucket),
                TyLe = PhanTram(tenureCounts.GetValueOrDefault(bucket), total)
            })
            .ToList();

        var averageMonths = total == 0
            ? 0m
            : Math.Round(nhanSu.Average(n => (decimal)ReportPolicy.TenureMonths(n.NgayVaoLam, asOf)), 1);

        return new HocVanThamNienReport
        {
            TheoTrinhDo = theoTrinhDo,
            TheoThamNien = theoThamNien,
            SoDaiHocTroLen = higherCount,
            TyLeDaiHocTroLen = ReportPolicy.HigherEducationRate(higherCount, total),
            ThamNienTrungBinh = Math.Round(averageMonths / 12m, 1)
        };
    }

    // ── 10.4 Đối soát quỹ lương vs định mức ────────────────────────────────

    /// <summary>
    /// Dựng báo cáo đối soát quỹ lương.
    ///
    /// <para>
    /// CSDL chưa có bảng ngân sách quỹ lương, nên "định mức tham chiếu" được SUY RA từ tổng
    /// lương cơ bản của các hợp đồng đang hiệu lực nhân với số tháng của kỳ. Đây là định mức
    /// kỹ thuật chứ không phải hạn mức Ban Giám đốc phê duyệt — giao diện phải nói rõ điều này.
    /// </para>
    /// </summary>
    private async Task<QuyLuongNganSachReport> BuildQuyLuongAsync(
        DateOnly from,
        DateOnly to,
        int year,
        int? departmentId)
    {
        var phieuLuong = await _context.PhieuLuongs
            .AsNoTracking()
            .Where(p => p.KyLuong != null
                        && p.KyLuong.Nam == year
                        && (departmentId == null || (p.NhanVien != null && p.NhanVien.MaPhongBan == departmentId)))
            .Select(p => new PhieuLuongRow
            {
                Thang = p.KyLuong!.Thang,
                PhongBan = p.NhanVien != null && p.NhanVien.PhongBan != null
                    ? p.NhanVien.PhongBan.TenPhongBan
                    : "(Chưa xếp phòng ban)",
                TienLuongCong = p.TienLuongCong,
                TienTangCa = p.TienTangCa,
                TongPhuCap = p.TongPhuCap,
                TongThuong = p.TongThuong,
                ThucNhan = p.ThucNhan
            })
            .ToArrayAsync();

        var hopDong = await _context.HopDongs
            .AsNoTracking()
            .Where(h => h.TrangThai == "HIEU_LUC"
                        && (departmentId == null || (h.NhanVien != null && h.NhanVien.MaPhongBan == departmentId)))
            .Select(h => new HopDongRow
            {
                NgayBatDau = h.NgayBatDau,
                NgayKetThuc = h.NgayKetThuc,
                LuongCoBan = h.LuongCoBan,
                PhongBan = h.NhanVien != null && h.NhanVien.PhongBan != null
                    ? h.NhanVien.PhongBan.TenPhongBan
                    : "(Chưa xếp phòng ban)"
            })
            .ToArrayAsync();

        // Định mức của một khoảng ngày: tổng lương cơ bản của hợp đồng đang hiệu lực trong khoảng đó.
        decimal BudgetBetween(DateOnly start, DateOnly end) => hopDong
            .Where(h => h.NgayBatDau <= end && (h.NgayKetThuc == null || h.NgayKetThuc.Value >= start))
            .Sum(h => h.LuongCoBan);

        // Chỉ nhận phiếu lương thuộc đúng kỳ báo cáo (cùng năm, tháng nằm trong khoảng).
        var trongKy = phieuLuong
            .Where(p => p.Thang >= from.Month && p.Thang <= to.Month)
            .ToArray();

        // Quỹ lương gộp = lương công + tăng ca + phụ cấp + thưởng, tức mọi khoản chi từ quỹ lương.
        static decimal Gross(PhieuLuongRow p) => p.TienLuongCong + p.TienTangCa + p.TongPhuCap + p.TongThuong;

        var gross = trongKy.Sum(Gross);
        var net = trongKy.Sum(p => p.ThucNhan);

        // Định mức cả kỳ = cộng định mức từng tháng, vì một hợp đồng kéo dài nhiều tháng phải
        // được tính nhiều lần. So quỹ lương một quý với định mức một tháng sẽ lệch đúng ba lần.
        decimal budget = 0m;
        for (var m = from.Month; m <= to.Month; m++)
        {
            budget += BudgetBetween(new DateOnly(year, m, 1), new DateOnly(year, m, DateTime.DaysInMonth(year, m)));
        }

        var variance = ReportPolicy.BudgetVariance(gross, budget);
        var soThang = ReportPolicy.MonthsInPeriod(from, to);

        var series = new List<QuyLuongSeriesItem>(to.Month - from.Month + 1);
        for (var m = from.Month; m <= to.Month; m++)
        {
            series.Add(new QuyLuongSeriesItem
            {
                Nhan = $"T{m:00}",
                NganSach = BudgetBetween(new DateOnly(year, m, 1), new DateOnly(year, m, DateTime.DaysInMonth(year, m))),
                ThucChi = phieuLuong.Where(p => p.Thang == m).Sum(Gross)
            });
        }

        // Phân rã theo phòng ban: chỉ hiện phòng ban thực sự phát sinh chi phí lương trong kỳ.
        var theoPhongBan = trongKy
            .GroupBy(p => p.PhongBan)
            .Select(g => new
            {
                PhongBan = g.Key,
                ThucChi = g.Sum(Gross),
                NganSach = hopDong
                    .Where(h => string.Equals(h.PhongBan, g.Key, StringComparison.CurrentCulture))
                    .Sum(h => h.LuongCoBan) * soThang
            })
            .Select(item => new QuyLuongPhongBanItem
            {
                PhongBan = item.PhongBan,
                ThucChi = item.ThucChi,
                NganSach = item.NganSach,
                ChenhLech = item.ThucChi - item.NganSach
            })
            .OrderByDescending(item => item.ThucChi)
            .ThenBy(item => item.PhongBan, StringComparer.CurrentCulture)
            .ToList();

        return new QuyLuongNganSachReport
        {
            ThucChi = net,
            ThucChiGross = gross,
            NganSachThamChieu = budget,
            ChenhLech = variance.Variance,
            PhanTramGiaiNgan = variance.Rate,
            VuotNguong = variance.Exceeded,
            SoThangTrongKy = soThang,
            SoHopDong = hopDong.Length,
            SoPhieuLuong = trongKy.Length,
            Series = series,
            TheoPhongBan = theoPhongBan,
            GhiChuPhuongPhap =
                "Định mức tham chiếu được suy ra từ tổng lương cơ bản của hợp đồng đang hiệu lực "
                + "nhân với số tháng của kỳ, không phải hạn mức ngân sách đã được phê duyệt."
        };
    }

    // ── 10.5 Chuyên cần & vi phạm ──────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo chuyên cần &amp; vi phạm. Chấm công không mang mã nhân viên trực tiếp nên
    /// mọi truy vấn phải đi vòng qua lịch phân ca (<c>PhanCa</c>) để lấy ngày làm việc và bộ phận.
    /// </summary>
    private async Task<ChuyenCanViPhamReport> BuildChuyenCanAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var rows = await LoadAttendanceAsync(from, to, departmentId);
        var total = rows.Length;
        var dungGio = rows.Count(r => ReportPolicy.IsOnTime(r.TrangThai));

        var series = new List<ChuyenCanSeriesItem>(to.Month - from.Month + 1);
        for (var m = from.Month; m <= to.Month; m++)
        {
            var cuaThang = rows.Where(r => r.Ngay.Month == m).ToArray();
            var dungGioThang = cuaThang.Count(r => ReportPolicy.IsOnTime(r.TrangThai));

            series.Add(new ChuyenCanSeriesItem
            {
                Nhan = $"T{m:00}",
                TyLeChuyenCan = ReportPolicy.AttendanceRate(dungGioThang, cuaThang.Length),
                ViPham = cuaThang.Count(r => ReportPolicy.IsViolation(r.TrangThai))
            });
        }

        return new ChuyenCanViPhamReport
        {
            TongBanGhi = total,
            DungGio = dungGio,
            DiTre = rows.Count(r => r.TrangThai == "DI_TRE"),
            VeSom = rows.Count(r => r.TrangThai == "VE_SOM"),
            TreVaSom = rows.Count(r => r.TrangThai == "TRE_VA_SOM"),
            // Mã vắng trong CSDL là VANG_MAT, không phải "VANG" — so sánh cũ luôn ra 0.
            Vang = rows.Count(r => ReportPolicy.IsAbsent(r.TrangThai)),
            TongPhutTre = rows.Sum(r => r.PhutDiTre),
            TongPhutVeSom = rows.Sum(r => r.PhutVeSom),
            TongGioOt = rows.Sum(r => r.GioTangCaOt),
            TyLeChuyenCan = ReportPolicy.AttendanceRate(dungGio, total),
            Series = series,

            // Bảng xếp hạng kỷ luật: chỉ xét lượt vi phạm (trễ / sớm), không gồm vắng mặt.
            XepHangViPham = [.. rows
                .Where(r => ReportPolicy.IsViolation(r.TrangThai))
                .GroupBy(r => new { r.HoTen, r.PhongBan })
                .Select(g => new ViPhamNhanSuItem
                {
                    HoTen = g.Key.HoTen,
                    PhongBan = g.Key.PhongBan,
                    SoLanViPham = g.Count(),
                    TongPhutTre = g.Sum(r => r.PhutDiTre),
                    TongPhutVeSom = g.Sum(r => r.PhutVeSom)
                })
                .OrderByDescending(item => item.SoLanViPham)
                .ThenByDescending(item => item.TongPhutTre + item.TongPhutVeSom)
                .ThenBy(item => item.HoTen, StringComparer.CurrentCulture)
                .Take(10)],

            // Tỷ lệ chuyên cần theo phòng ban.
            TheoPhongBan = [.. rows
                .GroupBy(r => r.PhongBan)
                .Select(g => new PhanBoItem
                {
                    Nhan = g.Key,
                    GiaTri = g.Count(),
                    TyLe = ReportPolicy.AttendanceRate(g.Count(r => ReportPolicy.IsOnTime(r.TrangThai)), g.Count())
                })
                .OrderByDescending(item => item.TyLe)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)]
        };
    }

    // ── 10.6 Hiệu quả nhân sự dự án / sự kiện ─────────────────────────────

    /// <summary>
    /// Dựng báo cáo hiệu quả nhân sự sự kiện. Sự kiện được lọc theo ngày bắt đầu nằm trong kỳ;
    /// Quản lý chỉ thấy sự kiện có nhân sự thuộc bộ phận mình tham gia.
    /// </summary>
    private async Task<HieuQuaSuKienReport> BuildSuKienAsync(
        DateOnly from,
        DateOnly to,
        ReportPolicy.BaoCaoScope scope,
        int? departmentId)
    {
        var fromUtc = HotelEventRules.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtcExclusive = HotelEventRules.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var suKien = await _context.DuAns
            .AsNoTracking()
            .Where(d => d.NgayBatDau >= fromUtc && d.NgayBatDau < toUtcExclusive)
            .OrderBy(d => d.NgayBatDau)
            .Select(d => new SuKienRow
            {
                MaDuAn = d.MaDuAn,
                TenDuAn = d.TenDuAn,
                LoaiDuAn = d.LoaiDuAn,
                TrangThai = d.TrangThai,
                NgayBatDau = d.NgayBatDau,
                NgayKetThuc = d.NgayKetThuc,
                TinhTheoGio = d.TinhTheoGio,
                NganSachPhuCap = d.NganSachPhuCap
            })
            .ToArrayAsync();

        var maDuAns = suKien.Select(d => d.MaDuAn).ToArray();

        PhanCongRow[] phanCong = maDuAns.Length == 0
            ? []
            : await _context.PhanCongDuAns
                .AsNoTracking()
                .Where(p => maDuAns.Contains(p.MaDuAn))
                .Select(p => new PhanCongRow
                {
                    MaDuAn = p.MaDuAn,
                    MaNhanVien = p.MaNhanVien,
                    MaPhongBan = p.NhanVien != null ? p.NhanVien.MaPhongBan : null,
                    DanhGia = p.DanhGia,
                    SoTienPhuCap = p.SoTienPhuCap
                })
                .ToArrayAsync();

        // Quản lý bị thu hẹp: chỉ giữ sự kiện có ít nhất một phân công thuộc bộ phận của mình.
        // Sự kiện chưa phân công ai sẽ không hiện — thà thiếu còn hơn lộ sự kiện ngoài phạm vi.
        if (!scope.IsWholeHotel && departmentId is int dept)
        {
            var visibleIds = phanCong
                .Where(p => p.MaPhongBan == dept)
                .Select(p => p.MaDuAn)
                .ToHashSet();

            suKien = [.. suKien.Where(d => visibleIds.Contains(d.MaDuAn))];
            phanCong = [.. phanCong.Where(p => visibleIds.Contains(p.MaDuAn))];
        }

        var hourlyIds = suKien.Where(d => d.TinhTheoGio).Select(d => d.MaDuAn).ToArray();
        var hourlyAmounts = hourlyIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await _context.QuyetToanSuKiens.AsNoTracking()
                .Where(q => hourlyIds.Contains(q.MaDuAn))
                .GroupBy(q => q.MaDuAn)
                .Select(g => new { MaDuAn = g.Key, SoTien = g.Sum(q => q.SoTien) })
                .ToDictionaryAsync(q => q.MaDuAn, q => q.SoTien);
        decimal EventAmount(SuKienRow item) => item.TinhTheoGio
            ? hourlyAmounts.GetValueOrDefault(item.MaDuAn)
            : phanCong.Where(p => p.MaDuAn == item.MaDuAn).Sum(p => p.SoTienPhuCap);

        var tongLuot = phanCong.Length;

        return new HieuQuaSuKienReport
        {
            TongSuKien = suKien.Length,
            DangDienRa = suKien.Count(d => d.TrangThai == "DANG_DIEN_RA"),
            DaHoanThanh = suKien.Count(d => d.TrangThai == "HOAN_THANH"),
            DaHuy = suKien.Count(d => d.TrangThai == "HUY"),
            TongLuotPhanCong = tongLuot,
            TongNganSach = suKien.Sum(d => d.NganSachPhuCap),
            TongPhuCapDaPhanBo = suKien.Sum(EventAmount),
            TyLeXuatSac = PhanTram(phanCong.Count(p => p.DanhGia == "XUAT_SAC"), tongLuot),
            TyLeHoanThanh = PhanTram(phanCong.Count(p => p.DanhGia is "HOAN_THANH" or "XUAT_SAC"), tongLuot),

            DanhSach = [.. suKien
                .Select(d =>
                {
                    var cuaSuKien = phanCong.Where(p => p.MaDuAn == d.MaDuAn).ToArray();
                    var phuCap = EventAmount(d);

                    return new SuKienItem
                    {
                        MaDuAn = d.MaDuAn,
                        TenDuAn = d.TenDuAn,
                        LoaiLabel = HotelEventRules.TypeLabels.GetValueOrDefault(d.LoaiDuAn, d.LoaiDuAn),
                        TrangThaiLabel = HotelEventRules.StatusLabels.GetValueOrDefault(d.TrangThai, d.TrangThai),
                        // Đổi mốc UTC sang giờ Việt Nam để ngày hiển thị không lệch một ngày.
                        TuNgay = DateOnly.FromDateTime(HotelEventRules.Local(d.NgayBatDau)),
                        DenNgay = DateOnly.FromDateTime(HotelEventRules.Local(d.NgayKetThuc)),
                        SoNhanSu = cuaSuKien.Select(p => p.MaNhanVien).Distinct().Count(),
                        NganSach = d.NganSachPhuCap,
                        PhuCapDaPhanBo = phuCap,
                        TyLeSuDungNganSach = d.NganSachPhuCap <= 0m
                            ? 0m
                            : Math.Round(phuCap / d.NganSachPhuCap * 100m, 1),
                        SoXuatSac = cuaSuKien.Count(p => p.DanhGia == "XUAT_SAC"),
                        SoHoanThanh = cuaSuKien.Count(p => p.DanhGia == "HOAN_THANH"),
                        SoChuaDanhGia = cuaSuKien.Count(p => p.DanhGia == "CHUA_DANH_GIA")
                    };
                })
                .OrderByDescending(item => item.TuNgay)],

            TheoLoai = [.. suKien
                .GroupBy(d => HotelEventRules.TypeLabels.GetValueOrDefault(d.LoaiDuAn, d.LoaiDuAn))
                .Select(g => new PhanBoItem
                {
                    Nhan = g.Key,
                    GiaTri = g.Count(),
                    TyLe = PhanTram(g.Count(), suKien.Length)
                })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoDanhGia = [.. phanCong
                .GroupBy(p => HotelEventRules.RatingLabels.GetValueOrDefault(p.DanhGia, p.DanhGia))
                .Select(g => new PhanBoItem
                {
                    Nhan = g.Key,
                    GiaTri = g.Count(),
                    TyLe = PhanTram(g.Count(), tongLuot)
                })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)]
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TRUY VẤN DỮ LIỆU THÔ
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nạp hồ sơ nhân sự trong phạm vi cho phép, kèm tên phòng ban và chức vụ đã phân giải.
    /// </summary>
    private async Task<NhanSuRow[]> LoadEmployeesAsync(int? departmentId)
    {
        var raw = await _context.NhanViens
            .AsNoTracking()
            .Where(n => departmentId == null || n.MaPhongBan == departmentId)
            .Select(n => new
            {
                n.MaNhanVien,
                TenPhongBan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "(Chưa xếp phòng ban)",
                n.TrinhDo,
                n.GioiTinh,
                TenChucVu = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp chức vụ",
                n.NgayVaoLam,
                n.NgayThoiViec,
                n.TrangThai
            })
            .ToArrayAsync();

        return [.. raw.Select(n => new NhanSuRow
        {
            MaNhanVien = n.MaNhanVien,
            TenPhongBan = n.TenPhongBan,
            TrinhDo = n.TrinhDo,
            GioiTinh = n.GioiTinh ?? string.Empty,
            TenChucVu = n.TenChucVu,
            NgayVaoLam = n.NgayVaoLam,
            NgayThoiViec = n.NgayThoiViec,
            TrangThai = n.TrangThai
        })];
    }

    /// <summary>
    /// Nạp nhật ký chấm công trong kỳ. Bảng chấm công chỉ có mã phân ca nên phải nối qua
    /// lịch phân ca mới biết ngày làm việc, nhân viên và bộ phận.
    /// </summary>
    private async Task<ChamCongRow[]> LoadAttendanceAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var raw = await _context.ChamCongs
            .AsNoTracking()
            .Where(c => c.PhanCa != null
                        && c.PhanCa.NgayLamViec >= from
                        && c.PhanCa.NgayLamViec <= to
                        && (departmentId == null || (c.PhanCa.NhanVien != null && c.PhanCa.NhanVien.MaPhongBan == departmentId)))
            .Select(c => new
            {
                HoTen = c.PhanCa!.NhanVien != null ? c.PhanCa.NhanVien.Ho + " " + c.PhanCa.NhanVien.Ten : string.Empty,
                PhongBan = c.PhanCa!.NhanVien != null && c.PhanCa.NhanVien.PhongBan != null
                    ? c.PhanCa.NhanVien.PhongBan.TenPhongBan
                    : "(Chưa xếp phòng ban)",
                c.PhanCa!.NgayLamViec,
                c.TrangThai,
                c.PhutDiTre,
                c.PhutVeSom,
                c.GioTangCaOt
            })
            .ToArrayAsync();

        return [.. raw.Select(c => new ChamCongRow
        {
            HoTen = c.HoTen,
            PhongBan = c.PhongBan,
            Ngay = c.NgayLamViec,
            TrangThai = c.TrangThai,
            PhutDiTre = c.PhutDiTre,
            PhutVeSom = c.PhutVeSom,
            GioTangCaOt = c.GioTangCaOt
        })];
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TIỆN ÍCH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Nhãn phạm vi hiển thị trên thanh trạng thái.</summary>
    private static string ResolveScopeLabel(
        ReportPolicy.BaoCaoScope scope,
        int? departmentId,
        IReadOnlyDictionary<int, string> deptNames)
    {
        if (departmentId is int selected)
        {
            var name = deptNames.GetValueOrDefault(selected, "Không xác định");
            return scope.IsWholeHotel ? $"Phòng ban: {name}" : $"Bộ phận: {name}";
        }

        return "Toàn khách sạn";
    }

    /// <summary>Các năm có thể chọn: năm hiện tại lùi bốn năm, cộng thêm năm đang xem nếu nằm ngoài.</summary>
    private static IReadOnlyList<int> BuildYearOptions(int selectedYear, int currentYear)
    {
        var years = Enumerable.Range(currentYear - 4, 5).ToList();
        if (!years.Contains(selectedYear))
        {
            years.Add(selectedYear);
        }

        return [.. years.OrderByDescending(y => y)];
    }

    /// <summary>Chuẩn hóa tham số tab; giá trị lạ rơi về tab biến động nhân sự.</summary>
    public static string NormalizeTab(string? tab)
    {
        var value = (tab ?? string.Empty).Trim().ToLowerInvariant();
        return ValidTabs.Contains(value) ? value : "bien-dong";
    }

    /// <summary>Nhãn tiếng Việt của vai trò đang đăng nhập.</summary>
    public static string ResolveRoleLabel(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "ADMIN" or "QUANTRI" or "QUANTRIVIEN" => "Quản trị viên",
        "HR" or "NHANSU" or "QUANLYNHANSU" => "Nhân sự",
        "MANAGER" or "QUANLY" or "TRUONGPHONG" or "TRUONG_PHONG" => "Quản lý",
        "IT" or "CNTT" or "IT_SUPPORT" or "IT_MANAGER" => "IT",
        _ => "Người dùng"
    };

    /// <summary>Nhãn tiếng Việt của trạng thái làm việc.</summary>
    private static string ResolveStatusLabel(string? status) => status switch
    {
        "DANG_LAM" => "Đang làm việc",
        "NGHI_PHEP" => "Đang nghỉ phép",
        "THOI_VIEC" or "DA_THOI_VIEC" => "Đã thôi việc",
        _ => "Chưa xác định"
    };

    /// <summary>Nhãn tiếng Việt của giới tính.</summary>
    private static string ResolveGenderLabel(string? gender) => gender?.Trim().ToUpperInvariant() switch
    {
        "NAM" => "Nam",
        "NU" => "Nữ",
        _ => "Khác / Chưa cập nhật"
    };

    /// <summary>Tỷ lệ phần trăm làm tròn một chữ số, chặn chia cho 0.</summary>
    private static decimal PhanTram(int value, int total) =>
        total <= 0 ? 0m : Math.Round((decimal)value / total * 100m, 1);
}

/// <summary>Hồ sơ nhân sự đã phân giải tên phòng ban / chức vụ.</summary>
internal sealed class NhanSuRow
{
    public int MaNhanVien { get; set; }
    public string TenPhongBan { get; set; } = string.Empty;
    public string TrinhDo { get; set; } = string.Empty;
    public string GioiTinh { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public DateOnly NgayVaoLam { get; set; }
    public DateOnly? NgayThoiViec { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

/// <summary>Một lượt chấm công kèm nhân viên và bộ phận suy ra từ lịch phân ca.</summary>
internal sealed class ChamCongRow
{
    public string HoTen { get; set; } = string.Empty;
    public string PhongBan { get; set; } = string.Empty;
    public DateOnly Ngay { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public int PhutDiTre { get; set; }
    public int PhutVeSom { get; set; }
    public decimal GioTangCaOt { get; set; }
}

/// <summary>Một phiếu lương với các khoản cấu thành quỹ lương.</summary>
internal sealed class PhieuLuongRow
{
    public int Thang { get; set; }
    public string PhongBan { get; set; } = string.Empty;
    public decimal TienLuongCong { get; set; }
    public decimal TienTangCa { get; set; }
    public decimal TongPhuCap { get; set; }
    public decimal TongThuong { get; set; }
    public decimal ThucNhan { get; set; }
}

/// <summary>Một hợp đồng lao động dùng để suy định mức quỹ lương.</summary>
internal sealed class HopDongRow
{
    public DateOnly NgayBatDau { get; set; }
    public DateOnly? NgayKetThuc { get; set; }
    public decimal LuongCoBan { get; set; }
    public string PhongBan { get; set; } = string.Empty;
}

/// <summary>Một sự kiện/dự án kèm ngân sách phụ cấp.</summary>
internal sealed class SuKienRow
{
    public int MaDuAn { get; set; }
    public string TenDuAn { get; set; } = string.Empty;
    public string LoaiDuAn { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public DateTimeOffset NgayBatDau { get; set; }
    public DateTimeOffset NgayKetThuc { get; set; }
    public bool TinhTheoGio { get; set; }
    public decimal NganSachPhuCap { get; set; }
}

/// <summary>Một lượt phân công nhân sự vào sự kiện.</summary>
internal sealed class PhanCongRow
{
    public int MaDuAn { get; set; }
    public int MaNhanVien { get; set; }
    public int? MaPhongBan { get; set; }
    public string DanhGia { get; set; } = string.Empty;
    public decimal SoTienPhuCap { get; set; }
}
