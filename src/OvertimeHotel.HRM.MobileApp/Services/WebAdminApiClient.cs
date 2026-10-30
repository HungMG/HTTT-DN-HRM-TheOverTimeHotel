using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>Authenticated calls to the server that enforces request workflow rules.</summary>
public sealed class WebAdminApiClient
{
    private readonly IAuthService _auth;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WebAdminApiClient(IAuthService auth) : this(auth, CreateHttpClient()) { }

    public WebAdminApiClient(IAuthService auth, HttpClient http)
    {
        _auth = auth;
        _http = http;
    }

    private static HttpClient CreateHttpClient()
    {
        var url = typeof(WebAdminApiClient).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "WebAdminApiUrl")?.Value;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return new HttpClient();
        return new HttpClient { BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsConfigured => _http.BaseAddress != null;

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        await SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    public async Task<T> PostAsync<T>(string path, object? body = null) =>
        await SendAsync<T>(HttpMethod.Post, path, body, CancellationToken.None);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress == null)
            throw new InvalidOperationException("Chưa cấu hình địa chỉ HTTPS của WebAdmin cho ứng dụng.");
        var token = await _auth.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        if (!response.IsSuccessStatusCode)
        {
            var message = "Không thể thực hiện yêu cầu. Vui lòng thử lại.";
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (json.RootElement.TryGetProperty("message", out var element) && element.ValueKind == JsonValueKind.String)
                    message = element.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Máy chủ trả về dữ liệu không hợp lệ.");
    }
}
