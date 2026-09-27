using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ xác thực dành riêng cho CHẾ ĐỘ DEMO.
/// Hoạt động 100% trên bộ nhớ cục bộ, không gọi API hay Cloud, đảm bảo an toàn tuyệt đối khi thuyết trình.
/// </summary>
public class DemoAuthService : IAuthService
{
    private readonly ILogger<DemoAuthService> _logger;

    public bool IsDemoMode => true;
    public UserSession? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null && CurrentUser.IsLoggedIn;

    public DemoAuthService(ILogger<DemoAuthService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string?> GetTokenAsync()
    {
        return Task.FromResult<string?>(CurrentUser?.Token ?? "demo-mode-token");
    }

    public Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(new LoginResponse(false, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu."));
        }

        var trimmedUser = username.Trim();
        var seedAccounts = GetSeedAccounts();
        var account = seedAccounts.FirstOrDefault(a =>
            a.TenDangNhap.Equals(trimmedUser, StringComparison.OrdinalIgnoreCase) &&
            a.Password.Equals(password));

        if (account == null)
        {
            _logger.LogWarning("[DEMO] Đăng nhập thất bại cho '{Username}'", trimmedUser);
            return Task.FromResult(new LoginResponse(false, "Tên đăng nhập hoặc mật khẩu demo không chính xác."));
        }

        CurrentUser = new UserSession
        {
            MaTaiKhoan = account.MaTaiKhoan,
            MaNhanVien = account.MaNhanVien,
            TenDangNhap = account.TenDangNhap,
            HoTen = account.HoTen,
            TenVaiTro = account.Role,
            TenPhongBan = account.Department,
            TenChucVu = account.Position,
            Email = account.Email,
            DienThoai = account.Phone,
            MaPhongBan = account.MaPhongBan,
            Token = "demo-session-token-jwt",
            IsLoggedIn = true,
            ThoiGianDangNhap = DateTime.UtcNow
        };

        _logger.LogInformation("[DEMO] Tài khoản '{Username}' ({Role}) đăng nhập Demo thành công.", trimmedUser, account.Role);

        return Task.FromResult(new LoginResponse(
            Success: true,
            Message: "[CHẾ ĐỘ DEMO] Đăng nhập thành công.",
            MaTaiKhoan: account.MaTaiKhoan,
            MaNhanVien: account.MaNhanVien,
            HoTen: account.HoTen,
            TenDangNhap: account.TenDangNhap,
            TenVaiTro: account.Role,
            Token: CurrentUser.Token
        ));
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        return Task.CompletedTask;
    }

    public Task<bool> TryAutoLoginAsync()
    {
        return Task.FromResult(false);
    }

    public Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        return Task.FromResult((true, "[DEMO] Đổi mật khẩu thành công trong bộ nhớ tạm."));
    }

    private static List<DemoAccountSeed> GetSeedAccounts() => new()
    {
        new(1, 1, "admin", "admin123", "Nguyễn Quản Trị", "Admin", "Ban Giám Đốc", "Tổng Giám Đốc", "admin@overtimehotel.com", "0901234567", 1),
        new(2, 2, "hr", "hr123", "Trần Thị Thu Thảo", "HR", "Phòng Nhân Sự", "Trưởng Phòng Nhân Sự", "thao.tran@overtimehotel.com", "0912345678", 2),
        new(3, 4, "manager", "manager123", "Lê Văn Lương", "Manager", "Bộ Phận Tiền Sảnh (FO)", "Trưởng Bộ Phận Lễ Tân", "luong.le@overtimehotel.com", "0923456789", 4),
        new(4, 5, "employee", "emp123", "Phạm Thị Mai", "Employee", "Bộ Phận Tiền Sảnh (FO)", "Nhân Viên Lễ Tân", "mai.pham@overtimehotel.com", "0934567890", 4)
    };

    private record DemoAccountSeed(
        int MaTaiKhoan,
        int MaNhanVien,
        string TenDangNhap,
        string Password,
        string HoTen,
        string Role,
        string Department,
        string Position,
        string Email,
        string Phone,
        int? MaPhongBan
    );
}
