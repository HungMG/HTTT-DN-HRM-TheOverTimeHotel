using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Xác thực trực tiếp với Supabase Cloud (GoTrue) và đọc hồ sơ qua PostgREST.
///
/// <para>
/// <b>Vì sao không gọi Web Admin</b> — PRD 06 vẽ Mobile App → Supabase REST/Realtime
/// thẳng, không có bước qua Web Admin. Nếu giữ đường /api/mobile/auth/login thì app
/// lại phụ thuộc máy chủ nhóm đang bật, đúng cái PRD muốn loại bỏ.
/// </para>
///
/// <para>
/// <b>Vai trò lấy ở đâu</b> — KHÔNG do app tự khai. custom_access_token_hook trong
/// migration 20261005_hon_ho_so_jwt_quyen_theo_vai_tro.sql nhét ma_nhan_vien,
/// ma_vai_tro và quyen vào app_metadata mỗi lần GoTrue cấp token; app chỉ đọc.
/// Nhờ vậy app không thể tự nâng quyền — RLS ở Cloud vẫn là lớp chặn cuối.
/// </para>
///
/// <para>
/// <b>Đăng nhập bằng EMAIL, không phải tên đăng nhập</b> — tai_khoan (bảng chứa
/// ten_dang_nhap) đã bị thu hồi toàn bộ quyền cho cả anon lẫn authenticated, nên
/// app không thể tra emp_bao → email. GoTrue chỉ nhận email, vì vậy ô đăng nhập
/// phải là email. (Đã kiểm chứng: curl với 6 tài khoản trả HTTP 200 + claim đúng vai.)
/// </para>
///
/// <para>
/// <b>Hạn token là 1 GIỜ</b>, không phải 8 giờ như JWT Web Admin tự phát. Mà
/// refresh_token trả về chỉ dài 12 ký tự — dấu hiệu project đang đặt
/// flow_type=implicit, khiến refresh token không dùng lại được. Nên hết giờ thì
/// buộc đăng nhập lại; TryAutoLoginAsync xử lý đúng trường hợp đó.
/// </para>
/// </summary>
public class AuthService : IAuthService
{
    private const string JwtStorageKey = "hrm_mobile_jwt_token";

    // GoTrue trả lỗi 400 kèm mã error_code cụ thể; hiển thị mã này cho người dùng
    // giúp phân biệt "sai mật khẩu" với "tài khoản bị khoá", nhưng KHÔNG bao giờ
    // tiết lộ email nào tồn tại (GoTrue trả cùng một mã cho cả hai trường hợp).
    private const string MessageSaiThongTin = "Email hoặc mật khẩu không chính xác.";

    private readonly HttpClient _authClient;
    private readonly HttpClient _restClient;
    private readonly ILogger<AuthService> _logger;

    public bool IsDemoMode => false;
    public UserSession? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null && CurrentUser.IsLoggedIn;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _authClient = CreateClient(ApiConfig.AuthBaseUrl);
        _restClient = CreateClient(ApiConfig.RestBaseUrl);
    }

    private static HttpClient CreateClient(string baseAddress)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // apikey KHÔNG phải xác thực — nó chỉ định danh project. Ranh giới bảo mật
        // thật là RLS, và RLS lại đọc claim trong header Authorization.
        // "apikey" không có thuộc tính mạnh trong HttpRequestHeaders nên phải thêm
        // dạng chuỗi.
        client.DefaultRequestHeaders.TryAddWithoutValidation("apikey", ApiConfig.SupabaseAnonKey);
        return client;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(CurrentUser?.Token))
        {
            return CurrentUser.Token;
        }

        try
        {
            return await SecureStorage.GetAsync(JwtStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc JWT từ SecureStorage.");
            return null;
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResponse(false, "Vui lòng nhập đầy đủ email và mật khẩu.");
        }

        var email = username.Trim();

        try
        {
            var token = await SignInWithPasswordAsync(email, password);
            if (token == null)
            {
                return new LoginResponse(false, MessageSaiThongTin);
            }

            try
            {
                await SecureStorage.SetAsync(JwtStorageKey, token);
            }
            catch (Exception ex)
            {
                // Mất SecureStorage không được làm hỏng phiên hiện tại — chỉ mất
                // khả năng "ghi nhớ đăng nhập" cho lần sau.
                _logger.LogWarning(ex, "Lỗi khi lưu JWT vào SecureStorage.");
            }

            var session = await BuildSessionAsync(token, email);
            if (session == null)
            {
                // Đã đúng mật khẩu nhưng không dựng được phiên: token vô dụng với app.
                SecureStorage.Remove(JwtStorageKey);
                return new LoginResponse(false, "Đăng nhập được nhưng không tải được hồ sơ nhân viên. Vui lòng thử lại.");
            }

            CurrentUser = session;
            _logger.LogInformation(
                "Đăng nhập thành công: NV {MaNhanVien}, vai trò {MaVaiTro}.", session.MaNhanVien, session.TenVaiTro);

            return new LoginResponse(
                Success: true,
                Message: "Đăng nhập thành công.",
                MaTaiKhoan: session.MaTaiKhoan,
                MaNhanVien: session.MaNhanVien,
                HoTen: session.HoTen,
                TenDangNhap: email,
                TenVaiTro: session.TenVaiTro,
                TenPhongBan: session.TenPhongBan,
                TenChucVu: session.TenChucVu,
                Token: token);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được Supabase Cloud tại {BaseUrl}", ApiConfig.SupabaseUrl);
            return new LoginResponse(false, "Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng rồi thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi đăng nhập.");
            return new LoginResponse(false, "Đã xảy ra lỗi trong quá trình đăng nhập. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Gọi GoTrue cấp access token. Trả null khi thông tin đăng nhập sai.
    /// </summary>
    private async Task<string?> SignInWithPasswordAsync(string email, string password)
    {
        var payload = JsonSerializer.Serialize(new { email, password });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        // KHÔNG được viết "/token": đường dẫn tương đối bắt đầu bằng "/" sẽ xoá
        // sạch "/auth/v1" trong BaseAddress (xem ApiConfig.AuthBaseUrl) rồi gõi
        // thẳng vào https://<ref>.supabase.co/token — 404, dễ bị nhầm thành sai mật khẩu.
        using var response = await _authClient.PostAsync("token?grant_type=password", content);

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GoTrue từ chối đăng nhập: HTTP {StatusCode} - {Code}", response.StatusCode, ReadErrorCode(json));
            return null;
        }

        return ReadStringProperty(json, "access_token");
    }

    /// <summary>
    /// Dựng phiên từ JWT: ma_nhan_vien / ma_vai_tro đọc thẳng từ app_metadata do
    /// hook sinh ra, còn tên phòng ban / chức vụ / họ tên thì hỏi PostgREST.
    ///
    /// JWT không mang tên (chỉ mang mã), mà màn Hồ Sơ và thanh tab AppShell đều cần
    /// tên — nên nếu bỏ bước này, mọi phiên sẽ mặc định vai trò "Employee" và quản
    /// lý bị đẩy nhầm sang nhánh nhân viên.
    /// </summary>
    private async Task<UserSession?> BuildSessionAsync(string token, string email)
    {
        var maNhanVien = ReadIntClaim(token, "ma_nhan_vien");
        var maVaiTro = ReadIntClaim(token, "ma_vai_tro");

        // Không có claim vai trò = hook chưa chạy (hoặc tài khoản không có trong
        // tai_khoan). Từ chối đăng nhập thay vì vào app với quyền rỗng — RLS sẽ
        // chặn hết và người dùng thấy màn trắng không hiểu vì sao.
        if (maNhanVien is null or 0 || maVaiTro is null or 0)
        {
            _logger.LogError("JWT không có claim ma_nhan_vien/ma_vai_tro — hook chưa được bật trên Dashboard.");
            return null;
        }

        var profile = await FetchProfileAsync(token, maNhanVien.Value);

        return new UserSession
        {
            Token = token,
            IsLoggedIn = true,
            ThoiGianDangNhap = DateTime.UtcNow,

            // tai_khoan bị thu hồi quyền nên app không biết ma_tai_khoan.
            // LeaveApprovalPolicy chỉ cần vai trò + phòng ban, các truy vấn
            // PostgREST cũng chỉ cần ma_nhan_vien nên để 0 là an toàn.
            MaTaiKhoan = 0,
            MaNhanVien = maNhanVien.Value,
            MaPhongBan = profile?.MaPhongBan,
            TenDangNhap = email,
            HoTen = profile?.HoTen ?? string.Empty,
            DienThoai = profile?.DienThoai ?? string.Empty,
            TenVaiTro = await ResolveRoleNameAsync(token, maVaiTro.Value),
            TenPhongBan = profile?.TenPhongBan ?? string.Empty,
            TenChucVu = profile?.TenChucVu ?? string.Empty
        };
    }

    /// <summary>
    /// Tên vai trò lấy từ bảng vai_tro theo mã trong claim. Rơi về "Employee" để
    /// phần hậu xử lý fail-closed: thiếu tên thì bị coi là quyền thấp nhất, không
    /// phải quyền cao.
    /// </summary>
    private async Task<string> ResolveRoleNameAsync(string token, int maVaiTro)
    {
        try
        {
            var json = await GetRestAsync(token, "/vai_tro?select=ten_vai_tro&ma_vai_tro=eq." + maVaiTro);
            var name = FirstStringArrayElement(json, "ten_vai_tro");
            return string.IsNullOrWhiteSpace(name) ? "Employee" : name;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đọc được tên vai trò {MaVaiTro} — mặc định Employee.", maVaiTro);
            return "Employee";
        }
    }

    private async Task<Profile?> FetchProfileAsync(string token, int maNhanVien)
    {
        // ?select= chỉ lấy đúng 5 cột cần dùng: PostgREST lọc cột ở tầng API nên
        // dù GRANT SELECT cả bảng thì app không nhận được ngày sinh / địa chỉ.
        const string select = "ho,ten,dien_thoai,ma_phong_ban,phong_ban(ten_phong_ban),chuc_vu(ten_chuc_vu)";
        var json = await GetRestAsync(token, $"/nhan_vien?select={select}&ma_nhan_vien=eq.{maNhanVien}");

        var rows = ParseArray(json);
        if (rows.Length == 0)
        {
            return null;
        }

        var root = rows[0];

        return new Profile(
            HoTen: string.Join(' ', new[] { ReadString(root, "ho"), ReadString(root, "ten") }.Where(s => s.Length > 0)).Trim(),
            DienThoai: ReadString(root, "dien_thoai"),
            MaPhongBan: ReadNullableInt(root, "ma_phong_ban"),
            TenPhongBan: ReadEmbeddedString(root, "phong_ban", "ten_phong_ban"),
            TenChucVu: ReadEmbeddedString(root, "chuc_vu", "ten_chuc_vu"));
    }

    /// <summary>
    /// Truy vấn PostgREST kèm Bearer token.
    ///
    /// pathAndQuery phải là đường dẫn TƯƠNG ĐỐI không dấu "/" ở đầu, ví dụ
    /// "vai_tro?select=ten_vai_tro". Dấu "/" ở đầu sẽ xoá sạch "/rest/v1" trong
    /// BaseAddress theo RFC 3986 rồi trỏ thẳng vào gốc domain — 404, mà 404 ở
    /// đây lại dễ rơi vào nhánh "phiên hết hạn" và báo sai cho người dùng. Hàm này
    /// tự cắt dấu "/" cho an toàn thay vì trông chờ mỗi caller nhớ quy tắc.
    /// </summary>
    private async Task<string> GetRestAsync(string token, string pathAndQuery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery.TrimStart('/'));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _restClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return json;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        _logger.LogError("PostgREST lỗi: HTTP {StatusCode} - {Content}", response.StatusCode, json);
        throw new InvalidOperationException("Máy chủ từ chối truy vấn dữ liệu. Vui lòng thử lại.");
    }

    public async Task LogoutAsync()
    {
        try
        {
            SecureStorage.Remove(JwtStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xóa JWT khỏi SecureStorage.");
        }

        CurrentUser = null;
        await Task.CompletedTask;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(JwtStorageKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            // Token hạn 1 giờ nên phải kiểm hạn trước; hết hạn thì xoá và bắt
            // đăng nhập lại — không có đường làm mới (xem ghi chú ở đầu class).
            if (IsExpired(token))
            {
                SecureStorage.Remove(JwtStorageKey);
                return false;
            }

            var email = ReadStringClaim(token, "email");
            var session = await BuildSessionAsync(token, email);

            if (session == null)
            {
                // Token còn hạn nhưng dựng phiên hỏng (mạng lỗi, hồ sơ bị xoá).
                // Xoá token để vòng sau thử lại từ đầu, thay vì lặp lại vô ích.
                SecureStorage.Remove(JwtStorageKey);
                return false;
            }

            CurrentUser = session;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi thử tự động đăng nhập từ JWT lưu trữ.");
            return false;
        }
    }

    public Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        // Cần mật khẩu cũ để GoTrue xác nhận, mà IAuthService không truyền email
        // nên chưa làm được. Để trả về thông báo hướng dẫn thay vì báo lỗi chung.
        return Task.FromResult((false, "Vui lòng thực hiện đổi mật khẩu trên cổng Web Admin."));
    }

    private static bool IsExpired(string token)
    {
        var exp = ReadLongClaim(token, "exp");
        return exp is not null && DateTimeOffset.FromUnixTimeSeconds(exp.Value) <= DateTimeOffset.UtcNow;
    }

    private static JsonElement? DecodeJwtPayload(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Đọc claim số ở cấp gốc hoặc trong app_metadata (hook nhét vào đây).
    /// </summary>
    private static int? ReadIntClaim(string token, string name) => ReadNumberClaim(token, name, n => n.TryGetInt32(out var v) ? v : (int?)null);

    private static long? ReadLongClaim(string token, string name) => ReadNumberClaim(token, name, n => n.TryGetInt64(out var v) ? v : (long?)null);

    private static T? ReadNumberClaim<T>(string token, string name, Func<JsonElement, T?> convert)
        where T : struct
    {
        var root = DecodeJwtPayload(token);
        if (root is null)
        {
            return null;
        }

        foreach (var scope in new[] { root.Value, AppMetadata(root.Value) })
        {
            if (scope.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (scope.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return convert(prop);
            }
        }

        return null;
    }

    private static string ReadStringClaim(string token, string name)
    {
        var root = DecodeJwtPayload(token);
        return root is null ? string.Empty : ReadString(root.Value, name);
    }

    /// <summary>
    /// app_metadata có thể vắng mặt (token của tài khoản chưa qua hook) — trả
    /// JsonElement mặc định để caller chỉ cần kiểm tra ValueKind, không phải
    /// kiểm tra null trước mỗi lần đọc.
    /// </summary>
    private static JsonElement AppMetadata(JsonElement root) =>
        root.TryGetProperty("app_metadata", out var meta) ? meta : default;

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;

    private static int? ReadNullableInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : null;

    /// <summary>
    /// Đọc cột của quan hệ nhúng PostgREST (vd "phong_ban(ten_phong_ban)").
    ///
    /// Quan hệ 1-n trả về OBJECT: "phong_ban": {"ten_phong_ban": ...} — hoặc null
    /// khi nhân viên chưa được gán phòng. Vẫn xử lý nốt dạng mảng vì cùng một
    /// cấu trúc được dùng khi nhúng qua bảng nối 1-n; thiếu nhánh này thì
    /// phiếu lương sẽ hiện trống phòng ban dù API trả về đúng.
    /// </summary>
    private static string ReadEmbeddedString(JsonElement element, string relation, string column)
    {
        if (!element.TryGetProperty(relation, out var prop))
        {
            return string.Empty;
        }

        if (prop.ValueKind == JsonValueKind.Object)
        {
            return ReadString(prop, column);
        }

        if (prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    return ReadString(item, column);
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Tách mảng JSON thành danh sách element độc lập.
    ///
    /// <para>
    /// <b>Bắt buộc Clone.</b> <see cref="JsonElement"/> là struct chỉ giữ <i>con trỏ
    /// vào bộ đệm của JsonDocument</i>, chứ không tự mang nội dung. Toán tử
    /// <c>[.. doc.RootElement.EnumerateArray()]</c> chỉ copy con trỏ đó ra mảng
    /// mới, nên khi <c>using</c> Dispose tài liệu, toàn bộ element đã trả về
    /// hoá thành con trỏ treo và lần đọc kế tiếp ném
    /// <see cref="ObjectDisposedException"/>.
    /// </para>
    ///
    /// <para>
    /// <see cref="JsonElement.Clone"/> là cách duy nhất tạo bản sao tự quản lý
    /// vòng đời riêng, sống được sau khi tài liệu gốc biến mất. Bỏ Clone ở đây
    /// từng làm đăng nhập chết ở bước đọc hồ sơ: token đã lấy được, PostgREST
    /// đã trả 200, nhưng <c>FetchProfileAsync</c> không có try/catch nên lỗi nổi
    /// thành "Đã xảy ra lỗi trong quá trình đăng nhập" — hoàn toàn giống lỗi mạng.
    /// </para>
    /// </summary>
    private static JsonElement[] ParseArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<JsonElement>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                result.Add(item.Clone());
            }

            return [.. result];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FirstStringArrayElement(string json, string column)
    {
        var rows = ParseArray(json);
        return rows.Length == 0 ? string.Empty : ReadString(rows[0], column);
    }

    private static string ReadStringProperty(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(name, out var prop) ? prop.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Đọc error_code của GoTrue (vd "invalid_credentials"). Chỉ phục vụ log —
    /// thông báo trả về cho người dùng phải giống nhau mọi trường hợp để không tiết
    /// lộ email nào đã tồn tại trong hệ thống.
    /// </summary>
    private static string ReadErrorCode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error_code", out var prop)
                ? prop.GetString() ?? "-"
                : "-";
        }
        catch (JsonException)
        {
            return "-";
        }
    }

    /// <summary>Hồ sơ tối thiểu app cần để dựng phiên.</summary>
    private sealed record Profile(
        string HoTen,
        string DienThoai,
        int? MaPhongBan,
        string TenPhongBan,
        string TenChucVu);
}
