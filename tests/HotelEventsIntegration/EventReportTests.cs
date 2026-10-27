using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Services;

static class EventReportTests
{
    public static async Task Run(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Schema cleanup report uses hourly settlements once and preserves legacy money", async () =>
        {
            var legacy = await f.Create(f.Input());
            var input = f.Input(); input.HourlyCompensation = true;
            var hourly = await f.Create(input);
            await using var db = f.Open();
            var month = HourlyEventRules.WorkMonth(hourly.NgayKetThuc);
            var period = await db.KyLuongs.FirstOrDefaultAsync(k => k.Thang == month.Month && k.Nam == month.Year);
            if (period == null)
            {
                period = new KyLuong { Thang = month.Month, Nam = month.Year, TuNgay = month, DenNgay = month.AddMonths(1).AddDays(-1) };
                db.KyLuongs.Add(period); await db.SaveChangesAsync();
                HourlyOperationsTests.TrackPeriod(period.MaKyLuong);
            }
            PhanCongDuAn Assignment(DuAn item, DateTimeOffset start, DateTimeOffset end, decimal amount) => new()
            {
                MaDuAn = item.MaDuAn, MaNhanVien = f.WorkerA, VaiTroDuAn = "Phụ trách âm thanh và micro",
                BatDau = start, KetThuc = end, DanhGia = "HOAN_THANH", SoTienPhuCap = amount
            };
            db.PhanCongDuAns.AddRange(Assignment(legacy, legacy.NgayBatDau, legacy.NgayKetThuc, 100_000m),
                Assignment(hourly, hourly.NgayBatDau, hourly.NgayBatDau.AddHours(1), 999_999m),
                Assignment(hourly, hourly.NgayBatDau.AddHours(1), hourly.NgayKetThuc, 999_999m));
            foreach (var item in new[] { legacy, hourly })
            {
                db.QuyetToanSuKiens.Add(new() { MaDuAn = item.MaDuAn, MaNhanVien = f.WorkerA,
                    ThangPhatSinh = month, MaKyChiTra = period.MaKyLuong, SoPhut = 120,
                    SoTien = item == legacy ? 100_000m : 76_923m, CanCuTinh = "[]",
                    MaNguoiDuyet = f.HR.EmployeeId, NgayDuyet = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new BaoCaoReportService(db, cache, NullLogger<BaoCaoReportService>.Instance);
            var method = typeof(BaoCaoReportService).GetMethod("BuildSuKienAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            async Task<HieuQuaSuKienReport> Report(ReportPolicy.BaoCaoScope scope, int? department) =>
                await (Task<HieuQuaSuKienReport>)method.Invoke(service, [DateOnly.FromDateTime(HotelEventRules.Local(legacy.NgayBatDau)),
                    DateOnly.FromDateTime(HotelEventRules.Local(hourly.NgayKetThuc)), scope, department])!;
            var report = await Report(ReportPolicy.ResolveScope("HR", null), null);
            var rows = report.DanhSach.Where(r => r.MaDuAn == legacy.MaDuAn || r.MaDuAn == hourly.MaDuAn).ToList();
            Require(rows.Single(r => r.MaDuAn == hourly.MaDuAn).PhuCapDaPhanBo == 76_923m,
                "Hourly report used legacy assignment money or multiplied a settlement by assignment segments.");
            Require(rows.Single(r => r.MaDuAn == legacy.MaDuAn).PhuCapDaPhanBo == 100_000m,
                "Reconciled legacy money was lost or counted twice.");
            var foreign = await db.NhanViens.Where(n => n.MaNhanVien == f.WorkerUnrelated).Select(n => n.MaPhongBan).SingleAsync();
            var restricted = await Report(ReportPolicy.ResolveScope("Manager", foreign), foreign);
            Require(restricted.DanhSach.All(r => r.MaDuAn != hourly.MaDuAn && r.MaDuAn != legacy.MaDuAn),
                "Settlement report leaked events outside department scope.");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
