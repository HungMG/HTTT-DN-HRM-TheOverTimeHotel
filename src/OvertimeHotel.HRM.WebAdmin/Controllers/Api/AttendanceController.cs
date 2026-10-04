using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

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

    public AttendanceController(AppDbContext context, ILogger<AttendanceController> logger)
    {
        _context = context;
        _logger = logger;
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

            return Ok(ToTodayShiftDto(phanCa));
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

            var gioCheckIn = AttendanceCalculator.ToUtc(request.Timestamp);
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

            var gioCheckOut = AttendanceCalculator.ToUtc(request.Timestamp);
            if (gioCheckOut < chamCong.GioCheckIn.Value)
            {
                return BadRequestError("Thời gian quẹt thẻ ra không hợp lệ.");
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
                    p.ChamCong!.GioCheckIn,
                    p.ChamCong.GioCheckOut,
                    PhutDiTre = p.ChamCong.PhutDiTre,
                    PhutVeSom = p.ChamCong.PhutVeSom,
                    GioOt = p.ChamCong.GioTangCaOt,
                    p.ChamCong.TrangThai
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
        var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountIdStr, out var accountId))
        {
            return null;
        }

        var account = await _context.TaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MaTaiKhoan == accountId);

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

    private static TodayShiftDto ToTodayShiftDto(Core.Models.PhanCa phanCa)
    {
        var chamCong = phanCa.ChamCong;
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
            TrangThai: chamCong?.TrangThai);
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