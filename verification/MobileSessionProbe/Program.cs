using System.Net;
using OvertimeHotel.HRM.MobileApp.Services;
foreach (var (status, body, expected) in new[] {
    (HttpStatusCode.OK, "[{\"ma_nhan_vien\":1}]", SessionStatus.Valid),
    (HttpStatusCode.OK, "[]", SessionStatus.Invalid),
    (HttpStatusCode.Unauthorized, "", SessionStatus.Invalid),
    (HttpStatusCode.InternalServerError, "", SessionStatus.Unavailable),
    (HttpStatusCode.OK, "not json", SessionStatus.Unavailable)
}) {
    using var client = new HttpClient(new Handler(status, body)) { BaseAddress = new Uri("https://example.invalid/rest/v1/") };
    var result = await SessionStatusProbe.CheckAsync(client, "token", 1);
    if (result != expected) throw new Exception($"{status}/{body}: expected {expected}, got {result}");
    Console.WriteLine($"PASS {status}/{expected}");
}
using (var client = new HttpClient(new OfflineHandler()) { BaseAddress = new Uri("https://example.invalid/rest/v1/") }) {
    if (await SessionStatusProbe.CheckAsync(client, "token", 1) != SessionStatus.Unavailable) throw new Exception("Network failure must not log out the user");
    Console.WriteLine("PASS offline/Unavailable");
}
sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
  if (request.Headers.Authorization?.Parameter != "token" || request.RequestUri?.Query != "?select=ma_nhan_vien&ma_nhan_vien=eq.1") throw new Exception("Wrong session probe request");
  return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
 }
}
sealed class OfflineHandler : HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new HttpRequestException("offline");
}
