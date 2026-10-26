using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;

static partial class HourlyOperationsTests
{
    public static async Task RunMergeCompatibility(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Merge compatibility approved MO payroll rejects detail changes and recalculation", async () =>
        {
            var month = new DateOnly(2032, 1, 1);
            int payslipId, detailId;
            await using (var db = f.Open())
            {
                var period = new KyLuong { Thang = month.Month, Nam = month.Year, TuNgay = month,
                    DenNgay = month.AddMonths(1).AddDays(-1), TrangThai = "MO",
                    NgayGuiDuyet = DateTimeOffset.UtcNow.AddHours(-1), NgayDuyet = DateTimeOffset.UtcNow };
                var payslip = new PhieuLuong { MaNhanVien = f.WorkerA, KyLuong = period, TrangThai = "DA_DUYET" };
                var detail = new ChiTietPhieuLuong { PhieuLuong = payslip, TenKhoanLuu = "Approved amount",
                    LoaiKhoanLuu = "THUONG", LoaiKhoan = "THUONG", SoTien = 123_456m };
                db.Add(detail); await db.SaveChangesAsync();
                TrackPeriod(period.MaKyLuong); payslipId = payslip.MaPhieuLuong; detailId = detail.MaChiTiet;
            }
            async Task RejectPayroll(Func<PayrollCalculationService, Task> operation)
            {
                await using var db = f.Open();
                try { await operation(new PayrollCalculationService(db)); }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("Approved payroll was changed without reopening approval.");
            }
            await RejectPayroll(s => s.AdjustSalaryItemAsync(payslipId, "THUONG", "Unapproved addition", 1m, null, "Test", null));
            await RejectPayroll(s => s.DeleteSalaryItemAsync(detailId, "Test", null));
            await RejectPayroll(s => s.RecalculateEmployeeAsync(payslipId, "Test", null));
            await using var verify = f.Open();
            Require(await verify.ChiTietPhieuLuongs.CountAsync(d => d.MaPhieuLuong == payslipId) == 1
                && await verify.ChiTietPhieuLuongs.AnyAsync(d => d.MaChiTiet == detailId && d.SoTien == 123_456m),
                "Rejected operations altered the approved amount.");
        });

        await check("Merge compatibility reads configured rates and freezes the accepted rate", async () =>
        {
            decimal? originalRate;
            await using (var db = f.Open())
                originalRate = await db.CauHinhKhoanLuongs.Where(c => c.MaKhoan == 35).Select(c => (decimal?)c.GiaTriMacDinh).SingleOrDefaultAsync();
            async Task SetRate(decimal rate)
            {
                await using var db = f.Open();
                var config = await db.CauHinhKhoanLuongs.SingleOrDefaultAsync(c => c.MaKhoan == 35);
                if (config == null) { config = new() { MaKhoan = 35, TenKhoan = EventRateConfig.ConfigName, LoaiKhoan = "THUONG" }; db.Add(config); }
                config.GiaTriMacDinh = rate; await db.SaveChangesAsync();
            }
            try
            {
                var (item, work) = await WorkFixture(f);
                await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work));
                item = await f.Get(item.MaDuAn);
                var periodId = await Period(f, HourlyEventRules.WorkMonth(item.NgayBatDau));
                await SetRate(3m);
                var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
                Require(await f.With(s => s.HourlyEventRateAsync()) == 3m && preview.Total == 230_769m,
                    "Preview ignored the configured multiplier.");
                await SetRate(4m);
                await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, item.Version, periodId, preview.Token)));
                preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
                await f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, item.Version, periodId, preview.Token));
                await SetRate(5m);
                var frozen = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
                Require(frozen.Total == 307_692m && frozen.Rows.Single().Basis.All(b => b.Rate == 4m),
                    "A configuration change repriced a frozen settlement.");
            }
            finally
            {
                if (originalRate.HasValue) await SetRate(originalRate.Value);
                else { await using var db = f.Open(); await db.CauHinhKhoanLuongs.Where(c => c.MaKhoan == 35).ExecuteDeleteAsync(); }
            }
        });

        await check("Merge compatibility excludes sent and approved MO periods from event payments", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work));
            item = await f.Get(item.MaDuAn);
            var periodId = await Period(f, HourlyEventRules.WorkMonth(item.NgayBatDau));
            foreach (var approved in new[] { false, true })
            {
                await using (var db = f.Open())
                    await db.KyLuongs.Where(k => k.MaKyLuong == periodId).ExecuteUpdateAsync(p => p
                        .SetProperty(k => k.TrangThai, "MO").SetProperty(k => k.NgayGuiDuyet, DateTimeOffset.UtcNow)
                        .SetProperty(k => k.NgayDuyet, approved ? DateTimeOffset.UtcNow : (DateTimeOffset?)null)
                        .SetProperty(k => k.LyDoTuChoi, (string?)null));
                Require((await f.With(s => s.PaymentPeriodsAsync(f.HR))).All(k => k.MaKyLuong != periodId),
                    "A sent/approved MO period is still offered for event payment.");
                var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
                await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, item.Version, periodId, preview.Token)));
            }
            await using var verify = f.Open();
            Require(!await verify.QuyetToanSuKiens.AnyAsync(q => q.MaDuAn == item.MaDuAn), "Rejected approval created a payment.");
        });

        await check("Merge compatibility keeps x1 evidence for frozen snapshots without a rate", async () =>
        {
            var (item, work) = await WorkFixture(f);
            var periodId = await Period(f, HourlyEventRules.WorkMonth(item.NgayBatDau));
            await using (var db = f.Open())
            {
                var contractId = await db.HopDongs.Where(h => h.MaNhanVien == f.WorkerA).Select(h => h.MaHopDong).FirstAsync();
                db.QuyetToanSuKiens.Add(new() { MaDuAn = item.MaDuAn, MaNhanVien = f.WorkerA,
                    ThangPhatSinh = HourlyEventRules.WorkMonth(item.NgayBatDau), MaKyChiTra = periodId, SoPhut = 120, SoTien = 76_923m,
                    CanCuTinh = JsonSerializer.Serialize(new[] { new { Start = HotelEventRules.ToUtc(work.Start), End = HotelEventRules.ToUtc(work.End), MonthlySalary = 8_000_000m, ContractId = contractId } }),
                    MaNguoiDuyet = f.HR.EmployeeId, NgayDuyet = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            await f.SeedStatus(item.MaDuAn, "HOAN_THANH");
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Total == 76_923m && preview.Rows.Single().Basis.Single().HourlyRate == 8_000_000m / 26m / 8m,
                "The new x2 default changed the evidence of an already frozen x1 settlement.");
        });

        await check("Merge compatibility schema initialization preserves historical payroll details", async () =>
        {
            await using var db = f.Open();
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO cau_hinh_phu_cap(ma_phu_cap,ten_phu_cap,loai_tinh,muc_dinh_muc) VALUES(3,'Historical phone','CO_DINH_THANG',150000) ON CONFLICT(ma_phu_cap) DO NOTHING");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO cau_hinh_khau_tru(ma_khoan_tru,ten_khoan_tru,loai_khoan_tru,ty_le_hoac_dinh_muc) VALUES(6,'Historical advance','TAM_UNG',500000) ON CONFLICT(ma_khoan_tru) DO NOTHING");
            var month = new DateOnly(2031, 1, 1);
            var period = await db.KyLuongs.FirstOrDefaultAsync(k => k.Thang == month.Month && k.Nam == month.Year);
            if (period == null) { period = new() { Thang = month.Month, Nam = month.Year, TuNgay = month, DenNgay = month.AddMonths(1).AddDays(-1), TrangThai = "KHOA_SO" }; db.Add(period); }
            var payslip = new PhieuLuong { MaNhanVien = f.WorkerB, KyLuong = period, TrangThai = "DA_THANH_TOAN" };
            var phone = new ChiTietPhieuLuong { PhieuLuong = payslip, MaPhuCap = 3, TenKhoanLuu = "Historical phone", LoaiKhoanLuu = "PHU_CAP", LoaiKhoan = "PHU_CAP", SoTien = 150_000m };
            var advance = new ChiTietPhieuLuong { PhieuLuong = payslip, MaKhoanTru = 6, TenKhoanLuu = "Historical advance", LoaiKhoanLuu = "KHAU_TRU", LoaiKhoan = "KHAU_TRU", SoTien = 500_000m };
            db.AddRange(phone, advance); await db.SaveChangesAsync();
            await DatabaseSchemaInitializer.EnsurePayrollWorkflowSchemaAsync(db);
            Require(await db.ChiTietPhieuLuongs.CountAsync(d => d.MaChiTiet == phone.MaChiTiet || d.MaChiTiet == advance.MaChiTiet) == 2,
                "Opening payroll schema removed details from an already paid historical payslip.");
            await tx.RollbackAsync();
        });
    }
}
