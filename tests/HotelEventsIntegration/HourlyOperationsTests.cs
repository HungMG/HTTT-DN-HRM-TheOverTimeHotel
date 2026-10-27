using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;

static partial class HourlyOperationsTests
{
    private static readonly List<int> OwnedPeriods = [];

    public static async Task Run(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Hourly booking uses the hotel without venue capacity or preparation buffers", async () =>
        {
            var input = await Input(f);
            input.Venue = ""; input.VenueId = null; input.ExpectedGuests = 250;
            var item = await f.Create(input);
            Require(item.TinhTheoGio && item.PhanCongs.Count == 0 && item.NhuCaus.Count > 0,
                "Booking should create editable staffing needs without requiring assigned employees.");
            Require(item.DiaDiem == "Khách sạn The Overtime", "Booking still requires a hall or venue catalog.");
            var edit = f.Edit(item); edit.VenueId = int.MaxValue; edit.Venue = "Off-site venue";
            edit.SetupMinutes = 60; edit.TeardownMinutes = 45;
            await f.With(s => s.SaveAsync(f.HR, edit));
            item = await f.Get(item.MaDuAn);
            Require(item.DiaDiem == "Khách sạn The Overtime", "Client input restored removed venue or buffer settings.");
            await Reject(() => f.With(s => s.SaveVenueAsync(f.Manager, f.Prefix + "-forged-venue", 50)), forbidden: true);
            var tooLarge = await Input(f);
            tooLarge.ExpectedGuests = 101;
            Require((await f.Create(tooLarge)).SoKhachDuKien == 101, "A removed venue capacity still limits guest counts.");
            var need = item.NhuCaus.First(n => n.MaPhongBan == "KY_THUAT");
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new EventNeedInput
            {
                Version = item.Version, NeedId = need.MaNhuCau,
                Department = "KY_THUAT", Role = need.VaiTro, Count = 2,
                Reason = "Two technicians requested by customer"
            }));
            item = await f.Get(item.MaDuAn);
            Require(item.NhuCaus.Any(n => n.MaNhuCau == need.MaNhuCau && n.SoNguoi == 2), "Edited need was not stored.");
        });

        await check("Hourly assignment requires a published matching source shift and clips its interval", async () =>
        {
            var item = await f.Create(await Input(f));
            var shift = await Shift(f, item, f.WorkerA);
            var assignment = Assignment(f, item, shift);
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, assignment)));
            await Publish(f, shift);
            var available = await f.With(s => s.AvailableShiftsAsync(f.HR, item.MaDuAn));
            Require(available.Any(s => s.MaPhanCa == shift.MaPhanCa), "Published matching shift is unavailable.");
            assignment.EmployeeId = f.WorkerB;
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, assignment)));
            assignment.EmployeeId = f.WorkerA;
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, assignment));
            item = await f.Get(item.MaDuAn);
            var stored = item.PhanCongs.Single();
            Require(stored.MaPhanCa == shift.MaPhanCa && stored.BatDau == item.NgayBatDau
                && HotelEventRules.Local(stored.KetThuc).Hour == 17,
                "A 15-20 request against a 09-17 shift must store only 15-17 and retain the source shift.");
            Require(stored.SoTienPhuCap == 0 && stored.MaChiTiet is null, "Staffing created pay before acceptance.");
            await Reject(() => f.With(s => s.AssignAsync(f.OtherManager, item.MaDuAn, Assignment(f, item, shift))), forbidden: true);
        });

        await check("Hourly participation permits adjacent segments and source edits preserve cancelled history", async () =>
        {
            var item = await f.Create(await Input(f));
            var shift = await Shift(f, item, f.WorkerA);
            await Publish(f, shift);
            var first = Assignment(f, item, shift);
            first.End = first.Start.AddHours(1);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, first));
            item = await f.Get(item.MaDuAn);
            var second = Assignment(f, item, shift);
            second.Start = second.Start.AddHours(1);
            second.End = second.Start.AddHours(1);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, second));
            item = await f.Get(item.MaDuAn);
            Require(item.PhanCongs.Count == 2, "Adjacent segments for one employee were collapsed or rejected.");
            var overlap = Assignment(f, item, shift);
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, overlap)));
            var oldVersion = item.Version;
            await using (var db = f.Open())
            {
                var changed = await db.PhanCas.SingleAsync(s => s.MaPhanCa == shift.MaPhanCa && s.MaNhanVien == f.WorkerA);
                changed.KetThucDuKien = changed.KetThucDuKien.AddMinutes(-30);
                await db.SaveChangesAsync();
            }
            item = await f.Get(item.MaDuAn);
            Require(item.Version > oldVersion && item.PhanCongs.Count == 2
                && item.PhanCongs.All(a => a.TrangThaiThamGia == "HUY_PHAN_CONG"),
                "Editing a future source shift must invalidate staffing while retaining both historical segments.");
            await using var verify = f.Open();
            Require(!await verify.PhanCas.Where(s => s.MaPhanCa == shift.MaPhanCa).Select(s => s.DaCongBo).SingleAsync(),
                "A changed source shift remained published.");
        });

        await OfficeAttendanceCorrection(f, check);
        await StopAndEnd(f, check);

        await check("Hourly actual work previews salary pay and acceptance rejects stale or invalid periods", async () =>
        {
            var item = await f.Create(await Input(f));
            var shift = await Shift(f, item, f.WorkerA);
            await Publish(f, shift);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, shift)));
            item = await f.Get(item.MaDuAn);
            await Historical(f, item, shift);
            item = await f.Get(item.MaDuAn);
            var assignment = item.PhanCongs.Single();
            await Salary(f, f.WorkerA, 8_000_000m);
            var work = new EventWorkInput { Version = item.Version, AssignmentId = assignment.MaPhanCong,
                Start = HotelEventRules.Local(assignment.BatDau), End = HotelEventRules.Local(assignment.KetThuc) };
            await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work));
            await using (var db = f.Open())
            {
                db.ChamCongs.Add(new ChamCong { MaPhanCa = shift.MaPhanCa, GioCheckIn = assignment.BatDau,
                    GioCheckOut = assignment.KetThuc, TrangThai = "DUNG_GIO" });
                await db.SaveChangesAsync();
            }
            var month = new DateOnly(HotelEventRules.Local(assignment.BatDau).Year, HotelEventRules.Local(assignment.BatDau).Month, 1);
            var period = await Period(f, month);
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count == 0 && preview.Rows.Count == 1 && preview.Rows[0].Minutes == 120
                && preview.Rows[0].Amount == 153_846m, "Two hours at salary 8,000,000 must preview 153,846 (salary/26/8 × 2.0), rounded once.");
            await using (var db = f.Open())
                Require(!await db.QuyetToanSuKiens.AnyAsync(q => q.MaDuAn == item.MaDuAn), "Preview created a settlement.");
            await using (var db = f.Open())
                await db.ChamCongs.Where(c => c.MaPhanCa == shift.MaPhanCa)
                    .ExecuteUpdateAsync(p => p.SetProperty(c => c.GioCheckOut, assignment.KetThuc.AddMinutes(-30)));
            var unchanged = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(unchanged.Warnings.Count == 0 && unchanged.Total == preview.Total && unchanged.Token == preview.Token,
                "Shift attendance changed event participation pay.");
            await using (var db = f.Open())
                await db.ChamCongs.Where(c => c.MaPhanCa == shift.MaPhanCa)
                    .ExecuteUpdateAsync(p => p.SetProperty(c => c.GioCheckOut, assignment.KetThuc));
            preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            await Salary(f, f.WorkerA, 9_000_000m);
            await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token)));
            await Salary(f, f.WorkerA, 8_000_000m);
            preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            var earlierPeriod = await Period(f, month.AddMonths(-1));
            await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, earlierPeriod, preview.Token)));
            await using (var db = f.Open())
                await db.KyLuongs.Where(k => k.MaKyLuong == period).ExecuteUpdateAsync(p => p.SetProperty(k => k.TrangThai, "KHOA_SO"));
            await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token)));
            await using (var db = f.Open())
                await db.KyLuongs.Where(k => k.MaKyLuong == period).ExecuteUpdateAsync(p => p.SetProperty(k => k.TrangThai, "MO"));
            preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            var accepted = await Task.WhenAll(
                TryAccept(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token))),
                TryAccept(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token))));
            Require(accepted.Count(success => success) == 1, "Concurrent acceptance must commit exactly one settlement.");
            await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token)));
            await using (var beforePayroll = f.Open())
            {
                var frozen = await beforePayroll.QuyetToanSuKiens.SingleAsync(q => q.MaDuAn == item.MaDuAn);
                Require(frozen.SoPhut == 120 && frozen.SoTien == 153_846m && frozen.MaKyChiTra == period
                    && frozen.ThangPhatSinh == month && frozen.MaChiTiet is null && frozen.CanCuTinh.Length > 2,
                    "Acceptance must freeze one auditable settlement, leaving the payroll link for calculation.");
            }
            await Salary(f, f.WorkerA, 9_000_000m);
            async Task Calculate()
            {
                await using var db = f.Open();
                await new PayrollCalculationService(db).CalculatePayrollAsync(month.Month, month.Year, f.Prefix, f.HR.AccountId);
            }
            await Task.WhenAll(Calculate(), Calculate());
            await using var verify = f.Open();
            var settlement = await verify.QuyetToanSuKiens.SingleAsync(q => q.MaDuAn == item.MaDuAn);
            Require(settlement.SoTien == 153_846m && settlement.MaChiTiet.HasValue,
                "Concurrent payroll calculation must link the frozen amount without repricing it after salary changes.");
            var detail = await verify.ChiTietPhieuLuongs.SingleAsync(d => d.MaChiTiet == settlement.MaChiTiet);
            Require(detail.SoTien == 153_846m, "Payroll detail differs from the accepted preview.");
            Require(await verify.PhieuLuongs.CountAsync(p => p.MaNhanVien == f.WorkerA && p.MaKyLuong == period) == 1,
                "Concurrent payroll calculation created duplicate payslips.");
        });
        await RunPhase2(f, check);
        await ScheduleGuards(f, check);
    }

    private static async Task ScheduleGuards(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Hourly guard publication rejects overlap and insufficient rest", async () =>
        {
            var input = await Input(f);
            var monday = HourlyEventRules.WeekStart(DateOnly.FromDateTime(input.Start)).AddDays(7);
            input.Start = monday.ToDateTime(new TimeOnly(15, 0)); input.End = input.Start.AddHours(5);
            var item = await f.Create(input);
            var first = await Shift(f, item, f.WorkerB);
            await using (var db = f.Open())
            {
                var ca = new CaLamViec { TenCa = f.Prefix + "-second-shift", GioBatDau = new(10, 0), GioKetThuc = new(18, 0) };
                db.PhanCas.Add(new PhanCa { MaNhanVien = f.WorkerB, CaLamViec = ca, NgayLamViec = monday,
                    BatDauDuKien = first.BatDauDuKien.AddHours(1), KetThucDuKien = first.KetThucDuKien.AddHours(1) });
                await db.SaveChangesAsync();
            }
            try
            {
                await Reject(() => Publish(f, first));
                await using (var db = f.Open())
                {
                    var second = await db.PhanCas.SingleAsync(s => s.MaNhanVien == f.WorkerB && s.NgayLamViec == monday && s.MaPhanCa != first.MaPhanCa);
                    second.BatDauDuKien = first.KetThucDuKien.AddHours(1); second.KetThucDuKien = second.BatDauDuKien.AddHours(8);
                    await db.SaveChangesAsync();
                }
                await Reject(() => Publish(f, first));
            }
            finally
            {
                await using var db = f.Open();
                await db.PhanCas.Where(s => s.MaNhanVien == f.WorkerB && s.NgayLamViec == monday).ExecuteDeleteAsync();
            }
        });
        await check("Hourly guard source capacity excludes inactive workers and approved leave", async () =>
        {
            var item = await f.Create(await Input(f));
            var source = await Shift(f, item, f.WorkerA);
            await using (var db = f.Open())
            {
                var employee = await db.NhanViens.SingleAsync(n => n.MaNhanVien == f.WorkerA);
                db.PhanCas.Add(new PhanCa { MaNhanVien = f.WorkerB, MaCa = source.MaCa, NgayLamViec = source.NgayLamViec,
                    BatDauDuKien = source.BatDauDuKien, KetThucDuKien = source.KetThucDuKien });
                db.DinhBienCas.Add(new DinhBienCa { MaCa = source.MaCa, MaChucVu = employee.MaChucVu!.Value,
                    MaPhongBan = employee.MaPhongBan, SoNguoiToiThieu = 1 });
                await db.SaveChangesAsync();
            }
            await Publish(f, source);
            await using (var db = f.Open())
                await db.NhanViens.Where(n => n.MaNhanVien == f.WorkerB).ExecuteUpdateAsync(p => p.SetProperty(n => n.TrangThai, "NGHI_PHEP"));
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, source))));
            await using (var db = f.Open())
            {
                await db.NhanViens.Where(n => n.MaNhanVien == f.WorkerB).ExecuteUpdateAsync(p => p.SetProperty(n => n.TrangThai, "DANG_LAM"));
                var leave = new LoaiDon { TenLoaiDon = f.Prefix + "-leave", LaNghiPhep = true };
                db.DonTus.Add(new DonTu { MaNhanVien = f.WorkerB, LoaiDon = leave, TuNgay = source.NgayLamViec,
                    DenNgay = source.NgayLamViec, TrangThai = "DA_DUYET", LyDo = "Synthetic approved leave", MaNguoiDuyet = f.HR.EmployeeId });
                await db.SaveChangesAsync();
            }
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, source))));
            await using (var db = f.Open())
                await db.DonTus.Where(d => d.MaNhanVien == f.WorkerB && d.LoaiDon!.TenLoaiDon == f.Prefix + "-leave").ExecuteDeleteAsync();
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, source)));
        });
    }

    private static async Task OfficeAttendanceCorrection(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Hourly participation needs only start and end without shift attendance", async () =>
        {
            var item = await f.Create(await Input(f));
            var shift = await Shift(f, item, f.WorkerA);
            await Publish(f, shift);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, shift)));
            item = await f.Get(item.MaDuAn);
            await Historical(f, item, shift);
            item = await f.Get(item.MaDuAn);
            var assignment = item.PhanCongs.Single();
            var input = new EventWorkInput { Version = item.Version, AssignmentId = assignment.MaPhanCong,
                Start = HotelEventRules.Local(assignment.BatDau), End = HotelEventRules.Local(assignment.KetThuc) };
            await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, input));
            await using var db = f.Open();
            var actual = await db.CongSuKiens.SingleAsync(c => c.MaPhanCong == assignment.MaPhanCong);
            Require(actual.MaNguoiXacNhan == f.HR.EmployeeId,
                "Participation incorrectly required attendance-exception approval.");
        });
    }

    private static async Task StopAndEnd(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Hourly early end retains participation history and stopping retains its reason", async () =>
        {
            var item = await f.Create(await Input(f));
            var shift = await Shift(f, item, f.WorkerA);
            await Publish(f, shift);
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, Assignment(f, item, shift)));
            item = await f.Get(item.MaDuAn);
            await Historical(f, item, shift);
            await f.SeedStatus(item.MaDuAn, "DANG_DIEN_RA");
            item = await f.Get(item.MaDuAn);
            var assignment = item.PhanCongs.Single();
            var end = HotelEventRules.Local(assignment.BatDau).AddHours(1);
            await Reject(() => f.With(s => s.EndParticipationAsync(f.HR, item.MaDuAn, assignment.MaPhanCong, item.Version, end, "")));
            await f.With(s => s.EndParticipationAsync(f.HR, item.MaDuAn, assignment.MaPhanCong, item.Version, end, "Employee became ill"));
            item = await f.Get(item.MaDuAn);
            Require(item.PhanCongs.Single().TrangThaiThamGia == "DA_KET_THUC"
                && item.PhanCongs.Single().LyDoKetThuc == "Employee became ill", "Early participation end removed its historical record or reason.");
            await f.With(s => s.StopAsync(f.HR, item.MaDuAn, item.Version, end.AddMinutes(30), "Customer ended the event early"));
            item = await f.Get(item.MaDuAn);
            Require(item.KetThucThucTe == HotelEventRules.ToUtc(end.AddMinutes(30))
                && item.LyDoDung == "Customer ended the event early", "Stopping did not retain its real cutoff and reason.");
        });
    }
    public static async Task<(int EventId, int ShiftId)> PreparePreview(Fixture f)
    {
        var item = await f.Create(await Input(f));
        var shift = await Shift(f, item, f.WorkerA);
        await Publish(f, shift);
        return (item.MaDuAn, shift.MaPhanCa);
    }

    private static async Task<EventInput> Input(Fixture f)
    {
        var input = f.Input();
        var monday = HourlyEventRules.WeekStart(DateOnly.FromDateTime(input.Start)).AddDays(28 + (input.Start.Day * 7));
        input.Start = monday.ToDateTime(new TimeOnly(15, 0));
        input.End = input.Start.AddHours(5);
        input.HourlyCompensation = true;
        return input;
    }

    private static async Task<PhanCa> Shift(Fixture f, DuAn item, int employee)
    {
        await using var db = f.Open();
        var date = DateOnly.FromDateTime(HotelEventRules.Local(item.NgayBatDau));
        var ca = new CaLamViec { TenCa = $"{f.Prefix}-{date:yyyyMMdd}", GioBatDau = new(9, 0), GioKetThuc = new(17, 0) };
        var shift = new PhanCa { MaNhanVien = employee, CaLamViec = ca, NgayLamViec = date,
            BatDauDuKien = HotelEventRules.ToUtc(date.ToDateTime(new TimeOnly(9, 0))),
            KetThucDuKien = HotelEventRules.ToUtc(date.ToDateTime(new TimeOnly(17, 0))) };
        db.PhanCas.Add(shift);
        await db.SaveChangesAsync();
        return shift;
    }

    private static async Task Publish(Fixture f, PhanCa shift)
    {
        var week = HourlyEventRules.WeekStart(shift.NgayLamViec);
        var published = await f.With(s => s.PublishedWeekAsync(week));
        await f.With(s => s.PublishWeekAsync(f.HR, week, published?.Version ?? 0, "Synthetic integration publication"));
    }

    private static EventAssignmentInput Assignment(Fixture f, DuAn item, PhanCa shift)
    {
        var input = f.Assignment(item, shift.MaNhanVien, 0);
        input.ShiftId = shift.MaPhanCa;
        return input;
    }

    private static async Task Historical(Fixture f, DuAn item, PhanCa shift)
    {
        // Move only this run's fixtures; restore staffing after the real shift-change trigger invalidates it.
        await using (var db = f.Open())
        {
            var source = await db.PhanCas.SingleAsync(s => s.MaPhanCa == shift.MaPhanCa && s.MaNhanVien == f.WorkerA);
            source.NgayLamViec = source.NgayLamViec.AddDays(-366);
            source.BatDauDuKien = source.BatDauDuKien.AddDays(-366);
            source.KetThucDuKien = source.KetThucDuKien.AddDays(-366);
            await db.SaveChangesAsync();
        }
        await f.MoveToPast(item.MaDuAn);
        await using (var db = f.Open())
        {
            await db.PhanCongDuAns.Where(a => a.MaDuAn == item.MaDuAn).ExecuteUpdateAsync(p => p.SetProperty(a => a.TrangThaiThamGia, "DA_PHAN"));
            await db.PhanCas.Where(s => s.MaPhanCa == shift.MaPhanCa).ExecuteUpdateAsync(p => p.SetProperty(s => s.DaCongBo, true));
        }
        await f.SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
    }

    private static async Task Salary(Fixture f, int employee, decimal salary)
    {
        await using var db = f.Open();
        var changed = await db.HopDongs.Where(h => h.MaNhanVien == employee && h.SoHopDong.StartsWith(f.Prefix))
            .ExecuteUpdateAsync(p => p.SetProperty(h => h.LuongCoBan, salary));
        Require(changed == 1, "Expected one fixture-owned contract.");
    }

    private static async Task<int> Period(Fixture f, DateOnly month)
    {
        await using var db = f.Open();
        var period = await db.KyLuongs.FirstOrDefaultAsync(k => k.Thang == month.Month && k.Nam == month.Year);
        if (period == null)
        {
            period = new KyLuong { Thang = month.Month, Nam = month.Year, TuNgay = month,
                DenNgay = month.AddMonths(1).AddDays(-1), TrangThai = "MO" };
            db.KyLuongs.Add(period);
            await db.SaveChangesAsync();
        }
        else
        {
            period.TrangThai = "MO";
            period.NgayGuiDuyet = null;
            period.NgayDuyet = null;
            period.LyDoTuChoi = null;
            await db.SaveChangesAsync();
        }
        OwnedPeriods.Add(period.MaKyLuong);
        return period.MaKyLuong;
    }

    public static void TrackPeriod(int id) => OwnedPeriods.Add(id);

    public static async Task Cleanup(Fixture f)
    {
        await using var db = f.Open();
        var ids = await db.NhanViens.Where(n => n.EmailCongVu.StartsWith(f.Prefix + "-")).Select(n => n.MaNhanVien).ToArrayAsync();
        var events = await db.DuAns.Where(e => e.TenDuAn.StartsWith(f.Prefix)).Select(e => e.MaDuAn).ToArrayAsync();
        var assignments = db.PhanCongDuAns.Where(a => events.Contains(a.MaDuAn)).Select(a => a.MaPhanCong);
        await db.CongSuKiens.Where(c => assignments.Contains(c.MaPhanCong)).ExecuteDeleteAsync();
        await db.QuyetToanSuKiens.Where(q => events.Contains(q.MaDuAn)).ExecuteDeleteAsync();
        var periodPayslips = db.PhieuLuongs.Where(p => OwnedPeriods.Contains(p.MaKyLuong) || ids.Contains(p.MaNhanVien)).Select(p => p.MaPhieuLuong);
        await db.ChiTietTruBaoHiems.Where(b => db.ChiTietPhieuLuongs.Any(c => periodPayslips.Contains(c.MaPhieuLuong) && c.MaChiTiet == b.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietTruThueTncns.Where(t => db.ChiTietPhieuLuongs.Any(c => periodPayslips.Contains(c.MaPhieuLuong) && c.MaChiTiet == t.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietTruViPhamTamUngs.Where(v => db.ChiTietPhieuLuongs.Any(c => periodPayslips.Contains(c.MaPhieuLuong) && c.MaChiTiet == v.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietPhieuLuongs.Where(c => periodPayslips.Contains(c.MaPhieuLuong)).ExecuteDeleteAsync();
        await db.PhieuLuongs.Where(p => OwnedPeriods.Contains(p.MaKyLuong) || ids.Contains(p.MaNhanVien)).ExecuteDeleteAsync();
        await db.KyLuongs.Where(k => OwnedPeriods.Contains(k.MaKyLuong)).ExecuteDeleteAsync();
        OwnedPeriods.Clear();
    }

    private static async Task<bool> TryAccept(Func<Task> run)
    {
        try { await run(); return true; }
        catch (EventOperationException) { return false; }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Reject(Func<Task> run, bool forbidden = false)
    {
        try { await run(); }
        catch (EventOperationException error) { Require(!forbidden || error.Forbidden, "Expected forbidden authorization failure."); return; }
        throw new InvalidOperationException("Expected service rejection.");
    }
}

