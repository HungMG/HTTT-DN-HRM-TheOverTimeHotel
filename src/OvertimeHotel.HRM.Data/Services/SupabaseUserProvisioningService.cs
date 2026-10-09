using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OvertimeHotel.HRM.Data.Services;

/// <summary>
/// Hiện thực <see cref="ISupabaseUserProvisioningService"/> gọi Supabase Admin API.
///
/// <para>
/// Cố tình KHÔNG phụ thuộc <c>IConfiguration</c>/<c>IHttpClientFactory</c>: project
/// Data không khai báo package <c>Microsoft.Extensions.*</c> và khoá bí mật phải
/// do Web Admin đọc rồi truyền vào qua constructor. Cách đó cũng đảm bảo khoá
/// không bao giờ nằm trong object của lớp hạ tầng dùng chung.
/// </para>
/// </summary>
public sealed class SupabaseUserProvisioningService : ISupabaseUserProvisioningService, IDisposable
{
    // HttpClient dùng chung cho cả vòng đời ứng dụng: tạo mới ở mỗi lần gọi sẽ
    // rò rỉ socket, còn dispose thì phải theo vòng đời DI chứ không phải theo
    // request. Số request này rất thấp nên một instance là quá đủ.
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly string _supabaseUrl;
    private readonly string? _serviceRoleKey;

    public SupabaseUserProvisioningService(string supabaseUrl, string? serviceRoleKey)
    {
        _supabaseUrl = supabaseUrl?.TrimEnd('/') ?? string.Empty;
        _serviceRoleKey = string.IsNullOrWhiteSpace(serviceRoleKey) ? null : serviceRoleKey.Trim();
    }

    /// <summary>
    /// Khoá phải nằm ở phía SERVER và chỉ có tác dụng ở môi trường đã cấu hình
    /// nó. Không cấu hình thì tính năng báo lỗi rõ ràng thay vì im lặng bỏ qua.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_supabaseUrl) && _serviceRoleKey is not null;

    public async Task<SupabaseUserProvisionResult> CreateUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.NotConfigured,
                "Chưa cấu hình khoá ServiceRoleKey cho Web Admin nên chưa thể cấp tài khoản đăng nhập trên Mobile App. "
                + "Vui lòng liên hệ quản trị viên hệ thống.");
        }

        var normalizedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.Unexpected,
                "Nhân viên chưa có email công vụ nên không thể cấp tài khoản đăng nhập trên Mobile App.");
        }

        // email_confirm = true: không gửi mail xác nhận. Tài khoản do quản trị viên
        // tạo và nhân viên nhận mật khẩu trực tiếp, chờ xác nhận mail chỉ khiến
        // nhân viên không đăng nhập được dù mật khẩu đúng.
        var payload = new
        {
            email = normalizedEmail,
            password,
            email_confirm = true
        };

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/admin/users")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_serviceRoleKey}");

            response = await SharedClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.NetworkFailure,
                "Không kết nối được tới máy chủ Supabase. Tài khoản chưa được cấp, vui lòng thử lại.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.NetworkFailure,
                "Máy chủ Supabase phản hồi quá thời gian cho phép. Tài khoản chưa được cấp, vui lòng thử lại.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content
                    .ReadFromJsonAsync<AdminUserResponse>(cancellationToken);

                if (string.IsNullOrWhiteSpace(created?.Id))
                {
                    return SupabaseUserProvisionResult.Failed(
                        SupabaseUserProvisionError.Unexpected,
                        "Supabase đã tạo tài khoản nhưng không trả về mã định danh. Vui lòng kiểm tra lại trên Supabase Dashboard.");
                }

                return SupabaseUserProvisionResult.Succeeded(created.Id);
            }

            return TranslateErrorStatus(response.StatusCode);
        }
    }

    /// <summary>
    /// Dịch mã lỗi của GoTrue sang thông báo tiếng Việt. Cố tình KHÔNG đính kèm
    /// nội dung response: nó có thể chứa chi tiết cấu hình dự án, không nên đưa
    /// ra UI hay ghi log.
    /// </summary>
    private static SupabaseUserProvisionResult TranslateErrorStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Conflict or HttpStatusCode.BadRequest =>
            SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.EmailAlreadyExists,
                "Email công vụ này đã có tài khoản trên hệ thống. Vui lòng kiểm tra lại hoặc dùng email khác."),

        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            SupabaseUserProvisionResult.Failed(
                SupabaseUserProvisionError.NotConfigured,
                "Khoá cấu hình không hợp lệ nên không thể cấp tài khoản. Vui lòng liên hệ quản trị viên hệ thống."),

        _ => SupabaseUserProvisionResult.Failed(
            SupabaseUserProvisionError.Unexpected,
            "Không tạo được tài khoản đăng nhập trên Mobile App do lỗi máy chủ. Vui lòng thử lại.")
    };

    /// <summary>
    /// Client dùng chung nên vòng đời của nó gắn với tiến trình, không phải với
    /// service. Dispose ở đây chỉ để DI không giữ tham chiếu thừa; không
    /// dispose <see cref="SharedClient"/>.
    /// </summary>
    public void Dispose()
    {
    }

    private sealed class AdminUserResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }
}
