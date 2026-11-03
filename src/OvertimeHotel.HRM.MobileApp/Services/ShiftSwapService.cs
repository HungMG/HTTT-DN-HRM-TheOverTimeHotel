using OvertimeHotel.HRM.Core.DTOs;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class ShiftSwapService(SupabaseRestClient rest) : IShiftSwapService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public bool IsDemoMode => false;
    public Task<List<EligibleMyShiftDto>> GetEligibleMyShiftsAsync(CancellationToken cancellationToken = default) => Read<List<EligibleMyShiftDto>>("my-shifts", null, cancellationToken);
    public async Task<List<EligiblePartnerShiftDto>> GetEligiblePartnerShiftsAsync(CancellationToken cancellationToken = default) =>
        JsonSerializer.Deserialize<List<EligiblePartnerShiftDto>>(await rest.PostAsync("rpc/hrm_shift_swap_partner_options", "{}", cancellationToken), JsonOptions)
        ?? throw new InvalidOperationException("Máy chủ trả về danh sách ca đổi không hợp lệ.");
    public Task<List<ShiftSwapItemDto>> GetMyShiftSwapsAsync(CancellationToken cancellationToken = default) => Read<List<ShiftSwapItemDto>>("list", null, cancellationToken);
    public Task<ShiftSwapItemDto?> GetShiftSwapDetailAsync(int id, CancellationToken cancellationToken = default) => Rpc<ShiftSwapItemDto>("detail", new { id }, cancellationToken);
    public Task<ShiftSwapOperationResult> CreateShiftSwapAsync(CreateShiftSwapRequest request) => Send("create", new { senderShift = request.MaPhanCaGui, receiverShift = request.MaPhanCaNhan, reason = request.LyDo });
    public Task<ShiftSwapOperationResult> RespondShiftSwapAsync(int id, RespondShiftSwapRequest request) => Send("respond", new { id, accept = request.DongY, reason = request.LyDo });
    public Task<ShiftSwapOperationResult> WithdrawShiftSwapAsync(int id) => Send("withdraw", new { id });

    private async Task<T?> Rpc<T>(string action, object? payload, CancellationToken ct = default) =>
        JsonSerializer.Deserialize<T>(await rest.PostAsync("rpc/hrm_shift_swap", JsonSerializer.Serialize(new { p_action = action, p_payload = payload ?? new { } }), ct), JsonOptions);

    private async Task<T> Read<T>(string action, object? payload, CancellationToken ct = default) =>
        await Rpc<T>(action, payload, ct) ?? throw new InvalidOperationException("Máy chủ trả về dữ liệu đổi ca không hợp lệ.");

    private async Task<ShiftSwapOperationResult> Send(string path, object? body = null)
    {
        try { return await Read<ShiftSwapOperationResult>(path, body); }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            return new ShiftSwapOperationResult { Success = false, Message = ex is TaskCanceledException ? "Kết nối hết thời gian. Vui lòng thử lại." : ex.Message };
        }
    }
}
