using System.Collections;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Controllers.Api;
using System.Reflection;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;

var passed = 0;
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { throw new Exception($"FAIL {name}: {ex.Message}", ex); }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
void Reject(Action action)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Expected an explicit InvalidOperationException");
}
HopDong Contract(int id, decimal salary, DateOnly start, DateOnly? end = null, bool probation = false) => new()
{
    MaHopDong = id, MaNhanVien = 1, LuongCoBan = salary, NgayBatDau = start, NgayKetThuc = end,
    HinhThucHopDong = probation ? "THU_VIEC" : "CHINH_THUC", TrangThai = end.HasValue ? "HET_HAN" : "HIEU_LUC"
};
PhanCa Shift(int day, int month = 1, decimal ot = 0, bool night = false, bool worked = true)
{
    var date = new DateOnly(2026,month,day);
    return new() { MaPhanCa = day, MaNhanVien = 1, NgayLamViec = date,
        CaLamViec = new() { GioLamChuan = 8, QuaDem = night },
        ChamCong = worked ? new() { GioCheckIn = date.ToDateTime(new(8,0)), GioCheckOut = date.ToDateTime(new(16,0)), GioTangCaOt = ot } : null };
}
var configType = typeof(PayrollCalculationService).GetNestedType("PayrollConfigContext", BindingFlags.NonPublic)!;
PhieuLuong Payslip(List<HopDong> contracts, List<PhanCa> shifts, int month = 1, List<DonTu>? leaves = null,
    Action<object>? configure = null, List<QuyetToanSuKien>? settlements = null)
{
    var config = Activator.CreateInstance(configType)!;
    configure?.Invoke(config);
    var period = new KyLuong { MaKyLuong = 1, TuNgay = new(2026,month,1), DenNgay = new(2026,month,DateTime.DaysInMonth(2026,month)) };
    var employee = new NhanVien { MaNhanVien = 1, Ho = "Kiểm", Ten = "Thử", HopDongs = contracts };
    var build = typeof(PayrollCalculationService).GetMethod("BuildEmployeePayslip", BindingFlags.NonPublic | BindingFlags.Instance)!;
    object?[] inputs = [employee, EffectiveSalaryResolver.BuildSegments(contracts, period.TuNgay, period.DenNgay), period,
        shifts, leaves ?? [], settlements ?? [], config, null];
    try { return (PhieuLuong)build.Invoke(new PayrollCalculationService(null!), inputs)!; }
    catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
}
void Set(object config, string name, object value) => configType.GetProperty(name)!.SetValue(config,value);
List<HopDong> TwoContracts(decimal salary1 = 5_200_000m, decimal salary2 = 10_400_000m, int switchDay = 16, bool probation = false) =>
    [Contract(1,salary1,new(2026,1,1),new(2026,1,switchDay-1),probation), Contract(2,salary2,new(2026,1,switchDay))];
decimal Item(PhieuLuong p, string name) => p.ChiTietPhieuLuongs.Single(d => d.TenKhoanLuu.StartsWith(name)).SoTien;

Check("Sequential wages use the rate on each work date", () => Equal(600_000m, Payslip(TwoContracts(),[Shift(10),Shift(20)]).TienLuongCong));
Check("Normal OT uses both daily rates", () => Equal(112_500m, Payslip(TwoContracts(),[Shift(10,ot:1),Shift(20,ot:1)]).TienTangCa));
Check("Sunday OT uses both daily rates", () => Equal(150_000m, Payslip(TwoContracts(),[Shift(4,ot:1),Shift(18,ot:1)]).TienTangCa));
Check("Night allowance uses both daily rates", () => Equal(180_000m, Item(Payslip(TwoContracts(),[Shift(10,night:true),Shift(20,night:true)]),"Phụ cấp ca đêm")));
Check("Holiday allowance uses historical contract rate", () => Equal(400_000m, Item(Payslip(TwoContracts(),[Shift(1)]),"Phụ cấp đi làm ngày Lễ")));
Check("Holiday OT uses historical contract rate", () => Equal(75_000m, Payslip(TwoContracts(),[Shift(1,ot:1)]).TienTangCa));
Check("Paid leave uses its scheduled date's rate", () =>
{
    var leave = new DonTu { MaDon = 1, MaNhanVien = 1, TrangThai = "DA_DUYET", TuNgay = new(2026,1,10), DenNgay = new(2026,1,20), LoaiDon = new() { CoHuongLuong = true } };
    var p = Payslip(TwoContracts(),[Shift(10,worked:false),Shift(20,worked:false)],leaves:[leave]);
    Equal(600_000m,p.TienLuongCong); Equal(0m,p.TienTangCa);
    Equal(false,p.ChiTietPhieuLuongs.Any(d=>d.MaPhuCap==1));
});
Check("Monthly bonuses are assessed once after combining segments", () =>
{
    var p = Payslip(TwoContracts(switchDay:14),Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    Equal(7_800_000m,p.TienLuongCong); Equal(1_500_000m,p.TongThuong);
    Equal(1,p.ChiTietPhieuLuongs.Count(d=>d.MaKhoan==4)); Equal(1,p.ChiTietPhieuLuongs.Count(d=>d.MaKhoan==5));
});
Check("Insurance assessed once using final formal effective rate and caps", () =>
{
    var p = Payslip(TwoContracts(52_000_000m,78_000_000m,14),Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    var bh = p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1); Equal(5_226_000m,bh.SoTien);
    Equal(46_800_000m,bh.ChiTietTruBaoHiems.Single().LuongDongBaoHiem);
});
Check("Personal deduction and progressive tax assessed once on combined gross", () =>
{
    var p = Payslip(TwoContracts(52_000_000m,78_000_000m,14),Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    var tax = p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==4).ChiTietTruThueTncns.Single();
    Equal(SalaryConstants.PERSONAL_TAX_DEDUCTION,tax.GiamTruBanThan);
    var insurance = p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1).SoTien;
    Equal(Math.Max(0m,tax.ThuNhapChiuThue-insurance-SalaryConstants.PERSONAL_TAX_DEDUCTION),tax.ThuNhapTinhThue);
    Equal(PersonalIncomeTaxCalculator.CalculateTax(tax.ThuNhapTinhThue),tax.TienThue);
    Equal(p.TienLuongCong+p.TienTangCa+p.TongPhuCap+p.TongThuong-p.TongKhauTru,p.ThucNhan);
});
Check("Stored probation salary is never multiplied by 85 percent again", () =>
{
    var p = Payslip([Contract(1,5_100_000m,new(2026,1,1),probation:true)],Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    Equal(5_100_000m,p.TienLuongCong); Equal(0m,p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1).SoTien);
});
Check("Probation days do not satisfy formal insurance threshold", () =>
{
    var p = Payslip(TwoContracts(switchDay:14,probation:true),Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    Equal(0m,p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1).SoTien);
});
Check("Fourteen formal days trigger one monthly contribution", () =>
{
    var p = Payslip(TwoContracts(switchDay:13,probation:true),Enumerable.Range(1,26).Select(d=>Shift(d)).ToList());
    Equal(1_092_000m,p.ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1).SoTien);
});
Check("Role and department allowances follow contract snapshots", () =>
{
    var contracts = TwoContracts(switchDay:14); contracts[0].MaChucVuKyKet=1; contracts[1].MaChucVuKyKet=2;
    contracts[0].MaPhongBanKyKet=1; contracts[1].MaPhongBanKyKet=2;
    var p=Payslip(contracts,Enumerable.Range(1,26).Select(d=>Shift(d)).ToList(),configure:c=>
    { Set(c,"PositionNames",new Dictionary<int,string>{{1,"Trưởng bộ phận"},{2,"Nhân viên"}}); Set(c,"DepartmentNames",new Dictionary<int,string>{{1,"Buồng phòng"},{2,"Văn phòng"}}); });
    Equal(500_000m,Item(p,"Phụ cấp trách nhiệm")); Equal(260_000m,Item(p,"Phụ cấp độc hại"));
});
Check("A work date in a contract gap fails explicitly", () =>
{ var contracts=TwoContracts();contracts[0].NgayKetThuc=new(2026,1,10);Reject(()=>Payslip(contracts,[Shift(12)])); });
Check("Inclusive contract-end overlap fails instead of choosing latest", () =>
{ var contracts=TwoContracts();contracts[1].NgayBatDau=new(2026,1,15);Reject(()=>EffectiveSalaryResolver.BuildSegments(contracts,new(2026,1,1),new(2026,1,31))); });
Check("Uncovered date returns no salary", () => Equal<EffectiveSalary?>(null,EffectiveSalaryResolver.Resolve([Contract(1,8_000_000m,new(2026,2,1))],new(2026,1,31))));
Check("A historical expired contract still prices historical work", () => Equal(8_000_000m,EffectiveSalaryResolver.Resolve([Contract(1,8_000_000m,new(2025,1,1),new(2025,12,31))],new(2025,5,1))!.MonthlySalary));
Check("Undated termination is not treated as indefinite coverage", () =>
{var c=Contract(1,8_000_000m,new(2025,1,1));c.TrangThai="DA_CHAM_DUT";Equal<EffectiveSalary?>(null,EffectiveSalaryResolver.Resolve([c],new(2026,1,1)));});
var amended=Contract(1,8_000_000m,new(2026,1,1));
amended.PhuLucLuongs=[new(){MaPhuLuc=1,MaHopDong=1,NgayHieuLuc=new(2026,2,1),LuongCoBanMoi=10_000_000m,MaChucVuMoi=2,MaPhongBanMoi=3},new(){MaPhuLuc=2,MaHopDong=1,NgayHieuLuc=new(2026,3,1),LuongCoBanMoi=12_000_000m}];
Check("Before amendment keeps original salary",()=>Equal(8_000_000m,EffectiveSalaryResolver.Resolve([amended],new(2026,1,31))!.MonthlySalary));
Check("Amendment day uses replacement salary",()=>Equal(10_000_000m,EffectiveSalaryResolver.Resolve([amended],new(2026,2,1))!.MonthlySalary));
Check("Latest applicable amendment wins",()=>Equal(12_000_000m,EffectiveSalaryResolver.Resolve([amended],new(2026,3,1))!.MonthlySalary));
Check("Salary-only amendment retains earlier role and department move",()=>
{var effective=EffectiveSalaryResolver.Resolve([amended],new(2026,3,1))!;Equal<int?>(2,effective.PositionId);Equal<int?>(3,effective.DepartmentId);});
Check("January and February payroll use respective effective base",()=>
{Equal(Math.Round(8_000_000m/26m,MidpointRounding.AwayFromZero),Payslip([amended],[Shift(20)]).TienLuongCong);Equal(Math.Round(10_000_000m/26m,MidpointRounding.AwayFromZero),Payslip([amended],[Shift(10,2)],2).TienLuongCong);});
Check("Snapshots retain all segment salary and contract bases",()=>
{var note=Payslip(TwoContracts(),[Shift(10),Shift(20)]).ChiTietPhieuLuongs.Single(d=>d.MaKhoanTru==1).GhiChu!;Equal(true,note.Contains("HĐ #1")&&note.Contains("HĐ #2")&&note.Contains("01/01/2026")&&note.Contains("16/01/2026"));});
Check("Same-date duplicate amendments fail clearly",()=>
{var c=Contract(1,8_000_000m,new(2026,1,1));c.PhuLucLuongs=[new(){NgayHieuLuc=new(2026,2,1),LuongCoBanMoi=10_000_000m},new(){NgayHieuLuc=new(2026,2,1),LuongCoBanMoi=12_000_000m}];Reject(()=>EffectiveSalaryResolver.Resolve([c],new(2026,2,1)));});
Check("Non-first-day amendment fails clearly",()=>
{var c=Contract(1,8_000_000m,new(2026,1,1));c.PhuLucLuongs=[new(){NgayHieuLuc=new(2026,2,15),LuongCoBanMoi=10_000_000m}];Reject(()=>EffectiveSalaryResolver.Resolve([c],new(2026,2,16)));});
Check("Hourly event spans midnight and appendix effective date",()=>
{var start=new DateTimeOffset(2026,1,31,23,30,0,TimeSpan.FromHours(7));var slices=HourlyEventRules.Price(start,start.AddHours(1),[amended]);Equal(2,slices.Count);Equal(8_000_000m,slices[0].MonthlySalary);Equal(10_000_000m,slices[1].MonthlySalary);Equal(86_538m,HourlyEventRules.Amount(slices));});
Check("Calendar reference prorates both transition segments",()=>Equal(7_883_870.97m,EffectiveSalaryResolver.BuildSegments(TwoContracts(),new(2026,1,1),new(2026,1,31)).Sum(s=>s.CalendarProratedAmount)));
Check("Full-month salary reference is preserved",()=>Equal(8_000_000m,EffectiveSalaryResolver.BuildSegments([amended],new(2026,1,1),new(2026,1,31)).Sum(s=>s.CalendarProratedAmount)));
Check("Multi-month reference respects amendment boundary",()=>Equal(18_000_000m,EffectiveSalaryResolver.BuildSegments([amended],new(2026,1,1),new(2026,2,28)).Sum(s=>s.CalendarProratedAmount)));
Check("Locked approved pending and paid periods cannot recalculate",()=>
{
    var guard=typeof(PayrollCalculationService).GetMethod("EnsureWritablePeriod",BindingFlags.NonPublic|BindingFlags.Static)!;
    foreach(var period in new[]{new KyLuong{TrangThai="KHOA_SO"},new KyLuong{NgayDuyet=DateTimeOffset.UtcNow},new KyLuong{NgayGuiDuyet=DateTimeOffset.UtcNow},new KyLuong{TrangThai="DA_THANH_TOAN"}})
        Reject(()=>{try{guard.Invoke(null,[period]);}catch(TargetInvocationException ex)when(ex.InnerException!=null){throw ex.InnerException;}});
    guard.Invoke(null,[new KyLuong{TrangThai="MO"}]);
});
// In-memory integration: prove callers load the navigation and use effective salary rather than base.
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var fixtures=TwoContracts();fixtures[0].MaPhongBanKyKet=1;fixtures[1].MaPhongBanKyKet=2;
fixtures[1].PhuLucLuongs=[new(){MaPhuLuc=1,MaHopDong=2,NgayHieuLuc=new(2026,2,1),LuongCoBanMoi=15_600_000m}];
db.PhongBans.AddRange(new PhongBan{MaPhongBan=1,TenPhongBan="Bộ phận cũ"},new PhongBan{MaPhongBan=2,TenPhongBan="Bộ phận mới"});
db.NhanViens.Add(new NhanVien{MaNhanVien=1,Ho="Fixture",Ten="Payroll",MaPhongBan=2,NgayVaoLam=new(2025,1,1),HopDongs=fixtures});
db.TaiKhoans.Add(new TaiKhoan{MaTaiKhoan=1,MaNhanVien=1,TrangThai=true});
await db.SaveChangesAsync();db.ChangeTracker.Clear();
using var cache=new MemoryCache(new MemoryCacheOptions());
var reports=new BaoCaoReportService(db,cache,NullLogger<BaoCaoReportService>.Instance);
var load=typeof(BaoCaoReportService).GetMethod("LoadEmployeesAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
async Task<decimal> ReportSalary(DateOnly date)
{
    var task=(Task)load.Invoke(reports,[null,date])!;await task;
    var rows=(IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
    var row=rows.Cast<object>().Single();return (decimal)row.GetType().GetProperty("LuongCoBan")!.GetValue(row)!;
}
var januarySalary=await ReportSalary(new(2026,1,10));
Check("Personnel report loads historical contract salary",()=>Equal(5_200_000m,januarySalary));
var februarySalary=await ReportSalary(new(2026,2,10));
Check("Personnel report includes and resolves salary appendix",()=>Equal(15_600_000m,februarySalary));
var fundBuilder=typeof(BaoCaoReportService).GetMethod("BuildQuyLuongAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
var januaryFund=await (Task<QuyLuongNganSachReport>)fundBuilder.Invoke(reports,[new DateOnly(2026,1,1),new DateOnly(2026,1,31),2026,null,1,null])!;
Check("Fund report uses effective calendar-prorated reference",()=>Equal(7_883_870.97m,januaryFund.NganSachThamChieu));
Check("Fund report allocates historical department reference",()=>
{Equal(2_516_129.03m,januaryFund.TheoPhongBan.Single(d=>d.PhongBan=="Bộ phận cũ").NganSach);Equal(5_367_741.94m,januaryFund.TheoPhongBan.Single(d=>d.PhongBan=="Bộ phận mới").NganSach);});
var oldDepartmentFund=await (Task<QuyLuongNganSachReport>)fundBuilder.Invoke(reports,[new DateOnly(2026,1,1),new DateOnly(2026,1,31),2026,null,1,1])!;
Check("Department fund scope uses historical contract snapshot",()=>Equal(2_516_129.03m,oldDepartmentFund.NganSachThamChieu));
var ai=new AiDataController(null!,null!,reports,null!,db,NullLogger<AiDataController>.Instance)
{ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1"),new Claim(ClaimTypes.Role,"HR")],"fixture"));
var lookup=typeof(AiDataController).GetMethod("BuildContractAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
var aiResult=await (Task<IActionResult>)lookup.Invoke(ai,[principal,new AiDataRequest(null,null,null,null,null,null)])!;
Check("AI contract lookup distinguishes original from effective salary",()=>
{
    var digest=(AiDataDigestDto)((OkObjectResult)aiResult).Value!;
    var active=digest.Contract!.DanhSach.Single(c=>c.MaHopDong==2);
    Equal(10_400_000m,active.LuongCoBan);Equal<decimal?>(15_600_000m,active.LuongHieuLuc);
    Equal(DateOnly.FromDateTime(DateTime.Today),active.NgayThamChieuLuong);
    Equal<decimal?>(null,digest.Contract!.DanhSach.Single(c=>c.MaHopDong==1).LuongHieuLuc);
});
Console.WriteLine($"{passed}/{passed} checks passed. Pure local/in-memory data; no external database operations.");
