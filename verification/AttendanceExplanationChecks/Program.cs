using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Services;

var rest = new SupabaseRestClient();
var service = new AttendanceExplanationService(rest);
rest.Handler = (path, body, ct) => {
 Check(path == "rpc/hrm_attendance_explanation", "Uses Supabase RPC");
 using var json = JsonDocument.Parse(body);
 var payload = json.RootElement.GetProperty("p_payload");
 Check(json.RootElement.GetProperty("p_action").GetString() == "create", "Create action");
 Check(payload.GetProperty("in").GetDateTimeOffset() == DateTimeOffset.Parse("2026-11-01T23:00:00Z"), "UTC instant");
 Check(payload.GetProperty("in").GetDateTimeOffset().Offset == TimeSpan.Zero, "Normalizes +07 to UTC");
 Check(!payload.TryGetProperty("TrangThai", out _), "No preapproval payload");
 return Task.FromResult("{\"Success\":true,\"Code\":\"CREATED\",\"Message\":\"ok\",\"MaGiaiTrinh\":42}");
};
var result = await service.SubmitExplanationAsync(new(11, DateTimeOffset.Parse("2026-11-02T06:00:00+07:00"), null, "test", null));
Check(result.Success && result.MaGiaiTrinh == 42, "Reads operation response");
rest.Handler = (_, _, _) => Task.FromResult("[{\"MaPhanCa\":11,\"NgayLamViec\":\"2026-11-02\",\"MaCa\":1,\"TenCa\":\"Sáng\",\"GioBatDau\":\"06:00:00\",\"GioKetThuc\":\"14:00:00\",\"GioVaoThucTe\":null,\"GioRaThucTe\":null,\"TrangThaiChamCong\":null}]");
Check((await service.GetEligibleShiftsAsync())[0].GioBatDau == new TimeOnly(6,0), "DateOnly and TimeOnly mapping");
rest.Handler = (_, _, _) => Task.FromResult("null");
Check(await service.GetExplanationDetailAsync(999) == null, "Absent detail stays null");
rest.Handler = (_, _, _) => throw new InvalidOperationException("Ca đang chờ xử lý");
Check(!(await service.WithdrawExplanationAsync(1)).Success, "Operation preserves server error");
using var cts = new CancellationTokenSource(); cts.Cancel();
rest.Handler = (_, _, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult("[]"); };
bool cancelled = false; try { await service.GetMyExplanationsAsync(cts.Token); } catch(OperationCanceledException) { cancelled = true; }
Check(cancelled, "Read cancellation reaches HTTP client");
Console.WriteLine("PASS: RPC routing, UTC payload, DTO times, null detail, errors and cancellation");
static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
namespace OvertimeHotel.HRM.MobileApp.Services {
 public sealed class SupabaseRestClient {
  public Func<string,string,CancellationToken,Task<string>> Handler { get; set; } = (_,_,_) => Task.FromResult("[]");
  public Task<string> PostAsync(string path,string body,CancellationToken ct=default) => Handler(path,body,ct);
 }
}
