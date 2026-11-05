using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Tiện ích kiểm tra quyền RBAC dùng chung cho tầng giao diện (Razor View).
/// Nội dung phải khớp với đăng ký policy trong <c>Program.cs</c>: chỉ IT có
/// quyền quản trị tài khoản, phân quyền, cấp tài khoản và sao lưu.
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

        var itAdministration = permissionCode is PermissionCodes.ManageAccounts
            or PermissionCodes.ManagePermissions or PermissionCodes.ProvisionMobileAccount
            or PermissionCodes.BackupDatabase;
        if (itAdministration) return user.IsInRole("IT");
        if (user.IsInRole("IT")) return false;
        return user.IsInRole("Admin") || user.HasClaim(PermissionCodes.ClaimType, permissionCode);
    }
}
