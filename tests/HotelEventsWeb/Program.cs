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
        TenKhung = "Chuẩn bị & phục vụ", SoNguoi = 2, BatDau = new DateTimeOffset(2028, 5, 10, 2, 0, 0, TimeSpan.Zero),
        KetThuc = new DateTimeOffset(2028, 5, 10, 10, 0, 0, TimeSpan.Zero), LaMau = true }]
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
async Task<string> Render(string view, object model, ModelStateDictionary? state = null)
{
    await using var scope = app.Services.CreateAsyncScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.Request.Scheme = "http";
    http.Request.Host = new HostString("phase1.test");
    var routes = new RouteData();
    routes.Values["controller"] = "SuKien";
    routes.Values["action"] = "Details";
    routes.Routers.Add(new TestRouter());
    var context = new ActionContext(http, routes, new ActionDescriptor(), state ?? new());
    var engine = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
    var result = engine.GetView(null, "/Views/SuKien/" + view + ".cshtml", false);
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
await Check("Validation response preserves the edited need and entered values", async () =>
{
    var model = new EventDetailsViewModel
    {
        Event = Event(), CanEdit = true,
        Need = new() { NeedId = 21, Name = "Giữ tên khung", Department = "KY_THUAT", Role = "Phụ trách âm thanh và micro",
            Count = 7, Start = new(2028, 5, 10, 9, 30, 0), End = new(2028, 5, 10, 17, 0, 0), Reason = "Cần thêm kỹ thuật" }
    };
    var state = new ModelStateDictionary();
    state.AddModelError("Need.End", "Khung giờ không hợp lệ.");
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

if (args.Contains("--export"))
{
    var export = Path.Combine(root, ".probe/codex-phase1-html");
    Directory.CreateDirectory(export);
    await File.WriteAllTextAsync(Path.Combine(export, "details.html"), await Render("_EventDetailsContent", new EventDetailsViewModel
    {
        Event = Event(), CanEdit = true, CanManage = true,
        Need = new() { Start = new(2028, 5, 10, 9, 0, 0), End = new(2028, 5, 10, 17, 0, 0) }
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
}
Console.WriteLine($"Hotel event web tests: {passed} passed, {failures} failed");
return failures == 0 ? 0 : 1;

public class StaffServiceStub : DispatchProxy
{
    public DuAn Item = null!;
    public bool FailPicker, FailStaff, ForbidStaff, OutOfScope;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "GetOverviewAsync" => Task.FromResult<DuAn?>(OutOfScope ? null : new DuAn
        {
            MaDuAn = Item.MaDuAn, LoaiDuAn = Item.LoaiDuAn, TrangThai = Item.TrangThai,
            NgayBatDau = Item.NgayBatDau, NgayKetThuc = Item.NgayKetThuc, NhuCaus = Item.NhuCaus
        }),
        "GetAsync" => ForbidStaff ? Task.FromException<DuAn?>(new EventOperationException("", "Permission changed", true))
            : FailStaff ? Task.FromException<DuAn?>(new IOException("Assignment query unavailable")) : Task.FromResult<DuAn?>(OutOfScope ? null : Item),
        "EmployeesAsync" => FailPicker ? Task.FromException<IReadOnlyList<NhanVien>>(new IOException("Picker unavailable")) : Task.FromResult<IReadOnlyList<NhanVien>>([]),
        "ManagersAsync" => Task.FromResult<IReadOnlyList<NhanVien>>([]),
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
