using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

[ApiController]
[Route("api/mobile/leave-requests")]
[Authorize(AuthenticationSchemes = "MobileBearer")]
public class LeaveRequestsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<LeaveRequestsController> _logger;

    public LeaveRequestsController(
        AppDbContext context,
        ILogger<LeaveRequestsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách đơn từ và số lượng chờ duyệt theo thẩm quyền RBAC của người dùng.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLeaveRequests(
        [FromQuery] string view = "pending",
        [FromQuery] string type = "all")
    {
        if ((!string.Equals(view, "pending", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(view, "reviewed", StringComparison.OrdinalIgnoreCase)) ||
            (!string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) &&
             type is not ("1" or "2" or "3" or "4" or "5")))
        {
            return BadRequest(new LeaveReviewResult(false, "VALIDATION_ERROR",
                "Bộ lọc không hợp lệ. view phải là pending/reviewed; type phải là all hoặc 1–5."));
        }

        try
        {
            return await GetLeaveRequestsCoreAsync(view, type);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while listing leave requests.");
            return DatabaseUnavailableResult();
        }
    }

    private async Task<IActionResult> GetLeaveRequestsCoreAsync(string view, string type)
    {
        // 1. Trích xuất tài khoản từ JWT Claims và tải lại từ DB để xác thực tức thời
        var (account, errorResult) = await GetCurrentAccountAsync();
        if (errorResult != null) return errorResult;

        var role = account!.VaiTro?.TenVaiTro;
        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        var deptId = account.NhanVien?.MaPhongBan;

        // Nguyên tắc Fail-Closed: Employee không có quyền truy cập
        if (normalizedRole == null || normalizedRole == RoleType.Employee)
        {
            return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Tài khoản nhân viên không có quyền truy cập duyệt đơn."));
        }

        // Manager bắt buộc phải có phòng ban
        if (normalizedRole == RoleType.Manager && !deptId.HasValue)
        {
            return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Tài khoản quản lý chưa được phân công phòng ban."));
        }

        // 2. Query cơ sở kèm Include đầy đủ quan hệ
        var baseQuery = _context.DonTus
            .AsNoTracking()
            .Include(d => d.LoaiDon)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .Include(d => d.NguoiDuyet)
            .AsQueryable();

        var canReviewLeave = OvertimeHotel.HRM.WebAdmin.Security.WebAdminPermissions.HasPermission(User, "DUYET_DON_NGHI");
        var canReviewResignation = OvertimeHotel.HRM.WebAdmin.Security.WebAdminPermissions.HasPermission(User, "DUYET_THOI_VIEC");
        if (!canReviewLeave && !canReviewResignation)
            return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Bạn không có quyền duyệt đơn."));
        baseQuery = baseQuery.Where(d => d.MaLoaiDon == 5 ? canReviewResignation : canReviewLeave);

        // 3. Áp dụng RBAC filtering trên server
        IQueryable<Core.Models.DonTu> authorizedQuery;
        if (normalizedRole == RoleType.Admin)
        {
            authorizedQuery = baseQuery; // Admin xem toàn bộ
        }
        else if (normalizedRole != RoleType.Manager)
        {
            // HR xử lý toàn bộ 5 loại đơn, kể cả đơn của quản lý — quản lý nộp đơn
            // thì không còn ai ở cấp dưới để duyệt. Loại trừ đơn của chính HR.
            authorizedQuery = baseQuery.Where(d =>
                d.MaLoaiDon >= 1 && d.MaLoaiDon <= 5 &&
                d.MaNhanVien != account.MaNhanVien);
        }
        else // Manager
        {
            var managerDept = deptId!.Value;
            authorizedQuery = baseQuery.Where(d =>
                (d.MaLoaiDon == 1 || d.MaLoaiDon == 2 || d.MaLoaiDon == 4) &&
                d.NhanVien != null &&
                d.NhanVien.MaPhongBan == managerDept &&
                d.MaNhanVien != account.MaNhanVien); // không tự duyệt đơn của chính mình
        }

        // 4. Tính toán pendingCount (tổng số đơn chờ duyệt mà user này có quyền duyệt)
        var pendingCount = await authorizedQuery.CountAsync(d => d.TrangThai == "CHO_DUYET");

        // 5. Lọc theo view (pending vs reviewed)
        var filteredQuery = authorizedQuery;
        var isPending = string.Equals(view, "pending", StringComparison.OrdinalIgnoreCase);
        if (isPending)
        {
            filteredQuery = filteredQuery.Where(d => d.TrangThai == "CHO_DUYET")
                                         .OrderByDescending(d => d.NgayGui);
        }
        else
        {
            filteredQuery = filteredQuery.Where(d => d.TrangThai != "CHO_DUYET")
                                         .OrderByDescending(d => d.NgayDuyet ?? d.NgayGui);
        }

        // 6. Lọc theo loại đơn nếu client chỉ định
        if (!string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) && int.TryParse(type, out var maLoai))
        {
            filteredQuery = filteredQuery.Where(d => d.MaLoaiDon == maLoai);
        }

        var donTus = await filteredQuery.ToListAsync();

        // 7. Chuyển đổi sang DTO
        var items = donTus.Select(d => new LeaveRequestListItemDto(
            MaDon: d.MaDon,
            MaNhanVien: d.MaNhanVien,
            HoTenNhanVien: d.NhanVien != null ? $"{d.NhanVien.Ho} {d.NhanVien.Ten}".Trim() : $"NV#{d.MaNhanVien}",
            ChucVu: d.NhanVien?.ChucVu?.TenChucVu,
            MaPhongBan: d.NhanVien?.MaPhongBan,
            TenPhongBan: d.NhanVien?.PhongBan?.TenPhongBan,
            MaLoaiDon: d.MaLoaiDon,
            TenLoaiDon: d.LoaiDon?.TenLoaiDon ?? $"Loại #{d.MaLoaiDon}",
            CoHuongLuong: d.LoaiDon?.CoHuongLuong ?? false,
            TuNgay: d.TuNgay,
            DenNgay: d.DenNgay,
            LyDo: d.LyDo,
            NgayGui: d.NgayGui,
            TrangThai: d.TrangThai,
            PhanHoiDuyet: d.PhanHoiDuyet,
            NgayDuyet: d.NgayDuyet,
            HoTenNguoiDuyet: d.NguoiDuyet != null ? $"{d.NguoiDuyet.Ho} {d.NguoiDuyet.Ten}".Trim() : null
        )).ToList();

        return Ok(new LeaveRequestListResponse(
            Items: items,
            PendingCount: pendingCount,
            Source: "production"
        ));
    }

    /// <summary>
    /// Phê duyệt hoặc từ chối một đơn từ (Atomically và ngăn chặn Race Condition).
    /// </summary>
    [HttpPost("{maDon:int}/review")]
    public async Task<IActionResult> ReviewLeaveRequest(
        [FromRoute] int maDon,
        [FromBody] LeaveReviewRequest request)
    {
        try
        {
            return await ReviewLeaveRequestCoreAsync(maDon, request);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while reviewing leave request {RequestId}.", maDon);
            return DatabaseUnavailableResult();
        }
    }

    private async Task<IActionResult> ReviewLeaveRequestCoreAsync(int maDon, LeaveReviewRequest request)
    {
        if (request == null)
        {
            return BadRequest(new LeaveReviewResult(false, "VALIDATION_ERROR", "Dữ liệu yêu cầu không hợp lệ."));
        }

        // 1. Xác thực người duyệt từ JWT và kiểm tra DB
        var (account, errorResult) = await GetCurrentAccountAsync();
        if (errorResult != null) return errorResult;

        var role = account!.VaiTro?.TenVaiTro;
        var deptId = account.NhanVien?.MaPhongBan;
        var reviewerEmployeeId = account.MaNhanVien;

        // 2. Tìm đơn từ trong DB kèm nhân viên nộp đơn
        var donTu = await _context.DonTus
            .Include(d => d.NhanVien)
            .FirstOrDefaultAsync(d => d.MaDon == maDon);

        if (donTu == null)
        {
            return NotFound(new LeaveReviewResult(false, "NOT_FOUND", "Không tìm thấy đơn từ yêu cầu."));
        }

        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        if (normalizedRole is null or RoleType.Employee ||
            (normalizedRole == RoleType.Manager && (deptId is not > 0 || donTu.NhanVien?.MaPhongBan != deptId)))
            return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Đơn không thuộc phạm vi được phép."));
        var permission = OvertimeHotel.HRM.Core.Policies.MobilePermissionPolicy.ReviewPermission(donTu.MaLoaiDon);
        if (!OvertimeHotel.HRM.WebAdmin.Security.WebAdminPermissions.HasPermission(User, permission))
            return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Bạn không còn quyền xử lý loại đơn này."));

        var todayVietnam = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").DateTime);
        if (donTu.MaLoaiDon is >= 1 and <= 4 && donTu.TuNgay <= todayVietnam)
            return BadRequest(new LeaveReviewResult(false, "EXPIRED", "Đơn nghỉ đã hết hạn, không thể xử lý."));

        // 3. Thẩm định qua LeaveApprovalPolicy
        var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateReviewAction(
            role: normalizedRole is RoleType.IT or RoleType.Manager ? "HR" : role,
            reviewerDeptId: deptId,
            requestDeptId: donTu.NhanVien?.MaPhongBan,
            maLoaiDon: donTu.MaLoaiDon,
            currentStatus: donTu.TrangThai,
            approved: request.Approved,
            phanHoi: request.PhanHoi,
            reviewerEmployeeId: reviewerEmployeeId,
            requestEmployeeId: donTu.MaNhanVien
        );

        if (!isValid)
        {
            return errorCode switch
            {
                "ALREADY_REVIEWED" => StatusCode(409, new LeaveReviewResult(false, errorCode, errorMessage!)),
                // Tự duyệt và không đủ thẩm quyền đều là bị từ chối — cùng lớp
                // lỗi 403 để client xử lý một kiểu.
                "FORBIDDEN" or "SELF_REVIEW_FORBIDDEN" =>
                    StatusCode(403, new LeaveReviewResult(false, errorCode, errorMessage!)),
                _ => BadRequest(new LeaveReviewResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!))
            };
        }

        // 4. Cập nhật nguyên tử (Optimistic Concurrency Check: Chỉ update nếu trang_thai == 'CHO_DUYET')
        var newStatus = request.Approved ? "DA_DUYET" : "TU_CHOI";
        var now = DateTimeOffset.UtcNow;
        var trimmedFeedback = string.IsNullOrWhiteSpace(request.PhanHoi) ? null : request.PhanHoi.Trim();

        var affected = await _context.DonTus
            .Where(d => d.MaDon == maDon && d.TrangThai == "CHO_DUYET" && (d.MaLoaiDon == 5 || d.TuNgay > todayVietnam))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TrangThai, newStatus)
                .SetProperty(d => d.NgayDuyet, now)
                .SetProperty(d => d.MaNguoiDuyet, reviewerEmployeeId)
                .SetProperty(d => d.PhanHoiDuyet, trimmedFeedback)
            );

        if (affected == 0)
        {
            _logger.LogWarning("Xung đột xử lý đồng thời đơn #{MaDon}: Đơn đã bị cập nhật bởi người khác.", maDon);
            return StatusCode(409, new LeaveReviewResult(false, "ALREADY_REVIEWED", "Đơn từ đã được xử lý bởi người khác trước đó."));
        }

        _logger.LogInformation("Người duyệt #{ReviewerId} ({Role}) đã {Action} đơn #{MaDon} thành công.",
            reviewerEmployeeId, role, request.Approved ? "duyệt" : "từ chối", maDon);

        return Ok(new LeaveReviewResult(
            Success: true,
            Code: "REVIEWED",
            Message: request.Approved ? "Phê duyệt đơn thành công." : "Đã từ chối đơn thành công."
        ));
    }

    /// <summary>
    /// Nhân viên rút đơn nghỉ phép đang ở trạng thái CHO_DUYET.
    /// Hỗ trợ cả 2 route để tương thích tuyệt đối.
    /// </summary>
    [Authorize(AuthenticationSchemes = "MobileBearer", Policy = OvertimeHotel.HRM.WebAdmin.Security.PermissionCodes.SubmitRequest)]
    [HttpPost("{maDon:int}/withdraw")]
    [HttpPost("/api/leaverequests/{maDon:int}/withdraw")]
    public async Task<IActionResult> WithdrawLeaveRequest([FromRoute] int maDon)
    {
        try
        {
            var (account, errorResult) = await GetCurrentAccountAsync();
            if (errorResult != null) return errorResult;

            var currentEmployeeId = account!.MaNhanVien;

            // 1. Tìm đơn từ trong DB
            var donTu = await _context.DonTus.FirstOrDefaultAsync(d => d.MaDon == maDon);
            if (donTu == null)
            {
                return NotFound(new LeaveReviewResult(false, "NOT_FOUND", "Không tìm thấy đơn từ yêu cầu."));
            }

            // 2. Zero Trust: Phân quyền - Người gọi bắt buộc phải là chủ đơn
            if (donTu.MaNhanVien != currentEmployeeId)
            {
                return StatusCode(403, new LeaveReviewResult(false, "FORBIDDEN", "Bạn không có quyền rút đơn của người khác."));
            }

            // 3. Kiểm tra trạng thái hợp lệ để rút
            if (donTu.TrangThai != "CHO_DUYET")
            {
                if (donTu.TrangThai == "DA_HUY")
                {
                    return StatusCode(409, new LeaveReviewResult(false, "ALREADY_WITHDRAWN", "Đơn từ này đã được rút trước đó."));
                }
                return StatusCode(409, new LeaveReviewResult(false, "ALREADY_REVIEWED", "Đơn từ đã được xử lý bởi cấp quản lý nên không thể rút."));
            }

            // 4. Conditional Update (Optimistic Concurrency)
            var affected = await _context.DonTus
                .Where(d => d.MaDon == maDon && d.MaNhanVien == currentEmployeeId && d.TrangThai == "CHO_DUYET")
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.TrangThai, "DA_HUY"));

            if (affected == 0)
            {
                return StatusCode(409, new LeaveReviewResult(false, "CONCURRENCY_CONFLICT", "Đơn từ đã được xử lý hoặc thay đổi trạng thái trước đó."));
            }

            _logger.LogInformation("Nhân viên #{EmployeeId} đã rút đơn #{MaDon} thành công.", currentEmployeeId, maDon);

            return Ok(new LeaveReviewResult(true, "WITHDRAWN", "Đã rút đơn nghỉ phép thành công."));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            _logger.LogError(ex, "Database unavailable while withdrawing leave request {RequestId}.", maDon);
            return DatabaseUnavailableResult();
        }
    }

    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DbException or TimeoutException ||
        exception.InnerException is DbException or TimeoutException;

    private ObjectResult DatabaseUnavailableResult() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new LeaveReviewResult(false, "SERVICE_UNAVAILABLE",
                "Không thể truy cập dữ liệu lúc này. Vui lòng thử lại sau."));
    private async Task<(Core.Models.TaiKhoan? Account, IActionResult? Error)> GetCurrentAccountAsync()
    {
        // Token do Supabase GoTrue cấp: mã nhân viên nằm trong app_metadata và đã
        // được rải ra claim phẳng "ma_nhan_vien" ở OnTokenValidated. Không dùng
        // ClaimTypes.NameIdentifier vì "sub" của Supabase là UUID của auth.users —
        // parse ra int luôn fail, dù token hoàn toàn hợp lệ.
        var maNhanVien = SupabaseClaims.GetMaNhanVien(User);
        if (maNhanVien is not > 0)
        {
            return (null, Unauthorized(new LeaveReviewResult(false, "UNAUTHENTICATED", "Token xác thực không hợp lệ.")));
        }

        // Tra theo mã nhân viên (unique index) thay vì mã tài khoản. Vẫn kiểm
        // tra TrangThai trong CSDL: token đã cấp vẫn còn hiệu lực sau khi quản
        // trị viên khoá tài khoản (Zero Trust).
        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .FirstOrDefaultAsync(t => t.MaNhanVien == maNhanVien.Value);

        if (account == null || account.TrangThai != true)
        {
            return (null, Unauthorized(new LeaveReviewResult(false, "UNAUTHENTICATED", "Tài khoản không tồn tại hoặc đã bị khóa.")));
        }

        return (account, null);
    }
}
