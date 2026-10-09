using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.Data.Services;

public interface IAttendanceSummaryService
{
    Task<AttendanceSummaryDto?> GetAsync(int employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}
