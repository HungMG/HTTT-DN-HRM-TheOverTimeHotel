using System.Globalization;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Kết quả kiểm tra vị trí quẹt thẻ.
/// </summary>
/// <param name="IsAllowed">Được phép chấm công hay không.</param>
/// <param name="Method">Phương thức thắng cuộc: <c>GPS</c> hoặc <c>WIFI</c>.</param>
/// <param name="KhoangCachMet">Khoảng cách tới khách sạn, null khi không có toạ độ hợp lệ.</param>
/// <param name="ThongDiep">Câu giải thích hiển thị cho nhân viên khi bị chặn.</param>
public record GeofenceCheckResult(bool IsAllowed, string? Method, double? KhoangCachMet, string? ThongDiep);

/// <summary>
/// Luật xác thực vị trí quẹt thẻ (PRD 4.1) — nguồn duy nhất cho cả Mobile App và Web Admin.
///
/// <para>
/// PRD yêu cầu <b>xác thực hai lớp</b>: chấp nhận khi thiết bị nằm trong bán kính
/// cho phép quanh khách sạn <b>hoặc</b> đang nối đúng Wi-Fi nội bộ. Hai lớp là
/// "hoặc" chứ không phải "và": GPS trong nhà cao tầng thường trôi vài chục mét,
/// còn Wi-Fi nội bộ thì chính xác tuyệt đối nhưng chỉ có khi nhân viên đã vào
/// toà nhà. Bắt buộc cả hai sẽ khoá đường chấm công của người đứng ngay quầy
/// lễ tân — đúng lúc dễ xảy ra nhất.
/// </para>
///
/// <para>
/// <b>Vì sao Haversine chứ không trừ toạ độ.</b> Kinh độ 1° không dài bằng vĩ độ
/// 1° ở mọi vĩ tuyến (Việt Nam chênh khoảng 1,6%). Trừ thẳng toạ độ sẽ báo sai
/// vài mét ở rìa bán kính — đủ để một người đứng trong sân vẫn bị chặn. Công
/// thức Haversine tính trên mặt cầu, sai số dưới 0,5% trong phạm vi 100 m.
/// </para>
/// </summary>
public static class GeofencePolicy
{
    /// <summary>Bán kính mặc định theo PRD 4.1.</summary>
    public const double DEFAULT_RADIUS_METERS = 100d;

    /// <summary>Tên Wi-Fi nội bộ mặc định theo PRD 4.1.</summary>
    public const string DEFAULT_WIFI_SSID = "OverTimeHotel_Staff";

    /// <summary>Bán kính trái đất (mét) — giá trị trung bình dùng cho Haversine.</summary>
    private const double EARTH_RADIUS_METERS = 6371000d;

    /// <summary>
    /// Toạ độ hợp lệ nằm trong khoảng này. Ngoài khoảng tức là chuỗi rác hoặc
    /// thiết bị trả về 0,0 (lỗi định vị rất hay gặp khi GPS chưa có tín hiệu).
    /// </summary>
    private static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90d and <= 90d &&
        longitude is >= -180d and <= 180d &&
        !(latitude == 0d && longitude == 0d);

    /// <summary>
    /// Đọc chuỗi toạ độ đúng quy ước lưu trong <c>cham_cong.toa_do_checkin</c>:
    /// <c>"latitude,longitude"</c> theo InvariantCulture (dấu chấm thập phân).
    /// </summary>
    public static bool TryParseCoordinate(string? toaDo, out double latitude, out double longitude)
    {
        latitude = 0d;
        longitude = 0d;

        if (string.IsNullOrWhiteSpace(toaDo))
        {
            return false;
        }

        var parts = toaDo.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
        {
            return false;
        }

        if (!IsValidCoordinate(lat, lng))
        {
            return false;
        }

        latitude = lat;
        longitude = lng;
        return true;
    }

    /// <summary>
    /// Khoảng cách (mét) giữa hai toạ độ trên mặt cầu — công thức Haversine.
    /// </summary>
    public static double CalculateDistanceMeters(
        double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EARTH_RADIUS_METERS * c;
    }

    /// <summary>
    /// Xác thực hai lớp theo PRD 4.1: trong bán kính <b>hoặc</b> đúng Wi-Fi nội bộ.
    /// </summary>
    /// <param name="toaDo">Chuỗi toạ độ thiết bị gửi lên, có thể null khi mất GPS.</param>
    /// <param name="tenWifiHienTai">SSID đang kết nối, null khi không phải Wi-Fi.</param>
    /// <param name="hotelLatitude">Vĩ độ khách sạn lấy từ cấu hình.</param>
    /// <param name="hotelLongitude">Kinh độ khách sạn lấy từ cấu hình.</param>
    /// <param name="radiusMeters">Bán kính cho phép (mét).</param>
    /// <param name="wifiSsid">SSID nội bộ được chấp nhận.</param>
    public static GeofenceCheckResult Evaluate(
        string? toaDo,
        string? tenWifiHienTai,
        double hotelLatitude,
        double hotelLongitude,
        double radiusMeters = DEFAULT_RADIUS_METERS,
        string wifiSsid = DEFAULT_WIFI_SSID)
    {
        // Lớp 1 — GPS. Đây là lớp ưu tiên vì nó chứng minh được vị trí vật lý,
        // còn SSID thì bất kỳ ai đặt tên Wi-Fi trùng cũng qua được.
        double? khoangCach = null;
        if (TryParseCoordinate(toaDo, out var lat, out var lng))
        {
            khoangCach = CalculateDistanceMeters(lat, lng, hotelLatitude, hotelLongitude);
            if (khoangCach.Value <= radiusMeters)
            {
                return new GeofenceCheckResult(true, "GPS", khoangCach, null);
            }
        }

        // Lớp 2 — Wi-Fi nội bộ. So sánh không phân biệt hoa thường vì Android trả
        // SSID kèm dấu nháy kép ở một số dòng máy và đổi hoa thường tuỳ ROM.
        // Chỉ áp dụng khi wifiSsid được cấu hình (khác rỗng).
        if (!string.IsNullOrWhiteSpace(wifiSsid) &&
            !string.IsNullOrWhiteSpace(tenWifiHienTai) &&
            string.Equals(tenWifiHienTai.Trim().Trim('"'), wifiSsid, StringComparison.OrdinalIgnoreCase))
        {
            return new GeofenceCheckResult(true, "WIFI", khoangCach, null);
        }

        var coWifiConfig = !string.IsNullOrWhiteSpace(wifiSsid);
        var thongDiep = khoangCach.HasValue
            ? (coWifiConfig
                ? string.Create(CultureInfo.InvariantCulture,
                    $"Bạn đang ở cách khách sạn {khoangCach.Value:F0} m, ngoài phạm vi {radiusMeters:F0} m và không kết nối Wi-Fi \"{wifiSsid}\". Vui lòng di chuyển vào trong khuôn viên khách sạn rồi quẹt lại.")
                : string.Create(CultureInfo.InvariantCulture,
                    $"Bạn đang ở cách khách sạn {khoangCach.Value:F0} m, ngoài phạm vi {radiusMeters:F0} m. Vui lòng di chuyển vào trong khuôn viên khách sạn rồi quẹt lại."))
            : (coWifiConfig
                ? $"Không xác định được vị trí của bạn và thiết bị không kết nối Wi-Fi \"{wifiSsid}\". Vui lòng bật định vị (GPS) hoặc kết nối Wi-Fi nội bộ rồi quẹt lại."
                : "Không xác định được vị trí của bạn. Vui lòng bật định vị (GPS) rồi quẹt lại.");

        return new GeofenceCheckResult(false, null, khoangCach, thongDiep);
    }

    /// <summary>
    /// Đánh giá lại một bản ghi chấm công đã lưu, phục vụ màn hình kiểm tra của
    /// Web Admin. Chỉ dựa vào bằng chứng đã ghi (phương thức + toạ độ), không
    /// suy đoán thêm — bản ghi <c>WIFI</c> hoặc <c>MANUAL</c> do quản lý nhập tay
    /// luôn được coi là hợp lệ vì đã có người chịu trách nhiệm.
    /// </summary>
    public static GeofenceCheckResult EvaluateStored(
        string? phuongThuc,
        string? toaDo,
        double hotelLatitude,
        double hotelLongitude,
        double radiusMeters = DEFAULT_RADIUS_METERS)
    {
        if (string.Equals(phuongThuc, "MANUAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(phuongThuc, "WIFI", StringComparison.OrdinalIgnoreCase))
        {
            return new GeofenceCheckResult(true, phuongThuc?.ToUpperInvariant(), null, null);
        }

        if (!TryParseCoordinate(toaDo, out var lat, out var lng))
        {
            return new GeofenceCheckResult(false, null, null, "Không có toạ độ GPS hợp lệ.");
        }

        var khoangCach = CalculateDistanceMeters(lat, lng, hotelLatitude, hotelLongitude);
        var hopLe = khoangCach <= radiusMeters;

        return new GeofenceCheckResult(
            hopLe,
            "GPS",
            khoangCach,
            hopLe
                ? null
                : string.Create(CultureInfo.InvariantCulture,
                    $"Vị trí ghi nhận cách khách sạn {khoangCach:F0} m, vượt phạm vi {radiusMeters:F0} m."));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
