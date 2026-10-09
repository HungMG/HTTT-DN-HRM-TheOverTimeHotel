using OvertimeHotel.HRM.Core.Enums;

namespace OvertimeHotel.HRM.Core.Policies;

public static class AttendanceAccessPolicy
{
    public static bool CanView(string? role, int viewerEmployeeId, int? viewerDepartmentId,
        int targetEmployeeId, int? targetDepartmentId)
    {
        var normalized = LeaveApprovalPolicy.NormalizeRole(role);
        if (normalized == null || viewerEmployeeId <= 0 || targetEmployeeId <= 0)
            return false;

        return viewerEmployeeId == targetEmployeeId || normalized is RoleType.Admin or RoleType.HR ||
            (normalized == RoleType.Manager && viewerDepartmentId.HasValue &&
             viewerDepartmentId == targetDepartmentId);
    }
}
