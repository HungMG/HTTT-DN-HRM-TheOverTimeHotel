using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
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
[Authorize(Policy = PermissionCodes.AssignShifts)]
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
    private readonly ILogger<GiaiTrinhChamCongController> _logger;

    public GiaiTrinhChamCongController(AppDbContext context, ILogger<GiaiTrinhChamCongController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =====================================================================
    //  INDEX — DANH SÁCH GIẢI TRÌNH
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, string? keyword, int page = 1)
    {
        trangThai = IsKnownStatus(trangThai) ? trangThai : null;
        keyword = keyword?.Trim();
        page = Math.Max(1, page);

        var laHr = User.IsInRole("HR") || User.IsInRole("Admin") || User.IsInRole("IT");
        var scopeDept = GetManagerDepartmentScope();

        var baseQuery = _context.GiaiTrinhChamCongs
            .AsNoTracking()
            .Include(g => g.NhanVien)!.ThenInclude(n => n!.PhongBan)
            .Include(g => g.PhanCa)!.ThenInclude(p => p!.CaLamViec)
            .Include(g => g.PhanCa)!.ThenInclude(p => p!.ChamCong)
            .Include(g => g.QuanLyXacMinh)
            .Include(g => g.HrXacNhan)
            .AsQueryable();

        if (scopeDept.HasValue)
        {
            baseQuery = scopeDept.Value <= 0
                ? baseQuery.Where(_ => false)
                : baseQuery.Where(g => g.NhanVien != null && g.NhanVien.MaPhongBan == scopeDept.Value);
        }

        var visibleItems = await this.SearchDataAsync($"attendance-explanations:scope:{scopeDept}", () => baseQuery.ToListAsync());
        baseQuery = visibleItems.AsQueryable();
        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(g => g.TrangThai == trangThai);
        }

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
            .OrderBy(g => g.TrangThai == STATUS_CHO_QUAN_LY || g.TrangThai == STATUS_CHO_HR ? 0 : 1)
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

        var model = new GiaiTrinhIndexViewModel
        {
            TrangThai = trangThai,
            Keyword = keyword,
            TrangThaiOptions = BuildStatusOptions(trangThai),
            Items = rows.Select(r => new GiaiTrinhItemViewModel
            {
                MaGiaiTrinh = r.MaGiaiTrinh,
                MaPhanCa = r.MaPhanCa,
                MaNhanVien = r.MaNhanVien,
                TenNhanVien = r.TenNhanVien,
                TenPhongBan = r.TenPhongBan,
                NgayLamViec = r.NgayLamViec,
                TenCa = r.TenCa,
                GioCa = r.TenCa == "—" ? "—" : $"{r.GioBatDau:HH\\:mm} – {r.GioKetThuc:HH\\:mm}",
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
                NgayGui = r.NgayGui
            }).ToList(),
            TongGiaiTrinh = baseQuery.Count(),
            ChoQuanLy = baseQuery.Count(g => g.TrangThai == STATUS_CHO_QUAN_LY),
            ChoHr = baseQuery.Count(g => g.TrangThai == STATUS_CHO_HR),
            DaDuyet = baseQuery.Count(g => g.TrangThai == STATUS_DA_DUYET),
            TuChoi = baseQuery.Count(g => g.TrangThai == STATUS_TU_CHOI),
            CoQuyenQuanLy = User.IsInRole("Manager") || User.IsInRole("Admin"),
            CoQuyenHr = laHr,
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
    public async Task<IActionResult> HrConfirm(int id, bool dongY, string? yKien)
    {
        if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
        {
            return Finish(false, "Chỉ Phòng HR mới xác nhận được giải trình.");
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

        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

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

                _context.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    MaTaiKhoanThucHien = nguoiXacNhanId,
                    TenNguoiThucHien = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Không xác định",
                    TenTaiKhoanBiTacDong = "NV-" + giaiTrinh.MaNhanVien,
                    HanhDong = "SUA_CONG_GIAI_TRINH",
                    NoiDung = $"Xác nhận giải trình #{giaiTrinh.MaGiaiTrinh} và điều chỉnh giờ công ca #{ca.MaPhanCa}.",
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

            return Finish(true,
                $"Đã xác nhận giải trình #{id} và cập nhật bảng chấm công. Thay đổi được ghi vào nhật ký quản trị.",
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
