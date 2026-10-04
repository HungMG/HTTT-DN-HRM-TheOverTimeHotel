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
}