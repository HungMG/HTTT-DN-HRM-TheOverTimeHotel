namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>The web administration switches; mobile-only and legacy permissions are preserved separately.</summary>
public static class PermissionConfiguration
{
    public static readonly string[] FixedItCodes = [PermissionCodes.ManageAccounts, PermissionCodes.ProvisionMobileAccount,
        PermissionCodes.ManagePermissions, PermissionCodes.BackupDatabase];
    public static readonly string[] VisibleCodes = [PermissionCodes.ManageEmployees, PermissionCodes.ManageContracts,
        PermissionCodes.ManageCatalog, PermissionCodes.ViewShifts, PermissionCodes.AssignShifts,
        PermissionCodes.ViewShiftSwaps, PermissionCodes.ApproveShiftSwaps, PermissionCodes.ViewDepartmentAttendance,
        PermissionCodes.ViewAttendanceExplanations, PermissionCodes.VerifyAttendanceExplanations,
        PermissionCodes.ViewLeave, PermissionCodes.ApproveLeave, PermissionCodes.ApproveResignation,
        PermissionCodes.ManageHotelEvents, PermissionCodes.ViewPayroll, PermissionCodes.CalculatePayroll, PermissionCodes.ApprovePayroll,
        PermissionCodes.ReportView, PermissionCodes.ReportExport, PermissionCodes.MobileCheckIn, PermissionCodes.SubmitRequest, PermissionCodes.ManageAccounts,
        PermissionCodes.ProvisionMobileAccount, PermissionCodes.ManagePermissions, PermissionCodes.BackupDatabase];
    public static readonly IReadOnlyDictionary<string, string> RequiresView = new Dictionary<string, string>
    {
        [PermissionCodes.AssignShifts] = PermissionCodes.ViewShifts,
        [PermissionCodes.ApproveShiftSwaps] = PermissionCodes.ViewShiftSwaps,
        [PermissionCodes.VerifyAttendanceExplanations] = PermissionCodes.ViewAttendanceExplanations,
        [PermissionCodes.ApproveLeave] = PermissionCodes.ViewLeave,
        [PermissionCodes.CalculatePayroll] = PermissionCodes.ViewPayroll,
        [PermissionCodes.ApprovePayroll] = PermissionCodes.ViewPayroll
    };

    public static void AddRequiredPermissions(ISet<string> codes, string? role)
    {
        foreach (var (action, view) in RequiresView)
            if (codes.Contains(action)) codes.Add(view);
        if (role == "IT") foreach (var code in FixedItCodes) codes.Add(code);
    }
}
