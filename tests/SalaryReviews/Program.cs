using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.Core.Policies;

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var today=DateOnly.FromDateTime(DateTime.Today);
db.ChucVus.Add(new ChucVu { MaChucVu=4,TenChucVu="Lễ tân",NhomLuong="NHAN_VIEN" });
db.KhungLuongCapBacs.AddRange(new KhungLuongCapBac {MaKhungLuong=1,NhomChucVu="NHAN_VIEN",TrinhDo="Cao đẳng",ThamNienTuThang=0,ThamNienDenThang=12,LuongToiThieu=7_000_000m,LuongToiDa=9_000_000m},new KhungLuongCapBac {MaKhungLuong=2,NhomChucVu="NHAN_VIEN",TrinhDo="Cao đẳng",ThamNienTuThang=12,ThamNienDenThang=36,LuongToiThieu=8_000_000m,LuongToiDa=10_000_000m});
HopDong Add(int id,decimal salary,DateOnly joined) {
 var e=new NhanVien {MaNhanVien=id,Ho="Test",Ten=id.ToString(),EmailCongVu=$"{id}@example.invalid",DienThoai="0900000000",MaChucVu=4,TrinhDo="Cao đẳng",NgayVaoLam=joined};
 var h=new HopDong {MaHopDong=id,MaNhanVien=id,NhanVien=e,SoHopDong=$"TEST-{id}",NgayBatDau=joined,LoaiHopDong="KHONG_XAC_DINH_THOI_HAN",LuongCoBan=salary};db.HopDongs.Add(h);return h;
}
var invalid=Add(2333,0,today.AddMonths(-13));
Add(2334,-1,today.AddMonths(-13));
Add(2335,8_000_000m,today.AddYears(-1)); // threshold review due, salary still in range
Add(2336,8_000_000m,today.AddMonths(-1)); // valid, no review due
var malformed=Add(2337,8_000_000m,today.AddMonths(-13));
malformed.PhuLucLuongs.Add(new PhuLucLuong {MaPhuLuc=1,NgayHieuLuc=new DateOnly(today.Year,today.Month,2).AddYears(-1),LuongCoBanMoi=8_500_000m});
var overlap=Add(2338,8_000_000m,today.AddMonths(-13));
db.HopDongs.Add(new HopDong {MaHopDong=2339,MaNhanVien=2338,NhanVien=overlap.NhanVien,SoHopDong="OVERLAP",NgayBatDau=today.AddMonths(-13),LuongCoBan=8_000_000m,LoaiHopDong="KHONG_XAC_DINH_THOI_HAN"});
db.ChucVus.Add(new ChucVu { MaChucVu=1,TenChucVu="Tổng Giám Đốc",NhomLuong="TONG_GIAM_DOC" });
db.KhungLuongCapBacs.Add(new KhungLuongCapBac { MaKhungLuong=3,NhomChucVu="TONG_GIAM_DOC",LuongToiThieu=25_000_000m,LuongToiDa=40_000_000m });
Add(2340,30_000_000m,today.AddYears(-5)).NhanVien!.MaChucVu=1;
Add(2341,20_000_000m,today.AddYears(-5)).NhanVien!.MaChucVu=1;
db.KhungLuongCapBacs.Add(new KhungLuongCapBac {MaKhungLuong=4,NhomChucVu="NHAN_VIEN",TrinhDo="Đại học",ThamNienTuThang=12,ThamNienDenThang=36,LuongToiThieu=9_000_000m,LuongToiDa=11_000_000m});
var detailed=Add(2350,18_000_000m,today.AddYears(-2).AddMonths(-8));
detailed.NhanVien!.TrinhDo="Đại học Quản trị Nhân sự";
Add(2360,18_000_000m,today.AddMonths(-2)); // outside band alone is not a tenure reminder
Add(2361,0,today.AddMonths(-2)); // missing salary alone is not a tenure reminder
Add(2362,8_000_000m,today.AddMonths(-2)).NhanVien!.TrinhDo="Chưa cập nhật";
var reviewed=Add(2363,8_000_000m,today.AddMonths(-14));
reviewed.PhuLucLuongs.Add(new PhuLucLuong {MaPhuLuc=2,NgayHieuLuc=new DateOnly(today.Year,today.Month,1),LuongCoBanMoi=8_000_000m});
var renewed=Add(2364,18_000_000m,today.AddMonths(-14));
renewed.NgayBatDau=new DateOnly(today.Year,today.Month,1);
db.KhungLuongCapBacs.Add(new KhungLuongCapBac {MaKhungLuong=5,NhomChucVu="NHAN_VIEN",TrinhDo="Cao đẳng",ThamNienTuThang=36,LuongToiThieu=9_000_000m,LuongToiDa=11_000_000m});
Add(2365,9_000_000m,today.AddYears(-3));
await db.SaveChangesAsync();
var c=new HopDongController(db,NullLogger<HopDongController>.Instance) {ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
var view=await c.SalaryReviews() as ViewResult;
if(view?.Model is not IReadOnlyList<HopDongController.SalaryReviewItem> rows)throw new Exception("Review page must return rows");
foreach(var id in new[]{2333,2334,2335,2337,2338,2339,2365}) if(!rows.Any(x=>x.ContractId==id))throw new Exception($"Missing review row {id}");
var detailRow=rows.Single(x=>x.ContractId==2350);
if(detailRow.Minimum!=9_000_000m || detailRow.Maximum!=11_000_000m)throw new Exception("Detailed university degree at 2y8m must resolve 9-11m band");
if(detailRow.Salary!=18_000_000m || detailed.NhanVien!.TrinhDo!="Đại học Quản trị Nhân sự")throw new Exception("Do not alter recorded degree or legacy salary");
foreach(var qualification in new[]{"Đại học Quản trị Nhân sự"," đại học Du lịch ","Đại học".Normalize(System.Text.NormalizationForm.FormD)})
 if(await c.SalaryQuote(4,qualification,today,2350) is not JsonResult)throw new Exception("Quote must use the same degree matching as review");
if(await c.SalaryQuote(4,"Chứng chỉ nghiệp vụ đại học",today,2350) is not BadRequestObjectResult)throw new Exception("Do not upgrade certificates into university degrees");
if(await c.SalaryQuote(4,"Sau Đại học (Thạc sĩ / Tiến sĩ)",today,2350) is not BadRequestObjectResult)throw new Exception("Unsupported higher degree must require policy confirmation");
if(rows.Single(x=>x.ContractId==2333).Salary != null || !rows.Single(x=>x.ContractId==2333).Reason.Contains("Chưa có mức lương hợp lệ"))throw new Exception("Missing salary must be explicit, not zero fallback");
if(!rows.Single(x=>x.ContractId==2337).Reason.Contains("phụ lục"))throw new Exception("Invalid history must have reason");
if(!rows.Single(x=>x.ContractId==2338).Reason.Contains("nhiều hợp đồng"))throw new Exception("Overlap must have reason");
if(rows.Any(x=>new[]{2340,2341,2360,2361,2362,2363,2364}.Contains(x.ContractId)))throw new Exception("GM fixed band must not trigger tenure review");
if(rows.Any(x=>x.ContractId==2336))throw new Exception("Valid employee not due must stay out");
if(await db.HopDongs.Where(x=>x.MaHopDong==2333).Select(x=>x.LuongCoBan).SingleAsync()!=0)throw new Exception("Must not repair data silently");
try { EffectiveSalaryResolver.Resolve(new[]{invalid},today);throw new Exception("Payroll must remain strict"); }catch(InvalidOperationException){}
c.Request.Headers.XRequestedWith="XMLHttpRequest";
var partial=await c.SalaryReviews();
if(partial is not PartialViewResult { ViewName:"_SalaryReviewList" })throw new Exception("Modal requests must receive only the review list partial");
foreach(var person in await db.NhanViens.ToListAsync()) person.TrangThai="DA_THOI_VIEC";
await db.SaveChangesAsync();
var empty=await c.SalaryReviews();
if(empty is not PartialViewResult { Model:IReadOnlyList<HopDongController.SalaryReviewItem> emptyRows } || emptyRows.Count!=0)throw new Exception("No eligible employees must return an empty list, not an error");
Console.WriteLine("PASS tenure-only reminders exclude outside-band, missing-data, reviewed, renewed and GM records; review page survives zero/negative salary, invalid appendix and overlapping contracts; valid due rows remain visible; no data changed; payroll remains strict");
