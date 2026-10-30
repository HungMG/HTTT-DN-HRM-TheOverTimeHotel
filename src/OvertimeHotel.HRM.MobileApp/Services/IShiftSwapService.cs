using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public interface IShiftSwapService
{
    bool IsDemoMode { get; }

    Task<List<EligibleMyShiftDto>> GetEligibleMyShiftsAsync(CancellationToken cancellationToken = default);

    Task<List<EligiblePartnerShiftDto>> GetEligiblePartnerShiftsAsync(CancellationToken cancellationToken = default);

    Task<List<ShiftSwapItemDto>> GetMyShiftSwapsAsync(CancellationToken cancellationToken = default);

    Task<ShiftSwapItemDto?> GetShiftSwapDetailAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftSwapOperationResult> CreateShiftSwapAsync(CreateShiftSwapRequest request);

    Task<ShiftSwapOperationResult> RespondShiftSwapAsync(int id, RespondShiftSwapRequest request);

    Task<ShiftSwapOperationResult> WithdrawShiftSwapAsync(int id);
}
