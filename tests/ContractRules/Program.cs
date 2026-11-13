using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using System.Text.Json;

var failures = new List<string>();
var passed = 0;
async Task Check(string name, Func<Task> check)
{
    try { await check(); passed++; }
    catch (Exception error) { failures.Add(name + ": " + error.Message); }
}
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
var today = DateOnly.FromDateTime(DateTime.Today);
var leaveToday = new DateOnly(2026, 11, 13);
await Check("Leave submission deadline", () =>
{
    bool Valid(int type, int days) => LeaveApprovalPolicy.ValidateCreate(type, leaveToday.AddDays(days), leaveToday.AddDays(days), "Lý do", leaveToday).IsValid;
    Require(!Valid(1, 0), "Same-day leave must be rejected");
    Require(Valid(1, 1) && Valid(1, 30), "Leave must be allowed 1 through 30 days ahead");
    Require(!Valid(1, 31), "Leave beyond 30 days must be rejected");
    Require(Valid(5, 45), "Resignation is excluded from leave deadline");
    return Task.CompletedTask;
});

foreach (var departmentName in new[] { "Ban Giám Đốc", "Ban Giám Đốc (BOD)", "ban giám đốc" })
await Check("Board catalog " + departmentName, async () =>
{
    await using var db = await Database(departmentName);
    var controller = Attach(new ChucVuController(db, NullLogger<ChucVuController>.Instance));
    var result = (JsonResult)await controller.GetByDepartment(41);
    var ids = JsonSerializer.SerializeToElement(result.Value).EnumerateArray().Select(x => x.GetProperty("maChucVu").GetInt32()).ToArray();
    Require(ids.SequenceEqual([21]), "Ban Giám Đốc must contain only Tổng Giám Đốc; got " + string.Join(',', ids));
});
await Check("Other departments retain shared roles", async () =>
{
    await using var db = await Database();
    var controller = Attach(new ChucVuController(db, NullLogger<ChucVuController>.Instance));
    var result = (JsonResult)await controller.GetByDepartment(2);
    var ids = JsonSerializer.SerializeToElement(result.Value).EnumerateArray().Select(x => x.GetProperty("maChucVu").GetInt32()).Order().ToArray();
    Require(ids.SequenceEqual([22, 25]), "Shared roles and own department roles must remain available");
});
await Check("Unknown or unselected department returns no positions", async () =>
{
    await using var db = await Database();
    var controller = Attach(new ChucVuController(db, NullLogger<ChucVuController>.Instance));
    foreach (var id in new int?[] { null, 0, 999 })
    {
        var result = (JsonResult)await controller.GetByDepartment(id);
        Require(JsonSerializer.SerializeToElement(result.Value).GetArrayLength() == 0, "No department must not expose all positions");
    }
});
await Check("Position policy translates to PostgreSQL without database access", () =>
{
    using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Database=not_opened;Username=test").Options);
    var sql = db.ChucVus.Where(OrganizationPositionPolicy.ForDepartment(new() { MaPhongBan = 41, TenPhongBan = "Ban Giám Đốc" })).ToQueryString();
    Require(sql.Contains("upper(") && sql.Contains("ma_phong_ban"), "Policy must translate to SQL");
    return Task.CompletedTask;
});
foreach (var position in new[] { 22, 23, 24, 25 })
await Check("Create rejects invalid Board position " + position, async () =>
{
    await using var db = await Database();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var model = Form(today); model.PositionId = position;
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey("PositionId"), "Expected position validation error");
    Require(await db.HopDongs.CountAsync() == 0, "Invalid position must not create a contract");
});
foreach (var status in new[] { "HET_HAN", "DA_CHAM_DUT", "CHAM_DUT", "THANH_LY", "BOGUS" })
await Check("Create rejects status " + status, async () =>
{
    await using var db = await Database();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var model = Form(today); model.Contract.TrangThai = status;
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey("Contract.TrangThai"), "Expected status validation error");
    Require(await db.HopDongs.CountAsync() == 0, "Invalid status must not be saved or silently changed");
});
await Check("Create rejects a contract already expired by date", async () =>
{
    await using var db = await Database();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var model = Form(today.AddYears(-2)); model.Contract.NgayKetThuc = today.AddDays(-1);
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey("Contract.NgayKetThuc"), "Expected expired-date error");
    Require(await db.HopDongs.CountAsync() == 0, "Past end date must not create a new expired contract");
});
foreach (var start in new[] { today, today.AddDays(10) })
await Check("Valid creation with start " + start, async () =>
{
    await using var db = await Database();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(Form(start));
    Require(controller.ModelState.IsValid, "Valid creation rejected");
    var saved = await db.HopDongs.SingleAsync();
    Require(saved.TrangThai == "HIEU_LUC", "Store the standard signed contract state");
    Require(ContractState.Classify(saved, today) == (start > today ? "CHUA_HIEU_LUC" : "HIEU_LUC"), "Display must follow dates");
});
await Check("Existing contracts can be terminated via Terminate action", async () =>
{
    await using var db = await Database();
    var model = Form(today); model.Contract.MaHopDong = 7;
    db.HopDongs.Add(model.Contract); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var termModel = new HopDongTerminateViewModel { NgayChamDut = today, LoaiChamDut = ContractTerminationTypes.NHAN_VIEN_XIN_NGHI };
    await controller.Terminate(7, termModel);
    var saved = await db.HopDongs.AsNoTracking().SingleAsync();
    Require(saved.TrangThai == "DA_CHAM_DUT", "Terminate must save DA_CHAM_DUT");
    Require(saved.LoaiChamDut == ContractTerminationTypes.NHAN_VIEN_XIN_NGHI, "Terminate must save reason type");
});
await Check("Employee profile cannot bypass Board position rule", async () =>
{
    await using var db = await Database();
    var employee = await db.NhanViens.AsNoTracking().SingleAsync(); employee.MaChucVu = 23;
    var controller = Attach(new NhanVienController(db, NullLogger<NhanVienController>.Instance));
    await controller.Edit(10, new() { Employee = employee });
    Require(controller.ModelState.ContainsKey("Employee.MaChucVu"), "Employee profile must enforce department rule");
    Require((await db.NhanViens.AsNoTracking().SingleAsync()).MaChucVu == 21, "Invalid profile edit must not persist");
});

foreach (var (stage, type, hasEnd, errorKey) in new[]
{
    ("THU_VIEC", "XAC_DINH_THOI_HAN_1_NAM", true, "Contract.LoaiHopDong"),
    ("THU_VIEC", "XAC_DINH_THOI_HAN_1_NAM", false, "Contract.LoaiHopDong"),
    ("CHINH_THUC", "THU_VIEC", true, "Contract.LoaiHopDong"),
    ("UNKNOWN", "XAC_DINH_THOI_HAN_1_NAM", true, "Contract.HinhThucHopDong")
})
await Check($"Create rejects mismatched stage/duration {stage}/{type}/{hasEnd}", async () =>
{
    await using var db = await Database();
    var model = Form(today);
    model.Contract.HinhThucHopDong = stage; model.Contract.LoaiHopDong = type;
    model.Contract.NgayKetThuc = hasEnd ? today.AddDays(30) : null;
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey(errorKey), "Expected validation error for " + errorKey);
    Require(await db.HopDongs.CountAsync() == 0, "Rejected creation must not be saved");
});

await Check("Create calculates probation dates and salary on server", async () =>
{
    await using var db = await Database();
    var model = Form(today);
    model.Contract.HinhThucHopDong = "THU_VIEC"; model.Contract.LoaiHopDong = "THU_VIEC";
    model.Contract.NgayKetThuc = today.AddDays(30);
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.IsValid, "Valid probation rejected");
    var saved = await db.HopDongs.AsNoTracking().SingleAsync();
    Require(saved.NgayKetThuc == today.AddMonths(2).AddDays(-1) && saved.LoaiHopDong == "THU_VIEC" && saved.LuongCoBan == 21_250_000m, "Two months and 85 percent must be derived");
});
await Check("Official one-year contract still gets its end date", async () =>
{
    await using var db = await Database(); var model = Form(today); model.Contract.NgayKetThuc = null;
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance)); await controller.Create(model);
    Require(controller.ModelState.IsValid && (await db.HopDongs.SingleAsync()).NgayKetThuc == today.AddYears(1), "Official dates must still calculate");
});

foreach (var (type, years) in new[] { ("XAC_DINH_THOI_HAN_1_NAM", 1), ("XAC_DINH_THOI_HAN_3_NAM", 3) })
await Check("Create fills missing official end for " + type, async () =>
{
    await using var db = await Database();
    var model = Form(today); model.Contract.LoaiHopDong = type; model.Contract.NgayKetThuc = null;
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance)); await controller.Create(model);
    Require(controller.ModelState.IsValid && (await db.HopDongs.AsNoTracking().SingleAsync()).NgayKetThuc == today.AddYears(years), "Missing date must calculate end date");
});

await Check("Create allows transition/promotion closing previous contract when checked", async () =>
{
    await using var db = await Database();
    var oldContract = Form(today.AddMonths(-6)).Contract;
    oldContract.MaHopDong = 1;
    oldContract.NgayKetThuc = today.AddMonths(6);
    db.HopDongs.Add(oldContract);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var newModel = Form(today);
    newModel.Contract.SoHopDong = "HDLD-NEW";
    newModel.ClosePreviousContract = true;
    newModel.PositionId = 21; // BOD
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(newModel);

    Require(controller.ModelState.IsValid, "Transition contract must be valid");
    var contracts = await db.HopDongs.AsNoTracking().OrderBy(c => c.MaHopDong).ToListAsync();
    Require(contracts.Count == 2, "Must have 2 contracts");
    Require(contracts[0].TrangThai == "DA_CHAM_DUT" && contracts[0].NgayKetThuc == today.AddDays(-1), "Old contract must be closed on day before new start");
    Require((await db.NhanViens.AsNoTracking().SingleAsync()).TrangThai == "DANG_LAM", "Employee must remain active DANG_LAM");
});
await Check("Create rejects automatically calculated past end", async () =>
{
    await using var db = await Database(); var model = Form(today.AddYears(-2)); model.Contract.NgayKetThuc = null;
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance)); await controller.Create(model);
    Require(controller.ModelState.ContainsKey("Contract.NgayKetThuc") && await db.HopDongs.CountAsync() == 0, "Calculated expiry must still be validated");
});

await Check("Transition preserves previously expired contract history", async () =>
{
    await using var db = await Database();
    var old = Form(today.AddYears(-3)).Contract;
    old.MaHopDong = 1; old.SoHopDong="EXPIRED-OLD";
    var oldEnd = old.NgayKetThuc;
    db.HopDongs.Add(old); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var model=Form(today); model.Contract.SoHopDong="NEXT-VALID"; model.ClosePreviousContract=true;
    var controller=Attach(new HopDongController(db,NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.IsValid,"Renewal should be valid");
    var stored=await db.HopDongs.AsNoTracking().SingleAsync(x=>x.MaHopDong==1);
    Require(stored.NgayKetThuc==oldEnd && stored.TrangThai=="HIEU_LUC","Expired historical row must not be extended or terminated retroactively");
});
Console.WriteLine($"Contract rules: {passed} passed, {failures.Count} failed.");
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

static HopDongFormViewModel Form(DateOnly start) => new()
{
    EmployeeId = 10, EmployeeName = "Nguyễn An", DepartmentId = 41, PositionId = 21,
    Contract = new() { MaNhanVien = 10, SoHopDong = "HDLD-TEST", NgayBatDau = start, NgayKetThuc = start.AddYears(1),
        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM", LuongCoBan = 35000000, TrangThai = "HIEU_LUC" }
};
static T Attach<T>(T controller) where T : Controller
{
    var http = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
    controller.ControllerContext = new(new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()));
    controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
    controller.TempData = new TempDataDictionary(http, new TestTempDataProvider());
    return controller;
}
static async Task<AppDbContext> Database(string boardName = "Ban Giám Đốc")
{
    var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    db.PhongBans.AddRange(new PhongBan { MaPhongBan = 41, TenPhongBan = boardName }, new PhongBan { MaPhongBan = 2, TenPhongBan = "Tiền sảnh" });
    db.ChucVus.AddRange(
        new ChucVu { MaChucVu = 21, MaPhongBan = 41, NhomLuong = "TONG_GIAM_DOC", TenChucVu = "Tổng Giám Đốc (General Manager)" },
        new ChucVu { MaChucVu = 22, TenChucVu = "Trưởng Bộ Phận" },
        new ChucVu { MaChucVu = 24, MaPhongBan = 41, TenChucVu = "Trợ lý" },
        new ChucVu { MaChucVu = 25, MaPhongBan = 2, TenChucVu = "Lễ tân" });
    db.KhungLuongCapBacs.Add(new KhungLuongCapBac { MaKhungLuong=1, NhomChucVu="TONG_GIAM_DOC", LuongToiThieu=25_000_000m, LuongToiDa=40_000_000m });
    db.NhanViens.Add(new() { MaNhanVien = 10, MaPhongBan = 41, MaChucVu = 21, Ho = "Nguyễn", Ten = "An", EmailCongVu = "an@example.test",
        NgaySinh = new(1990, 1, 1), GioiTinh = "NAM", DienThoai = "0912345678", DiaChi = "Hà Nội", TrinhDo = "Đại học", NgayVaoLam = new(2020, 1, 1) });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
}
sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
