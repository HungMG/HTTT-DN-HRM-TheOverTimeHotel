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
    /// Lấy danh sách đơn và số lượng chờ duyệt trong cùng 1 request tối ưu network.
    /// </summary>
    Task<LeaveRequestListResponse> GetLeaveRequestsAsync(string view = "pending", string type = "all");

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
}
