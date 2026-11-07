using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Services;

public enum SessionStatus { Valid, Invalid, Unavailable }

public static class SessionStatusProbe
{
    public static async Task<SessionStatus> CheckAsync(HttpClient client, string token, int employeeId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"nhan_vien?select=ma_nhan_vien&ma_nhan_vien=eq.{employeeId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return SessionStatus.Invalid;
            if (!response.IsSuccessStatusCode) return SessionStatus.Unavailable;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.ValueKind != JsonValueKind.Array) return SessionStatus.Unavailable;
            return json.RootElement.GetArrayLength() == 0 ? SessionStatus.Invalid : SessionStatus.Valid;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return SessionStatus.Unavailable;
        }
    }
}
