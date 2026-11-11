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

    public string BadgeColor => GetBadgeStyle(ActionCode).Background;

    public string BadgeTextColor => GetBadgeStyle(ActionCode).Foreground;

    private static (string Background, string Foreground) GetBadgeStyle(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is "USER_CREATE" or "ACCOUNT_CREATE" or "APPROVE"
            or "CREATE_ACCOUNT" or "CREATE_ACCOUNT_AUTH" or "DUYET_BANG_LUONG"
            or "KHOA_SO_VA_PHAT_HANH_PHIEU_LUONG" or "KHOA_SO_BANG_LUONG"
            or "DUYET_DON_TU" or "DUYET_DOI_CA" or "NGHIEM_THU_SU_KIEN"
            or "PHAT_HANH_PHIEU_LUONG")
        {
            return ("#DCFCE7", "#15803D"); // Xanh lục nhạt
        }

        if (normalized is "USER_LOCK" or "ACCOUNT_LOCK" or "REJECT"
            or "LOCK_ACCOUNT" or "REJECT_ACCOUNT_REQUEST" or "TU_CHOI_BANG_LUONG"
            or "HUY_SU_KIEN" or "TU_DONG_HUY_SU_KIEN" or "XOA_KHOAN_LUONG")
        {
            return ("#FEE2E2", "#B91C1C"); // Đỏ nhạt
        }

        if (normalized is "SHIFT_CHANGE" or "UPDATE" or "UPDATE_ROLE_PERMISSION"
            or "UPDATE_ACCOUNT" or "TINH_LAI_PHIEU_LUONG" or "TINH_LUONG_TU_DONG"
            or "GUI_DUYET_BANG_LUONG" or "DIEU_CHINH_LUONG" or "SUA_CONG_GIAI_TRINH"
            or "MO_KHOA_KY_LUONG")
        {
            return ("#FEF3C7", "#B45309"); // Vàng hổ phách nhạt
        }

        return ("#EFF6FF", "#1E3A8A"); // Xanh navy nhạt
    }
}
