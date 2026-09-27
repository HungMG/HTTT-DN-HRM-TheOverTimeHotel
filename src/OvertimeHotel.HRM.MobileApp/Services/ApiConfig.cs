namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Cấu hình Endpoint máy chủ API cho Mobile App.
/// Bảo đảm tuân thủ nguyên tắc Zero Trust và giao thức bảo mật HTTPS trong Production.
/// </summary>
public static class ApiConfig
{
    // URL môi trường Production bắt buộc dùng HTTPS để bảo vệ Bearer JWT Token
    public const string ProductionBaseUrl = "https://api.overtimehotel.com";

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
        return DeviceInfo.Platform == DevicePlatform.Android
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
