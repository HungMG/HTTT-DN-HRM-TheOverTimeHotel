using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Services;

public static class AttendanceSummaryCalculator
{
    public static void ValidatePeriod(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 365)
            throw new ArgumentException("Khoảng tính công phải từ 1 đến 366 ngày.");
    }

    public static AttendanceSummaryDto Calculate(int employeeId, string employeeName,
        DateOnly from, DateOnly to, IEnumerable<PhanCa> shifts, IEnumerable<DonTu> requests,
        string source = "production")
    {
        ValidatePeriod(from, to);
        // Night shifts belong to NgayLamViec, never the checkout date or the device timezone.
        var shiftsByDay = shifts
            // Mã vắng của phan_ca trong CSDL là VANG_MAT (không phải "VANG").
            .Where(s => s.MaNhanVien == employeeId && s.NgayLamViec >= from && s.NgayLamViec <= to &&
                s.TrangThai is "DA_PHAN" or "HOAN_THANH" or "VANG_MAT")
            .GroupBy(s => s.NgayLamViec)
            .ToDictionary(g => g.Key, g => g.DistinctBy(s => s.MaPhanCa).ToList());

        var leavesByDay = new Dictionary<DateOnly, List<PaidLeaveSourceDto>>();
        foreach (var request in requests.Where(r => r.MaNhanVien == employeeId &&
                     r.TrangThai == "DA_DUYET" && r.LoaiDon?.CoHuongLuong == true &&
                     r.TuNgay <= r.DenNgay && r.TuNgay <= to && r.DenNgay >= from)
                     .DistinctBy(r => r.MaDon).OrderBy(r => r.MaDon))
        {
            var leave = new PaidLeaveSourceDto(request.MaDon, request.LoaiDon!.TenLoaiDon,
                request.TuNgay, request.DenNgay, request.NgayDuyet);
            var start = Math.Max(from.DayNumber, request.TuNgay.DayNumber);
            var end = Math.Min(to.DayNumber, request.DenNgay.DayNumber);
            for (var number = start; number <= end; number++)
            {
                var date = DateOnly.FromDayNumber(number);
                if (!leavesByDay.TryGetValue(date, out var entries))
                    leavesByDay[date] = entries = [];
                entries.Add(leave);
            }
        }

        var days = shiftsByDay.Keys.Union(leavesByDay.Keys).Order().Select(date =>
        {
            var dayShifts = shiftsByDay.GetValueOrDefault(date) ?? [];
            // An open punch is not yet a completed worked day. Never infer work from default status alone.
            var worked = dayShifts.Any(s => s.ChamCong is { GioCheckIn: not null, GioCheckOut: not null } a &&
                a.GioCheckOut > a.GioCheckIn && a.TrangThai is "DUNG_GIO" or "DI_TRE" or "VE_SOM" or "TRE_VA_SOM");
            var hasAttendance = dayShifts.Any(s => s.ChamCong is { } a &&
                (a.GioCheckIn.HasValue || a.GioCheckOut.HasValue));
            return new AttendanceDayDto(date,
                dayShifts.OrderBy(s => s.BatDauDuKien).Select(s =>
                    new AttendanceShiftDto(s.MaPhanCa, s.CaLamViec?.TenCa ?? $"Ca #{s.MaCa}")).ToList(),
                worked, hasAttendance, leavesByDay.GetValueOrDefault(date) ?? []);
        }).ToList();

        return new AttendanceSummaryDto(employeeId, employeeName, from, to, days, source);
    }
}
