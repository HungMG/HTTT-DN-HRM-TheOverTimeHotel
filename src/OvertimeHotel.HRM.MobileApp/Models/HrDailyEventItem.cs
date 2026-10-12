namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Đại diện cho một sự kiện / ghi chú nhân sự trong ngày trên trang chủ HR.
/// Ví dụ: Sinh nhật nhân viên, Onboarding nhân viên mới, Đánh giá hết hạn thử việc.
/// </summary>
public class HrDailyEventItem
{
    public string Icon { get; set; } = "📌";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string DepartmentBadge { get; set; } = string.Empty;
    public string ActionButtonText { get; set; } = string.Empty;
    public bool HasAction { get; set; }
}
