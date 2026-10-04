using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Interface dịch vụ chấm công Check-in/Check-out dành cho nhân viên trên Mobile App.
/// Hỗ trợ cả Production Mode (kết nối JWT API) và Demo Mode (chạy trên bộ nhớ giả lập).
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Cho biết dịch vụ đang chạy ở chế độ Demo hay Production.
    /// </summary>
    bool IsDemoMode { get; }

    /// <summary>
    /// Lấy ca làm việc hôm nay kèm trạng thái đã quẹt thẻ hay chưa.
    /// </summary>
    Task<TodayShiftDto> GetTodayShiftAsync();

    /// <summary>
    /// Quẹt thẻ vào ca. Toạ độ và tên thiết bị được gửi lên để đối chiếu chống gian lận.
    /// </summary>
    Task<CheckInResponse> CheckInAsync(int maPhanCa, string? toaDo, string? thietBi);

    /// <summary>
    /// Quẹt thẻ ra ca. Máy chủ tự tính phút về sớm và giờ làm thêm.
    /// </summary>
    Task<CheckInResponse> CheckOutAsync(int maPhanCa, string? toaDo, string? thietBi);

    /// <summary>
    /// Lịch sử chấm công của nhân viên trong tháng.
    /// </summary>
    Task<List<MonthlyAttendanceDto>> GetMonthlyAttendanceAsync(int year, int month);
}