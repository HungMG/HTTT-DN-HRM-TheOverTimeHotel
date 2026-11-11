using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;

// Isolated local test database only, never uses application connection settings.
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Port=55439;Database=salary_gm_regression;Username=bryan", o=>o.EnableRetryOnFailure()).Options);
await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
for(var i=1;i<=9;i++) db.PhongBans.Add(new PhongBan { MaPhongBan=i,TenPhongBan=i==1?"Ban Giám Đốc":"Department "+i });
var names = new (int Id,string Name)[] { (1,"Tổng Giám Đốc (GM)"),(2,"Trưởng Bộ Phận (Department Head)"),(4,"Nhân viên Lễ tân (Receptionist)"),(5,"Nhân viên Buồng phòng (Housekeeper)"),(6,"Bếp Trưởng (Head Chef)"),(7,"Nhân viên Phục vụ (F&B Staff)"),(8,"Kỹ sư Tòa nhà (Technician)"),(9,"Nhân viên An ninh (Security Guard)"),(10,"Chuyên viên Nhân sự & Tiền lương (HR & Payroll)"),(11,"Quản trị hệ thống CNTT (IT Support)"),(12,"Trưởng bộ phận CNTT (IT Manager)"),(13,"Nhân viên Bếp (Kitchen Staff)") };
foreach(var (id,name) in names) db.ChucVus.Add(new ChucVu {MaChucVu=id,TenChucVu=name,MaPhongBan=id==1?1:null});
db.NhanViens.Add(new NhanVien {MaNhanVien=1,Ho="Existing",Ten="Director",DienThoai="0900000000",EmailCongVu="existing@example.invalid",TrinhDo="Đại học",MaChucVu=1,MaPhongBan=1,NgayVaoLam=new(2022,1,1)});
db.HopDongs.Add(new HopDong {MaHopDong=1,MaNhanVien=1,SoHopDong="EXISTING",LoaiHopDong="KHONG_XAC_DINH_THOI_HAN",NgayBatDau=new(2022,1,1),LuongCoBan=30_000_000m});
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await db.Database.ExecuteSqlRawAsync("DROP TABLE phu_luc_luong, khung_luong_cap_bac; ALTER TABLE hop_dong DROP COLUMN ma_chuc_vu_ky_ket, DROP COLUMN ma_phong_ban_ky_ket, DROP COLUMN trinh_do_ky_ket; ALTER TABLE chuc_vu DROP COLUMN nhom_luong; SELECT setval(pg_get_serial_sequence('nhan_vien','ma_nhan_vien'),1); SELECT setval(pg_get_serial_sequence('hop_dong','ma_hop_dong'),1);");
var migration=File.ReadAllText("src/OvertimeHotel.HRM.Data/migrations/20261111_salary_bands.sql");
await db.Database.ExecuteSqlRawAsync(migration);
// The disposable test cluster uses C locale; avoid relying on Vietnamese upper()
// conversion in the unrelated department selector so this reaches the seat guard.
await db.Database.ExecuteSqlRawAsync("UPDATE chuc_vu SET ten_chuc_vu='TỔNG GIÁM ĐỐC (GM)' WHERE ma_chuc_vu=1");
var http = new DefaultHttpContext { RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider() };
var controller = new HopDongController(db,LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HopDongController>()) {ControllerContext=new ControllerContext {HttpContext=http}};
controller.TempData=new TempDataDictionary(http,new MemoryTemp());
controller.Url = new TestUrlHelper();
var start=DateOnly.FromDateTime(DateTime.Today);
var model=new HopDongFormViewModel {EmployeeName="Another Director",DepartmentId=1,PositionId=1,TrinhDo="Đại học",Contract=new HopDong {SoHopDong="DUPLICATE-GM",NgayBatDau=start,LoaiHopDong="KHONG_XAC_DINH_THOI_HAN",LuongCoBan=30_000_000m}};
try
{
 var result=await controller.Create(model);
 if(result is not RedirectToActionResult || controller.TempData["Error"] is not string message || !message.Contains("đã có Tổng Giám Đốc")) throw new Exception("Expected a readable occupied-seat message; got: " + controller.TempData["Error"]);
 db.ChangeTracker.Clear();
 if(await db.NhanViens.CountAsync()!=1 || await db.HopDongs.CountAsync()!=1) throw new Exception("Rejected appointment left partial records");
 var index=await controller.Index(null,null);
 if(index is not ViewResult { Model: HopDongIndexViewModel page } || page.Contracts.Count!=1 || controller.TempData["Error"] is not string kept || !kept.Contains("đã có Tổng Giám Đốc")) throw new Exception("Index failed after rejected transaction");
 Console.WriteLine("PASS duplicate GM creation returns readable rejection and saves no records");
 Console.WriteLine("PASS contract list loads normally after rejection and preserves the business error");
}
catch(Exception e) { Console.WriteLine("FAIL "+e.GetType().Name+": "+e.Message); Environment.ExitCode=1; }
sealed class MemoryTemp : ITempDataProvider {public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>(); public void SaveTempData(HttpContext c,IDictionary<string,object> values){} }

sealed class TestUrlHelper : IUrlHelper
{
 public ActionContext ActionContext { get; } = new();
 public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext c)=>"/HopDong";
 public string? Content(string? c)=>c;
 public bool IsLocalUrl(string? url)=>true;
 public string? Link(string? routeName,object? values)=>"/HopDong";
 public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext c)=>"/HopDong";
}
