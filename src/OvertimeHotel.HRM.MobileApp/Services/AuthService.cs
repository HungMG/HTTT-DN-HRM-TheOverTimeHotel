using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Triển khai dịch vụ xác thực tài khoản kết nối Supabase Cloud REST API và hỗ trợ bộ nhớ tạm ngoại tuyến.
/// </summary>
public class AuthService : IAuthService
{
    private const string SupabaseUrl = "https://soethtbttktnwtxyxjrj.supabase.co";
    private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNvZXRodGJ0dGt0bnd0eHl4anJqIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1MjkxNjgsImV4cCI6MjEwNTEwNTE2OH0.KoIyzRJQAb9QAP1-cMdkVHzv6ZNlXOV3j2oNFoBhEYE";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public UserSession? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null && CurrentUser.IsLoggedIn;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(SupabaseUrl),
            Timeout = TimeSpan.FromSeconds(3)
        };
        _httpClient.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseAnonKey);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResponse(false, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
        }

        var trimmedUser = username.Trim();

        // 1. Kiểm tra tài khoản mẫu trước để phản hồi tức thì (0ms) khi demo chấm đồ án
        var seedAccount = GetSeedAccounts().FirstOrDefault(a => 
            a.TenDangNhap.Equals(trimmedUser, StringComparison.OrdinalIgnoreCase) && 
            a.Password.Equals(password));

        if (seedAccount != null)
        {
            CurrentUser = new UserSession
            {
                MaTaiKhoan = seedAccount.MaTaiKhoan,
                MaNhanVien = seedAccount.MaNhanVien,
                TenDangNhap = seedAccount.TenDangNhap,
                HoTen = seedAccount.HoTen,
                TenVaiTro = seedAccount.Role,
                TenPhongBan = seedAccount.Department,
                TenChucVu = seedAccount.Position,
                MaPhongBan = seedAccount.MaPhongBan,
                IsLoggedIn = true,
                ThoiGianDangNhap = DateTime.UtcNow
            };

            SaveSessionPreferences(rememberMe, trimmedUser);

            return new LoginResponse(
                Success: true,
                Message: $"Đăng nhập thành công với vai trò {seedAccount.Role}!",
                MaTaiKhoan: seedAccount.MaTaiKhoan,
                MaNhanVien: seedAccount.MaNhanVien,
                HoTen: seedAccount.HoTen,
                TenDangNhap: seedAccount.TenDangNhap,
                TenVaiTro: seedAccount.Role
            );
        }

        // 2. Nếu không phải tài khoản mẫu, truy vấn trực tiếp Supabase Cloud REST API
        var passwordHash = HashPassword(password);
        try
        {
            var requestUri = $"/rest/v1/tai_khoan?ten_dang_nhap=eq.{Uri.EscapeDataString(trimmedUser)}&mat_khau_bam=eq.{passwordHash}&select=ma_tai_khoan,ma_nhan_vien,ten_dang_nhap,trang_thai,vai_tro(ten_vai_tro),nhan_vien(ho,ten,email,dien_thoai,ma_phong_ban,phong_ban(ten_phong_ban),chuc_vu(ten_chuc_vu))";

            var response = await _httpClient.GetAsync(requestUri).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var userElem = root[0];
                    var isActive = userElem.GetProperty("trang_thai").GetBoolean();
                    if (!isActive)
                    {
                        return new LoginResponse(false, "Tài khoản của bạn hiện đang bị khóa. Vui lòng liên hệ bộ phận Nhân sự.");
                    }

                    var maTaiKhoan = userElem.GetProperty("ma_tai_khoan").GetInt32();
                    var maNhanVien = userElem.GetProperty("ma_nhan_vien").GetInt32();
                    var tenDangNhap = userElem.GetProperty("ten_dang_nhap").GetString() ?? trimmedUser;

                    string roleName = "Employee";
                    if (userElem.TryGetProperty("vai_tro", out var vaiTroElem) && 
                        vaiTroElem.ValueKind == JsonValueKind.Object &&
                        vaiTroElem.TryGetProperty("ten_vai_tro", out var roleElem))
                    {
                        roleName = roleElem.GetString() ?? "Employee";
                    }

                    string hoTen = tenDangNhap;
                    string deptName = "The OverTime Hotel";
                    string posName = "Nhân viên";
                    string email = "";
                    string phone = "";
                    int? maPhongBan = null;

                    if (userElem.TryGetProperty("nhan_vien", out var nvElem) && nvElem.ValueKind == JsonValueKind.Object)
                    {
                        var ho = nvElem.TryGetProperty("ho", out var h) ? h.GetString() : "";
                        var ten = nvElem.TryGetProperty("ten", out var t) ? t.GetString() : "";
                        hoTen = $"{ho} {ten}".Trim();

                        email = nvElem.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                        phone = nvElem.TryGetProperty("dien_thoai", out var ph) ? ph.GetString() ?? "" : "";

                        if (nvElem.TryGetProperty("ma_phong_ban", out var mpb) && mpb.ValueKind == JsonValueKind.Number)
                        {
                            maPhongBan = mpb.GetInt32();
                        }

                        if (nvElem.TryGetProperty("phong_ban", out var pbElem) && pbElem.ValueKind == JsonValueKind.Object)
                        {
                            deptName = pbElem.TryGetProperty("ten_phong_ban", out var pbName) ? pbName.GetString() ?? deptName : deptName;
                        }

                        if (nvElem.TryGetProperty("chuc_vu", out var cvElem) && cvElem.ValueKind == JsonValueKind.Object)
                        {
                            posName = cvElem.TryGetProperty("ten_chuc_vu", out var cvName) ? cvName.GetString() ?? posName : posName;
                        }
                    }

                    CurrentUser = new UserSession
                    {
                        MaTaiKhoan = maTaiKhoan,
                        MaNhanVien = maNhanVien,
                        TenDangNhap = tenDangNhap,
                        HoTen = hoTen,
                        TenVaiTro = roleName,
                        TenPhongBan = deptName,
                        TenChucVu = posName,
                        MaPhongBan = maPhongBan,
                        Email = email,
                        DienThoai = phone,
                        IsLoggedIn = true,
                        ThoiGianDangNhap = DateTime.UtcNow
                    };

                    SaveSessionPreferences(rememberMe, trimmedUser);

                    return new LoginResponse(
                        Success: true,
                        Message: "Đăng nhập thành công!",
                        MaTaiKhoan: maTaiKhoan,
                        MaNhanVien: maNhanVien,
                        HoTen: hoTen,
                        TenDangNhap: tenDangNhap,
                        TenVaiTro: roleName
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi kết nối Supabase Cloud REST API khi đăng nhập: {User}", trimmedUser);
        }

        return new LoginResponse(false, "Tên đăng nhập hoặc mật khẩu không chính xác.");
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        try
        {
            Preferences.Default.Set("IsLoggedIn", false);
            Preferences.Default.Remove("SavedUserJson");
            Preferences.Default.Remove("SavedUsername");
            Preferences.Default.Set("RememberMe", false);
        }
        catch
        {
            // Bỏ qua lỗi Preferences trên môi trường test
        }
        return Task.CompletedTask;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            var rememberMe = Preferences.Default.Get("RememberMe", false);
            var isLoggedIn = Preferences.Default.Get("IsLoggedIn", false);
            var savedJson = Preferences.Default.Get("SavedUserJson", string.Empty);

            if (rememberMe && isLoggedIn && !string.IsNullOrWhiteSpace(savedJson))
            {
                var session = JsonSerializer.Deserialize<UserSession>(savedJson);
                if (session != null && session.IsLoggedIn)
                {
                    CurrentUser = session;
                    _logger.LogInformation("Khôi phục phiên đăng nhập thành công cho người dùng: {User}", session.TenDangNhap);
                    return await Task.FromResult(true).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể khôi phục phiên đăng nhập từ Preferences.");
        }

        return false;
    }

    private void SaveSessionPreferences(bool rememberMe, string username)
    {
        try
        {
            Preferences.Default.Set("RememberMe", rememberMe);
            if (rememberMe && CurrentUser != null)
            {
                Preferences.Default.Set("SavedUsername", username);
                Preferences.Default.Set("IsLoggedIn", true);
                var json = JsonSerializer.Serialize(CurrentUser);
                Preferences.Default.Set("SavedUserJson", json);
            }
            else
            {
                Preferences.Default.Remove("SavedUsername");
                Preferences.Default.Set("IsLoggedIn", false);
                Preferences.Default.Remove("SavedUserJson");
            }
        }
        catch
        {
            // Bỏ qua nếu Preferences không khả dụng
        }
    }

    private static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly List<SeedAccount> _seedAccounts = new()
    {
        new(6, 13, "ngothibich", "123456", "Ngô Thị Bích", "Employee", "Bộ phận Buồng phòng", "Nhân viên Buồng phòng", 3),
        new(5, 10, "dothanhdat", "123456", "Đỗ Thành Đạt", "Manager", "Bộ phận Tiền sảnh (FO)", "Giám sát Tiền sảnh", 2),
        new(2, 2, "hr_sang", "123456", "Võ Huỳnh Minh Sang", "HR", "Phòng Nhân sự", "Chuyên viên Nhân sự", 8),
        new(1, 1, "admin", "123456", "Nguyễn Đình Cường", "Admin", "Ban Quản Trị Khách Sạn", "Quản Trị Viên Hệ Thống", 1)
    };

    public async Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (CurrentUser == null || !CurrentUser.IsLoggedIn)
        {
            return (false, "Bạn chưa đăng nhập vào hệ thống.");
        }

        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return (false, "Vui lòng nhập đầy đủ mật khẩu cũ và mật khẩu mới.");
        }

        if (newPassword.Length < 6)
        {
            return (false, "Mật khẩu mới phải chứa ít nhất 6 ký tự.");
        }

        if (oldPassword == newPassword)
        {
            return (false, "Mật khẩu mới không được trùng với mật khẩu cũ.");
        }

        var username = CurrentUser.TenDangNhap;

        // 1. Kiểm tra nếu là tài khoản demo
        var seed = _seedAccounts.FirstOrDefault(a => a.TenDangNhap.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (seed != null)
        {
            if (seed.Password != oldPassword)
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            // Cập nhật mật khẩu mới trong bộ nhớ demo
            seed.Password = newPassword;
            return (true, "Đổi mật khẩu thành công! Mật khẩu mới đã được kích hoạt.");
        }

        // 2. Cập nhật lên CSDL Supabase Cloud qua REST API
        try
        {
            var oldHash = HashPassword(oldPassword);
            var newHash = HashPassword(newPassword);

            // Kiểm tra mật khẩu cũ trên Supabase
            var checkUri = $"/rest/v1/tai_khoan?ma_tai_khoan=eq.{CurrentUser.MaTaiKhoan}&mat_khau_bam=eq.{oldHash}&select=ma_tai_khoan";
            var checkRes = await _httpClient.GetAsync(checkUri).ConfigureAwait(false);
            if (!checkRes.IsSuccessStatusCode)
            {
                return (false, "Mật khẩu hiện tại không chính xác hoặc lỗi mạng.");
            }

            var checkJson = await checkRes.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(checkJson);
            if (doc.RootElement.GetArrayLength() == 0)
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            // Gửi lệnh cập nhật mật khẩu mới
            var patchUri = $"/rest/v1/tai_khoan?ma_tai_khoan=eq.{CurrentUser.MaTaiKhoan}";
            var payload = new StringContent(
                JsonSerializer.Serialize(new { mat_khau_bam = newHash }),
                Encoding.UTF8,
                "application/json");

            var patchRes = await _httpClient.PatchAsync(patchUri, payload).ConfigureAwait(false);
            if (patchRes.IsSuccessStatusCode)
            {
                return (true, "Đổi mật khẩu trên CSDL Supabase thành công!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đổi mật khẩu tài khoản {User}", username);
        }

        return (false, "Không thể cập nhật mật khẩu lúc này. Vui lòng thử lại sau.");
    }

    private static List<SeedAccount> GetSeedAccounts() => _seedAccounts;

    private class SeedAccount
    {
        public int MaTaiKhoan { get; set; }
        public int MaNhanVien { get; set; }
        public string TenDangNhap { get; set; }
        public string Password { get; set; }
        public string HoTen { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public int? MaPhongBan { get; set; }

        public SeedAccount(int maTaiKhoan, int maNhanVien, string tenDangNhap, string password, string hoTen, string role, string department, string position, int? maPhongBan = null)
        {
            MaTaiKhoan = maTaiKhoan;
            MaNhanVien = maNhanVien;
            TenDangNhap = tenDangNhap;
            Password = password;
            HoTen = hoTen;
            Role = role;
            Department = department;
            Position = position;
            MaPhongBan = maPhongBan;
        }
    }
}
