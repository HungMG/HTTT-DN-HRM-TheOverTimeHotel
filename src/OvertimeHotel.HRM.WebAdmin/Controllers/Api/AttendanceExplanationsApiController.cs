using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

[ApiController]
[Route("api/mobile/attendance-explanations")]
[Authorize(AuthenticationSchemes = "MobileBearer")]
public class AttendanceExplanationsApiController : ControllerBase
{
    private const string STATUS_CHO_QUAN_LY = "CHO_QUAN_LY";
    private const string STATUS_CHO_HR = "CHO_HR";
    private const string STATUS_DA_DUYET = "DA_DUYET";
    private const string STATUS_TU_CHOI = "TU_CHOI";
    private const string STATUS_DA_HUY = "DA_HUY";

    private const int SO_NGAY_GIAI_TRINH = 45;
    private const int MAX_LY_DO = 500;
    private const int MAX_MINH_CHUNG = 500;

    private readonly AppDbContext _context;
    private readonly ILogger<AttendanceExplanationsApiController> _logger;

    public AttendanceExplanationsApiController(
        AppDbContext context,
        ILogger<AttendanceExplanationsApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách ca làm việc trong 45 ngày qua đủ điều kiện nộp giải trình của nhân viên đăng nhập.
    /// </summary>
    [HttpGet("eligible-shifts")]
    public async Task<IActionResult> GetEligibleShifts()
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;
            var now = DateTimeOffset.UtcNow;
            var earliest = now.AddDays(-SO_NGAY_GIAI_TRINH);

            // Các ca của chính nhân viên, đã kết thúc, trong 45 ngày qua
            var shifts = await _context.PhanCas
                .AsNoTracking()
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .Where(p => p.MaNhanVien == myId &&
                            p.KetThucDuKien <= now &&
                            p.KetThucDuKien >= earliest)
                .OrderByDescending(p => p.NgayLamViec)
                .ThenByDescending(p => p.BatDauDuKien)
                .ToListAsync();

            // Lấy danh sách mã ca đang có giải trình mở (CHO_QUAN_LY hoặc CHO_HR)
            var openShiftIds = await _context.GiaiTrinhChamCongs
                .AsNoTracking()
                .Where(g => g.MaNhanVien == myId &&
                            (g.TrangThai == STATUS_CHO_QUAN_LY || g.TrangThai == STATUS_CHO_HR))
                .Select(g => g.MaPhanCa)
                .ToListAsync();

            var openShiftSet = new HashSet<int>(openShiftIds);

            var result = shifts
                .Where(p => !openShiftSet.Contains(p.MaPhanCa))
                .Select(p => new EligibleShiftDto(
                    MaPhanCa: p.MaPhanCa,
                    NgayLamViec: p.NgayLamViec,
                    MaCa: p.MaCa,
                    TenCa: p.CaLamViec?.TenCa ?? $"Ca #{p.MaCa}",
                    GioBatDau: p.CaLamViec?.GioBatDau ?? TimeOnly.MinValue,
                    GioKetThuc: p.CaLamViec?.GioKetThuc ?? TimeOnly.MinValue,
                    GioVaoThucTe: p.ChamCong?.GioCheckIn,
                    GioRaThucTe: p.ChamCong?.GioCheckOut,
                    TrangThaiChamCong: p.ChamCong?.TrangThai
                ))
                .ToList();

            return Ok(result);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL khi tải ca đủ điều kiện giải trình.");
            return DatabaseUnavailableResult();
        }
    }

    /// <summary>
    /// Lấy danh sách giải trình chấm công của chính nhân viên đang đăng nhập.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyExplanations()
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            var list = await _context.GiaiTrinhChamCongs
                .AsNoTracking()
                .Include(g => g.NhanVien)!.ThenInclude(nv => nv!.PhongBan)
                .Include(g => g.PhanCa)!.ThenInclude(p => p!.CaLamViec)
                .Include(g => g.PhanCa)!.ThenInclude(p => p!.ChamCong)
                .Include(g => g.QuanLyXacMinh)
                .Include(g => g.HrXacNhan)
                .Where(g => g.MaNhanVien == myId)
                .OrderByDescending(g => g.NgayGui)
                .ToListAsync();

            var dtos = list.Select(MapToItemDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL khi tải danh sách giải trình.");
            return DatabaseUnavailableResult();
        }
    }

    /// <summary>
    /// Lấy chi tiết một bản ghi giải trình chấm công (Zero Trust: chủ đơn hoặc quản lý trong scope).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExplanationDetail([FromRoute] int id)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var g = await _context.GiaiTrinhChamCongs
                .AsNoTracking()
                .Include(x => x.NhanVien)!.ThenInclude(nv => nv!.PhongBan)
                .Include(x => x.PhanCa)!.ThenInclude(p => p!.CaLamViec)
                .Include(x => x.PhanCa)!.ThenInclude(p => p!.ChamCong)
                .Include(x => x.QuanLyXacMinh)
                .Include(x => x.HrXacNhan)
                .FirstOrDefaultAsync(x => x.MaGiaiTrinh == id);

            if (g == null)
            {
                return NotFound(new AttendanceExplanationOperationResult(false, "NOT_FOUND", "Không tìm thấy giải trình yêu cầu."));
            }

            var myId = account!.MaNhanVien;
            var role = account.VaiTro?.TenVaiTro ?? string.Empty;
            var isOwner = g.MaNhanVien == myId;
            var isManagerInDept = (role == "Manager" || role == "QuanLy") &&
                                  account.NhanVien?.MaPhongBan == g.NhanVien?.MaPhongBan;
            var isAdminOrHr = role is "Admin" or "HR" or "IT";

            if (!isOwner && !isManagerInDept && !isAdminOrHr)
            {
                return StatusCode(403, new AttendanceExplanationOperationResult(false, "FORBIDDEN", "Bạn không có quyền xem giải trình này."));
            }

            return Ok(MapToItemDto(g));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL khi tải chi tiết giải trình #{Id}.", id);
            return DatabaseUnavailableResult();
        }
    }

    /// <summary>
    /// Nộp giải trình chấm công mới.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateExplanation([FromBody] CreateAttendanceExplanationRequest request)
    {
        if (request == null)
        {
            return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", "Dữ liệu giải trình không hợp lệ."));
        }

        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;
            var lyDo = request.LyDo?.Trim() ?? string.Empty;
            var minhChung = string.IsNullOrWhiteSpace(request.MinhChung) ? null : request.MinhChung.Trim();

            if (lyDo.Length is 0 or > MAX_LY_DO)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", $"Lý do giải trình bắt buộc (tối đa {MAX_LY_DO} ký tự)."));
            }

            if (minhChung is { Length: > MAX_MINH_CHUNG })
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", $"Minh chứng không được vượt quá {MAX_MINH_CHUNG} ký tự."));
            }

            if (!request.GioDeXuatVao.HasValue && !request.GioDeXuatRa.HasValue)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", "Phải đề xuất ít nhất một mốc giờ vào hoặc giờ ra."));
            }

            var now = DateTimeOffset.UtcNow;

            if (request.GioDeXuatVao.HasValue && request.GioDeXuatVao.Value > now)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", "Giờ vào đề xuất không được ở tương lai."));
            }

            if (request.GioDeXuatRa.HasValue && request.GioDeXuatRa.Value > now)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", "Giờ ra đề xuất không được ở tương lai."));
            }

            if (request.GioDeXuatVao.HasValue && request.GioDeXuatRa.HasValue && request.GioDeXuatRa.Value <= request.GioDeXuatVao.Value)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "VALIDATION_ERROR", "Giờ ra đề xuất phải sau giờ vào đề xuất."));
            }

            // Kiểm tra phân ca
            var ca = await _context.PhanCas.FirstOrDefaultAsync(p => p.MaPhanCa == request.MaPhanCa);
            if (ca == null)
            {
                return NotFound(new AttendanceExplanationOperationResult(false, "NOT_FOUND", "Ca làm việc không tồn tại."));
            }

            if (ca.MaNhanVien != myId)
            {
                return StatusCode(403, new AttendanceExplanationOperationResult(false, "FORBIDDEN", "Bạn chỉ được nộp giải trình cho ca trực của chính mình."));
            }

            if (ca.KetThucDuKien > now)
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "SHIFT_NOT_ENDED", "Ca trực chưa kết thúc, chưa cần giải trình."));
            }

            if (ca.KetThucDuKien < now.AddDays(-SO_NGAY_GIAI_TRINH))
            {
                return BadRequest(new AttendanceExplanationOperationResult(false, "EXPIRED", $"Chỉ nhận giải trình trong vòng {SO_NGAY_GIAI_TRINH} ngày kể từ khi ca kết thúc."));
            }

            // Kiểm tra xem đã có giải trình đang mở chưa
            var hasOpen = await _context.GiaiTrinhChamCongs.AnyAsync(g =>
                g.MaPhanCa == request.MaPhanCa &&
                (g.TrangThai == STATUS_CHO_QUAN_LY || g.TrangThai == STATUS_CHO_HR));

            if (hasOpen)
            {
                return StatusCode(409, new AttendanceExplanationOperationResult(false, "ALREADY_OPEN", "Ca này đang có một giải trình chờ xử lý."));
            }

            var entity = new GiaiTrinhChamCong
            {
                MaPhanCa = request.MaPhanCa,
                MaNhanVien = myId,
                GioDeXuatVao = request.GioDeXuatVao?.ToUniversalTime(),
                GioDeXuatRa = request.GioDeXuatRa?.ToUniversalTime(),
                LyDo = lyDo,
                MinhChung = minhChung,
                TrangThai = STATUS_CHO_QUAN_LY,
                NgayGui = now
            };

            _context.GiaiTrinhChamCongs.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nhân viên #{EmpId} đã nộp giải trình #{MaGiaiTrinh} cho ca #{MaPhanCa}.", myId, entity.MaGiaiTrinh, ca.MaPhanCa);

            return Ok(new AttendanceExplanationOperationResult(true, "CREATED", "Gửi giải trình chấm công thành công.", entity.MaGiaiTrinh));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL khi tạo giải trình chấm công.");
            return DatabaseUnavailableResult();
        }
    }

    /// <summary>
    /// Rút giải trình chấm công (chỉ khi còn ở trạng thái CHO_QUAN_LY).
    /// </summary>
    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> WithdrawExplanation([FromRoute] int id)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            var g = await _context.GiaiTrinhChamCongs.FirstOrDefaultAsync(x => x.MaGiaiTrinh == id);
            if (g == null)
            {
                return NotFound(new AttendanceExplanationOperationResult(false, "NOT_FOUND", "Không tìm thấy giải trình yêu cầu."));
            }

            // Zero Trust: Phân quyền - Người gọi phải là chủ giải trình
            if (g.MaNhanVien != myId)
            {
                return StatusCode(403, new AttendanceExplanationOperationResult(false, "FORBIDDEN", "Bạn không có quyền rút giải trình của người khác."));
            }

            // Quy định nghiệp vụ: Chỉ cho phép rút khi còn ở CHO_QUAN_LY.
            // Nếu đã chuyển sang CHO_HR thì Quản lý đã xác minh, không được tự ý rút.
            if (g.TrangThai != STATUS_CHO_QUAN_LY)
            {
                if (g.TrangThai == STATUS_DA_HUY)
                {
                    return StatusCode(409, new AttendanceExplanationOperationResult(false, "ALREADY_WITHDRAWN", "Giải trình này đã được rút trước đó."));
                }
                if (g.TrangThai == STATUS_CHO_HR)
                {
                    return StatusCode(409, new AttendanceExplanationOperationResult(false, "MANAGER_ALREADY_VERIFIED", "Giải trình đã được cấp Quản lý xác minh và chuyển lên HR nên không thể rút."));
                }
                return StatusCode(409, new AttendanceExplanationOperationResult(false, "ALREADY_PROCESSED", "Giải trình đã được xử lý xong nên không thể rút."));
            }

            // Conditional Update (Optimistic Concurrency)
            var affected = await _context.GiaiTrinhChamCongs
                .Where(x => x.MaGiaiTrinh == id && x.MaNhanVien == myId && x.TrangThai == STATUS_CHO_QUAN_LY)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, STATUS_DA_HUY));

            if (affected == 0)
            {
                return StatusCode(409, new AttendanceExplanationOperationResult(false, "CONCURRENCY_CONFLICT", "Giải trình đã bị thay đổi trạng thái bởi bên khác."));
            }

            _logger.LogInformation("Nhân viên #{EmpId} đã rút giải trình #{Id} thành công.", myId, id);

            return Ok(new AttendanceExplanationOperationResult(true, "WITHDRAWN", "Đã rút giải trình chấm công thành công."));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL khi rút giải trình #{Id}.", id);
            return DatabaseUnavailableResult();
        }
    }

    private static AttendanceExplanationItemDto MapToItemDto(GiaiTrinhChamCong g)
    {
        var label = g.TrangThai switch
        {
            STATUS_CHO_QUAN_LY => "Chờ Quản lý xác minh",
            STATUS_CHO_HR => "Chờ HR xác nhận",
            STATUS_DA_DUYET => "Đã duyệt",
            STATUS_TU_CHOI => "Từ chối",
            STATUS_DA_HUY => "Đã rút",
            _ => g.TrangThai
        };

        return new AttendanceExplanationItemDto(
            MaGiaiTrinh: g.MaGiaiTrinh,
            MaPhanCa: g.MaPhanCa,
            MaNhanVien: g.MaNhanVien,
            HoTenNhanVien: g.NhanVien != null ? $"{g.NhanVien.Ho} {g.NhanVien.Ten}".Trim() : $"NV#{g.MaNhanVien}",
            TenPhongBan: g.NhanVien?.PhongBan?.TenPhongBan,
            NgayLamViec: g.PhanCa?.NgayLamViec ?? DateOnly.MinValue,
            TenCa: g.PhanCa?.CaLamViec?.TenCa ?? $"Ca #{g.PhanCa?.MaCa}",
            GioDeXuatVao: g.GioDeXuatVao,
            GioDeXuatRa: g.GioDeXuatRa,
            GioVaoThucTe: g.PhanCa?.ChamCong?.GioCheckIn,
            GioRaThucTe: g.PhanCa?.ChamCong?.GioCheckOut,
            LyDo: g.LyDo,
            MinhChung: g.MinhChung,
            TrangThai: g.TrangThai,
            TrangThaiLabel: label,
            NgayGui: g.NgayGui,
            MaQuanLyXacMinh: g.MaQuanLyXacMinh,
            HoTenQuanLy: g.QuanLyXacMinh != null ? $"{g.QuanLyXacMinh.Ho} {g.QuanLyXacMinh.Ten}".Trim() : null,
            NgayQuanLyXacMinh: g.NgayQuanLyXacMinh,
            YKienQuanLy: g.YKienQuanLy,
            MaHrXacNhan: g.MaHrXacNhan,
            HoTenHr: g.HrXacNhan != null ? $"{g.HrXacNhan.Ho} {g.HrXacNhan.Ten}".Trim() : null,
            NgayHrXacNhan: g.NgayHrXacNhan,
            YKienHr: g.YKienHr,
            CanWithdraw: g.TrangThai == STATUS_CHO_QUAN_LY
        );
    }

    private async Task<(TaiKhoan? Account, IActionResult? Error)> GetCurrentAccountAsync()
    {
        var maNhanVien = SupabaseClaims.GetMaNhanVien(User);
        if (maNhanVien is not > 0)
        {
            return (null, Unauthorized(new AttendanceExplanationOperationResult(false, "UNAUTHENTICATED", "Token xác thực không hợp lệ.")));
        }

        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .FirstOrDefaultAsync(t => t.MaNhanVien == maNhanVien.Value);

        if (account == null || account.TrangThai != true)
        {
            return (null, Unauthorized(new AttendanceExplanationOperationResult(false, "UNAUTHENTICATED", "Tài khoản không tồn tại hoặc đã bị khóa.")));
        }

        return (account, null);
    }

    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DbException or TimeoutException ||
        exception.InnerException is DbException or TimeoutException;

    private ObjectResult DatabaseUnavailableResult() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new AttendanceExplanationOperationResult(false, "SERVICE_UNAVAILABLE",
                "Không thể truy cập dữ liệu lúc này. Vui lòng thử lại sau."));
}
