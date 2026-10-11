using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;

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
    public int? MaPhongBan { get; set; }
    public string? Token { get; set; }
    public bool IsLoggedIn { get; set; }
    public DateTime ThoiGianDangNhap { get; set; } = DateTime.UtcNow;

    public bool IsManager => LeaveApprovalPolicy.IsManager(TenVaiTro);
    public bool IsHR => LeaveApprovalPolicy.IsHR(TenVaiTro);
    public bool IsAdmin => LeaveApprovalPolicy.IsAdmin(TenVaiTro);
    public bool IsIT => LeaveApprovalPolicy.IsIT(TenVaiTro);
    public bool CanApproveLeave => IsLoggedIn && (IsManager || IsHR || IsAdmin) && (!IsManager || MaPhongBan.HasValue);
    public bool IsEmployee => LeaveApprovalPolicy.NormalizeRole(TenVaiTro) == RoleType.Employee;

    /// <summary>
    /// Bám <see cref="AttendanceAccessPolicy.CanViewOwnAttendanceSummary"/> thay vì
    /// tự viết điều kiện — cùng một nguồn luật với tầng service.
    /// </summary>
    public bool CanViewAttendanceSummary => AttendanceAccessPolicy.CanViewOwnAttendanceSummary(TenVaiTro);
}
