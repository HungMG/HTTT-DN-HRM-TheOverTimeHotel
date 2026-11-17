namespace OvertimeHotel.HRM.Core.Policies;

public static class ItMobileAccessPolicy
{
    // Chức vụ dùng chung cho trưởng các bộ phận. Mã 12 là dữ liệu cũ
    // được nhận diện trong thời gian chuyển đổi cơ sở dữ liệu.
    public const int ItHeadPositionId = 2;
    private const int LegacyItHeadPositionId = 12;
    public static bool IsItHead(string? role, int? positionId) =>
        LeaveApprovalPolicy.IsIT(role) && positionId is ItHeadPositionId or LegacyItHeadPositionId;

    public static readonly string[] ManagerMobilePermissions =
    [
        MobilePermissionPolicy.CheckIn, MobilePermissionPolicy.SubmitRequest,
        MobilePermissionPolicy.DepartmentAttendance, MobilePermissionPolicy.AssignShifts,
        MobilePermissionPolicy.ViewLeave, MobilePermissionPolicy.ApproveLeave,
        MobilePermissionPolicy.ApproveResignation, MobilePermissionPolicy.Reports
    ];
}
