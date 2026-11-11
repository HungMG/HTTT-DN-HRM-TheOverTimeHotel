namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Đại diện cho một sự kiện / dự án trọng điểm kèm điều động nhân lực (Task Force)
/// theo PRD 02 Mục 9.0 hiển thị trên Dashboard Ban Giám Đốc.
/// </summary>
public class AdminHotelEventItem
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string EventType { get; set; } = "TIỆC CƯỚI";
    public string Location { get; set; } = string.Empty;
    public string TimeRangeText { get; set; } = string.Empty;
    public int ExpectedGuests { get; set; }
    public int TaskForceStaffCount { get; set; }
    public string TaskForceDetail { get; set; } = string.Empty;
    public string StatusText { get; set; } = "Đang diễn ra";
    public string StatusBgColor { get; set; } = "#DCFCE7";
    public string StatusTextColor { get; set; } = "#15803D";
    public string TypeBgColor { get; set; } = "#FEF3C7";
    public string TypeTextColor { get; set; } = "#B45309";
}
