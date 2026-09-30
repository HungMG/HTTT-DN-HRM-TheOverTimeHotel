using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Tiện ích kiểm tra quyền RBAC dùng chung cho tầng giao diện (Razor View).
/// Nội dung phải khớp với đăng ký policy trong <c>Program.cs</c>: Admin luôn đạt
/// mọi quyền do chỉ nắm quyền gốc SYS_ADMIN, các vai trò khác cần có claim "Permission".
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

        return user.IsInRole("Admin") || user.HasClaim(PermissionCodes.ClaimType, permissionCode);
    }
}
