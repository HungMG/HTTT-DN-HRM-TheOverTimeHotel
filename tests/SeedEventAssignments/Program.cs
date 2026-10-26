using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

string? connection = null;
foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
{
    var path = Path.Combine(AppContext.BaseDirectory, file);
    if (!File.Exists(path)) continue;
    var config = new ConfigurationBuilder().AddJsonFile(path, optional: true).Build();
    var value = config["ConnectionStrings:DefaultConnection"];
    if (!string.IsNullOrWhiteSpace(value)) connection = value;
}

if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("Không tìm thấy connection string.");
    return 1;
}

var normalized = new NpgsqlConnectionStringBuilder(connection) { IncludeErrorDetail = false, Timeout = 30 }.ConnectionString;
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(normalized).Options;
await using var context = new AppDbContext(options);

Console.WriteLine("======================================================================");
Console.WriteLine("HỆ THỐNG QUẢN LÝ NHÂN SỰ & SỰ KIỆN - THU HỒI DỮ LIỆU THÁNG 11 & GIỮ THÁNG 10");
Console.WriteLine("======================================================================");

// -----------------------------------------------------------------------------
// 1. XÓA CÁC SỰ KIỆN THÁNG 11 VÀ CÔNG VIỆC LIÊN QUAN
// -----------------------------------------------------------------------------
Console.WriteLine("\n>>> 1. XÓA CÁC SỰ KIỆN THÁNG 11...");
var novEvents = await context.DuAns
    .Where(d => d.NgayBatDau >= new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)))
    .ToListAsync();

foreach (var ev in novEvents)
{
    Console.WriteLine($"   [-] Xóa sự kiện tháng 11: ID={ev.MaDuAn} - '{ev.TenDuAn}'");
    var assignments = await context.PhanCongDuAns
        .Include(p => p.CongThucTes)
        .Where(p => p.MaDuAn == ev.MaDuAn)
        .ToListAsync();

    foreach (var a in assignments)
    {
        context.CongSuKiens.RemoveRange(a.CongThucTes);
    }
    context.PhanCongDuAns.RemoveRange(assignments);

    var needs = await context.NhuCauSuKiens.Where(n => n.MaDuAn == ev.MaDuAn).ToListAsync();
    context.NhuCauSuKiens.RemoveRange(needs);

    context.DuAns.Remove(ev);
}
await context.SaveChangesAsync();
Console.WriteLine($"   [✓] Đã xóa {novEvents.Count} sự kiện tháng 11.");

// -----------------------------------------------------------------------------
// 2. XÓA TOÀN BỘ CA PHÂN & CHẤM CÔNG THÁNG 11
// -----------------------------------------------------------------------------
Console.WriteLine("\n>>> 2. XÓA CA PHÂN & CHẤM CÔNG THÁNG 11 (2026-11-01 -> 2026-11-30)...");
var novStart = new DateOnly(2026, 11, 1);
var novEnd = new DateOnly(2026, 11, 30);

var novShifts = await context.PhanCas
    .Include(p => p.ChamCong)
    .Where(p => p.NgayLamViec >= novStart && p.NgayLamViec <= novEnd)
    .ToListAsync();

foreach (var s in novShifts)
{
    if (s.ChamCong != null)
    {
        context.ChamCongs.Remove(s.ChamCong);
    }
}
context.PhanCas.RemoveRange(novShifts);
await context.SaveChangesAsync();
Console.WriteLine($"   [✓] Đã xóa {novShifts.Count} ca phân & chấm công tháng 11.");

// -----------------------------------------------------------------------------
// 3. XÓA CÔNG BỐ LỊCH TUẦN THÁNG 11
// -----------------------------------------------------------------------------
Console.WriteLine("\n>>> 3. XÓA CÔNG BỐ LỊCH TUẦN THÁNG 11...");
var novWeeks = await context.CongBoLichTuans
    .Where(w => w.DauTuan >= novStart)
    .ToListAsync();

context.CongBoLichTuans.RemoveRange(novWeeks);
await context.SaveChangesAsync();
Console.WriteLine($"   [✓] Đã xóa {novWeeks.Count} đợt công bố lịch tháng 11.");

// -----------------------------------------------------------------------------
// 4. KIỂM TRA & BẢO ĐẢM DỮ LIỆU THÁNG 10 CHÍNH XÁC
// -----------------------------------------------------------------------------
Console.WriteLine("\n>>> 4. TỔNG HỢP & BẢO ĐẢM DỮ LIỆU THÁNG 10...");
var octEvents = await context.DuAns
    .Include(d => d.PhanCongs)
    .Where(d => d.NgayBatDau < new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)))
    .ToListAsync();

var octShiftsCount = await context.PhanCas.CountAsync(p => p.NgayLamViec.Year == 2026 && p.NgayLamViec.Month == 10);
var octAssignmentsCount = await context.PhanCongDuAns
    .Where(p => p.MaPhanCa.HasValue && p.PhanCa!.NgayLamViec.Month == 10)
    .CountAsync();
var octActualWorkCount = await context.CongSuKiens
    .Where(c => c.PhanCong!.MaPhanCa.HasValue && c.PhanCong!.PhanCa!.NgayLamViec.Month == 10)
    .CountAsync();

Console.WriteLine("======================================================================");
Console.WriteLine("HOÀN TẤT! DỮ LIỆU CHỈ CÒN THÁNG 10/2026:");
Console.WriteLine("======================================================================");
Console.WriteLine($"- Số sự kiện tháng 10: {octEvents.Count}");
foreach (var e in octEvents)
{
    Console.WriteLine($"   * [{e.MaDuAn}] {e.TenDuAn} ({HotelEventRules.Local(e.NgayBatDau):dd/MM/yyyy}) - {e.PhanCongs.Count} nhân sự");
}
Console.WriteLine($"- Số ca phân tháng 10: {octShiftsCount} ca");
Console.WriteLine($"- Số ca phân tháng 11: {await context.PhanCas.CountAsync(p => p.NgayLamViec >= novStart && p.NgayLamViec <= novEnd)} ca");
Console.WriteLine($"- Phân công sự kiện gắn ca tháng 10: {octAssignmentsCount}");
Console.WriteLine($"- Giờ công sự kiện thực tế tháng 10: {octActualWorkCount}");

return 0;
