using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ chấm công từ Production kết nối Backend WebAdmin qua Mobile REST API có JWT.
/// Mọi phép đối soát ca do máy chủ thực hiện, tuyệt đối không fallback giả mạo kết quả.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<AttendanceService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsDemoMode => false;

    public AttendanceService(
        IAuthService authService,
        ILogger<AttendanceService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<TodayShiftDto> GetTodayShiftAsync()
    {
        var json = await SendAsync(HttpMethod.Get, "/api/mobile/attendance/today");
        return Deserialize<TodayShiftDto>(json, new TodayShiftDto(0, string.Empty, default, default, null, null));
    }

    public Task<CheckInResponse> CheckInAsync(int maPhanCa, string? toaDo, string? thietBi) =>
        SendCheckAsync("/api/mobile/attendance/check-in", maPhanCa, toaDo, thietBi);

    public Task<CheckInResponse> CheckOutAsync(int maPhanCa, string? toaDo, string? thietBi) =>
        SendCheckAsync("/api/mobile/attendance/check-out", maPhanCa, toaDo, thietBi);

    public async Task<List<MonthlyAttendanceDto>> GetMonthlyAttendanceAsync(int year, int month)
    {
        var json = await SendAsync(HttpMethod.Get, $"/api/mobile/attendance/month?year={year}&month={month}");
        return Deserialize<List<MonthlyAttendanceDto>>(json, new List<MonthlyAttendanceDto>());
    }

    /// <summary>
    /// Gửi yến cầu quẹt thẻ kèm mốc thời gian và thông tin định vị của thiết bị.
    /// </summary>
    private async Task<CheckInResponse> SendCheckAsync(string path, int maPhanCa, string? toaDo, string? thietBi)
    {
        var payload = new CheckInRequest(
            MaPhanCa: maPhanCa,
            Timestamp: DateTimeOffset.Now,
            PhuongThuc: toaDo is null ? "WIFI" : "GPS",
            ToaDo: toaDo,
            ThietBi: thietBi);

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var json = await SendAsync(HttpMethod.Post, path, jsonContent);

        return Deserialize<CheckInResponse>(json, new CheckInResponse(false, "Không đọc được phản hồi từ máy chủ."));
    }

    /// <summary>
    /// Thực thi HTTP request kèm Bearer token và chuẩn hoá lỗi theo mã trạng thái.
    /// </summary>
    private async Task<string> SendAsync(HttpMethod method, string path, HttpContent? content = null)
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Không tìm thấy JWT token khi gọi {Path}.", path);
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return json;
            }

            _logger.LogError("Lỗi chấm công từ máy chủ: HTTP {StatusCode} - {Content}", response.StatusCode, json);

            // Máy chủ trả kèm thông điệp tiếng Việt trong body, ưu tiên hiển thị thông điệp đó.
            var serverMessage = TryReadServerMessage(json);

            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException(serverMessage ?? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
                case System.Net.HttpStatusCode.Forbidden:
                    throw new InvalidOperationException(serverMessage ?? "Bạn không có thẩm quyền thực hiện thao tác này.");
                case System.Net.HttpStatusCode.NotFound:
                    throw new InvalidOperationException(serverMessage ?? "Không tìm thấy ca làm việc của bạn.");
                case System.Net.HttpStatusCode.Conflict:
                    throw new InvalidOperationException(serverMessage ?? "Thao tác này đã được thực hiện trước đó.");
                case System.Net.HttpStatusCode.BadRequest:
                    throw new InvalidOperationException(serverMessage ?? "Dữ liệu gửi lên không hợp lệ.");
                case System.Net.HttpStatusCode.ServiceUnavailable:
                    throw new InvalidOperationException(serverMessage ?? "Máy chủ đang bận. Vui lòng thử lại sau.");
                default:
                    // Ném ngoài họ HttpRequestException để không bị bắt nhầm thành lỗi đường truyền.
                    throw new InvalidOperationException($"Lỗi máy chủ ({response.StatusCode}). Vui lòng thử lại sau.");
            }
        }
        catch (HttpRequestException ex)
        {
            // Chỉ bắt lỗi đường truyền. UnauthorizedAccessException / InvalidOperationException
            // ném ở trên không thuộc họ này nên vẫn báo nguyên vẹn tới tầng giao diện.
            _logger.LogError(ex, "Lỗi kết nối tới API WebAdmin tại {BaseUrl}", ApiConfig.BaseUrl);
            throw new HttpRequestException("Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối mạng.", ex);
        }
    }

    /// <summary>
    /// Cố gắng lấy thông điệp lỗi tiếng Việt do máy chủ trả về, không có thì trả null.
    /// </summary>
    private string? TryReadServerMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CheckInResponse>(json, _jsonOptions);
            return string.IsNullOrWhiteSpace(parsed?.Message) ? null : parsed.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private T Deserialize<T>(string json, T fallback) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? fallback;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không giải mã được phản hồi từ máy chủ chấm công.");
            return fallback;
        }
    }
}