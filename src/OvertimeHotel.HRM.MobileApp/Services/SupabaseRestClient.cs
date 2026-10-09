using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Lớp bọc mỏng quanh PostgREST dùng chung cho các service đọc/ghi dữ liệu nghiệp vụ.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao có lớp này.</b> Trước đây <c>LeaveApprovalService</c> và
/// <c>AttendanceService</c> gọi qua cổng API của WebAdmin, mỗi service tự dựng
/// HttpClient và tự xử lý lỗi. Sau khi chuyển sang gọi thẳng Supabase (đúng
/// PRD 06), cả hai cùng cần: gắn apikey + Bearer token, đọc lỗi của PostgREST,
/// và phân tích JSON mà không để <see cref="JsonElement"/> sống quá vòng đời của
/// <see cref="JsonDocument"/>. Gom vào đây để không lặp lại ba lớp đó.
/// </para>
///
/// <para>
/// <b>Đường dẫn phải là tương đối, KHÔNG dấu "/" ở đầu.</b> BaseAddress đã kết
/// thúc bằng "/rest/v1/", nên RFC 3986 quyết định đường dẫn bắt đầu bằng "/" sẽ
/// xoá sạch "/rest/v1" rồi gõi thẳng vào gốc domain. Hàm <see cref="BuildUri"/>
/// tự cắt dấu "/" cho an toàn thay vì trông chời mỗi caller nhớ quy tắc.
/// </para>
///
/// <para>
/// <b>Ranh giới bảo mật nằm ở RLS, không ở app.</b> App gửi khoá anon công khai
/// và JWT của chính người dùng; policy trên Supabase là lớp chặn duy nhất. Vì vậy
/// service này KHÔNG tự lọc quyền rồi tin là an toàn — mọi phép lọc phải nằm ở
/// phía CSDL, và client chỉ chịu trách nhiệm hiển thị đúng.
/// </para>
/// </remarks>
public sealed class SupabaseRestClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<SupabaseRestClient> _logger;

    /// <summary>Dựng client mới trỏ <see cref="ApiConfig.RestBaseUrl"/>.</summary>
    public SupabaseRestClient(IAuthService authService, ILogger<SupabaseRestClient> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.RestBaseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };

        // apikey KHÔNG phải xác thực — nó chỉ định danh project. Ranh giới bảo mật
        // thật là RLS, và RLS lại đọc claim trong header Authorization. Header này
        // không có thuộc tính mạnh trong HttpRequestHeaders nên thêm dạng chuỗi.
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("apikey", ApiConfig.SupabaseAnonKey);
    }

    /// <summary>True khi lần gọi cuối bị PostgREST từ chối vì policy RLS chặn.</summary>
    public bool WasDeniedByPolicy { get; private set; }

    /// <summary>
    /// True khi thao tác ghi không khớp dòng nào — tức đối tượng không còn ở trạng
    /// thái dự kiến (ví dụ đơn đã được người khác duyệt trước đó).
    /// </summary>
    /// <remarks>
    /// Cần phân biệt với <see cref="WasDeniedByPolicy"/>: một là "việc này đã có
    /// người khác làm xong" (nên báo tải lại danh sách), một là "bạn không có
    /// quyền" (nên báo thẩm quyền). Gộp hai thông điệp khiến người dùng không
    /// biết phải làm gì tiếp.
    /// </remarks>
    public bool NoRowsMatched { get; private set; }

    /// <summary>GET một bảng, trả về thân JSON thô (đã tách khỏi JsonDocument).</summary>
    public async Task<string> GetAsync(string tableAndQuery, CancellationToken cancellationToken = default)
    {
        ResetCallState();
        var uri = BuildUri(tableAndQuery);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        await AttachTokenAsync(request);
        return await SendAsync(request, uri, cancellationToken);
    }

    /// <summary>
    /// Đếm số dòng khớp điều kiện mà KHÔNG tải nội dung — đọc header
    /// <c>Content-Range</c> mà PostgREST trả về khi có
    /// <c>Prefer: count=exact</c> và phương thức HEAD.
    /// </summary>
    public async Task<int> CountAsync(string tableAndQuery, CancellationToken cancellationToken = default)
    {
        ResetCallState();
        var uri = BuildUri(tableAndQuery);
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        await AttachTokenAsync(request);

        HttpStatusCode status;
        string? contentRange;
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            status = response.StatusCode;

            // Content-Range là CONTENT header (HttpContentHeaders), không phải
            // response header — tra cứu trên response.Headers sẽ không thấy.
            // Và phải đọc ra chuỗi ngay trong using: dispose response là mất header.
            // PostgREST emits item ranges such as "0-39/1205", without the
            // "bytes" unit required by .NET's strongly typed ContentRange parser.
            contentRange = response.Content.Headers.TryGetValues("Content-Range", out var ranges)
                ? ranges.FirstOrDefault()
                : null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được Supabase PostgREST tại {Uri}", uri);
            throw new HttpRequestException("Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối mạng.", ex);
        }

        if (status != HttpStatusCode.OK && status != HttpStatusCode.NoContent && status != HttpStatusCode.PartialContent)
        {
            throw BuildException(status, string.Empty, uri);
        }

        return ReadTotalCount(contentRange);
    }

    /// <summary>
    /// Ghi một dòng. <paramref name="json"/> là body đã serialize; trả về JSON
    /// phản hồi (PostgREST trả chính dòng vừa ghi nhờ <c>Prefer: return=representation</c>).
    /// </summary>
    public async Task<string> PostAsync(string table, string json, CancellationToken cancellationToken = default)
    {
        ResetCallState();
        var uri = BuildUri(table);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        await AttachTokenAsync(request);
        return await SendAsync(request, uri, cancellationToken);
    }

    /// <summary>UPDATE những dòng khớp điều kiện trong truy vấn.</summary>
    public async Task<string> PatchAsync(string tableAndQuery, string json, CancellationToken cancellationToken = default)
    {
        ResetCallState();
        var uri = BuildUri(tableAndQuery);
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        await AttachTokenAsync(request);
        return await SendAsync(request, uri, cancellationToken);
    }

    /// <summary>
    /// Chuyển thành URI tuyệt đối, tự bỏ dấu "/" ở đầu đường dẫn tương đối.
    /// </summary>
    private static Uri BuildUri(string tableAndQuery) =>
        new(new Uri(ApiConfig.RestBaseUrl), tableAndQuery.TrimStart('/'));

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, Uri uri, CancellationToken cancellationToken)
    {
        HttpStatusCode status;
        string body;

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Lỗi đường truyền: mất mạng, DNS, timeout. Ném lại nguyên văn để tầng
            // trên phân biệt với các lỗi nghiệp vụ bên dưới.
            _logger.LogError(ex, "Không kết nối được Supabase PostgREST tại {Uri}", uri);
            throw new HttpRequestException("Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối mạng.", ex);
        }

        // PATCH không khớp dòng nào trả 204 với body rỗng — KHÔNG phải lỗi, mà là
        // tín hiệu "đối tượng đã đổi trạng thái". Người gọi đọc NoRowsMatched.
        if (status == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(body))
        {
            if (request.Method != HttpMethod.Get)
            {
                NoRowsMatched = true;
            }

            return string.Empty;
        }

        if (status == HttpStatusCode.OK || status == HttpStatusCode.Created)
        {
            return body;
        }

        throw BuildException(status, body, uri);
    }

    /// <summary>Đặt lại cờ trạng thái của lần gọi trước khi bắt đầu lần mới.</summary>
    private void ResetCallState()
    {
        WasDeniedByPolicy = false;
        NoRowsMatched = false;
    }

    /// <summary>
    /// Dịch mã lỗi của PostgREST sang thông điệp tiếng Việt cho người dùng.
    /// </summary>
    /// <remarks>
    /// PostgREST trả thân trạng rút gọn trong <c>message</c> (vd "permission denied
    /// for table don_tu") kèm mã PostgreSQL trong <c>code</c> (42501) và gợi ý
    /// trong <c>hint</c>. Cả ba đều là tiếng Anh kỹ thuật nên không đưa thẳng lên
    /// giao diện — người dùng chỉ cần biết "không có quyền" hay "dữ liệu lỗi".
    /// </remarks>
    private Exception BuildException(HttpStatusCode status, string body, Uri uri)
    {
        var (code, message, hint) = ReadPostgrestError(body);

        // 42501 = insufficient_privilege (RLS chặn). 404 kèm nhắc "policy" nghĩa
        // là PostgREST không tìm thấy policy nào cho thao tác đó.
        WasDeniedByPolicy = code == "42501"
            || status == HttpStatusCode.Forbidden
            || (status == HttpStatusCode.NotFound && hint.Contains("policy", StringComparison.OrdinalIgnoreCase));

        _logger.LogError(
            "PostgREST từ chối {Uri}: HTTP {StatusCode}, mã {Code}, chi tiết {Detail}",
            uri, status, code ?? "-", message ?? "-");

        if (status == HttpStatusCode.Unauthorized)
        {
            return new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        if (status == HttpStatusCode.Forbidden || (status == HttpStatusCode.NotFound && WasDeniedByPolicy))
        {
            return new InvalidOperationException("Bạn không có thẩm quyền thực hiện thao tác này.");
        }

        if (status == HttpStatusCode.BadRequest)
        {
            // 23503/23505/22P02/23502 = vi phạm ràng buộc CSDL: khoá ngoại sai,
            // trùng khoá duy nhất, sai kiểu dữ liệu, hoặc cột không tồn tại.
            return new InvalidOperationException(
                code is "23503" or "23505" or "22P02" or "23502" or "PGRST204"
                    ? "Dữ liệu gửi lên không hợp lệ với cơ sở dữ liệu."
                    : "Máy chủ từ chối yêu cầu. Vui lòng thử lại.");
        }

        return new InvalidOperationException($"Lỗi máy chủ ({status}). Vui lòng thử lại sau.");
    }

    private static (string? Code, string? Message, string Hint) ReadPostgrestError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null, string.Empty);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? Read(string name) =>
                root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                    ? prop.GetString()
                    : null;

            return (Read("code"), Read("message"), Read("hint") ?? string.Empty);
        }
        catch (JsonException)
        {
            // Không phải JSON của PostgREST (vd gateway trả HTML): coi như lỗi
            // chung, không ném parse exception làm hỏng luồng xử lý lỗi.
            return (null, null, string.Empty);
        }
    }

    /// <summary>
    /// Đọc tổng số dòng từ header <c>Content-Range</c> có dạng "0-24/25" → 25.
    /// </summary>
    /// <remarks>
    /// Không có header thì trả 0 thay vì ném lỗi: badge đếm sai còn hơn làm hỏng
    /// cả màn hình. Giá trị "*" (PostgREST không đếm được) cũng rơi vào 0.
    /// </remarks>
    private static int ReadTotalCount(string? contentRange)
    {
        if (string.IsNullOrEmpty(contentRange))
        {
            return 0;
        }

        var raw = contentRange;
        var slash = raw.LastIndexOf('/');
        if (slash < 0 || slash == raw.Length - 1)
        {
            return 0;
        }

        var tail = raw[(slash + 1)..].Trim();
        return int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;
    }
}
