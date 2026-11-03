using OvertimeHotel.HRM.Core.DTOs;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class AttendanceExplanationService(SupabaseRestClient rest) : IAttendanceExplanationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public bool IsDemoMode => false;
    public Task<List<EligibleShiftDto>> GetEligibleShiftsAsync(CancellationToken cancellationToken = default) => Read<List<EligibleShiftDto>>("eligible-shifts", null, cancellationToken);
    public Task<List<AttendanceExplanationItemDto>> GetMyExplanationsAsync(CancellationToken cancellationToken = default) => Read<List<AttendanceExplanationItemDto>>("list", null, cancellationToken);
    public Task<AttendanceExplanationItemDto?> GetExplanationDetailAsync(int id, CancellationToken cancellationToken = default) => Rpc<AttendanceExplanationItemDto>("detail", new { id }, cancellationToken);
    public Task<AttendanceExplanationOperationResult> SubmitExplanationAsync(CreateAttendanceExplanationRequest request) => Send("create", new { shift = request.MaPhanCa, @in = request.GioDeXuatVao?.ToUniversalTime(), @out = request.GioDeXuatRa?.ToUniversalTime(), reason = request.LyDo, evidence = request.MinhChung });
    public Task<AttendanceExplanationOperationResult> WithdrawExplanationAsync(int id) => Send("withdraw", new { id });

    private async Task<T?> Rpc<T>(string action, object? payload, CancellationToken ct = default) =>
        JsonSerializer.Deserialize<T>(await rest.PostAsync("rpc/hrm_attendance_explanation", JsonSerializer.Serialize(new { p_action = action, p_payload = payload ?? new { } }), ct), JsonOptions);

    private async Task<T> Read<T>(string action, object? payload, CancellationToken ct = default) =>
        await Rpc<T>(action, payload, ct) ?? throw new InvalidOperationException("Máy chủ trả về dữ liệu giải trình không hợp lệ.");

    private async Task<AttendanceExplanationOperationResult> Send(string path, object? body = null)
    {
        try { return await Read<AttendanceExplanationOperationResult>(path, body); }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            return new AttendanceExplanationOperationResult(false, "ERROR", ex is TaskCanceledException ? "Kết nối hết thời gian. Vui lòng thử lại." : ex.Message);
        }
    }
}
