using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>Chooses the first visible, authorized Web Admin destination.</summary>
public static class WebAdminLanding
{
    private static readonly string[] DashboardPermissions =
    [
        PermissionCodes.ManageEmployees, PermissionCodes.ManageContracts,
        PermissionCodes.ViewDepartmentAttendance, PermissionCodes.ViewShifts,
        PermissionCodes.ViewLeave, PermissionCodes.ApproveLeave, PermissionCodes.ApproveResignation,
        PermissionCodes.CalculatePayroll, PermissionCodes.ViewPayroll
    ];

    public static bool CanViewDashboard(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && !user.IsInRole("IT") && !user.IsInRole("Manager") &&
        DashboardPermissions.All(user.HasPermission);

    public static (string Controller, string Action) Resolve(ClaimsPrincipal user)
    {
        if (CanViewDashboard(user)) return ("Home", "Index");
        if (user.HasPermission(PermissionCodes.ManageHotelEvents)) return ("SuKien", "Index");
        if (user.HasPermission(PermissionCodes.ManageEmployees)) return ("NhanVien", "Index");
        if (user.HasPermission(PermissionCodes.ManageContracts)) return ("HopDong", "Index");
        if (user.HasPermission(PermissionCodes.ManageCatalog)) return ("PhongBan", "Index");
        if (user.HasPermission(PermissionCodes.ViewShifts)) return ("PhanCa", "Index");
        if (user.HasPermission(PermissionCodes.ViewDepartmentAttendance)) return ("ChamCong", "Index");
        if (user.HasPermission(PermissionCodes.ViewAttendanceExplanations)) return ("GiaiTrinhChamCong", "Index");
        if (user.HasPermission(PermissionCodes.ViewShiftSwaps)) return ("YeuCauDoiCa", "Index");
        if (user.HasPermission(PermissionCodes.ViewLeave) || user.HasPermission(PermissionCodes.ApproveResignation)) return ("DonTu", "Index");
        if (user.HasPermission(PermissionCodes.ViewPayroll)) return ("BangLuong", "Index");
        if (user.HasPermission(PermissionCodes.ReportView)) return ("BaoCao", "Index");
        if (user.HasPermission(PermissionCodes.ManageAccounts)) return ("UserAccounts", "Index");
        if (user.HasPermission(PermissionCodes.ManagePermissions)) return ("UserAccounts", "RolePermissions");
        if (user.HasPermission(PermissionCodes.ProvisionMobileAccount)) return ("YeuCauTaiKhoan", "Index");
        if (user.HasPermission(PermissionCodes.BackupDatabase)) return ("Backup", "Index");
        return ("Home", "Index");
    }

    public static bool IsDashboardUrl(string url)
    {
        var path = url.Split('?', '#')[0].TrimEnd('/');
        return path.Length == 0 || path.Equals("/Home", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase);
    }
}
