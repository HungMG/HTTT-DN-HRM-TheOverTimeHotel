using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

[ApiController]
[Route("api/mobile/auth")]
[AllowAnonymous]
public class MobileAuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<MobileAuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse(false, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu."));
        }

        var username = request.Username.Trim();
        var hashedPassword = PasswordHasher.Hash(request.Password);

        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == username.ToLower());

        // Kiểm tra bảo mật: Không tiết lộ username đúng hay password đúng (nguyên tắc Zero Trust)
        if (account == null || account.MatKhauBam != hashedPassword || account.TrangThai != true)
        {
            _logger.LogWarning("Đăng nhập Mobile thất bại cho tài khoản '{Username}'.", username);
            return Unauthorized(new LoginResponse(false, "Tên đăng nhập hoặc mật khẩu không chính xác."));
        }

        var roleName = account.VaiTro?.TenVaiTro ?? "Employee";
        var hoTen = account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap;
        var deptId = account.NhanVien?.MaPhongBan;

        // Sinh JWT Token
        var token = GenerateJwtToken(account.MaTaiKhoan, account.MaNhanVien, account.TenDangNhap, roleName, deptId, hoTen);

        _logger.LogInformation("Tài khoản '{Username}' (Vai trò: {Role}) đăng nhập Mobile thành công.", username, roleName);

        return Ok(new LoginResponse(
            Success: true,
            Message: "Đăng nhập thành công.",
            MaTaiKhoan: account.MaTaiKhoan,
            MaNhanVien: account.MaNhanVien,
            HoTen: hoTen,
            TenDangNhap: account.TenDangNhap,
            TenVaiTro: roleName,
            Token: token
        ));
    }

    private string GenerateJwtToken(int accountId, int employeeId, string username, string role, int? departmentId, string fullName)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "OvertimeHotel_Super_Secret_JWT_Signing_Key_2026_Secure_Key_HMAC256";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "OvertimeHotel.HRM";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "OvertimeHotel.MobileApp";
        var expireHours = int.TryParse(_configuration["Jwt:ExpireHours"], out var h) ? h : 8;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accountId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new("EmployeeId", employeeId.ToString()),
            new("FullName", fullName)
        };

        if (departmentId.HasValue)
        {
            claims.Add(new Claim("DepartmentId", departmentId.Value.ToString()));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
