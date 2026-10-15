using System.Security.Claims;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;

// Only run-prefixed fixtures are created or removed. Supabase calls use a fake.
var connection = Environment.GetEnvironmentVariable("ACCOUNT_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connection))
{
    foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
    {
        var path = Path.Combine("src", "OvertimeHotel.HRM.WebAdmin", file);
        if (!File.Exists(path)) continue;
        using var config = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (config.RootElement.TryGetProperty("ConnectionStrings", out var strings) && strings.TryGetProperty("DefaultConnection", out var value)) connection = value.GetString();
    }
}
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set ACCOUNT_TEST_CONNECTION or run from the repository root.");
var settings = new NpgsqlConnectionStringBuilder(connection) { IncludeErrorDetail = false, Timeout = 15 };
var fixture = new Fixture(settings.ConnectionString);
if (args.Contains("--inspect"))
{
    await using var db = fixture.Open();
    await db.Database.OpenConnectionAsync();
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND tablename='yeu_cau_tai_khoan' UNION ALL SELECT pg_get_functiondef(t.tgfoid) FROM pg_trigger t WHERE t.tgrelid='public.yeu_cau_tai_khoan'::regclass AND NOT t.tgisinternal";
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) Console.WriteLine(reader.GetString(0));
    return 0;
}
var passed = 0;
var failed = 0;
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
}
try
{
    await fixture.Initialize();
    await Check("Direct account POST redirects to the approved-request workflow without creating an account", async () =>
    {
        var worker = await fixture.Worker();
        await using var db = fixture.Open();
        var service = new Provisioner(false);
        var controller = new UserAccountsController(db, NullLogger<UserAccountsController>.Instance, service);
        fixture.Authenticate(controller);
        var result = await controller.Create(new CreateAccountViewModel { EmployeeId = worker.MaNhanVien, Username = worker.Ten, RoleId = fixture.EmployeeRole, Password = "Test-only@123", ConfirmPassword = "Test-only@123", IsActive = true });
        Require(result is RedirectToActionResult { ControllerName: "YeuCauTaiKhoan", ActionName: "Create" }, "POST bypasses the queue.");
        Require(!await db.TaiKhoans.AnyAsync(a => a.MaNhanVien == worker.MaNhanVien) && service.Calls == 0, "Direct creation persisted an account.");
    });
    await Check("Missing Auth configuration cannot mark a request granted", async () =>
    {
        var request = await fixture.Request();
        await using var db = fixture.Open();
        var controller = fixture.Controller(db, new Provisioner(false));
        Require(Status(await controller.Complete(request.MaYeuCau)) == 502, "Unconfigured Auth must fail rather than grant a local-only account.");
        await fixture.RequirePending(request);
    });
    await Check("Auth failure restores the pending request without a local account", async () =>
    {
        var request = await fixture.Request();
        await using var db = fixture.Open();
        var controller = fixture.Controller(db, new Provisioner(true, fail: true));
        Require(Status(await controller.Complete(request.MaYeuCau)) == 502, "Auth failure must return an error.");
        await fixture.RequirePending(request);
    });
    await Check("Local transaction failure cannot create an orphan Auth user", async () =>
    {
        var request = await fixture.Request();
        await using var db = fixture.Open(new FailAccountInsert());
        var service = new Provisioner(true);
        Require(Status(await fixture.Controller(db, service).Complete(request.MaYeuCau)) == 500, "Injected failure was not reported.");
        Require(service.Calls == 1 && service.DeletedUserIds.SequenceEqual(new[] { service.CreatedUserId }), "A failed local grant must remove only its newly created Auth identity.");
        await fixture.RequirePending(request);
    });
    await Check("Successful provisioning persists both the account grant and Auth audit once", async () =>
    {
        var request = await fixture.Request();
        await using var db = fixture.Open();
        var service = new Provisioner(true);
        var controller = fixture.Controller(db, service);
        Require(Status(await controller.Complete(request.MaYeuCau)) == 200 && service.Calls == 1, "Provisioning did not succeed.");
        await using var check = fixture.Open();
        var grant = await check.YeuCauTaiKhoans.AsNoTracking().SingleAsync(r => r.MaYeuCau == request.MaYeuCau);
        Require(grant.TrangThai == "DA_CAP" && grant.MaTaiKhoan != null, "Grant did not persist.");
        Require(await check.NhatKyQuanTris.CountAsync(a => a.MaTaiKhoanBiTacDong == grant.MaTaiKhoan && a.HanhDong == "CREATE_ACCOUNT_AUTH") == 1, "Auth audit is missing or duplicated.");
        Require(Status(await controller.Complete(request.MaYeuCau)) == 200 && service.Calls == 1, "Retry must not reprovision Auth.");
    });
    await Check("An ambiguous commit response never deletes a committed Auth identity", async () =>
    {
        var request = await fixture.Request();
        await using var db = fixture.Open(new LostCommitResponse());
        var service = new Provisioner(true);
        Require(Status(await fixture.Controller(db, service).Complete(request.MaYeuCau)) == 200, "Committed grant was incorrectly reported as failed.");
        Require(service.Calls == 1 && service.DeletedUserIds.Count == 0, "Committed Auth identity was removed.");
        await using var check = fixture.Open();
        Require(await check.YeuCauTaiKhoans.AnyAsync(r => r.MaYeuCau == request.MaYeuCau && r.TrangThai == "DA_CAP"), "Committed grant disappeared.");
    });
    await Check("Concurrent submissions for one employee cannot create two pending requests", async () =>
    {
        var worker = await fixture.Worker();
        async Task<IActionResult> Submit(string suffix)
        {
            await using var db = fixture.Open();
            return await fixture.Controller(db, new Provisioner(false)).Create(new CreateYeuCauTaiKhoanViewModel
            {
                EmployeeId = worker.MaNhanVien, ContractId = worker.HopDongs.Single().MaHopDong,
                RoleId = fixture.EmployeeRole, Username = worker.Ten + suffix
            });
        }
        var results = await Task.WhenAll(Submit("a"), Submit("b"));
        Require(results.Count(r => r is RedirectToActionResult) == 1 && results.Count(r => r is ViewResult) == 1, "Both duplicate submissions were accepted.");
        await using var check = fixture.Open();
        Require(await check.YeuCauTaiKhoans.CountAsync(r => r.MaNhanVien == worker.MaNhanVien) == 1, "Duplicate pending requests persisted.");
    });
}
catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL fixture setup: {ex.GetType().Name}"); }
finally
{
    try { await fixture.Cleanup(); Console.WriteLine("PASS owned account fixture cleanup"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL owned cleanup: {ex.GetType().Name}; prefix {fixture.Prefix}"); }
}
Console.WriteLine($"Account queue integration: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

static int Status(IActionResult result) => result switch { ObjectResult r => r.StatusCode ?? 200, JsonResult r => r.StatusCode ?? 200, StatusCodeResult r => r.StatusCode, _ => 302 };
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

sealed class Provisioner(bool configured, bool fail = false) : ISupabaseUserProvisioningService
{
    public bool IsConfigured => configured;
    public int Calls { get; private set; }
    public string CreatedUserId { get; } = Guid.NewGuid().ToString();
    public List<string> DeletedUserIds { get; } = [];
    public Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        DeletedUserIds.Add(id); return Task.FromResult(true);
    }
    public Task<SupabaseUserProvisionResult> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(!configured ? SupabaseUserProvisionResult.Failed(SupabaseUserProvisionError.NotConfigured, "Auth is not configured.")
            : fail ? SupabaseUserProvisionResult.Failed(SupabaseUserProvisionError.NetworkFailure, "Auth failed.")
            : SupabaseUserProvisionResult.Succeeded(CreatedUserId));
    }
}
sealed class FailAccountInsert : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<TaiKhoan>().Any(e => e.State == EntityState.Added)) throw new DbUpdateException("Injected account insert failure.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
sealed class LostCommitResponse : DbTransactionInterceptor
{
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Injected lost commit response.");
}
sealed class Fixture(string connection)
{
    public string Prefix { get; } = "aq-" + Guid.NewGuid().ToString("N");
    public int EmployeeRole { get; private set; }
    private NhanVien admin = null!;
    private NhanVien hr = null!;
    private int department;
    private int position;
    private int sequence;
    public AppDbContext Open(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection);
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new AppDbContext(options.Options);
    }
    public async Task Initialize()
    {
        await using var db = Open();
        position = await db.ChucVus.Select(p => p.MaChucVu).FirstAsync();
        var unit = new PhongBan { TenPhongBan = Prefix };
        db.PhongBans.Add(unit); await db.SaveChangesAsync(); department = unit.MaPhongBan;
        EmployeeRole = await db.VaiTros.Where(r => r.TenVaiTro == "Employee").Select(r => r.MaVaiTro).SingleAsync();
        admin = await Worker("Admin"); hr = await Worker("HR");
    }
    public async Task<NhanVien> Worker(string? role = null)
    {
        await using var db = Open();
        var name = Prefix + "-" + ++sequence;
        var person = new NhanVien { Ho = "Account fixture", Ten = name, MaPhongBan = department, MaChucVu = position, NgaySinh = new DateOnly(1990, 1, 1), NgayVaoLam = new DateOnly(2010, 1, 1), EmailCongVu = name + "@example.invalid", DienThoai = "0900000000", TrinhDo = "Test" };
        person.HopDongs.Add(new HopDong { SoHopDong = name, LoaiHopDong = "KHONG_XAC_DINH_THOI_HAN", NgayBatDau = new DateOnly(2010, 1, 1), LuongCoBan = 1000000, HinhThucHopDong = "CHINH_THUC" });
        if (role != null) person.TaiKhoan = new TaiKhoan { TenDangNhap = name, MaVaiTro = await db.VaiTros.Where(r => r.TenVaiTro == role).Select(r => r.MaVaiTro).SingleAsync(), MatKhauBam = "fixture-login-disabled" };
        db.NhanViens.Add(person); await db.SaveChangesAsync(); return person;
    }
    public async Task<YeuCauTaiKhoan> Request()
    {
        var worker = await Worker();
        await using var db = Open();
        var request = new YeuCauTaiKhoan { MaNhanVien = worker.MaNhanVien, MaHopDong = worker.HopDongs.Single().MaHopDong, TenDangNhap = worker.Ten, MaVaiTro = EmployeeRole, MaNguoiGui = hr.MaNhanVien, MaNguoiXuLy = admin.TaiKhoan!.MaTaiKhoan, TrangThai = "DANG_XU_LY" };
        db.YeuCauTaiKhoans.Add(request); await db.SaveChangesAsync(); return request;
    }
    public YeuCauTaiKhoanController Controller(AppDbContext db, ISupabaseUserProvisioningService service)
    {
        var controller = new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, service);
        Authenticate(controller); return controller;
    }
    public void Authenticate(Controller controller)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, admin.TaiKhoan!.MaTaiKhoan.ToString()), new Claim("EmployeeId", admin.MaNhanVien.ToString()), new Claim(ClaimTypes.Role, "Admin") }, "Fixture")) };
        context.Request.Headers.XRequestedWith = "XMLHttpRequest";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new MemoryTempData());
    }
    public async Task RequirePending(YeuCauTaiKhoan request)
    {
        await using var db = Open();
        var actual = await db.YeuCauTaiKhoans.AsNoTracking().SingleAsync(r => r.MaYeuCau == request.MaYeuCau);
        if (actual.TrangThai != "DANG_XU_LY" || actual.MaTaiKhoan != null || await db.TaiKhoans.AnyAsync(a => a.MaNhanVien == request.MaNhanVien)) throw new InvalidOperationException("Failed provisioning left a grant or local account.");
    }
    public async Task Cleanup()
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employees = await db.NhanViens.Where(n => n.EmailCongVu.StartsWith(Prefix + "-")).Select(n => n.MaNhanVien).ToArrayAsync();
        var accounts = await db.TaiKhoans.Where(a => employees.Contains(a.MaNhanVien)).Select(a => a.MaTaiKhoan).ToArrayAsync();
        await db.NhatKyQuanTris.Where(a => a.MaTaiKhoanThucHien != null && accounts.Contains(a.MaTaiKhoanThucHien.Value) || a.MaTaiKhoanBiTacDong != null && accounts.Contains(a.MaTaiKhoanBiTacDong.Value)).ExecuteDeleteAsync();
        await db.YeuCauTaiKhoans.Where(r => employees.Contains(r.MaNhanVien)).ExecuteDeleteAsync();
        await db.TaiKhoans.Where(a => employees.Contains(a.MaNhanVien)).ExecuteDeleteAsync();
        await db.HopDongs.Where(h => employees.Contains(h.MaNhanVien)).ExecuteDeleteAsync();
        await db.NhanViens.Where(n => employees.Contains(n.MaNhanVien)).ExecuteDeleteAsync();
        await db.PhongBans.Where(p => p.TenPhongBan == Prefix).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
