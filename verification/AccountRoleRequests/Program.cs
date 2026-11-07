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
    ("Trưởng Bộ Phận", "Bộ phận An ninh (Security)", "HR", 2, false),
    ("Trưởng phòng", "CNTT", "HR", 3, true), ("IT Support", "IT", "HR", 3, true),
    ("Nhân viên", "Security", "HR", 1, false),
    ("Trưởng phòng", "Nhân sự", "HR", 4, true), ("Lễ tân", "Lễ tân", "HR", 1, false),
    ("Tổng Giám Đốc", "Ban Giám Đốc", "IT", 5, false), ("Tổng Giám Đốc", "Ban Giám Đốc", "HR", 5, true),
    ("Giám đốc nhân sự", "Nhân sự", "HR", 5, true)
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
    var model = new CreateYeuCauTaiKhoanViewModel { EmployeeId=1, RoleId=5, Username="testuser" };
    var popup = await controller.Create(true, 1);
    if (blocked && popup is not PartialViewResult { ViewName: "_AccountRequestUnavailable" } ||
        !blocked && popup is not PartialViewResult { ViewName: "_CreateRequestPopup" })
        throw new Exception($"Incorrect popup access for {position}/{department}/{sender}");
    await controller.Create(model, true); // Missing contract stops before any database writes.
    if(!blocked && controller.ModelState.TryGetValue("RoleId", out var roleState) && roleState.Errors.Count > 0) throw new Exception($"Submitted role was trusted for {position}/{department}/{sender}");
    if(!blocked && model.RoleId != expected) throw new Exception($"{position}/{department}/{sender}: expected {expected}, got {model.RoleId}");
    var denied = controller.ModelState.TryGetValue("EmployeeId", out var employeeState) && employeeState.Errors.Count > 0;
    if (denied != blocked) throw new Exception($"Incorrect request access for {position}/{department}/{sender}");
    Console.WriteLine($"PASS {position}/{department}/{sender}: role {expected}, blocked {blocked}");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)) {
    var today = DateOnly.FromDateTime(DateTime.Today);
    db.AddRange(new VaiTro { MaVaiTro=1, TenVaiTro="HR" }, new VaiTro { MaVaiTro=2, TenVaiTro="Manager" },
        new NhanVien { MaNhanVien=1, Ho="Target", Ten="Manager", TrangThai="DANG_LAM", EmailCongVu="manager@example.invalid" },
        new NhanVien { MaNhanVien=2, Ho="Sender", Ten="HR", TrangThai="DANG_LAM" },
        new TaiKhoan { MaTaiKhoan=2, MaNhanVien=2, MaVaiTro=1, TenDangNhap="hr" },
        new HopDong { MaHopDong=1, MaNhanVien=1, TrangThai="HIEU_LUC", NgayBatDau=today },
        new YeuCauTaiKhoan { MaYeuCau=1, MaNhanVien=1, MaHopDong=1, MaVaiTro=2, MaNguoiGui=2, TrangThai="CHO_IT", TenDangNhap="manager" });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"3"), new Claim(ClaimTypes.Role,"IT") }, "Test")) };
    http.Request.Headers.XRequestedWith="XMLHttpRequest";
    var service = new Provisioner();
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, service, new FakeLock()) { ControllerContext = new ControllerContext { HttpContext=http } };
    var result = await controller.Complete(1);
    if (result is not ObjectResult { StatusCode: 502 } || service.Calls != 1) throw new Exception("IT could not process HR manager request");
    http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"3"), new Claim(ClaimTypes.Role,"Admin") }, "Test"));
    result = await controller.Complete(1);
    if (result is not ObjectResult { StatusCode: 403 } || service.Calls != 1) throw new Exception("A non-IT account processed HR's request");
    Console.WriteLine("PASS IT processes HR manager request");
}
sealed class Provisioner : ISupabaseUserProvisioningService {
 public bool IsConfigured => false;
 public int Calls { get; private set; }
 public Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
 public Task<SupabaseUserProvisionResult> CreateUserAsync(string email,string password,CancellationToken cancellationToken=default) { Calls++; return Task.FromResult(SupabaseUserProvisionResult.Failed(SupabaseUserProvisionError.NetworkFailure, "test")); }
}
sealed class FakeLock : OvertimeHotel.HRM.WebAdmin.Services.IAccountRequestLock, IAsyncDisposable { public Task<IAsyncDisposable?> TryAcquireAsync(int id, CancellationToken c) => Task.FromResult<IAsyncDisposable?>(this); public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
