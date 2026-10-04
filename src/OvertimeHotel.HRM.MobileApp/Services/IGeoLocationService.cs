namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ lấy toạ độ vị trí của thiết bị để ghi nhận khi nhân viên quẹt thẻ.
/// Tách riêng interface để ViewModel không phụ thuộc trực tiếp vào API nền tảng.
/// </summary>
public interface IGeoLocationService
{
    /// <summary>
    /// Lấy toạ độ hiện tại. Trả về null nếu người dùng từ chối quyền,
    /// thiết bị không có GPS hoặc lấy toạ độ quá thời gian cho phép.
    /// </summary>
    Task<string?> GetCurrentCoordinateAsync();

    /// <summary>
    /// Lấy tên thiết bị để ghi nhận vào hồ sơ chấm công.
    /// </summary>
    Task<string?> GetDeviceNameAsync();
}