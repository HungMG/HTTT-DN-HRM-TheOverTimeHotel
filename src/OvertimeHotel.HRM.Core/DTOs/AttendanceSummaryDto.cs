namespace OvertimeHotel.HRM.Core.DTOs;

public record AttendanceShiftDto(int Id, string Name);

public record PaidLeaveSourceDto(
    int RequestId, string LeaveType, DateOnly FromDate, DateOnly ToDate, DateTimeOffset? ApprovedAt);

public record AttendanceDayDto(
    DateOnly Date,
    List<AttendanceShiftDto> Shifts,
    bool HasWorked,
    bool HasAttendance,
    List<PaidLeaveSourceDto> LeaveRequests)
{
    public bool HasSchedule => Shifts.Count > 0;
    public bool HasPaidLeave => LeaveRequests.Count > 0;
    public bool CountsAsPaidLeave => HasSchedule && HasPaidLeave && !HasWorked;
    public bool NeedsReview => HasPaidLeave && HasAttendance;
    public int CreditedDays => HasWorked || CountsAsPaidLeave ? 1 : 0;
}

/// <summary>
/// Live day-based input for payroll, not a closed payroll snapshot.
/// WorkedDays and PaidLeaveDays are disjoint; OT/night/meal allowances must use actual attendance.
/// </summary>
public record AttendanceSummaryDto(
    int EmployeeId, string EmployeeName, DateOnly FromDate, DateOnly ToDate,
    List<AttendanceDayDto> Days, string Source = "production")
{
    public int ScheduledDays => Days.Count(d => d.HasSchedule);
    public int WorkedDays => Days.Count(d => d.HasWorked);
    public int PaidLeaveDays => Days.Count(d => d.CountsAsPaidLeave);
    public int TotalCreditedDays => Days.Sum(d => d.CreditedDays);
    public int ReviewDays => Days.Count(d => d.NeedsReview);
}

public record AttendanceApiError(string Code, string Message);
