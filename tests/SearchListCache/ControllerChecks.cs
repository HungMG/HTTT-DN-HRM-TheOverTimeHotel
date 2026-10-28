using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Services;

static class ControllerChecks
{
    public static async Task Run(SearchListCache cache, IMemoryCache memory)
    {
        var guard = new NoDatabase();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=never_connected;Username=test;Password=test")
            .AddInterceptors(guard).Options);
        using var provider = new ServiceCollection().AddSingleton(cache).BuildServiceProvider();
        T Setup<T>(T controller, string role = "Admin", int? department = null) where T : Controller
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, role) };
            if (department.HasValue) claims.Add(new("DepartmentId", department.Value.ToString()));
            var http = new DefaultHttpContext { RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
            http.Request.Headers.XRequestedWith = "XMLHttpRequest";
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            controller.TempData = new TempDataDictionary(http, new EmptyTempData());
            return controller;
        }
        async Task Prime<T>(string key, T value) => _ = await cache.GetAsync(key, () => Task.FromResult(value));
        async Task Empty<T>(string key) => await Prime(key, new List<T>());
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employee = new NhanVien { MaNhanVien = 1, Ho = "Nguyễn", Ten = "An", TrangThai = "DANG_LAM", IsProfileComplete = true };

        await Empty<PhongBan>("employees:departments");
        await Empty<ChucVu>("employees:positions");
        await Prime("employees:list", new List<NhanVien> { employee });
        await Empty<HopDong>($"employees:eligible-contracts:{today:yyyyMMdd}");
        var employees = Setup(new NhanVienController(db, NullLogger<NhanVienController>.Instance));
        var found = (PartialViewResult)await employees.Index("An", null, null, null, null);
        var foundCount = ((NhanVienIndexViewModel)found.Model!).TotalFilteredEmployees;
        var missing = (PartialViewResult)await employees.Index("Bình", null, null, null, null);
        Check(foundCount == 1 && ((NhanVienIndexViewModel)missing.Model!).TotalFilteredEmployees == 0,
            "employee live-search keywords filter cached rows without SQL");

        await Prime("departments:list", new List<PhongBan> { new() { MaPhongBan = 1, TenPhongBan = "HR", MoTa = "Nhân sự" } });
        var departments = Setup(new PhongBanController(db, NullLogger<PhongBanController>.Instance));
        var deptResult = (ViewResult)await departments.Index("HR");
        Check(((List<PhongBan>)deptResult.Model!).Count == 1 && departments.ViewBag.IsDemoData != true, "department search uses cached list");
        await Empty<ChucVu>("positions:list");
        await Empty<PhongBan>("positions:departments");
        await Setup(new ChucVuController(db, NullLogger<ChucVuController>.Instance)).Index("An", null);
        await Empty<CaLamViec>("shifts:templates");
        await Setup(new CaLamViecController(db, NullLogger<CaLamViecController>.Instance)).Index("Sáng");
        await Empty<HopDong>("contracts:list");
        await Empty<NhanVien>("contracts:employees");
        await Empty<PhongBan>("contracts:departments");
        await Empty<ChucVu>("contracts:positions");
        await Prime($"contracts:next-number:{today:yyyyMMdd}", "HDLD-test");
        await Setup(new HopDongController(db, NullLogger<HopDongController>.Instance)).Index("An", null, null);
        foreach (var category in Enum.GetValues<CauHinhLuongCategory>())
        {
            await Empty<CauHinhLuongRow>($"payroll-config:{category}");
            await Setup(new CauHinhLuongController(db, memory, NullLogger<CauHinhLuongController>.Instance)).Index(category, "test");
        }
        await Empty<YeuCauDoiCa>("shift-requests:scope:");
        await Setup(new YeuCauDoiCaController(db, NullLogger<YeuCauDoiCaController>.Instance)).Index(null, "An");
        await Empty<GiaiTrinhChamCong>("attendance-explanations:scope:");
        await Setup(new GiaiTrinhChamCongController(db, NullLogger<GiaiTrinhChamCongController>.Instance)).Index(null, "An");
        await Empty<YeuCauTaiKhoan>("account-requests:process:True:sender:");
        await Setup(new YeuCauTaiKhoanController(db, NullLogger<YeuCauTaiKhoanController>.Instance, null!)).Index("An", null);
        await Empty<TaiKhoan>("accounts:list");
        await Empty<VaiTro>("accounts:roles");
        await Empty<NhatKyQuanTri>("accounts:audit::");
        await Setup(new UserAccountsController(db, NullLogger<UserAccountsController>.Instance, null!)).Index("An", null, null, null, null);
        await Prime($"leave-requests:year:{today.Year}:role:Admin:department::employee:",
            (Requests: new List<DonTu>(), AnnualLeaveDays: new Dictionary<int, int>(), Contracts: new Dictionary<int, HopDong>()));
        await Empty<LoaiDon>("leave-requests:types");
        await Empty<PhongBan>("leave-requests:departments");
        await Setup(new DonTuController(db, NullLogger<DonTuController>.Instance)).Index(keyword: "An");
        await Empty<PhongBan>("attendance:departments");
        await Empty<CaLamViec>("attendance:shifts");
        await Prime($"attendance:list:{today:yyyyMMdd}:{today:yyyyMMdd}:scope::department::shift:",
            (Shifts: new List<PhanCa>(), Events: new List<PhanCongDuAn>()));
        await Setup(new ChamCongController(db, NullLogger<ChamCongController>.Instance)).Index(null, null, null, null, null, null, null, "An");
        await Empty<KyLuong>("payroll:periods");
        await Prime($"payroll:period:{today.Year}:{today.Month}", new KyLuong { Thang = today.Month, Nam = today.Year });
        await Setup(new BangLuongController(null!, NullLogger<BangLuongController>.Instance)).Index(null, null, "An", null);
        Check(guard.Attempts == 0, "all 13 search groups (including four payroll categories) make zero database calls on warm cache");

        await Prime("shift-requests:scope:2", new List<YeuCauDoiCa> { new() { MaYeuCau = 22, LyDo = "Scope2", TrangThai = "CHO_DUYET" } });
        var manager = Setup(new YeuCauDoiCaController(db, NullLogger<YeuCauDoiCaController>.Instance), "Manager", 2);
        var managerResult = (PartialViewResult)await manager.Index(null, "Scope2");
        Check(((YeuCauDoiCaIndexViewModel)managerResult.Model!).Items.Single().MaYeuCau == 22,
            "manager search selects its scoped cache rather than the admin cache");
    }

    private sealed class NoDatabase : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        { Attempts++; throw new Exception("Unexpected database call on warm cache"); }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { Attempts++; throw new Exception("Unexpected database call on warm cache"); }
    }
    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
