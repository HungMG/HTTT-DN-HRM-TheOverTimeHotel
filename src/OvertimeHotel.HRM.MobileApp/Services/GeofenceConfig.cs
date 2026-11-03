using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Toạ độ khách sạn và phạm vi cho phép quẹt thẻ (PRD 4.1), đọc từ
/// <c>Resources/Raw/appsettings.json</c> đóng gói trong APK.
///
/// <para>
/// <b>Vì sao đọc file chứ không hằng số trong code.</b> Toạ độ khách sạn là dữ
/// liệu vận hành, không phải logic: đổi chi nhánh hoặc nới bán kính phải làm
/// được mà không cần build lại app rồi phát hành bản mới cho toàn bộ nhân viên.
/// </para>
///
/// <para>
/// <b>Vì sao thiếu cấu hình thì dùng mặc định chứ không chặn.</b> Không đọc được
/// file thì trả về đúng số của PRD (<see cref="GeofencePolicy.DEFAULT_RADIUS_METERS"/>
/// và <see cref="GeofencePolicy.DEFAULT_WIFI_SSID"/>). App vẫn chạy đúng luật;
/// ghi log để biết file đóng gói hỏng.
/// </para>
/// </summary>
public sealed class GeofenceConfig
{
    /// <summary>Tên file đóng gói — phải khớp <c>MauiAsset</c> trong .csproj.</summary>
    public const string AssetFileName = "appsettings.json";

    public double HotelLatitude { get; init; } = 10.778848d;
    public double HotelLongitude { get; init; } = 106.687421d;
    public double RadiusMeters { get; init; } = 250d;
    public string WifiSsid { get; init; } = "";

    /// <summary>
    /// Đọc cấu hình từ gói cài. Luôn trả về một đối tượng dùng được — không bao
    /// giờ null, không bao giờ ném lỗi ra ngoài.
    /// </summary>
    public static async Task<GeofenceConfig> LoadAsync(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetFileName);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions
            {
                // File cấu hình có ghi chú giải thích; nếu không bật cờ này thì
                // một dấu // là đủ làm JsonException và rơi về mặc định.
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };

            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, options);
            var checkIn = root?.CheckIn;

            if (checkIn is null)
            {
                logger.LogWarning("File {File} không có mục CheckIn — dùng toạ độ mặc định.", AssetFileName);
                return new GeofenceConfig();
            }

            var config = new GeofenceConfig
            {
                HotelLatitude = checkIn.HotelLatitude,
                HotelLongitude = checkIn.HotelLongitude,
                RadiusMeters = checkIn.RadiusMeters > 0 ? checkIn.RadiusMeters : GeofencePolicy.DEFAULT_RADIUS_METERS,
                WifiSsid = string.IsNullOrWhiteSpace(checkIn.WifiSsid)
                    ? GeofencePolicy.DEFAULT_WIFI_SSID
                    : checkIn.WifiSsid.Trim()
            };

            // Toạ độ 0,0 hoặc ngoài miền giá trị = file bị sửa hỏng. Chặn toàn bộ
            // nhân viên vì một dấu phẩy sai vị trí là hậu quả tệ hơn nhiều so với
            // việc lùi về toạ độ mặc định và ghi log.
            if (!IsUsableCoordinate(config.HotelLatitude, config.HotelLongitude))
            {
                logger.LogWarning(
                    "Toạ độ khách sạn trong {File} không hợp lệ ({Lat}, {Lng}) — dùng toạ độ mặc định.",
                    AssetFileName, config.HotelLatitude, config.HotelLongitude);
                return new GeofenceConfig
                {
                    RadiusMeters = config.RadiusMeters,
                    WifiSsid = config.WifiSsid
                };
            }

            return config;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không đọc được {File} — dùng toạ độ mặc định của PRD 4.1.", AssetFileName);
            return new GeofenceConfig();
        }
    }

    private static bool IsUsableCoordinate(double latitude, double longitude) =>
        latitude is >= -90d and <= 90d &&
        longitude is >= -180d and <= 180d &&
        !(latitude == 0d && longitude == 0d);

    private sealed class AppSettingsRoot
    {
        [JsonPropertyName("CheckIn")]
        public CheckInSection? CheckIn { get; init; }
    }

    private sealed class CheckInSection
    {
        [JsonPropertyName("HotelLatitude")]
        public double HotelLatitude { get; init; }

        [JsonPropertyName("HotelLongitude")]
        public double HotelLongitude { get; init; }

        [JsonPropertyName("RadiusMeters")]
        public double RadiusMeters { get; init; }

        [JsonPropertyName("WifiSsid")]
        public string? WifiSsid { get; init; }
    }
}
