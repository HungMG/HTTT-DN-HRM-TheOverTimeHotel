using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Services;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class DemoAttendanceSummaryService(IAuthService authService, DemoLeaveApprovalService leaveService,
    IAttendanceService attendanceService) : IMobileAttendanceSummaryService
{
    public bool IsDemoMode => true;

    public async Task<AttendanceSummaryDto> GetSummaryAsync(int year, int month, int? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (year is < 1 or > 9999 || month is < 1 or > 12 || employeeId is <= 0)
            throw new InvalidOperationException("Tháng, năm hoặc mã nhân viên không hợp lệ.");
        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true) throw new UnauthorizedAccessException("Vui lòng đăng nhập lại.");
        var targetId = employeeId ?? user.MaNhanVien;
        if (targetId == user.MaNhanVien && !user.CanViewAttendanceSummary)
            throw new AttendanceSummaryUnavailableException();
        var requests = leaveService.GetAttendanceSnapshot();
        var target = requests.FirstOrDefault(r => r.MaNhanVien == targetId)?.NhanVien;
        var departmentId = targetId == user.MaNhanVien ? user.MaPhongBan : target?.MaPhongBan;
        if ((targetId != user.MaNhanVien && target == null) ||
            !AttendanceAccessPolicy.CanView(user.TenVaiTro, user.MaNhanVien, user.MaPhongBan, targetId, departmentId))
            throw new InvalidOperationException("Bạn không có quyền xem bảng công này.");

        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var today = DateOnly.FromDateTime(DateTime.Now);
        var shifts = new List<PhanCa>();
        for (var number = from.DayNumber; number <= to.DayNumber; number++)
        {
            var day = DateOnly.FromDayNumber(number);
            if (day.DayOfWeek == DayOfWeek.Sunday) continue;
            var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(6, 0)), TimeSpan.FromHours(7));
            var isLeaveDate = requests.Any(r => r.MaNhanVien == targetId && r.TuNgay <= day && r.DenNgay >= day);
            shifts.Add(new PhanCa
            {
                MaPhanCa = number, MaNhanVien = targetId, MaCa = 1, NgayLamViec = day,
                BatDauDuKien = start, KetThucDuKien = start.AddHours(8),
                CaLamViec = new CaLamViec { TenCa = "Ca sáng · 06:00–14:00" },
                ChamCong = day < today && !isLeaveDate
                    ? new ChamCong { GioCheckIn = start, GioCheckOut = start.AddHours(8) } : null
            });
        }

        if (targetId == user.MaNhanVien && today >= from && today <= to)
        {
            var current = await attendanceService.GetTodayShiftAsync();
            cancellationToken.ThrowIfCancellationRequested();
            shifts.RemoveAll(s => s.NgayLamViec == today);
            if (current.MaPhanCa > 0)
                shifts.Add(new PhanCa
                {
                    MaPhanCa = current.MaPhanCa, MaNhanVien = targetId, MaCa = 1, NgayLamViec = today,
                    BatDauDuKien = current.GioBatDau, KetThucDuKien = current.GioKetThuc,
                    CaLamViec = new CaLamViec { TenCa = current.TenCa },
                    ChamCong = current.DaCheckIn.HasValue || current.DaCheckOut.HasValue
                        ? new ChamCong { GioCheckIn = current.DaCheckIn, GioCheckOut = current.DaCheckOut,
                            TrangThai = current.TrangThai ?? "" } : null
                });
        }

        var name = targetId == user.MaNhanVien ? user.HoTen : target!.Ho;
        return AttendanceSummaryCalculator.Calculate(targetId, name, from, to, shifts, requests, "demo");
    }
}
