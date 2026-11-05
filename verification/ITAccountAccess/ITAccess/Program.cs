using System.Security.Claims;
using OvertimeHotel.HRM.WebAdmin.Security;
ClaimsPrincipal User(string role, params string[] perms) => new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,role)}.Concat(perms.Select(p => new Claim(PermissionCodes.ClaimType,p))), "test"));
void Check(bool value, bool expected, string name) { if(value != expected) throw new Exception(name); }
Check(User("Admin").HasPermission(PermissionCodes.ManageAccounts), false, "Admin must not access account administration");
Check(User("HR", PermissionCodes.ManageAccounts).HasPermission(PermissionCodes.ManageAccounts), false, "HR must not access account administration");
Check(User("IT").HasPermission(PermissionCodes.ManagePermissions), true, "IT must manage RBAC");
Check(User("IT", PermissionCodes.ReportView).HasPermission(PermissionCodes.ReportView), false, "IT must not access reports");
Check(User("Admin").HasPermission(PermissionCodes.ReportView), true, "Director retains business access");
Console.WriteLine("Access policy checks passed");
