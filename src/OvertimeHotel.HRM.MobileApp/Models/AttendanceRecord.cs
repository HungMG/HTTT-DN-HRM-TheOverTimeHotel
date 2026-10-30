namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Một dòng lịch sử chấm công đã được định dạng sẵn để hiển thị trên giao diện.
/// </summary>
public class AttendanceRecord
{
    /// <summary>Ngày làm việc dạng DateOnly để lọc và đối chiếu.</summary>
    public DateOnly NgayLamViecDate { get; set; }

    /// <summary>Ngày làm việc, định dạng dd/MM/yyyy.</summary>
    public string NgayLamViec { get; set; } = string.Empty;

    /// <summary>Tên ca làm việc.</summary>
    public string TenCa { get; set; } = string.Empty;

    /// <summary>Giờ quẹt thẻ vào, định dạng HH:mm. Rỗng khi chưa quẹt thẻ.</summary>
    public string CheckInText { get; set; } = string.Empty;

    /// <summary>Giờ quẹt thẻ ra, định dạng HH:mm. Rỗng khi chưa quẹt thẻ ra.</summary>
    public string CheckOutText { get; set; } = string.Empty;

    /// <summary>Số phút đi trễ đã định dạng kèm đơn vị.</summary>
    public string PhutDiTreText { get; set; } = "0 phút";

    /// <summary>Số phút về sớm đã định dạng kèm đơn vị.</summary>
    public string PhutVeSomText { get; set; } = "0 phút";

    /// <summary>Số giờ làm thêm đã định dạng kèm đơn vị.</summary>
    public string GioOtText { get; set; } = "0 giờ";

    /// <summary>Nhãn trạng thái hiển thị.</summary>
    public string TrangThaiText { get; set; } = string.Empty;

    /// <summary>Mã trạng thái thô từ máy chủ, dùng để chọn màu badge.</summary>
    public string TrangThaiCode { get; set; } = string.Empty;

    /// <summary>Màu nền badge trạng thái (mã màu hex).</summary>
    public string TrangThaiBgColor { get; set; } = "#F1F5F9";

    /// <summary>Màu chữ badge trạng thái (mã màu hex).</summary>
    public string TrangThaiTextColor { get; set; } = "#475569";
}