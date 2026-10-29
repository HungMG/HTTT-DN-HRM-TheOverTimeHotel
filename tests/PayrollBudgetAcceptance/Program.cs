using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

// Explicit opt-in, fresh local database only. Never reads appsettings or live credentials.
var connection = Environment.GetEnvironmentVariable("HRM_ACCEPTANCE_DB")
    ?? throw new InvalidOperationException("Set HRM_ACCEPTANCE_DB to a fresh local test database.");
var cs = new NpgsqlConnectionStringBuilder(connection);
if (cs.Host != "127.0.0.1" || cs.Port != 55439 || !cs.Database!.StartsWith("hrm_budget_acceptance_"))
    throw new InvalidOperationException("Acceptance requires 127.0.0.1:55439/hrm_budget_acceptance_*.");
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
await using (var conn = new NpgsqlConnection(connection))
{
    await conn.OpenAsync();
    await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_tables WHERE schemaname='public'",conn);
    if ((long)(await command.ExecuteScalarAsync())! != 0)
        throw new InvalidOperationException("Refusing to modify a nonempty database. Create a fresh acceptance database.");
}
AppDbContext Open() => new(options);
using var cache = new MemoryCache(new MemoryCacheOptions());
var failed = 0; var passed = 0;
void Require(bool value, string message) { if (!value) throw new Exception(message); }
async Task Check(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
async Task Reject(Func<Task> action)
{
    try { await action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Expected stale/invalid operation to be rejected.");
}
async Task<NganSachLuong> Row(int month)
{
    await using var db = Open();
    return await db.NganSachLuongs.AsNoTracking().SingleAsync(b => b.Nam == 2026 && b.Thang == month);
}
async Task Save(int month, decimal amount = 10000000m)
{
    await using var db = Open();
    await new PayrollBudgetService(db, cache).SaveAsync(new() { Nam=2026, Thang=month, MaPhongBan=701, SoTien=amount },7101);
}
async Task Transition(NganSachLuong row, string action, int actor)
{
    await using var db = Open();
    await new PayrollBudgetService(db,cache).TransitionAsync(row.MaNganSach,action,actor,row.Version);
}

// Ignore only the new table so the real SQL migration, rather than EnsureCreated,
// is responsible for its schema, constraints, indexes and RLS.
await using (var db = new BeforeBudgetContext(options))
{
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("DO $$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF; IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $$;");
    var migration = await File.ReadAllTextAsync("src/OvertimeHotel.HRM.Data/migrations/20261028_approved_payroll_budget.sql");
    await db.Database.ExecuteSqlRawAsync(migration);
    await db.Database.ExecuteSqlRawAsync(migration); // Must also be safe to rerun.
}
await using (var db = Open())
{
    db.PhongBans.Add(new() {MaPhongBan=701,TenPhongBan="Acceptance HR"});
    db.VaiTros.Add(new() {MaVaiTro=701,TenVaiTro="Admin"});
    foreach (var id in new[] {7101,7102})
    {
        db.NhanViens.Add(new() {MaNhanVien=id,MaPhongBan=701,Ho="Acceptance",Ten=id.ToString(),
            EmailCongVu=$"acceptance{id}@example.invalid",DienThoai=id.ToString(),NgayVaoLam=new(2020,1,1)});
        db.TaiKhoans.Add(new() {MaTaiKhoan=id+1000,MaNhanVien=id,MaVaiTro=701,
            TenDangNhap=$"budget{id}",MatKhauBam=PasswordHasher.Hash("Acceptance-local-only-2026!")});
    }
    db.KyLuongs.Add(new() {MaKyLuong=701,Nam=2026,Thang=10,TuNgay=new(2026,10,1),DenNgay=new(2026,10,31)});
    db.PhieuLuongs.Add(new() {MaNhanVien=7101,MaKyLuong=701,TienLuongCong=8000000m,TienTangCa=500000m,
        TongPhuCap=300000m,TongThuong=200000m,ThucNhan=8100000m,TrangThai="DA_DUYET"});
    await db.SaveChangesAsync();
}
await Check("Migration creates RLS and blocks client roles", async () =>
{
    await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT relrowsecurity AND NOT has_table_privilege('anon',oid,'SELECT') AND NOT has_table_privilege('authenticated',oid,'INSERT') FROM pg_class WHERE oid='public.ngan_sach_luong'::regclass",conn);
    Require((bool)(await cmd.ExecuteScalarAsync())!,"RLS or client permissions missing");
});
await Check("Controller records employee ID when account ID differs", async () =>
{
    await using var db = Open();
    var http = new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier,"8101"),new Claim("EmployeeId","7101"),new Claim(ClaimTypes.Role,"Admin")],"Test"))};
    var controller = new PayrollBudgetController(db,new(db,cache)) {ControllerContext=new() {HttpContext=http},
        TempData=new TempDataDictionary(http,new TestTempDataProvider())};
    await controller.Save(new() {Nam=2026,Thang=9,MaPhongBan=701,SoTien=10000000m});
    Require(await db.NganSachLuongs.AnyAsync(b=>b.Thang==9 && b.NguoiTao==7101),"Budget was not saved with the authenticated employee");
});
await Check("Controller refuses account without employee claim", async () =>
{
    await using var db=Open();
    var controller=new PayrollBudgetController(db,new(db,cache)) {ControllerContext=new() {HttpContext=new DefaultHttpContext {
        User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"7101"),new Claim(ClaimTypes.Role,"Admin")],"Test"))}}};
    Require(await controller.Index(2026) is ForbidResult,"Missing employee claim used account ID");
});
await Check("Save edit submit independent approval lock persists", async () =>
{
    await Save(10); var row=await Row(10);
    await using(var db=Open()) await new PayrollBudgetService(db,cache).SaveAsync(new() {
        Id=row.MaNganSach,Version=row.Version,Nam=2026,Thang=10,MaPhongBan=701,SoTien=12000000m},7101);
    row=await Row(10); Require(row.Version==2 && row.SoTienNganSach==12000000m,"Edit did not persist");
    await Transition(row,"submit",7101); row=await Row(10);
    await Reject(()=>Transition(row,"approve",7101));
    await Transition(row,"approve",7102); row=await Row(10);
    Require(row.NguoiDuyet==7102 && row.NgayDuyet!=null,"Approval metadata missing");
    await Transition(row,"lock",7102); row=await Row(10);
    Require(row.TrangThai=="KHOA" && row.Version==5,"Lock did not persist");
    await using var edit=Open();
    await Reject(()=>new PayrollBudgetService(edit,cache).SaveAsync(new() {Id=row.MaNganSach,Version=row.Version,
        Nam=2026,Thang=10,MaPhongBan=701,SoTien=1m},7101));
});
await Check("Concurrent edits accept exactly one displayed version", async () =>
{
    await Save(11); var row=await Row(11);
    async Task<bool> Edit(int actor,decimal amount)
    {
        await using var db=Open();
        try {await new PayrollBudgetService(db,cache).SaveAsync(new() {Id=row.MaNganSach,Version=row.Version,
            Nam=2026,Thang=11,MaPhongBan=701,SoTien=amount},actor);return true;}
        catch(InvalidOperationException) {return false;}
    }
    var results=await Task.WhenAll(Edit(7101,110m),Edit(7102,220m));
    Require(results.Count(x=>x)==1 && (await Row(11)).Version==2,"Concurrent edit overwrote another editor");
});
await Check("Concurrent approve reject accepts exactly one transition", async () =>
{
    await Save(12); await Transition(await Row(12),"submit",7101); var row=await Row(12);
    async Task<bool> Act(string action)
    {
        try {await Transition(row,action,7102); return true;} catch(InvalidOperationException) {return false;}
    }
    var results=await Task.WhenAll(Act("approve"),Act("reject"));
    Require(results.Count(x=>x)==1 && (await Row(12)).Version==3,"Competing transitions both succeeded");
});
await Check("Rejected budget can be revised but stale approval cannot approve the new amount",async()=>
{
    await Save(6); await Transition(await Row(6),"submit",7101);var stale=await Row(6);
    await Transition(stale,"reject",7102);var rejected=await Row(6);
    await using(var db=Open()) await new PayrollBudgetService(db,cache).SaveAsync(new() {Id=rejected.MaNganSach,
        Version=rejected.Version,Nam=2026,Thang=6,MaPhongBan=701,SoTien=16000000m},7101);
    await Transition(await Row(6),"submit",7101);
    await Reject(()=>Transition(stale,"approve",7102));
    var current=await Row(6);
    Require(current.TrangThai=="CHO_DUYET" && current.SoTienNganSach==16000000m && current.NguoiDuyet==null,
        "Stale approval changed the resubmitted budget");
});
await Check("RLS hides existing budget rows even when client role temporarily has SELECT",async()=>
{
    await using var conn=new NpgsqlConnection(connection);await conn.OpenAsync();
    await using var transaction=await conn.BeginTransactionAsync();
    await using(var grant=new NpgsqlCommand("GRANT SELECT ON public.ngan_sach_luong TO authenticated; SET LOCAL ROLE authenticated;",conn,transaction))
        await grant.ExecuteNonQueryAsync();
    await using var read=new NpgsqlCommand("SELECT count(*) FROM public.ngan_sach_luong",conn,transaction);
    Require((long)(await read.ExecuteScalarAsync())! == 0,"Client role can read private budget rows");
    await transaction.RollbackAsync();
});
await Check("Duplicate month department rejected by PostgreSQL", async () =>
{
    try {await Save(10);} catch(DbUpdateException e) when(e.InnerException is PostgresException {SqlState:"23505"}) {return;}
    throw new Exception("Duplicate budget accepted");
});
await Check("PostgreSQL rejects self approved row",async()=>
{
    await using var db=Open();
    try {await db.Database.ExecuteSqlRawAsync("INSERT INTO ngan_sach_luong(nam,thang,ma_phong_ban,so_tien_ngan_sach,trang_thai,nguoi_tao,nguoi_duyet,ngay_duyet) VALUES(2026,8,701,10,'DA_DUYET',7101,7101,now())");}
    catch(PostgresException e) when(e.SqlState=="23514") {return;}
    throw new Exception("Invalid approval bypassed database constraint");
});
await Check("Report reconciles approved budget and persisted payslip amounts",async()=>
{
    await using var db=Open();
    var report=await new BaoCaoReportService(db,cache,NullLogger<BaoCaoReportService>.Instance).BuildAsync(
        ReportPolicy.ResolveScope("Admin",null),"Admin","THANG",2026,10,null,701,"quy-luong");
    Require(report.IsDatabaseOnline,"Report silently fell back to offline data");
    Require(report.PhongBanOptions.Single(p=>p.Value=="701").Selected,"Initial department filter does not match reported scope");
    Require(report.QuyLuong.NganSachDuocDuyet==12000000m && report.QuyLuong.ThucChiGross==9000000m
        && report.QuyLuong.ThucChiGrossDaDuyet==9000000m && report.QuyLuong.ChenhLechNganSachDuyet==-3000000m,
        "Report differs from budget 12m / payslip gross 9m / variance -3m");
});
Console.WriteLine($"PayrollBudgetAcceptance: {passed} passed, {failed} failed. Local PostgreSQL only.");
return failed==0 ? 0 : 1;

sealed class BeforeBudgetContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder) {base.OnModelCreating(builder);builder.Ignore<NganSachLuong>();}
}
sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
    public void SaveTempData(HttpContext context,IDictionary<string,object> values) { }
}
