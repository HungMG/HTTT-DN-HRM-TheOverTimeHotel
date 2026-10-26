using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

/// <summary>
/// Cổng API chấm công Check-in/Check-out dành cho Mobile App (Task 17).
/// Toàn bộ phép đối soát ca chạy trên máy chủ, không tin số liệu thiết bị gửi lên.
/// </summary>
[ApiController]
[Route("api/mobile/attendance")]
[Authorize(AuthenticationSchemes = "MobileBearer")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AttendanceController> _logger;
    private readonly IAttendanceSummaryService _summaryService;

    public AttendanceController(AppDbContext context, ILogger<AttendanceController> logger,
        IAttendanceSummaryService summaryService)
    {
        _context = context;
        _logger = logger;
        _summaryService = summaryService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int year, [FromQuery] int month,
        [FromQuery] int? employeeId, CancellationToken cancellationToken)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12 || employeeId is <= 0)
            return BadRequest(new AttendanceApiError("VALIDATION_ERROR", "Tháng, năm hoặc mã nhân viên không hợp lệ."));

        var viewerId = SupabaseClaims.GetMaNhanVien(User);
        if (viewerId is not > 0)
            return Unauthorized(new AttendanceApiError("UNAUTHENTICATED", "Vui lòng đăng nhập lại."));

        try
        {
            var account = await _context.TaiKhoans.AsNoTracking().Include(a => a.VaiTro).Include(a => a.NhanVien)
                .SingleOrDefaultAsync(a => a.MaNhanVien == viewerId.Value, cancellationToken);
            if (account?.TrangThai != true || account.NhanVien == null)
                return Unauthorized(new AttendanceApiError("UNAUTHENTICATED", "Phiên đăng nhập không còn hợp lệ."));

            var targetId = employeeId ?? account.MaNhanVien;
            var target = await _context.NhanViens.AsNoTracking().Where(e => e.MaNhanVien == targetId)
                .Select(e => new { e.MaNhanVien, e.MaPhongBan }).SingleOrDefaultAsync(cancellationToken);
            if (target == null || !AttendanceAccessPolicy.CanView(account.VaiTro?.TenVaiTro,
                    account.MaNhanVien, account.NhanVien.MaPhongBan, targetId, target.MaPhongBan))
                return StatusCode(403, new AttendanceApiError("FORBIDDEN", "Bạn không có quyền xem bảng công này."));

            var from = new DateOnly(year, month, 1);
            var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var summary = await _summaryService.GetAsync(targetId, from, to, cancellationToken);
            return summary == null
                ? NotFound(new AttendanceApiError("NOT_FOUND", "Không tìm thấy nhân viên."))
                : Ok(summary);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Unable to load attendance summary for employee {EmployeeId}.", viewerId);
            return StatusCode(503, new AttendanceApiError("SERVICE_UNAVAILABLE", "Không thể tải bảng công lúc này. Vui lòng thử lại."));
        }
    }

    /// <summary>
    /// Lấy ca làm việc hôm nay của nhân viên đang đăng nhập, kèm trạng thái quẹt thẻ.
    /// </summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayShift()
    {
        try
        {
            var maNhanVien = await ResolveEmployeeIdAsync();
            if (maNhanVien is null)
            {
                return UnauthorizedError("Tài khoản không hợp lệ hoặc đã bị khóa.");
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var phanCa = await _context.PhanCas
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .AsNoTracking()
                .Where(p => p.MaNhanVien == maNhanVien.Value && p.NgayLamViec == today)
                .OrderBy(p => p.BatDauDuKien)
                .FirstOrDefaultAsync();

            if (phanCa == null)
            {
                return Ok(new TodayShiftDto(0, string.Empty, default, default, null, null));
            }

            var eventAssignment = await _context.PhanCongDuAns
                .Include(p => p.DuAn)
                .Include(p => p.CongThucTes)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MaPhanCa == phanCa.MaPhanCa && p.TrangThaiThamGia != "HUY_PHAN_CONG");

            return Ok(ToTodayShiftDto(phanCa, eventAssignment));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while loading today's shift.");
            return ServiceUnavailable();
        }
    }

    /// <summary>
    /// Quẹt thẻ vào ca. Đối soát số phút đi trễ so với giờ bắt đầu dự kiến của ca.
    /// </summary>
    [HttpPost("check-in")]
    [Authorize(AuthenticationSchemes = "MobileBearer", Policy = PermissionCodes.MobileCheckIn)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        if (request == null)
        {
            return BadRequestError("Dữ liệu quẹt thẻ không hợp lệ.");
        }

        try
        {
            var maNhanVien = await ResolveEmployeeIdAsync();
            if (maNhanVien is null)
            {
                return UnauthorizedError("Tài khoản không hợp lệ hoặc đã bị khóa.");
            }

            // RÀNG BUỘC HỢP ĐỒNG: Chỉ nhân viên còn hợp đồng lao động có hiệu lực mới được chấm công
            var contracts = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.MaNhanVien == maNhanVien.Value)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var contractValidation = ContractAttendancePolicy.ValidateAttendanceContract(contracts, today);
            if (!contractValidation.IsAllowed)
            {
                return BadRequestError(contractValidation.ErrorMessage ?? ContractAttendancePolicy.MSG_NO_CONTRACT);
            }

            // Zero Trust: phương thức quẹt thẻ phải nằm trong danh sách cho phép.
            var phuongThuc = AttendanceCalculator.NormalizeMethod(request.PhuongThuc);
            if (phuongThuc is null)
            {
                return BadRequestError("Phương thức quẹt thẻ không hợp lệ.");
            }

            var phanCa = await LoadOwnShiftAsync(request.MaPhanCa, maNhanVien.Value);
            if (phanCa is null)
            {
                return NotFoundError("Không tìm thấy ca làm việc của bạn. Vui lòng liên hệ quản lý.");
            }

            var chamCong = await _context.ChamCongs.FirstOrDefaultAsync(c => c.MaPhanCa == phanCa.MaPhanCa);
            if (chamCong != null && chamCong.GioCheckIn.HasValue)
            {
                return ConflictError("Bạn đã quẹt thẻ vào ca này rồi.");
            }

            var gioCheckIn = AttendanceCalculator.ToUtc(DateTimeOffset.UtcNow);
            var windowValidation = AttendanceCalculator.ValidateCheckInWindow(
                phanCa.BatDauDuKien, phanCa.KetThucDuKien, gioCheckIn);
            if (!windowValidation.IsValid)
            {
                return BadRequestError(windowValidation.ErrorMessage ?? "Thời điểm quẹt thẻ vào ca không hợp lệ.");
            }

            var result = AttendanceCalculator.EvaluateCheckIn(phanCa.BatDauDuKien, gioCheckIn);

            if (chamCong == null)
            {
                chamCong = new Core.Models.ChamCong { MaPhanCa = phanCa.MaPhanCa };
                _context.ChamCongs.Add(chamCong);
            }

            chamCong.GioCheckIn = gioCheckIn;
            chamCong.PhutDiTre = result.PhutDiTre;
            chamCong.PhutVeSom = 0;
            chamCong.GioTangCaOt = 0m;
            chamCong.TrangThai = result.TrangThai;
            chamCong.PhuongThucCheckIn = phuongThuc;
            chamCong.ToaDoCheckIn = SanitizeCoordinate(request.ToaDo);
            chamCong.ThietBiCheckIn = SanitizeDevice(request.ThietBi);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Nhân viên {MaNhanVien} quẹt thẻ vào ca {MaPhanCa}, trễ {PhutDiTre} phút.",
                maNhanVien.Value, phanCa.MaPhanCa, result.PhutDiTre);

            return Ok(new CheckInResponse(
                Success: true,
                Message: BuildCheckInMessage(result, phanCa.CaLamViec?.TenCa),
                MaChamCong: chamCong.MaChamCong,
                PhutDiTre: result.PhutDiTre,
                PhutVeSom: 0,
                GioOt: 0m,
                TrangThai: result.TrangThai));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while processing check-in.");
            return ServiceUnavailable();
        }
    }

    /// <summary>
    /// Quẹt thẻ ra ca. Đối soát số phút về sớm và số giờ làm thêm (OT).
    /// </summary>
    [HttpPost("check-out")]
    [Authorize(AuthenticationSchemes = "MobileBearer", Policy = PermissionCodes.MobileCheckIn)]
    public async Task<IActionResult> CheckOut([FromBody] CheckInRequest request)
    {
        if (request == null)
        {
            return BadRequestError("Dữ liệu quẹt thẻ không hợp lệ.");
        }

        try
        {
            var maNhanVien = await ResolveEmployeeIdAsync();
            if (maNhanVien is null)
            {
                return UnauthorizedError("Tài khoản không hợp lệ hoặc đã bị khóa.");
            }

            // RÀNG BUỘC HỢP ĐỒNG: Chỉ nhân viên còn hợp đồng lao động có hiệu lực mới được chấm công (Check-out)
            var contracts = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.MaNhanVien == maNhanVien.Value)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var contractValidation = ContractAttendancePolicy.ValidateAttendanceContract(contracts, today);
            if (!contractValidation.IsAllowed)
            {
                return BadRequestError(contractValidation.ErrorMessage ?? ContractAttendancePolicy.MSG_NO_CONTRACT);
            }

            var phuongThuc = AttendanceCalculator.NormalizeMethod(request.PhuongThuc);
            if (phuongThuc is null)
            {
                return BadRequestError("Phương thức quẹt thẻ không hợp lệ.");
            }

            var phanCa = await LoadOwnShiftAsync(request.MaPhanCa, maNhanVien.Value);
            if (phanCa is null)
            {
                return NotFoundError("Không tìm thấy ca làm việc của bạn. Vui lòng liên hệ quản lý.");
            }

            var chamCong = await _context.ChamCongs.FirstOrDefaultAsync(c => c.MaPhanCa == phanCa.MaPhanCa);
            if (chamCong == null || !chamCong.GioCheckIn.HasValue)
            {
                return ConflictError("Bạn chưa quẹt thẻ vào ca này.");
            }

            if (chamCong.GioCheckOut.HasValue)
            {
                return ConflictError("Bạn đã quẹt thẻ ra ca này rồi.");
            }

            var gioCheckOut = AttendanceCalculator.ToUtc(DateTimeOffset.UtcNow);
            var windowValidation = AttendanceCalculator.ValidateCheckOutWindow(
                phanCa.BatDauDuKien, phanCa.KetThucDuKien, chamCong.GioCheckIn.Value, gioCheckOut);
            if (!windowValidation.IsValid)
            {
                return BadRequestError(windowValidation.ErrorMessage ?? "Thời điểm quẹt thẻ ra ca không hợp lệ.");
            }

            var result = AttendanceCalculator.EvaluateCheckOut(
                phanCa.KetThucDuKien,
                chamCong.GioCheckIn.Value,
                gioCheckOut,
                chamCong.PhutDiTre);

            chamCong.GioCheckOut = gioCheckOut;
            chamCong.PhutVeSom = result.PhutVeSom;
            chamCong.GioTangCaOt = result.GioOt;
            chamCong.TrangThai = result.TrangThai;

            // Ca đã quẹt đủ thẻ vào và thẻ ra thì đánh dấu hoàn thành trên lịch phân ca.
            phanCa.TrangThai = "HOAN_THANH";

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Nhân viên {MaNhanVien} quẹt thẻ ra ca {MaPhanCa}, về sớm {PhutVeSom} phút, OT {GioOt} giờ.",
                maNhanVien.Value, phanCa.MaPhanCa, result.PhutVeSom, result.GioOt);

            return Ok(new CheckInResponse(
                Success: true,
                Message: BuildCheckOutMessage(result, phanCa.CaLamViec?.TenCa),
                MaChamCong: chamCong.MaChamCong,
                PhutDiTre: result.PhutDiTre,
                PhutVeSom: result.PhutVeSom,
                GioOt: result.GioOt,
                TrangThai: result.TrangThai));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while processing check-out.");
            return ServiceUnavailable();
        }
    }

    /// <summary>
    /// Lịch sử chấm công của nhân viên trong tháng, sắp xếp mới nhất trước.
    /// </summary>
    [HttpGet("month")]
    public async Task<IActionResult> GetMonthlyAttendance(
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        try
        {
            var maNhanVien = await ResolveEmployeeIdAsync();
            if (maNhanVien is null)
            {
                return UnauthorizedError("Tài khoản không hợp lệ hoặc đã bị khóa.");
            }

            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            if (targetYear < 2000 || targetYear > 2100 || targetMonth < 1 || targetMonth > 12)
            {
                return BadRequestError("Tháng hoặc năm không hợp lệ.");
            }

            var firstDay = new DateOnly(targetYear, targetMonth, 1);
            var lastDay = firstDay.AddMonths(1);

            var rows = await _context.PhanCas
                .AsNoTracking()
                .Where(p => p.MaNhanVien == maNhanVien.Value
                            && p.NgayLamViec >= firstDay
                            && p.NgayLamViec < lastDay)
                .OrderByDescending(p => p.NgayLamViec)
                .Select(p => new
                {
                    p.MaPhanCa,
                    p.NgayLamViec,
                    TenCa = p.CaLamViec != null ? p.CaLamViec.TenCa : string.Empty,

                    // ChamCong là LEFT JOIN — ca vừa phân chưa ai quẹt thẻ thì cả
                    // dòng cham_cong NULL. Đọc thẳng p.ChamCong!.X vào cột không
                    // nullable khiến EF ném "Nullable object must have a value", làm
                    // HỎNG CẢ THÁNG chỉ vì có MỘT ca chưa quẹt thẻ. Phải đệ quy
                    // null ở từng cột.
                    GioCheckIn = p.ChamCong != null ? p.ChamCong.GioCheckIn : null,
                    GioCheckOut = p.ChamCong != null ? p.ChamCong.GioCheckOut : null,
                    PhutDiTre = p.ChamCong != null ? p.ChamCong.PhutDiTre : 0,
                    PhutVeSom = p.ChamCong != null ? p.ChamCong.PhutVeSom : 0,
                    GioOt = p.ChamCong != null ? p.ChamCong.GioTangCaOt : 0m,

                    // Ca chưa quẹt thẻ chưa có trạng thái chấm công. Trả chuỗi rỗng
                    // thay vì "DUNG_GIO" — để mặc định thì Mobile hiển thị nhầm là
                    // đã đi làm đúng giờ. _ => của ToAttendanceRecord rơi về "Vắng
                    // mặt", đúng nghĩa với ca chưa có dữ liệu quẹt thẻ.
                    TrangThai = p.ChamCong != null && !string.IsNullOrEmpty(p.ChamCong.TrangThai)
                        ? p.ChamCong.TrangThai
                        : string.Empty
                })
                .ToListAsync();

            var result = rows.Select(r => new MonthlyAttendanceDto(
                MaPhanCa: r.MaPhanCa,
                NgayLamViec: r.NgayLamViec,
                TenCa: r.TenCa,
                CheckIn: r.GioCheckIn,
                CheckOut: r.GioCheckOut,
                PhutDiTre: r.PhutDiTre,
                PhutVeSom: r.PhutVeSom,
                GioOt: r.GioOt,
                TrangThai: r.TrangThai)).ToList();

            return Ok(result);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while loading monthly attendance.");
            return ServiceUnavailable();
        }
    }

    /// <summary>
    /// Trích xuất mã nhân viên từ JWT rồi nạp lại tài khoản từ CSDL để kiểm tra trạng thái tài khoản.
    /// Không tin claim một cách mù quáng (Zero Trust).
    /// </summary>
    private async Task<int?> ResolveEmployeeIdAsync()
    {
        // Token do Supabase GoTrue cấp: mã nhân viên nằm trong app_metadata và đã
        // được rải ra claim phẳng "ma_nhan_vien" ở OnTokenValidated. Không dùng
        // ClaimTypes.NameIdentifier vì "sub" của Supabase là UUID của auth.users —
        // parse ra int luôn fail, dù token hoàn toàn hợp lệ.
        var maNhanVien = SupabaseClaims.GetMaNhanVien(User);
        if (maNhanVien is not > 0)
        {
            return null;
        }

        // TaiKhoan.MaNhanVien có unique index nên FirstOrDefault trả đúng một dòng.
        var account = await _context.TaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MaNhanVien == maNhanVien.Value);

        // Vẫn kiểm tra TrangThai trong CSDL: token đã cấp vẫn còn hiệu lực sau khi
        // quản trị viên khoá tài khoản. Claim chỉ là gợi ý để tra, không phải
        // giấy uỷ quyền (Zero Trust).
        if (account == null || account.TrangThai != true || account.MaNhanVien == 0)
        {
            return null;
        }

        return account.MaNhanVien;
    }

    /// <summary>
    /// Nạp ca làm việc và xác nhận ca đó thuộc về nhân viên đang đăng nhập.
    /// Chặn nhân viên quẹt thẻ hộ cho ca của người khác.
    /// </summary>
    private Task<Core.Models.PhanCa?> LoadOwnShiftAsync(int maPhanCa, int maNhanVien) =>
        _context.PhanCas
            .Include(p => p.CaLamViec)
            .FirstOrDefaultAsync(p => p.MaPhanCa == maPhanCa && p.MaNhanVien == maNhanVien);

    private static TodayShiftDto ToTodayShiftDto(Core.Models.PhanCa phanCa, Core.Models.PhanCongDuAn? eventAssignment = null)
    {
        var chamCong = phanCa.ChamCong;
        var eventHours = eventAssignment != null && eventAssignment.CongThucTes.Count > 0
            ? Math.Round((decimal)eventAssignment.CongThucTes.Sum(c => (c.KetThuc - c.BatDau).TotalHours), 2)
            : (decimal?)null;

        return new TodayShiftDto(
            MaPhanCa: phanCa.MaPhanCa,
            TenCa: phanCa.CaLamViec?.TenCa ?? string.Empty,
            GioBatDau: phanCa.BatDauDuKien,
            GioKetThuc: phanCa.KetThucDuKien,
            DaCheckIn: chamCong?.GioCheckIn,
            DaCheckOut: chamCong?.GioCheckOut,
            PhutDiTre: chamCong?.PhutDiTre ?? 0,
            PhutVeSom: chamCong?.PhutVeSom ?? 0,
            GioOt: chamCong?.GioTangCaOt ?? 0m,
            TrangThai: chamCong?.TrangThai,
            TenSuKien: eventAssignment?.DuAn?.TenDuAn,
            VaiTroSuKien: eventAssignment?.VaiTroDuAn,
            SoGioSuKien: eventHours);
    }

    /// <summary>
    /// Chuẩn hóa toạ độ gửi lên thành định dạng "latitude,longitude" và chặn vượt quá độ dài cột.
    /// </summary>
    private static string? SanitizeCoordinate(string? toaDo)
    {
        if (string.IsNullOrWhiteSpace(toaDo))
        {
            return null;
        }

        var clean = toaDo.Trim();
        return clean.Length <= 50 ? clean : clean[..50];
    }

    /// <summary>
    /// Chuẩn hóa tên thiết bị và chặn vượt quá độ dài cột.
    /// </summary>
    private static string? SanitizeDevice(string? thietBi)
    {
        if (string.IsNullOrWhiteSpace(thietBi))
        {
            return null;
        }

        var clean = thietBi.Trim();
        return clean.Length <= 100 ? clean : clean[..100];
    }

    private static string BuildCheckInMessage(AttendanceResult result, string? tenCa)
    {
        var ca = string.IsNullOrWhiteSpace(tenCa) ? "ca làm việc" : $"ca {tenCa}";
        return result.PhutDiTre > 0
            ? $"Đã quẹt thẻ vào {ca} muộn {result.PhutDiTre} phút."
            : $"Đã quẹt thẻ vào {ca} đúng giờ.";
    }

    private static string BuildCheckOutMessage(AttendanceResult result, string? tenCa)
    {
        var ca = string.IsNullOrWhiteSpace(tenCa) ? "ca làm việc" : $"ca {tenCa}";

        if (result.PhutVeSom > 0)
        {
            return $"Đã quẹt thẻ ra {ca} sớm {result.PhutVeSom} phút.";
        }

        return result.GioOt > 0m
            ? $"Đã quẹt thẻ ra {ca}. Giờ làm thêm: {result.GioOt:0.##} giờ."
            : $"Đã quẹt thẻ ra {ca} đúng giờ.";
    }

    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DbException or TimeoutException ||
        exception.InnerException is DbException or TimeoutException;

    private ObjectResult BadRequestError(string message) =>
        StatusCode(StatusCodes.Status400BadRequest,
            new CheckInResponse(false, message, Code: "VALIDATION_ERROR"));

    private ObjectResult UnauthorizedError(string message) =>
        StatusCode(StatusCodes.Status401Unauthorized,
            new CheckInResponse(false, message, Code: "UNAUTHENTICATED"));

    private ObjectResult NotFoundError(string message) =>
        StatusCode(StatusCodes.Status404NotFound,
            new CheckInResponse(false, message, Code: "NOT_FOUND"));

    private ObjectResult ConflictError(string message) =>
        StatusCode(StatusCodes.Status409Conflict,
            new CheckInResponse(false, message, Code: "ALREADY_RECORDED"));

    private ObjectResult ServiceUnavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new CheckInResponse(false,
                "Không thể truy cập dữ liệu lúc này. Vui lòng thử lại sau.",
                Code: "SERVICE_UNAVAILABLE"));
}
