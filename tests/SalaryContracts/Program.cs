using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;

var options = new DbContextOptionsBuilder<AppDbContext>();
if (args.Contains("--postgres")) options.UseNpgsql("Host=127.0.0.1;Port=55439;Database=salary_ef_checks;Username=bryan", o => o.EnableRetryOnFailure());
else options.UseInMemoryDatabase(Guid.NewGuid().ToString());
await using var db = new AppDbContext(options.Options);
if (args.Contains("--postgres")) { await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync(); }
db.PhongBans.Add(new PhongBan { MaPhongBan=2, TenPhongBan="Tiền Sảnh" });
db.ChucVus.Add(new ChucVu { MaChucVu=4, MaPhongBan=2, TenChucVu="Lễ tân", NhomLuong="NHAN_VIEN" });
db.KhungLuongCapBacs.Add(new KhungLuongCapBac { MaKhungLuong=1, NhomChucVu="NHAN_VIEN", TrinhDo="Cao đẳng", ThamNienTuThang=0, ThamNienDenThang=12, LuongToiThieu=7_000_000m, LuongToiDa=9_000_000m });
var today = DateOnly.FromDateTime(DateTime.Today);
var employee = new NhanVien { MaNhanVien=1, Ho="Test", Ten="Employee", EmailCongVu="test@example.invalid", DienThoai="0900000000", TrinhDo="Cao đẳng", NgayVaoLam=today.AddMonths(-1), MaChucVu=4, MaPhongBan=2 };
db.NhanViens.Add(employee);
db.HopDongs.Add(new HopDong { MaHopDong=1, MaNhanVien=1, SoHopDong="TEST-1", NgayBatDau=today.AddMonths(-1), LuongCoBan=8_000_000m, LoaiHopDong="KHONG_XAC_DINH_THOI_HAN", MaChucVuKyKet=4, MaPhongBanKyKet=2 });
await db.SaveChangesAsync();
var c = new HopDongController(db, NullLogger<HopDongController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
c.TempData = new TempDataDictionary(c.HttpContext, new MemoryTemp());
void Require(bool value,string message) { if(!value) throw new Exception(message); }
Require(await c.SalaryQuote(4,"Cao đẳng",today,1) is JsonResult,"Quote from database");
Require(await c.SalaryQuote(4,"Unknown",today,1) is BadRequestObjectResult,"Reject unknown education");
var next = new DateOnly(today.Year,today.Month,1).AddMonths(1);
var scheduled = new PhuLucLuong { MaPhuLuc = 100, MaHopDong = 1, NgayHieuLuc = next, LuongCoBanMoi = 9_000_000m, LyDo = "Scheduled", NguoiTao = "Test" };
db.PhuLucLuongs.Add(scheduled); await db.SaveChangesAsync();
var scheduledQuote = (JsonResult)await c.SalaryQuote(4,"Cao đẳng",next.AddMonths(1),1);
var scheduledJson = System.Text.Json.JsonSerializer.SerializeToElement(scheduledQuote.Value);
Require(scheduledJson.GetProperty("currentSalary").GetDecimal() == 8_000_000m, "Current salary must use today, not a future scheduled amendment");
var currentContract = await db.HopDongs.SingleAsync(x => x.MaHopDong == 1);
currentContract.LuongCoBan = 0; await db.SaveChangesAsync();
var invalidQuote = (JsonResult)await c.SalaryQuote(4,"Cao đẳng",next.AddMonths(1),1);
Require(System.Text.Json.JsonSerializer.SerializeToElement(invalidQuote.Value).GetProperty("currentSalary").ValueKind == System.Text.Json.JsonValueKind.Null,
    "Invalid current history must not be replaced by a guessed salary");
var invalidResult = (PartialViewResult)await c.SalaryAmendments(1);
Require(((OvertimeHotel.HRM.WebAdmin.Models.HopDongSalaryAmendmentsViewModel)invalidResult.Model!).CurrentSalary == null,
    "Amendment header must not show a future amount as current when history is invalid");
currentContract.LuongCoBan = 8_000_000m; await db.SaveChangesAsync();
db.PhuLucLuongs.Remove(scheduled); await db.SaveChangesAsync();
var expiringContract = await db.HopDongs.SingleAsync(x => x.MaHopDong == 1);
expiringContract.NgayKetThuc = next.AddDays(-1); await db.SaveChangesAsync();
var expiringResult = (PartialViewResult)await c.SalaryAmendments(1);
var expiringModel = (OvertimeHotel.HRM.WebAdmin.Models.HopDongSalaryAmendmentsViewModel)expiringResult.Model!;
Require(expiringModel.DefaultEffectiveDate == next && !expiringModel.CanAdd && expiringModel.NextMonthOutsideContract, "No retroactive default when the contract ends before next month");
expiringContract.NgayKetThuc = next; await db.SaveChangesAsync();
var boundaryResult = (PartialViewResult)await c.SalaryAmendments(1);
var boundaryModel = (OvertimeHotel.HRM.WebAdmin.Models.HopDongSalaryAmendmentsViewModel)boundaryResult.Model!;
Require(boundaryModel.DefaultEffectiveDate == next && boundaryModel.CanAdd && !boundaryModel.NextMonthOutsideContract, "The last contract day is inclusive");
expiringContract.NgayKetThuc = null; await db.SaveChangesAsync();
Require(await c.AddSalaryAmendment(1,next.AddDays(1),8_500_000m,"Raise",null,null) is BadRequestObjectResult,"Reject middle month");
Require(await c.AddSalaryAmendment(1,next,10_000_000m,"Raise",null,null) is BadRequestObjectResult,"Reject out of band");
Require(await c.AddSalaryAmendment(1,next,8_500_000m,"Raise",null,null) is JsonResult,"Save valid amendment");
Require(db.PhuLucLuongs.Count()==1 && db.HopDongs.Single().LuongCoBan==8_000_000m,"Keep original salary");
Require(await c.AddSalaryAmendment(1,next,8_500_000m,"Raise again",null,null) is BadRequestObjectResult,"Reject duplicate");
db.KyLuongs.Add(new KyLuong { MaKyLuong=1,Thang=next.Month,Nam=next.Year,TuNgay=next,DenNgay=next.AddMonths(1).AddDays(-1),TrangThai="KHOA_SO" }); await db.SaveChangesAsync();
Require(await c.AddSalaryAmendment(1,next,8_500_000m,"Locked",null,null) is BadRequestObjectResult,"Protect locked period");
db.KyLuongs.RemoveRange(db.KyLuongs); await db.SaveChangesAsync();
if (args.Contains("--postgres"))
{
    await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('nhan_vien','ma_nhan_vien'), (SELECT max(ma_nhan_vien) FROM nhan_vien)); SELECT setval(pg_get_serial_sequence('hop_dong','ma_hop_dong'), (SELECT max(ma_hop_dong) FROM hop_dong));");
}
var create = new OvertimeHotel.HRM.WebAdmin.Models.HopDongFormViewModel { EmployeeName="New Test", DepartmentId=2, PositionId=4, TrinhDo="Cao đẳng", Contract=new HopDong { SoHopDong="NEW-TEST", NgayBatDau=today, HinhThucHopDong="THU_VIEC", LoaiHopDong="THU_VIEC", LuongCoBan=1m } };
await c.Create(create);
Require(c.ModelState.IsValid,"Create new trial: "+ string.Join(";",c.ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage)));
var trial = await db.HopDongs.Include(x=>x.NhanVien).SingleAsync(x=>x.SoHopDong=="NEW-TEST");
Require(trial.LuongCoBan==5_950_000m && trial.NgayKetThuc==today.AddMonths(2).AddDays(-1),"Trial calculated on server");
Require(trial.NhanVien!.TrinhDo=="Cao đẳng" && !trial.NhanVien.IsProfileComplete,"Carry education to incomplete profile");
db.ChucVus.Add(new ChucVu { MaChucVu=5, MaPhongBan=2, TenChucVu="Other role", NhomLuong="NHAN_VIEN" });
var baseContract=await db.HopDongs.SingleAsync(x=>x.MaHopDong==1);
baseContract.NgayBatDau = new DateOnly(today.Year,today.Month,1).AddMonths(-6);
employee.NgayVaoLam=baseContract.NgayBatDau;
await db.SaveChangesAsync();
var currentMonth=new DateOnly(today.Year,today.Month,1);
Require(await c.AddSalaryAmendment(1,currentMonth,8_500_000m,"Current move",2,5) is JsonResult,"Current position move");
Require(employee.MaChucVu==5,"Current role applied");
Require(await c.AddSalaryAmendment(1,currentMonth.AddMonths(-1),8_000_000m,"Older move",2,4) is JsonResult,"Historical move recorded");
Require(employee.MaChucVu==5,"Earlier move cannot overwrite later role");
Console.WriteLine("Salary contracts: all checks passed.");
sealed class MemoryTemp : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>(); public void SaveTempData(HttpContext c,IDictionary<string,object> v){} }
