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

        // Lùi về cấu hình mặc định trong appsettings.json hoặc hằng số PRD 4.1
        var checkInSection = _configuration.GetSection("CheckIn");
        var lat = checkInSection.GetValue<double?>("HotelLatitude") ?? 10.778848;
        var lng = checkInSection.GetValue<double?>("HotelLongitude") ?? 106.687421;
        var radius = checkInSection.GetValue<double?>("RadiusMeters") ?? GeofencePolicy.DEFAULT_RADIUS_METERS;
        var wifi = checkInSection.GetValue<string>("WifiSsid") ?? GeofencePolicy.DEFAULT_WIFI_SSID;

        return new AttendanceLocationViewModel
        {
            HotelName = "The OverTime Hotel & Resort",
            Address = "123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            HotelLatitude = lat,
            HotelLongitude = lng,
            RadiusMeters = radius,
            WifiSsid = string.IsNullOrWhiteSpace(wifi) ? GeofencePolicy.DEFAULT_WIFI_SSID : wifi
        };
    }

    public async Task<bool> UpdateLocationConfigAsync(
        AttendanceLocationViewModel model,
        string updatedBy,
        int? accountId,
        string ipAddress)
    {
        ArgumentNullException.ThrowIfNull(model);

        var currentConfig = await GetLocationConfigAsync();
        var filePath = GetConfigFilePath();

        model.LastUpdatedAt = DateTimeOffset.UtcNow;
        model.LastUpdatedBy = updatedBy;

        await FileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(model, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Bộ phận IT {User} đã lưu cấu hình toạ độ chấm công thành công.", updatedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi file cấu hình toạ độ {File}.", ConfigFileName);
            return false;
        }
        finally
        {
            FileLock.Release();
        }

        // Ghi nhật ký kiểm toán quản trị & Cập nhật CSDL Supabase để Mobile App nhận tức thời
        try
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.cau_hinh_cham_cong (id, ten_khach_san, dia_chi, vi_do, kinh_do, ban_kinh_met, wifi_ssid, cap_nhat_luc, nguoi_cap_nhat)
                VALUES (1, {model.HotelName}, {model.Address}, {model.HotelLatitude}, {model.HotelLongitude}, {model.RadiusMeters}, {model.WifiSsid}, {DateTimeOffset.UtcNow}, {updatedBy})
                ON CONFLICT (id) DO UPDATE SET
                    ten_khach_san = EXCLUDED.ten_khach_san,
                    dia_chi = EXCLUDED.dia_chi,
                    vi_do = EXCLUDED.vi_do,
                    kinh_do = EXCLUDED.kinh_do,
                    ban_kinh_met = EXCLUDED.ban_kinh_met,
                    wifi_ssid = EXCLUDED.wifi_ssid,
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
                NoiDung = $"Bộ phận CNTT '{updatedBy}' đã cập nhật vị trí định vị và bán kính chấm công của khách sạn.",
                GiaTriCu = $"Toạ độ: ({currentConfig.HotelLatitude}, {currentConfig.HotelLongitude}); Bán kính: {currentConfig.RadiusMeters}m; SSID: {currentConfig.WifiSsid}",
                GiaTriMoi = $"Toạ độ: ({model.HotelLatitude}, {model.HotelLongitude}); Bán kính: {model.RadiusMeters}m; SSID: {model.WifiSsid}",
                LyDo = "Cập nhật vị trí cơ sở khách sạn phục vụ xác thực chấm công di động",
                DiaChiIp = ipAddress,
                ThoiGian = DateTimeOffset.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi nhật ký quản trị hoặc đồng bộ CSDL khi cập nhật vị trí chấm công.");
        }

        return true;
    }
}
