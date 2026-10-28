using System.Text.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Services;
using OvertimeHotel.HRM.WebAdmin.Controllers;

static class EventTypeIntegrationTests
{
    public static async Task Run(Fixture f, Func<string, Func<Task>, Task> check)
    {
        EventInput Input(string type)
        { var input = f.Input(); input.Type = type; input.HourlyCompensation = true; return input; }
        async Task<DuAn> Historical(string type)
        {
            var current = await f.Create(Input("HOI_NGHI_MICE"));
            await using var db = f.Open();
            await db.NhuCauSuKiens.Where(n => n.MaDuAn == current.MaDuAn).ExecuteDeleteAsync();
            var item = await db.DuAns.SingleAsync(e => e.MaDuAn == current.MaDuAn);
            item.LoaiDuAn = type;
            foreach (var department in HotelEventRules.GetRequiredDepartments(type))
                foreach (var role in HotelEventRoleRules.GetRoles(type, department, item.SoKhachDuKien).Where(r => r.IsRequired))
                    item.NhuCaus.Add(new() { MaPhongBan = department, VaiTro = role.Name, SoNguoi = role.RequiredCount, LaMau = true });
            await db.SaveChangesAsync();
            return await f.Get(current.MaDuAn);
        }
        await check("Event types create all five catalogs with required needs without staff or shifts", async () =>
        {
            foreach (var type in new[] { "TIEC_CUOI", "HOI_NGHI_MICE", "TIEC_SINH_NHAT", "TIEC_TAT_NIEN", "HOP_BAO" })
            {
                var item = await f.Create(Input(type));
                var expected = HotelEventRules.GetRequiredDepartments(type).SelectMany(d => HotelEventRoleRules.GetRoles(type, d, item.SoKhachDuKien).Where(r => r.IsRequired)).ToList();
                Require(item.LoaiDuAn == type && item.PhanCongs.Count == 0 && item.TrangThai == "LEN_KE_HOACH", "Creating the event incorrectly requires staff.");
                Require(item.NhuCaus.Count == expected.Count && item.NhuCaus.All(n => n.LaMau && n.SoNguoi > 0), "Wrong default staffing needs.");
            }
        });
        await check("Event types reject retired creation and unrelated retired edits but preserve the current old type", async () =>
        {
            foreach (var type in new[] { "GALA_DINNER", "FESTIVAL_AM_THUC", "DOAN_VIP", "INVALID" })
                await Reject(() => f.With(s => s.SaveAsync(f.HR, Input(type))), "Input.Type");
            var item = await Historical("GALA_DINNER");
            var edit = f.Edit(item); edit.Description = "Keep the historical classification";
            await f.With(s => s.SaveAsync(f.HR, edit));
            item = await f.Get(item.MaDuAn);
            Require(item.LoaiDuAn == "GALA_DINNER" && item.MoTa == edit.Description, "Ordinary legacy editing changed the classification.");
            edit = f.Edit(item); edit.Type = "DOAN_VIP";
            await Reject(() => f.With(s => s.SaveAsync(f.HR, edit)), "Input.Type");
            edit.Type = "TIEC_TAT_NIEN";
            await f.With(s => s.SaveAsync(f.HR, edit));
            Require((await f.Get(item.MaDuAn)).LoaiDuAn == "TIEC_TAT_NIEN", "Legacy planned event could not explicitly move to an active type.");
        });
        await check("Event types rescale new party needs and preserve HR custom needs", async () =>
        {
            var item = await f.Create(Input("TIEC_SINH_NHAT"));
            var need = item.NhuCaus.First(n => n.MaPhongBan == "KY_THUAT");
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new() { Version = item.Version, NeedId = need.MaNhuCau,
                Department = need.MaPhongBan, Role = need.VaiTro, Count = 7, Reason = "Seven technicians requested" }));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item); edit.ExpectedGuests = 81;
            await f.With(s => s.SaveAsync(f.HR, edit));
            item = await f.Get(item.MaDuAn);
            Require(item.NhuCaus.Single(n => n.MaNhuCau == need.MaNhuCau).SoNguoi == 7 && !item.NhuCaus.Single(n => n.MaNhuCau == need.MaNhuCau).LaMau, "HR custom need was overwritten.");
            Require(item.NhuCaus.Single(n => n.MaPhongBan == "AM_THUC" && n.VaiTro == "Phục vụ tiệc sinh nhật").SoNguoi == 5, "Guest ratio was not rounded up.");
            edit = f.Edit(item); edit.Type = "HOP_BAO";
            await Reject(() => f.With(s => s.SaveAsync(f.HR, edit)), "Input.Start");
            Require((await f.Get(item.MaDuAn)).LoaiDuAn == "TIEC_SINH_NHAT", "Incompatible custom role did not block type change.");
        });
        await check("Event types allow HR optional press catering without making it mandatory by default", async () =>
        {
            var item = await f.Create(Input("HOP_BAO"));
            Require(item.NhuCaus.All(n => n.MaPhongBan is not ("AM_THUC" or "BEP" or "BUONG_PHONG")), "Press catering was mandatory.");
            var role = HotelEventRoleRules.GetRoles("HOP_BAO", "AM_THUC", item.SoKhachDuKien).First();
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new() { Version = item.Version, Department = "AM_THUC", Role = role.Name,
                Count = 2, Reason = "Customer requested a tea break" }));
            item = await f.Get(item.MaDuAn);
            Require(item.NhuCaus.Any(n => n.MaPhongBan == "AM_THUC" && n.SoNguoi == 2 && !n.LaMau), "Optional catering could not be enabled by HR.");
        });
        await check("Event types preserve completed legacy records without regenerating needs", async () =>
        {
            var item = await Historical("FESTIVAL_AM_THUC");
            await f.SeedStatus(item.MaDuAn, "HOAN_THANH"); item = await f.Get(item.MaDuAn);
            var before = JsonSerializer.Serialize(item.NhuCaus.Select(n => new { n.MaNhuCau, n.MaPhongBan, n.VaiTro, n.SoNguoi, n.LaMau }));
            var edit = f.Edit(item); edit.Type = "HOP_BAO";
            await Reject(() => f.With(s => s.SaveAsync(f.HR, edit)));
            item = await f.Get(item.MaDuAn);
            Require(item.LoaiDuAn == "FESTIVAL_AM_THUC" && before == JsonSerializer.Serialize(item.NhuCaus.Select(n => new { n.MaNhuCau, n.MaPhongBan, n.VaiTro, n.SoNguoi, n.LaMau })), "Completed history was regenerated.");
        });
        await check("Event types reject incompatible assigned roles before changing type", async () =>
        {
            var input = Input("TIEC_SINH_NHAT"); input.HourlyCompensation = false;
            var item = await f.Create(input);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA)));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item); edit.Type = "HOP_BAO";
            await Reject(() => f.With(s => s.SaveAsync(f.HR, edit)), "Input.Type");
            Require((await f.Get(item.MaDuAn)).LoaiDuAn == "TIEC_SINH_NHAT", "Assigned event changed type with an incompatible role.");
        });
        await check("Event types optional departments remain available for converted non-hourly legacy assignments", async () =>
        {
            var item = await Historical("GALA_DINNER");
            await using (var legacyDb = f.Open())
                await legacyDb.DuAns.Where(d => d.MaDuAn == item.MaDuAn).ExecuteUpdateAsync(u => u.SetProperty(d => d.TinhTheoGio, false));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item); edit.Type = "HOP_BAO";
            await f.With(s => s.SaveAsync(f.HR, edit)); item = await f.Get(item.MaDuAn);
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new() { Version = item.Version, Department = "AM_THUC",
                Role = HotelEventRoleRules.GetRoles("HOP_BAO", "AM_THUC", 40)[0].Name, Count = 1, Reason = "Optional catering" }));
            await using var db = f.Open();
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, f.HR.AccountId.ToString())], "Test")) };
            var controller = new SuKienController(db, new HotelEventService(db), NullLogger<SuKienController>.Instance)
            { ControllerContext = new() { HttpContext = http }, ViewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary()) };
            var result = await controller.Assignment(item.MaDuAn, [f.RoleWorkers["AM_THUC"][0]], "AM_THUC", null, null, null);
            Require(result is PartialViewResult { Model: EventDetailsViewModel model } && model.RequiredDepartments.Contains("AM_THUC"), "Optional department assignment endpoint rejected a saved HR need.");
        });
        await check("Event types reports resolve all five current and three historical labels", async () =>
        {
            var items = new List<DuAn>();
            foreach (var type in new[] { "TIEC_CUOI", "HOI_NGHI_MICE", "TIEC_SINH_NHAT", "TIEC_TAT_NIEN", "HOP_BAO" })
                items.Add(await f.Create(Input(type)));
            foreach (var type in new[] { "GALA_DINNER", "FESTIVAL_AM_THUC", "DOAN_VIP" }) items.Add(await Historical(type));
            await using var db = f.Open();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new BaoCaoReportService(db, cache, NullLogger<BaoCaoReportService>.Instance);
            var method = typeof(BaoCaoReportService).GetMethod("BuildSuKienAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var report = await (Task<HieuQuaSuKienReport>)method.Invoke(service, [
                DateOnly.FromDateTime(HotelEventRules.Local(items.Min(i => i.NgayBatDau))),
                DateOnly.FromDateTime(HotelEventRules.Local(items.Max(i => i.NgayKetThuc))), ReportPolicy.ResolveScope("HR", null), null])!;
            foreach (var item in items)
                Require(report.DanhSach.Single(r => r.MaDuAn == item.MaDuAn).LoaiLabel == HotelEventRules.TypeLabels[item.LoaiDuAn], "Report did not resolve " + item.LoaiDuAn);
        });
    }
    static async Task Reject(Func<Task> action, string? field = null)
    {
        try { await action(); }
        catch (EventOperationException ex) when (field == null || ex.Field == field) { return; }
        throw new Exception("Expected operation to reject " + field);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
