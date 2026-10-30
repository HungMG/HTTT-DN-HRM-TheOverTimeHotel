using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class ShiftSwapService(WebAdminApiClient api) : IShiftSwapService
{
    private const string Root = "api/mobile/shift-swaps";
    public bool IsDemoMode => false;
    public Task<List<EligibleMyShiftDto>> GetEligibleMyShiftsAsync(CancellationToken cancellationToken = default) => api.GetAsync<List<EligibleMyShiftDto>>($"{Root}/eligible-my-shifts", cancellationToken);
    public Task<List<EligiblePartnerShiftDto>> GetEligiblePartnerShiftsAsync(CancellationToken cancellationToken = default) => api.GetAsync<List<EligiblePartnerShiftDto>>($"{Root}/eligible-partner-shifts", cancellationToken);
    public Task<List<ShiftSwapItemDto>> GetMyShiftSwapsAsync(CancellationToken cancellationToken = default) => api.GetAsync<List<ShiftSwapItemDto>>(Root, cancellationToken);
    public async Task<ShiftSwapItemDto?> GetShiftSwapDetailAsync(int id, CancellationToken cancellationToken = default) => await api.GetAsync<ShiftSwapItemDto>($"{Root}/{id}", cancellationToken);
    public Task<ShiftSwapOperationResult> CreateShiftSwapAsync(CreateShiftSwapRequest request) => Send(Root, request);
    public Task<ShiftSwapOperationResult> RespondShiftSwapAsync(int id, RespondShiftSwapRequest request) => Send($"{Root}/{id}/respond", request);
    public Task<ShiftSwapOperationResult> WithdrawShiftSwapAsync(int id) => Send($"{Root}/{id}/withdraw");

    private async Task<ShiftSwapOperationResult> Send(string path, object? body = null)
    {
        try { return await api.PostAsync<ShiftSwapOperationResult>(path, body); }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            return new ShiftSwapOperationResult { Success = false, Message = ex is TaskCanceledException ? "Kết nối hết thời gian. Vui lòng thử lại." : ex.Message };
        }
    }
}
