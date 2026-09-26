using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Interface dịch vụ duyệt đơn từ trực tuyến dành cho Manager, HR và Admin trên Mobile App.
/// Tái sử dụng LeaveRequestReviewDto từ thư viện Core để chuẩn hóa payload và đồ thị phụ thuộc.
/// </summary>
public interface ILeaveApprovalService
{
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
    /// Thực thi quyết định duyệt hoặc từ chối thông qua LeaveRequestReviewDto chuẩn.
    /// </summary>
    Task<(bool Success, string Message)> ReviewRequestAsync(LeaveRequestReviewDto reviewDto);

    /// <summary>
    /// Phê duyệt nhanh một đơn từ.
    /// </summary>
    Task<(bool Success, string Message)> ApproveAsync(int maDon, int maNguoiDuyet);

    /// <summary>
    /// Từ chối một đơn từ kèm lý do bắt buộc.
    /// </summary>
    Task<(bool Success, string Message)> RejectAsync(int maDon, int maNguoiDuyet, string lyDoTuChoi);
}
