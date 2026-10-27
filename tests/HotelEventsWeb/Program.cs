using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Services;

var root = Directory.GetCurrentDirectory();
var webRoot = Path.Combine(root, "src/OvertimeHotel.HRM.WebAdmin");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = webRoot, ApplicationName = typeof(SuKienController).Assembly.GetName().Name
});
builder.Logging.ClearProviders();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(SuKienController).Assembly);
await using var app = builder.Build();
var failures = 0;
var passed = 0;
async Task Check(string name, Func<Task> run)
{
    try { await run(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static DuAn Event() => new()
{
    MaDuAn = 17, TenDuAn = "Phase 1 web regression", LoaiDuAn = "HOI_NGHI_MICE",
    TrangThai = "LEN_KE_HOACH", MaNguoiQuanLy = 1,
    NgayBatDau = new DateTimeOffset(2028, 5, 10, 2, 0, 0, TimeSpan.Zero),
    NgayKetThuc = new DateTimeOffset(2028, 5, 10, 10, 0, 0, TimeSpan.Zero),
    PhanCongs = [new() { MaPhanCong = 1, MaNhanVien = 10, TrangThaiThamGia = "DA_PHAN", VaiTroDuAn = "Phụ trách âm thanh và micro" },
        new() { MaPhanCong = 2, MaNhanVien = 10, TrangThaiThamGia = "DA_KET_THUC" },
        new() { MaPhanCong = 3, MaNhanVien = 11, TrangThaiThamGia = "HUY_PHAN_CONG" }],
    NhuCaus = [new() { MaNhuCau = 21, MaPhongBan = "KY_THUAT", VaiTro = "Phụ trách âm thanh và micro",
        SoNguoi = 2, LaMau = true }]
};
static (SuKienController Controller, StaffServiceStub Stub) Controller()
{
    var service = DispatchProxy.Create<IHotelEventService, StaffServiceStub>();
    var stub = (StaffServiceStub)(object)service;
    stub.Item = Event();
    return (new SuKienController(null!, service, NullLogger<SuKienController>.Instance), stub);
}
static Task<EventDetailsViewModel?> Load(SuKienController controller)
{
    var method = typeof(SuKienController).GetMethod("DetailsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    object?[] values = [new EventActor(1, 1, "Manager", "Probe"), 17, null, null, null, null, "staff"];
    if (method.GetParameters().Length == 8) values = [..values, false];
    return (Task<EventDetailsViewModel?>)method.Invoke(controller, values)!;
}
async Task<string> Render(string view, object model, ModelStateDictionary? state = null, string controller = "SuKien")
{
    await using var scope = app.Services.CreateAsyncScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.Request.Scheme = "http";
    http.Request.Host = new HostString("phase1.test");
    var routes = new RouteData();
    routes.Values["controller"] = controller;
    routes.Values["action"] = "Details";
    routes.Routers.Add(new TestRouter());
    var context = new ActionContext(http, routes, new ActionDescriptor(), state ?? new());
    var engine = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
    var result = engine.GetView(null, "/Views/" + controller + "/" + view + ".cshtml", false);
    Require(result.Success, "Razor view missing: " + string.Join(", ", result.SearchedLocations ?? []));
    var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState) { Model = model };
    var tempData = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    using var writer = new StringWriter();
    await result.View!.RenderAsync(new ViewContext(context, result.View, data, tempData, writer, new HtmlHelperOptions()));
    return writer.ToString();
}

await Check("Read-only staff remains visible when employee-picker data is unavailable", async () =>
{
    var (controller, stub) = Controller();
    stub.FailPicker = true;
    var model = (await Load(controller))!;
    Require(!model.StaffLoadFailed && model.Event.PhanCongs.Count == 3, "Picker failure hid loaded assignments.");
});
await Check("Assignment query failure returns event information with an explicit staff failure", async () =>
{
    var (controller, stub) = Controller();
    stub.FailStaff = true;
    var model = (await Load(controller))!;
    Require(model.StaffLoadFailed && model.Event.MaDuAn == 17, "Missing staff-failure model.");
    Require(!model.CanManage && !model.CanEdit && !model.CanAccept, "Incomplete staff data must not enable mutations.");
    var html = System.Net.WebUtility.HtmlDecode(await Render("_EventDetailsContent", model));
    Require(html.Contains("Không tải được danh sách nhân sự") && !html.Contains("Chưa sắp xếp sự kiện"), "Staff failure was rendered as an empty assignment list.");
});
await Check("Out-of-scope event returns no details", async () =>
{
    var (controller, stub) = Controller();
    stub.OutOfScope = true;
    Require(await Load(controller) is null, "Out-of-scope event was exposed.");
});
await Check("A permission change during staff loading is not treated as a connection failure", async () =>
{
    var (controller, stub) = Controller();
    stub.ForbidStaff = true;
    try { await Load(controller); throw new Exception("Permission failure was swallowed."); }
    catch (EventOperationException ex) when (ex.Forbidden) { }
});
await Check("Calendar and event details count distinct people and omit cancelled assignments", async () =>
{
    var item = Event();
    var html = await Render("Index", new EventCalendarViewModel { Year = 2028, Month = 5, SelectedDate = new(2028, 5, 10), Events = [item] });
    var json = Regex.Match(html, "<script id=\"event-calendar-data\"[^>]*>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
    using var parsed = JsonDocument.Parse(json);
    Require(parsed.RootElement[0].GetProperty("count").GetInt32() == 1, "Calendar counts assignment rows instead of active people.");
    var decoded = System.Net.WebUtility.HtmlDecode(html);
    Require(decoded.Contains("· 1 người") && !decoded.Contains("· 3 người"), "Calendar chip has a different staff count.");
    var details = await Render("_EventDetailsContent", new EventDetailsViewModel { Event = item });
    Require(details.Contains("data-event-count=\"1\""), "Event details has a different staff count.");
});
await Check("Staffing need form applies to the whole event without separate windows", async () =>
{
    var model = new EventDetailsViewModel { Event = Event(), CanEdit = true, IsOffice = true };
    var html = await Render("_EventOverview", model);
    Require(!html.Contains("name=\"Need.Start\"") && !html.Contains("name=\"Need.End\"")
        && !html.Contains("name=\"Need.Name\""), "Need editor still requests a separate staffing window.");
    Require(html.Contains("name=\"Need.Count\"") && html.Contains("name=\"Need.Reason\""),
        "Whole-event staffing lost the count or change reason.");
});
await Check("Validation response preserves the edited need and entered values", async () =>
{
    var model = new EventDetailsViewModel
    {
        Event = Event(), CanEdit = true,
        Need = new() { NeedId = 21, Department = "KY_THUAT", Role = "Phụ trách âm thanh và micro",
            Count = 7, Reason = "Cần thêm kỹ thuật" }
    };
    var state = new ModelStateDictionary();
    state.AddModelError("Need.Count", "Số người không hợp lệ.");
    var html = await Render("_EventOverview", model, state);
    Require(Regex.IsMatch(html, "<input[^>]*name=\"Need.NeedId\"[^>]*value=\"21\""), "Edited need ID was reset to a new row.");
    Require(Regex.IsMatch(html, "<input[^>]*name=\"Need.Count\"[^>]*value=\"7\""), "Entered count was discarded.");
});

await Check("Legacy assignment popup keeps optional catalog roles alongside saved staffing counts", async () =>
{
    var item = Event();
    item.NhuCaus.First().SoNguoi = 7;
    var html = System.Net.WebUtility.HtmlDecode(await Render("_AssignmentPopup", new EventDetailsViewModel
    {
        Event = item, SelectedDepartment = "KY_THUAT",
        Assignments = [new() { EmployeeId = 10 }], Employees = [new() { MaNhanVien = 10 }]
    }));
    Require(html.Contains("value=\"Phụ trách ánh sáng\""), "Legacy picker lost an optional catalog role.");
    Require(html.Contains("Cần 7, đã có 0"), "Legacy picker ignored the saved seven-person requirement.");
});

await Check("Need editor retains allowed departments after their last need is deleted", async () =>
{
    var item = Event();
    item.TinhTheoGio = true;
    item.PhanCongs.Clear();
    // The last IT need has been deleted; only the technical need remains.
    foreach (var clearAll in new[] { false, true })
    {
        if (clearAll) item.NhuCaus.Clear();
        var html = await Render("_EventOverview", new EventDetailsViewModel { Event = item, CanEdit = true });
        var select = Regex.Match(html, "<select[^>]*name=\"Need.Department\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
        foreach (var code in new[] { "TIEN_SANH", "AM_THUC", "BEP", "KY_THUAT", "IT", "AN_NINH" })
            Require(select.Contains("value=\"" + code + "\""), "Need editor omitted allowed department " + code);
        Require(!select.Contains("value=\"BUONG_PHONG\""), "Need editor offered a department outside the MICE catalog.");
    }
});

await Check("Both staff views distinguish an empty active list from cancelled history", async () =>
{
    foreach (var view in new[] { "_EventStaff", "_HourlyEventStaff" })
    {
        var item = Event();
        item.PhanCongs = [new() { MaNhanVien = 11, TrangThaiThamGia = "HUY_PHAN_CONG" }];
        var html = System.Net.WebUtility.HtmlDecode(await Render(view, new EventDetailsViewModel { Event = item, CanManage = true }));
        Require(html.Contains("Chưa sắp xếp sự kiện") && html.Contains("Bố trí ca bên Lịch làm việc") && html.Contains("Lịch sử phân công"),
            view + " lost the empty-state schedule link or cancelled history.");
        Require(html.Contains("0 nhân sự tham gia") && !html.Contains("Không tải được danh sách nhân sự"), "Empty staffing was rendered as a load failure.");
    }
});
await Check("Both read-only staff views show the source shift, task and participation time", async () =>
{
    foreach (var view in new[] { "_EventStaff", "_HourlyEventStaff" })
    {
        var item = Event();
        item.PhanCongs = [new()
        {
            MaNhanVien = 10, TrangThaiThamGia = "DA_PHAN", MaPhanCa = 55,
            NhanVien = new() { Ho = "Nguyễn", Ten = "An" }, MaPhongBanSuKien = "KY_THUAT",
            VaiTroDuAn = "Phụ trách âm thanh và micro", NhiemVuCuThe = "Kiểm tra micro hội trường",
            BatDau = item.NgayBatDau, KetThuc = item.NgayKetThuc
        }];
        var html = System.Net.WebUtility.HtmlDecode(await Render(view, new EventDetailsViewModel { Event = item, CanManage = true }));
        foreach (var text in new[] { "Nguyễn An", "Kỹ thuật", "Kiểm tra micro hội trường", "Ca #55", "10/05 09:00", "10/05 17:00" })
            Require(html.Contains(text), view + " omitted " + text);
        Require(!html.Contains("<form") && !html.Contains("data-add-member"), "Read-only staff view contains assignment actions.");
    }
});

await Check("Frozen event explanation uses saved basis rather than the current multiplier", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "HOAN_THANH"; item.PhanCongs = [];
    var basis = new OvertimeHotel.HRM.Core.Policies.EventPaySlice(item.NgayBatDau, item.NgayBatDau.AddHours(2), 8_000_000m, 1, 4m);
    var model = new EventDetailsViewModel { Event = item, HourlyEventRate = 2m,
        PayPreview = new(1, [new(10, "Frozen worker", new(2028, 5, 1), 120m, 307_692m, [basis])], [], "") };
    var html = System.Net.WebUtility.HtmlDecode(await Render("_HourlyEventAcceptance", model));
    Require(!html.Contains("× hệ số 2.00") && html.Contains("× 4.00"),
        "A frozen event is explained using the current multiplier instead of its saved rate.");
});
await Check("Hourly work corrections retain entered values and remain office-only", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "CHO_NGHIEM_THU";
    item.PhanCongs = [new() { MaPhanCong = 1, MaNhanVien = 10, MaPhanCa = 55, TrangThaiThamGia = "DA_PHAN",
        BatDau = item.NgayBatDau, KetThuc = item.NgayKetThuc,
        CongThucTes = [new() { MaCong = 81, MaPhanCong = 1, BatDau = item.NgayBatDau, KetThuc = item.NgayBatDau.AddHours(1),
            MaNguoiXacNhan = 5, NgayXacNhan = item.NgayBatDau.AddHours(2) }] }];
    var work = JsonSerializer.Deserialize<EventWorkInput>("{\"WorkId\":81,\"AssignmentId\":1,\"Start\":\"2028-05-10T09:30:00\",\"End\":\"2028-05-10T10:00:00\",\"Reason\":\"Keep correction reason\"}")!;
    var model = new EventDetailsViewModel { Event = item, IsOffice = true, CanManage = true, Work = work };
    var state = new ModelStateDictionary(); state.AddModelError("Work.End", "Invalid work interval");
    var html = await Render("_HourlyEventAcceptance", model, state);
    Require(html.Contains("name=\"Work.WorkId\" value=\"81\"")
        && html.Contains("2028-05-10T09:30") && html.Contains("Keep correction reason"),
        "Office correction form lost the existing work ID or submitted values.");
    model.IsOffice = false;
    html = await Render("_HourlyEventAcceptance", model);
    Require(!html.Contains("name=\"Work.WorkId\"") && !html.Contains("name=\"Work.Reason\""),
        "Manager was offered an office correction form.");
});

await Check("Participation forms request only start and end without attendance exceptions or additional segments", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "CHO_NGHIEM_THU";
    item.PhanCongs = [new() { MaPhanCong = 1, MaNhanVien = 10, BatDau = item.NgayBatDau, KetThuc = item.NgayKetThuc }];
    var model = new EventDetailsViewModel { Event = item, CanManage = true, IsOffice = true };
    var html = await Render("_HourlyEventAcceptance", model);
    Require(!html.Contains("name=\"Work.Explanation\""), "Participation still requests an attendance explanation.");
    Require(Regex.Matches(html, "name=\"Work.Start\"").Count == 1 && Regex.Matches(html, "name=\"Work.End\"").Count == 1,
        "New participation must expose exactly one start/end pair.");
    item.PhanCongs.Single().CongThucTes.Add(new() { MaCong = 81, BatDau = item.NgayBatDau, KetThuc = item.NgayKetThuc });
    model.IsOffice = false;
    html = await Render("_HourlyEventAcceptance", model);
    Require(!html.Contains("name=\"Work.Start\""), "Recorded participation still offers additional break segments.");
});

await Check("Event form loads without querying the venue catalog", async () =>
{
    var (controller, _) = Controller();
    var method = typeof(SuKienController).GetMethod("FormAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    var model = await (Task<EventFormViewModel>)method.Invoke(controller, [new EventFormViewModel()])!;
    Require(model.Venues.Count == 0, "Event form still depends on a venue catalog.");
});

await Check("Event form uses the hotel and only event start and end", async () =>
{
    foreach (var hourly in new[] { false, true })
    {
        var html = System.Net.WebUtility.HtmlDecode(await Render("_EventForm", new EventFormViewModel
        { Input = new() { HourlyCompensation = hourly, Start = new(2028, 5, 10, 9, 0, 0), End = new(2028, 5, 10, 17, 0, 0) } }));
        Require(html.Contains("Khách sạn The Overtime"), "Form lost its fixed hotel location.");
        foreach (var field in new[] { "Venue", "VenueId", "SetupMinutes", "TeardownMinutes" })
            Require(!html.Contains("name=\"Input." + field + "\""), "Form still requests removed setting " + field);
        Require(html.Contains("name=\"Input.Start\"") && html.Contains("name=\"Input.End\"")
            && html.Contains("name=\"Input.ExpectedGuests\""), "Form lost event duration or guest count.");
    }
});

await Check("Payroll confirmation never executes employee names as script", async () =>
{
    var html = System.Net.WebUtility.HtmlDecode(await Render("Index", new BangLuongIndexViewModel
    {
        IsHR = true, CanCalculate = true, ShowPayrollData = true,
        CurrentPeriod = new() { TrangThai = "MO" },
        Items = [new() { MaPhieuLuong = 41, HoTen = "O'Brien');window.injected=true;//", TienSuKien = 100m }]
    }, controller: "BangLuong"));
    Require(!Regex.Matches(html, "onclick=\"([^\"]*)\"").Any(m => m.Value.Contains("window.injected")),
        "Employee name entered an executable confirmation handler.");
});
await Check("Payroll partial forms return to a complete payslip page", async () =>
{
    var html = await Render("_PayslipDetailBody", new PhieuLuongDetailViewModel
    { MaPhieuLuong = 41, CanEdit = true }, controller: "BangLuong");
    var targets = Regex.Matches(html, "name=\"returnUrl\" value=\"([^\"]*)\"");
    Require(targets.Count > 0 && targets.All(m => m.Groups[1].Value.Contains("Detail") && !m.Groups[1].Value.Contains("Partial")),
        "A payroll action can redirect to partial HTML instead of the full page.");
});
await Check("Payroll event explanation does not invent a fixed settlement multiplier", async () =>
{
    var html = System.Net.WebUtility.HtmlDecode(await Render("_PayslipDetailBody", new PhieuLuongDetailViewModel
    { EventAllowances = [new() { TenKhoan = "Saved event payment", SoTien = 307692m }] }, controller: "BangLuong"));
    Require(!html.Contains("+100%") && !html.Contains("+1.0x") && !html.Contains("x2"),
        "Payroll displays a hardcoded multiplier instead of the saved settlement.");
});

await Check("Phase 3 readiness counts distinct people across assignments and flags unresolved work", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "CHO_NGHIEM_THU";
    item.PhanCongs = [
        new() { MaPhanCong = 1, MaNhanVien = 10, DanhGia = "HOAN_THANH", NgayDanhGia = DateTimeOffset.UtcNow,
            CongThucTes = [new() { BatDau = item.NgayBatDau, KetThuc = item.NgayBatDau.AddHours(1) }] },
        new() { MaPhanCong = 2, MaNhanVien = 10, DanhGia = "CHUA_DANH_GIA" },
        new() { MaPhanCong = 3, MaNhanVien = 11, DanhGia = "KHONG_THAM_GIA", NgayDanhGia = DateTimeOffset.UtcNow },
        new() { MaPhanCong = 4, MaNhanVien = 12, TrangThaiThamGia = "DA_HUY" }
    ];
    var html = await Render("_HourlyEventAcceptance", new EventDetailsViewModel { Event = item });
    Require(html.Contains("data-acceptance-staff=\"2\"") && html.Contains("data-acceptance-ready=\"1\"")
        && html.Contains("data-acceptance-missing-work=\"1\"") && html.Contains("data-acceptance-unreviewed=\"1\""),
        "Readiness summary counts rows or hides incomplete employees.");
});
await Check("Phase 3 acceptance filters early payment periods and retains selection with a confirmation summary", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "CHO_NGHIEM_THU";
    item.NgayBatDau = item.NgayBatDau.AddYears(-4); item.NgayKetThuc = item.NgayKetThuc.AddYears(-4);
    var model = new EventDetailsViewModel { Event = item, CanAccept = true, IsOffice = true,
        PayPreview = new(item.Version, [new(10, "Worker", new(2028,6,1), 120, 153846, [])], [], "token"),
        PaymentPeriods = [new() { MaKyLuong = 1, Thang = 5, Nam = 2028, TrangThai = "MO" },
            new() { MaKyLuong = 2, Thang = 6, Nam = 2028, TrangThai = "MO" }] };
    var html = await Render("_HourlyEventAcceptance", model);
    model.PaymentPeriodId = 2;
    html = await Render("_HourlyEventAcceptance", model);
    var options = Regex.Match(html, "<select[^>]*name=\"paymentPeriodId\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
    Require(!options.Contains("value=\"1\"") && options.Contains("value=\"2\""), "A payment month precedes the latest work month.");
    Require(Regex.IsMatch(options,"value=\"2\"[^>]*selected"),"The chosen eligible period was discarded.");
    Require(html.Contains("data-event-acceptance-form") && html.Contains("data-acceptance-confirm")
        && html.Contains("153.846") && html.Contains("data-acceptance-period"), "Missing explicit confirmation of the exact settlement and period.");
});
await Check("Phase 3 planned events cannot offer final acceptance even with a forged UI flag", async () =>
{
    var item = Event(); item.TinhTheoGio = true;
    var html = await Render("_HourlyEventAcceptance", new EventDetailsViewModel { Event = item, CanAccept = true, IsOffice = true,
        PayPreview = new(item.Version, [], [], "token"), PaymentPeriods = [new() { MaKyLuong = 2, Thang=6, Nam=2028, TrangThai="MO" }] });
    Require(!html.Contains("data-event-acceptance-form") && !Regex.IsMatch(html, "<form[^>]*action=\"[^\"]*AcceptHourly"), "Planned event exposes acceptance.");
});

await Check("Phase 3 frozen settlements distinguish pending links, payslips and locked periods", async () =>
{
    var item = Event(); item.TinhTheoGio = true; item.TrangThai = "HOAN_THANH";
    var model = new EventDetailsViewModel { Event=item, IsOffice=true, PayPreview=new(item.Version,[
        new(10,"Pending",new(2024,5,1),120,153846,[],new(41,6,2024,"MO",null,null)),
        new(11,"Linked",new(2024,5,1),120,153846,[],new(41,6,2024,"MO",71,81)),
        new(12,"Locked",new(2024,5,1),120,153846,[],new(42,7,2024,"KHOA_SO",72,82))
    ],[],"saved") };
    var html = System.Net.WebUtility.HtmlDecode(await Render("_HourlyEventAcceptance", model));
    Require(html.Contains("data-transfer-status=\"pending\"") && html.Contains("data-transfer-status=\"linked\"")
        && html.Contains("data-transfer-status=\"locked\"") && html.Contains("data-payslip-id=\"81\"")
        && html.Contains("06/2024"), "Frozen settlement lost its actual payroll stage, period or payslip link.");
    Require(!html.Contains("data-event-acceptance-form") && !html.Contains("data-acceptance-confirm"), "Frozen event can be accepted again.");
});
await Check("Phase 3 zero settlement has no invented payment or paid status", async () =>
{
    var item=Event(); item.TinhTheoGio=true; item.TrangThai="HOAN_THANH";
    item.MaNguoiNghiemThu=1; item.NgayNghiemThu=DateTimeOffset.UtcNow;
    item.PhanCongs=[new() { MaNhanVien=10, DanhGia="KHONG_THAM_GIA", NgayDanhGia=DateTimeOffset.UtcNow }];
    var html=await Render("_HourlyEventAcceptance",new EventDetailsViewModel { Event=item, PayPreview=new(item.Version,[],[],"saved") });
    Require(html.Contains("data-zero-settlement") && !html.Contains("data-payslip-id") && !html.Contains("data-transfer-status=\"paid\""),
        "Zero settlement invents a payroll entry or claims payment.");
});

await Check("Phase 3 zero acceptance remains available while closed payment periods are excluded", async () =>
{
    var item=Event(); item.TinhTheoGio=true; item.TrangThai="CHO_NGHIEM_THU";
    item.NgayBatDau=item.NgayBatDau.AddYears(-4); item.NgayKetThuc=item.NgayKetThuc.AddYears(-4);
    item.PhanCongs=[new() { MaNhanVien=10, DanhGia="KHONG_THAM_GIA", NgayDanhGia=DateTimeOffset.UtcNow }];
    var model=new EventDetailsViewModel { Event=item, CanAccept=true, IsOffice=true, PayPreview=new(item.Version,[],[],"zero"),
        PaymentPeriods=[new() { MaKyLuong=1,Nam=2024,Thang=5,TrangThai="MO" },
            new() { MaKyLuong=2,Nam=2024,Thang=5,TrangThai="MO",NgayGuiDuyet=DateTimeOffset.UtcNow },
            new() { MaKyLuong=3,Nam=2024,Thang=5,TrangThai="MO",NgayDuyet=DateTimeOffset.UtcNow },
            new() { MaKyLuong=4,Nam=2024,Thang=5,TrangThai="KHOA_SO" }] };
    Require(model.CanFinalizeAcceptance && model.EligiblePaymentPeriods.Select(k=>k.MaKyLuong).SequenceEqual([1]),
        "Zero approval is blocked or a sent/approved/locked period is offered.");
    var html=await Render("_HourlyEventAcceptance",model);
    var form=Regex.Match(html,"<form[^>]*data-event-acceptance-form[^>]*>(.*?)</form>",RegexOptions.Singleline).Groups[1].Value;
    Require(form.Contains("data-acceptance-confirm") && !Regex.IsMatch(form,"<button[^>]*type=\"submit\"[^>]*disabled"),
        "Zero approval cannot reach its confirmation.");
});

await Check("Completed participation is a read-only record without pending confirmation labels", async () =>
{
    var item=Event(); item.TinhTheoGio=true; item.TrangThai="HOAN_THANH";
    var html=System.Net.WebUtility.HtmlDecode(await Render("_HourlyEventAcceptance",new EventDetailsViewModel {
        Event=item, IsOffice=true, CanManage=true, CanAccept=true, PayPreview=new(item.Version,[],[],"saved") }));
    Require(!html.Contains("Nhân viên cần xác nhận") && !html.Contains("Chưa đánh giá") && !html.Contains("<form"),
        "Completed event still asks for employee confirmation.");
    Require(html.Contains("data-acceptance-closed"), "Completed participation lacks a read-only explanation.");
});
await Check("Imported completion without reviews or settlement warns instead of claiming zero acceptance", async () =>
{
    var item=Event(); item.TinhTheoGio=true; item.TrangThai="HOAN_THANH";
    var html=await Render("_HourlyEventAcceptance",new EventDetailsViewModel {
        Event=item, PayPreview=new(item.Version,[],[],"saved") });
    Require(html.Contains("data-acceptance-reconciliation") && !html.Contains("data-zero-settlement"),
        "Incomplete historical data is incorrectly presented as an approved zero payment.");
});

await Check("Participation uses exactly one combined table per employee across source assignments", async () =>
{
    var item=Event(); item.TinhTheoGio=true; item.TrangThai="CHO_NGHIEM_THU";
    item.PhanCongs=[
        new() { MaPhanCong=1,MaNhanVien=10,MaPhanCa=55,NhanVien=new(){Ho="Nguyễn",Ten="An"},
            VaiTroDuAn="Vai trò đầu",BatDau=item.NgayBatDau,KetThuc=item.NgayBatDau.AddHours(2),
            CongThucTes=[new(){MaCong=81,BatDau=item.NgayBatDau,KetThuc=item.NgayBatDau.AddHours(1)}] },
        new() { MaPhanCong=2,MaNhanVien=10,MaPhanCa=56,VaiTroDuAn="Vai trò sau",BatDau=item.NgayBatDau.AddHours(2),KetThuc=item.NgayKetThuc },
        new() { MaPhanCong=3,MaNhanVien=11,MaPhanCa=57,VaiTroDuAn="Vai trò khác",BatDau=item.NgayBatDau,KetThuc=item.NgayKetThuc }
    ];
    var html=System.Net.WebUtility.HtmlDecode(await Render("_HourlyEventAcceptance",new EventDetailsViewModel {
        Event=item,CanManage=true,IsOffice=true }));
    Require(Regex.Matches(html,"data-participation-person=\"10\"").Count==1
        && Regex.Matches(html,"data-participation-person=\"11\"").Count==1
        && Regex.Matches(html,"data-participation-table").Count==2,
        "One person is split into multiple participation tables.");
    var first=Regex.Match(html,"<section[^>]*data-participation-person=\"10\"[^>]*>(.*?)</section>",RegexOptions.Singleline).Groups[1].Value;
    foreach(var text in new[]{"Vai trò đầu","Vai trò sau","Ca #55","Ca #56","Giờ phân công","Giờ thực tế","Kết quả","name=\"Work.WorkId\" value=\"81\"","name=\"Review.AssignmentId\" value=\"2\""})
        Require(first.Contains(text),"Combined employee table omitted "+text);
});

if (args.Contains("--export"))
{
    var export = Path.Combine(root, ".probe/codex-phase1-html");
    Directory.CreateDirectory(export);
    await File.WriteAllTextAsync(Path.Combine(export, "details.html"), await Render("_EventDetailsContent", new EventDetailsViewModel
    {
        Event = Event(), CanEdit = true, CanManage = true,
        Need = new() { Count = 1 }
    }));
    await File.WriteAllTextAsync(Path.Combine(export, "dialogs.html"), await Render("_EventDialogs", new EventDetailsViewModel()));
    await File.WriteAllTextAsync(Path.Combine(export, "calendar.html"), await Render("Index", new EventCalendarViewModel
    {
        Year = 2028, Month = 5, SelectedDate = new(2028, 5, 10), Events = [Event()]
    }));
    var hourly = Event(); hourly.TinhTheoGio = true; hourly.TrangThai = "CHO_NGHIEM_THU";
    hourly.PhanCongs = [new() { MaPhanCong = 1, MaNhanVien = 10, MaPhanCa = 55, TrangThaiThamGia = "DA_PHAN",
        NhanVien = new() { Ho = "Nguyễn", Ten = "An" }, BatDau = hourly.NgayBatDau, KetThuc = hourly.NgayKetThuc,
        CongThucTes = [new() { MaCong = 81, MaPhanCong = 1, BatDau = hourly.NgayBatDau, KetThuc = hourly.NgayBatDau.AddHours(1),
            MaNguoiXacNhan = 5, NgayXacNhan = hourly.NgayBatDau.AddHours(2) }] }];
    var workModel = new EventDetailsViewModel { Event = hourly, IsOffice = true, CanManage = true, Tab = "acceptance" };
    await File.WriteAllTextAsync(Path.Combine(export, "hourly-work.html"), await Render("_EventDetailsContent", workModel));
    workModel.Work = new() { WorkId = 81, AssignmentId = 1, Start = new(2028, 5, 10, 9, 30, 0),
        End = new(2028, 5, 10, 10, 0, 0), Reason = "Correct entry with evidence" };
    var workState = new ModelStateDictionary(); workState.AddModelError("Work.End", "Review participation time");
    await File.WriteAllTextAsync(Path.Combine(export, "hourly-work-invalid.html"), await Render("_EventDetailsContent", workModel, workState));
    var acceptedItem = Event(); acceptedItem.TinhTheoGio = true; acceptedItem.TrangThai = "CHO_NGHIEM_THU";
    acceptedItem.NgayBatDau = acceptedItem.NgayBatDau.AddYears(-4); acceptedItem.NgayKetThuc = acceptedItem.NgayKetThuc.AddYears(-4);
    acceptedItem.PhanCongs = [new() { MaPhanCong=1, MaNhanVien=10, DanhGia="HOAN_THANH", NgayDanhGia=DateTimeOffset.UtcNow,
        BatDau=acceptedItem.NgayBatDau, KetThuc=acceptedItem.NgayKetThuc,
        CongThucTes=[new() { BatDau=acceptedItem.NgayBatDau, KetThuc=acceptedItem.NgayBatDau.AddHours(2) }] }];
    var acceptModel = new EventDetailsViewModel { Event=acceptedItem, CanManage=true, CanAccept=true, IsOffice=true, Tab="acceptance",
        PayPreview=new(acceptedItem.Version,[new(10,"Worker",new(2024,5,1),120,153846,[])],[],"saved-preview"),
        PaymentPeriods=[new() { MaKyLuong=41, Thang=5, Nam=2024, TrangThai="MO" }] };
    await File.WriteAllTextAsync(Path.Combine(export,"hourly-acceptance.html"), await Render("_EventDetailsContent",acceptModel));

}
await Check("Auto start overview explains readiness without requiring a manual start button", async () =>
{
    var item = Event(); item.TinhTheoGio = true;
    var html = System.Net.WebUtility.HtmlDecode(await Render("_EventOverview", new EventDetailsViewModel
        { Event = item, CanManage = true, IsOffice = true }));
    Require(html.Contains("Hệ thống tự bắt đầu") && !Regex.IsMatch(html, "name=\"status\"[^>]*value=\"DANG_DIEN_RA\""),
        "Planned event still requires pressing Start instead of explaining automatic readiness.");
    item.NgayBatDau = DateTimeOffset.UtcNow.AddHours(-4); item.NgayKetThuc = DateTimeOffset.UtcNow.AddHours(-2);
    html = System.Net.WebUtility.HtmlDecode(await Render("_EventOverview", new EventDetailsViewModel
        { Event = item, CanManage = true, IsOffice = true }));
    Require(html.Contains("đã quá giờ kết thúc") && html.Contains("đối soát"),
        "An expired planned event incorrectly promises automatic start.");
});

await Check("Auto start worker runs without page requests and retries after a failed scan", async () =>
{
    var service = DispatchProxy.Create<IHotelEventService, StaffServiceStub>();
    var stub = (StaffServiceStub)(object)service;
    var calls = 0;
    var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    stub.AutoStart = cancellationToken =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref calls) == 1) throw new IOException("Temporary database failure");
        retried.TrySetResult();
        return Task.FromResult(0);
    };
    await using var services = new ServiceCollection().AddScoped<IHotelEventService>(_ => service).BuildServiceProvider();
    using var worker = new HotelEventAutoStartWorker(services.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<HotelEventAutoStartWorker>.Instance);
    await worker.StartAsync(CancellationToken.None);
    try { await retried.Task.WaitAsync(TimeSpan.FromSeconds(25)); }
    finally { await worker.StopAsync(CancellationToken.None); }
    Require(calls >= 2, "Worker requires an HTTP request or stopped retrying after a database error.");
});

Console.WriteLine($"Hotel event web tests: {passed} passed, {failures} failed");
return failures == 0 ? 0 : 1;

public class StaffServiceStub : DispatchProxy
{
    public DuAn Item = null!;
    public bool FailPicker, FailStaff, ForbidStaff, OutOfScope;
    public Func<CancellationToken, Task<int>> AutoStart = _ => Task.FromResult(0);
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "AutoStartReadyEventsAsync" => AutoStart((CancellationToken)args![0]!),
        "GetOverviewAsync" => Task.FromResult<DuAn?>(OutOfScope ? null : new DuAn
        {
            MaDuAn = Item.MaDuAn, LoaiDuAn = Item.LoaiDuAn, TrangThai = Item.TrangThai,
            NgayBatDau = Item.NgayBatDau, NgayKetThuc = Item.NgayKetThuc, NhuCaus = Item.NhuCaus
        }),
        "GetAsync" => ForbidStaff ? Task.FromException<DuAn?>(new EventOperationException("", "Permission changed", true))
            : FailStaff ? Task.FromException<DuAn?>(new IOException("Assignment query unavailable")) : Task.FromResult<DuAn?>(OutOfScope ? null : Item),
        "EmployeesAsync" => FailPicker ? Task.FromException<IReadOnlyList<NhanVien>>(new IOException("Picker unavailable")) : Task.FromResult<IReadOnlyList<NhanVien>>([]),
        "ManagersAsync" => Task.FromResult<IReadOnlyList<NhanVien>>([]),
        "HourlyEventRateAsync" => Task.FromResult(2m),
        "VenuesAsync" => Task.FromException<IReadOnlyList<DiaDiemSuKien>>(new IOException("Venue catalog unavailable")),
        "ShiftsAsync" or "AvailableShiftsAsync" => Task.FromResult<IReadOnlyList<PhanCa>>([]),
        "HistoryAsync" => Task.FromResult<IReadOnlyList<NhatKyQuanTri>>([]),
        _ => throw new InvalidOperationException("Unexpected method: " + method.Name)
    };
}
public class TestRouter : IRouter
{
    public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, "/SuKien/" + context.Values.GetValueOrDefault("action"));
}
