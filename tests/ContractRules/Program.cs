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
await Check("Contract suggestions include only probation awaiting official contract", async () =>
{
    await using var db = await Database();
    for (var id = 11; id <= 15; id++)
        db.NhanViens.Add(new NhanVien { MaNhanVien = id, Ho = "Kiểm", Ten = $"Thử{id}", TrangThai = id == 15 ? "DA_THOI_VIEC" : "DANG_LAM" });
    HopDong Contract(int id, int employee, string kind, DateOnly start, DateOnly end) => new()
    {
        MaHopDong = id, MaNhanVien = employee, SoHopDong = $"TEST-{id}", HinhThucHopDong = kind,
        LoaiHopDong = kind == "THU_VIEC" ? "THU_VIEC" : "XAC_DINH_THOI_HAN_1_NAM",
        NgayBatDau = start, NgayKetThuc = end, TrangThai = "HIEU_LUC"
    };
    db.HopDongs.AddRange(
        Contract(1, 11, "THU_VIEC", today.AddDays(-10), today.AddDays(20)),
        Contract(2, 12, "THU_VIEC", today.AddMonths(-3), today.AddDays(-1)),
        Contract(3, 13, "THU_VIEC", today.AddMonths(-2), today.AddDays(-1)),
        Contract(4, 13, "CHINH_THUC", today.AddDays(1), today.AddYears(1)),
        Contract(5, 14, "CHINH_THUC", today.AddMonths(-2), today.AddYears(1)),
        Contract(6, 15, "THU_VIEC", today.AddDays(-10), today.AddDays(20)));
    await db.SaveChangesAsync();
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var result = await controller.Index(null, null) as ViewResult;
    var model = result?.Model as HopDongIndexViewModel;
    Require(model != null && model.CreateContractForm.EmployeeList.Select(x => x.Id).Order().SequenceEqual([11, 12]),
        "Suggestions must include active and expired probation without a following official contract only");
});
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
// These regressions exercise the real create actions against isolated in-memory data.
foreach (var (type, end) in new[] {
    (ContractTypes.XAC_DINH_THOI_HAN_1_NAM, today.AddYears(5)),
    (ContractTypes.XAC_DINH_THOI_HAN_3_NAM, today.AddYears(1)),
    (ContractTypes.KHONG_XAC_DINH_THOI_HAN, today.AddYears(1)) })
await Check("Reject submitted end inconsistent with " + type, async () =>
{
    await using var db = await Database(); var model = Form(today);
    model.Contract.LoaiHopDong = type; model.Contract.NgayKetThuc = end;
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey("Contract.NgayKetThuc") && !await db.HopDongs.AnyAsync(), "Mismatched end must not save");
});
foreach (var name in new[] { "An", "Nguyễn 123", "123 An", new string('A', 101) + " An", "Nguyễn " + new string('A', 51) })
await Check("Reject invalid new employee name " + name.Length, async () =>
{
    await using var db = await Database(); var model = Form(today);
    model.EmployeeId = null; model.EmployeeName = name; model.TrinhDo = "Đại học";
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey("EmployeeName") && !await db.HopDongs.AnyAsync(), "Invalid name must not create pending employee");
    Require(await db.NhanViens.CountAsync() == 1, "Rejected employee must not be persisted");
});
foreach (var field in new[] { "number", "email" })
await Check("Reject overlong contract " + field, async () =>
{
    await using var db = await Database(); var model = Form(today);
    if (field == "number") model.Contract.SoHopDong = new string('X', 51);
    else model.PersonalEmail = new string('a', 90) + "@example.test";
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.ContainsKey(field == "number" ? "Contract.SoHopDong" : "PersonalEmail") && !await db.HopDongs.AnyAsync(), "Length must be rejected before save");
});
await Check("Invalid contract returns populated form and all field errors", async () =>
{
    await using var db = await Database(); var model = Form(today);
    model.Contract.LuongCoBan = 1; model.PersonalEmail = "bad-email";
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    var result = await controller.Create(model) as ViewResult;
    Require(result?.ViewName == "Index", "Invalid save must render form instead of redirecting");
    var page = result!.Model as HopDongIndexViewModel;
    Require(page?.CreateContractForm.Contract.SoHopDong == "HDLD-TEST" && page.CreateContractForm.PersonalEmail == "bad-email", "Keep submitted values");
    Require(page!.CreateContractForm.PositionOptions.Any(x => x.Value == "21"), "Repopulate matching positions");
    Require(controller.ModelState.ContainsKey("PersonalEmail") && controller.ModelState.ContainsKey("Contract.LuongCoBan"), "Keep every error");
    Require(controller.ViewData["OpenCreateContractModal"] is true, "Reopen create dialog");
});
foreach (var (birth, dependents, error) in new[] {
    (new DateOnly(2021,1,1), 0, "Employee.NgaySinh"),
    (new DateOnly(2020,1,1), 0, "Employee.NgaySinh"),
    (new DateOnly(1990,1,1), -1, "Employee.SoNguoiPhuThuoc"),
    (new DateOnly(1990,1,1), 21, "Employee.SoNguoiPhuThuoc") })
await Check("Reject inconsistent completion " + error + birth + dependents, async () =>
{
    await using var db = await Database();
    var person = await db.NhanViens.SingleAsync(); person.IsProfileComplete = false;
    db.HopDongs.Add(Form(today).Contract); await db.SaveChangesAsync();
    var contractId = await db.HopDongs.Select(x => x.MaHopDong).SingleAsync();
    var controller = Attach(new NhanVienController(db, NullLogger<NhanVienController>.Instance));
    await controller.Create(new() { ContractId = contractId, Employee = new() { NgaySinh = birth, GioiTinh = "NAM", DienThoai = "0912345678", DiaChi = "Hà Nội", SoNguoiPhuThuoc = dependents } });
    Require(controller.ModelState.ContainsKey(error), "Invalid completion must report field error");
    Require(!(await db.NhanViens.AsNoTracking().SingleAsync()).IsProfileComplete, "Invalid profile must stay incomplete");
});
foreach (var dependents in new[] { 0, 20 })
await Check("Valid completion at dependent boundary " + dependents, async () =>
{
    await using var db = await Database(); var person = await db.NhanViens.SingleAsync(); person.IsProfileComplete = false;
    db.HopDongs.Add(Form(today).Contract); await db.SaveChangesAsync();
    var contractId = await db.HopDongs.Select(x => x.MaHopDong).SingleAsync();
    var controller = Attach(new NhanVienController(db, NullLogger<NhanVienController>.Instance));
    await controller.Create(new() { ContractId = contractId, Employee = new() { NgaySinh = new(1990,1,1), GioiTinh = "NU", DienThoai = "+84912345678", DiaChi = "Hà Nội", SoNguoiPhuThuoc = dependents } });
    var saved = await db.NhanViens.AsNoTracking().SingleAsync();
    Require(saved.IsProfileComplete && saved.SoNguoiPhuThuoc == dependents, "Valid completion must persist unchanged");
});
foreach (var name in new[] { "Nguyễn Văn An", "Jean-Pierre D’Arcy", new string('A', 100) + " " + new string('B', 50) })
await Check("Valid pending employee including long email source " + name.Length, async () =>
{
    await using var db = await Database(); var model = Form(today);
    model.EmployeeId = null; model.EmployeeName = name; model.TrinhDo = "Đại học";
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.IsValid && await db.HopDongs.CountAsync() == 1, "Valid name must create contract");
    var saved = await db.NhanViens.SingleAsync(x => x.MaNhanVien != 10);
    Require(saved.EmailCongVu.Length <= 100 && !saved.IsProfileComplete, "Generated email must fit DB column; profile stays pending");
});
await Check("Blank contract number is generated despite implicit required binding error", async () =>
{
    await using var db = await Database(); var model = Form(today); model.Contract.SoHopDong = "";
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    controller.ModelState.AddModelError("Contract.SoHopDong", "The SoHopDong field is required.");
    await controller.Create(model);
    Require(controller.ModelState.IsValid && (await db.HopDongs.SingleAsync()).SoHopDong.Length > 0, "Blank auto-number field must save with generated value");
});
await Check("Leap-day official duration agrees with browser calculation", async () =>
{
    await using var db = await Database(); var model = Form(new DateOnly(2028,2,29)); model.Contract.NgayKetThuc = new DateOnly(2029,2,28);
    var controller = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance));
    await controller.Create(model);
    Require(controller.ModelState.IsValid && (await db.HopDongs.SingleAsync()).NgayKetThuc == new DateOnly(2029,2,28), "Leap day must end on Feb 28 next year");
});
await Check("First signature reuses direct profile and finalizes proposed organization", async () =>
{
    await using var db = await Database(); await SeedPending(db);
    var person = await db.NhanViens.SingleAsync(x => x.DienThoai == "0987654321");
    var model = Form(today.AddDays(1)); model.EmployeeId = person.MaNhanVien; model.TrinhDo = "Đại học";
    var contracts = Attach(new HopDongController(db, NullLogger<HopDongController>.Instance)); await contracts.Create(model);
    Require(contracts.ModelState.IsValid, "First signature must accept confirmed department/position");
    var saved = await db.NhanViens.AsNoTracking().Include(x=>x.HopDongs).SingleAsync(x=>x.MaNhanVien==person.MaNhanVien);
    Require(await db.NhanViens.CountAsync()==2 && saved.HopDongs.Count==1, "Reuse exactly one profile");
    Require(saved.NgayVaoLam==today.AddDays(1) && saved.MaChucVu==21 && saved.MaPhongBan==41, "First contract initializes joining date and confirmed organization");
    Require(saved.IsProfileComplete && !ContractAttendancePolicy.HasValidContractOn(saved,today) && ContractAttendancePolicy.HasValidContractOn(saved,today.AddDays(1)), "Future signed contract must wait until its start");
});
await Check("Pending profile edit cannot activate employee or set joining date", async () =>
{
    await using var db = await Database(); await SeedPending(db);
    var person = await db.NhanViens.AsNoTracking().SingleAsync(x => x.DienThoai == "0987654321");
    person.TrangThai="DANG_LAM"; person.NgayVaoLam=today; person.MaPhongBan=41; person.MaChucVu=21;
    var editor=Attach(new NhanVienController(db, NullLogger<NhanVienController>.Instance));
    await editor.Edit(person.MaNhanVien, new() { Employee=person });
    var saved=await db.NhanViens.AsNoTracking().SingleAsync(x=>x.MaNhanVien==person.MaNhanVien);
    Require(editor.ModelState.IsValid && saved.NgayVaoLam==null && saved.TrangThai=="CHO_HOP_DONG" && saved.MaChucVu==21, "Pending edits may update proposed organization but cannot activate");
});

await Check("Stale pending edit cannot undo concurrent first-contract activation", async () =>
{
    await using var db = await Database();
    await SeedPending(db);
    var id = await db.NhanViens.Where(x=>x.DienThoai=="0987654321").Select(x=>x.MaNhanVien).SingleAsync();
    var options = (DbContextOptions<AppDbContext>)Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>(db);
    await using var stale = new AppDbContext(options);
    var old = await stale.NhanViens.SingleAsync(x=>x.MaNhanVien==id);
    var signature = Form(today); signature.EmployeeId=id; signature.TrinhDo="Đại học";
    var contracts=Attach(new HopDongController(db, NullLogger<HopDongController>.Instance)); await contracts.Create(signature);
    Require(contracts.ModelState.IsValid,"Concurrent signature setup must succeed");
    old.DiaChi="Stale pending form address"; old.TrangThai="CHO_HOP_DONG"; old.NgayVaoLam=null;
    var rejected=false;
    try { await stale.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { rejected=true; }
    Require(rejected,"A stale pending update must not write after joining date changes");
    await using var verify=new AppDbContext(options);
    var current=await verify.NhanViens.SingleAsync(x=>x.MaNhanVien==id);
    Require(current.NgayVaoLam==today && current.TrangThai=="DANG_LAM" && current.MaChucVu==21,"Preserve first-contract state and organization");
});
await Check("Personnel drilldowns exclude pending and incomplete intake consistently", async () =>
{
    await using var db = await Database();
    var complete = await db.NhanViens.SingleAsync(); complete.NgayVaoLam = new DateOnly(2026,10,1);
    db.NhanViens.Add(new NhanVien { MaNhanVien=20,Ho="Contract",Ten="Incomplete",EmailCongVu="incomplete@example.test",IsProfileComplete=false,NgayVaoLam=new(2026,10,1),TrangThai="DANG_LAM" });
    db.NhanViens.Add(new NhanVien { MaNhanVien=21,Ho="Waiting",Ten="Signature",EmailCongVu="waiting@example.test",NgayVaoLam=null,TrangThai="CHO_HOP_DONG" });
    await db.SaveChangesAsync();
    using var cache=new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var reports=new OvertimeHotel.HRM.WebAdmin.Services.BaoCaoReportService(db,cache,NullLogger<OvertimeHotel.HRM.WebAdmin.Services.BaoCaoReportService>.Instance);
    foreach(var type in new[] { "cuoi-ky", "tuyen-moi", "dang-lam", "thong-tin-nhan-su" })
    {
        var result=await reports.BuildChiTietAsync(new ReportPolicy.BaoCaoScope(true,true,null),"HR",type,"THANG",2026,10,null,null);
        Require(result.DanhSach.Count==1 && result.DanhSach.Single().MaDinhDanh=="NV-10", "Drilldown must include only the completed employed profile: "+type);
    }
    complete.TrangThai="DA_THOI_VIEC"; complete.NgayThoiViec=new(2026,10,20);
    var incomplete=await db.NhanViens.SingleAsync(x=>x.MaNhanVien==20);
    incomplete.TrangThai="DA_THOI_VIEC"; incomplete.NgayThoiViec=new(2026,10,15);
    await db.SaveChangesAsync();
    foreach(var type in new[] { "thoi-viec", "da-thoi-viec" })
    {
        var result=await reports.BuildChiTietAsync(new ReportPolicy.BaoCaoScope(true,true,null),"HR",type,"THANG",2026,10,null,null);
        Require(result.DanhSach.Count==1 && result.DanhSach.Single().MaDinhDanh=="NV-10", "Former-employee drilldown must exclude incomplete profiles: "+type);
    }
});
await Check("Unsigned intake is never converted to a fake resigned employee by hide action", async () =>
{
    await using var db=await Database(); await SeedPending(db); var hr=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance));
    var id=await db.NhanViens.Where(x=>x.DienThoai=="0987654321").Select(x=>x.MaNhanVien).SingleAsync();
    await hr.Delete(id);
    var saved=await db.NhanViens.AsNoTracking().SingleAsync(x=>x.MaNhanVien==id);
    Require(saved.TrangThai=="CHO_HOP_DONG" && saved.NgayVaoLam==null && saved.NgayThoiViec==null,"Unsigned intake has no employment history to terminate");
});
foreach (var (birth, allowed) in new[] { (today.AddYears(-18), true), (today.AddYears(-18).AddDays(1), false), (today.AddYears(-18).AddDays(-1), true) })
await Check("Contract completion requires age 18 " + birth, async () =>
{
    await using var db=await Database(); var person=await db.NhanViens.SingleAsync();person.IsProfileComplete=false;
    db.HopDongs.Add(Form(today).Contract); await db.SaveChangesAsync();
    var contractId=await db.HopDongs.Select(x=>x.MaHopDong).SingleAsync();
    var model=new NhanVienFormViewModel { ContractId=contractId, Employee=new() { NgaySinh=birth,GioiTinh="NAM",DienThoai="0912345678",DiaChi="Hà Nội",EmailCaNhan="an@example.test" } };
    var c=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance)); await c.Create(model);
    Require(c.ModelState.IsValid==allowed && (await db.NhanViens.AsNoTracking().SingleAsync()).IsProfileComplete==allowed, "Age boundary must be enforced");
});
await Check("AJAX hide rejects unsigned employee as JSON", async () =>
{
    await using var db=await Database(); await SeedPending(db); var c=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance));
    var id=await db.NhanViens.Where(x=>x.DienThoai=="0987654321").Select(x=>x.MaNhanVien).SingleAsync();
    c.Request.Headers.XRequestedWith="XMLHttpRequest";
    var result=await c.Delete(id) as JsonResult;
    Require(result!=null && !JsonSerializer.SerializeToElement(result.Value).GetProperty("success").GetBoolean(), "Pending rejection must be JSON");
    var employee=await db.NhanViens.AsNoTracking().SingleAsync(x=>x.MaNhanVien==id);
    Require(employee.TrangThai=="CHO_HOP_DONG" && employee.NgayVaoLam==null, "Pending profile must not be marked resigned");
});
await Check("AJAX hide succeeds for active employee", async () =>
{
    await using var db=await Database(); var c=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance));
    c.Request.Headers.XRequestedWith="XMLHttpRequest";
    var result=await c.Delete(10) as JsonResult;
    Require(result!=null && JsonSerializer.SerializeToElement(result.Value).GetProperty("success").GetBoolean(), "Confirmed AJAX hide must return success");
    Require((await db.NhanViens.AsNoTracking().SingleAsync()).TrangThai=="DA_THOI_VIEC", "Hide must update existing status");
});
await Check("Direct profile creation is no longer accepted", async () =>
{
    await using var db=await Database(); var c=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance));
    var result=await c.Create(Intake());
    Require(result is BadRequestObjectResult && await db.NhanViens.CountAsync()==1,"Direct mode must not create a profile");
});
await Check("Contract completion requires and stores personal email", async () =>
{
    await using var db=await Database(); var person=await db.NhanViens.SingleAsync(); person.IsProfileComplete=false; person.EmailCaNhan=null;
    db.HopDongs.Add(Form(today).Contract);await db.SaveChangesAsync(); var id=await db.HopDongs.Select(x=>x.MaHopDong).SingleAsync();
    var model=new NhanVienFormViewModel { ContractId=id, Employee=new() { NgaySinh=new(1990,1,1),GioiTinh="NAM",DienThoai="0912345678",DiaChi="Hà Nội" } };
    var c=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance)); await c.Create(model);
    Require(c.ModelState.ContainsKey("Employee.EmailCaNhan") && !(await db.NhanViens.AsNoTracking().SingleAsync()).IsProfileComplete,"Missing personal email must be rejected");
    model.Employee.EmailCaNhan="nhanvien@example.test"; var retry=Attach(new NhanVienController(db,NullLogger<NhanVienController>.Instance)); await retry.Create(model);
    var saved=await db.NhanViens.AsNoTracking().SingleAsync(); Require(saved.IsProfileComplete && saved.EmailCaNhan=="nhanvien@example.test","Entered personal email must persist");
});
await Check("New contract requires personal email", async () =>
{
    await using var db=await Database();var model=Form(today);model.EmployeeId=null;model.EmployeeName="Lê Bình";model.TrinhDo="Đại học";model.PersonalEmail=null;
    var c=Attach(new HopDongController(db,NullLogger<HopDongController>.Instance));await c.Create(model);
    Require(c.ModelState.ContainsKey("PersonalEmail") && !await db.HopDongs.AnyAsync(),"Missing personal email must prevent contract creation");
});
Console.WriteLine($"Contract rules: {passed} passed, {failures.Count} failed.");
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

static NhanVienFormViewModel Intake()
{
    var model = new NhanVienFormViewModel { Employee = new() { Ho="Lê",Ten="Bình",NgaySinh=new(1992,1,1),GioiTinh="NAM",DienThoai="0987654321",DiaChi="Hà Nội",TrinhDo="Đại học",MaPhongBan=2,MaChucVu=25 } };
    model.EntryMode = "DIRECT";
    return model;
}
static async Task SeedPending(AppDbContext db)
{
    var person=Intake().Employee; person.MaNhanVien=0; person.TrangThai="CHO_HOP_DONG"; person.NgayVaoLam=null;
    person.EmailCongVu="pending@example.test"; person.EmailCaNhan="pending-personal@example.test"; person.IsProfileComplete=true;
    db.NhanViens.Add(person); await db.SaveChangesAsync();
}
static HopDongFormViewModel Form(DateOnly start) => new()
{
    EmployeeId = 10, EmployeeName = "Nguyễn An", DepartmentId = 41, PositionId = 21, PersonalEmail = "an@example.test",
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
    db.NhanViens.Add(new() { MaNhanVien = 10, MaPhongBan = 41, MaChucVu = 21, Ho = "Nguyễn", Ten = "An", EmailCongVu = "an@example.test", EmailCaNhan = "an@example.test",
        NgaySinh = new(1990, 1, 1), GioiTinh = "NAM", DienThoai = "0912345678", DiaChi = "Hà Nội", TrinhDo = "Đại học", NgayVaoLam = new(2020, 1, 1) });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
}
sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
