using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Phân hệ Báo cáo &amp; Thống kê Quản trị (Phân hệ 10.0 — TSK-24), gồm sáu báo cáo 10.1 → 10.6.
///
/// <para>
/// Mọi công thức nghiệp vụ (tỷ lệ thôi việc, xếp nhóm thâm niên, chuẩn hóa học vấn, đối soát
/// định mức, chặn chia 0) đều nằm ở <see cref="ReportPolicy"/> — controller chỉ truy vấn dữ liệu
/// thô rồi gọi lại. Nhờ vậy phần luật kiểm thử được mà không cần CSDL.
/// </para>
///
/// <para>
/// <b>Phạm vi dữ liệu do máy chủ quyết định, không do URL:</b> Quản lý bị
/// <see cref="ReportPolicy.ResolveScope"/> ghép cứng vào bộ phận của mình, nên tham số
/// <c>phongBan</c> trên địa chỉ không thể mở rộng tầm nhìn. Thiếu claim phòng ban thì bị từ chối
/// thẳng (fail-closed).
/// </para>
/// </summary>
[Authorize(Policy = PermissionCodes.ReportView)]
public class BaoCaoController : Controller
{
    /// <summary>Thời gian sống của bộ nhớ đệm số liệu báo cáo, tính bằng giây.</summary>
    private const int CacheSeconds = 60;

    /// <summary>Múi giờ Việt Nam, dùng để hiển thị mốc thời gian và quy đổi ngày sự kiện.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Danh sách tab hợp lệ; giá trị khác sẽ rơi về tab mặc định.</summary>
    private static readonly string[] ValidTabs =
        ["bien-dong", "co-cau", "hoc-van", "quy-luong", "chuyen-can", "su-kien"];

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BaoCaoController> _logger;

    public BaoCaoController(AppDbContext context, IMemoryCache cache, ILogger<BaoCaoController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Trang báo cáo. Gọi thường thì trả cả trang (header + bộ lọc + vùng dữ liệu); gọi bằng
    /// AJAX thì chỉ trả vùng dữ liệu <c>_BaoCaoBoard</c> để đổi kỳ / phòng ban / tab tại chỗ,
    /// không nạp lại toàn trang theo quy tắc "Zero Full-Page Reload" của coding-style mục 4.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            // Vai trò không có quyền, hoặc Quản lý thiếu claim phòng ban.
            return Forbid();
        }

        var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);

        if (IsAjaxRequest())
        {
            // Gửi kèm nhãn kỳ và phạm vi để thanh trạng thái cập nhật đúng dù không nạp lại trang.
            Response.Headers["X-Report-Period"] = Uri.EscapeDataString(model.NhanKy);
            Response.Headers["X-Report-Scope"] = Uri.EscapeDataString(model.PhamViLabel);
            return PartialView("_BaoCaoBoard", model);
        }

        return View(model);
    }

    /// <summary>
    /// Xuất CSV cho đúng tab đang chọn. Dùng UTF-8 kèm BOM để Excel đọc đúng tiếng Việt
    /// (theo mẫu <c>ExportBankTransferFileAsync</c> của phân hệ lương).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Export(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        try
        {
            var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);
            var (fileName, content) = BuildCsv(model);
            return File(content, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xuất CSV báo cáo thống kê.");
            TempData["Error"] = "Không thể xuất tệp báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index), new { ky, nam, thang, quy, phongBan, tab });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DỰNG SỐ LIỆU
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dựng toàn bộ sáu báo cáo cho kỳ và phạm vi đã chọn. Kết quả được đệm 60 giây theo
    /// khóa gồm đủ tham số, vì các báo cáo này quét lượng lớn dữ liệu chấm công và phiếu lương.
    /// </summary>
    private async Task<BaoCaoViewModel> BuildAsync(
        ReportPolicy.BaoCaoScope scope,
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
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
            // Tab là trạng thái giao diện, không nằm trong khóa đệm.
            cached.Tab = NormalizeTab(tab);
            return cached;
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
                ? [.. phongBans.Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan.ToString(CultureInfo.InvariantCulture)))]
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
            model.ChartJson = JsonSerializer.Serialize(new
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
            Vang = rows.Count(r => r.TrangThai == "VANG"),
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

        var tongLuot = phanCong.Length;

        return new HieuQuaSuKienReport
        {
            TongSuKien = suKien.Length,
            DangDienRa = suKien.Count(d => d.TrangThai == "DANG_DIEN_RA"),
            DaHoanThanh = suKien.Count(d => d.TrangThai == "HOAN_THANH"),
            DaHuy = suKien.Count(d => d.TrangThai == "HUY"),
            TongLuotPhanCong = tongLuot,
            TongNganSach = suKien.Sum(d => d.NganSachPhuCap),
            TongPhuCapDaPhanBo = phanCong.Sum(p => p.SoTienPhuCap),
            TyLeXuatSac = PhanTram(phanCong.Count(p => p.DanhGia == "XUAT_SAC"), tongLuot),
            TyLeHoanThanh = PhanTram(phanCong.Count(p => p.DanhGia is "HOAN_THANH" or "XUAT_SAC"), tongLuot),

            DanhSach = [.. suKien
                .Select(d =>
                {
                    var cuaSuKien = phanCong.Where(p => p.MaDuAn == d.MaDuAn).ToArray();
                    var phuCap = cuaSuKien.Sum(p => p.SoTienPhuCap);

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
            GioiTinh = n.GioiTinh,
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
    //  XUẤT CSV
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Dựng nội dung CSV cho tab đang chọn, kèm BOM để Excel nhận đúng UTF-8.</summary>
    private static (string FileName, byte[] Content) BuildCsv(BaoCaoViewModel model)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');

        sb.AppendLine($"BÁO CÁO THỐNG KÊ NHÂN SỰ — {model.NhanKy}");
        sb.AppendLine($"Phạm vi: {model.PhamViLabel}");
        sb.AppendLine($"Người xuất: {model.RoleLabel} · Lúc {model.GeneratedAt.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm}");
        sb.AppendLine();

        switch (model.Tab)
        {
            case "co-cau":
                AppendCoCau(sb, model.CoCau);
                break;
            case "hoc-van":
                AppendHocVan(sb, model.HocVanThamNien);
                break;
            case "quy-luong":
                AppendQuyLuong(sb, model.QuyLuong);
                break;
            case "chuyen-can":
                AppendChuyenCan(sb, model.ChuyenCan);
                break;
            case "su-kien":
                AppendSuKien(sb, model.SuKien);
                break;
            default:
                AppendBienDong(sb, model.BienDong);
                break;
        }

        var safePeriod = model.NhanKy.Replace('/', '-').Replace(' ', '_');
        return ($"BaoCao_{model.Tab}_{safePeriod}.csv", Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static void AppendBienDong(StringBuilder sb, BienDongNhanSuReport report)
    {
        sb.AppendLine("BIẾN ĐỘNG NHÂN SỰ");
        sb.AppendLine("Chỉ tiêu,Giá trị");
        sb.AppendLine($"Quân số đầu kỳ,{report.DauKy}");
        sb.AppendLine($"Tuyển mới trong kỳ,{report.TuyenMoi}");
        sb.AppendLine($"Thôi việc trong kỳ,{report.NghiViec}");
        sb.AppendLine($"Quân số cuối kỳ,{report.CuoiKy}");
        sb.AppendLine($"Biến động ròng,{report.BienDongRong}");
        sb.AppendLine($"Tỷ lệ thôi việc (%),{Num(report.TyLeThoiViec)}");
        sb.AppendLine();

        sb.AppendLine("DIỄN BIẾN THEO THÁNG");
        sb.AppendLine("Tháng,Tuyển mới,Thôi việc,Quân số cuối tháng");
        foreach (var item in report.Series)
        {
            sb.AppendLine($"{item.Nhan},{item.TuyenMoi},{item.NghiViec},{item.CuoiKy}");
        }

        sb.AppendLine();
        sb.AppendLine("THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Quân số,Tuyển mới,Thôi việc");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.PhongBan)},{item.QuanSo},{item.TuyenMoi},{item.NghiViec}");
        }
    }

    private static void AppendCoCau(StringBuilder sb, CoCauNhanLucReport report)
    {
        sb.AppendLine("CƠ CẤU NHÂN LỰC");
        sb.AppendLine($"Tổng nhân sự,{report.TongNhanSu}");
        sb.AppendLine($"Đang làm việc,{report.DangLam}");
        sb.AppendLine($"Đang nghỉ phép,{report.NghiPhep}");
        sb.AppendLine($"Đã thôi việc,{report.DaThoiViec}");
        sb.AppendLine();
        AppendPhanBo(sb, "THEO PHÒNG BAN", report.TheoPhongBan);
        AppendPhanBo(sb, "THEO TRẠNG THÁI", report.TheoTrangThai);
        AppendPhanBo(sb, "THEO GIỚI TÍNH", report.TheoGioiTinh);
        AppendPhanBo(sb, "THEO CHỨC VỤ", report.TheoChucVu);
    }

    private static void AppendHocVan(StringBuilder sb, HocVanThamNienReport report)
    {
        sb.AppendLine("HỌC VẤN & THÂM NIÊN");
        sb.AppendLine($"Số nhân sự từ đại học trở lên,{report.SoDaiHocTroLen}");
        sb.AppendLine($"Tỷ lệ từ đại học trở lên (%),{Num(report.TyLeDaiHocTroLen)}");
        sb.AppendLine($"Thâm niên trung bình (năm),{Num(report.ThamNienTrungBinh)}");
        sb.AppendLine();
        AppendPhanBo(sb, "THEO TRÌNH ĐỘ", report.TheoTrinhDo);
        AppendPhanBo(sb, "THEO THÂM NIÊN", report.TheoThamNien);
    }

    private static void AppendQuyLuong(StringBuilder sb, QuyLuongNganSachReport report)
    {
        sb.AppendLine("ĐỐI SOÁT QUỸ LƯƠNG VS ĐỊNH MỨC THAM CHIẾU");
        sb.AppendLine($"Thực chi (thực nhận),{Num(report.ThucChi)}");
        sb.AppendLine($"Thực chi (gộp),{Num(report.ThucChiGross)}");
        sb.AppendLine($"Định mức tham chiếu,{Num(report.NganSachThamChieu)}");
        sb.AppendLine($"Chênh lệch,{Num(report.ChenhLech)}");
        sb.AppendLine($"Tỷ lệ giải ngân (%),{Num(report.PhanTramGiaiNgan)}");
        sb.AppendLine($"Vượt định mức,{(report.VuotNguong ? "Có" : "Không")}");
        sb.AppendLine($"Ghi chú,{Esc(report.GhiChuPhuongPhap)}");
        sb.AppendLine();

        sb.AppendLine("DIỄN BIẾN THEO THÁNG");
        sb.AppendLine("Tháng,Định mức tham chiếu,Thực chi,Chênh lệch");
        foreach (var item in report.Series)
        {
            sb.AppendLine($"{item.Nhan},{Num(item.NganSach)},{Num(item.ThucChi)},{Num(item.ThucChi - item.NganSach)}");
        }

        sb.AppendLine();
        sb.AppendLine("THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Định mức tham chiếu,Thực chi,Chênh lệch");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.PhongBan)},{Num(item.NganSach)},{Num(item.ThucChi)},{Num(item.ChenhLech)}");
        }
    }

    private static void AppendChuyenCan(StringBuilder sb, ChuyenCanViPhamReport report)
    {
        sb.AppendLine("CHUYÊN CẦN & VI PHẠM");
        sb.AppendLine($"Tổng lượt chấm công,{report.TongBanGhi}");
        sb.AppendLine($"Đúng giờ,{report.DungGio}");
        sb.AppendLine($"Đi trễ,{report.DiTre}");
        sb.AppendLine($"Về sớm,{report.VeSom}");
        sb.AppendLine($"Trễ và sớm,{report.TreVaSom}");
        sb.AppendLine($"Vắng mặt,{report.Vang}");
        sb.AppendLine($"Tỷ lệ chuyên cần (%),{Num(report.TyLeChuyenCan)}");
        sb.AppendLine($"Tổng phút đi trễ,{report.TongPhutTre}");
        sb.AppendLine($"Tổng phút về sớm,{report.TongPhutVeSom}");
        sb.AppendLine($"Tổng giờ tăng ca OT,{Num(report.TongGioOt)}");
        sb.AppendLine();

        sb.AppendLine("XẾP HẠNG VI PHẠM");
        sb.AppendLine("Nhân viên,Phòng ban,Số lần vi phạm,Tổng phút trễ,Tổng phút sớm");
        foreach (var item in report.XepHangViPham)
        {
            sb.AppendLine($"{Esc(item.HoTen)},{Esc(item.PhongBan)},{item.SoLanViPham},{item.TongPhutTre},{item.TongPhutVeSom}");
        }

        sb.AppendLine();
        sb.AppendLine("TỶ LỆ CHUYÊN CẦN THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Số lượt,Tỷ lệ (%)");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.Nhan)},{item.GiaTri},{Num(item.TyLe)}");
        }
    }

    private static void AppendSuKien(StringBuilder sb, HieuQuaSuKienReport report)
    {
        sb.AppendLine("HIỆU QUẢ NHÂN SỰ DỰ ÁN / SỰ KIỆN");
        sb.AppendLine($"Tổng số sự kiện,{report.TongSuKien}");
        sb.AppendLine($"Đang diễn ra,{report.DangDienRa}");
        sb.AppendLine($"Đã hoàn thành,{report.DaHoanThanh}");
        sb.AppendLine($"Đã hủy,{report.DaHuy}");
        sb.AppendLine($"Tổng lượt phân công,{report.TongLuotPhanCong}");
        sb.AppendLine($"Tổng ngân sách phụ cấp,{Num(report.TongNganSach)}");
        sb.AppendLine($"Tổng phụ cấp đã phân bổ,{Num(report.TongPhuCapDaPhanBo)}");
        sb.AppendLine($"Tỷ lệ xuất sắc (%),{Num(report.TyLeXuatSac)}");
        sb.AppendLine($"Tỷ lệ hoàn thành (%),{Num(report.TyLeHoanThanh)}");
        sb.AppendLine();

        sb.AppendLine("DANH SÁCH SỰ KIỆN");
        sb.AppendLine("Tên sự kiện,Loại,Trạng thái,Từ ngày,Đến ngày,Số nhân sự,Ngân sách,Đã phân bổ,Tỷ lệ sử dụng (%),Xuất sắc,Hoàn thành,Chưa đánh giá");
        foreach (var item in report.DanhSach)
        {
            sb.AppendLine(string.Join(',',
                Esc(item.TenDuAn), Esc(item.LoaiLabel), Esc(item.TrangThaiLabel),
                item.TuNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.DenNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.SoNhanSu, Num(item.NganSach), Num(item.PhuCapDaPhanBo), Num(item.TyLeSuDungNganSach),
                item.SoXuatSac, item.SoHoanThanh, item.SoChuaDanhGia));
        }

        sb.AppendLine();
        AppendPhanBo(sb, "THEO LOẠI SỰ KIỆN", report.TheoLoai);
        AppendPhanBo(sb, "THEO ĐÁNH GIÁ NHÂN SỰ", report.TheoDanhGia);
    }

    /// <summary>Ghi một khối phân bổ ba cột: nhãn, số lượng, tỷ lệ.</summary>
    private static void AppendPhanBo(StringBuilder sb, string title, IReadOnlyList<PhanBoItem> items)
    {
        sb.AppendLine(title);
        sb.AppendLine("Nhóm,Số lượng,Tỷ lệ (%)");
        foreach (var item in items)
        {
            sb.AppendLine($"{Esc(item.Nhan)},{item.GiaTri},{Num(item.TyLe)}");
        }

        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TIỆN ÍCH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Giải quyết phạm vi báo cáo từ phiên đăng nhập. Trả phạm vi bị từ chối khi vai trò
    /// không được phép hoặc Quản lý thiếu claim phòng ban.
    /// </summary>
    private ReportPolicy.BaoCaoScope ResolveScope()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var raw = User.FindFirst("DepartmentId")?.Value;
        int? departmentId = int.TryParse(raw, out var id) && id > 0 ? id : null;

        return ReportPolicy.ResolveScope(role, departmentId);
    }

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
    private static string NormalizeTab(string? tab)
    {
        var value = (tab ?? string.Empty).Trim().ToLowerInvariant();
        return ValidTabs.Contains(value) ? value : "bien-dong";
    }

    /// <summary>Nhãn tiếng Việt của vai trò đang đăng nhập.</summary>
    private static string ResolveRoleLabel(string? role) => role?.Trim().ToUpperInvariant() switch
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

    /// <summary>Số thập phân gọn cho CSV, dùng dấu chấm để Excel không hiểu nhầm dấu phân cách.</summary>
    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Bọc trường CSV trong dấu nháy kép khi chứa dấu phẩy, nháy kép hoặc xuống dòng.</summary>
    private static string Esc(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Yêu cầu AJAX nạp vùng dữ liệu tại chỗ.</summary>
    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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
