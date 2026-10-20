using System.Globalization;
using Microsoft.Extensions.Logging;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Cài đặt dịch vụ toạ độ dựa trên API Geolocation của .NET MAUI.
/// Mọi lỗi đều được nuốt gọn và trả về null để nhân viên vẫn quẹt thẻ được
/// khi thiết bị không bật được GPS hoặc người dùng từ chối cấp quyền.
/// </summary>
public class GeoLocationService : IGeoLocationService
{
    private readonly ILogger<GeoLocationService> _logger;

    // Độ chính xác trung bình đủ dùng để đối chiếu vị trí trong khuôn viên khách sạn,
    // vừa đủ nhanh để không bắt người dùng chờ khi quẹt thẻ.
    private static readonly GeolocationRequest Request = new(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

    public GeoLocationService(ILogger<GeoLocationService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetCurrentCoordinateAsync()
    {
        try
        {
            var location = await Geolocation.GetLocationAsync(Request);

            if (location is null)
            {
                _logger.LogWarning("Không lấy được toạ độ vị trí khi quẹt thẻ.");
                return null;
            }

            // Định dạng "latitude,longitude" đúng quy ước lưu vào cột toa_do_checkin.
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{location.Latitude:F6},{location.Longitude:F6}");
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.LogWarning(ex, "Thiết bị không hỗ trợ định vị. Nhân viên vẫn quẹt thẻ được, chỉ không ghi toạ độ.");
            return null;
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Người dùng chưa cấp quyền vị trí. Nhân viên vẫn quẹt thẻ được, chỉ không ghi toạ độ.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi lấy toạ độ vị trí.");
            return null;
        }
    }

    public Task<string?> GetDeviceNameAsync()
    {
        try
        {
            var name = DeviceInfo.Current.Name;
            return Task.FromResult(string.IsNullOrWhiteSpace(name) ? null : name.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy tên thiết bị.");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Đọc SSID của mạng Wi-Fi đang kết nối (PRD 4.1 — lớp xác thực thứ hai).
    ///
    /// <para>
    /// <b>Vì sao nuốt mọi lỗi và trả null.</b> Đây là lớp dự phòng: thiếu SSID thì
    /// lớp GPS vẫn quyết định được, còn ném lỗi ở đây sẽ chặn luôn nhân viên đang
    /// đứng trong khách sạn chỉ vì máy không đọc được tên mạng.
    /// </para>
    ///
    /// <para>
    /// <b>Android trả SSID có dấu nháy kép</b> (<c>"OverTimeHotel_Staff"</c>) và trả
    /// chuỗi <c>&lt;unknown ssid&gt;</c> khi chưa cấp quyền vị trí. Lớp so sánh trong
    /// <c>GeofencePolicy</c> đã tự bóc nháy và so không phân biệt hoa thường; ở đây
    /// chỉ cần lọc bỏ giá trị giả để không ghi rác vào bằng chứng chấm công.
    /// </para>
    /// </summary>
    public Task<string?> GetCurrentWifiSsidAsync()
    {
        try
        {
            return Task.FromResult(ReadWifiSsid());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đọc được tên Wi-Fi đang kết nối.");
            return Task.FromResult<string?>(null);
        }
    }

    private static string? ReadWifiSsid()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        string? ssid = null;

        // API 29 trở lên: đọc qua NetworkCapabilities.TransportInfo thay cho
        // WifiManager.ConnectionInfo vốn đã bị đánh dấu lỗi thời từ API 31.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var connectivity = context.GetSystemService(Android.Content.Context.ConnectivityService)
                as Android.Net.ConnectivityManager;
            var network = connectivity?.ActiveNetwork;
            var capabilities = network is null ? null : connectivity?.GetNetworkCapabilities(network);

            if (capabilities is not null &&
                capabilities.HasTransport(Android.Net.TransportType.Wifi) &&
                capabilities.TransportInfo is Android.Net.Wifi.WifiInfo wifiInfo)
            {
                ssid = wifiInfo.SSID;
            }
        }
        else
        {
            // Máy Android 9 trở xuống không có TransportInfo. Dùng API cũ và tự
            // tắt cảnh báo lỗi thời — nhánh này chỉ chạy trên thiết bị cũ.
#pragma warning disable CS0618
            var wifiManager = context.GetSystemService(Android.Content.Context.WifiService)
                as Android.Net.Wifi.WifiManager;
            ssid = wifiManager?.ConnectionInfo?.SSID;
#pragma warning restore CS0618
        }

        if (string.IsNullOrWhiteSpace(ssid))
        {
            return null;
        }

        var clean = ssid.Trim().Trim('"');

        // "<unknown ssid>" / "0x" là giá trị giả Android trả khi thiếu quyền hoặc
        // khi đang dùng mạng di động. Không được ghi chúng vào bằng chứng.
        if (clean.Length == 0 ||
            clean.StartsWith("<unknown", StringComparison.OrdinalIgnoreCase) ||
            clean == "0x")
        {
            return null;
        }

        return clean;
#else
        // iOS/Windows không cho ứng dụng thường đọc SSID nếu chưa bật entitlement
        // riêng. Lớp GPS đảm nhận việc xác thực vị trí trên các nền tảng này.
        return null;
#endif
    }
}