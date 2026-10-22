using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

/// <summary>
/// Recomputes from committed leave approvals and current schedules on every read.
/// No synthetic punches, schema migration, or accumulated credits; safe to recompute before payroll closing.
/// </summary>
public class AttendanceSummaryService(AppDbContext context) : IAttendanceSummaryService
{
    public async Task<AttendanceSummaryDto?> GetAsync(int employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        AttendanceSummaryCalculator.ValidatePeriod(from, to);
        var employee = await context.NhanViens.AsNoTracking()
            .Where(e => e.MaNhanVien == employeeId)
            .Select(e => new { e.Ho, e.Ten }).SingleOrDefaultAsync(cancellationToken);
        if (employee == null) return null;

        var shifts = await context.PhanCas.AsNoTracking()
            .Where(s => s.TrangThai != "DA_HUY" && s.MaNhanVien == employeeId && s.NgayLamViec >= from && s.NgayLamViec <= to)
            .Include(s => s.ChamCong).Include(s => s.CaLamViec).ToListAsync(cancellationToken);
        var requests = await context.DonTus.AsNoTracking()
            .Where(r => r.MaNhanVien == employeeId && r.TrangThai == "DA_DUYET" &&
                r.LoaiDon != null && r.LoaiDon.CoHuongLuong && r.TuNgay <= to && r.DenNgay >= from)
            .Include(r => r.LoaiDon).ToListAsync(cancellationToken);

        return AttendanceSummaryCalculator.Calculate(employeeId, $"{employee.Ho} {employee.Ten}".Trim(),
            from, to, shifts, requests);
    }
}
