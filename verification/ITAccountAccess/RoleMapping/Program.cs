using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;

foreach(var (position, department, sender, expected, blocked) in new[] {
    ("Giám Sát Ca (Shift Supervisor)", "Bộ phận An ninh (Security)", "HR", 2, true),
    ("Trưởng phòng", "CNTT", "HR", 3, true), ("IT Support", "IT", "HR", 3, true),
    ("Nhân viên", "Security", "HR", 1, false),
    ("Nhân viên", "Security", "Admin", 1, false),
    ("Giám Sát Ca (Shift Supervisor)", "Bộ phận An ninh (Security)", "Admin", 2, true),
    ("Nhân viên", "Security", "IT", 1, false),
    ("IT Support", "IT", "IT", 3, false),
    ("Trưởng phòng", "Nhân sự", "HR", 4, true), ("Lễ tân", "Lễ tân", "HR", 1, false),
    ("Tổng Giám Đốc", "Ban Giám Đốc", "IT", 5, false), ("Tổng Giám Đốc", "Ban Giámốc", "HR", 2, true)
}) {
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    db.AddRange(new ChucVu { MaChucVu=1, TenChucVu=position }, new PhongBan { MaPhongBan=1, TenPhongBan=department },
      new NhanVien { MaNhanVien=1, Ho="Test", Ten="Test", TrangThai="DANG_LAM", EmailCongVu="test@example.invalid", MaChucVu=1, MaPhongBan=1 },
      new VaiTro { MaVaiTro=1, TenVaiTro="Employee" }, new VaiTro { MaVaiTro=2, TenVaiTro="Manager" }, new VaiTro { MaVaiTro=3, TenVaiTro="IT" }, new VaiTro { MaVaiTro=4, TenVaiTro="HR" }, new VaiTro { MaVaiTro=5, TenVaiTro="Admin" });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, new Provisioner());
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"1"), new Claim("EmployeeId","2"), new Claim(ClaimTypes.Role,sender) }, "Test")) };
    http.Request.Headers.XRequestedWith="XMLHttpRequest";
    controller.ControllerContext = new ControllerContext { HttpContext=http };
    var popup = await controller.Create(true, 1);
    if (blocked)
    {
        if (popup is not PartialViewResult { ViewName: "_AccountRequestUnavailable" })
            throw new Exception($"Expected an immediate unavailable notice for {position}/{sender}");
        var employee = await db.NhanViens.FindAsync(1) ?? throw new Exception("Employee missing");
        employee.EmailCongVu = "";
        await db.SaveChangesAsync();
        if (await controller.Create(true, 1) is not PartialViewResult { ViewName: "_AccountRequestUnavailable" })
            throw new Exception($"Role notice missing for employee without email: {position}/{sender}");
        employee.EmailCongVu = "test@example.invalid";
        await db.SaveChangesAsync();
    }
    else if (popup is not PartialViewResult { ViewName: "_CreateRequestPopup", Model: CreateYeuCauTaiKhoanViewModel popupModel } || popupModel.EmployeeId != 1 || !popupModel.EmployeeOptions.Any(option => option.Selected && option.Value == "1"))
        throw new Exception($"Popup did not select the requested employee for {sender}");
    var model = new CreateYeuCauTaiKhoanViewModel { EmployeeId=1, RoleId=5, Username="testuser" };
    await controller.Create(model, true); // Missing contract stops before any database writes.
    if(controller.ModelState.TryGetValue("RoleId", out var roleState) && roleState.Errors.Count > 0) throw new Exception($"Submitted role was trusted for {position}/{department}/{sender}");
    if(model.RoleId != expected) throw new Exception($"{position}/{department}/{sender}: expected {expected}, got {model.RoleId}");
    var denied = controller.ModelState.TryGetValue("EmployeeId", out var employeeState) && employeeState.Errors.Count > 0;
    if (denied != blocked) throw new Exception($"Incorrect request access for {position}/{department}/{sender}");
    Console.WriteLine($"PASS {position}/{department}/{sender}: role {expected}, blocked {blocked}");
}
sealed class Provisioner : ISupabaseUserProvisioningService {
 public bool IsConfigured => false;
 public Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
 public Task<SupabaseUserProvisionResult> CreateUserAsync(string email,string password,CancellationToken cancellationToken=default) => throw new Exception("Not expected");
}
