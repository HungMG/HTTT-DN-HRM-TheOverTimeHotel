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
manager.Permissions.Add("UNRELATED_PERMISSION");
Check(!manager.CanReviewRequest(2), "An unrelated permission does not grant leave approval");
manager.Permissions.Add(MobilePermissionPolicy.ViewLeave);
Check(manager.CanViewLeave && manager.CanViewRequest(1), "View-only grant opens the leave list");
Check(!manager.CanReviewRequest(1), "View-only grant never allows approval");
manager.Permissions.Add(MobilePermissionPolicy.ApproveLeave);
foreach (var type in new[] { 1, 2, 3, 4 })
    Check(manager.CanReviewRequest(type), "Manager must retain review of the leave types allowed in their role");
Check(manager.CanReviewRequest(3), "Explicit leave approval includes maternity leave within the department");
Check(!manager.CanReviewRequest(5), "Revoked resignation permission must hide that category");
Check(!manager.CanReviewRequest(0) && !manager.CanReviewRequest(6), "Unknown request categories must fail closed");
manager.Permissions.Remove(MobilePermissionPolicy.ApproveLeave);
Check(manager.CanViewRequest(1), "Revoking approval retains view access");
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
foreach (var role in new[] { "HR", "IT", "Manager" })
    Check(Principal(role, true).HasPermission(PermissionCodes.ManageHotelEvents), $"{role}'s explicit event grant must be honored");
var scheduleViewer = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim(ClaimTypes.Role, "Manager"), new Claim(PermissionCodes.ClaimType, PermissionCodes.ViewShifts) }, "test"));
Check(scheduleViewer.HasPermission(PermissionCodes.ViewShifts), "Viewing shifts must not depend on shift assignment");
Check(!scheduleViewer.HasPermission(PermissionCodes.AssignShifts), "Viewing shifts must not imply assignment");
var managerPayroll = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim(ClaimTypes.Role, "Manager"), new Claim(PermissionCodes.ClaimType, PermissionCodes.ViewPayroll) }, "test"));
var paid = OvertimeHotel.HRM.Core.Constants.SalaryConstants.PAYSLIP_PAID;
var draft = OvertimeHotel.HRM.Core.Constants.SalaryConstants.PAYSLIP_DRAFT;
Check(ManagerPayrollAccess.CanView(managerPayroll, 1, 1, paid), "Manager can see their own published payslip");
Check(!ManagerPayrollAccess.CanView(managerPayroll, 1, 2, paid), "Manager cannot see another employee’s payslip");
Check(!ManagerPayrollAccess.CanView(managerPayroll, null, null, paid), "Missing employee IDs must fail closed");
Check(!ManagerPayrollAccess.CanView(managerPayroll, 1, 1, draft), "Manager cannot see a draft payslip");
Check(!ManagerPayrollAccess.CanView(Principal("Manager", false), 1, 1, paid), "Manager cannot view payroll without the payroll permission");
var viewer = LandingUserForPayroll("Admin", PermissionCodes.ViewPayroll);
var approver = LandingUserForPayroll("Admin", PermissionCodes.ViewPayroll, PermissionCodes.ApprovePayroll);
var operatorUser = LandingUserForPayroll("HR", PermissionCodes.ViewPayroll, PermissionCodes.CalculatePayroll);
ClaimsPrincipal LandingUserForPayroll(string role, params string[] codes) => new(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.Role, role) }.Concat(codes.Select(c => new Claim(PermissionCodes.ClaimType, c))), "test"));
Check(PayrollAuthorization.CanAccess(viewer, "Index") && !PayrollAuthorization.CanAccess(viewer, "Approve") && !PayrollAuthorization.CanAccess(viewer, "Lock"), "View-only must not grant payroll actions");
Check(PayrollAuthorization.CanAccess(approver, "Approve") && PayrollAuthorization.CanAccess(approver, "Reject") && !PayrollAuthorization.CanAccess(approver, "Lock"), "Approver can approve or reject but cannot lock");
Check(PayrollAuthorization.CanAccess(operatorUser, "Lock") && PayrollAuthorization.CanAccess(operatorUser, "PublishDraft") && PayrollAuthorization.CanAccess(operatorUser, "SubmitApproval") && !PayrollAuthorization.CanAccess(operatorUser, "Approve"), "Payroll manager can publish and lock but cannot approve");
Check(!PayrollAuthorization.CanAccess(LandingUserForPayroll("Admin", PermissionCodes.ApprovePayroll), "Approve"), "Approval alone without view must be denied");
Check(!Principal("Admin", false).HasPermission(PermissionCodes.AssignShifts), "Admin cannot bypass explicit grants");
Check(Principal("IT", false).HasPermission(PermissionCodes.ManagePermissions), "IT retains fixed administration permissions");
Check(!Principal("HR", false).HasPermission(PermissionCodes.ManagePermissions), "Administration permission is not granted to other roles by default");
var grants = new HashSet<string> { PermissionCodes.ApproveLeave, PermissionCodes.AssignShifts, PermissionCodes.CalculatePayroll, PermissionCodes.ApprovePayroll };
PermissionConfiguration.AddRequiredPermissions(grants, "Manager");
Check(grants.Contains(PermissionCodes.ViewLeave) && grants.Contains(PermissionCodes.ViewShifts) && grants.Contains(PermissionCodes.ViewPayroll), "Action grants require corresponding view grants");
Check(grants.Contains(PermissionCodes.ViewPayroll), "Payroll approval requires the view grant");
Check(ManagerPayrollAccess.CanView(managerPayroll, 1, 1, "DA_DUYET", true), "Manager can see their own approved payslip");
Check(!ManagerPayrollAccess.CanView(managerPayroll, 1, 1, "DA_DUYET", false), "Unapproved salary cannot be viewed");
var existingReportView = new HashSet<string> { PermissionCodes.ReportView };
PermissionConfiguration.AddRequiredPermissions(existingReportView, "IT");
Check(!existingReportView.Contains(PermissionCodes.ReportExport), "Saving unrelated settings must not restore a previously revoked report export grant");
Check(!KnowledgeAccessPolicy.CanIngestKnowledge(), "AI knowledge uploads are managed in code, not granted to roles");
Check(KnowledgeAccessPolicy.ResolveDocumentScopes("HR", 8).Contains(KnowledgeAccessPolicy.ScopeWholeKnowledgeBase), "Existing AI knowledge remains readable under its scope");
var itGrants = new HashSet<string>();
PermissionConfiguration.AddRequiredPermissions(itGrants, "IT");
Check(PermissionConfiguration.FixedItCodes.All(itGrants.Contains), "Fixed IT permissions cannot be removed through the form");
ClaimsPrincipal LandingUser(string role, params string[] codes) => new(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.Role, role) }.Concat(codes.Select(c => new Claim(PermissionCodes.ClaimType, c))), "test"));
Check(WebAdminLanding.Resolve(LandingUser("Admin", PermissionCodes.ViewPayroll, PermissionCodes.ReportView)) == ("BangLuong", "Index"), "Admin lands on first permitted page");
Check(WebAdminLanding.Resolve(LandingUser("Manager", PermissionCodes.ViewShifts, PermissionCodes.ViewLeave)) == ("PhanCa", "Index"), "Manager lands on first permitted schedule page");
Check(WebAdminLanding.Resolve(LandingUser("IT")) == ("UserAccounts", "Index"), "IT uses its fixed permissions");
Check(WebAdminLanding.Resolve(LandingUser("HR", PermissionCodes.ManageHotelEvents, PermissionCodes.ManageEmployees)) == ("SuKien", "Index"), "Landing follows sidebar order");
Check(WebAdminLanding.Resolve(LandingUser("HR")) == ("Home", "Index"), "Users without a menu have a safe fallback");
Check(WebAdminLanding.IsDashboardUrl("/?x=1") && WebAdminLanding.IsDashboardUrl("/Home/Index") && !WebAdminLanding.IsDashboardUrl("/BangLuong"), "Only dashboard return URLs are replaced");
Console.WriteLine($"PASS: {checks} permission checks");
