using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Giao diện dịch vụ xác thực tài khoản và quản lý phiên đăng nhập cho Mobile App.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Thông tin phiên đăng nhập của người dùng hiện tại.
    /// </summary>
    UserSession? CurrentUser { get; }

    /// <summary>
    /// Kiểm tra người dùng đã đăng nhập chưa.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Thực hiện đăng nhập với tên người dùng và mật khẩu.
    /// </summary>
    Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true);

    /// <summary>
    /// Đăng xuất khỏi thiết bị.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Tự động khôi phục phiên đăng nhập từ bộ nhớ thiết bị nếu đã chọn Ghi nhớ.
    /// </summary>
    Task<bool> TryAutoLoginAsync();

    /// <summary>
    /// Đổi mật khẩu tài khoản người dùng hiện tại.
    /// </summary>
    Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword);
}
