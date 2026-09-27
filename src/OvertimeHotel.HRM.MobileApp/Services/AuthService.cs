using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ xác thực tài khoản Production kết nối WebAdmin Mobile API qua JWT Token.
/// Tuân thủ nguyên tắc Zero Trust: Không lưu trữ key nhạy cảm phía client, lưu token an toàn trong SecureStorage.
/// </summary>
public class AuthService : IAuthService
{
    private const string JwtStorageKey = "hrm_mobile_jwt_token";
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public bool IsDemoMode => false;
    public UserSession? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null && CurrentUser.IsLoggedIn;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(CurrentUser?.Token))
        {
            return CurrentUser.Token;
        }

        try
        {
            return await SecureStorage.GetAsync(JwtStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc JWT từ SecureStorage.");
            return null;
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResponse(false, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
        }

        try
        {
            var requestPayload = new LoginRequest(username.Trim(), password);
            var content = new StringContent(
                JsonSerializer.Serialize(requestPayload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/api/mobile/auth/login", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loginResult = JsonSerializer.Deserialize<LoginResponse>(responseJson, options);

            if (response.IsSuccessStatusCode && loginResult != null && loginResult.Success)
            {
                if (!string.IsNullOrEmpty(loginResult.Token))
                {
                    try
                    {
                        await SecureStorage.SetAsync(JwtStorageKey, loginResult.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi khi lưu JWT vào SecureStorage.");
                    }
                }

                // Thiết lập phiên đăng nhập
                CurrentUser = ParseUserSessionFromJwt(loginResult.Token, loginResult);
                _logger.LogInformation("Đăng nhập thành công cho người dùng '{Username}' (Vai trò: {Role}).", username, CurrentUser.TenVaiTro);

                return loginResult;
            }

            var errMsg = loginResult?.Message ?? "Tên đăng nhập hoặc mật khẩu không chính xác.";
            _logger.LogWarning("Đăng nhập thất bại: {Message}", errMsg);
            return new LoginResponse(false, errMsg);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối máy chủ API tại {BaseUrl}", ApiConfig.BaseUrl);
            return new LoginResponse(false, "Không thể kết nối tới máy chủ API. Vui lòng kiểm tra mạng hoặc thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi đăng nhập.");
            return new LoginResponse(false, "Đã xảy ra lỗi trong quá trình đăng nhập. Vui lòng thử lại.");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            SecureStorage.Remove(JwtStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xóa JWT khỏi SecureStorage.");
        }

        CurrentUser = null;
        await Task.CompletedTask;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(JwtStorageKey);
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var payload = DecodeJwtPayload(token);
            if (payload == null)
            {
                SecureStorage.Remove(JwtStorageKey);
                return false;
            }

            // Kiểm tra hạn token
            if (payload.Value.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expSec))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSec).UtcDateTime;
                if (expiresAt < DateTime.UtcNow)
                {
                    SecureStorage.Remove(JwtStorageKey);
                    return false;
                }
            }

            CurrentUser = ParseUserSessionFromJwt(token, null);
            return CurrentUser.IsLoggedIn;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi thử tự động đăng nhập từ JWT lưu trữ.");
            return false;
        }
    }

    public Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        return Task.FromResult((false, "Vui lòng thực hiện đổi mật khẩu trên cổng Web Admin."));
    }

    private static JsonElement? DecodeJwtPayload(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var jsonBytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(jsonBytes);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static UserSession ParseUserSessionFromJwt(string? token, LoginResponse? response)
    {
        var session = new UserSession
        {
            Token = token,
            IsLoggedIn = true,
            ThoiGianDangNhap = DateTime.UtcNow
        };

        if (response != null)
        {
            session.MaTaiKhoan = response.MaTaiKhoan ?? 0;
            session.MaNhanVien = response.MaNhanVien ?? 0;
            session.TenDangNhap = response.TenDangNhap ?? "";
            session.HoTen = response.HoTen ?? "";
            session.TenVaiTro = response.TenVaiTro ?? "Employee";
        }

        if (!string.IsNullOrEmpty(token))
        {
            var payload = DecodeJwtPayload(token);
            if (payload.HasValue)
            {
                var root = payload.Value;
                // nameid / sub
                if (session.MaTaiKhoan == 0)
                {
                    if (root.TryGetProperty("nameid", out var p1) && int.TryParse(p1.GetString(), out var id)) session.MaTaiKhoan = id;
                    else if (root.TryGetProperty("sub", out var p2) && int.TryParse(p2.GetString(), out var id2)) session.MaTaiKhoan = id2;
                }

                // EmployeeId
                if (session.MaNhanVien == 0 && root.TryGetProperty("EmployeeId", out var empProp) && int.TryParse(empProp.GetString(), out var empId))
                {
                    session.MaNhanVien = empId;
                }

                // DepartmentId
                if (!session.MaPhongBan.HasValue && root.TryGetProperty("DepartmentId", out var deptProp) && int.TryParse(deptProp.GetString(), out var deptId))
                {
                    session.MaPhongBan = deptId;
                }

                // FullName
                if (string.IsNullOrEmpty(session.HoTen) && root.TryGetProperty("FullName", out var fnProp))
                {
                    session.HoTen = fnProp.GetString() ?? "";
                }

                // role
                if (string.IsNullOrEmpty(session.TenVaiTro) || session.TenVaiTro == "Employee")
                {
                    if (root.TryGetProperty("role", out var rProp))
                        session.TenVaiTro = rProp.GetString() ?? "Employee";
                }

                // unique_name / name
                if (string.IsNullOrEmpty(session.TenDangNhap))
                {
                    if (root.TryGetProperty("unique_name", out var unProp)) session.TenDangNhap = unProp.GetString() ?? "";
                    else if (root.TryGetProperty("name", out var nProp)) session.TenDangNhap = nProp.GetString() ?? "";
                }
            }
        }

        return session;
    }
}
