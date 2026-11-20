using System.ComponentModel.DataAnnotations;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class AttendanceLocationViewModel : IValidatableObject
{
    [Display(Name = "Tên cơ sở khách sạn")]
    [Required(ErrorMessage = "Vui lòng nhập tên cơ sở khách sạn.")]
    [StringLength(150, ErrorMessage = "Tên khách sạn tối đa 150 ký tự.")]
    public string HotelName { get; set; } = "The OverTime Hotel & Resort";

    [Display(Name = "Địa chỉ cơ sở")]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ cơ sở khách sạn.")]
    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
    public string Address { get; set; } = "123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh";

    [Display(Name = "Vĩ độ GPS (Latitude)")]
    [Required(ErrorMessage = "Vui lòng nhập vĩ độ GPS.")]
    [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ phải nằm trong khoảng từ -90 đến 90.")]
    public double HotelLatitude { get; set; } = 10.778848;

    [Display(Name = "Kinh độ GPS (Longitude)")]
    [Required(ErrorMessage = "Vui lòng nhập kinh độ GPS.")]
    [Range(-180.0, 180.0, ErrorMessage = "Kinh độ phải nằm trong khoảng từ -180 đến 180.")]
    public double HotelLongitude { get; set; } = 106.687421;

    [Display(Name = "Bán kính chấm công cho phép (Mét)")]
    [Required(ErrorMessage = "Vui lòng nhập bán kính quét.")]
    [Range(20.0, 5000.0, ErrorMessage = "Bán kính chấm công phải từ 20m đến 5000m.")]
    public double RadiusMeters { get; set; } = 250;

    [Display(Name = "Tên Wi-Fi nội bộ (SSID)")]
    [StringLength(64, ErrorMessage = "Tên Wi-Fi tối đa 64 ký tự.")]
    public string? WifiSsid { get; set; } = "OverTimeHotel_Staff";

    [Display(Name = "Bật lớp xác thực GPS")]
    public bool EnableGps { get; set; } = true;

    [Display(Name = "Bật lớp xác thực Wi-Fi nội bộ")]
    public bool EnableWifi { get; set; } = true;

    public DateTimeOffset? LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EnableGps && !EnableWifi)
        {
            yield return new ValidationResult(
                "Không thể tắt đồng thời cả lớp GPS và lớp Wi-Fi. Hệ thống bắt buộc phải bật ít nhất 1 lớp xác thực chấm công.",
                [nameof(EnableGps), nameof(EnableWifi)]);
        }

        if (EnableWifi && string.IsNullOrWhiteSpace(WifiSsid))
        {
            yield return new ValidationResult(
                "Vui lòng nhập Tên Wi-Fi nội bộ (SSID) khi đang bật lớp xác thực Wi-Fi.",
                [nameof(WifiSsid)]);
        }
    }
}
