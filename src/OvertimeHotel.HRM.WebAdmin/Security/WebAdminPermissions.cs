using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Tiện ích kiểm tra quyền RBAC dùng chung cho tầng giao diện (Razor View).
/// Các quyền quản trị cố định của IT luôn được cấp; các quyền còn lại
/// và quyền của vai trò khác dựa trên claim đã nạp từ cơ sở dữ liệu.
/// </summary>
public static class WebAdminPermissions
{
    /// <summary>
    /// Kiểm tra người dùng hiện tại có quyền chức năng tương ứng hay không.
    /// </summary>
    /// <param name="user">Principal của phiên đăng nhập hiện tại.</param>
    /// <param name="permissionCode">Mã quyền lấy từ bảng <c>QUYEN.ma_quyen_code</c>.</param>
    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode)
    {
        if (user is null)
        {
            return false;
        }

        if (user.Identity?.IsAuthenticated != true) return false;
        var fixedItPermission = permissionCode is PermissionCodes.ManageAccounts
            or PermissionCodes.ManagePermissions or PermissionCodes.ProvisionMobileAccount
            or PermissionCodes.BackupDatabase;
        return (fixedItPermission && user.IsInRole("IT"))
            || user.HasClaim(PermissionCodes.ClaimType, permissionCode);
    }
}
