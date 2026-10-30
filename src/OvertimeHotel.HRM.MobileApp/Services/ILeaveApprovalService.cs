using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Interface dịch vụ duyệt đơn từ trực tuyến dành cho Manager, HR và Admin trên Mobile App.
/// Hỗ trợ cả Production Mode (kết nối JWT API) và Demo Mode (chạy trên bộ nhớ giả lập).
/// </summary>
public interface ILeaveApprovalService
{
    /// <summary>
    /// Cho biết dịch vụ đang chạy ở chế độ Demo hay Production.
    /// </summary>
    bool IsDemoMode { get; }

    /// <summary>
    /// Lấy danh sách đơn, số chờ duyệt và trạng thái phân trang theo bộ lọc.
    /// </summary>
    Task<LeaveRequestListResponse> GetLeaveRequestsAsync(string view = "pending", string type = "all",
        int? departmentId = null, int offset = 0, int? pageSize = null);

    Task<List<DepartmentOption>> GetDepartmentOptionsAsync();

    /// <summary>
    /// Phê duyệt hoặc từ chối một đơn từ (Atomic và an toàn concurrency).
    /// </summary>
    Task<LeaveReviewResult> ReviewLeaveRequestAsync(int maDon, bool approved, string? phanHoi);

    /// <summary>
    /// Lấy danh sách đơn đang chờ duyệt (CHO_DUYET) theo vai trò và bộ phận.
    /// </summary>
    Task<List<LeaveRequestItem>> GetPendingRequestsAsync(string role, int? maPhongBan);

    /// <summary>
    /// Lấy danh sách đơn đã xử lý (DA_DUYET, TU_CHOI) theo vai trò và bộ phận.
    /// </summary>
    Task<List<LeaveRequestItem>> GetReviewedRequestsAsync(string role, int? maPhongBan);

    /// <summary>
    /// Đếm số lượng đơn đang chờ duyệt phục vụ hiển thị Badge.
    /// </summary>
    Task<int> GetPendingCountAsync(string role, int? maPhongBan);

    /// <summary>
    /// Phê duyệt nhanh một đơn từ.
    /// </summary>
    Task<(bool Success, string Message)> ApproveAsync(int maDon);

    /// <summary>
    /// Từ chối một đơn từ kèm lý do bắt buộc.
    /// </summary>
    Task<(bool Success, string Message)> RejectAsync(int maDon, string lyDoTuChoi);

    // ═══════════════════════════════════════════════════════════════
    //  PHÍA NHÂN VIÊN NỘP ĐƠN (TSK-18)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Danh sách loại đơn người dùng được nộp, đọc từ bảng LOAI_DON của CSDL.
    /// </summary>
    Task<List<LeaveTypeOption>> GetSubmittableTypesAsync();

    /// <summary>
    /// Lịch sử đơn của CHÍNH người đang đăng nhập, mới nhất trước.
    /// </summary>
    /// <param name="chiChoDuyet">
    /// true thì chỉ lấy đơn còn chờ duyệt; false thì lấy toàn bộ lịch sử.
    /// </param>
    /// <param name="cancellationToken">Token huỷ thao tác mạng nếu cần dừng sớm.</param>
    Task<List<LeaveRequestItem>> GetMyRequestsAsync(bool chiChoDuyet = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nộp một đơn từ mới. Mã nhân viên luôn lấy từ phiên đăng nhập, không nhận
    /// từ tham số để không thể nộp hộ người khác.
    /// </summary>
    Task<LeaveSubmitResult> SubmitRequestAsync(int maLoaiDon, DateOnly tuNgay, DateOnly denNgay, string lyDo);

    /// <summary>
    /// Số ngày phép năm đã dùng trong năm hiện tại (các đơn loại 1 đã được duyệt).
    /// </summary>
    Task<int> GetUsedAnnualLeaveDaysAsync();

    /// <summary>
    /// Nhân viên tự rút đơn nghỉ phép của chính mình (chỉ khi đơn còn ở trạng thái CHO_DUYET).
    /// </summary>
    Task<LeaveReviewResult> WithdrawRequestAsync(int maDon);
}
