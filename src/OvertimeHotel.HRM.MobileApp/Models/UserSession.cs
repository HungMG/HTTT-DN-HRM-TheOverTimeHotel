namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Lưu trữ phiên làm việc của người dùng đang đăng nhập trên Mobile App.
/// </summary>
public class UserSession
{
    public int MaTaiKhoan { get; set; }
    public int MaNhanVien { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string TenVaiTro { get; set; } = "Employee";
    public string TenPhongBan { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; }
    public DateTime ThoiGianDangNhap { get; set; } = DateTime.UtcNow;

    public bool IsManager => TenVaiTro.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                             TenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsEmployee => TenVaiTro.Equals("Employee", StringComparison.OrdinalIgnoreCase);
}
