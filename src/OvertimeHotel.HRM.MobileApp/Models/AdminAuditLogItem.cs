namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Đại diện một dòng nhật ký hoạt động quản trị hệ thống hiển thị trên Mobile App.
/// </summary>
public class AdminAuditLogItem
{
    public long AuditLogId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string TargetUsername { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }

    public string FormattedTime => OccurredAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm · dd/MM/yyyy");

    public string BadgeColor => ActionCode switch
    {
        "USER_CREATE" or "ACCOUNT_CREATE" or "APPROVE" => "#DCFCE7", // Xanh lục nhạt
        "USER_LOCK" or "ACCOUNT_LOCK" or "REJECT" => "#FEE2E2",      // Đỏ nhạt
        "SHIFT_CHANGE" or "UPDATE" => "#FEF3C7",                     // Vàng hổ phách nhạt
        _ => "#EFF6FF"                                               // Xanh navy nhạt
    };

    public string BadgeTextColor => ActionCode switch
    {
        "USER_CREATE" or "ACCOUNT_CREATE" or "APPROVE" => "#15803D",
        "USER_LOCK" or "ACCOUNT_LOCK" or "REJECT" => "#B91C1C",
        "SHIFT_CHANGE" or "UPDATE" => "#B45309",
        _ => "#1E3A8A"
    };
}
