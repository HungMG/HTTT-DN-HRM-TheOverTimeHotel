using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;

static partial class HourlyOperationsTests
{
    private static async Task<(DuAn Event, EventWorkInput Work)> WorkFixture(Fixture f)
    {
        var item = await f.Create(await Input(f));
        var shift = await Shift(f, item, f.WorkerA);
        await Publish(f, shift);
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, Assignment(f, item, shift)));
        item = await f.Get(item.MaDuAn);
        await Historical(f, item, shift);
        item = await f.Get(item.MaDuAn);
        var assignment = item.PhanCongs.Single();
        await Salary(f, f.WorkerA, 8_000_000m);
        await using var db = f.Open();
        db.ChamCongs.Add(new ChamCong { MaPhanCa = shift.MaPhanCa,
            GioCheckIn = assignment.BatDau, GioCheckOut = assignment.KetThuc, TrangThai = "DUNG_GIO" });
        await db.SaveChangesAsync();
        return (item, new() { Version = item.Version, AssignmentId = assignment.MaPhanCong,
            Start = HotelEventRules.Local(assignment.BatDau), End = HotelEventRules.Local(assignment.KetThuc) });
    }

    private static async Task RunPhase2(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Phase 2 one participation interval prices its duration without separate break segments", async () =>
        {
            var (item, work) = await WorkFixture(f);
            var end = work.End;
            var originalStart = work.Start;
            work.Start = work.Start.AddMinutes(30);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, work));
            item = await f.Get(item.MaDuAn);
            work.Version = item.Version;
            await Reject(() => f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, work)));
            work.End = work.Start; work.Start = originalStart;
            await Reject(() => f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, work)));
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count == 0 && preview.Rows.Single().Minutes == 90
                && preview.Total == 57_692m && (await f.Get(item.MaDuAn)).PhanCongs.Single().CongThucTes.Count == 1,
                "Participation did not use one continuous ninety-minute interval.");
            item = await f.Get(item.MaDuAn); work.Version = item.Version;
            work.End = end.AddMinutes(1);
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work)));
            work.Start = DateTime.Today.AddDays(2); work.End = work.Start.AddHours(1);
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work)));
            await using var db = f.Open();
            Require(!await db.QuyetToanSuKiens.AnyAsync(q => q.MaDuAn == item.MaDuAn)
                && item.PhanCongs.All(p => p.MaChiTiet == null), "Recording/previewing work created a payment.");
        });

        await check("Phase 2 unpublished source shift blocks participation recording", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await using (var db = f.Open())
                await db.PhanCas.Where(s => s.MaPhanCa == item.PhanCongs.Single().MaPhanCa)
                    .ExecuteUpdateAsync(p => p.SetProperty(s => s.DaCongBo, false));
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work)));
            Require((await f.Get(item.MaDuAn)).PhanCongs.Single().CongThucTes.Count == 0,
                "Unpublished source created actual work.");
        });

        await check("Phase 2 work outside event time is rejected even when source and assignment cover it", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await using (var db = f.Open())
                await db.PhanCongDuAns.Where(p => p.MaDuAn == item.MaDuAn)
                    .ExecuteUpdateAsync(p => p.SetProperty(a => a.BatDau, item.NgayBatDau.AddHours(-1)));
            work.End = work.Start; work.Start = work.Start.AddMinutes(-30);
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work)));
        });

        await check("Phase 2 office corrections update one work record with reason and complete audit", async () =>
        {
            var (item, input) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, input));
            item = await f.Get(item.MaDuAn);
            var work = item.PhanCongs.Single().CongThucTes.Single();
            var creation = (await f.With(s => s.HistoryAsync(item.MaDuAn))).Single(h => h.HanhDong == "XAC_NHAN_CONG_SU_KIEN");
            using var created = JsonDocument.Parse(creation.GiaTriMoi!);
            Require(created.RootElement.GetProperty("MaCong").GetInt32() == work.MaCong, "Creation audit did not identify the saved work record.");
            EventWorkInput Edit(string reason) => JsonSerializer.Deserialize<EventWorkInput>(JsonSerializer.Serialize(new
            {
                Version = item.Version, AssignmentId = input.AssignmentId, WorkId = work.MaCong,
                Start = input.Start.AddMinutes(30), input.End, Reason = reason
            }))!;
            await Reject(() => f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, Edit("Manager cannot correct approved work"))), forbidden: true);
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, Edit(""))));
            await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, Edit("Correct actual arrival using attendance sheet")));
            item = await f.Get(item.MaDuAn);
            var corrected = item.PhanCongs.Single().CongThucTes.Single();
            Require(corrected.MaCong == work.MaCong && corrected.BatDau == work.BatDau.AddMinutes(30)
                && corrected.MaNguoiXacNhan == f.HR.EmployeeId, "Correction duplicated work or lost its approver.");
            var history = await f.With(s => s.HistoryAsync(item.MaDuAn));
            var audit = history.Single(h => h.HanhDong == "SUA_CONG_SU_KIEN");
            using var before = JsonDocument.Parse(audit.GiaTriCu!);
            using var after = JsonDocument.Parse(audit.GiaTriMoi!);
            Require(before.RootElement.GetProperty("BatDau").GetDateTimeOffset() == work.BatDau
                && after.RootElement.GetProperty("BatDau").GetDateTimeOffset() == corrected.BatDau
                && after.RootElement.GetProperty("MaNguoiXacNhan").GetInt32() == f.HR.EmployeeId
                && audit.LyDo == "Correct actual arrival using attendance sheet", "Correction lacks before/after evidence.");
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count == 0 && preview.Total == 57_692m, "Corrected ninety minutes have the wrong price.");
            await f.With(s => s.DeleteWorkAsync(f.HR, item.MaDuAn, corrected.MaCong, item.Version, "Remove mistaken entry after checking"));
            history = await f.With(s => s.HistoryAsync(item.MaDuAn));
            var deletion = history.First(h => h.HanhDong == "SUA_CONG_SU_KIEN");
            using var deleted = JsonDocument.Parse(deletion.GiaTriCu!);
            Require(deleted.RootElement.TryGetProperty("XacNhanNgoaiLe", out _)
                && deleted.RootElement.TryGetProperty("GiaiTrinh", out _) && deletion.GiaTriMoi == "null",
                "Deletion lost the complete work evidence.");
        });

        await check("Phase 2 preview warns about changed event bounds and retains cancelled work for reconciliation", async () =>
        {
            var (item, input) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, input));
            await using (var db = f.Open())
                await db.DuAns.Where(e => e.MaDuAn == item.MaDuAn).ExecuteUpdateAsync(p => p.SetProperty(e => e.NgayBatDau, item.NgayBatDau.AddMinutes(30)));
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count > 0 && preview.Rows.Single().Minutes == 120,
                "Preview silently accepted changed event bounds or hid recorded work.");
            await using (var db = f.Open())
                await db.PhanCongDuAns.Where(a => a.MaDuAn == item.MaDuAn)
                    .ExecuteUpdateAsync(p => p.SetProperty(a => a.TrangThaiThamGia, "HUY_PHAN_CONG"));
            preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count > 0 && preview.Rows.Single().Minutes == 120,
                "Cancelled staffing silently discarded recorded work from reconciliation.");
        });

        await check("Phase 2 duplicate work across events is rejected and historical duplicates require reconciliation", async () =>
        {
            var (item, input) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, input));
            var other = await f.Create(await Input(f));
            var source = item.PhanCongs.Single();
            await using (var db = f.Open())
            {
                var imported = await db.DuAns.SingleAsync(e => e.MaDuAn == other.MaDuAn);
                imported.NgayBatDau = item.NgayBatDau; imported.NgayKetThuc = item.NgayKetThuc;
                imported.TrangThai = "CHO_NGHIEM_THU";
                // Simulate inconsistent staffing received from another module, without bypassing actual-work validation.
                db.PhanCongDuAns.Add(new() { MaDuAn = other.MaDuAn, MaNhanVien = f.WorkerA, MaPhanCa = source.MaPhanCa,
                    MaPhongBanSuKien = source.MaPhongBanSuKien, VaiTroDuAn = source.VaiTroDuAn,
                    BatDau = source.BatDau, KetThuc = source.KetThuc });
                await db.SaveChangesAsync();
            }
            other = await f.Get(other.MaDuAn);
            var secondInput = new EventWorkInput { Version = other.Version, AssignmentId = other.PhanCongs.Single().MaPhanCong,
                Start = input.Start, End = input.End };
            await using (var db = f.Open())
                await db.DuAns.Where(e => e.MaDuAn == item.MaDuAn)
                    .ExecuteUpdateAsync(p => p.SetProperty(e => e.MaNguoiQuanLy, f.OtherManager.EmployeeId));
            await Reject(() => f.With(s => s.RecordWorkAsync(f.Manager, other.MaDuAn, secondInput)));
            // Existing imported work must remain visible but cannot pass reconciliation silently.
            await using (var db = f.Open())
            {
                db.CongSuKiens.Add(new() { MaPhanCong = secondInput.AssignmentId, BatDau = source.BatDau,
                    KetThuc = source.KetThuc, MaNguoiXacNhan = f.HR.EmployeeId, NgayXacNhan = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count > 0 && preview.Rows.Single().Minutes == 120,
                "Cross-event duplicate work was silently accepted or hidden.");
            await using (var db = f.Open())
                await db.PhanCongDuAns.Where(a => a.MaDuAn == other.MaDuAn)
                    .ExecuteUpdateAsync(p => p.SetProperty(a => a.TrangThaiThamGia, "HUY_PHAN_CONG"));
            preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count > 0, "Cancelled staffing hid overlapping actual work from reconciliation.");
        });

        await check("Phase 2 stopping early pays only recorded work and retains its history", async () =>
        {
            var (item, input) = await WorkFixture(f);
            input.Start = input.Start.AddMinutes(30); input.End = input.Start.AddHours(1);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, input));
            await f.SeedStatus(item.MaDuAn, "DANG_DIEN_RA");
            item = await f.Get(item.MaDuAn);
            var workId = item.PhanCongs.Single().CongThucTes.Single().MaCong;
            await f.With(s => s.StopAsync(f.HR, item.MaDuAn, item.Version, input.End, "Customer closed the event early"));
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            item = await f.Get(item.MaDuAn);
            Require(preview.Warnings.Count == 0 && preview.Rows.Single().Minutes == 60 && preview.Total == 38_462m
                && item.PhanCongs.Single().CongThucTes.Single().MaCong == workId,
                "Stopping counted unworked time or discarded performed work.");
        });

        await check("Phase 2 source schedule edits cannot erase work already performed", async () =>
        {
            var (item, input) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.Manager, item.MaDuAn, input));
            await using (var db = f.Open())
            {
                var shift = await db.PhanCas.SingleAsync(s => s.MaPhanCa == item.PhanCongs.Single().MaPhanCa);
                shift.KetThucDuKien = shift.KetThucDuKien.AddMinutes(-30);
                try { await db.SaveChangesAsync(); throw new InvalidOperationException("Recorded work permitted a destructive schedule edit."); }
                catch (DbUpdateException) { }
            }
            var stored = await f.Get(item.MaDuAn);
            Require(stored.PhanCongs.Single().CongThucTes.Count == 1
                && stored.PhanCongs.Single().TrangThaiThamGia == "DA_PHAN", "A rejected schedule edit changed work history.");
        });
    }
}
