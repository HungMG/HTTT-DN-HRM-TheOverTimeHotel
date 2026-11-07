using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;

foreach (var (position, department, shouldLock) in new[] { ("Quản lý", "Lễ tân", true), ("Tổng Giám đốc", "Ban Giám Đốc", false) })
{
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    db.AddRange(new PhongBan { MaPhongBan = 1, TenPhongBan = department }, new ChucVu { MaChucVu = 1, MaPhongBan = 1, TenChucVu = position }, new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new NhanVien { MaNhanVien = 1, Ho = "Nguyen", Ten = "An", TrangThai = "DANG_LAM" }, new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", MatKhauBam = "hash", TrangThai = true });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "HR") }, "test")) };
    var controller = new HopDongController(db, NullLogger<HopDongController>.Instance) { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
    await controller.Create(new HopDongFormViewModel { EmployeeId = 1, DepartmentId = 1, PositionId = 1, Contract = new HopDong { SoHopDong = "HD-01", HinhThucHopDong = "CHINH_THUC", LoaiHopDong = "KHONG_XAC_DINH_THOI_HAN", NgayBatDau = DateOnly.FromDateTime(DateTime.Today), LuongCoBan = 10000000, TrangThai = "HIEU_LUC" } });
    if (!await db.HopDongs.AnyAsync()) throw new Exception($"{position}: contract not saved: {string.Join(", ", controller.ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage))}");
    if (!(await db.TaiKhoans.FindAsync(1))!.TrangThai) throw new Exception($"{position}: account must remain active");
    if (controller.TempData.ContainsKey("PromotionPrompt_EmployeeId") != shouldLock) throw new Exception($"{position}: incorrect promotion prompt");
    Console.WriteLine($"PASS {position}");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    db.AddRange(new PhongBan { MaPhongBan = 1, TenPhongBan = "Lễ tân" }, new ChucVu { MaChucVu = 1, MaPhongBan = 1, TenChucVu = "Quản lý" },
        new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new NhanVien { MaNhanVien = 1, Ho = "Nguyen", Ten = "An", TrangThai = "DANG_LAM" },
        new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", TrangThai = true },
        new HopDong { MaHopDong = 1, MaNhanVien = 1, SoHopDong = "OLD", HinhThucHopDong = "CHINH_THUC", LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM", NgayBatDau = today, NgayKetThuc = today.AddYears(1), LuongCoBan = 8000000, TrangThai = "HIEU_LUC" });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext();
    var controller = new HopDongController(db, NullLogger<HopDongController>.Instance) { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
    await controller.Create(new HopDongFormViewModel { EmployeeId = 1, DepartmentId = 1, PositionId = 1, ClosePreviousContract = true,
        Contract = new HopDong { SoHopDong = "NEW", HinhThucHopDong = "CHINH_THUC", LoaiHopDong = "KHONG_XAC_DINH_THOI_HAN", NgayBatDau = today, LuongCoBan = 12000000, TrangThai = "HIEU_LUC" } });
    var contract = await db.HopDongs.FindAsync(1);
    if (await db.HopDongs.CountAsync() != 1 || contract!.SoHopDong != "OLD" || contract.LuongCoBan != 12000000 || contract.NgayKetThuc != null) throw new Exception("Same-day promotion must update the existing contract, preserving its identity");
    if (!(await db.TaiKhoans.FindAsync(1))!.TrangThai || !controller.TempData.ContainsKey("PromotionPrompt_EmployeeId")) throw new Exception("Same-day promotion must keep the account active and offer request submission");
    if (!await db.NhatKyQuanTris.AnyAsync(x => x.HanhDong == "UPDATE_CONTRACT_PROMOTION")) throw new Exception("Same-day contract changes must be audited");
    Console.WriteLine("PASS same-day promotion updates existing contract, preserves active account and records audit");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    db.AddRange(new PhongBan { MaPhongBan = 1, TenPhongBan = "Lễ tân" }, new ChucVu { MaChucVu = 1, MaPhongBan = 1, TenChucVu = "Quản lý" },
        new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new NhanVien { MaNhanVien = 1, Ho = "Nguyen", Ten = "An", TrangThai = "DANG_LAM" },
        new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", TrangThai = true },
        new HopDong { MaHopDong = 1, MaNhanVien = 1, SoHopDong = "OLD", HinhThucHopDong = "CHINH_THUC", LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM", NgayBatDau = today, NgayKetThuc = today.AddYears(1), TrangThai = "HIEU_LUC" });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext();
    var controller = new HopDongController(db, NullLogger<HopDongController>.Instance) { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
    await controller.Create(new HopDongFormViewModel { EmployeeId = 1, DepartmentId = 1, PositionId = 1, ClosePreviousContract = true,
        Contract = new HopDong { SoHopDong = "NEW", HinhThucHopDong = "CHINH_THUC", LoaiHopDong = "KHONG_XAC_DINH_THOI_HAN", NgayBatDau = today.AddDays(1), LuongCoBan = 12000000, TrangThai = "HIEU_LUC" } });
    var old = await db.HopDongs.FindAsync(1);
    if (await db.HopDongs.CountAsync() != 2 || old!.TrangThai != "DA_CHAM_DUT" || old.NgayKetThuc != today || !(await db.TaiKhoans.FindAsync(1))!.TrangThai)
        throw new Exception("Later promotion must create a separate contract and close the old one");
    Console.WriteLine("PASS later promotion retains separate contracts and keeps account active");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    db.AddRange(new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new VaiTro { MaVaiTro = 2, TenVaiTro = "Manager" },
        new ChucVu { MaChucVu = 1, TenChucVu = "Quản lý" },
        new NhanVien { MaNhanVien = 1, Ho = "Test", Ten = "An", TrangThai = "DANG_LAM", EmailCongVu = "an@example.invalid", MaChucVu = 1 },
        new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", TrangThai = true });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "HR"), new Claim("EmployeeId", "2") }, "test")) };
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, new Provisioner()) { ControllerContext = new ControllerContext { HttpContext = http } };
    var result = await controller.QuickSubmitPromotion(1, 999);
    if (result is not JsonResult json || json.Value?.GetType().GetProperty("message")?.GetValue(json.Value)?.ToString() != "Hợp đồng không hợp lệ.") throw new Exception("Active accounts must be eligible for promotion requests");
    var form = await controller.CreatePromotion(1) as ViewResult;
    if (form?.Model is not CreatePromotionRequestViewModel viewModel || !viewModel.EligibleEmployees.Any(x => x.Value == "1")) throw new Exception("Active account missing from promotion form");
    if (!(await db.TaiKhoans.FindAsync(1))!.TrangThai) throw new Exception("Checking promotion eligibility changed account status");
    Console.WriteLine("PASS promotion request accepts active accounts");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    db.AddRange(new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new VaiTro { MaVaiTro = 2, TenVaiTro = "Manager" }, new ChucVu { MaChucVu = 1, TenChucVu = "Tổng Giám đốc" }, new NhanVien { MaNhanVien = 1, Ho = "An", Ten = "Test", TrangThai = "DANG_LAM", MaChucVu = 1 }, new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", TrangThai = true }, new HopDong { MaHopDong = 1, MaNhanVien = 1, TrangThai = "HIEU_LUC", NgayBatDau = DateOnly.FromDateTime(DateTime.Today) });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "HR"), new Claim("EmployeeId", "2") }, "test")) };
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, new Provisioner()) { ControllerContext = new ControllerContext { HttpContext = http } };
    await controller.QuickSubmitPromotion(1, 1);
    if (await db.YeuCauTaiKhoans.AnyAsync()) throw new Exception("Director must not be submitted as manager promotion");
    Console.WriteLine("PASS reject director promotion request");
}
await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    db.AddRange(new VaiTro { MaVaiTro = 1, TenVaiTro = "Employee" }, new VaiTro { MaVaiTro = 2, TenVaiTro = "Manager" },
        new ChucVu { MaChucVu = 1, TenChucVu = "Quản lý" },
        new NhanVien { MaNhanVien = 1, Ho = "An", Ten = "Test", TrangThai = "DANG_LAM", MaChucVu = 1 },
        new TaiKhoan { MaTaiKhoan = 1, MaNhanVien = 1, MaVaiTro = 1, TenDangNhap = "an", TrangThai = false },
        new HopDong { MaHopDong = 1, MaNhanVien = 1, TrangThai = "HIEU_LUC", NgayBatDau = today },
        new YeuCauTaiKhoan { MaYeuCau = 1, MaNhanVien = 1, MaHopDong = 1, MaVaiTro = 2, MaNguoiGui = 1, TrangThai = "CHO_IT", GhiChuTuChoi = "[THANG_CHUC]" });
    await db.SaveChangesAsync();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.NameIdentifier, "9") }, "test")) };
    http.Request.Headers.XRequestedWith = "XMLHttpRequest";
    var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, new Provisioner(), new FakeLock()) { ControllerContext = new ControllerContext { HttpContext = http } };
    var result = await controller.Complete(1);
    if (result is not ObjectResult { StatusCode: 403 }) throw new Exception("Admin must not approve manager promotion");
    if ((await db.TaiKhoans.FindAsync(1))!.TrangThai) throw new Exception("Rejected approval unlocked account");
    Console.WriteLine("PASS only IT can approve, account remains locked");
}
sealed class MemoryTempData : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext c) => new Dictionary<string,object>(); public void SaveTempData(HttpContext c, IDictionary<string,object> v) {} }

sealed class Provisioner : OvertimeHotel.HRM.Data.Services.ISupabaseUserProvisioningService { public bool IsConfigured => false; public Task<bool> DeleteUserAsync(string id, CancellationToken c = default) => Task.FromResult(true); public Task<OvertimeHotel.HRM.Data.Services.SupabaseUserProvisionResult> CreateUserAsync(string email, string password, CancellationToken c = default) => throw new NotImplementedException(); }
sealed class FakeLock : OvertimeHotel.HRM.WebAdmin.Services.IAccountRequestLock, IAsyncDisposable { public Task<IAsyncDisposable?> TryAcquireAsync(int id, CancellationToken c) => Task.FromResult<IAsyncDisposable?>(this); public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
