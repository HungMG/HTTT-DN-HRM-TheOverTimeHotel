using System.Security.Claims;
using OvertimeHotel.HRM.Core.Constants;

namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class ManagerPayrollAccess
{
    public static bool CanView(ClaimsPrincipal user, int? currentEmployeeId, int? payslipEmployeeId,
        string? status, bool periodApproved = false) =>
        user.IsInRole("Manager") && user.HasPermission(PermissionCodes.ViewPayroll) &&
        currentEmployeeId is > 0 && currentEmployeeId == payslipEmployeeId &&
        (status == SalaryConstants.PAYSLIP_PAID || (status == SalaryConstants.PAYSLIP_APPROVED && periodApproved));
}
