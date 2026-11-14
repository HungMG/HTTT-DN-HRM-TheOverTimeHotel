namespace OvertimeHotel.HRM.Core.Policies;

public static class ItMobileAccessPolicy
{
    // Chức vụ trong danh mục: Trưởng bộ phận CNTT (IT Manager).
    public const int ItHeadPositionId = 12;
    public static bool IsItHead(string? role, int? positionId) =>
        LeaveApprovalPolicy.IsIT(role) && positionId == ItHeadPositionId;

    public static readonly string[] ManagerMobilePermissions =
    [
        MobilePermissionPolicy.CheckIn, MobilePermissionPolicy.SubmitRequest,
        MobilePermissionPolicy.DepartmentAttendance, MobilePermissionPolicy.AssignShifts,
        MobilePermissionPolicy.ViewLeave, MobilePermissionPolicy.ApproveLeave,
        MobilePermissionPolicy.ApproveResignation, MobilePermissionPolicy.Reports
    ];
}
