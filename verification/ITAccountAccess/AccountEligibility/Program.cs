using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Services;

var today = DateOnly.FromDateTime(DateTime.Today);
foreach (var (name, status, start, end, employeeStatus, expectedCalls) in new[] {
    ("expired", "HIEU_LUC", today.AddYears(-1), (DateOnly?)today.AddDays(-1), "DANG_LAM", 0),
    ("terminated", "DA_CHAM_DUT", today.AddYears(-1), (DateOnly?)null, "DANG_LAM", 0),
    ("future", "HIEU_LUC", today.AddDays(1), (DateOnly?)null, "DANG_LAM", 0),
    ("inactive employee", "HIEU_LUC", today.AddYears(-1), (DateOnly?)null, "NGHI_VIEC", 0),
    ("valid through today", "HIEU_LUC", today.AddYears(-1), (DateOnly?)today, "DANG_LAM", 1)
}) {
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    var employee = new NhanVien { MaNhanVien = 1, Ho = "Test", Ten = "Test", TrangThai = employeeStatus, EmailCongVu = "test@example.invalid" };
    var contract = new HopDong { MaHopDong = 1, MaNhanVien = 1, TrangThai = status, NgayBatDau = start, NgayKetThuc = end };
    db.AddRange(employee, contract, new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new YeuCauTaiKhoan { MaYeuCau = 1, MaNhanVien = 1, MaHopDong = 1, MaVaiTro = 1, MaNguoiGui = 1, TrangThai = "CHO_IT" });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var service = new Provisioner();
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, service, new TestLock());
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "IT") }, "Test")) };
    http.Request.Headers.XRequestedWith = "XMLHttpRequest";
    controller.ControllerContext = new ControllerContext { HttpContext = http };
    var result = await controller.Complete(1);
    var code = result is ObjectResult objectResult ? objectResult.StatusCode : (result as JsonResult)?.StatusCode;
    if (service.Calls != expectedCalls || code != (expectedCalls == 0 ? 400 : 502)) throw new Exception($"{name}: status {code}, provisioning calls {service.Calls}");
    if (service.Calls > 0 && service.Password != "Overtime@2026") throw new Exception("Unexpected initial password");
    Console.WriteLine($"PASS {name}");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)) {
    var sender = new NhanVien { MaNhanVien=2, Ho="HR", Ten="Sender", TrangThai="DANG_LAM" };
    var employee = new NhanVien { MaNhanVien=1, Ho="Test", Ten="Manager", TrangThai="DANG_LAM", EmailCongVu="test@example.invalid" };
    db.AddRange(sender, employee, new VaiTro { MaVaiTro=1, TenVaiTro="HR" }, new VaiTro { MaVaiTro=2, TenVaiTro="Manager" },
        new TaiKhoan { MaTaiKhoan=2, MaNhanVien=2, MaVaiTro=1, TenDangNhap="hr", MatKhauBam="hash", TrangThai=true },
        new YeuCauTaiKhoan { MaYeuCau=1, MaNhanVien=1, MaNguoiGui=2, MaVaiTro=2, TrangThai="DANG_XU_LY", MaNguoiXuLy=1 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var service = new Provisioner();
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, service, new TestLock());
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"1"), new Claim(ClaimTypes.Role,"IT") }, "Test")) };
    http.Request.Headers.XRequestedWith="XMLHttpRequest";
    controller.ControllerContext = new ControllerContext { HttpContext=http };
    if (await controller.Complete(1) is not ObjectResult { StatusCode: 403 } || service.Calls != 0) throw new Exception("Legacy privileged HR request was accepted");
    Console.WriteLine("PASS legacy privileged HR request rejected");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)) {
    var service = new Provisioner();
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, service, new BusyLock());
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"1"), new Claim(ClaimTypes.Role,"IT") }, "Test")) };
    http.Request.Headers.XRequestedWith="XMLHttpRequest";
    controller.ControllerContext = new ControllerContext { HttpContext=http };
    if (await controller.Complete(1) is not ObjectResult { StatusCode: 409 } || service.Calls != 0) throw new Exception("Concurrent provision was not stopped");
    if (await controller.Reject(1, "Test") is not ObjectResult { StatusCode: 409 }) throw new Exception("Concurrent rejection was not stopped");
    Console.WriteLine("PASS concurrent issue or rejection returns conflict");
}
sealed class BusyLock : IAccountRequestLock {
    public Task<IAsyncDisposable?> TryAcquireAsync(int requestId, CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(null);
}
sealed class TestLock : IAccountRequestLock, IAsyncDisposable {
    public Task<IAsyncDisposable?> TryAcquireAsync(int requestId, CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(this);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
sealed class Provisioner : ISupabaseUserProvisioningService {
    public bool IsConfigured => true;
    public int Calls { get; private set; }
    public string? Password { get; private set; }
    public Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<SupabaseUserProvisionResult> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default) { Calls++; Password = password; return Task.FromResult(SupabaseUserProvisionResult.Failed(SupabaseUserProvisionError.NetworkFailure, "test")); }
}
