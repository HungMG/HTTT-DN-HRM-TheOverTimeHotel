using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class PayrollAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) => !user.IsInRole("Manager") && user.HasPermission(PermissionCodes.CalculatePayroll);
    public static bool CanApprove(ClaimsPrincipal user) => user.HasPermission(PermissionCodes.ApprovePayroll);

    public static bool CanAccess(ClaimsPrincipal user, string? action)
    {
        if (!user.HasPermission(PermissionCodes.ViewPayroll)) return false;
        if (action is "Index" or "Detail" or "DetailPartial" or "Print") return true;
        if (action is "Approve" or "Reject") return CanApprove(user);
        return CanManage(user);
    }
}
