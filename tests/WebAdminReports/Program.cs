using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Services;

var failed = 0; var passed = 0;
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + (ex.InnerException?.Message ?? ex.Message)); }
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static object Call(Type type, string method, params object?[] args) =>
    type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(null, args)!;
var assembly = typeof(BaoCaoReportService).Assembly;
await Check("Headcount total equals department breakdown when resignation date is missing", () =>
{
    var type = assembly.GetType("OvertimeHotel.HRM.WebAdmin.Services.NhanSuRow")!;
    var rows = Array.CreateInstance(type, 2);
    for (var i = 0; i < 2; i++)
    {
        var row = Activator.CreateInstance(type)!;
        foreach (var item in new Dictionary<string, object?> { ["MaNhanVien"] = i+1, ["TenPhongBan"] = "A",
                     ["NgayVaoLam"] = new DateOnly(2020,1,1), ["TrangThai"] = i == 0 ? "DANG_LAM" : "THOI_VIEC" })
            type.GetProperty(item.Key)!.SetValue(row, item.Value);
        rows.SetValue(row, i);
    }
    var report = (BienDongNhanSuReport)Call(typeof(BaoCaoReportService), "BuildBienDong", rows,
        new DateOnly(2026,10,1), new DateOnly(2026,10,31), 2026, null, 10);
    Require(report.CuoiKy == 2 && report.TheoPhongBan.Sum(p => p.QuanSo) == report.CuoiKy, "Total and departments disagree");
    return Task.CompletedTask;
});
await Check("Excel numeric cells use invariant culture", () =>
{
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        var model = new BaoCaoViewModel { Tab="quy-luong", QuyLuong=new() { ThucChiGross=1234.5m, ThucChi=1234.5m } };
        var (_, bytes) = ((string, byte[]))Call(typeof(BaoCaoController), "BuildExcelXml", model);
        XNamespace ns="urn:schemas-microsoft-com:office:spreadsheet";
        var values=XDocument.Parse(Encoding.UTF8.GetString(bytes)).Descendants(ns+"Data")
            .Where(n => (string?)n.Attribute(ns+"Type") == "Number").Select(n=>n.Value).ToArray();
        Require(values.Contains("1234.5") && values.All(n => !n.Contains(',')), "SpreadsheetML numbers depend on request culture");
    }
    finally { CultureInfo.CurrentCulture=previous; }
    return Task.CompletedTask;
});
await Check("Excel export is a genuine xlsx workbook with text cells instead of formulas", () =>
{
    var method=typeof(BaoCaoController).GetMethod("BuildExcelFile",BindingFlags.Static|BindingFlags.NonPublic);
    Require(method!=null,"Missing genuine xlsx exporter");
    var model=new BaoCaoViewModel {Tab="co-cau",CoCau=new(){TheoPhongBan=[new(){Nhan="=1+1",GiaTri=2}]}};
    var (name,bytes)=((string,byte[]))method!.Invoke(null,[model])!;
    Require(name.EndsWith(".xlsx"),"Workbook extension is not xlsx");
    using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
    Require(zip.GetEntry("[Content_Types].xml")!=null,"Missing OpenXML content types");
    using var reader=new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
    var xml=reader.ReadToEnd(); XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var sheet=XDocument.Parse(xml);
    Require(!sheet.Descendants(ns+"f").Any() && sheet.Descendants(ns+"t").Any(n=>n.Value=="=1+1"),"Text was converted to a formula");
    if (args.Contains("--export"))
    {
        Directory.CreateDirectory("/tmp/hrm-report-artifacts");
        File.WriteAllBytes("/tmp/hrm-report-artifacts/report.xlsx",bytes);
    }
    return Task.CompletedTask;
});
await Check("Attendance excludes unpublished, unfinished, leave and incomplete punches from violations", () =>
{
    var method=typeof(ReportPolicy).GetMethod("ResolveAttendanceStatus");
    Require(method is not null, "Missing shared attendance classifier");
    var now=new DateTimeOffset(2026,10,28,5,0,0,TimeSpan.Zero);
    string? Status(bool published, DateTimeOffset end, bool leave, ChamCong? punch=null, string state="DA_PHAN") =>
        (string?)method!.Invoke(null,[published,state,end,now,leave,punch]);
    Require(Status(true,now.AddHours(5),false) is null,"Future shift counted absent");
    Require(Status(false,now.AddHours(-1),false) is null,"Unpublished shift counted absent");
    Require(Status(true,now.AddHours(-1),true)=="NGHI_DA_DUYET","Approved leave counted absent");
    Require(Status(true,now.AddHours(-1),false)=="VANG_MAT","Unattended completed shift missing");
    Require(Status(true,now.AddHours(-1),false,new(){GioCheckIn=now.AddHours(-8)})=="THIEU_QUET","Incomplete punch counted on time");
    Require(Status(true,now.AddHours(-1),false,null,"DA_HUY") is null,"Cancelled shift counted absent");
    return Task.CompletedTask;
});
await Check("Approved budget has a monthly coverage policy and prevents self approval", () =>
{
    var policy=typeof(ReportPolicy).Assembly.GetType("OvertimeHotel.HRM.Core.Policies.PayrollBudgetPolicy");
    Require(policy is not null,"Approved budget workflow is missing");
    var transition=policy!.GetMethod("CanTransition")!;
    Require(!(bool)transition.Invoke(null,["CHO_DUYET","approve",7,7])!,"Creator approved their own budget");
    Require((bool)transition.Invoke(null,["CHO_DUYET","approve",7,8])!,"Independent approval rejected");
    Require(!(bool)transition.Invoke(null,["KHOA","submit",7,8])!,"Locked budget changed");
    var summary=policy.GetMethod("Summarize")!.Invoke(null,[new NganSachLuong[]{
        new(){Nam=2026,Thang=1,MaPhongBan=1,SoTienNganSach=10,TrangThai="DA_DUYET"},
        new(){Nam=2026,Thang=2,MaPhongBan=1,SoTienNganSach=20,TrangThai="DRAFT"}},
        2026,new int[]{1,2},new int[]{1}])!;
    Require((int)summary.GetType().GetProperty("MissingSlots")!.GetValue(summary)! == 1,"Draft counted as approved budget");
    return Task.CompletedTask;
});
await Check("Hourly dashboard and drill-down use the same accrual settlements once", async () =>
{
    var reader=new OfflineReader(); await using var db=reader.Open();
    using var cache=new MemoryCache(new MemoryCacheOptions());
    var service=new BaoCaoReportService(db,cache,NullLogger<BaoCaoReportService>.Instance);
    var method=typeof(BaoCaoReportService).GetMethod("BuildSuKienAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
    var scope=ReportPolicy.ResolveScope("HR",null);
    var report=await (Task<HieuQuaSuKienReport>)method.Invoke(service,[new DateOnly(2026,10,1),new DateOnly(2026,10,31),scope,null])!;
    var detail=await service.BuildChiTietAsync(scope,"HR","su-kien","THANG",2026,10,null,null);
    Require(report.TongPhuCapDaPhanBo==76923m,"Dashboard counted assignment money or duplicated segments");
    Require(detail.TongTien==report.TongPhuCapDaPhanBo && detail.DanhSach.Count==1,"Drill-down disagrees with KPI");
    foreach(var sql in reader.Commands.Where(s=>s.Contains("quyet_toan_su_kien")))
        Require(sql.Contains("thang_phat_sinh >=") && sql.Contains("thang_phat_sinh <="),"Settlement query lost accrual period filter");
});
await Check("Department event report reads each money source once and retains settlement totals", async () =>
{
    var reader=new OfflineReader(); await using var db=reader.Open();
    using var cache=new MemoryCache(new MemoryCacheOptions());
    var service=new BaoCaoReportService(db,cache,NullLogger<BaoCaoReportService>.Instance);
    var method=typeof(BaoCaoReportService).GetMethod("BuildSuKienAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
    var report=await (Task<HieuQuaSuKienReport>)method.Invoke(service,[new DateOnly(2026,10,1),new DateOnly(2026,10,31),
        ReportPolicy.ResolveScope("HR",null),1])!;
    Require(report.TongPhuCapDaPhanBo==76923m,"Reusing money rows changed the settlement total");
    Require(reader.Commands.Count(s=>s.Contains("FROM quyet_toan_su_kien"))==1,
        "Department report read hourly settlements more than once");
    Require(reader.Commands.Count(s=>s.Contains("FROM phan_cong_du_an") && s.Contains("so_tien_phu_cap <>"))==1,
        "Department report read legacy allowances more than once");
});
await Check("Attendance SQL gates completed published shifts and approved leave types", async () =>
{
    var reader=new OfflineReader(); await using var db=reader.Open();
    using var cache=new MemoryCache(new MemoryCacheOptions());
    var service=new BaoCaoReportService(db,cache,NullLogger<BaoCaoReportService>.Instance);
    var method=typeof(BaoCaoReportService).GetMethod("LoadAttendanceAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
    try { await (Task)method.Invoke(service,[new DateOnly(2026,10,1),new DateOnly(2026,10,31),null])!; }
    catch (NotSupportedException e) when (e.Message=="captured attendance query") { }
    var sql=reader.Commands.Single();
    Require(sql.Contains("da_cong_bo") && sql.Contains("ket_thuc_du_kien <=") && sql.Contains("la_nghi_phep")
        && sql.Contains("DA_DUYET"),"Attendance SQL lost eligibility or leave constraints");
});
await Check("Missing optional budget table is unavailable, not an approved zero budget", async () =>
{
    var reader=new OfflineReader(); await using var db=reader.Open();
    Require(await PayrollBudgetService.ReadAsync(db,2026) is null,"Missing table presented as valid zero budget");
});
await Check("Attendance KPI, series and departments use the same assessed population", () =>
{
    var type=assembly.GetType("OvertimeHotel.HRM.WebAdmin.Services.ChamCongRow")!;
    var states=new[]{"DUNG_GIO","DI_TRE","NGHI_DA_DUYET","THIEU_QUET"};
    var rows=Array.CreateInstance(type,states.Length);
    for(var i=0;i<states.Length;i++)
    {
        var row=Activator.CreateInstance(type)!;
        foreach(var item in new Dictionary<string,object> { ["TrangThai"]=states[i], ["PhongBan"]="A",
            ["MaNhanVien"]=i+1,["Ngay"]=new DateOnly(2026,10,1) }) type.GetProperty(item.Key)!.SetValue(row,item.Value);
        rows.SetValue(row,i);
    }
    var method=typeof(BaoCaoReportService).GetMethod("BuildAttendanceReport",BindingFlags.NonPublic|BindingFlags.Static);
    Require(method!=null,"Missing testable shared attendance aggregation");
    var report=(ChuyenCanViPhamReport)method!.Invoke(null,[rows,Array.CreateInstance(type,0),
        new DateOnly(2026,10,1),new DateOnly(2026,10,31),"Previous"])!;
    Require(report.TongBanGhi==2 && report.TyLeChuyenCan==50m && report.Series.Single().TyLeChuyenCan==50m
        && report.TheoPhongBan.Single().TyLe==50m && report.TheoPhongBan.Single().GiaTri==2,"Attendance populations disagree");
    return Task.CompletedTask;
});
await Check("Stale budget page cannot approve a resubmitted amount",async()=>
{
    var method=typeof(PayrollBudgetService).GetMethod("TransitionAsync")!;
    Require(method.GetParameters().Length==4,"Approval does not carry the displayed version");
    var reader=new OfflineReader {BudgetVersion=3}; await using var db=reader.Open();
    using var cache=new MemoryCache(new MemoryCacheOptions());
    var service=new PayrollBudgetService(db,cache);
    var rejected=false;
    try { await (Task)method.Invoke(service,[Guid.NewGuid(),"approve",8,1L])!; }
    catch(InvalidOperationException) {rejected=true;}
    Require(rejected && !reader.Commands.Any(s=>s.StartsWith("UPDATE")),"Stale approval reached a database update");
    try { await service.TransitionAsync(Guid.NewGuid(),"approve",8,3L); }
    catch(InvalidOperationException) { /* Fake UPDATE reports a concurrent conflict. */ }
    var update=reader.Commands.Single(s=>s.StartsWith("UPDATE"));
    Require(System.Text.RegularExpressions.Regex.IsMatch(update,@"WHERE[\s\S]*version") && update.Contains("version + 1"),
        "Approval mutation does not atomically compare and increment version");
});
await Check("Draft saves compare and increment the displayed budget version",async()=>
{
    var version=typeof(PayrollBudgetInput).GetProperty("Version");
    Require(version!=null,"Draft edit does not carry the displayed version");
    var reader=new OfflineReader();await using var db=reader.Open();
    using var cache=new MemoryCache(new MemoryCacheOptions());
    var service=new PayrollBudgetService(db,cache);
    var input=new PayrollBudgetInput {Id=Guid.NewGuid(),Nam=2026,Thang=10,MaPhongBan=1,SoTien=100};
    version!.SetValue(input,1L);var rejected=false;
    try {await service.SaveAsync(input,8);}catch(InvalidOperationException){rejected=true;}
    var sql=reader.Commands.Single(s=>s.StartsWith("UPDATE"));
    Require(rejected && System.Text.RegularExpressions.Regex.IsMatch(sql,@"WHERE[\s\S]*version")
        && sql.Contains("version + 1"),"Draft mutation is not guarded against stale edits");
});
await Check("Reference budgets exclude terminated contracts with unknown termination date",()=>
{
    var filter=typeof(ReportPolicy).GetProperty("ReferenceContractFilter");
    Require(filter!=null,"Reference contract eligibility filter is missing");
    var expression=(System.Linq.Expressions.Expression<Func<HopDong,bool>>)filter!.GetValue(null)!;
    var accepts=expression.Compile();
    Require(!accepts(new(){TrangThai="DA_CHAM_DUT",NgayKetThuc=null}),"Undated terminated contract inflated budget");
    Require(accepts(new(){TrangThai="HET_HAN",NgayKetThuc=new DateOnly(2026,3,31)}),"Dated historical contract excluded");
    Require(accepts(new(){TrangThai="HIEU_LUC"}),"Active indefinite contract excluded");
    return Task.CompletedTask;
});

// Builds only a Razor test host: never starts WebAdmin Program or its background workers.
var builder=WebApplication.CreateBuilder(new WebApplicationOptions {
    ContentRootPath=Path.Combine(Directory.GetCurrentDirectory(),"src/OvertimeHotel.HRM.WebAdmin"),
    ApplicationName=assembly.GetName().Name });
builder.Logging.ClearProviders();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(assembly);
await using var app=builder.Build();
async Task<string> Render(string view, object model, string controller="BaoCao")
{
    await using var scope=app.Services.CreateAsyncScope();
    var http=new DefaultHttpContext {RequestServices=scope.ServiceProvider};
    var routes=new RouteData(); routes.Values["controller"]=controller; routes.Values["action"]="Print";
    routes.Routers.Add(new ReportTestRouter());
    var context=new ActionContext(http,routes,new ActionDescriptor());
    var engine=scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
    var result=engine.GetView(null,"/Views/"+controller+"/"+view+".cshtml",false);
    Require(result.Success,"Razor view missing: "+view);
    var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};
    var temp=new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    using var writer=new StringWriter();
    await result.View!.RenderAsync(new ViewContext(context,result.View,data,temp,writer,new HtmlHelperOptions()));
    return System.Net.WebUtility.HtmlDecode(writer.ToString());
}
await Check("Budget page hides self approval and provides antiforgery protected forms",async()=>
{
    var html=await Render("Index",new PayrollBudgetViewModel {Nam=2026,Available=true,ActorId=7,
        Departments=[new("Department A","1")], Rows=[new(){MaNganSach=Guid.NewGuid(),Nam=2026,Thang=10,
            MaPhongBan=1,NguoiTao=7,TrangThai="CHO_DUYET",SoTienNganSach=100}]},"PayrollBudget");
    Require(!html.Contains("value=\"approve\""),"Budget page exposes self approval");
    Require(html.Contains("__RequestVerificationToken"),"Budget forms lack antiforgery token");
});
await Check("Budget card shows the exact cents stored for approval",async()=>
{
    var previous=CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");
        var html=await Render("Index",new PayrollBudgetViewModel {Nam=2026,Available=true,ActorId=7,
            Departments=[new("Department A","1")],Rows=[new(){MaNganSach=Guid.NewGuid(),Nam=2026,Thang=10,
                MaPhongBan=1,NguoiTao=7,TrangThai="CHO_DUYET",SoTienNganSach=1000.50m}]},"PayrollBudget");
        Require(html.Contains("1.000,50 đ"),"Approval card rounds away the stored cents");
    }
    finally {CultureInfo.CurrentCulture=previous;}
});
foreach (var (tab,marker) in new[]{("co-cau","Marker department"),("hoc-van","Marker education"),("su-kien","Marker event")})
    await Check("Print renders actual data for "+tab,async()=>
    {
        var model=new BaoCaoViewModel {Tab=tab,IsDatabaseOnline=true,
            CoCau=new(){TheoPhongBan=[new(){Nhan="Marker department",GiaTri=1}]},
            HocVanThamNien=new(){TheoTrinhDo=[new(){Nhan="Marker education",GiaTri=1}]},
            SuKien=new(){DanhSach=[new(){TenDuAn="Marker event"}]}};
        Require((await Render("Print",model)).Contains(marker),"Print contains only placeholder, not report data");
    });
await Check("Print refuses to present offline zeros as a valid report",async()=>
{
    var html=await Render("Print",new BaoCaoViewModel {Tab="bien-dong",IsDatabaseOnline=false});
    Require(html.Contains("Dữ liệu báo cáo chưa sẵn sàng"),"Offline report prints default zeros");
});
Console.WriteLine($"WebAdminReports: {passed} passed, {failed} failed. No production database was used.");
return failed==0 ? 0 : 1;

sealed class ReportTestRouter : IRouter
{
    public Task RouteAsync(RouteContext context)=>Task.CompletedTask;
    public VirtualPathData? GetVirtualPath(VirtualPathContext context)=>new(this,"/test");
}
