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
    /// Đọc cấu hình toạ độ từ CSDL Supabase Cloud (đồng bộ tức thời khi IT đổi cấu hình),
    /// lưu đệm vào Preferences máy để chạy offline, và dự phòng bằng file appsettings.json.
    /// </summary>
    public static async Task<GeofenceConfig> LoadAsync(ILogger logger, SupabaseRestClient? rest = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        // 1. ƯU TIÊN 1: Đồng bộ cấu hình mới nhất từ bảng cau_hinh_cham_cong trên Supabase
        if (rest != null)
        {
            var lastSyncedAt = Preferences.Default.Get("Geofence_SyncedAt", 0L);
            var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Đồng bộ định kỳ mỗi 5 phút hoặc khi chưa từng đồng bộ
            if (nowSec - lastSyncedAt > 300 || lastSyncedAt == 0)
            {
                try
                {
                    var json = await rest.GetAsync("cau_hinh_cham_cong?select=vi_do,kinh_do,ban_kinh_met,wifi_ssid&order=id.asc&limit=1");
                    var rows = PostgrstJson.ParseArray(json);
                    if (rows.Length > 0)
                    {
                        var row = rows[0];
                        var lat = (double)PostgrstJson.ReadDecimal(row, "vi_do", 0m);
                        var lng = (double)PostgrstJson.ReadDecimal(row, "kinh_do", 0m);
                        var radius = (double)PostgrstJson.ReadDecimal(row, "ban_kinh_met", 0m);
                        var wifi = PostgrstJson.ReadString(row, "wifi_ssid");

                        if (IsUsableCoordinate(lat, lng))
                        {
                            Preferences.Default.Set("Geofence_Latitude", lat);
                            Preferences.Default.Set("Geofence_Longitude", lng);
                            Preferences.Default.Set("Geofence_Radius", radius > 0 ? radius : GeofencePolicy.DEFAULT_RADIUS_METERS);
                            Preferences.Default.Set("Geofence_Wifi", wifi ?? string.Empty);
                            Preferences.Default.Set("Geofence_SyncedAt", nowSec);

                            logger.LogInformation(
                                "Đã đồng bộ toạ độ GPS chấm công từ Supabase Cloud: ({Lat}, {Lng}), R={Radius}m, SSID={Wifi}.",
                                lat, lng, radius, wifi);

                            return new GeofenceConfig
                            {
                                HotelLatitude = lat,
                                HotelLongitude = lng,
                                RadiusMeters = radius > 0 ? radius : GeofencePolicy.DEFAULT_RADIUS_METERS,
                                WifiSsid = wifi ?? string.Empty
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Chưa đồng bộ được toạ độ từ Supabase Cloud. Lùi về bộ nhớ máy.");
                }
            }
        }

        // 2. ƯU TIÊN 2: Đọc cấu hình đã lưu trong Preferences (bản đồng bộ gần nhất khi offline)
        if (Preferences.Default.ContainsKey("Geofence_Latitude"))
        {
            var cachedLat = Preferences.Default.Get("Geofence_Latitude", 0d);
            var cachedLng = Preferences.Default.Get("Geofence_Longitude", 0d);
            var cachedRadius = Preferences.Default.Get("Geofence_Radius", 0d);
            var cachedWifi = Preferences.Default.Get("Geofence_Wifi", string.Empty);

            if (IsUsableCoordinate(cachedLat, cachedLng))
            {
                return new GeofenceConfig
                {
                    HotelLatitude = cachedLat,
                    HotelLongitude = cachedLng,
                    RadiusMeters = cachedRadius > 0 ? cachedRadius : GeofencePolicy.DEFAULT_RADIUS_METERS,
                    WifiSsid = cachedWifi
                };
            }
        }

        // 3. ƯU TIÊN 3 (DỰ PHÒNG CƠ SỞ): Đọc từ gói cài đặt đóng gói appsettings.json
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
