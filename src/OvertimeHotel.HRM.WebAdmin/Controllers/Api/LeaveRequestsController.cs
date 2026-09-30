using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

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

        // 3. Áp dụng RBAC filtering trên server
        IQueryable<Core.Models.DonTu> authorizedQuery;
        if (normalizedRole == RoleType.Admin)
        {
            authorizedQuery = baseQuery; // Admin xem toàn bộ
        }
        else if (normalizedRole == RoleType.HR)
        {
            authorizedQuery = baseQuery.Where(d => d.MaLoaiDon == 3 || d.MaLoaiDon == 5); // HR xem loại 3, 5
        }
        else // Manager
        {
            var managerDept = deptId!.Value;
            authorizedQuery = baseQuery.Where(d =>
                (d.MaLoaiDon == 1 || d.MaLoaiDon == 2 || d.MaLoaiDon == 4) &&
                d.NhanVien != null &&
                d.NhanVien.MaPhongBan == managerDept);
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

        // 3. Thẩm định qua LeaveApprovalPolicy
        var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateReviewAction(
            role: role,
            reviewerDeptId: deptId,
            requestDeptId: donTu.NhanVien?.MaPhongBan,
            maLoaiDon: donTu.MaLoaiDon,
            currentStatus: donTu.TrangThai,
            approved: request.Approved,
            phanHoi: request.PhanHoi
        );

        if (!isValid)
        {
            return errorCode switch
            {
                "ALREADY_REVIEWED" => StatusCode(409, new LeaveReviewResult(false, errorCode, errorMessage!)),
                "FORBIDDEN" => StatusCode(403, new LeaveReviewResult(false, errorCode, errorMessage!)),
                _ => BadRequest(new LeaveReviewResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!))
            };
        }

        // 4. Cập nhật nguyên tử (Optimistic Concurrency Check: Chỉ update nếu trang_thai == 'CHO_DUYET')
        var newStatus = request.Approved ? "DA_DUYET" : "TU_CHOI";
        var now = DateTimeOffset.UtcNow;
        var trimmedFeedback = string.IsNullOrWhiteSpace(request.PhanHoi) ? null : request.PhanHoi.Trim();

        var affected = await _context.DonTus
            .Where(d => d.MaDon == maDon && d.TrangThai == "CHO_DUYET")
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

    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DbException or TimeoutException ||
        exception.InnerException is DbException or TimeoutException;

    private ObjectResult DatabaseUnavailableResult() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new LeaveReviewResult(false, "SERVICE_UNAVAILABLE",
                "Không thể truy cập dữ liệu lúc này. Vui lòng thử lại sau."));
    private async Task<(Core.Models.TaiKhoan? Account, IActionResult? Error)> GetCurrentAccountAsync()
    {
        var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountIdStr, out var accountId))
        {
            return (null, Unauthorized(new LeaveReviewResult(false, "UNAUTHENTICATED", "Token xác thực không hợp lệ.")));
        }

        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .FirstOrDefaultAsync(t => t.MaTaiKhoan == accountId);

        if (account == null || account.TrangThai != true)
        {
            return (null, Unauthorized(new LeaveReviewResult(false, "UNAUTHENTICATED", "Tài khoản không tồn tại hoặc đã bị khóa.")));
        }

        return (account, null);
    }
}
