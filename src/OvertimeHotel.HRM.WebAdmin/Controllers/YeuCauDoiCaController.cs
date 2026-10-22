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
/// Quản lý yêu cầu đổi ca giữa hai nhân viên (PRD 3.4).
///
/// <para>
/// <b>Ai làm gì.</b> Nhân viên nộp yêu cầu từ Mobile App; Quản lý bộ phận và
/// Phòng HR duyệt trên Web Admin. Ở đây cũng cho phép Quản lý/HR <b>tạo hộ</b>
/// yêu cầu — thực tế khách sạn vẫn nhận đề nghị đổi ca qua điện thoại hoặc giấy,
/// và bắt buộc phải ghi lại được vì sao lịch đổi.
/// </para>
///
/// <para>
/// <b>Vì sao duyệt lại phải kiểm tra lần nữa.</b> Từ lúc nhân viên gửi đến lúc
/// Quản lý bấm duyệt có thể đã vài ngày, trong khoảng đó hai người có thể được
/// xếp thêm ca khác. Kiểm tra đúng một lần ở bước tạo thì yêu cầu cũ vẫn duyệt
/// được và tạo ra lịch vi phạm khoảng nghỉ.
/// </para>
/// </summary>
[Authorize(Policy = PermissionCodes.AssignShifts)]
public class YeuCauDoiCaController : Controller
{
    private const string STATUS_CHO_DUYET = "CHO_DUYET";
    private const string STATUS_DA_DUYET = "DA_DUYET";
    private const string STATUS_TU_CHOI = "TU_CHOI";
    private const string STATUS_DA_HUY = "DA_HUY";

    /// <summary>
    /// Số ngày tới được phép chọn ca khi tạo yêu cầu. Lịch phân ca lập theo tuần;
    /// 30 ngày đủ để phủ trọn tháng đang xếp mà danh sách chọn vẫn đọc được.
    /// </summary>
    private const int SO_NGAY_TOI_CHON_CA = 30;

    private const int PAGE_SIZE = 10;

    private readonly AppDbContext _context;
    private readonly ILogger<YeuCauDoiCaController> _logger;

    public YeuCauDoiCaController(AppDbContext context, ILogger<YeuCauDoiCaController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =====================================================================
    //  INDEX — DANH SÁCH YÊU CẦU
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai, string? keyword, int page = 1)
    {
        trangThai = IsKnownStatus(trangThai) ? trangThai : null;
        keyword = keyword?.Trim();
        page = Math.Max(1, page);

        var scopeDept = GetManagerDepartmentScope();

        var baseQuery = _context.YeuCauDoiCas
            .AsNoTracking()
            .Include(y => y.NhanVienGui)!.ThenInclude(n => n!.PhongBan)
            .Include(y => y.NhanVienNhan)!.ThenInclude(n => n!.PhongBan)
            .Include(y => y.PhanCaGui)!.ThenInclude(p => p!.CaLamViec)
            .Include(y => y.PhanCaNhan)!.ThenInclude(p => p!.CaLamViec)
            .Include(y => y.NguoiDuyet)
            .AsQueryable();

        if (scopeDept.HasValue)
        {
            // Quản lý chỉ thấy yêu cầu liên quan bộ phận mình — ở vai trò nào
            // trong giao dịch cũng tính: người nhường hoặc người nhận.
            if (scopeDept.Value <= 0)
            {
                baseQuery = baseQuery.Where(_ => false);
            }
            else
            {
                baseQuery = baseQuery.Where(y =>
                    (y.NhanVienGui != null && y.NhanVienGui.MaPhongBan == scopeDept.Value) ||
                    (y.NhanVienNhan != null && y.NhanVienNhan.MaPhongBan == scopeDept.Value));
            }
        }

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(y => y.TrangThai == trangThai);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLowerInvariant();
            query = query.Where(y =>
                (y.NhanVienGui != null &&
                    (y.NhanVienGui.Ho + " " + y.NhanVienGui.Ten).ToLower().Contains(value)) ||
                (y.NhanVienNhan != null &&
                    (y.NhanVienNhan.Ho + " " + y.NhanVienNhan.Ten).ToLower().Contains(value)) ||
                y.LyDo.ToLower().Contains(value));
        }

        var totalFiltered = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)PAGE_SIZE));
        var pageNumber = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderBy(y => y.TrangThai == STATUS_CHO_DUYET ? 0 : 1)
            .ThenByDescending(y => y.NgayGui)
            .Skip((pageNumber - 1) * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .Select(y => new YeuCauDoiCaItemViewModel
            {
                MaYeuCau = y.MaYeuCau,
                MaPhanCaGui = y.MaPhanCaGui,
                MaPhanCaNhan = y.MaPhanCaNhan,
                TenNguoiGui = y.NhanVienGui == null ? "NV #" + y.MaNhanVienGui : (y.NhanVienGui.Ho + " " + y.NhanVienGui.Ten).Trim(),
                MaNguoiGuiFormat = "NV-" + y.MaNhanVienGui,
                TenPhongBanNguoiGui = y.NhanVienGui == null || y.NhanVienGui.PhongBan == null ? "—" : y.NhanVienGui.PhongBan.TenPhongBan,
                TenNguoiNhan = y.NhanVienNhan == null ? "NV #" + y.MaNhanVienNhan : (y.NhanVienNhan.Ho + " " + y.NhanVienNhan.Ten).Trim(),
                MaNguoiNhanFormat = "NV-" + y.MaNhanVienNhan,
                TenPhongBanNguoiNhan = y.NhanVienNhan == null || y.NhanVienNhan.PhongBan == null ? "—" : y.NhanVienNhan.PhongBan.TenPhongBan,
                NgayCaGui = y.PhanCaGui == null ? DateOnly.MinValue : y.PhanCaGui.NgayLamViec,
                TenCaGui = y.PhanCaGui == null || y.PhanCaGui.CaLamViec == null ? "—" : y.PhanCaGui.CaLamViec.TenCa,
                GioCaGui = y.PhanCaGui == null || y.PhanCaGui.CaLamViec == null
                    ? "—"
                    : y.PhanCaGui.CaLamViec.GioBatDau.ToString("HH:mm") + " – " + y.PhanCaGui.CaLamViec.GioKetThuc.ToString("HH:mm"),
                NgayCaNhan = y.PhanCaNhan == null ? DateOnly.MinValue : y.PhanCaNhan.NgayLamViec,
                TenCaNhan = y.PhanCaNhan == null || y.PhanCaNhan.CaLamViec == null ? "—" : y.PhanCaNhan.CaLamViec.TenCa,
                GioCaNhan = y.PhanCaNhan == null || y.PhanCaNhan.CaLamViec == null
                    ? "—"
                    : y.PhanCaNhan.CaLamViec.GioBatDau.ToString("HH:mm") + " – " + y.PhanCaNhan.CaLamViec.GioKetThuc.ToString("HH:mm"),
                LyDo = y.LyDo,
                TrangThai = y.TrangThai,
                TenNguoiDuyet = y.NguoiDuyet == null ? null : (y.NguoiDuyet.Ho + " " + y.NguoiDuyet.Ten).Trim(),
                NgayGui = y.NgayGui,
                NgayDuyet = y.NgayDuyet,
                PhanHoiDuyet = y.PhanHoiDuyet
            })
            .ToListAsync();

        var model = new YeuCauDoiCaIndexViewModel
        {
            TrangThai = trangThai,
            Keyword = keyword,
            TrangThaiOptions = BuildStatusOptions(trangThai),
            Items = items,
            TongYeuCau = await baseQuery.CountAsync(),
            ChoDuyet = await baseQuery.CountAsync(y => y.TrangThai == STATUS_CHO_DUYET),
            DaDuyet = await baseQuery.CountAsync(y => y.TrangThai == STATUS_DA_DUYET),
            TuChoi = await baseQuery.CountAsync(y => y.TrangThai == STATUS_TU_CHOI),
            DaHuy = await baseQuery.CountAsync(y => y.TrangThai == STATUS_DA_HUY),
            CoQuyenDuyet = true,
            PageNumber = pageNumber,
            PageSize = PAGE_SIZE,
            TotalPages = totalPages,
            TotalFilteredItems = totalFiltered
        };

        // Điều hướng tuần/tháng, lọc trạng thái và phân trang đều phải cập nhật
        // tại chỗ — trả partial cho AJAX để _Layout không bị dựng lại.
        if (IsAjaxRequest())
        {
            return PartialView("_YeuCauDoiCaTable", model);
        }

        return View(model);
    }

    // =====================================================================
    //  CREATE — TẠO YÊU CẦU HỘ NHÂN VIÊN
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new YeuCauDoiCaCreateViewModel();
        await LoadShiftOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(YeuCauDoiCaCreateViewModel model)
    {
        model.LyDo = model.LyDo?.Trim() ?? string.Empty;

        if (model.LyDo.Length is 0 or > 500)
        {
            ModelState.AddModelError(nameof(model.LyDo), "Vui lòng nhập lý do đổi ca (tối đa 500 ký tự).");
        }

        if (model.MaPhanCaGui <= 0 || model.MaPhanCaNhan <= 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn đủ ca nhường và ca nhận thay.");
        }

        var scopeDept = GetManagerDepartmentScope();

        var caGui = model.MaPhanCaGui > 0 ? await LoadShiftAsync(model.MaPhanCaGui) : null;
        var caNhan = model.MaPhanCaNhan > 0 ? await LoadShiftAsync(model.MaPhanCaNhan) : null;

        if (caGui == null || caNhan == null)
        {
            ModelState.AddModelError(string.Empty, "Không tìm thấy ca trực đã chọn. Vui lòng tải lại trang và chọn lại.");
        }
        else if (!InScope(caGui.MaPhongBan, scopeDept) || !InScope(caNhan.MaPhongBan, scopeDept))
        {
            ModelState.AddModelError(string.Empty, "Ca trực được chọn không thuộc bộ phận mà bạn quản lý.");
        }
        else
        {
            var now = DateTimeOffset.UtcNow;

            if (caGui.MaNhanVien == caNhan.MaNhanVien)
            {
                ModelState.AddModelError(string.Empty, "Hai ca được chọn thuộc cùng một nhân viên — đổi ca phải là hai người khác nhau.");
            }
            else if (caGui.MaPhanCa == caNhan.MaPhanCa)
            {
                ModelState.AddModelError(string.Empty, "Ca nhường và ca nhận không được trùng nhau.");
            }
            else if (caGui.KetThucDuKien <= now || caNhan.KetThucDuKien <= now)
            {
                ModelState.AddModelError(string.Empty, "Chỉ đổi được ca chưa diễn ra. Một trong hai ca đã kết thúc.");
            }
            else if (await HasAttendanceAsync(caGui.MaPhanCa) || await HasAttendanceAsync(caNhan.MaPhanCa))
            {
                ModelState.AddModelError(string.Empty, "Một trong hai ca đã có dữ liệu chấm công nên không thể đổi.");
            }
            else if (await HasOpenRequestAsync(caGui.MaPhanCa) || await HasOpenRequestAsync(caNhan.MaPhanCa))
            {
                ModelState.AddModelError(string.Empty, "Một trong hai ca đang có yêu cầu đổi ca chờ duyệt. Vui lòng xử lý yêu cầu đó trước.");
            }
            else
            {
                var ketQua = await ValidateSwapAsync(caGui, caNhan);
                if (!ketQua.IsValid)
                {
                    ModelState.AddModelError(string.Empty, ketQua.ThongDiep ?? "Không thể đổi ca.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadShiftOptionsAsync(model);
            return View(model);
        }

        var yeuCau = new YeuCauDoiCa
        {
            MaPhanCaGui = caGui!.MaPhanCa,
            MaPhanCaNhan = caNhan!.MaPhanCa,
            MaNhanVienGui = caGui.MaNhanVien,
            MaNhanVienNhan = caNhan.MaNhanVien,
            LyDo = model.LyDo,
            TrangThai = STATUS_CHO_DUYET,
            NgayGui = DateTimeOffset.UtcNow
        };

        _context.YeuCauDoiCas.Add(yeuCau);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Tạo yêu cầu đổi ca #{RequestId}: ca #{ShiftFrom} (NV {EmployeeFrom}) ↔ ca #{ShiftTo} (NV {EmployeeTo}).",
            yeuCau.MaYeuCau, caGui.MaPhanCa, caGui.MaNhanVien, caNhan.MaPhanCa, caNhan.MaNhanVien);

        TempData["Success"] = $"Đã tạo yêu cầu đổi ca #{yeuCau.MaYeuCau}. Yêu cầu đang chờ duyệt.";
        return RedirectToAction(nameof(Index));
    }

    // =====================================================================
    //  APPROVE — DUYỆT VÀ HOÁN ĐỔI HAI CA
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? phanHoi)
    {
        phanHoi = string.IsNullOrWhiteSpace(phanHoi) ? null : phanHoi.Trim();

        if (phanHoi is { Length: > 500 })
        {
            return Finish(false, "Ý kiến duyệt không được vượt quá 500 ký tự.");
        }

        var yeuCau = await _context.YeuCauDoiCas
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.MaYeuCau == id);

        if (yeuCau == null)
        {
            return Finish(false, "Không tìm thấy yêu cầu đổi ca.");
        }

        if (yeuCau.TrangThai != STATUS_CHO_DUYET)
        {
            return Finish(false, "Yêu cầu này đã được xử lý xong, không duyệt lại được.");
        }

        var scopeDept = GetManagerDepartmentScope();

        // Đối soát lại toàn bộ điều kiện tại thời điểm duyệt — dữ liệu có thể đã
        // đổi kể từ lúc gửi yêu cầu.
        var caGuiHienTai = await LoadShiftAsync(yeuCau.MaPhanCaGui);
        var caNhanHienTai = await LoadShiftAsync(yeuCau.MaPhanCaNhan);

        if (caGuiHienTai == null || caNhanHienTai == null)
        {
            return Finish(false, "Một trong hai ca đã bị xoá khỏi lịch phân ca.");
        }

        if (!InScope(caGuiHienTai.MaPhongBan, scopeDept) && !InScope(caNhanHienTai.MaPhongBan, scopeDept))
        {
            return Finish(false, "Yêu cầu này không thuộc bộ phận mà bạn quản lý.");
        }

        if (caGuiHienTai.MaNhanVien != yeuCau.MaNhanVienGui || caNhanHienTai.MaNhanVien != yeuCau.MaNhanVienNhan)
        {
            return Finish(false, "Lịch phân ca đã thay đổi so với lúc gửi yêu cầu (người trực khác). Vui lòng từ chối và tạo yêu cầu mới.");
        }

        if (await HasAttendanceAsync(caGuiHienTai.MaPhanCa) || await HasAttendanceAsync(caNhanHienTai.MaPhanCa))
        {
            return Finish(false, "Một trong hai ca đã phát sinh chấm công nên không thể đổi được nữa.");
        }

        var ketQua = await ValidateSwapAsync(caGuiHienTai, caNhanHienTai);
        if (!ketQua.IsValid)
        {
            return Finish(false, ketQua.ThongDiep ?? "Không thể duyệt yêu cầu đổi ca.");
        }

        var nguoiDuyetId = GetCurrentEmployeeId();

        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
                var currentFirst = await LoadShiftAsync(yeuCau.MaPhanCaGui);
                var currentSecond = await LoadShiftAsync(yeuCau.MaPhanCaNhan);
                if (currentFirst == null || currentSecond == null
                    || currentFirst.MaNhanVien != yeuCau.MaNhanVienGui || currentSecond.MaNhanVien != yeuCau.MaNhanVienNhan
                    || (!InScope(currentFirst.MaPhongBan, scopeDept) && !InScope(currentSecond.MaPhongBan, scopeDept))
                    || await HasAttendanceAsync(currentFirst.MaPhanCa) || await HasAttendanceAsync(currentSecond.MaPhanCa))
                    throw new InvalidOperationException("Ca hoặc phạm vi quản lý đã thay đổi. Tải lại yêu cầu trước khi duyệt.");
                var currentValidation = await ValidateSwapAsync(currentFirst, currentSecond);
                if (!currentValidation.IsValid)
                    throw new InvalidOperationException(currentValidation.ThongDiep ?? "Điều kiện đổi ca vừa thay đổi.");
                var changeReason = phanHoi ?? yeuCau.LyDo;
                await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.shift_change_reason', {changeReason}, true)");

                // Hoán đổi người trực: câu UPDATE mang theo điều kiện "vẫn đúng
                // người cũ" để nếu ai đó vừa sửa lịch thì không ghi đè.
                var doiCaGui = await _context.PhanCas
                    .Where(p => p.MaPhanCa == caGuiHienTai.MaPhanCa && p.MaNhanVien == yeuCau.MaNhanVienGui)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.MaNhanVien, yeuCau.MaNhanVienNhan));

                var doiCaNhan = await _context.PhanCas
                    .Where(p => p.MaPhanCa == caNhanHienTai.MaPhanCa && p.MaNhanVien == yeuCau.MaNhanVienNhan)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.MaNhanVien, yeuCau.MaNhanVienGui));

                if (doiCaGui != 1 || doiCaNhan != 1)
                {
                    throw new InvalidOperationException(
                        "Lịch phân ca vừa bị thay đổi bởi thao tác khác. Không duyệt được yêu cầu này.");
                }

                var daCapNhat = await _context.YeuCauDoiCas
                    .Where(y => y.MaYeuCau == id && y.TrangThai == STATUS_CHO_DUYET)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(y => y.TrangThai, STATUS_DA_DUYET)
                        .SetProperty(y => y.MaNguoiDuyet, nguoiDuyetId)
                        .SetProperty(y => y.NgayDuyet, DateTimeOffset.UtcNow)
                        .SetProperty(y => y.PhanHoiDuyet, phanHoi));

                if (daCapNhat != 1)
                {
                    throw new InvalidOperationException("Yêu cầu đã được người khác xử lý trước đó.");
                }

                var accountName = User.Identity?.Name ?? "Người duyệt đổi ca";
                _context.NhatKyQuanTris.Add(new NhatKyQuanTri {
                    MaTaiKhoanThucHien = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : null,
                    TenNguoiThucHien = accountName.Length > 100 ? accountName[..100] : accountName,
                    TenTaiKhoanBiTacDong = $"DOI_CA-{id}", HanhDong = "DUYET_DOI_CA",
                    NoiDung = $"Đổi người trực ca #{yeuCau.MaPhanCaGui} và #{yeuCau.MaPhanCaNhan}; phân công sự kiện tương lai cần xác nhận lại.",
                    LyDo = changeReason
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            });

            _logger.LogInformation("Duyệt yêu cầu đổi ca #{RequestId}.", id);
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Không duyệt được yêu cầu đổi ca #{RequestId}.", id);
            return Finish(false, "Không duyệt được yêu cầu đổi ca: " + ex.Message);
        }

        return Finish(true, $"Đã duyệt yêu cầu đổi ca #{id}. Lịch phân ca đã cập nhật; phân công sự kiện tương lai liên quan cần được xác nhận lại.",
            new { maYeuCau = id, trangThai = STATUS_DA_DUYET });
    }

    // =====================================================================
    //  REJECT — TỪ CHỐI
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? lyDo)
    {
        lyDo = lyDo?.Trim();

        // CHECK chk_doi_ca_tu_choi_can_ly_do của CSDL đòi lý do; bắt ở đây để
        // người dùng thấy câu tiếng Việt thay vì lỗi 23514.
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            return Finish(false, "Bắt buộc phải nhập lý do từ chối để người gửi biết đường xử lý.");
        }

        if (lyDo.Length > 500)
        {
            return Finish(false, "Lý do từ chối không được vượt quá 500 ký tự.");
        }

        var scopeDept = GetManagerDepartmentScope();

        var yeuCau = await _context.YeuCauDoiCas
            .AsNoTracking()
            .Include(y => y.NhanVienGui)
            .Include(y => y.NhanVienNhan)
            .FirstOrDefaultAsync(y => y.MaYeuCau == id);

        if (yeuCau == null)
        {
            return Finish(false, "Không tìm thấy yêu cầu đổi ca.");
        }

        if (scopeDept.HasValue &&
            !InScope(yeuCau.NhanVienGui?.MaPhongBan, scopeDept) &&
            !InScope(yeuCau.NhanVienNhan?.MaPhongBan, scopeDept))
        {
            return Finish(false, "Yêu cầu này không thuộc bộ phận mà bạn quản lý.");
        }

        var nguoiDuyetId = GetCurrentEmployeeId();

        var daCapNhat = await _context.YeuCauDoiCas
            .Where(y => y.MaYeuCau == id && y.TrangThai == STATUS_CHO_DUYET)
            .ExecuteUpdateAsync(s => s
                .SetProperty(y => y.TrangThai, STATUS_TU_CHOI)
                .SetProperty(y => y.MaNguoiDuyet, nguoiDuyetId)
                .SetProperty(y => y.NgayDuyet, DateTimeOffset.UtcNow)
                .SetProperty(y => y.PhanHoiDuyet, lyDo));

        if (daCapNhat != 1)
        {
            return Finish(false, "Yêu cầu này đã được xử lý trước đó. Vui lòng tải lại danh sách.");
        }

        _logger.LogInformation("Từ chối yêu cầu đổi ca #{RequestId}.", id);
        return Finish(true, $"Đã từ chối yêu cầu đổi ca #{id}.",
            new { maYeuCau = id, trangThai = STATUS_TU_CHOI });
    }

    // =====================================================================
    //  HÀM PHỤ
    // =====================================================================

    /// <summary>Một ca trực đã kèm các cột cần cho việc đối soát.</summary>
    private sealed record CaTrucInfo(
        int MaPhanCa,
        int MaNhanVien,
        int? MaPhongBan,
        string HoTen,
        DateTimeOffset BatDauDuKien,
        DateTimeOffset KetThucDuKien);

    private async Task<CaTrucInfo?> LoadShiftAsync(int maPhanCa)
    {
        return await _context.PhanCas
            .AsNoTracking()
            .Where(p => p.MaPhanCa == maPhanCa)
            .Select(p => new CaTrucInfo(
                p.MaPhanCa,
                p.MaNhanVien,
                p.NhanVien == null ? null : p.NhanVien.MaPhongBan,
                p.NhanVien == null ? "Nhân viên #" + p.MaNhanVien : (p.NhanVien.Ho + " " + p.NhanVien.Ten).Trim(),
                p.BatDauDuKien,
                p.KetThucDuKien))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Đối soát hai chiều bằng <see cref="ShiftSwapPolicy"/>: mỗi người phải được
    /// kiểm tra trên lịch <b>còn lại của chính họ</b> (đã loại ca đem nhường).
    /// </summary>
    private async Task<ShiftSwapPolicy.SwapValidation> ValidateSwapAsync(CaTrucInfo caGui, CaTrucInfo caNhan)
    {
        // Cửa sổ ±3 ngày là đủ: một ca chỉ dài tối đa 12 tiếng nên mọi xung đột
        // chồng giờ hoặc nghỉ dưới 8 tiếng đều phải nằm trong khoảng này.
        var lech = TimeSpan.FromHours(7);
        var tuNgay = DateOnly.FromDateTime(caGui.BatDauDuKien.ToOffset(lech).DateTime).AddDays(-3);
        var denNgay = DateOnly.FromDateTime(caNhan.KetThucDuKien.ToOffset(lech).DateTime).AddDays(3);

        var cacCa = await _context.PhanCas
            .AsNoTracking()
            .Where(p => (p.MaNhanVien == caGui.MaNhanVien || p.MaNhanVien == caNhan.MaNhanVien) &&
                        p.NgayLamViec >= tuNgay && p.NgayLamViec <= denNgay)
            .Select(p => new
            {
                p.MaPhanCa,
                p.MaNhanVien,
                p.BatDauDuKien,
                p.KetThucDuKien
            })
            .ToListAsync();

        var caKhacCuaNguoiGui = cacCa
            .Where(p => p.MaNhanVien == caGui.MaNhanVien && p.MaPhanCa != caGui.MaPhanCa)
            .Select(p => new ShiftSchedulingPolicy.ShiftWindow(p.BatDauDuKien, p.KetThucDuKien))
            .ToList();

        var caKhacCuaNguoiNhan = cacCa
            .Where(p => p.MaNhanVien == caNhan.MaNhanVien && p.MaPhanCa != caNhan.MaPhanCa)
            .Select(p => new ShiftSchedulingPolicy.ShiftWindow(p.BatDauDuKien, p.KetThucDuKien))
            .ToList();

        // Người gửi trực ca của người nhận; người nhận trực ca của người gửi.
        return ShiftSwapPolicy.Validate(
            caNguoiGuiNhanVe: new ShiftSchedulingPolicy.ShiftWindow(caNhan.BatDauDuKien, caNhan.KetThucDuKien),
            caNguoiNhanNhanVe: new ShiftSchedulingPolicy.ShiftWindow(caGui.BatDauDuKien, caGui.KetThucDuKien),
            caKhacCuaNguoiGui: caKhacCuaNguoiGui,
            caKhacCuaNguoiNhan: caKhacCuaNguoiNhan,
            tenNguoiGui: caGui.HoTen,
            tenNguoiNhan: caNhan.HoTen);
    }

    private Task<bool> HasAttendanceAsync(int maPhanCa) =>
        _context.ChamCongs.AnyAsync(c => c.MaPhanCa == maPhanCa);

    private Task<bool> HasOpenRequestAsync(int maPhanCa) =>
        _context.YeuCauDoiCas.AnyAsync(y =>
            y.TrangThai == STATUS_CHO_DUYET &&
            (y.MaPhanCaGui == maPhanCa || y.MaPhanCaNhan == maPhanCa));

    private static bool InScope(int? employeeDeptId, int? scopeDeptId)
    {
        if (!scopeDeptId.HasValue)
        {
            return true; // HR/Admin: toàn khách sạn
        }

        return scopeDeptId.Value > 0 && employeeDeptId == scopeDeptId.Value;
    }

    private async Task LoadShiftOptionsAsync(YeuCauDoiCaCreateViewModel model)
    {
        var scopeDept = GetManagerDepartmentScope();
        var now = DateTimeOffset.UtcNow;
        var tuNgay = DateOnly.FromDateTime(DateTime.Today);
        var denNgay = tuNgay.AddDays(SO_NGAY_TOI_CHON_CA);

        var caQuery = _context.PhanCas
            .AsNoTracking()
            .Include(p => p.NhanVien)!.ThenInclude(n => n!.PhongBan)
            .Include(p => p.CaLamViec)
            .Where(p => p.NgayLamViec >= tuNgay && p.NgayLamViec <= denNgay && p.KetThucDuKien > now);

        if (scopeDept.HasValue)
        {
            caQuery = scopeDept.Value <= 0
                ? caQuery.Where(_ => false)
                : caQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == scopeDept.Value);
        }

        var cacCa = await caQuery
            .OrderBy(p => p.NgayLamViec)
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

        // Cùng một danh sách ca nhưng hai ô chọn khác nhau: ô "ca của tôi" cần
        // thấy mã nhân viên để người lập biết mình đang nhường ca của ai.
        var options = cacCa
            .Select(p => new SelectListItem
            {
                Value = p.MaPhanCa.ToString(),
                Text = $"NV-{p.MaNhanVien} {p.HoTen} · {p.TenCa} · {p.NgayLamViec:dd/MM} ({p.GioBatDau:HH\\:mm}–{p.GioKetThuc:HH\\:mm})",
                Selected = p.MaPhanCa == model.MaPhanCaGui
            })
            .ToList();

        model.CaCuaToi = options;

        model.CaDongNghiep = cacCa
            .Select(p => new SelectListItem
            {
                Value = p.MaPhanCa.ToString(),
                Text = $"NV-{p.MaNhanVien} {p.HoTen} · {p.TenCa} · {p.NgayLamViec:dd/MM} ({p.GioBatDau:HH\\:mm}–{p.GioKetThuc:HH\\:mm})",
                Selected = p.MaPhanCa == model.MaPhanCaNhan
            })
            .ToList();
    }

    private static bool IsKnownStatus(string? status) =>
        status is STATUS_CHO_DUYET or STATUS_DA_DUYET or STATUS_TU_CHOI or STATUS_DA_HUY;

    private static IReadOnlyList<SelectListItem> BuildStatusOptions(string? selected) =>
    [
        new SelectListItem { Value = string.Empty, Text = "-- Tất cả trạng thái --", Selected = string.IsNullOrWhiteSpace(selected) },
        new SelectListItem { Value = STATUS_CHO_DUYET, Text = "Chờ duyệt", Selected = selected == STATUS_CHO_DUYET },
        new SelectListItem { Value = STATUS_DA_DUYET, Text = "Đã duyệt", Selected = selected == STATUS_DA_DUYET },
        new SelectListItem { Value = STATUS_TU_CHOI, Text = "Từ chối", Selected = selected == STATUS_TU_CHOI },
        new SelectListItem { Value = STATUS_DA_HUY, Text = "Đã huỷ", Selected = selected == STATUS_DA_HUY }
    ];

    /// <summary>
    /// Bộ phận mà Quản lý được thao tác. <c>null</c> = không giới hạn (HR/Admin/IT).
    /// <c>-1</c> = Manager nhưng thiếu claim phòng ban ⇒ không khớp phòng ban nào.
    /// </summary>
    private int? GetManagerDepartmentScope() => User.GetManagedDepartmentScope();

    private int? GetCurrentEmployeeId() =>
        int.TryParse(User.FindFirstValue("EmployeeId"), out var employeeId) ? employeeId : null;

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Trả kết quả theo đúng đường mà yêu cầu đến. AJAX nhận JSON để cập nhật
    /// hàng trong bảng tại chỗ — không reload cả trang làm mất vị trí cuộn và
    /// bộ lọc người dùng đang chọn. Form thường vẫn nhận redirect như cũ.
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
