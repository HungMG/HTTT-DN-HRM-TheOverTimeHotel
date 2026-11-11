using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.Core.Policies;
using System.Security.Claims;
using OvertimeHotel.HRM.WebAdmin.Security;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
var manager = new UserSession { IsLoggedIn = true, TenVaiTro = "Manager", MaPhongBan = 1 };
Check(!manager.CanApproveLeave, "A role alone must not grant approval");
manager.Permissions.Add(MobilePermissionPolicy.ApproveOvertime);
Check(!manager.CanReviewRequest(2), "Sick leave is not an overtime request");
manager.Permissions.Add(MobilePermissionPolicy.ApproveLeave);
foreach (var type in new[] { 1, 2, 4 })
    Check(manager.CanReviewRequest(type), "Manager must retain review of the leave types allowed in their role");
Check(!manager.CanReviewRequest(3), "Manager must not display maternity leave outside their role scope");
Check(!manager.CanReviewRequest(5), "Revoked resignation permission must hide that category");
Check(!manager.CanReviewRequest(0) && !manager.CanReviewRequest(6), "Unknown request categories must fail closed");
manager.Permissions.Remove(MobilePermissionPolicy.ApproveLeave);
Check(!manager.CanApproveLeave, "Revocation must immediately update computed capabilities");
manager.Permissions.Add(MobilePermissionPolicy.ApproveLeave);
manager.MaPhongBan = null;
Check(!manager.CanApproveLeave, "A manager without a department must fail closed");
manager.TenVaiTro = "Employee";
Check(!manager.CanApproveLeave, "Employee must not gain review capability from a grant alone");
manager.TenVaiTro = "HR";
manager.Permissions.Add(MobilePermissionPolicy.ApproveResignation);
Check(manager.CanReviewRequest(5), "HR with the explicit resignation grant can review it");
manager.Permissions.Remove(MobilePermissionPolicy.ApproveResignation);
Check(!manager.CanReviewRequest(5) && manager.CanReviewRequest(1), "Revoking resignation must hide only that category and retain leave review");
manager.Permissions.Add(MobilePermissionPolicy.ApproveResignation);
manager.IsLoggedIn = false;
Check(!manager.CanApproveLeave && !manager.HasPermission(MobilePermissionPolicy.ApproveLeave), "A logged-out session must deny every grant");
ClaimsPrincipal Principal(string role, bool granted) => new(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.Role, role) }.Concat(granted
        ? new[] { new Claim(PermissionCodes.ClaimType, PermissionCodes.ManageHotelEvents) }
        : Array.Empty<Claim>()), "test"));
foreach (var role in new[] { "Admin", "HR", "Manager", "Employee", "IT" })
    Check(!Principal(role, false).HasPermission(PermissionCodes.ManageHotelEvents), $"{role} must not inherit event access without a grant");
Check(Principal("HR", true).HasPermission(PermissionCodes.ManageHotelEvents), "HR's event grant must be honored");
Check(!Principal("Employee", true).HasPermission(PermissionCodes.ManageHotelEvents), "Employee must not enter the web event workflow");
Check(!Principal("IT", true).HasPermission(PermissionCodes.ManageHotelEvents), "IT administrative role must not grant event access");
Check(Principal("Admin", false).HasPermission(PermissionCodes.AssignShifts), "Other Admin permissions keep their existing behavior");
Console.WriteLine($"PASS: {checks} permission checks");
