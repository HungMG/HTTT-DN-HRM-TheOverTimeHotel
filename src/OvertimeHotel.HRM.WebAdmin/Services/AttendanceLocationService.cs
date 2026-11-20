using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public class AttendanceLocationService : IAttendanceLocationService
{
    private const string ConfigFileName = "attendance-geofence.json";
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AttendanceLocationService> _logger;

    public AttendanceLocationService(
        IWebHostEnvironment env,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<AttendanceLocationService> logger)
    {
        _env = env;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    private string GetConfigFilePath() => Path.Combine(_env.ContentRootPath, ConfigFileName);

    public async Task<AttendanceLocationViewModel> GetLocationConfigAsync()
    {
        // 1. Ưu tiên đọc từ CSDL Supabase (nguồn chân lý dùng chung giữa WebAdmin và MobileApp)
        try
        {
            var dbConfig = await _dbContext.Database
                .SqlQueryRaw<AttendanceLocationViewModel>("""
                    SELECT
                        COALESCE(ten_khach_san, '') AS "HotelName",
                        COALESCE(dia_chi, '') AS "Address",
                        vi_do AS "HotelLatitude",
                        kinh_do AS "HotelLongitude",
                        ban_kinh_met AS "RadiusMeters",
                        COALESCE(wifi_ssid, '') AS "WifiSsid",
                        COALESCE(bat_gps, true) AS "EnableGps",
                        COALESCE(bat_wifi, true) AS "EnableWifi",
                        cap_nhat_luc AS "LastUpdatedAt",
                        nguoi_cap_nhat AS "LastUpdatedBy"
                    FROM public.cau_hinh_cham_cong
                    WHERE id = 1
                    LIMIT 1
                    """)
                .FirstOrDefaultAsync();

            if (dbConfig != null && !(dbConfig.HotelLatitude == 0d && dbConfig.HotelLongitude == 0d))
            {
                EnsureAtLeastOneLayer(dbConfig);
                return dbConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc cấu hình vị trí từ CSDL Supabase, lùi về file {File}.", ConfigFileName);
        }

        // 2. Dự phòng: đọc từ file JSON cục bộ
        var filePath = GetConfigFilePath();

        await FileLock.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<AttendanceLocationViewModel>(json);
                if (config != null)
                {
                    EnsureAtLeastOneLayer(config);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc file cấu hình toạ độ {File}, lùi về cấu hình appsettings.", ConfigFileName);
        }
        finally
        {
            FileLock.Release();
        }

        // 3. Lùi về cấu hình mặc định trong appsettings.json hoặc hằng số PRD 4.1
        var checkInSection = _configuration.GetSection("CheckIn");
        var lat = checkInSection.GetValue<double?>("HotelLatitude") ?? 10.778848;
        var lng = checkInSection.GetValue<double?>("HotelLongitude") ?? 106.687421;
        var radius = checkInSection.GetValue<double?>("RadiusMeters") ?? GeofencePolicy.DEFAULT_RADIUS_METERS;
        var wifi = checkInSection.GetValue<string>("WifiSsid") ?? GeofencePolicy.DEFAULT_WIFI_SSID;
        var enableGps = checkInSection.GetValue<bool?>("EnableGps") ?? true;
        var enableWifi = checkInSection.GetValue<bool?>("EnableWifi") ?? true;

        var fallback = new AttendanceLocationViewModel
        {
            HotelName = "The OverTime Hotel & Resort",
            Address = "123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            HotelLatitude = lat,
            HotelLongitude = lng,
            RadiusMeters = radius,
            WifiSsid = string.IsNullOrWhiteSpace(wifi) ? GeofencePolicy.DEFAULT_WIFI_SSID : wifi,
            EnableGps = enableGps,
            EnableWifi = enableWifi
        };
        EnsureAtLeastOneLayer(fallback);
        return fallback;
    }

    public async Task<bool> UpdateLocationConfigAsync(
        AttendanceLocationViewModel model,
        string updatedBy,
        int? accountId,
        string ipAddress)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!model.EnableGps && !model.EnableWifi)
        {
            _logger.LogWarning("Từ chối lưu cấu hình từ {User} vì tắt đồng thời cả GPS và Wi-Fi.", updatedBy);
            return false;
        }

        var cleanWifi = model.WifiSsid?.Trim() ?? string.Empty;
        if (model.EnableWifi && string.IsNullOrWhiteSpace(cleanWifi))
        {
            _logger.LogWarning("Từ chối lưu cấu hình từ {User} vì bật Wi-Fi nhưng bỏ trống SSID.", updatedBy);
            return false;
        }

        var currentConfig = await GetLocationConfigAsync();
        var nowUtc = DateTimeOffset.UtcNow;

        model.WifiSsid = cleanWifi;
        model.LastUpdatedAt = nowUtc;
        model.LastUpdatedBy = updatedBy;

        // 1. Cập nhật CSDL Supabase & nhật ký quản trị trước để Mobile App nhận tức thời
        try
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.cau_hinh_cham_cong (id, ten_khach_san, dia_chi, vi_do, kinh_do, ban_kinh_met, wifi_ssid, bat_gps, bat_wifi, cap_nhat_luc, nguoi_cap_nhat)
                VALUES (1, {model.HotelName}, {model.Address}, {model.HotelLatitude}, {model.HotelLongitude}, {model.RadiusMeters}, {cleanWifi}, {model.EnableGps}, {model.EnableWifi}, {nowUtc}, {updatedBy})
                ON CONFLICT (id) DO UPDATE SET
                    ten_khach_san = EXCLUDED.ten_khach_san,
                    dia_chi = EXCLUDED.dia_chi,
                    vi_do = EXCLUDED.vi_do,
                    kinh_do = EXCLUDED.kinh_do,
                    ban_kinh_met = EXCLUDED.ban_kinh_met,
                    wifi_ssid = EXCLUDED.wifi_ssid,
                    bat_gps = EXCLUDED.bat_gps,
                    bat_wifi = EXCLUDED.bat_wifi,
                    cap_nhat_luc = EXCLUDED.cap_nhat_luc,
                    nguoi_cap_nhat = EXCLUDED.nguoi_cap_nhat;
                """);

            _dbContext.NhatKyQuanTris.Add(new NhatKyQuanTri
            {
                MaTaiKhoanThucHien = accountId,
                MaTaiKhoanBiTacDong = accountId,
                TenNguoiThucHien = updatedBy,
                TenTaiKhoanBiTacDong = "HE_THONG_GEOFENCE",
                HanhDong = "CAP_NHAT_VI_TRI_CHAM_CONG",
                NoiDung = $"Bộ phận CNTT '{updatedBy}' đã cập nhật cấu hình vị trí và lớp xác thực chấm công của khách sạn.",
                GiaTriCu = $"Toạ độ: ({currentConfig.HotelLatitude}, {currentConfig.HotelLongitude}); Bán kính: {currentConfig.RadiusMeters}m; SSID: {currentConfig.WifiSsid}; GPS: {(currentConfig.EnableGps ? "Bật" : "Tắt")}; Wi-Fi: {(currentConfig.EnableWifi ? "Bật" : "Tắt")}",
                GiaTriMoi = $"Toạ độ: ({model.HotelLatitude}, {model.HotelLongitude}); Bán kính: {model.RadiusMeters}m; SSID: {cleanWifi}; GPS: {(model.EnableGps ? "Bật" : "Tắt")}; Wi-Fi: {(model.EnableWifi ? "Bật" : "Tắt")}",
                LyDo = "Cập nhật vị trí cơ sở khách sạn phục vụ xác thực chấm công di động",
                DiaChiIp = ipAddress,
                ThoiGian = nowUtc
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể đồng bộ vị trí chấm công lên CSDL Supabase.");
            return false;
        }

        // 2. Đồng bộ bản sao dự phòng ra file JSON cục bộ (không chặn nếu hệ thống tệp chỉ đọc)
        var filePath = GetConfigFilePath();
        await FileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(model, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Bộ phận IT {User} đã lưu cấu hình toạ độ chấm công thành công.", updatedBy);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Đã cập nhật CSDL Supabase nhưng không thể ghi file dự phòng {File}.", ConfigFileName);
        }
        finally
        {
            FileLock.Release();
        }

        return true;
    }

    private static void EnsureAtLeastOneLayer(AttendanceLocationViewModel config)
    {
        if (!config.EnableGps && !config.EnableWifi)
        {
            config.EnableGps = true;
        }
    }
}
