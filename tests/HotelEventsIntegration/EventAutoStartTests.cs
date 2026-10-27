using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;

static class EventAutoStartTests
{
    public static async Task Run(Fixture f, Func<string, Func<Task>, Task> check)
    {
        var owned = new List<int>();
        async Task<DuAn> Ready(int employee, DateTimeOffset start, DateTimeOffset end)
        {
            var input = f.Input(); input.HourlyCompensation = true;
            var item = await f.Create(input); owned.Add(item.MaDuAn);
            await using (var db = f.Open())
            {
                var saved = await db.DuAns.Include(e => e.NhuCaus).SingleAsync(e => e.MaDuAn == item.MaDuAn);
                var need = saved.NhuCaus.First(n => n.MaPhongBan == "KY_THUAT");
                db.NhuCauSuKiens.RemoveRange(saved.NhuCaus.Where(n => n != need));
                need.SoNguoi = 1; need.LaMau = false;
                saved.NgayBatDau = start; saved.NgayKetThuc = end;
                var shift = new PhanCa
                {
                    MaNhanVien = employee, DaCongBo = true,
                    NgayLamViec = DateOnly.FromDateTime(HotelEventRules.Local(start.AddHours(-1))),
                    BatDauDuKien = start.AddHours(-1), KetThucDuKien = start.AddHours(7),
                    CaLamViec = new() { TenCa = $"{f.Prefix}-auto-{item.MaDuAn}",
                        GioBatDau = TimeOnly.FromDateTime(HotelEventRules.Local(start.AddHours(-1))),
                        GioKetThuc = TimeOnly.FromDateTime(HotelEventRules.Local(start.AddHours(7))),
                        QuaDem = HotelEventRules.Local(start.AddHours(7)).Date > HotelEventRules.Local(start.AddHours(-1)).Date }
                };
                db.PhanCas.Add(shift); await db.SaveChangesAsync();
                item = await f.Get(item.MaDuAn);
                var assignment = f.Assignment(item, employee, 0);
                assignment.Role = need.VaiTro; assignment.ShiftId = shift.MaPhanCa;
                await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, assignment));
            }
            return await f.Get(item.MaDuAn);
        }
        try
        {
            await check("Auto start starts a ready due event once with a system audit and no pay", async () =>
            {
                var now = DateTimeOffset.UtcNow;
                var item = await Ready(f.WorkerA, now.AddMinutes(-10), now.AddHours(2));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 1, "Ready event did not start automatically.");
                var saved = await f.Get(item.MaDuAn);
                Require(saved.TrangThai == "DANG_DIEN_RA" && saved.Version == item.Version + 1,
                    "Automatic start did not change status and version together.");
                Require(saved.QuyetToans.Count == 0 && saved.PhanCongs.All(p => p.CongThucTes.Count == 0),
                    "Automatic start invented actual work or payment.");
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0, "A repeated scan started the event again.");
                var audit = (await f.With(s => s.HistoryAsync(item.MaDuAn))).Single(h => h.HanhDong == "TU_DONG_BAT_DAU_SU_KIEN");
                Require(audit.MaTaiKhoanThucHien == null && audit.TenNguoiThucHien == "Hệ thống", "Automatic start impersonated an employee.");
            });
            await check("Auto start preserves fully staffed future expired and terminal events", async () =>
            {
                var now = DateTimeOffset.UtcNow;
                var future = await Ready(f.WorkerA, now.AddDays(2), now.AddDays(2).AddHours(2));
                var expired = await Ready(f.WorkerA, now.AddHours(-4), now.AddHours(-2));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0, "Scan started an event outside its time window.");
                Require((await f.Get(future.MaDuAn)).TrangThai == "LEN_KE_HOACH"
                    && (await f.Get(expired.MaDuAn)).TrangThai == "LEN_KE_HOACH", "Scan rewrote future or historical planning.");
                foreach (var status in new[] { "HUY", "HOAN_THANH", "CHO_NGHIEM_THU" })
                {
                    await f.SeedStatus(expired.MaDuAn, status);
                    Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0
                        && (await f.Get(expired.MaDuAn)).TrangThai == status, "Scan reopened a terminal or review event.");
                }
            });
            await check("Auto start skips invalid source staffing and guests without blocking ready events", async () =>
            {
                var now = DateTimeOffset.UtcNow;
                var invalid = await Ready(f.WorkerB, now.AddMinutes(-10), now.AddHours(2));
                var valid = await Ready(f.RoleWorkers["KY_THUAT"][2], now.AddMinutes(-10), now.AddHours(2));
                var sourceId = invalid.PhanCongs.Single().MaPhanCa;
                await using var db = f.Open();
                await db.PhanCas.Where(s => s.MaPhanCa == sourceId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DaCongBo, false));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 1
                    && (await f.Get(valid.MaDuAn)).TrangThai == "DANG_DIEN_RA"
                    && (await f.Get(invalid.MaDuAn)).Version == invalid.Version,
                    "Invalid source either started or prevented another event from starting.");
                await db.PhanCas.Where(s => s.MaPhanCa == sourceId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DaCongBo, true));
                await db.NhuCauSuKiens.Where(n => n.MaDuAn == invalid.MaDuAn).ExecuteUpdateAsync(n => n.SetProperty(p => p.SoNguoi, 2));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0, "Missing mandatory staffing was ignored.");
                await db.NhuCauSuKiens.Where(n => n.MaDuAn == invalid.MaDuAn).ExecuteUpdateAsync(n => n.SetProperty(p => p.SoNguoi, 1));
                await db.PhanCas.Where(s => s.MaPhanCa == sourceId).ExecuteUpdateAsync(s => s.SetProperty(p => p.TrangThai, "VANG_MAT"));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0, "An absent source shift was accepted.");
                await db.PhanCas.Where(s => s.MaPhanCa == sourceId).ExecuteUpdateAsync(s => s.SetProperty(p => p.TrangThai, "DA_PHAN"));
                await db.DuAns.Where(e => e.MaDuAn == invalid.MaDuAn).ExecuteUpdateAsync(e => e.SetProperty(p => p.SoKhachDuKien, 0));
                Require(await f.With(s => s.AutoStartReadyEventsAsync()) == 0, "Zero guests were accepted.");
            });
            await check("Auto start concurrent scans commit one transition and invalidate stale manual updates", async () =>
            {
                var now = DateTimeOffset.UtcNow;
                var item = await Ready(f.BoundaryWorker, now.AddMinutes(-10), now.AddHours(2));
                var counts = await Task.WhenAll(f.With(s => s.AutoStartReadyEventsAsync()), f.With(s => s.AutoStartReadyEventsAsync()));
                Require(counts.Sum() == 1, "Concurrent scans duplicated automatic start.");
                var audit = await f.With(s => s.HistoryAsync(item.MaDuAn));
                Require(audit.Count(h => h.HanhDong == "TU_DONG_BAT_DAU_SU_KIEN") == 1, "Automatic audit was duplicated.");
                try
                {
                    await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
                    throw new Exception("A stale manual update overwrote automatic start.");
                }
                catch (EventOperationException) { }
            });
        }
        finally
        {
            await using var db = f.Open();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var subjects = owned.Select(id => $"SK-{id}").ToArray();
            await db.NhatKyQuanTris.Where(h => subjects.Contains(h.TenTaiKhoanBiTacDong)).ExecuteDeleteAsync();
            await db.PhanCongDuAns.Where(p => owned.Contains(p.MaDuAn)).ExecuteDeleteAsync();
            await db.NhuCauSuKiens.Where(n => owned.Contains(n.MaDuAn)).ExecuteDeleteAsync();
            await db.DuAns.Where(e => owned.Contains(e.MaDuAn)).ExecuteDeleteAsync();
            var shifts = db.PhanCas.Where(s => s.CaLamViec!.TenCa.StartsWith(f.Prefix + "-auto-"));
            await shifts.ExecuteDeleteAsync();
            await db.CaLamViecs.Where(c => c.TenCa.StartsWith(f.Prefix + "-auto-")).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
