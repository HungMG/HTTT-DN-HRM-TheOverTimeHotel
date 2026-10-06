namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Cấu hình Endpoint máy chủ API cho Mobile App.
/// Bảo đảm tuân thủ nguyên tắc Zero Trust và giao thức bảo mật HTTPS trong Production.
/// </summary>
public static class ApiConfig
{
    // URL môi trường Production bắt buộc dùng HTTPS để bảo vệ Bearer JWT Token
    public const string ProductionBaseUrl = "https://api.overtimehotel.com";

    // ------------------------------------------------------------------
    //  Supabase Cloud — nguồn dữ liệu thật của Mobile App
    // ------------------------------------------------------------------
    //  PRD 06 vẽ Mobile App -> Supabase REST thẳng, không đi qua Web Admin.
    //  Vì vậy app tự gọi GoTrue (đăng nhập) + PostgREST (dữ liệu).
    //
    //  AnonKey là khoá CÔNG KHAI theo thiết kế của Supabase: mọi client đều
    //  phải gửi nó nên không thể giấu — app là binary tải về, ai cũng đọc được.
    //  Ranh giới bảo mật thật nằm ở RLS: migration
    //  20261005_hon_ho_so_jwt_quyen_theo_vai_tro.sql đã thu hồi toàn bộ quyền
    //  của role anon, nên dù có khoá này thì cũng không đọc được dòng nào nào.
    //  ServiceRoleKey (khoá bí mật) KHÔNG BAO GIỜ đặt ở đây.
    public const string SupabaseUrl = "https://soethtbttktnwtxyxjrj.supabase.co";

    /// <summary>Khoá anon (JWT "eyJ...") — cả GoTrue lẫn PostgREST đều chấp nhận.</summary>
    public const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNvZXRodGJ0dGt0bnd0eHl4anJqIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1MjkxNjgsImV4cCI6MjEwNTEwNTE2OH0.KoIyzRJQAb9QAP1-cMdkVHzv6ZNlXOV3j2oNFoBhEYE";

    /// <summary>
    /// Gốc GoTrue — nơi cấp và làm mới JWT.
    ///
    /// Dấu "/" CUỐI bắt buộc phải có, và đường dẫn tương đối gọi kèm phải KHÔNG
    /// có dấu "/" ở đầu. HttpClient nối hai bên bằng thuật toán RFC 3986: chỉ cần
    /// một trong hai sai là mất "/auth/v1" và gõi thẳng vào gốc domain.
    ///
    ///   BaseAddress ".../auth/v1/" + "token?..."  ✔  .../auth/v1/token?...
    ///   BaseAddress ".../auth/v1/" + "/token?..." ✘  .../token?...  → 404
    ///   BaseAddress ".../auth/v1"  + "token?..."  ✘  .../token?...  → 404
    ///
    /// 404 kiểu này rất dễ bị nhầm là "sai mật khẩu" vì code chỉ kiểm tra
    /// IsSuccessStatusCode rồi báo thông báo chung. Đã gặp đúng triệu chứng này
    /// trên emulator: nhãn trạng thái đỏ và đăng nhập báo sai thông tin.
    /// </summary>
    public static string AuthBaseUrl => $"{SupabaseUrl}/auth/v1/";

    /// <summary>Gốc PostgREST — nơi đọc/ghi bảng nghiệp vụ. Cùng lý do dấu "/" cuối.</summary>
    public static string RestBaseUrl => $"{SupabaseUrl}/rest/v1/";

    // URL môi trường Local Debug cho lập trình viên (chỉ khả dụng trong bản dựng Debug)
    private const string AndroidEmulatorDebugUrl = "http://10.0.2.2:5065";
    private const string LocalhostDebugUrl = "http://localhost:5065";

    /// <summary>
    /// Kiểm tra ứng dụng đang chạy ở chế độ Production hay Debug.
    /// Trong bản phát hành Release, IsProduction luôn luôn là true.
    /// </summary>
    public static bool IsProduction =>
#if DEBUG
        Preferences.Get("enforce_production_mode", false);
#else
        true;
#endif

    public static string BaseUrl
    {
        get
        {
            var rawUrl = ResolveBaseUrl();
            ValidateTransportSecurity(rawUrl);
            return rawUrl;
        }
    }

    private static string ResolveBaseUrl()
    {
        // Cho phép cấu hình qua Preferences nếu muốn trỏ tới server riêng hoặc cloud test
        var customUrl = Preferences.Get("custom_api_base_url", string.Empty);
        if (!string.IsNullOrWhiteSpace(customUrl))
        {
            return customUrl.TrimEnd('/');
        }

#if DEBUG
        // Môi trường Debug cục bộ: hỗ trợ tiện ích Android Emulator và Localhost
        return DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
            ? AndroidEmulatorDebugUrl
            : LocalhostDebugUrl;
#else
        // Môi trường Production: Mặc định luôn là HTTPS production URL
        return ProductionBaseUrl;
#endif
    }

    /// <summary>
    /// Kiểm soát bảo mật đường truyền (Transport Layer Security - TLS):
    /// Bắt buộc sử dụng HTTPS trong Production để triệt tiêu nguy cơ Man-in-the-Middle (MitM) đánh cắp JWT.
    /// </summary>
    public static void ValidateTransportSecurity(string url)
    {
        if (IsProduction && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Bảo mật đường truyền (Zero Trust): Trong môi trường Production, URL API '{url}' bắt buộc phải sử dụng giao thức HTTPS để bảo vệ mã xác thực JWT.");
        }
    }
}
