using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

[ApiController]
[Route("api/mobile/shift-swaps")]
[Authorize(AuthenticationSchemes = "MobileBearer")]
public class ShiftSwapsApiController : ControllerBase
{
    private const string STATUS_CHO_NGUOI_NHAN = "CHO_NGUOI_NHAN";
    private const string STATUS_CHO_DUYET = "CHO_DUYET";
    private const string STATUS_DA_DUYET = "DA_DUYET";
    private const string STATUS_TU_CHOI = "TU_CHOI";
    private const string STATUS_DA_HUY = "DA_HUY";

    private const int SO_NGAY_TOI_CHON_CA = 30;
    private const int MAX_LY_DO = 500;

    private readonly AppDbContext _context;
    private readonly ILogger<ShiftSwapsApiController> _logger;

    public ShiftSwapsApiController(
        AppDbContext context,
        ILogger<ShiftSwapsApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Health-check & API Version cho cutover verification.
    /// </summary>
    [HttpGet("/api/version")]
    [AllowAnonymous]
    public IActionResult GetApiVersion()
    {
        return Ok(new
        {
            version = "2.4.0",
            commit = typeof(ShiftSwapsApiController).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+').Skip(1).FirstOrDefault() ?? "unknown",
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Danh sách ca của chính nhân viên đăng nhập có thể đem đi đổi (chưa kết thúc, chưa chấm công, không có yêu cầu mở).
    /// </summary>
    [HttpGet("eligible-my-shifts")]
    public async Task<IActionResult> GetEligibleMyShifts()
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;
            var now = DateTimeOffset.UtcNow;
            var tuNgay = DateOnly.FromDateTime(DateTime.Today);
            var denNgay = tuNgay.AddDays(SO_NGAY_TOI_CHON_CA);

            var shifts = await _context.PhanCas
                .AsNoTracking()
                .Include(p => p.CaLamViec)
                .Where(p => p.MaNhanVien == myId &&
                            p.KetThucDuKien > now &&
                            p.NgayLamViec >= tuNgay &&
                            p.NgayLamViec <= denNgay &&
                            !_context.ChamCongs.Any(c => c.MaPhanCa == p.MaPhanCa) &&
                            !_context.YeuCauDoiCas.Any(y =>
                                (y.TrangThai == STATUS_CHO_NGUOI_NHAN || y.TrangThai == STATUS_CHO_DUYET) &&
                                (y.MaPhanCaGui == p.MaPhanCa || y.MaPhanCaNhan == p.MaPhanCa)))
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.BatDauDuKien)
                .Select(p => new EligibleMyShiftDto
                {
                    MaPhanCa = p.MaPhanCa,
                    NgayLamViec = p.NgayLamViec,
                    TenCa = p.CaLamViec != null ? p.CaLamViec.TenCa : "Ca làm việc",
                    GioBatDau = p.CaLamViec != null ? p.CaLamViec.GioBatDau.ToString("HH:mm") : string.Empty,
                    GioKetThuc = p.CaLamViec != null ? p.CaLamViec.GioKetThuc.ToString("HH:mm") : string.Empty,
                    BatDauDuKien = p.BatDauDuKien,
                    KetThucDuKien = p.KetThucDuKien
                })
                .ToListAsync();

            return Ok(shifts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách ca của tôi có thể đổi.");
            return StatusCode(500, new { message = "Lỗi máy chủ khi lấy danh sách ca trực." });
        }
    }

    /// <summary>
    /// Danh sách ca của đồng nghiệp cùng bộ phận có thể nhận đổi.
    /// </summary>
    [HttpGet("eligible-partner-shifts")]
    public async Task<IActionResult> GetEligiblePartnerShifts()
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;
            var myEmployee = await _context.NhanViens
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.MaNhanVien == myId);

            if (myEmployee == null || !myEmployee.MaPhongBan.HasValue)
            {
                return BadRequest(new { message = "Không xác định được phòng ban của nhân viên." });
            }

            var myDeptId = myEmployee.MaPhongBan.Value;
            var now = DateTimeOffset.UtcNow;
            var tuNgay = DateOnly.FromDateTime(DateTime.Today);
            var denNgay = tuNgay.AddDays(SO_NGAY_TOI_CHON_CA);

            // Chỉ đồng nghiệp cùng phòng ban, khác nhân viên hiện tại
            var shifts = await _context.PhanCas
                .AsNoTracking()
                .Include(p => p.NhanVien)
                .Include(p => p.CaLamViec)
                .Where(p => p.MaNhanVien != myId &&
                            p.NhanVien != null &&
                            p.NhanVien.MaPhongBan == myDeptId &&
                            p.KetThucDuKien > now &&
                            p.NgayLamViec >= tuNgay &&
                            p.NgayLamViec <= denNgay &&
                            !_context.ChamCongs.Any(c => c.MaPhanCa == p.MaPhanCa) &&
                            !_context.YeuCauDoiCas.Any(y =>
                                (y.TrangThai == STATUS_CHO_NGUOI_NHAN || y.TrangThai == STATUS_CHO_DUYET) &&
                                (y.MaPhanCaGui == p.MaPhanCa || y.MaPhanCaNhan == p.MaPhanCa)))
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.BatDauDuKien)
                .Select(p => new EligiblePartnerShiftDto
                {
                    MaPhanCa = p.MaPhanCa,
                    MaNhanVien = p.MaNhanVien,
                    TenNhanVien = p.NhanVien != null ? (p.NhanVien.Ho + " " + p.NhanVien.Ten).Trim() : ("NV #" + p.MaNhanVien),
                    NgayLamViec = p.NgayLamViec,
                    TenCa = p.CaLamViec != null ? p.CaLamViec.TenCa : "Ca làm việc",
                    GioBatDau = p.CaLamViec != null ? p.CaLamViec.GioBatDau.ToString("HH:mm") : string.Empty,
                    GioKetThuc = p.CaLamViec != null ? p.CaLamViec.GioKetThuc.ToString("HH:mm") : string.Empty,
                    BatDauDuKien = p.BatDauDuKien,
                    KetThucDuKien = p.KetThucDuKien
                })
                .ToListAsync();

            return Ok(shifts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách ca của đồng nghiệp.");
            return StatusCode(500, new { message = "Lỗi máy chủ khi lấy danh sách ca đồng nghiệp." });
        }
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi ca liên quan đến nhân viên đăng nhập (gửi hoặc nhận).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyShiftSwaps()
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            var entities = await _context.YeuCauDoiCas
                .AsNoTracking()
                .Include(y => y.NhanVienGui)
                .Include(y => y.NhanVienNhan)
                .Include(y => y.PhanCaGui)!.ThenInclude(p => p!.CaLamViec)
                .Include(y => y.PhanCaNhan)!.ThenInclude(p => p!.CaLamViec)
                .Include(y => y.NguoiDuyet)
                .Where(y => y.MaNhanVienGui == myId || y.MaNhanVienNhan == myId)
                .OrderBy(y => (y.MaNhanVienNhan == myId && y.TrangThai == STATUS_CHO_NGUOI_NHAN) ? 0 :
                              (y.TrangThai == STATUS_CHO_DUYET ? 1 : 2))
                .ThenByDescending(y => y.NgayGui)
                .ToListAsync();

            var items = entities.Select(y => new ShiftSwapItemDto
                {
                    MaYeuCau = y.MaYeuCau,
                    MaPhanCaGui = y.MaPhanCaGui,
                    TenCaGui = y.PhanCaGui != null && y.PhanCaGui.CaLamViec != null ? y.PhanCaGui.CaLamViec.TenCa : "—",
                    NgayCaGui = y.PhanCaGui != null ? y.PhanCaGui.NgayLamViec.ToString("dd/MM/yyyy") : string.Empty,
                    GioCaGui = y.PhanCaGui != null && y.PhanCaGui.CaLamViec != null
                        ? y.PhanCaGui.CaLamViec.GioBatDau.ToString("HH:mm") + " - " + y.PhanCaGui.CaLamViec.GioKetThuc.ToString("HH:mm")
                        : string.Empty,

                    MaNhanVienGui = y.MaNhanVienGui,
                    TenNguoiGui = y.NhanVienGui != null ? (y.NhanVienGui.Ho + " " + y.NhanVienGui.Ten).Trim() : ("NV #" + y.MaNhanVienGui),

                    MaPhanCaNhan = y.MaPhanCaNhan,
                    TenCaNhan = y.PhanCaNhan != null && y.PhanCaNhan.CaLamViec != null ? y.PhanCaNhan.CaLamViec.TenCa : "—",
                    NgayCaNhan = y.PhanCaNhan != null ? y.PhanCaNhan.NgayLamViec.ToString("dd/MM/yyyy") : string.Empty,
                    GioCaNhan = y.PhanCaNhan != null && y.PhanCaNhan.CaLamViec != null
                        ? y.PhanCaNhan.CaLamViec.GioBatDau.ToString("HH:mm") + " - " + y.PhanCaNhan.CaLamViec.GioKetThuc.ToString("HH:mm")
                        : string.Empty,

                    MaNhanVienNhan = y.MaNhanVienNhan,
                    TenNguoiNhan = y.NhanVienNhan != null ? (y.NhanVienNhan.Ho + " " + y.NhanVienNhan.Ten).Trim() : ("NV #" + y.MaNhanVienNhan),

                    LyDo = y.LyDo,
                    TrangThai = y.TrangThai,
                    BenTuChoi = y.BenTuChoi,
                    PhanHoiNguoiNhan = y.PhanHoiNguoiNhan,
                    NgayNguoiNhanPhanHoi = y.NgayNguoiNhanPhanHoi,
                    MaNguoiDuyet = y.MaNguoiDuyet,
                    TenNguoiDuyet = y.NguoiDuyet != null ? (y.NguoiDuyet.Ho + " " + y.NguoiDuyet.Ten).Trim() : null,
                    NgayGui = y.NgayGui,
                    NgayDuyet = y.NgayDuyet,
                    PhanHoiDuyet = y.PhanHoiDuyet,
                    IsSender = y.MaNhanVienGui == myId,
                    IsReceiver = y.MaNhanVienNhan == myId
                }).ToList();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách yêu cầu đổi ca của tôi.");
            return StatusCode(500, new { message = "Lỗi máy chủ khi lấy danh sách đổi ca." });
        }
    }

    /// <summary>
    /// Lấy chi tiết 1 yêu cầu đổi ca.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetShiftSwapDetail(int id)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            var y = await _context.YeuCauDoiCas
                .AsNoTracking()
                .Include(x => x.NhanVienGui)
                .Include(x => x.NhanVienNhan)
                .Include(x => x.PhanCaGui)!.ThenInclude(p => p!.CaLamViec)
                .Include(x => x.PhanCaNhan)!.ThenInclude(p => p!.CaLamViec)
                .Include(x => x.NguoiDuyet)
                .FirstOrDefaultAsync(x => x.MaYeuCau == id);

            if (y == null)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu đổi ca." });
            }

            if (y.MaNhanVienGui != myId && y.MaNhanVienNhan != myId)
            {
                return StatusCode(403);
            }

            var dto = new ShiftSwapItemDto
            {
                MaYeuCau = y.MaYeuCau,
                MaPhanCaGui = y.MaPhanCaGui,
                TenCaGui = y.PhanCaGui?.CaLamViec?.TenCa ?? "—",
                NgayCaGui = y.PhanCaGui?.NgayLamViec.ToString("dd/MM/yyyy") ?? string.Empty,
                GioCaGui = y.PhanCaGui?.CaLamViec != null
                    ? y.PhanCaGui.CaLamViec.GioBatDau.ToString("HH:mm") + " - " + y.PhanCaGui.CaLamViec.GioKetThuc.ToString("HH:mm")
                    : string.Empty,
                MaNhanVienGui = y.MaNhanVienGui,
                TenNguoiGui = y.NhanVienGui != null ? (y.NhanVienGui.Ho + " " + y.NhanVienGui.Ten).Trim() : ("NV #" + y.MaNhanVienGui),
                MaPhanCaNhan = y.MaPhanCaNhan,
                TenCaNhan = y.PhanCaNhan?.CaLamViec?.TenCa ?? "—",
                NgayCaNhan = y.PhanCaNhan?.NgayLamViec.ToString("dd/MM/yyyy") ?? string.Empty,
                GioCaNhan = y.PhanCaNhan?.CaLamViec != null
                    ? y.PhanCaNhan.CaLamViec.GioBatDau.ToString("HH:mm") + " - " + y.PhanCaNhan.CaLamViec.GioKetThuc.ToString("HH:mm")
                    : string.Empty,
                MaNhanVienNhan = y.MaNhanVienNhan,
                TenNguoiNhan = y.NhanVienNhan != null ? (y.NhanVienNhan.Ho + " " + y.NhanVienNhan.Ten).Trim() : ("NV #" + y.MaNhanVienNhan),
                LyDo = y.LyDo,
                TrangThai = y.TrangThai,
                BenTuChoi = y.BenTuChoi,
                PhanHoiNguoiNhan = y.PhanHoiNguoiNhan,
                NgayNguoiNhanPhanHoi = y.NgayNguoiNhanPhanHoi,
                MaNguoiDuyet = y.MaNguoiDuyet,
                TenNguoiDuyet = y.NguoiDuyet != null ? (y.NguoiDuyet.Ho + " " + y.NguoiDuyet.Ten).Trim() : null,
                NgayGui = y.NgayGui,
                NgayDuyet = y.NgayDuyet,
                PhanHoiDuyet = y.PhanHoiDuyet,
                IsSender = y.MaNhanVienGui == myId,
                IsReceiver = y.MaNhanVienNhan == myId
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy chi tiết yêu cầu đổi ca #{Id}.", id);
            return StatusCode(500, new { message = "Lỗi máy chủ khi lấy chi tiết đổi ca." });
        }
    }

    /// <summary>
    /// Tạo yêu cầu đổi ca mới (Người gửi khởi tạo ở trạng thái CHO_NGUOI_NHAN).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateShiftSwap([FromBody] CreateShiftSwapRequest request)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            if (request == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            var lyDo = request.LyDo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                return BadRequest(new { message = "Vui lòng nhập lý do đổi ca." });
            }

            if (lyDo.Length > MAX_LY_DO)
            {
                return BadRequest(new { message = $"Lý do đổi ca không được vượt quá {MAX_LY_DO} ký tự." });
            }

            if (request.MaPhanCaGui <= 0 || request.MaPhanCaNhan <= 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ca nhường và ca nhận thay." });
            }

            if (request.MaPhanCaGui == request.MaPhanCaNhan)
            {
                return BadRequest(new { message = "Ca nhường và ca nhận không được trùng nhau." });
            }

            // Thực thi trong transaction với advisory lock 2026101509
            var createdRequestId = 0;
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Advisory lock đảm bảo hoán đổi ca và kiểm tra đồng thời tuần tự tuyệt đối
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");

                var caGui = await LoadShiftAsync(request.MaPhanCaGui);
                var caNhan = await LoadShiftAsync(request.MaPhanCaNhan);

                if (caGui == null || caNhan == null)
                {
                    throw new InvalidOperationException("Không tìm thấy một trong hai ca trực.");
                }

                if (caGui.MaNhanVien != myId)
                {
                    throw new UnauthorizedAccessException("Ca nhường không thuộc quyền sở hữu của bạn.");
                }

                if (caNhan.MaNhanVien == myId)
                {
                    throw new InvalidOperationException("Không thể đổi ca với chính mình.");
                }

                if (!caGui.MaPhongBan.HasValue || !caNhan.MaPhongBan.HasValue || caGui.MaPhongBan.Value != caNhan.MaPhongBan.Value)
                {
                    throw new InvalidOperationException("Chỉ được đổi ca với đồng nghiệp trong cùng một bộ phận.");
                }

                var now = DateTimeOffset.UtcNow;
                if (caGui.KetThucDuKien <= now || caNhan.KetThucDuKien <= now)
                {
                    throw new InvalidOperationException("Chỉ được đổi ca chưa kết thúc.");
                }

                if (await HasAttendanceAsync(caGui.MaPhanCa) || await HasAttendanceAsync(caNhan.MaPhanCa))
                {
                    throw new InvalidOperationException("Một trong hai ca đã có dữ liệu chấm công.");
                }

                if (await HasOpenRequestAsync(caGui.MaPhanCa) || await HasOpenRequestAsync(caNhan.MaPhanCa))
                {
                    throw new InvalidOperationException("Một trong hai ca đang có yêu cầu đổi ca chưa xử lý xong.");
                }

                var validation = await ValidateSwapAsync(caGui, caNhan);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.ThongDiep ?? "Yêu cầu đổi ca vi phạm quy định phân ca.");
                }

                var yeuCau = new YeuCauDoiCa
                {
                    MaPhanCaGui = caGui.MaPhanCa,
                    MaPhanCaNhan = caNhan.MaPhanCa,
                    MaNhanVienGui = caGui.MaNhanVien,
                    MaNhanVienNhan = caNhan.MaNhanVien,
                    LyDo = lyDo,
                    TrangThai = STATUS_CHO_NGUOI_NHAN,
                    NgayGui = DateTimeOffset.UtcNow
                };

                _context.YeuCauDoiCas.Add(yeuCau);
                await _context.SaveChangesAsync();

                createdRequestId = yeuCau.MaYeuCau;
                await transaction.CommitAsync();
            });

            _logger.LogInformation("Nhân viên #{EmpId} đã tạo yêu cầu đổi ca #{RequestId} (CHO_NGUOI_NHAN).",
                myId, createdRequestId);

            return Ok(new ShiftSwapOperationResult
            {
                Success = true,
                Message = $"Đã gửi yêu cầu đổi ca #{createdRequestId}. Chờ đồng nghiệp xác nhận.",
                MaYeuCau = createdRequestId
            });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }
        catch (InvalidOperationException iex)
        {
            return BadRequest(new { message = iex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo yêu cầu đổi ca.");
            return StatusCode(500, new { message = "Lỗi máy chủ khi tạo yêu cầu đổi ca." });
        }
    }

    /// <summary>
    /// Đồng nghiệp B phản hồi (Đồng ý hoặc Từ chối).
    /// </summary>
    [HttpPost("{id:int}/respond")]
    public async Task<IActionResult> RespondShiftSwap(int id, [FromBody] RespondShiftSwapRequest request)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            if (request == null)
            {
                return BadRequest(new { message = "Dữ liệu phản hồi không hợp lệ." });
            }

            var lyDo = request.LyDo?.Trim();
            if (!request.DongY)
            {
                if (string.IsNullOrWhiteSpace(lyDo))
                {
                    return BadRequest(new { message = "Bắt buộc phải nhập lý do khi từ chối đổi ca." });
                }
                if (lyDo.Length > MAX_LY_DO)
                {
                    return BadRequest(new { message = $"Lý do từ chối không được vượt quá {MAX_LY_DO} ký tự." });
                }
            }
            else if (lyDo is { Length: > MAX_LY_DO })
            {
                return BadRequest(new { message = $"Ghi chú không được vượt quá {MAX_LY_DO} ký tự." });
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Advisory lock để đảm bảo tính toàn vẹn
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");

                var yeuCau = await _context.YeuCauDoiCas
                    .FirstOrDefaultAsync(y => y.MaYeuCau == id);

                if (yeuCau == null)
                {
                    throw new KeyNotFoundException("Không tìm thấy yêu cầu đổi ca.");
                }

                if (yeuCau.MaNhanVienNhan != myId)
                {
                    throw new UnauthorizedAccessException("Bạn không phải người nhận của yêu cầu đổi ca này.");
                }

                if (yeuCau.TrangThai != STATUS_CHO_NGUOI_NHAN)
                {
                    throw new ShiftSwapConflictException("Yêu cầu này không ở trạng thái chờ bạn xác nhận (có thể đã được xử lý hoặc đã bị rút).");
                }

                var now = DateTimeOffset.UtcNow;

                if (request.DongY)
                {
                    // Kiểm tra lại ca trực
                    var caGui = await LoadShiftAsync(yeuCau.MaPhanCaGui);
                    var caNhan = await LoadShiftAsync(yeuCau.MaPhanCaNhan);

                    if (caGui == null || caNhan == null)
                    {
                        throw new InvalidOperationException("Một trong hai ca trực không còn tồn tại trên hệ thống.");
                    }

                    if (caGui.KetThucDuKien <= now || caNhan.KetThucDuKien <= now)
                    {
                        throw new InvalidOperationException("Một trong hai ca trực đã kết thúc.");
                    }

                    if (await HasAttendanceAsync(caGui.MaPhanCa) || await HasAttendanceAsync(caNhan.MaPhanCa))
                    {
                        throw new InvalidOperationException("Một trong hai ca trực đã có dữ liệu chấm công.");
                    }

                    var validation = await ValidateSwapAsync(caGui, caNhan);
                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException(validation.ThongDiep ?? "Điều kiện đổi ca không còn thỏa mãn quy định phân ca.");
                    }

                }

                var rows = await _context.YeuCauDoiCas
                    .Where(y => y.MaYeuCau == id && y.MaNhanVienNhan == myId && y.TrangThai == STATUS_CHO_NGUOI_NHAN)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(y => y.TrangThai, request.DongY ? STATUS_CHO_DUYET : STATUS_TU_CHOI)
                        .SetProperty(y => y.BenTuChoi, request.DongY ? null : "NGUOI_NHAN")
                        .SetProperty(y => y.PhanHoiNguoiNhan, lyDo)
                        .SetProperty(y => y.NgayNguoiNhanPhanHoi, now));
                if (rows != 1)
                    throw new ShiftSwapConflictException("Yêu cầu đổi ca vừa được thay đổi bởi thao tác khác.");
                await transaction.CommitAsync();
            });

            _logger.LogInformation("Nhân viên #{EmpId} đã phản hồi ({DongY}) yêu cầu đổi ca #{Id}.",
                myId, request.DongY, id);

            var message = request.DongY
                ? "Bạn đã đồng ý đổi ca. Yêu cầu đã được chuyển lên Quản lý để duyệt."
                : "Bạn đã từ chối yêu cầu đổi ca.";

            return Ok(new ShiftSwapOperationResult
            {
                Success = true,
                Message = message,
                MaYeuCau = id
            });
        }
        catch (KeyNotFoundException knf)
        {
            return NotFound(new { message = knf.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }
        catch (ShiftSwapConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException iex)
        {
            return BadRequest(new { message = iex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phản hồi yêu cầu đổi ca #{Id}.", id);
            return StatusCode(500, new { message = "Lỗi máy chủ khi phản hồi yêu cầu đổi ca." });
        }
    }

    /// <summary>
    /// Người gửi A rút yêu cầu đổi ca (chỉ khi CHO_NGUOI_NHAN hoặc CHO_DUYET).
    /// </summary>
    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> WithdrawShiftSwap(int id)
    {
        try
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
                var result = await WithdrawLockedAsync(id);
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi rút yêu cầu đổi ca #{Id}.", id);
            return StatusCode(500, new { message = "Lỗi máy chủ khi rút yêu cầu đổi ca." });
        }
    }

    private async Task<IActionResult> WithdrawLockedAsync(int id)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var myId = account!.MaNhanVien;

            var yeuCau = await _context.YeuCauDoiCas
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.MaYeuCau == id);

            if (yeuCau == null)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu đổi ca." });
            }

            if (yeuCau.MaNhanVienGui != myId)
            {
                return StatusCode(403);
            }

            if (yeuCau.TrangThai != STATUS_CHO_NGUOI_NHAN && yeuCau.TrangThai != STATUS_CHO_DUYET)
            {
                return Conflict(new { message = "Yêu cầu đã được duyệt, từ chối hoặc đã huỷ trước đó." });
            }

            var rows = await _context.YeuCauDoiCas
                .Where(y => y.MaYeuCau == id &&
                            y.MaNhanVienGui == myId &&
                            (y.TrangThai == STATUS_CHO_NGUOI_NHAN || y.TrangThai == STATUS_CHO_DUYET))
                .ExecuteUpdateAsync(s => s.SetProperty(y => y.TrangThai, STATUS_DA_HUY));

            if (rows != 1)
            {
                return Conflict(new { message = "Yêu cầu đổi ca vừa được thay đổi bởi thao tác khác." });
            }

            _logger.LogInformation("Nhân viên #{EmpId} đã rút yêu cầu đổi ca #{Id}.", myId, id);

            return Ok(new ShiftSwapOperationResult
            {
                Success = true,
                Message = "Rút yêu cầu đổi ca thành công.",
                MaYeuCau = id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi rút yêu cầu đổi ca #{Id}.", id);
            return StatusCode(500, new { message = "Lỗi máy chủ khi rút yêu cầu đổi ca." });
        }
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private sealed class ShiftSwapConflictException(string message) : Exception(message);

    private async Task<(TaiKhoan? account, IActionResult? errorResult)> GetCurrentAccountAsync()
    {
        var employeeId = SupabaseClaims.GetMaNhanVien(User);
        if (employeeId is not > 0)
            return (null, Unauthorized(new { message = "Token xác thực không hợp lệ." }));

        var account = await _context.TaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MaNhanVien == employeeId.Value);
        if (account == null || !account.TrangThai)
            return (null, Unauthorized(new { message = "Tài khoản không tồn tại hoặc đã bị khóa." }));

        return (account, null);
    }

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

    private async Task<ShiftSwapPolicy.SwapValidation> ValidateSwapAsync(CaTrucInfo caGui, CaTrucInfo caNhan)
    {
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
            (y.TrangThai == STATUS_CHO_NGUOI_NHAN || y.TrangThai == STATUS_CHO_DUYET) &&
            (y.MaPhanCaGui == maPhanCa || y.MaPhanCaNhan == maPhanCa));
}
