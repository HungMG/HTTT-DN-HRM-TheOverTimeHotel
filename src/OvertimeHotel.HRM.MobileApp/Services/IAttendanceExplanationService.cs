using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public interface IAttendanceExplanationService
{
    bool IsDemoMode { get; }
    Task<List<EligibleShiftDto>> GetEligibleShiftsAsync(CancellationToken cancellationToken = default);
    Task<List<AttendanceExplanationItemDto>> GetMyExplanationsAsync(CancellationToken cancellationToken = default);
    Task<AttendanceExplanationItemDto?> GetExplanationDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<AttendanceExplanationOperationResult> SubmitExplanationAsync(CreateAttendanceExplanationRequest request);
    Task<AttendanceExplanationOperationResult> WithdrawExplanationAsync(int id);
}
