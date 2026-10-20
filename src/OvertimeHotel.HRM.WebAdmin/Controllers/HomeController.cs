using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

// Khách chưa đăng nhập không được thấy bảng điều khiển. Nếu không chặn ở đây thì
// mở web với cookie trống sẽ render nhánh "wip-panel" trần, trông như trang lỗi.
[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, IMemoryCache cache, ILogger<HomeController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var cacheKey = $"HomeDashboardMetrics_V3_{role}";

        if (!_cache.TryGetValue(cacheKey, out HomeDashboardViewModel? model) || model is null)
        {
            model = await BuildDashboardAsync(role);
            _cache.Set(cacheKey, model, TimeSpan.FromSeconds(60));
        }

        return View(model);
    }

    /// <summary>
    /// Dựng toàn bộ số liệu bảng điều khiển. Mọi truy vấn đều được gom về dạng projection
    /// để không kéo entity về bộ nhớ, và khu vực "Cần bạn xử lý" được lọc đúng theo
    /// ma trận thẩm quyền của vai trò đang đăng nhập.
    /// </summary>
    private async Task<HomeDashboardViewModel> BuildDashboardAsync(string? role)
    {
        var model = new HomeDashboardViewModel { GeneratedAt = DateTimeOffset.UtcNow };
        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTimeOffset.UtcNow;

        try
        {
            // ── Nhân sự ──────────────────────────────────────────────────────
            model.NhanVienDangLam = await _context.NhanViens
                .AsNoTracking()
                .CountAsync(n => n.TrangThai == "DANG_LAM");

            var kyTruoc = await _context.KyLuongs
                .AsNoTracking()
                .Where(k => k.Thang == today.AddMonths(-1).Month && k.Nam == today.AddMonths(-1).Year)
                .Select(k => k.MaKyLuong)
                .FirstOrDefaultAsync();

            if (kyTruoc > 0)
            {
                var nhanVienIds = await _context.PhieuLuongs
                    .AsNoTracking()
                    .Where(p => p.MaKyLuong == kyTruoc)
                    .Select(p => p.MaNhanVien)
                    .Distinct()
                    .ToArrayAsync();

                model.NhanVienKyTruoc = nhanVienIds.Length;
            }

            // ── Chấm công hôm nay (gắn qua lịch phân ca) ─────────────────────
            var homNay = await _context.ChamCongs
                .AsNoTracking()
                .Where(c => c.PhanCa != null && c.PhanCa.NgayLamViec == today)
                .Select(c => c.TrangThai)
                .ToArrayAsync();

            model.BanGhiChamCongHomNay = homNay.Length;
            model.ChamCongDungGioHomNay = homNay.Count(t => t == "DUNG_GIO");
            // TRE_VA_SOM được CSDL chấp nhận từ migration 20261020; thiếu mã này thì
            // người vừa trễ vừa về sớm không được tính là vi phạm chuyên cần.
            model.ChamCongDiTreHomNay = homNay.Count(t => t is "DI_TRE" or "VE_SOM" or "TRE_VA_SOM");

            var tuanTruoc = await _context.ChamCongs
                .AsNoTracking()
                .Where(c => c.PhanCa != null
                            && c.PhanCa.NgayLamViec >= today.AddDays(-7)
                            && c.PhanCa.NgayLamViec < today)
                .Select(c => c.TrangThai)
                .ToArrayAsync();

            if (tuanTruoc.Length > 0)
            {
                model.TyLeDungGioKyTruoc = Math.Round(
                    (decimal)tuanTruoc.Count(t => t == "DUNG_GIO") / tuanTruoc.Length * 100, 1);
            }

            // ── Kỳ lương đang mở ─────────────────────────────────────────────
            var kyHienTai = await _context.KyLuongs
                .AsNoTracking()
                .Where(k => k.TrangThai != "KHOA_SO")
                .OrderByDescending(k => k.Nam)
                .ThenByDescending(k => k.Thang)
                .Select(k => new { k.MaKyLuong, k.Thang, k.Nam })
                .FirstOrDefaultAsync();

            if (kyHienTai is not null && kyHienTai.MaKyLuong > 0)
            {
                model.KyLuongHienTai = $"{kyHienTai.Thang:00}/{kyHienTai.Nam}";

                var phieuLuong = await _context.PhieuLuongs
                    .AsNoTracking()
                    .Where(p => p.MaKyLuong == kyHienTai.MaKyLuong)
                    .Select(p => new { p.ThucNhan, p.TrangThai })
                    .ToArrayAsync();

                model.QuyLuongKyHienTai = phieuLuong.Sum(p => p.ThucNhan);
                model.SoPhieuLuongKyHienTai = phieuLuong.Length;
                model.SoPhieuLuongDaDuyet = phieuLuong.Count(p => p.TrangThai != "NHAP");
            }

            // ── Hợp đồng sắp hết hạn ─────────────────────────────────────────
            var horizon = today.AddDays(30);
            model.HopDongSapHetHan = await _context.HopDongs
                .AsNoTracking()
                .CountAsync(h => h.NgayKetThuc.HasValue
                                 && h.NgayKetThuc.Value >= today
                                 && h.NgayKetThuc.Value <= horizon);

            // ── Hàng đợi công việc chờ xử lý ────────────────────────────────
            model.DonTuChoDuyet = await BuildReviewableLeaveQuery(role)
                .CountAsync(d => d.TrangThai == "CHO_DUYET");
            model.ChoXuLy = await BuildPendingActionsAsync(role, today, now);

            // ── Đội hình ca trực hôm nay ─────────────────────────────────────
            model.CaTrucHomNay = await BuildShiftSummaryAsync(today);
            model.CaDangChay = ResolveCurrentShiftName(DateTime.Now);

            model.IsDatabaseOnline = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi nạp số liệu bảng điều khiển từ CSDL Supabase.");
            model.IsDatabaseOnline = false;
        }

        return model;
    }

    /// <summary>
    /// Dựng truy vấn đơn từ đã giới hạn theo thẩm quyền của vai trò đang đăng nhập:
    /// chỉ gồm loại đơn được phép duyệt, và Manager bị thu hẹp còn cùng phòng ban.
    /// </summary>
    private IQueryable<OvertimeHotel.HRM.Core.Models.DonTu> BuildReviewableLeaveQuery(string? role)
    {
        var allowedTypes = LeaveApprovalPolicy.GetReviewableRequestTypes(role);

        var query = _context.DonTus
            .AsNoTracking()
            .Where(d => d.TrangThai == "CHO_DUYET" && allowedTypes.Contains(d.MaLoaiDon));

        if (LeaveApprovalPolicy.IsManager(role))
        {
            var deptId = CurrentDepartmentId();
            query = deptId.HasValue
                ? query.Where(d => d.NhanVien != null && d.NhanVien.MaPhongBan == deptId.Value)
                : query.Where(d => false);
        }

        // Không ai tự duyệt đơn của chính mình, trừ quản trị viên — số liệu ở đây
        // phải khớp đúng với API Mobile, nếu không sẽ lệch nhau giữa hai nền tảng.
        if (!LeaveApprovalPolicy.IsAdmin(role) && CurrentEmployeeId() is int selfId)
        {
            query = query.Where(d => d.MaNhanVien != selfId);
        }

        return query;
    }

    /// <summary>
    /// Gom các việc tồn đọng thành một hàng đợi duy nhất, sắp xếp theo độ gấp.
    /// Danh sách đơn từ chỉ lấy những loại mà vai trò hiện tại thực sự có thẩm quyền duyệt.
    /// </summary>
    private async Task<List<PendingActionItem>> BuildPendingActionsAsync(
        string? role,
        DateOnly today,
        DateTimeOffset now)
    {
        var items = new List<PendingActionItem>();

        // Đơn từ: lọc theo loại đơn được phép duyệt, Manager còn bị giới hạn cùng phòng ban.
        var donRows = await BuildReviewableLeaveQuery(role)
            .Include(d => d.NhanVien)
            .Include(d => d.LoaiDon)
            .Select(d => new
            {
                d.MaDon,
                HoTen = d.NhanVien != null ? d.NhanVien.Ho + " " + d.NhanVien.Ten : string.Empty,
                TenLoai = d.LoaiDon != null ? d.LoaiDon.TenLoaiDon : string.Empty,
                d.TuNgay,
                d.DenNgay,
                d.NgayGui
            })
            .OrderBy(d => d.NgayGui)
            .Take(8)
            .ToArrayAsync();

        items.AddRange(donRows.Select(d => new PendingActionItem
        {
            Key = $"don-{d.MaDon}",
            Category = "ĐƠN_TỪ",
            CategoryLabel = d.TenLoai,
            Title = d.HoTen,
            Subtitle = $"{d.TuNgay:dd/MM} → {d.DenNgay:dd/MM} · chờ {WaitingDays(d.NgayGui, now)} ngày",
            WaitingDays = WaitingDays(d.NgayGui, now),
            Severity = "danger",
            ActionUrl = Url.Action("Index", "DonTu", new { view = "pending" })
        }));

        // Hợp đồng sắp hết hạn trong 30 ngày.
        var hopDongRows = await _context.HopDongs
            .AsNoTracking()
            .Where(h => h.NgayKetThuc.HasValue
                        && h.NgayKetThuc.Value >= today
                        && h.NgayKetThuc.Value <= today.AddDays(30))
            .OrderBy(h => h.NgayKetThuc)
            .Take(5)
            .Select(h => new
            {
                h.MaHopDong,
                h.SoHopDong,
                HoTen = h.NhanVien != null ? h.NhanVien.Ho + " " + h.NhanVien.Ten : string.Empty,
                h.NgayKetThuc
            })
            .ToArrayAsync();

        items.AddRange(hopDongRows.Select(h => new PendingActionItem
        {
            Key = $"hd-{h.MaHopDong}",
            Category = "HĐLĐ",
            CategoryLabel = "Sắp hết hạn",
            Title = h.HoTen,
            Subtitle = $"{h.SoHopDong} · hết hạn {h.NgayKetThuc:dd/MM/yyyy}",
            WaitingDays = h.NgayKetThuc!.Value.DayNumber - today.DayNumber,
            Severity = "warning",
            ActionUrl = Url.Action("Index", "HopDong", new { contractFilter = "SAP_HET_HAN" })
        }));

        // Phiếu lương kỳ hiện tại còn ở trạng thái nháp.
        var phieuChoDuyet = await _context.PhieuLuongs
            .AsNoTracking()
            .Include(p => p.KyLuong)
            .Where(p => p.TrangThai == "NHAP" && p.KyLuong != null && p.KyLuong.TrangThai != "KHOA_SO")
            .GroupBy(p => p.MaKyLuong)
            .Select(g => new { MaKyLuong = g.Key, TongSo = g.Count(), TongThucNhan = g.Sum(x => x.ThucNhan) })
            .FirstOrDefaultAsync();

        if (phieuChoDuyet is not null)
        {
            items.Add(new PendingActionItem
            {
                Key = $"luong-{phieuChoDuyet.MaKyLuong}",
                Category = "LƯƠNG",
                CategoryLabel = "Phiếu lương",
                Title = $"{phieuChoDuyet.TongSo} phiếu chờ duyệt",
                Subtitle = $"Tổng thực nhận {phieuChoDuyet.TongThucNhan:N0} đ",
                WaitingDays = 0,
                Severity = "warning"
            });
        }

        return items
            .OrderByDescending(i => i.Severity == "danger")
            .ThenByDescending(i => i.WaitingDays)
            .Take(12)
            .ToList();
    }

    /// <summary>Tóm tắt số người trực và số người đã quẹt thẻ cho từng ca trong ngày.</summary>
    private async Task<List<ShiftSummaryItem>> BuildShiftSummaryAsync(DateOnly today)
    {
        var caLamViecs = await _context.CaLamViecs
            .AsNoTracking()
            .OrderBy(c => c.GioBatDau)
            .ToArrayAsync();

        var counts = await _context.PhanCas
            .AsNoTracking()
            .Where(p => p.NgayLamViec == today)
            .Select(p => new { p.MaCa, DaCheckIn = p.ChamCong != null })
            .ToArrayAsync();

        var currentName = ResolveCurrentShiftName(DateTime.Now);

        return caLamViecs.Select(ca =>
        {
            var nhom = counts.Where(c => c.MaCa == ca.MaCa).ToArray();
            return new ShiftSummaryItem
            {
                TenCa = ca.TenCa,
                Gio = $"{ca.GioBatDau:HH\\:mm} – {ca.GioKetThuc:HH\\:mm}",
                TongSo = nhom.Length,
                DaCheckIn = nhom.Count(c => c.DaCheckIn),
                IsCurrent = ca.TenCa.Contains(currentName, StringComparison.OrdinalIgnoreCase)
            };
        }).ToList();
    }

    /// <summary>Số ngày đã chờ kể từ thời điểm gửi đến hiện tại.</summary>
    private static int WaitingDays(DateTimeOffset sentAt, DateTimeOffset now)
        => Math.Max(0, (int)(now - sentAt).TotalDays);

    /// <summary>Đọc mã phòng ban của người đang đăng nhập từ claim.</summary>
    private int? CurrentDepartmentId()
    {
        var raw = User.FindFirst("DepartmentId")?.Value;
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    /// <summary>
    /// Đọc mã nhân viên của người đang đăng nhập từ claim. Dùng để loại đơn của
    /// chính mình khỏi hàng đợi "cần bạn xử lý" — quản trị viên là ngoại lệ vì đó
    /// là thoát vòng lặp khi chính họ cần nghỉ (khớp LeaveApprovalPolicy).
    /// </summary>
    private int? CurrentEmployeeId()
    {
        var raw = User.FindFirst("EmployeeId")?.Value;
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    /// <summary>Xác định tên ca đang diễn ra dựa trên khung giờ 06:00 / 14:00 / 22:00.</summary>
    private static string ResolveCurrentShiftName(DateTime now)
        => now.Hour switch
        {
            >= 6 and < 14 => "Sáng",
            >= 14 and < 22 => "Chiều",
            _ => "Đêm"
        };

    public IActionResult Privacy()
    {
        return View();
    }

    // Trang lỗi phải mở cho khách: app.UseExceptionHandler("/Home/Error") là điểm
    // đích cho mọi ngoại lệ, kể cả khi chưa có cookie đăng nhập.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
