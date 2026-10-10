using System.Globalization;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>Một ca trực hiển thị trên màn lịch 24/7.</summary>
public class ScheduleShiftItem(PhanCa shift)
{
    // Giờ CSDL lưu theo TIMESTAMPTZ (UTC). Khách sạn ở múi giờ +7 nên mọi
    // giờ hiển thị đều quy đổi về giờ khách sạn trước khi in ra.
    private static readonly TimeZoneInfo HotelTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "OvertimeHotel", TimeSpan.FromHours(7), "Giờ khách sạn", "Giờ khách sạn");

    private static DateTimeOffset ToHotelTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, HotelTimeZone);

    public string Name => shift.CaLamViec?.TenCa is { Length: > 0 } name ? name : $"Ca #{shift.MaCa}";

    public string TimeText =>
        $"{ToHotelTime(shift.BatDauDuKien):HH:mm} – {ToHotelTime(shift.KetThucDuKien):HH:mm}";

    // Ca đêm khách sạn là ca bắt đầu trước 06:00. Suy ra từ giờ bắt đầu thay
    // vì đọc cột qua_dem: mapper dùng chung chỉ gán CaLamViec.TenCa, không
    // đưa qua_dem vào PhanCa. Suy ra ở tầng hiển thị giữ được mapper một chỗ.
    public bool IsNightShift => ToHotelTime(shift.BatDauDuKien).Hour < 6;

    public string StatusText => shift.TrangThai switch
    {
        "HOAN_THANH" => "Đã hoàn thành",
        "VANG" => "Đánh dấu vắng",
        _ => "Đã phân ca"
    };

    public string StatusColor => shift.TrangThai switch
    {
        "HOAN_THANH" => "#047857",
        "VANG" => "#B91C1C",
        _ => "#1E3A8A"
    };

    public string StatusBackground => shift.TrangThai switch
    {
        "HOAN_THANH" => "#ECFDF5",
        "VANG" => "#FEF2F2",
        _ => "#EFF6FF"
    };
}

/// <summary>Một ngày trên màn lịch, gom các ca trong ngày đó lại.</summary>
public class ScheduleDayItem(DateOnly date, IReadOnlyList<ScheduleShiftItem> shifts, bool isToday)
{
    public string DateText => date.ToString("dd/MM", CultureInfo.InvariantCulture);
    public string Weekday => date.ToString("dddd", CultureInfo.GetCultureInfo("vi-VN"));
    public bool IsToday => isToday;
    public IReadOnlyList<ScheduleShiftItem> Shifts => shifts;
    public bool HasSchedule => shifts.Count > 0;

    public string SummaryText => shifts.Count == 0
        ? "Không có ca"
        : shifts.Count == 1
            ? $"1 ca · {shifts[0].TimeText}"
            : $"{shifts.Count} ca trong ngày";
}