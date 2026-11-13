using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Giải trình chấm công — quy trình hai cấp Quản lý xác minh rồi HR xác nhận (PRD 4.5).
///
/// <para>
/// <b>Vì sao phải hai cấp.</b> Nhân viên quên quẹt thẻ là chuyện thật, nhưng sửa
/// giờ công là sửa cơ sở tính lương. Quản lý bộ phận là người duy nhất biết đêm
/// đó nhân viên có mặt hay không; HR là người giữ tính toàn vẹn của bảng công.
/// Bắt cả hai cùng ký thì một người không tự sửa được giờ công của chính mình
/// hoặc của người thân trong bộ phận.
/// </para>
///
/// <para>
/// <b>Chỉ cấp HR mới ghi vào <c>cham_cong</c>.</b> Xác minh của Quản lý chỉ đổi
/// trạng thái yêu cầu sang <c>CHO_HR</c>. Nếu để cấp 1 ghi luôn thì bước HR chỉ
/// còn là thủ tục hình thức.
/// </para>
/// </summary>
[Authorize(Policy = PermissionCodes.ViewAttendanceExplanations)]
public class GiaiTrinhChamCongController : Controller
{
    private const string STATUS_CHO_QUAN_LY = "CHO_QUAN_LY";
    private const string STATUS_CHO_HR = "CHO_HR";
    private const string STATUS_DA_DUYET = "DA_DUYET";
    private const string STATUS_TU_CHOI = "TU_CHOI";
    private const string STATUS_DA_HUY = "DA_HUY";

    private const int PAGE_SIZE = 10;

    /// <summary>
    /// Giải trình hợp lệ trong bao nhiêu ngày kể từ ngày kết thúc ca. Bảng công
    /// chốt theo tháng nên quá hạn này thì sửa vào sẽ lệch sổ lương đã chốt.
    /// </summary>
    private const int SO_NGAY_GIAI_TRINH = 45;

    // Bám đúng cột giai_trinh_cham_cong.ly_do VARCHAR(500) — cho dài hơn thì
    // Postgres ném 22001 ngay lúc ghi, sau khi người dùng đã đăng nhập và bấm gửi.
    private const int MAX_LY_DO = 500;
    private const int MAX_MINH_CHUNG = 500;
    private const int MAX_Y_KIEN = 500;

    private readonly AppDbContext _context;
    private readonly IPayrollCalculationService _payrollService;
    private readonly ILogger<GiaiTrinhChamCongController> _logger;

    public GiaiTrinhChamCongController(
        AppDbContext context,
        IPayrollCalculationService payrollService,
        ILogger<GiaiTrinhChamCongController> logger)
    {
        _context = context;
        _payrollService = payrollService;
        _logger = logger;
    }

    // =====================================================================
    //  INDEX — DANH SÁCH GIẢI TRÌNH (TÁCH 2 CẤP QUẢN LÝ / HR & ĐA BỐI CẢNH)
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? tab, string? loaiCongViec, string? trangThai, string? keyword, int? phongBanId, int page = 1)
    {
        trangThai = IsKnownStatus(trangThai) ? trangThai : null;
        keyword = keyword?.Trim();
        page = Math.Max(1, page);

        var scopeDept = GetManagerDepartmentScope();
        var phamViPhongBanId = User.ResolveEffectiveDepartmentId(phongBanId);
        var biGioiHanTheoBoPhan = scopeDept.HasValue;

        // Tự động chọn Tab phù hợp vai trò nếu người dùng chưa chỉ định:
        // Quản lý ưu tiên Tab 1: Cần xác minh Cấp 1 ("quan-ly")
        // HR & Admin ưu tiên Tab 1: Cần HR phê duyệt Cấp 2 ("hr")
        if (string.IsNullOrWhiteSpace(tab))
        {
            tab = biGioiHanTheoBoPhan ? "quan-ly" : "hr";
        }

        var allPhongBans = await this.SearchDataAsync("giai-trinh:departments", () =>
            _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync());
        var tenPhamVi = allPhongBans.FirstOrDefault(p => p.MaPhongBan == (phamViPhongBanId ?? 0))?.TenPhongBan;

        var baseQuery = _context.GiaiTrinhChamCongs
            .AsNoTracking()
            .Include(g => g.NhanVien)!.ThenInclude(n => n!.PhongBan)
            .Include(g => g.PhanCa)!.ThenInclude(p => p!.CaLamViec)
            .Include(g => g.PhanCa)!.ThenInclude(p => p!.ChamCong)
            .Include(g => g.QuanLyXacMinh)
            .Include(g => g.HrXacNhan)
            .AsQueryable();

        if (phamViPhongBanId.HasValue)
        {
            baseQuery = phamViPhongBanId.Value <= 0
                ? baseQuery.Where(_ => false)
                : baseQuery.Where(g => g.NhanVien != null && g.NhanVien.MaPhongBan == phamViPhongBanId.Value);
        }

        var visibleItems = await this.SearchDataAsync($"attendance-explanations:scope:{phamViPhongBanId}", () => baseQuery.ToListAsync());
        baseQuery = visibleItems.AsQueryable();

        // Nạp danh sách ca thuộc sự kiện để phân loại bối cảnh làm việc
        var eventShiftIds = await _context.PhanCongDuAns
            .AsNoTracking()
            .Where(p => p.MaPhanCa.HasValue)
            .Select(p => p.MaPhanCa!.Value)
            .Distinct()
            .ToListAsync();
        var eventShiftSet = eventShiftIds.ToHashSet();

        var query = baseQuery;

        // 1. Phân luồng theo Tab cấp độ duyệt
        switch (tab)
        {
            case "quan-ly":
                query = query.Where(g => g.TrangThai == STATUS_CHO_QUAN_LY);
                break;
            case "hr":
                query = query.Where(g => g.TrangThai == STATUS_CHO_HR);
                break;
            case "hoan-tat":
                query = query.Where(g => g.TrangThai == STATUS_DA_DUYET || g.TrangThai == STATUS_TU_CHOI || g.TrangThai == STATUS_DA_HUY);
                break;
            case "tat-ca":
            default:
                if (!string.IsNullOrWhiteSpace(trangThai))
                {
                    query = query.Where(g => g.TrangThai == trangThai);
                }
                break;
        }

        // 2. Lọc theo Bối cảnh công việc (Ca thường 24/7 vs Sự kiện theo giờ)
        if (loaiCongViec == "SU_KIEN")
        {
            query = query.Where(g => eventShiftSet.Contains(g.MaPhanCa));
        }
        else if (loaiCongViec == "CA_THUONG")
        {
            query = query.Where(g => !eventShiftSet.Contains(g.MaPhanCa));
        }

        // 3. Tìm kiếm từ khóa
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLowerInvariant();
            query = query.Where(g =>
                (g.NhanVien != null && (g.NhanVien.Ho + " " + g.NhanVien.Ten).ToLower().Contains(value)) ||
                g.LyDo.ToLower().Contains(value));
        }

        var totalFiltered = query.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)PAGE_SIZE));
        var pageNumber = Math.Clamp(page, 1, totalPages);

        var rows = query
            .OrderBy(g => g.TrangThai == STATUS_CHO_QUAN_LY ? 0 : (g.TrangThai == STATUS_CHO_HR ? 1 : 2))
            .ThenByDescending(g => g.NgayGui)
            .Skip((pageNumber - 1) * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .Select(g => new
            {
                g.MaGiaiTrinh,
                g.MaPhanCa,
                g.MaNhanVien,
                TenNhanVien = g.NhanVien == null ? "NV #" + g.MaNhanVien : (g.NhanVien.Ho + " " + g.NhanVien.Ten).Trim(),
                TenPhongBan = g.NhanVien == null || g.NhanVien.PhongBan == null ? "—" : g.NhanVien.PhongBan.TenPhongBan,
                NgayLamViec = g.PhanCa == null ? DateOnly.MinValue : g.PhanCa.NgayLamViec,
                TenCa = g.PhanCa == null || g.PhanCa.CaLamViec == null ? "—" : g.PhanCa.CaLamViec.TenCa,
                GioBatDau = g.PhanCa == null || g.PhanCa.CaLamViec == null ? TimeOnly.MinValue : g.PhanCa.CaLamViec.GioBatDau,
                GioKetThuc = g.PhanCa == null || g.PhanCa.CaLamViec == null ? TimeOnly.MinValue : g.PhanCa.CaLamViec.GioKetThuc,
                g.GioDeXuatVao,
                g.GioDeXuatRa,
                GioVaoHienTai = g.PhanCa == null || g.PhanCa.ChamCong == null || g.PhanCa.ChamCong.GioCheckIn == null
                    ? null
                    : g.PhanCa.ChamCong.GioCheckIn.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy"),
                GioRaHienTai = g.PhanCa == null || g.PhanCa.ChamCong == null || g.PhanCa.ChamCong.GioCheckOut == null
                    ? null
                    : g.PhanCa.ChamCong.GioCheckOut.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy"),
                g.LyDo,
                g.MinhChung,
                g.TrangThai,
                TenQuanLyXacMinh = g.QuanLyXacMinh == null ? null : (g.QuanLyXacMinh.Ho + " " + g.QuanLyXacMinh.Ten).Trim(),
                g.NgayQuanLyXacMinh,
                g.YKienQuanLy,
                TenHrXacNhan = g.HrXacNhan == null ? null : (g.HrXacNhan.Ho + " " + g.HrXacNhan.Ten).Trim(),
                g.NgayHrXacNhan,
                g.YKienHr,
                g.NgayGui
            })
            .ToList();

        // Nạp thông tin sự kiện cho các ca phân công được hiển thị trên trang hiện tại
        var pagedShiftIds = rows.Select(r => r.MaPhanCa).Distinct().ToList();
        var eventAssignments = await _context.PhanCongDuAns
            .AsNoTracking()
            .Include(p => p.DuAn)
            .Where(p => p.MaPhanCa.HasValue && pagedShiftIds.Contains(p.MaPhanCa.Value))
            .Select(p => new
            {
                MaPhanCa = p.MaPhanCa!.Value,
                p.MaDuAn,
                TenDuAn = p.DuAn != null ? p.DuAn.TenDuAn : "Sự kiện",
                LoaiDuAn = p.DuAn != null ? p.DuAn.LoaiDuAn : null,
                p.VaiTroDuAn
            })
            .ToListAsync();

        var eventMap = eventAssignments
            .GroupBy(p => p.MaPhanCa)
            .ToDictionary(g => g.Key, g => g.First());

        var lockedPeriods = await _context.KyLuongs
            .AsNoTracking()
            .Where(k => k.TrangThai == SalaryConstants.PERIOD_LOCKED || k.TrangThai == "DA_THANH_TOAN")
            .Select(k => new { k.Thang, k.Nam, k.TuNgay, k.DenNgay })
            .ToListAsync();

        var model = new GiaiTrinhIndexViewModel
        {
            ActiveTab = tab,
            LoaiCongViec = loaiCongViec,
            TrangThai = trangThai,
            Keyword = keyword,
            SelectedPhongBanId = phongBanId,
            PhamViPhongBanId = phamViPhongBanId,
            TenPhamViPhongBan = tenPhamVi,
            BiGioiHanTheoBoPhan = biGioiHanTheoBoPhan,
            PhongBanOptions = allPhongBans.Select(p => new SelectListItem
            {
                Value = p.MaPhongBan.ToString(),
                Text = p.TenPhongBan,
                Selected = phamViPhongBanId == p.MaPhongBan
            }).ToList(),
            TrangThaiOptions = BuildStatusOptions(trangThai),
            LoaiCongViecOptions = BuildLoaiCongViecOptions(loaiCongViec),
            Items = rows.Select(r =>
            {
                var isEvent = eventMap.TryGetValue(r.MaPhanCa, out var ev);
                return new GiaiTrinhItemViewModel
                {
                    MaGiaiTrinh = r.MaGiaiTrinh,
                    MaPhanCa = r.MaPhanCa,
                    MaNhanVien = r.MaNhanVien,
                    TenNhanVien = r.TenNhanVien,
                    TenPhongBan = r.TenPhongBan,
                    NgayLamViec = r.NgayLamViec,
                    TenCa = r.TenCa,
                    GioCa = r.TenCa == "—" ? "—" : $"{r.GioBatDau:HH\\:mm} – {r.GioKetThuc:HH\\:mm}",
                    LaSuKien = isEvent,
                    MaDuAn = isEvent ? ev!.MaDuAn : null,
                    TenSuKien = isEvent ? ev!.TenDuAn : null,
                    VaiTroSuKien = isEvent ? ev!.VaiTroDuAn : null,
                    LoaiSuKien = isEvent ? ev!.LoaiDuAn : null,
                    GioDeXuatVao = r.GioDeXuatVao,
                    GioDeXuatRa = r.GioDeXuatRa,
                    GioVaoHienTai = r.GioVaoHienTai,
                    GioRaHienTai = r.GioRaHienTai,
                    LyDo = r.LyDo,
                    MinhChung = r.MinhChung,
                    TrangThai = r.TrangThai,
                    TenQuanLyXacMinh = r.TenQuanLyXacMinh,
                    NgayQuanLyXacMinh = r.NgayQuanLyXacMinh,
                    YKienQuanLy = r.YKienQuanLy,
                    TenHrXacNhan = r.TenHrXacNhan,
                    NgayHrXacNhan = r.NgayHrXacNhan,
                    YKienHr = r.YKienHr,
                    KyLuongDaKhoa = lockedPeriods.Any(k =>
                        (k.Thang == r.NgayLamViec.Month && k.Nam == r.NgayLamViec.Year) ||
                        (k.TuNgay <= r.NgayLamViec && k.DenNgay >= r.NgayLamViec)),
                    NgayGui = r.NgayGui
                };
            }).ToList(),
            TongGiaiTrinh = baseQuery.Count(),
            ChoQuanLy = baseQuery.Count(g => g.TrangThai == STATUS_CHO_QUAN_LY),
            ChoHr = baseQuery.Count(g => g.TrangThai == STATUS_CHO_HR),
            DaDuyet = baseQuery.Count(g => g.TrangThai == STATUS_DA_DUYET),
            TuChoi = baseQuery.Count(g => g.TrangThai == STATUS_TU_CHOI),
            DaHuy = baseQuery.Count(g => g.TrangThai == STATUS_DA_HUY),
            SoLuongSuKien = baseQuery.Count(g => eventShiftSet.Contains(g.MaPhanCa)),
            SoLuongCaThuong = baseQuery.Count(g => !eventShiftSet.Contains(g.MaPhanCa)),
            CoQuyenQuanLy = (User.IsInRole("Manager") || User.IsInRole("Admin")) && User.HasPermission(PermissionCodes.VerifyAttendanceExplanations),
            CoQuyenHr = !User.IsInRole("Manager") && User.HasPermission(PermissionCodes.VerifyAttendanceExplanations),
            PageNumber = pageNumber,
            PageSize = PAGE_SIZE,
            TotalPages = totalPages,
            TotalFilteredItems = totalFiltered
        };

        if (IsAjaxRequest())
        {
            return PartialView("_GiaiTrinhTable", model);
        }

        return View(model);
    }

    // =====================================================================
    //  CREATE — LẬP GIẢI TRÌNH
    // =====================================================================
    [HttpGet]
    [Authorize(Policy = PermissionCodes.VerifyAttendanceExplanations)]
    public async Task<IActionResult> Create(int? maPhanCa)
    {
        var model = new GiaiTrinhCreateViewModel();

        if (maPhanCa is > 0)
        {
            var ca = await LoadShiftAsync(maPhanCa.Value);
            if (ca != null)
            {
                model.MaPhanCa = ca.MaPhanCa;
                model.MaNhanVien = ca.MaNhanVien;
                model.TenNhanVien = ca.HoTen;
                model.MoTaCa = $"{ca.TenCa} · {ca.NgayLamViec:dd/MM/yyyy} ({ca.GioBatDau:HH\\:mm}–{ca.GioKetThuc:HH\\:mm})";
                model.GioVaoHienTai = FormatGio(ca.GioCheckIn);
                model.GioRaHienTai = FormatGio(ca.GioCheckOut);
            }
        }

        await LoadShiftOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.VerifyAttendanceExplanations)]
    public async Task<IActionResult> Create(GiaiTrinhCreateViewModel model)
    {
        model.LyDo = model.LyDo?.Trim() ?? string.Empty;
        model.MinhChung = string.IsNullOrWhiteSpace(model.MinhChung) ? null : model.MinhChung.Trim();

        // Ô datetime-local không mang múi giờ; quy về +07:00 của khách sạn ngay
        // tại cửa vào để mọi phép so sánh bên dưới đều cùng một hệ quy chiếu.
        var gioVao = ToVietnamOffset(model.GioDeXuatVao);
        var gioRa = ToVietnamOffset(model.GioDeXuatRa);

        if (model.LyDo.Length is 0 or > MAX_LY_DO)
        {
            ModelState.AddModelError(nameof(model.LyDo), $"Vui lòng nhập lý do giải trình (tối đa {MAX_LY_DO} ký tự).");
        }

        if (model.MinhChung is { Length: > MAX_MINH_CHUNG })
        {
            ModelState.AddModelError(nameof(model.MinhChung), $"Minh chứng không được vượt quá {MAX_MINH_CHUNG} ký tự.");
        }

        var scopeDept = GetManagerDepartmentScope();
        var ca = model.MaPhanCa > 0 ? await LoadShiftAsync(model.MaPhanCa) : null;

        if (ca == null)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ca trực cần giải trình.");
        }
        else if (!InScope(ca.MaPhongBan, scopeDept))
        {
            ModelState.AddModelError(string.Empty, "Ca trực được chọn không thuộc bộ phận mà bạn quản lý.");
        }
        else if (ca.KetThucDuKien > DateTimeOffset.UtcNow)
        {
            // Ca chưa xong thì chưa có gì để giải trình — vẫn còn thời gian quẹt thẻ.
            ModelState.AddModelError(string.Empty, "Ca trực chưa kết thúc, chưa cần giải trình.");
        }
        else if (ca.KetThucDuKien < DateTimeOffset.UtcNow.AddDays(-SO_NGAY_GIAI_TRINH))
        {
            ModelState.AddModelError(string.Empty,
                $"Chỉ nhận giải trình trong vòng {SO_NGAY_GIAI_TRINH} ngày kể từ khi ca kết thúc.");
        }
        else
        {
            var now = DateTimeOffset.UtcNow;

            if (gioVao.HasValue && gioVao.Value > now)
            {
                ModelState.AddModelError(nameof(model.GioDeXuatVao), "Giờ vào đề xuất không được ở tương lai.");
            }

            if (gioRa.HasValue && gioRa.Value > now)
            {
                ModelState.AddModelError(nameof(model.GioDeXuatRa), "Giờ ra đề xuất không được ở tương lai.");
            }

            if (!gioVao.HasValue && !gioRa.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Phải đề xuất ít nhất một mốc giờ vào hoặc giờ ra.");
            }

            if (gioVao.HasValue && gioRa.HasValue && gioRa.Value <= gioVao.Value)
            {
                ModelState.AddModelError(nameof(model.GioDeXuatRa), "Giờ ra phải sau giờ vào.");
            }

            // Một ca chỉ mở một giải trình tại một thời điểm — trùng với unique
            // index uq_giai_trinh_dang_mo; bắt ở đây cho ra câu tiếng Việt.
            var daCoDangMo = await _context.GiaiTrinhChamCongs.AnyAsync(g =>
                g.MaPhanCa == ca.MaPhanCa &&
                (g.TrangThai == STATUS_CHO_QUAN_LY || g.TrangThai == STATUS_CHO_HR));

            if (daCoDangMo)
            {
                ModelState.AddModelError(string.Empty, "Ca này đang có giải trình chờ xử lý. Vui lòng chờ kết quả trước khi gửi tiếp.");
            }

            // Chặn giải trình nếu kỳ lương của ca trực đã khóa sổ hoặc đã thanh toán
            var thangCa = ca.NgayLamViec.Month;
            var namCa = ca.NgayLamViec.Year;
            var kyLuongDaKhoa = await _context.KyLuongs
                .AsNoTracking()
                .AnyAsync(k => (k.Thang == thangCa && k.Nam == namCa || (k.TuNgay <= ca.NgayLamViec && k.DenNgay >= ca.NgayLamViec))
                            && (k.TrangThai == SalaryConstants.PERIOD_LOCKED || k.TrangThai == "DA_THANH_TOAN"));

            if (kyLuongDaKhoa)
            {
                ModelState.AddModelError(string.Empty,
                    $"Kỳ lương tháng {thangCa:D2}/{namCa} đã khóa sổ tài chính. Cổng giải trình cho các ca trực trong kỳ này đã đóng.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadShiftOptionsAsync(model);
            if (ca != null)
            {
                model.TenNhanVien = ca.HoTen;
                model.MaNhanVien = ca.MaNhanVien;
                model.MoTaCa = $"{ca.TenCa} · {ca.NgayLamViec:dd/MM/yyyy} ({ca.GioBatDau:HH\\:mm}–{ca.GioKetThuc:HH\\:mm})";
                model.GioVaoHienTai = FormatGio(ca.GioCheckIn);
                model.GioRaHienTai = FormatGio(ca.GioCheckOut);
            }

            return View(model);
        }

        var giaiTrinh = new GiaiTrinhChamCong
        {
            MaPhanCa = ca!.MaPhanCa,
            MaNhanVien = ca.MaNhanVien,
            GioDeXuatVao = gioVao?.ToUniversalTime(),
            GioDeXuatRa = gioRa?.ToUniversalTime(),
            LyDo = model.LyDo,
            MinhChung = model.MinhChung,
            TrangThai = STATUS_CHO_QUAN_LY,
            NgayGui = DateTimeOffset.UtcNow
        };

        _context.GiaiTrinhChamCongs.Add(giaiTrinh);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Tạo giải trình chấm công #{RequestId} cho ca #{ShiftId} (NV {EmployeeId}).",
            giaiTrinh.MaGiaiTrinh, ca.MaPhanCa, ca.MaNhanVien);

        TempData["Success"] = $"Đã gửi giải trình #{giaiTrinh.MaGiaiTrinh}. Quản lý bộ phận sẽ xác minh trước.";
        return RedirectToAction(nameof(Index));
    }

    // =====================================================================
    //  CẤP 1 — QUẢN LÝ XÁC MINH  (CHO_QUAN_LY → CHO_HR | TU_CHOI)
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.VerifyAttendanceExplanations)]
    public async Task<IActionResult> ManagerVerify(int id, bool dongY, string? yKien)
    {
        if (!User.IsInRole("Manager") && !User.IsInRole("Admin"))
        {
            return Finish(false, "Chỉ Quản lý bộ phận mới xác minh được giải trình.");
        }

        var yKienChuanHoa = NormalizeYKien(yKien);
        if (yKienChuanHoa.Error != null)
        {
            return Finish(false, yKienChuanHoa.Error);
        }

        if (!dongY && string.IsNullOrWhiteSpace(yKienChuanHoa.Value))
        {
            return Finish(false, "Từ chối giải trình phải nêu lý do để nhân viên biết đường bổ sung.");
        }

        var giaiTrinh = await _context.GiaiTrinhChamCongs
            .AsNoTracking()
            .Include(g => g.NhanVien)
            .FirstOrDefaultAsync(g => g.MaGiaiTrinh == id);

        if (giaiTrinh == null)
        {
            return Finish(false, "Không tìm thấy giải trình.");
        }

        var scopeDept = GetManagerDepartmentScope();
        if (!InScope(giaiTrinh.NhanVien?.MaPhongBan, scopeDept))
        {
            return Finish(false, "Giải trình này không thuộc bộ phận mà bạn quản lý.");
        }

        // Quản lý không tự xác minh giải trình của chính mình — bước HR vẫn còn,
        // nhưng để nguyên thì một dòng mã là tự thông qua được cấp 1.
        var currentEmployeeId = GetCurrentEmployeeId();
        if (currentEmployeeId.HasValue && currentEmployeeId.Value == giaiTrinh.MaNhanVien)
        {
            return Finish(false, "Bạn không thể tự xác minh giải trình của chính mình. Vui lòng chuyển HR xử lý.");
        }

        var nguoiXacMinhId = currentEmployeeId;
        var trangThaiMoi = dongY ? STATUS_CHO_HR : STATUS_TU_CHOI;

        var daCapNhat = await _context.GiaiTrinhChamCongs
            .Where(g => g.MaGiaiTrinh == id && g.TrangThai == STATUS_CHO_QUAN_LY)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.TrangThai, trangThaiMoi)
                .SetProperty(g => g.MaQuanLyXacMinh, nguoiXacMinhId)
                .SetProperty(g => g.NgayQuanLyXacMinh, DateTimeOffset.UtcNow)
                .SetProperty(g => g.YKienQuanLy, yKienChuanHoa.Value));

        if (daCapNhat != 1)
        {
            return Finish(false, "Giải trình này đã được xử lý trước đó. Vui lòng tải lại danh sách.");
        }

        _logger.LogInformation(
            "Quản lý {Decision} giải trình #{RequestId}.", dongY ? "xác minh" : "từ chối", id);

        return Finish(true,
            dongY
                ? $"Đã xác minh giải trình #{id}. Hồ sơ chuyển sang HR xác nhận."
                : $"Đã từ chối giải trình #{id}.",
            new { maGiaiTrinh = id, trangThai = trangThaiMoi });
    }

    // =====================================================================
    //  CẤP 2 — HR XÁC NHẬN  (CHO_HR → DA_DUYET | TU_CHOI) + GHI cham_cong
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.VerifyAttendanceExplanations)]
    public async Task<IActionResult> HrConfirm(int id, bool dongY, string? yKien)
    {
        if (User.IsInRole("Manager"))
        {
            return Finish(false, "Quản lý xử lý bước xác minh của bộ phận; bước xác nhận thuộc người được cấp quyền xử lý toàn khách sạn.");
        }

        var yKienChuanHoa = NormalizeYKien(yKien);
        if (yKienChuanHoa.Error != null)
        {
            return Finish(false, yKienChuanHoa.Error);
        }

        if (!dongY && string.IsNullOrWhiteSpace(yKienChuanHoa.Value))
        {
            return Finish(false, "Từ chối giải trình phải nêu lý do.");
        }

        var giaiTrinh = await _context.GiaiTrinhChamCongs
            .AsNoTracking()
            .Include(g => g.PhanCa)
            .FirstOrDefaultAsync(g => g.MaGiaiTrinh == id);

        if (giaiTrinh == null)
        {
            return Finish(false, "Không tìm thấy giải trình.");
        }

        if (giaiTrinh.TrangThai != STATUS_CHO_HR)
        {
            return Finish(false, "Giải trình chưa được Quản lý xác minh nên HR chưa xác nhận được.");
        }

        var nguoiXacNhanId = GetCurrentEmployeeId();

        if (!dongY)
        {
            var soDongTuChoi = await _context.GiaiTrinhChamCongs
                .Where(g => g.MaGiaiTrinh == id && g.TrangThai == STATUS_CHO_HR)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.TrangThai, STATUS_TU_CHOI)
                    .SetProperty(g => g.MaHrXacNhan, nguoiXacNhanId)
                    .SetProperty(g => g.NgayHrXacNhan, DateTimeOffset.UtcNow)
                    .SetProperty(g => g.YKienHr, yKienChuanHoa.Value));

            return Finish(soDongTuChoi == 1,
                soDongTuChoi == 1
                    ? $"Đã từ chối giải trình #{id}."
                    : "Giải trình này vừa được người khác xử lý.",
                soDongTuChoi == 1 ? new { maGiaiTrinh = id, trangThai = STATUS_TU_CHOI } : null);
        }

        if (giaiTrinh.PhanCa == null)
        {
            return Finish(false, "Ca trực của giải trình không còn tồn tại — không ghi được bảng công.");
        }

        var ca = giaiTrinh.PhanCa;
        var thangCa = ca.NgayLamViec.Month;
        var namCa = ca.NgayLamViec.Year;

        // Kiểm tra xem kỳ lương của ca trực này đã khóa sổ tài chính hay chưa
        var kyLuongDaKhoa = await _context.KyLuongs
            .AsNoTracking()
            .AnyAsync(k => (k.Thang == thangCa && k.Nam == namCa || (k.TuNgay <= ca.NgayLamViec && k.DenNgay >= ca.NgayLamViec))
                        && (k.TrangThai == SalaryConstants.PERIOD_LOCKED || k.TrangThai == "DA_THANH_TOAN"));

        // Thu thập thông số chấm công cũ để tính toán chênh lệch truy lĩnh nếu kỳ đã khóa
        var oldChamCong = await _context.ChamCongs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.MaPhanCa == ca.MaPhanCa);

        var oldPhutDiTre = oldChamCong?.PhutDiTre ?? 0;
        var oldPhutVeSom = oldChamCong?.PhutVeSom ?? 0;
        var oldOtHours = oldChamCong?.GioTangCaOt ?? 0m;
        var oldHadPunch = oldChamCong?.GioCheckIn != null && oldChamCong?.GioCheckOut != null;

        var eventMsg = "";
        var newEventAmount = 0m;
        var oldEventAmount = 0m;
        var newPhutDiTre = 0;
        var newPhutVeSom = 0;
        var newOtHours = 0m;
        var newHadPunch = false;
        decimal? historicalSalary = null;

        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Resolve before committing attendance: an ambiguous salary must not leave a half-applied approval.
                if (kyLuongDaKhoa)
                {
                    var contracts = await _context.HopDongs.AsNoTracking()
                        .Include(h => h.PhuLucLuongs)
                        .Where(h => h.MaNhanVien == ca.MaNhanVien && h.NgayBatDau <= ca.NgayLamViec &&
                            (!h.NgayKetThuc.HasValue || h.NgayKetThuc >= ca.NgayLamViec))
                        .ToListAsync();
                    var salary = EffectiveSalaryResolver.Resolve(contracts, ca.NgayLamViec)
                        ?? throw new InvalidOperationException($"Thiếu hợp đồng làm căn cứ truy lĩnh ngày {ca.NgayLamViec:dd/MM/yyyy}.");
                    historicalSalary = salary.MonthlySalary;
                }

                var chamCong = await _context.ChamCongs
                    .FirstOrDefaultAsync(c => c.MaPhanCa == ca.MaPhanCa);

                var truoc = chamCong == null ? null : DescribeAttendance(chamCong);

                if (chamCong == null)
                {
                    chamCong = new ChamCong
                    {
                        MaPhanCa = ca.MaPhanCa,
                        PhuongThucCheckIn = "MANUAL"
                    };
                    _context.ChamCongs.Add(chamCong);
                }

                // Chỉ ghi đè mốc giờ mà giải trình có đề xuất; mốc còn lại giữ nguyên.
                if (giaiTrinh.GioDeXuatVao.HasValue)
                {
                    chamCong.GioCheckIn = giaiTrinh.GioDeXuatVao.Value;
                }

                if (giaiTrinh.GioDeXuatRa.HasValue)
                {
                    chamCong.GioCheckOut = giaiTrinh.GioDeXuatRa.Value;
                }

                // Tính lại từ đầu chứ không cộng dồn: phút trễ/về sớm và OT phải
                // suy ra từ cặp giờ cuối cùng, nếu không sẽ đếm hai lần.
                chamCong.PhutDiTre = chamCong.GioCheckIn.HasValue
                    ? AttendanceCalculator.CalculateLateMinutes(ca.BatDauDuKien, chamCong.GioCheckIn.Value)
                    : 0;
                chamCong.PhutVeSom = chamCong.GioCheckOut.HasValue
                    ? AttendanceCalculator.CalculateEarlyMinutes(ca.KetThucDuKien, chamCong.GioCheckOut.Value)
                    : 0;
                chamCong.GioTangCaOt = chamCong.GioCheckOut.HasValue
                    ? AttendanceCalculator.CalculateOvertimeHours(ca.KetThucDuKien, chamCong.GioCheckOut.Value)
                    : 0m;
                chamCong.TrangThai = AttendanceCalculator.ResolveStatus(chamCong.PhutDiTre, chamCong.PhutVeSom);

                newPhutDiTre = chamCong.PhutDiTre;
                newPhutVeSom = chamCong.PhutVeSom;
                newOtHours = chamCong.GioTangCaOt;
                newHadPunch = chamCong.GioCheckIn.HasValue && chamCong.GioCheckOut.HasValue;

                // Xoá dấu vết chấm công trước đó để bảng công thể hiện đây là
                // bản ghi do HR chỉnh tay, có hồ sơ giải trình chống lưng.
                chamCong.PhuongThucCheckIn = "MANUAL";
                chamCong.ToaDoCheckIn = null;
                chamCong.ThietBiCheckIn = null;

                var daCapNhat = await _context.GiaiTrinhChamCongs
                    .Where(g => g.MaGiaiTrinh == id && g.TrangThai == STATUS_CHO_HR)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(g => g.TrangThai, STATUS_DA_DUYET)
                        .SetProperty(g => g.MaHrXacNhan, nguoiXacNhanId)
                        .SetProperty(g => g.NgayHrXacNhan, DateTimeOffset.UtcNow)
                        .SetProperty(g => g.YKienHr, yKienChuanHoa.Value));

                if (daCapNhat != 1)
                {
                    throw new InvalidOperationException("Giải trình vừa được người khác xử lý. Không ghi bảng công.");
                }

                // =====================================================================
                //  LOGIC ĐỒNG BỘ CÔNG & QUYẾT TOÁN SỰ KIỆN (NẾU CA THUỘC SỰ KIỆN)
                // =====================================================================
                var phanCong = await _context.PhanCongDuAns
                    .Include(pc => pc.DuAn)
                    .Include(pc => pc.CongThucTes)
                    .FirstOrDefaultAsync(pc => pc.MaPhanCa == ca.MaPhanCa);

                if (phanCong?.DuAn != null && phanCong.DuAn.TinhTheoGio)
                {
                    var duAn = phanCong.DuAn;
                    if (duAn.TrangThai != "HOAN_THANH")
                    {
                        if (phanCong.CongThucTes.Count > 0)
                        {
                            var window = HourlyEventRules.AttendanceWindow(duAn, phanCong, DateTimeOffset.UtcNow);
                            if (window.HasValue)
                            {
                                var cong = phanCong.CongThucTes.First();
                                cong.BatDau = window.Value.Start;
                                cong.KetThuc = window.Value.End;
                                phanCong.DanhGia = HourlyEventRules.AutomaticRating(phanCong)!;
                                eventMsg = $" Đồng thời cập nhật giờ công sự kiện '{duAn.TenDuAn}'.";
                            }
                        }
                    }
                    else
                    {
                        var quyetToan = await _context.QuyetToanSuKiens
                            .Include(q => q.KyChiTra)
                            .FirstOrDefaultAsync(q => q.MaDuAn == duAn.MaDuAn && q.MaNhanVien == ca.MaNhanVien);

                        if (quyetToan != null)
                        {
                            oldEventAmount = quyetToan.SoTien;
                            var actualWindow = HourlyEventRules.ResolveActualWindow(
                                phanCong.BatDau, phanCong.KetThuc,
                                ca.BatDauDuKien, ca.KetThucDuKien,
                                chamCong.GioCheckIn, chamCong.GioCheckOut);

                            if (actualWindow.HasValue)
                            {
                                var (start, end, _) = actualWindow.Value;
                                var hourlyRate = await EventRateConfig.ReadAsync(_context);
                                var nhanVien = await _context.NhanViens
                                    .Include(e => e.HopDongs).ThenInclude(h => h.PhuLucLuongs)
                                    .FirstOrDefaultAsync(e => e.MaNhanVien == ca.MaNhanVien);

                                var slices = HourlyEventRules.Price(start, end, nhanVien?.HopDongs ?? [], hourlyRate);
                                newEventAmount = HourlyEventRules.Amount(slices);
                                var newMinutes = slices.Sum(s => s.Minutes);

                                if (quyetToan.KyChiTra != null &&
                                    quyetToan.KyChiTra.TrangThai != SalaryConstants.PERIOD_LOCKED &&
                                    quyetToan.KyChiTra.TrangThai != "DA_THANH_TOAN" &&
                                    !quyetToan.KyChiTra.NgayDuyet.HasValue)
                                {
                                    quyetToan.SoPhut = newMinutes;
                                    quyetToan.SoTien = newEventAmount;
                                    quyetToan.CanCuTinh = JsonSerializer.Serialize(slices);

                                    if (quyetToan.MaChiTiet.HasValue)
                                    {
                                        var detail = await _context.ChiTietPhieuLuongs
                                            .FirstOrDefaultAsync(d => d.MaChiTiet == quyetToan.MaChiTiet.Value);
                                        if (detail != null)
                                        {
                                            detail.SoTien = newEventAmount;
                                        }
                                    }
                                    eventMsg = $" Đã đồng bộ quyết toán sự kiện '{duAn.TenDuAn}' ({newMinutes:N0} phút, {newEventAmount:N0}đ).";
                                }
                            }
                        }
                    }
                }

                _context.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    MaTaiKhoanThucHien = nguoiXacNhanId,
                    TenNguoiThucHien = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Không xác định",
                    TenTaiKhoanBiTacDong = "NV-" + giaiTrinh.MaNhanVien,
                    HanhDong = "SUA_CONG_GIAI_TRINH",
                    NoiDung = $"Xác nhận giải trình #{giaiTrinh.MaGiaiTrinh} và điều chỉnh giờ công ca #{ca.MaPhanCa}.{(kyLuongDaKhoa ? " [Kỳ đã khóa sổ]" : "")}",
                    GiaTriCu = truoc,
                    GiaTriMoi = DescribeAttendance(chamCong),
                    LyDo = giaiTrinh.LyDo,
                    DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ThoiGian = DateTimeOffset.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            _logger.LogInformation(
                "HR xác nhận giải trình #{RequestId}; đã ghi lại chấm công ca #{ShiftId}.",
                id, ca.MaPhanCa);

            var payrollMsg = "";
            var callerName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "HR";

            if (!kyLuongDaKhoa)
            {
                // =====================================================================
                //  LOGIC TỰ ĐỘNG TÍNH LẠI PHIẾU LƯƠNG NHÁP (NẾU ĐÃ CÓ PHIẾU LƯƠNG TRONG KỲ)
                // =====================================================================
                var draftPayslip = await _context.PhieuLuongs
                    .Include(p => p.KyLuong)
                    .FirstOrDefaultAsync(p => p.MaNhanVien == ca.MaNhanVien
                        && p.KyLuong != null
                        && ((p.KyLuong.Thang == thangCa && p.KyLuong.Nam == namCa) ||
                            (p.KyLuong.TuNgay <= ca.NgayLamViec && p.KyLuong.DenNgay >= ca.NgayLamViec))
                        && p.KyLuong.TrangThai != SalaryConstants.PERIOD_LOCKED
                        && p.KyLuong.TrangThai != "DA_THANH_TOAN"
                        && !p.KyLuong.NgayDuyet.HasValue);

                if (draftPayslip != null)
                {
                    try
                    {
                        await _payrollService.RecalculateEmployeeAsync(
                            draftPayslip.MaPhieuLuong,
                            callerName,
                            nguoiXacNhanId);
                        payrollMsg = $" Đã tự động tính toán lại phiếu lương tháng {thangCa:D2}/{namCa} cho nhân viên.";
                    }
                    catch (Exception exRecalc)
                    {
                        _logger.LogWarning(exRecalc, "Không thể tự động tính lại phiếu lương #{PayslipId} sau khi duyệt giải trình.", draftPayslip.MaPhieuLuong);
                        payrollMsg = " Dữ liệu công đã cập nhật; vui lòng kiểm tra và tính lại phiếu lương trên màn hình Bảng lương.";
                    }
                }
                else
                {
                    payrollMsg = $" Dữ liệu công chuẩn sẽ được áp dụng khi chạy tính lương tháng {thangCa:D2}/{namCa}.";
                }
            }
            else
            {
                // =====================================================================
                //  LOGIC TRUY LĨNH KHI KỲ LƯƠNG ĐÃ KHÓA SỔ TÀI CHÍNH
                // =====================================================================
                var fineConfig = await _context.CauHinhKhauTrus.AsNoTracking()
                    .Where(x => x.MaKhoanTru == 5)
                    .Select(x => x.TyLeHoacDinhMuc)
                    .FirstOrDefaultAsync();
                var finePerMinute = fineConfig > 0 ? fineConfig : 5000m;

                var luongCoBan = historicalSalary!.Value;
                var donGiaNgay = luongCoBan > 0 ? luongCoBan / (decimal)SalaryConstants.STANDARD_WORKING_DAYS : 0m;
                var donGiaGio = donGiaNgay > 0 ? donGiaNgay / (decimal)SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY : 0m;

                var fineLateRefund = Math.Max(0, oldPhutDiTre - newPhutDiTre) * finePerMinute;
                var oldEarlyFine = oldPhutVeSom > 15 ? oldPhutVeSom * finePerMinute : 0m;
                var newEarlyFine = newPhutVeSom > 15 ? newPhutVeSom * finePerMinute : 0m;
                var fineEarlyRefund = Math.Max(0m, oldEarlyFine - newEarlyFine);

                var otAdded = Math.Max(0m, newOtHours - oldOtHours);
                var otRate = ca.NgayLamViec.DayOfWeek == DayOfWeek.Sunday ? 2.0m : 1.5m;
                var otAddedMoney = Math.Round(otAdded * donGiaGio * otRate, MidpointRounding.AwayFromZero);

                var workDayMoney = (!oldHadPunch && newHadPunch) ? Math.Round(donGiaNgay, MidpointRounding.AwayFromZero) : 0m;
                var eventDiff = Math.Max(0m, newEventAmount - oldEventAmount);

                var deltaMoney = fineLateRefund + fineEarlyRefund + otAddedMoney + workDayMoney + eventDiff;

                if (deltaMoney > 0)
                {
                    var openPeriod = await _context.KyLuongs
                        .Where(k => k.TrangThai != SalaryConstants.PERIOD_LOCKED
                                 && k.TrangThai != "DA_THANH_TOAN"
                                 && !k.NgayDuyet.HasValue)
                        .OrderBy(k => k.Nam).ThenBy(k => k.Thang)
                        .FirstOrDefaultAsync();

                    if (openPeriod != null)
                    {
                        var openPayslip = await _context.PhieuLuongs
                            .FirstOrDefaultAsync(p => p.MaKyLuong == openPeriod.MaKyLuong && p.MaNhanVien == ca.MaNhanVien);

                        if (openPayslip != null)
                        {
                            try
                            {
                                await _payrollService.AdjustSalaryItemAsync(
                                    openPayslip.MaPhieuLuong,
                                    "PHU_CAP",
                                    $"Truy lĩnh giải trình ca {ca.NgayLamViec:dd/MM/yyyy}",
                                    deltaMoney,
                                    $"Truy lĩnh do duyệt giải trình #{giaiTrinh.MaGiaiTrinh} sau khi kỳ {thangCa:D2}/{namCa} đã khóa sổ.",
                                    callerName,
                                    nguoiXacNhanId);
                                payrollMsg = $" Do kỳ {thangCa:D2}/{namCa} đã khóa sổ, hệ thống đã tự động kết chuyển khoản truy lĩnh {deltaMoney:N0}đ vào phiếu lương tháng {openPeriod.Thang:D2}/{openPeriod.Nam}.";
                            }
                            catch (Exception exAdj)
                            {
                                _logger.LogWarning(exAdj, "Không thể kết chuyển truy lĩnh #{DisputeId}", id);
                                payrollMsg = $" Khoản chênh lệch truy lĩnh ({deltaMoney:N0}đ) cần HR ghi nhận tay vào kỳ mở tiếp theo.";
                            }
                        }
                        else
                        {
                            payrollMsg = $" Khoản chênh lệch truy lĩnh ({deltaMoney:N0}đ) sẽ được tính khi lập phiếu lương kỳ mở {openPeriod.Thang:D2}/{openPeriod.Nam}.";
                        }
                    }
                    else
                    {
                        payrollMsg = $" Khoản chênh lệch truy lĩnh ({deltaMoney:N0}đ) đã được ghi nhận nhật ký (hiện chưa có kỳ lương mới mở).";
                    }
                }
                else
                {
                    payrollMsg = " Đã duyệt cập nhật bảng công lịch sử (không phát sinh chênh lệch tiền công).";
                }
            }

            return Finish(true,
                $"Đã xác nhận giải trình #{id} và cập nhật bảng chấm công.{eventMsg}{payrollMsg}",
                new { maGiaiTrinh = id, trangThai = STATUS_DA_DUYET });
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Không xác nhận được giải trình #{RequestId}.", id);
            return Finish(false, "Không xác nhận được giải trình: " + ex.Message);
        }
    }

    // =====================================================================
    //  HÀM PHỤ
    // =====================================================================

    private sealed record CaTrucInfo(
        int MaPhanCa,
        int MaNhanVien,
        int? MaPhongBan,
        string HoTen,
        string TenCa,
        DateOnly NgayLamViec,
        TimeOnly GioBatDau,
        TimeOnly GioKetThuc,
        DateTimeOffset BatDauDuKien,
        DateTimeOffset KetThucDuKien,
        DateTimeOffset? GioCheckIn,
        DateTimeOffset? GioCheckOut);

    private async Task<CaTrucInfo?> LoadShiftAsync(int maPhanCa)
    {
        return await _context.PhanCas
            .AsNoTracking()
            .Where(p => p.MaPhanCa == maPhanCa)
            .Select(p => new CaTrucInfo(
                p.MaPhanCa,
                p.MaNhanVien,
                p.NhanVien == null ? null : p.NhanVien.MaPhongBan,
                p.NhanVien == null ? "NV #" + p.MaNhanVien : (p.NhanVien.Ho + " " + p.NhanVien.Ten).Trim(),
                p.CaLamViec == null ? "—" : p.CaLamViec.TenCa,
                p.NgayLamViec,
                p.CaLamViec == null ? TimeOnly.MinValue : p.CaLamViec.GioBatDau,
                p.CaLamViec == null ? TimeOnly.MinValue : p.CaLamViec.GioKetThuc,
                p.BatDauDuKien,
                p.KetThucDuKien,
                p.ChamCong == null ? null : p.ChamCong.GioCheckIn,
                p.ChamCong == null ? null : p.ChamCong.GioCheckOut))
            .FirstOrDefaultAsync();
    }

    private async Task LoadShiftOptionsAsync(GiaiTrinhCreateViewModel model)
    {
        var scopeDept = GetManagerDepartmentScope();
        var now = DateTimeOffset.UtcNow;
        var tuNgay = DateOnly.FromDateTime(DateTime.Today.AddDays(-SO_NGAY_GIAI_TRINH));

        var caQuery = _context.PhanCas
            .AsNoTracking()
            .Include(p => p.NhanVien)!.ThenInclude(n => n!.PhongBan)
            .Include(p => p.CaLamViec)
            .Where(p => p.KetThucDuKien <= now && p.NgayLamViec >= tuNgay);

        if (scopeDept.HasValue)
        {
            caQuery = scopeDept.Value <= 0
                ? caQuery.Where(_ => false)
                : caQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == scopeDept.Value);
        }

        var cacCa = await caQuery
            .OrderByDescending(p => p.NgayLamViec)
            .ThenBy(p => p.BatDauDuKien)
            .Select(p => new
            {
                p.MaPhanCa,
                p.MaNhanVien,
                HoTen = p.NhanVien == null ? "NV #" + p.MaNhanVien : (p.NhanVien.Ho + " " + p.NhanVien.Ten).Trim(),
                TenCa = p.CaLamViec == null ? "Ca" : p.CaLamViec.TenCa,
                GioBatDau = p.CaLamViec == null ? TimeOnly.MinValue : p.CaLamViec.GioBatDau,
                GioKetThuc = p.CaLamViec == null ? TimeOnly.MinValue : p.CaLamViec.GioKetThuc,
                p.NgayLamViec
            })
            .ToListAsync();

        // Lọc bỏ các ca thuộc kỳ lương đã khóa sổ hoặc đã thanh toán
        var lockedPeriods = await _context.KyLuongs
            .AsNoTracking()
            .Where(k => k.TrangThai == SalaryConstants.PERIOD_LOCKED || k.TrangThai == "DA_THANH_TOAN")
            .Select(k => new { k.Thang, k.Nam, k.TuNgay, k.DenNgay })
            .ToListAsync();

        if (lockedPeriods.Count > 0)
        {
            cacCa = cacCa
                .Where(p => !lockedPeriods.Any(k =>
                    (k.Thang == p.NgayLamViec.Month && k.Nam == p.NgayLamViec.Year) ||
                    (k.TuNgay <= p.NgayLamViec && k.DenNgay >= p.NgayLamViec)))
                .ToList();
        }

        model.CaOptions = cacCa
            .Select(p => new SelectListItem
            {
                Value = p.MaPhanCa.ToString(),
                Text = $"NV-{p.MaNhanVien} {p.HoTen} · {p.TenCa} · {p.NgayLamViec:dd/MM} ({p.GioBatDau:HH\\:mm}–{p.GioKetThuc:HH\\:mm})",
                Selected = p.MaPhanCa == model.MaPhanCa
            })
            .ToList();
    }

    private static (string? Value, string? Error) NormalizeYKien(string? yKien)
    {
        var value = string.IsNullOrWhiteSpace(yKien) ? null : yKien.Trim();
        return value is { Length: > MAX_Y_KIEN }
            ? (null, $"Ý kiến không được vượt quá {MAX_Y_KIEN} ký tự.")
            : (value, null);
    }

    private static string FormatGio(DateTimeOffset? value) =>
        value?.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy") ?? "—";

    /// <summary>
    /// Gán độ lệch +07:00 cho giá trị người dùng nhập. Giờ trong CSDL đều là
    /// UTC, nhưng <c>datetime-local</c> chỉ trả về giờ đồng hồ treo tường — nếu
    /// để nguyên thì máy chủ tự gắn múi giờ của nó, sai lệch 7 tiếng trên máy
    /// chủ chạy UTC.
    /// </summary>
    private static DateTimeOffset? ToVietnamOffset(DateTime? value) =>
        value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(7))
            : null;

    private static string DescribeAttendance(ChamCong chamCong) =>
        $"Vào: {FormatGio(chamCong.GioCheckIn)}; Ra: {FormatGio(chamCong.GioCheckOut)}; " +
        $"Trễ: {chamCong.PhutDiTre} phút; Sớm: {chamCong.PhutVeSom} phút; " +
        $"OT: {chamCong.GioTangCaOt:0.##} giờ; Trạng thái: {chamCong.TrangThai}";

    private static bool IsKnownStatus(string? status) =>
        status is STATUS_CHO_QUAN_LY or STATUS_CHO_HR or STATUS_DA_DUYET or STATUS_TU_CHOI or STATUS_DA_HUY;

    private static IReadOnlyList<SelectListItem> BuildStatusOptions(string? selected) =>
    [
        new SelectListItem { Value = string.Empty, Text = "-- Tất cả trạng thái --", Selected = string.IsNullOrWhiteSpace(selected) },
        new SelectListItem { Value = STATUS_CHO_QUAN_LY, Text = "Chờ Quản lý xác minh", Selected = selected == STATUS_CHO_QUAN_LY },
        new SelectListItem { Value = STATUS_CHO_HR, Text = "Chờ HR xác nhận", Selected = selected == STATUS_CHO_HR },
        new SelectListItem { Value = STATUS_DA_DUYET, Text = "Đã duyệt", Selected = selected == STATUS_DA_DUYET },
        new SelectListItem { Value = STATUS_TU_CHOI, Text = "Từ chối", Selected = selected == STATUS_TU_CHOI },
        new SelectListItem { Value = STATUS_DA_HUY, Text = "Đã rút", Selected = selected == STATUS_DA_HUY }
    ];

    private static IReadOnlyList<SelectListItem> BuildLoaiCongViecOptions(string? selected) =>
    [
        new SelectListItem { Value = string.Empty, Text = "-- Tất cả loại công việc --", Selected = string.IsNullOrWhiteSpace(selected) },
        new SelectListItem { Value = "CA_THUONG", Text = "Ca làm việc 24/7 (Ca thường)", Selected = selected == "CA_THUONG" },
        new SelectListItem { Value = "SU_KIEN", Text = "Sự kiện / Tiệc theo giờ", Selected = selected == "SU_KIEN" }
    ];

    private static bool InScope(int? employeeDeptId, int? scopeDeptId)
    {
        if (!scopeDeptId.HasValue)
        {
            return true;
        }

        return scopeDeptId.Value > 0 && employeeDeptId == scopeDeptId.Value;
    }

    private int? GetManagerDepartmentScope() => User.GetManagedDepartmentScope();

    private int? GetCurrentEmployeeId() =>
        int.TryParse(User.FindFirstValue("EmployeeId"), out var employeeId) ? employeeId : null;

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// API trả về số lượng giải trình chấm công đang chờ xử lý thực tế của user hiện tại (để cập nhật Badge).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PendingCount()
    {
        var scopeDept = GetManagerDepartmentScope();
        var laHr = User.IsInRole("HR") || User.IsInRole("IT");

        var query = _context.GiaiTrinhChamCongs.AsNoTracking();

        if (User.IsInRole("Admin"))
        {
            query = query.Where(g => g.TrangThai == STATUS_CHO_QUAN_LY || g.TrangThai == STATUS_CHO_HR);
        }
        else if (laHr)
        {
            query = query.Where(g => g.TrangThai == STATUS_CHO_HR);
        }
        else if (User.IsInRole("Manager"))
        {
            if (!scopeDept.HasValue || scopeDept.Value <= 0)
                return Json(new { count = 0 });

            query = query.Where(g => g.TrangThai == STATUS_CHO_QUAN_LY &&
                                     g.NhanVien != null &&
                                     g.NhanVien.MaPhongBan == scopeDept.Value);
        }
        else
        {
            return Json(new { count = 0 });
        }

        var count = await query.CountAsync();
        return Json(new { count });
    }

    /// <summary>
    /// Trả kết quả theo đúng đường mà yêu cầu đến: AJAX nhận JSON để cập nhật
    /// hàng trong bảng tại chỗ, form thường nhận redirect kèm thông báo.
    /// </summary>
    private IActionResult Finish(bool thanhCong, string thongDiep, object? duLieu = null)
    {
        if (IsAjaxRequest())
        {
            if (!thanhCong)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return Json(new { success = thanhCong, message = thongDiep, data = duLieu });
        }

        TempData[thanhCong ? "Success" : "Error"] = thongDiep;
        return RedirectToAction(nameof(Index));
    }
}
