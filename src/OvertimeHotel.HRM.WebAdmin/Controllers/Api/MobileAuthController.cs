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

/// <summary>
/// Đăng nhập kiểu cũ: Web Admin tự ký JWT bằng HS256 (<c>Jwt:Key</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>ĐÃ LỖI THỜI — không còn client nào gọi.</b>
/// </para>
/// <para>
/// Theo PRD 06, Mobile App đăng nhập thẳng vào Supabase GoTrue và nhận token do
/// chính GoTrue ký (ES256 qua JWKS). Endpoint này tồn tại để các bản app cũ đã
/// cài trên máy người dùng không chết ngay khi backend đổi lớp xác thực.
/// </para>
/// <para>
/// <b>Không được mở rộng thêm tính năng lên đây.</b> Khi đã xoá hẳn bản app cũ
/// thì xoá luôn controller này cùng cấu hình <c>Jwt:Key</c> trong appsettings.
/// </para>
/// <para>
/// Lớp xác thực <c>MobileBearer</c> hiện đã chuyển sang JWKS của Supabase, nên
/// token do endpoint này cấp <b>không còn được chấp nhận</b> nữa — dù đăng nhập
/// thành công thì mọi API phía sau vẫn trả 401. Giữ lại chỉ để endpoint trả lỗi
/// rõ ràng cho bản app cũ thay vì trả 404 gây khó chẩn đoán.
/// </para>
/// </remarks>
[Obsolete("Mobile App đã chuyển sang đăng nhập trực tiếp Supabase GoTrue. " +
          "Token do endpoint này cấp không còn được MobileBearer chấp nhận.")]
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
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == username.ToLower());

        // Kiểm tra bảo mật: Không tiết lộ username đúng hay password đúng (nguyên tắc Zero Trust)
        if (account == null || account.MatKhauBam != hashedPassword || account.TrangThai != true)
        {
            _logger.LogWarning("Đăng nhập Mobile thất bại cho tài khoản '{Username}'.", username);
            return Unauthorized(new LoginResponse(false, "Tên đăng nhập hoặc mật khẩu không chính xác."));
        }

        // Kiểm tra hiệu lực hợp đồng lao động của nhân viên
        var latestContract = await _context.HopDongs
            .AsNoTracking()
            .Where(h => h.MaNhanVien == account.MaNhanVien)
            .OrderByDescending(h => h.NgayBatDau)
            .FirstOrDefaultAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (latestContract != null && latestContract.NgayKetThuc.HasValue && latestContract.NgayKetThuc.Value < today)
        {
            return Unauthorized(new LoginResponse(false, $"Hợp đồng lao động của bạn đã hết hạn ngày {latestContract.NgayKetThuc.Value:dd/MM/yyyy}. Vui lòng liên hệ phòng Nhân sự để gia hạn trước khi tiếp tục sử dụng app."));
        }

        var roleName = account.VaiTro?.TenVaiTro ?? "Employee";
        var hoTen = account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap;
        var deptId = account.NhanVien?.MaPhongBan;
        // Mobile hiển thị "Phạm vi quyền lý" và dòng phụ vai trò — cần TÊN phòng ban/chức vụ,
        // không phải mã. JWT chỉ mang DepartmentId nên phải trả kèm trong body.
        var tenPhongBan = account.NhanVien?.PhongBan?.TenPhongBan;
        var tenChucVu = account.NhanVien?.ChucVu?.TenChucVu;

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
            TenPhongBan: tenPhongBan,
            TenChucVu: tenChucVu,
            Token: token
        ));
    }

    private string GenerateJwtToken(int accountId, int employeeId, string username, string role, int? departmentId, string fullName)
    {
        var jwtKey = !string.IsNullOrWhiteSpace(_configuration["Jwt:Key"])
            ? _configuration["Jwt:Key"]
            : Environment.GetEnvironmentVariable("JWT_KEY");
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("Bảo mật hệ thống (Fail-Closed): Khóa bí mật Jwt:Key chưa được cấu hình.");
        }
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
