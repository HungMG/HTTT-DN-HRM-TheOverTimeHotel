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
        var today = DateOnly.FromDateTime(HotelEventRules.Local(DateTimeOffset.UtcNow));
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
            "BaoCaoMetrics_V2", role ?? "-", year, month, quarter, departmentId, today.DayNumber,
            _cache.Get<string>(PayrollBudgetService.RevisionKey) ?? "initial");

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
                    p.TenPhongBan, p.MaPhongBan.ToString(CultureInfo.InvariantCulture), p.MaPhongBan == departmentId))]
                : [];

            model.NamOptions = BuildYearOptions(year, today.Year);

            var nhanSu = await LoadEmployeesAsync(departmentId, to, model.CanViewPayrollFund);

            model.BienDong = BuildBienDong(nhanSu, from, to, year, quarter, month);
            model.CoCau = BuildCoCau(nhanSu, to);
            model.CoCau.CoNghiPhepTrongKy = await ApprovedLeaveQuery(from, to, departmentId)
                .Select(d => d.MaNhanVien).Distinct().CountAsync();

            // 10.3 lấy mốc thâm niên tại ngày cuối kỳ báo cáo để số liệu quá khứ vẫn tái lập được.
            model.HocVanThamNien = BuildHocVanThamNien(nhanSu, to, model.CanViewPayrollFund);

            model.ChuyenCan = await BuildChuyenCanAsync(from, to, year, quarter, month, departmentId);

            if (model.CanViewPayrollFund)
            {
                model.QuyLuong = await BuildQuyLuongAsync(from, to, year, quarter, month, departmentId);
            }

            model.SuKien = await BuildSuKienAsync(from, to, scope, departmentId);

            var (yoyFrom, yoyTo, yoyLabel) = ReportPolicy.ResolveSamePeriodLastYearRange(year, quarter, month);
            model.CungKyLabel = yoyLabel;
            var yoyStaff = BuildBienDong(nhanSu, yoyFrom, yoyTo, year - 1, quarter, month);
            var yoyAttendance = await LoadAttendanceAsync(yoyFrom, yoyTo, departmentId);
            var yoyAssessed = yoyAttendance.Where(r => r.TrangThai is not ("NGHI_DA_DUYET" or "THIEU_QUET")).ToArray();
            var comparisons = new List<ReportComparisonItem> {
                new() {Label="Nhân sự còn làm việc cuối kỳ", Unit="người", Current=model.BienDong.CuoiKy, Previous=yoyStaff.CuoiKy},
                new() {Label="Tuyển mới", Unit="người", Current=model.BienDong.TuyenMoi, Previous=yoyStaff.TuyenMoi},
                new() {Label="Thôi việc", Unit="người", Current=model.BienDong.NghiViec, Previous=yoyStaff.NghiViec},
                new() {Label="Chuyên cần", Unit="%", Current=model.ChuyenCan.TyLeChuyenCan,
                    Previous=ReportPolicy.AttendanceRate(yoyAssessed.Count(r=>ReportPolicy.IsOnTime(r.TrangThai)),yoyAssessed.Length)}
            };
            if (model.CanViewPayrollFund)
            {
                var yoyGross = await _context.PhieuLuongs.AsNoTracking().Where(p=>p.KyLuong != null
                    && p.KyLuong.Nam==yoyFrom.Year && p.KyLuong.Thang>=yoyFrom.Month && p.KyLuong.Thang<=yoyTo.Month
                    && (departmentId==null || (p.NhanVien!=null && p.NhanVien.MaPhongBan==departmentId)))
                    .SumAsync(p=>(decimal?)(p.TienLuongCong+p.TienTangCa+p.TongPhuCap+p.TongThuong)) ?? 0m;
                comparisons.Add(new() {Label="Tổng lương đã tính (Gross)",Unit="đ",Current=model.QuyLuong.ThucChiGross,Previous=yoyGross});
            }
            model.SoSanhCungKy = comparisons;


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
    internal static BienDongNhanSuReport BuildBienDong(
        IReadOnlyList<NhanSuRow> nhanSu,
        DateOnly from,
        DateOnly to,
        int year,
        int? quarter = null,
        int? month = null)
    {
        var resigned = nhanSu.Where(n => ReportPolicy.IsResigned(n.TrangThai)).ToArray();
        var soThieuNgayNghi = resigned.Count(n => !n.NgayThoiViec.HasValue);

        // Quân số tại một mốc ngày:
        // Chỉ những người ĐÃ vào làm tính đến mốc ngày đó (n.NgayVaoLam <= asOf)
        // VÀ chưa thôi việc tính đến ngày đó.
        // Nhân viên thôi việc chỉ trừ khi họ đã vào làm và ngày nghỉ xác định được <= asOf.
        // Nhân viên thôi việc thiếu ngày nghỉ không được phép làm sai quân số quá khứ trước ngày vào làm.
        int HeadcountAt(DateOnly asOf)
        {
            var hired = nhanSu.Where(n => n.NgayVaoLam <= asOf).ToList();
            var resignedCount = hired.Count(n => ReportPolicy.IsResigned(n.TrangThai)
                && n.NgayThoiViec.HasValue && n.NgayThoiViec.Value <= asOf);
            return Math.Max(0, hired.Count - resignedCount);
        }

        var dauKy = Math.Max(0, HeadcountAt(from.AddDays(-1)));
        var cuoiKy = Math.Max(0, HeadcountAt(to));
        var tuyenMoi = nhanSu.Count(n => n.NgayVaoLam >= from && n.NgayVaoLam <= to);
        var nghiViec = resigned.Count(n => n.NgayThoiViec.HasValue
                                           && n.NgayThoiViec.Value >= from
                                           && n.NgayThoiViec.Value <= to);

        // So sánh với kỳ trước (MoM / QoQ / YoY)
        var (prevFrom, prevTo, prevLabel) = ReportPolicy.ResolvePreviousPeriodRange(year, quarter, month);
        var prevDauKy = Math.Max(0, HeadcountAt(prevFrom.AddDays(-1)));
        var prevCuoiKy = Math.Max(0, HeadcountAt(prevTo));
        var prevTuyenMoi = nhanSu.Count(n => n.NgayVaoLam >= prevFrom && n.NgayVaoLam <= prevTo);
        var prevNghiViec = resigned.Count(n => n.NgayThoiViec.HasValue
                                               && n.NgayThoiViec.Value >= prevFrom
                                               && n.NgayThoiViec.Value <= prevTo);
        var prevTurnover = ReportPolicy.TurnoverRate(ReportPolicy.AverageHeadcount(prevDauKy, prevCuoiKy), prevNghiViec);
        var currentTurnover = ReportPolicy.TurnoverRate(ReportPolicy.AverageHeadcount(dauKy, cuoiKy), nghiViec);

        var report = new BienDongNhanSuReport
        {
            DauKy = dauKy,
            CuoiKy = cuoiKy,
            TuyenMoi = tuyenMoi,
            NghiViec = nghiViec,
            BienDongRong = tuyenMoi - nghiViec,
            TyLeThoiViec = currentTurnover,
            SoHoSoThieuNgayThoiViec = soThieuNgayNghi,
            KyTruocLabel = prevLabel,
            TuyenMoiKyTruoc = prevTuyenMoi,
            TuyenMoiThayDoi = tuyenMoi - prevTuyenMoi,
            NghiViecKyTruoc = prevNghiViec,
            NghiViecThayDoi = nghiViec - prevNghiViec,
            CuoiKyKyTruoc = prevCuoiKy,
            CuoiKyThayDoi = cuoiKy - prevCuoiKy,
            TyLeThoiViecKyTruoc = prevTurnover,
            TyLeThoiViecThayDoi = currentTurnover - prevTurnover
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
            .Select(g =>
            {
                var hiredInDept = g.Where(n => n.NgayVaoLam <= to).ToList();
                var resignedInDept = hiredInDept.Count(n => ReportPolicy.IsResigned(n.TrangThai)
                    && n.NgayThoiViec.HasValue && n.NgayThoiViec.Value <= to);
                return new BienDongPhongBanItem
                {
                    PhongBan = g.Key,
                    QuanSo = Math.Max(0, hiredInDept.Count - resignedInDept),
                    TuyenMoi = g.Count(n => n.NgayVaoLam >= from && n.NgayVaoLam <= to),
                    NghiViec = g.Count(n => ReportPolicy.IsResigned(n.TrangThai)
                                            && n.NgayThoiViec.HasValue
                                            && n.NgayThoiViec.Value >= from
                                            && n.NgayThoiViec.Value <= to)
                };
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
    /// Giới hạn tại mốc thời gian asOf để đảm bảo tính chuẩn xác lịch sử (không tính người vào làm tương lai).
    /// </summary>
    internal static CoCauNhanLucReport BuildCoCau(IReadOnlyList<NhanSuRow> nhanSu, DateOnly asOf)
    {
        // Chỉ xét những người đã vào làm tính đến ngày mốc asOf (loại bỏ nhân sự tương lai)
        var cohort = nhanSu.Where(n => n.NgayVaoLam <= asOf).ToList();
        var total = cohort.Count;

        // Trạng thái chuẩn hóa tại mốc asOf
        string StatusAtAsOf(NhanSuRow n)
        {
            if (ReportPolicy.IsResigned(n.TrangThai))
            {
                if (n.NgayThoiViec.HasValue && n.NgayThoiViec.Value > asOf)
                {
                    return "DANG_LAM";
                }
                return "THOI_VIEC";
            }
            return n.TrangThai;
        }

        var dangLam = cohort.Count(n => StatusAtAsOf(n) == "DANG_LAM");
        var nghiPhep = cohort.Count(n => StatusAtAsOf(n) == "NGHI_PHEP");
        var daThoiViec = cohort.Count(n => ReportPolicy.IsResigned(StatusAtAsOf(n)));

        // Xử lý chức vụ: Top 7 + gom phần còn lại vào "Chức vụ khác" để bảo toàn 100% mẫu số
        var positionGroups = cohort
            .GroupBy(n => string.IsNullOrWhiteSpace(n.TenChucVu) ? "Chưa xếp chức vụ" : n.TenChucVu)
            .Select(g => new { Nhan = g.Key, GiaTri = g.Count() })
            .OrderByDescending(x => x.GiaTri)
            .ThenBy(x => x.Nhan, StringComparer.CurrentCulture)
            .ToList();

        List<PhanBoItem> theoChucVu;
        if (positionGroups.Count <= 8)
        {
            theoChucVu = positionGroups.Select(p => new PhanBoItem
            {
                Nhan = p.Nhan,
                GiaTri = p.GiaTri,
                TyLe = PhanTram(p.GiaTri, total)
            }).ToList();
        }
        else
        {
            var top7 = positionGroups.Take(7).Select(p => new PhanBoItem
            {
                Nhan = p.Nhan,
                GiaTri = p.GiaTri,
                TyLe = PhanTram(p.GiaTri, total)
            }).ToList();

            var remaining = positionGroups.Skip(7).Sum(p => p.GiaTri);
            top7.Add(new PhanBoItem
            {
                Nhan = "Chức vụ khác",
                GiaTri = remaining,
                TyLe = PhanTram(remaining, total)
            });
            theoChucVu = top7;
        }

        return new CoCauNhanLucReport
        {
            TongNhanSu = total,
            DangLam = dangLam,
            NghiPhep = nghiPhep,
            DaThoiViec = daThoiViec,

            TheoPhongBan = [.. cohort
                .GroupBy(n => n.TenPhongBan)
                .Select(g => new PhanBoItem
                {
                    Nhan = g.Key,
                    GiaTri = g.Count(),
                    TyLe = PhanTram(g.Count(), total)
                })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoTrangThai = [.. cohort
                .GroupBy(n => ResolveStatusLabel(StatusAtAsOf(n)))
                .Select(g => new PhanBoItem { Nhan = g.Key, GiaTri = g.Count(), TyLe = PhanTram(g.Count(), total) })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoGioiTinh = [.. cohort
                .GroupBy(n => ResolveGenderLabel(n.GioiTinh))
                .Select(g => new PhanBoItem { Nhan = g.Key, GiaTri = g.Count(), TyLe = PhanTram(g.Count(), total) })
                .OrderByDescending(item => item.GiaTri)
                .ThenBy(item => item.Nhan, StringComparer.CurrentCulture)],

            TheoChucVu = theoChucVu
        };
    }

    // ── 10.3 Học vấn & thâm niên ──────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo học vấn, thâm niên &amp; mức lương nhân sự.
    /// Giới hạn mốc thâm niên tại ngày cuối kỳ báo cáo để số liệu quá khứ vẫn tái lập được,
    /// đồng thời thâm niên người đã thôi việc dừng tại ngày thôi việc.
    /// </summary>
    internal static HocVanThamNienReport BuildHocVanThamNien(
        IReadOnlyList<NhanSuRow> nhanSu,
        DateOnly asOf,
        bool canViewSalary = false)
    {
        // Chỉ xét những người đã vào làm trước hoặc đúng mốc asOf
        var cohort = nhanSu.Where(n => ReportPolicy.IsEmployedAt(n.NgayVaoLam, n.TrangThai, n.NgayThoiViec, asOf)).ToList();
        var total = cohort.Count;

        var educationCounts = cohort
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

        var tenureCounts = cohort
            .GroupBy(n => ReportPolicy.TenureBucketLabel(ReportPolicy.TenureMonths(n.NgayVaoLam, asOf, n.NgayThoiViec)))
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
            : Math.Round(cohort.Average(n => (decimal)ReportPolicy.TenureMonths(n.NgayVaoLam, asOf, n.NgayThoiViec)), 1);

        // ── Thống kê phân bổ mức lương (chỉ hiển thị với vai trò có quyền xem lương) ──
        var theoMucLuong = new List<PhanBoItem>();
        decimal luongTb = 0m;
        decimal luongMax = 0m;
        decimal luongMin = 0m;
        var nhanSuCoLuong = cohort.Where(n => n.LuongCoBan > 0m).ToList();

        if (canViewSalary && nhanSuCoLuong.Count > 0)
        {
            luongTb = Math.Round(nhanSuCoLuong.Average(n => n.LuongCoBan), 0);
            luongMax = nhanSuCoLuong.Max(n => n.LuongCoBan);
            luongMin = nhanSuCoLuong.Min(n => n.LuongCoBan);

            var salaryCounts = nhanSuCoLuong
                .GroupBy(n => ReportPolicy.SalaryBucketLabel(n.LuongCoBan))
                .ToDictionary(g => g.Key, g => g.Count());

            theoMucLuong = ReportPolicy.SalaryBucketOrder
                .Select(bucket => new PhanBoItem
                {
                    Nhan = bucket,
                    GiaTri = salaryCounts.GetValueOrDefault(bucket),
                    TyLe = PhanTram(salaryCounts.GetValueOrDefault(bucket), nhanSuCoLuong.Count)
                })
                .ToList();
        }

        return new HocVanThamNienReport
        {
            TheoTrinhDo = theoTrinhDo,
            TheoThamNien = theoThamNien,
            SoDaiHocTroLen = higherCount,
            TyLeDaiHocTroLen = ReportPolicy.HigherEducationRate(higherCount, total),
            ThamNienTrungBinh = Math.Round(averageMonths / 12m, 1),

            CanViewSalaryInfo = canViewSalary,
            TheoMucLuong = theoMucLuong,
            LuongCoBanTrungBinh = luongTb,
            LuongCoBanCaoNhat = luongMax,
            LuongCoBanThapNhat = luongMin,
            SoNhanSuCoLuong = canViewSalary ? nhanSuCoLuong.Count : 0,
            SoNhanSuThieuLuong = canViewSalary ? total - nhanSuCoLuong.Count : 0
        };
    }

    // ── 10.4 Đối soát quỹ lương vs định mức ────────────────────────────────

    /// <summary>
    /// Dựng báo cáo đối soát quỹ lương.
    /// Tính định mức tháng nhất quán giữa toàn khách sạn, chuỗi tháng và từng bộ phận.
    /// </summary>
    private async Task<QuyLuongNganSachReport> BuildQuyLuongAsync(
        DateOnly from,
        DateOnly to,
        int year,
        int? quarter,
        int? month,
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
                ThucNhan = p.ThucNhan,
                TrangThai = p.TrangThai
            })
            .ToArrayAsync();

        var contracts = await _context.HopDongs
            .AsNoTracking()
            .Include(h => h.PhuLucLuongs)
            .Include(h => h.NhanVien)
            .Where(ReportPolicy.ReferenceContractFilter)
            .Where(h => h.NgayBatDau <= to && (h.NgayKetThuc == null || h.NgayKetThuc >= from))
            .ToArrayAsync();
        var departments = await _context.PhongBans.AsNoTracking()
            .ToDictionaryAsync(d => d.MaPhongBan, d => d.TenPhongBan);
        var missingReferenceEmployees = 0;
        var referenceSegments = contracts.GroupBy(h => h.MaNhanVien)
            .Where(g => departmentId == null || g.Any(h =>
                (h.MaPhongBanKyKet ?? h.NhanVien?.MaPhongBan) == departmentId
                || h.PhuLucLuongs.Any(a => a.NgayHieuLuc <= to && a.MaPhongBanMoi == departmentId)))
            .SelectMany(g =>
            {
                var segments = SalarySegmentsForReport(g, from, to);
                if (segments == null) missingReferenceEmployees++;
                return segments ?? [];
            })
            .Select(segment => new
                {
                    Segment = segment,
                    DepartmentId = segment.Salary.DepartmentId ?? segment.Salary.Contract.NhanVien?.MaPhongBan
                })
            .Where(x => departmentId == null || x.DepartmentId == departmentId)
            .Select(x => new
            {
                x.Segment,
                Department = x.DepartmentId is { } id && departments.TryGetValue(id, out var name)
                    ? name : "(Chưa xếp phòng ban)"
            }).ToArray();

        decimal ReferenceBetween(SalarySegment segment, DateOnly start, DateOnly end)
        {
            var clippedFrom = segment.From > start ? segment.From : start;
            var clippedTo = segment.To < end ? segment.To : end;
            return clippedFrom <= clippedTo
                ? (segment with { From = clippedFrom, To = clippedTo }).CalendarProratedAmount : 0m;
        }
        decimal BudgetBetween(DateOnly start, DateOnly end) => referenceSegments
            .Sum(x => ReferenceBetween(x.Segment, start, end));
        decimal DeptBudgetBetween(string deptName, DateOnly start, DateOnly end) => referenceSegments
            .Where(x => string.Equals(x.Department, deptName, StringComparison.CurrentCulture))
            .Sum(x => ReferenceBetween(x.Segment, start, end));

        decimal DeptBudgetInPeriod(string deptName)
        {
            decimal b = 0m;
            for (var m = from.Month; m <= to.Month; m++)
            {
                b += DeptBudgetBetween(deptName, new DateOnly(year, m, 1), new DateOnly(year, m, DateTime.DaysInMonth(year, m)));
            }
            return b;
        }

        // Chỉ nhận phiếu lương thuộc đúng kỳ báo cáo (cùng năm, tháng nằm trong khoảng).
        var trongKy = phieuLuong
            .Where(p => p.Thang >= from.Month && p.Thang <= to.Month)
            .ToArray();

        // Quỹ lương gộp = lương công + tăng ca + phụ cấp + thưởng, tức mọi khoản chi từ quỹ lương.
        static decimal Gross(PhieuLuongRow p) => p.TienLuongCong + p.TienTangCa + p.TongPhuCap + p.TongThuong;

        var gross = trongKy.Sum(Gross);
        var net = trongKy.Sum(p => p.ThucNhan);

        // Định mức cả kỳ = cộng định mức từng tháng
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

        // Phân rã theo phòng ban: kết hợp cả phòng có phiếu lương và phòng có hợp đồng định mức
        var allDeptNames = trongKy.Select(p => p.PhongBan)
            .Union(referenceSegments.Select(x => x.Department))
            .Distinct()
            .ToList();

        var theoPhongBan = allDeptNames
            .Select(dept =>
            {
                var slips = trongKy.Where(p => string.Equals(p.PhongBan, dept, StringComparison.CurrentCulture)).ToArray();
                var thucChi = slips.Sum(Gross);
                var nganSach = DeptBudgetInPeriod(dept);
                return new QuyLuongPhongBanItem
                {
                    PhongBan = dept,
                    ThucChi = thucChi,
                    NganSach = nganSach,
                    ChenhLech = thucChi - nganSach
                };
            })
            .Where(row => row.ThucChi > 0m || row.NganSach > 0m)
            .OrderByDescending(row => row.ThucChi)
            .ThenBy(row => row.PhongBan, StringComparer.CurrentCulture)
            .ToList();

        // Phân loại trạng thái phiếu lương
        var soDaDuyet = trongKy.Count(p => p.TrangThai is "DA_DUYET" or "KHOA" or "DA_THANH_TOAN");
        var soNhap = trongKy.Count(p => p.TrangThai is "NHAP" or "CHO_DUYET");
        var tienDaDuyet = trongKy.Where(p => p.TrangThai is "DA_DUYET" or "KHOA" or "DA_THANH_TOAN").Sum(Gross);
        var tienNhap = trongKy.Where(p => p.TrangThai is "NHAP" or "CHO_DUYET").Sum(Gross);

        // So sánh với kỳ trước
        var (prevFrom, prevTo, prevLabel) = ReportPolicy.ResolvePreviousPeriodRange(year, quarter, month);
        var prevSlips = await _context.PhieuLuongs
            .AsNoTracking()
            .Where(p => p.KyLuong != null
                        && p.KyLuong.Nam == prevFrom.Year
                        && p.KyLuong.Thang >= prevFrom.Month && p.KyLuong.Thang <= prevTo.Month
                        && (departmentId == null || (p.NhanVien != null && p.NhanVien.MaPhongBan == departmentId)))
            .Select(p => p.TienLuongCong + p.TienTangCa + p.TongPhuCap + p.TongThuong)
            .ToArrayAsync();

        var approvedRows = await PayrollBudgetService.ReadAsync(_context, year);
        var departmentIds = departmentId.HasValue ? new[] {departmentId.Value}
            : await _context.PhongBans.AsNoTracking().Select(p=>p.MaPhongBan).ToArrayAsync();
        var coverage = PayrollBudgetPolicy.Summarize(approvedRows ?? [], year,
            Enumerable.Range(from.Month, to.Month-from.Month+1).ToArray(), departmentIds);
        decimal? approvedBudget = approvedRows != null && coverage.Complete ? coverage.Total : null;

        var prevGross = prevSlips.Sum();
        var change = ReportPolicy.CalculateChange(gross, prevGross);

        return new QuyLuongNganSachReport
        {
            ThucChi = net,
            ThucChiGross = gross,
            NganSachThamChieu = budget,
            ChenhLech = variance.Variance,
            PhanTramGiaiNgan = variance.Rate,
            VuotNguong = variance.Exceeded,
            SoThangTrongKy = soThang,
            SoNhanSuThieuCanCuLuong = missingReferenceEmployees,
            SoHopDong = referenceSegments.Select(x => x.Segment.Salary.Contract.MaHopDong).Distinct().Count(),
            SoPhieuLuong = trongKy.Length,
            SoPhieuDaDuyet = soDaDuyet,
            SoPhieuNhap = soNhap,
            ThucChiGrossDaDuyet = tienDaDuyet,
            ThucChiGrossNhap = tienNhap,
            NganSachDuocDuyet = approvedBudget,
            TyLeGiaiNganNganSachDuyet = approvedBudget > 0m ? Math.Round(tienDaDuyet / approvedBudget.Value * 100m, 1) : null,
            ChenhLechNganSachDuyet = approvedBudget.HasValue ? tienDaDuyet-approvedBudget.Value : null,
            TrangThaiNganSachDuyet = approvedRows == null ? "Chưa triển khai bảng ngân sách; cần DBA duyệt migration."
                : !coverage.Complete ? $"Chưa đủ ngân sách được duyệt: thiếu {coverage.MissingSlots}/{coverage.ExpectedSlots} ô tháng/phòng ban."
                : "Đã duyệt đủ ngân sách tháng/phòng ban; so với lương đã duyệt, không phải tiền đã thanh toán.",
            KyTruocLabel = prevLabel,
            ThucChiGrossKyTruoc = prevGross,
            ThucChiGrossThayDoi = change.Absolute,
            ThucChiGrossThayDoiPct = change.Percent,
            Series = series,
            TheoPhongBan = theoPhongBan,
            GhiChuPhuongPhap =
                (missingReferenceEmployees > 0
                    ? $"Có {missingReferenceEmployees} hồ sơ lương chưa hợp lệ được loại khỏi căn cứ tham chiếu. Định mức chưa đầy đủ; chênh lệch và tỷ lệ chỉ để tham khảo, chưa đủ để kết luận cho toàn phạm vi. " : "")
                + "Định mức tham chiếu dùng lương hợp đồng/phụ lục có hiệu lực từng giai đoạn, phân bổ theo số ngày lịch được bao phủ / số ngày lịch trong tháng, cộng từng tháng. "
                + "Đây là số tham chiếu, không phải ngân sách đã duyệt hay lương công thực tế (lương công dùng ngày công /26). Hợp đồng chấm dứt thiếu ngày kết thúc bị loại; hợp đồng trùng hiệu lực cần HR đối soát."
        };
    }

    // ── 10.5 Chuyên cần & vi phạm ──────────────────────────────────────────

    /// <summary>
    /// Dựng báo cáo chuyên cần &amp; vi phạm.
    /// Đối chiếu cả lịch phân ca kết thúc chưa chấm công để không bỏ sót vắng mặt.
    /// </summary>
    private async Task<ChuyenCanViPhamReport> BuildChuyenCanAsync(
        DateOnly from,
        DateOnly to,
        int year,
        int? quarter,
        int? month,
        int? departmentId)
    {
        var rows = await LoadAttendanceAsync(from, to, departmentId);
        var (prevFrom, prevTo, prevLabel) = ReportPolicy.ResolvePreviousPeriodRange(year, quarter, month);
        var prevRows = await LoadAttendanceAsync(prevFrom, prevTo, departmentId);
        return BuildAttendanceReport(rows, prevRows, from, to, prevLabel);
    }

    internal static ChuyenCanViPhamReport BuildAttendanceReport(ChamCongRow[] rows, ChamCongRow[] prevRows,
        DateOnly from, DateOnly to, string prevLabel)
    {
        var assessed = rows.Where(r => r.TrangThai is not ("NGHI_DA_DUYET" or "THIEU_QUET")).ToArray();
        var total = assessed.Length;
        var dungGio = rows.Count(r => ReportPolicy.IsOnTime(r.TrangThai));

        var series = new List<ChuyenCanSeriesItem>(to.Month - from.Month + 1);
        for (var m = from.Month; m <= to.Month; m++)
        {
            var cuaThang = assessed.Where(r => r.Ngay.Month == m).ToArray();
            var dungGioThang = cuaThang.Count(r => ReportPolicy.IsOnTime(r.TrangThai));

            series.Add(new ChuyenCanSeriesItem
            {
                Nhan = $"T{m:00}",
                TyLeChuyenCan = ReportPolicy.AttendanceRate(dungGioThang, cuaThang.Length),
                ViPham = cuaThang.Count(r => ReportPolicy.IsViolation(r.TrangThai))
            });
        }


        var prevTotal = prevRows.Count(r => r.TrangThai is not ("NGHI_DA_DUYET" or "THIEU_QUET"));
        var prevDungGio = prevRows.Count(r => ReportPolicy.IsOnTime(r.TrangThai));
        var prevRate = ReportPolicy.AttendanceRate(prevDungGio, prevTotal);
        var currentRate = ReportPolicy.AttendanceRate(dungGio, total);
        var prevViPham = prevRows.Count(r => ReportPolicy.IsViolation(r.TrangThai));
        var currentViPham = rows.Count(r => ReportPolicy.IsViolation(r.TrangThai));

        return new ChuyenCanViPhamReport
        {
            TongBanGhi = total,
            SoCaNghiDuocDuyet = rows.Count(r => r.TrangThai == "NGHI_DA_DUYET"),
            SoCaThieuQuet = rows.Count(r => r.TrangThai == "THIEU_QUET"),
            DungGio = dungGio,
            DiTre = rows.Count(r => r.TrangThai == "DI_TRE"),
            VeSom = rows.Count(r => r.TrangThai == "VE_SOM"),
            TreVaSom = rows.Count(r => r.TrangThai == "TRE_VA_SOM"),
            Vang = rows.Count(r => ReportPolicy.IsAbsent(r.TrangThai)),
            TongPhutTre = rows.Sum(r => r.PhutDiTre),
            TongPhutVeSom = rows.Sum(r => r.PhutVeSom),
            TongGioOt = rows.Sum(r => r.GioTangCaOt),
            TyLeChuyenCan = currentRate,
            KyTruocLabel = prevLabel,
            TyLeChuyenCanKyTruoc = prevRate,
            ChuyenCanThayDoiDiemPhanTram = currentRate - prevRate,
            ViPhamKyTruoc = prevViPham,
            ViPhamThayDoi = currentViPham - prevViPham,
            Series = series,

            // Bảng xếp hạng kỷ luật: nhóm theo định danh nhân viên (tránh gộp trùng tên)
            XepHangViPham = [.. rows
                .Where(r => ReportPolicy.IsViolation(r.TrangThai))
                .GroupBy(r => new { r.MaNhanVien, r.HoTen, r.PhongBan })
                .Select(g => new ViPhamNhanSuItem
                {
                    MaNhanVien = g.Key.MaNhanVien,
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

            TheoPhongBan = [.. assessed
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
    /// Dựng báo cáo hiệu quả nhân sự sự kiện.
    /// Lọc sự kiện diễn ra trong kỳ và áp dụng nhất quán phạm vi phòng ban/bộ phận.
    /// </summary>
    private async Task<HieuQuaSuKienReport> BuildSuKienAsync(
        DateOnly from,
        DateOnly to,
        ReportPolicy.BaoCaoScope scope,
        int? departmentId)
    {
        var fromUtc = HotelEventRules.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtcExclusive = HotelEventRules.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Sự kiện đang hoặc đã diễn ra giao với kỳ báo cáo
        var suKien = await _context.DuAns
            .AsNoTracking()
            .Where(d => d.NgayBatDau < toUtcExclusive && d.NgayKetThuc > fromUtc)
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
                    SoTienPhuCap = p.SoTienPhuCap,
                    TrangThaiThamGia = p.TrangThaiThamGia
                })
                .ToArrayAsync();

        // Loại bỏ các phân công đã bị hủy
        phanCong = [.. phanCong.Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY"))];

        var money = await LoadEventMoneyAsync(from, to, departmentId);

        // Lọc theo phòng ban: áp dụng cho cả Manager và Admin/HR khi chọn phòng ban
        if (departmentId is int dept)
        {
            var visibleIds = phanCong
                .Where(p => p.MaPhongBan == dept)
                .Select(p => p.MaDuAn)
                .ToHashSet();

            visibleIds.UnionWith(money.Select(m => m.EventId));
            suKien = [.. suKien.Where(d => visibleIds.Contains(d.MaDuAn))];
            phanCong = [.. phanCong.Where(p => p.MaPhongBan == dept)];
        }

        var amounts = money.GroupBy(m => m.EventId).ToDictionary(g => g.Key, g => g.Sum(m => m.Detail.SoTien ?? 0m));
        decimal EventAmount(SuKienRow item) => amounts.GetValueOrDefault(item.MaDuAn);

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
            PhamViBoPhan = departmentId.HasValue,
            GhiChuPhamVi = departmentId.HasValue
                ? "Ngân sách là số chung toàn sự kiện; nhân sự và tiền là phần của bộ phận. Tiền theo giờ lọc theo tháng phát sinh; tiền cũ theo tháng kết thúc phân công, chưa đồng nghĩa đã thanh toán."
                : "Tiền theo giờ lọc theo tháng phát sinh; tiền cũ theo tháng kết thúc phân công. Kỳ chi trả được ghi tại phần đối chứng, chưa đồng nghĩa đã thanh toán.",

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
                        TuNgay = DateOnly.FromDateTime(HotelEventRules.Local(d.NgayBatDau)),
                        DenNgay = DateOnly.FromDateTime(HotelEventRules.Local(d.NgayKetThuc)),
                        SoNhanSu = cuaSuKien.Select(p => p.MaNhanVien).Distinct().Count(),
                        NganSach = d.NganSachPhuCap,
                        NganSachToanSuKien = departmentId.HasValue,
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
    /// Giữ căn cứ lương chưa hợp lệ tách biệt với định mức bằng 0 trong báo cáo.
    /// </summary>
    private IReadOnlyList<SalarySegment>? SalarySegmentsForReport(
        IEnumerable<OvertimeHotel.HRM.Core.Models.HopDong> contracts, DateOnly from, DateOnly to)
    {
        try
        {
            return EffectiveSalaryResolver.BuildSegments(contracts, from, to);
        }
        catch (InvalidOperationException ex)
        {
            // Unknown reference is kept distinct from a valid zero amount; payroll remains strict.
            _logger.LogWarning(ex, "Căn cứ lương chưa hợp lệ trong báo cáo {From}–{To}; cần HR đối soát.", from, to);
            return null;
        }
    }

    /// <summary>Nạp hồ sơ trong phạm vi cho phép; chỉ đọc căn cứ lương khi được phân quyền.</summary>
    private async Task<NhanSuRow[]> LoadEmployeesAsync(int? departmentId, DateOnly asOf, bool includeSalary)
    {
        var raw = await _context.NhanViens
            .AsNoTracking()
            .Where(n => departmentId == null || n.MaPhongBan == departmentId)
            .Include(n => n.PhongBan).Include(n => n.ChucVu)
            .Include(n => n.HopDongs).ThenInclude(h => h.PhuLucLuongs)
            .ToArrayAsync();

        return [.. raw.Select(n => new NhanSuRow
        {
            MaNhanVien = n.MaNhanVien,
            HoTen = n.HoTen,
            TenPhongBan = n.PhongBan?.TenPhongBan ?? "(Chưa xếp phòng ban)",
            TrinhDo = n.TrinhDo,
            GioiTinh = n.GioiTinh ?? string.Empty,
            TenChucVu = n.ChucVu?.TenChucVu ?? "Chưa xếp chức vụ",
            NgayVaoLam = n.NgayVaoLam,
            NgayThoiViec = n.NgayThoiViec,
            TrangThai = n.TrangThai,
            LuongCoBan = includeSalary
                ? SalarySegmentsForReport(n.HopDongs, asOf, asOf)?.LastOrDefault()?.Salary.MonthlySalary ?? 0m
                : 0m
        })];
    }

    /// <summary>
    /// Nạp nhật ký chấm công trong kỳ. Bảng chấm công chỉ có mã phân ca nên phải nối qua
    /// lịch phân ca mới biết ngày làm việc, nhân viên và bộ phận.
    /// </summary>
    private async Task<ChamCongRow[]> LoadAttendanceAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var now = DateTimeOffset.UtcNow;
        var shifts = await _context.PhanCas.AsNoTracking()
            .Where(p => p.DaCongBo && p.TrangThai != "DA_HUY" && p.KetThucDuKien <= now
                && p.NgayLamViec >= from && p.NgayLamViec <= to
                && (departmentId == null || (p.NhanVien != null && p.NhanVien.MaPhongBan == departmentId)))
            .Select(p => new
            {
                p.MaNhanVien, p.NgayLamViec, p.DaCongBo, p.TrangThai, p.KetThucDuKien,
                HoTen = p.NhanVien != null ? p.NhanVien.Ho + " " + p.NhanVien.Ten : "",
                PhongBan = p.NhanVien != null && p.NhanVien.PhongBan != null ? p.NhanVien.PhongBan.TenPhongBan : "(Chưa xếp phòng ban)",
                Punch = p.ChamCong,
                ApprovedLeave = _context.DonTus.Any(d => d.MaNhanVien == p.MaNhanVien
                    && d.TrangThai == "DA_DUYET" && d.LoaiDon != null && d.LoaiDon.LaNghiPhep
                    && d.TuNgay <= p.NgayLamViec && d.DenNgay >= p.NgayLamViec)
            }).ToArrayAsync();
        return shifts.Select(p => new ChamCongRow
        {
            MaNhanVien = p.MaNhanVien, HoTen = p.HoTen, PhongBan = p.PhongBan, Ngay = p.NgayLamViec,
            TrangThai = ReportPolicy.ResolveAttendanceStatus(p.DaCongBo, p.TrangThai, p.KetThucDuKien,
                now, p.ApprovedLeave, p.Punch)!,
            PhutDiTre = p.Punch?.PhutDiTre ?? 0, PhutVeSom = p.Punch?.PhutVeSom ?? 0,
            GioTangCaOt = p.Punch?.GioTangCaOt ?? 0
        }).Where(r => r.TrangThai != null).ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CHI TIẾT ĐỐI CHỨNG DỮ LIỆU NGUỒN (DRILL-DOWN)
    // ═══════════════════════════════════════════════════════════════════════

    private IQueryable<OvertimeHotel.HRM.Core.Models.DonTu> ApprovedLeaveQuery(DateOnly from, DateOnly to, int? departmentId) =>
        _context.DonTus.AsNoTracking().Where(ReportPolicy.ApprovedLeaveFilter(from, to))
            .Where(d => departmentId == null || (d.NhanVien != null && d.NhanVien.MaPhongBan == departmentId));

    /// <summary>
    /// Nạp dữ liệu nguồn phục vụ đối chứng từ chỉ tiêu KPI (Task 9).
    /// </summary>
    public async Task<BaoCaoChiTietViewModel> BuildChiTietAsync(
        ReportPolicy.BaoCaoScope scope,
        string? role,
        string type,
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan)
    {
        var today = DateOnly.FromDateTime(HotelEventRules.Local(DateTimeOffset.UtcNow));
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

        int? departmentId = scope.IsWholeHotel ? (phongBan is > 0 ? phongBan : null) : scope.DepartmentId;

        var result = new BaoCaoChiTietViewModel
        {
            LoaiChiTiet = type,
            NhanKy = periodLabel,
            PhamViLabel = departmentId.HasValue ? $"Phòng ban #{departmentId.Value}" : "Toàn khách sạn"
        };

        switch (type.ToLowerInvariant())
        {
            case "cuoi-ky":
                result.TieuDe = "Danh sách nhân sự cuối kỳ";
                var finalEmployees = await _context.NhanViens.AsNoTracking()
                    .Where(n => n.NgayVaoLam <= to && (departmentId == null || n.MaPhongBan == departmentId)
                        && !((n.TrangThai == "THOI_VIEC" || n.TrangThai == "DA_THOI_VIEC") && n.NgayThoiViec.HasValue && n.NgayThoiViec <= to))
                    .OrderBy(n => n.Ten)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien, TenDoiTuong = n.Ho + " " + n.Ten,
                        BoPhan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp",
                        NgayThang = n.NgayVaoLam,
                        TrangThai = (n.TrangThai == "THOI_VIEC" || n.TrangThai == "DA_THOI_VIEC") && !n.NgayThoiViec.HasValue ? "Thiếu ngày nghỉ việc" : "Còn làm cuối kỳ"
                    }).ToArrayAsync();
                result.DanhSach = finalEmployees;
                break;
            case "thong-tin-nhan-su":
            case "thong-tin-tat-ca":
                var includeFormer = type.Equals("thong-tin-tat-ca", StringComparison.OrdinalIgnoreCase);
                result.TieuDe = includeFormer ? "Thông tin nhân sự (gồm người đã nghỉ việc)" : "Thông tin nhân sự còn làm việc cuối kỳ";
                var personnel = await LoadEmployeesAsync(departmentId, to, ReportPolicy.CanViewPayrollFundReport(role));
                var canSeeSalary = ReportPolicy.CanViewPayrollFundReport(role);
                result.DanhSach = personnel
                    .Where(n => includeFormer ? n.NgayVaoLam <= to : ReportPolicy.IsEmployedAt(n.NgayVaoLam, n.TrangThai, n.NgayThoiViec, to))
                    .OrderBy(n => n.TenPhongBan).ThenBy(n => n.HoTen)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien,
                        TenDoiTuong = n.HoTen,
                        BoPhan = n.TenPhongBan,
                        ChucVuHoacLoai = n.TenChucVu,
                        NgayThang = n.NgayVaoLam,
                        SoTien = canSeeSalary && n.LuongCoBan > 0 ? n.LuongCoBan : null,
                        TrangThai = ReportPolicy.IsEmployedAt(n.NgayVaoLam, n.TrangThai, n.NgayThoiViec, to) ? "Còn làm việc cuối kỳ" : "Đã nghỉ việc / chưa rõ ngày",
                        GhiChu = $"Trình độ: {n.TrinhDo} • Thâm niên: {ReportPolicy.TenureMonths(n.NgayVaoLam, to, n.NgayThoiViec)} tháng"
                            + (canSeeSalary && n.LuongCoBan <= 0 ? " • Chưa có lương cơ bản phù hợp" : "")
                    }).ToArray();
                break;
            case "nghi-phep-trong-ky":
                result.TieuDe = "Nhân viên có nghỉ phép trong kỳ (đơn đã duyệt)";
                var leaveRows = await ApprovedLeaveQuery(from, to, departmentId)
                    .Select(d => new
                    {
                        d.MaNhanVien, d.TuNgay, d.DenNgay,
                        HoTen = d.NhanVien != null ? d.NhanVien.Ho + " " + d.NhanVien.Ten : "",
                        PhongBan = d.NhanVien != null && d.NhanVien.PhongBan != null ? d.NhanVien.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVu = d.NhanVien != null && d.NhanVien.ChucVu != null ? d.NhanVien.ChucVu.TenChucVu : "Chưa xếp"
                    }).ToArrayAsync();
                result.DanhSach = leaveRows.GroupBy(d => d.MaNhanVien).Select(g =>
                {
                    var employee = g.First();
                    return new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + employee.MaNhanVien,
                        TenDoiTuong = employee.HoTen,
                        BoPhan = employee.PhongBan,
                        ChucVuHoacLoai = employee.ChucVu,
                        TrangThai = "Đã duyệt",
                        GhiChu = string.Join("; ", g.OrderBy(d => d.TuNgay).Select(d => $"{d.TuNgay:dd/MM/yyyy} – {d.DenNgay:dd/MM/yyyy}"))
                    };
                }).OrderBy(d => d.BoPhan).ThenBy(d => d.TenDoiTuong).ToArray();
                break;

            case "tuyen-moi":
                result.TieuDe = "Danh sách nhân sự tuyển mới trong kỳ";
                var tuyen = await _context.NhanViens
                    .AsNoTracking()
                    .Where(n => n.NgayVaoLam >= from && n.NgayVaoLam <= to
                                && (departmentId == null || n.MaPhongBan == departmentId))
                    .OrderBy(n => n.NgayVaoLam)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien.ToString(),
                        TenDoiTuong = n.Ho + " " + n.Ten,
                        BoPhan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp",
                        NgayThang = n.NgayVaoLam,
                        TrangThai = n.TrangThai
                    })
                    .ToArrayAsync();
                result.DanhSach = tuyen;
                break;

            case "dang-lam":
                result.TieuDe = "Danh sách nhân sự đang làm việc (Tính đến " + to.ToString("dd/MM/yyyy") + ")";
                var dangLam = await _context.NhanViens
                    .AsNoTracking()
                    .Where(n => n.NgayVaoLam <= to
                                && (n.TrangThai == "DANG_LAM" || ((n.TrangThai == "THOI_VIEC" || n.TrangThai == "DA_THOI_VIEC") && n.NgayThoiViec.HasValue && n.NgayThoiViec.Value > to))
                                && (departmentId == null || n.MaPhongBan == departmentId))
                    .OrderBy(n => n.PhongBan != null ? n.PhongBan.TenPhongBan : "")
                    .ThenBy(n => n.Ten)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien.ToString(),
                        TenDoiTuong = n.Ho + " " + n.Ten,
                        BoPhan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp",
                        NgayThang = n.NgayVaoLam,
                        TrangThai = "Đang làm việc",
                        GhiChu = $"Vào làm: {n.NgayVaoLam:dd/MM/yyyy} • Trình độ: {n.TrinhDo}"
                    })
                    .ToArrayAsync();
                result.DanhSach = dangLam;
                break;

            case "nghi-phep":
                result.TieuDe = "Danh sách nhân sự đang nghỉ phép (Tính đến " + to.ToString("dd/MM/yyyy") + ")";
                var nghiPhep = await _context.NhanViens
                    .AsNoTracking()
                    .Where(n => n.NgayVaoLam <= to
                                && n.TrangThai == "NGHI_PHEP"
                                && (departmentId == null || n.MaPhongBan == departmentId))
                    .OrderBy(n => n.PhongBan != null ? n.PhongBan.TenPhongBan : "")
                    .ThenBy(n => n.Ten)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien.ToString(),
                        TenDoiTuong = n.Ho + " " + n.Ten,
                        BoPhan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp",
                        NgayThang = n.NgayVaoLam,
                        TrangThai = "Đang nghỉ phép",
                        GhiChu = $"Vào làm: {n.NgayVaoLam:dd/MM/yyyy} • Trạng thái: Nghỉ phép"
                    })
                    .ToArrayAsync();
                result.DanhSach = nghiPhep;
                break;

            case "thoi-viec":
            case "da-thoi-viec":
                result.TieuDe = type.ToLowerInvariant() == "da-thoi-viec"
                    ? "Danh sách nhân sự đã thôi việc"
                    : "Danh sách nhân sự thôi việc trong kỳ";
                var thoi = await _context.NhanViens
                    .AsNoTracking()
                    .Where(n => (n.TrangThai == "THOI_VIEC" || n.TrangThai == "DA_THOI_VIEC")
                                && (type.ToLowerInvariant() == "da-thoi-viec"
                                    ? n.NgayVaoLam <= to && (!n.NgayThoiViec.HasValue || n.NgayThoiViec <= to)
                                    : n.NgayThoiViec >= from && n.NgayThoiViec <= to)
                                && (departmentId == null || n.MaPhongBan == departmentId))
                    .OrderBy(n => n.NgayThoiViec)
                    .Select(n => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + n.MaNhanVien.ToString(),
                        TenDoiTuong = n.Ho + " " + n.Ten,
                        BoPhan = n.PhongBan != null ? n.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = n.ChucVu != null ? n.ChucVu.TenChucVu : "Chưa xếp",
                        NgayThang = n.NgayThoiViec,
                        TrangThai = "Đã thôi việc",
                        GhiChu = n.NgayThoiViec.HasValue ? $"Thôi việc: {n.NgayThoiViec.Value:dd/MM/yyyy}" : "Chưa cập nhật ngày thôi việc"
                    })
                    .ToArrayAsync();
                result.DanhSach = thoi;
                break;

            case "muc-luong":
                if (!ReportPolicy.CanViewPayrollFundReport(role))
                {
                    result.TieuDe = "Không có quyền truy cập thông tin mức lương";
                    result.DanhSach = [];
                    break;
                }
                result.TieuDe = "Danh sách mức lương cơ bản nhân sự (Hợp đồng lao động)";
                var mucLuongRaw = (await LoadEmployeesAsync(departmentId, to, true))
                    .Where(n => ReportPolicy.IsEmployedAt(n.NgayVaoLam, n.TrangThai, n.NgayThoiViec, to) && n.LuongCoBan > 0)
                    .OrderByDescending(n => n.LuongCoBan)
                    .ThenBy(n => n.HoTen)
                    .ToArray();

                var mucLuongList = mucLuongRaw.Select(n => new ChiTietDongItem
                {
                    MaDinhDanh = "NV-" + n.MaNhanVien.ToString(),
                    TenDoiTuong = n.HoTen,
                    BoPhan = n.TenPhongBan,
                    ChucVuHoacLoai = n.TenChucVu,
                    NgayThang = n.NgayVaoLam,
                    SoTien = n.LuongCoBan,
                    TrangThai = ReportPolicy.SalaryBucketLabel(n.LuongCoBan),
                    GhiChu = $"Vào làm: {n.NgayVaoLam:dd/MM/yyyy} • Dải lương: {ReportPolicy.SalaryBucketLabel(n.LuongCoBan)}"
                }).ToArray();

                result.DanhSach = mucLuongList;
                result.TongTien = mucLuongList.Sum(s => s.SoTien ?? 0m);
                break;

            case "vi-pham":
                result.TieuDe = "Danh sách các lượt vi phạm kỷ luật (trễ/sớm)";
                var rawAtt = await LoadAttendanceAsync(from, to, departmentId);
                var violations = rawAtt
                    .Where(r => ReportPolicy.IsViolation(r.TrangThai))
                    .OrderByDescending(r => r.Ngay)
                    .Select(r => new ChiTietDongItem
                    {
                        MaDinhDanh = "NV-" + r.MaNhanVien,
                        TenDoiTuong = r.HoTen,
                        BoPhan = r.PhongBan,
                        ChucVuHoacLoai = r.TrangThai == "DI_TRE" ? "Đi trễ" : (r.TrangThai == "VE_SOM" ? "Về sớm" : "Trễ và sớm"),
                        NgayThang = r.Ngay,
                        SoLuong = r.PhutDiTre + r.PhutVeSom,
                        TrangThai = r.TrangThai,
                        GhiChu = $"Trễ {r.PhutDiTre}p, sớm {r.PhutVeSom}p"
                    })
                    .ToList();
                result.DanhSach = violations;
                break;

            case "phieu-luong":
                if (!ReportPolicy.CanViewPayrollFundReport(role))
                {
                    result.TieuDe = "Không có quyền truy cập chi tiết quỹ lương";
                    result.DanhSach = [];
                    break;
                }
                result.TieuDe = "Danh sách phiếu lương trong kỳ";
                var slips = await _context.PhieuLuongs
                    .AsNoTracking()
                    .Where(p => p.KyLuong != null && p.KyLuong.Nam == year
                                && p.KyLuong.Thang >= from.Month && p.KyLuong.Thang <= to.Month
                                && (departmentId == null || (p.NhanVien != null && p.NhanVien.MaPhongBan == departmentId)))
                    .OrderBy(p => p.KyLuong!.Thang)
                    .ThenBy(p => p.NhanVien != null ? p.NhanVien.Ten : "")
                    .Select(p => new ChiTietDongItem
                    {
                        MaDinhDanh = "PL-" + p.MaPhieuLuong,
                        TenDoiTuong = p.NhanVien != null ? p.NhanVien.Ho + " " + p.NhanVien.Ten : "Không tên",
                        BoPhan = p.NhanVien != null && p.NhanVien.PhongBan != null ? p.NhanVien.PhongBan.TenPhongBan : "Chưa xếp",
                        ChucVuHoacLoai = $"Tháng {p.KyLuong!.Thang:00}/{p.KyLuong.Nam}",
                        SoTien = p.TienLuongCong + p.TienTangCa + p.TongPhuCap + p.TongThuong,
                        TrangThai = p.TrangThai,
                        GhiChu = $"Thực nhận: {p.ThucNhan:N0} đ"
                    })
                    .ToArrayAsync();
                result.DanhSach = slips;
                result.TongTien = slips.Sum(s => s.SoTien ?? 0m);
                break;

            case "su-kien":
                result.TieuDe = "Các khoản tiền sự kiện phát sinh trong kỳ";
                var money = await LoadEventMoneyAsync(from, to, departmentId);
                result.DanhSach = money.Select(m => m.Detail).ToArray();
                result.TongTien = money.Sum(m => m.Detail.SoTien ?? 0m);
                break;
        }

        return result;
    }

    private sealed record EventMoneyRow(int EventId, ChiTietDongItem Detail);

    private async Task<EventMoneyRow[]> LoadEventMoneyAsync(DateOnly from, DateOnly to, int? departmentId)
    {
        var firstMonth = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var start = HotelEventRules.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = HotelEventRules.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var hourly = await _context.QuyetToanSuKiens.AsNoTracking()
            .Where(q => q.DuAn != null && q.DuAn.TinhTheoGio
                && q.DuAn.NgayBatDau < end && q.DuAn.NgayKetThuc > start
                && q.ThangPhatSinh >= firstMonth && q.ThangPhatSinh <= lastMonth
                && (departmentId == null || (q.NhanVien != null && q.NhanVien.MaPhongBan == departmentId)))
            .Select(q => new {
                q.MaDuAn, q.MaQuyetToan, q.ThangPhatSinh, q.MaKyChiTra, q.MaChiTiet, q.SoTien,
                Name = q.NhanVien != null ? q.NhanVien.Ho + " " + q.NhanVien.Ten : "Không rõ",
                Department = q.NhanVien != null && q.NhanVien.PhongBan != null ? q.NhanVien.PhongBan.TenPhongBan : "Chưa xếp",
                Event = q.DuAn!.TenDuAn
            }).ToArrayAsync();
        var legacy = await _context.PhanCongDuAns.AsNoTracking()
            .Where(p => p.DuAn != null && !p.DuAn.TinhTheoGio && p.SoTienPhuCap != 0m
                && p.DuAn.NgayBatDau < end && p.DuAn.NgayKetThuc > start
                && p.KetThuc > start && p.KetThuc <= end
                && (departmentId == null || (p.NhanVien != null && p.NhanVien.MaPhongBan == departmentId)))
            .Select(p => new {
                p.MaDuAn, p.MaPhanCong, p.KetThuc, p.SoTienPhuCap, p.TrangThaiChiTra,
                Name = p.NhanVien != null ? p.NhanVien.Ho + " " + p.NhanVien.Ten : "Không rõ",
                Department = p.NhanVien != null && p.NhanVien.PhongBan != null ? p.NhanVien.PhongBan.TenPhongBan : "Chưa xếp",
                Event = p.DuAn!.TenDuAn
            }).ToArrayAsync();
        return hourly.Select(q => new EventMoneyRow(q.MaDuAn, new ChiTietDongItem {
            MaDinhDanh = "QT-" + q.MaQuyetToan, TenDoiTuong = q.Name, BoPhan = q.Department,
            ChucVuHoacLoai = q.Event, NgayThang = q.ThangPhatSinh, SoTien = q.SoTien,
            TrangThai = q.MaChiTiet.HasValue ? "Đã kết chuyển vào phiếu lương" : "Chưa kết chuyển",
            GhiChu = "Tháng phát sinh; kỳ chi trả #" + q.MaKyChiTra + ". Kết chuyển không có nghĩa đã thanh toán."
        })).Concat(legacy.Select(p => new EventMoneyRow(p.MaDuAn, new ChiTietDongItem {
            MaDinhDanh = "PC-" + p.MaPhanCong, TenDoiTuong = p.Name, BoPhan = p.Department,
            ChucVuHoacLoai = p.Event, NgayThang = DateOnly.FromDateTime(HotelEventRules.Local(p.KetThuc.AddTicks(-1))),
            SoTien = p.SoTienPhuCap, TrangThai = p.TrangThaiChiTra,
            GhiChu = "Tiền lịch sử ghi nhận tại tháng kết thúc phân công; không cộng lại từ quyết toán."
        }))).ToArray();
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

    /// <summary>Chuẩn hóa tham số tab; giá trị lạ rơi về tình hình nhân sự.</summary>
    public static string NormalizeTab(string? tab)
    {
        var value = (tab ?? string.Empty).Trim().ToLowerInvariant();
        return ValidTabs.Contains(value) ? value : "co-cau";
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
    public string HoTen { get; set; } = string.Empty;
    public string TenPhongBan { get; set; } = string.Empty;
    public string TrinhDo { get; set; } = string.Empty;
    public string GioiTinh { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public DateOnly NgayVaoLam { get; set; }
    public DateOnly? NgayThoiViec { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public decimal LuongCoBan { get; set; }
}

/// <summary>Một lượt chấm công kèm nhân viên và bộ phận suy ra từ lịch phân ca.</summary>
internal sealed class ChamCongRow
{
    public int MaNhanVien { get; set; }
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
    public string TrangThai { get; set; } = string.Empty;
    public string PhongBan { get; set; } = string.Empty;
    public decimal TienLuongCong { get; set; }
    public decimal TienTangCa { get; set; }
    public decimal TongPhuCap { get; set; }
    public decimal TongThuong { get; set; }
    public decimal ThucNhan { get; set; }
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
    public string TrangThaiThamGia { get; set; } = "DA_PHAN";
}
