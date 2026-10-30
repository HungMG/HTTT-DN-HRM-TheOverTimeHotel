using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class AttendanceExplanationService(WebAdminApiClient api) : IAttendanceExplanationService
{
    private const string Root = "api/mobile/attendance-explanations";
    public bool IsDemoMode => false;
    public Task<List<EligibleShiftDto>> GetEligibleShiftsAsync(CancellationToken cancellationToken = default) => api.GetAsync<List<EligibleShiftDto>>($"{Root}/eligible-shifts", cancellationToken);
    public Task<List<AttendanceExplanationItemDto>> GetMyExplanationsAsync(CancellationToken cancellationToken = default) => api.GetAsync<List<AttendanceExplanationItemDto>>(Root, cancellationToken);
    public async Task<AttendanceExplanationItemDto?> GetExplanationDetailAsync(int id, CancellationToken cancellationToken = default) => await api.GetAsync<AttendanceExplanationItemDto>($"{Root}/{id}", cancellationToken);
    public Task<AttendanceExplanationOperationResult> SubmitExplanationAsync(CreateAttendanceExplanationRequest request) => Send(Root, request);
    public Task<AttendanceExplanationOperationResult> WithdrawExplanationAsync(int id) => Send($"{Root}/{id}/withdraw");

    private async Task<AttendanceExplanationOperationResult> Send(string path, object? body = null)
    {
        try { return await api.PostAsync<AttendanceExplanationOperationResult>(path, body); }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            return new AttendanceExplanationOperationResult(false, "ERROR", ex is TaskCanceledException ? "Kết nối hết thời gian. Vui lòng thử lại." : ex.Message);
        }
    }
}
