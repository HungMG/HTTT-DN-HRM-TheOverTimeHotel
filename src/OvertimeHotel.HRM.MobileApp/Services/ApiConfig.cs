namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Cấu hình Endpoint máy chủ API cho Mobile App.
/// Hỗ trợ linh hoạt giữa Android Emulator (10.0.2.2) và Windows / iOS (localhost).
/// </summary>
public static class ApiConfig
{
    private const string AndroidEmulatorBaseUrl = "http://10.0.2.2:5065";
    private const string DefaultLocalBaseUrl = "http://localhost:5065";

    public static string BaseUrl
    {
        get
        {
            // Cho phép cấu hình qua Preferences nếu muốn trỏ tới server riêng hoặc cloud
            var customUrl = Preferences.Get("custom_api_base_url", string.Empty);
            if (!string.IsNullOrWhiteSpace(customUrl))
            {
                return customUrl.TrimEnd('/');
            }

            return DeviceInfo.Platform == DevicePlatform.Android
                ? AndroidEmulatorBaseUrl
                : DefaultLocalBaseUrl;
        }
    }
}
