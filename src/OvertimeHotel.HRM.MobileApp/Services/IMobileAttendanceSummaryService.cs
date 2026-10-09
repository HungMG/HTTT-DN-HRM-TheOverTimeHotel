using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>Monthly worked days and paid leave, independent of check-in/check-out.</summary>
public interface IMobileAttendanceSummaryService
{
    bool IsDemoMode { get; }
    Task<AttendanceSummaryDto> GetSummaryAsync(int year, int month, int? employeeId = null,
        CancellationToken cancellationToken = default);
}
