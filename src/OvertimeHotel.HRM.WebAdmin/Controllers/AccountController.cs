using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AppDbContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Băm mật khẩu theo cùng cơ chế đang dùng trong dữ liệu tài khoản.
        var inputHash = PasswordHasher.Hash(model.Password);

        // Kiểm tra thông tin tài khoản trên Supabase
        var account = await _context.TaiKhoans
            .Include(t => t.NhanVien)
            .Include(t => t.VaiTro)
            .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == model.Username.Trim().ToLower() && t.MatKhauBam == inputHash);

        if (account == null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (!account.TrangThai)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị tạm khóa. Vui lòng liên hệ bộ phận IT / Admin.");
            return View(model);
        }

        var roleName = account.VaiTro?.TenVaiTro ?? "Employee";

        // Kiểm tra phân quyền cổng: Web Admin chỉ cho phép Admin và HR truy cập
        if (roleName != "Admin" && roleName != "HR")
        {
            ViewBag.RoleNotice = $"Tài khoản của bạn được phân quyền là '{roleName}'. Cổng Web Admin này chỉ dành cho cấp Quản trị (Admin) và Nhân sự (HR). Vui lòng đăng nhập trên Ứng dụng Di động (.NET MAUI App) để thực hiện quẹt thẻ chấm công và quản lý ca làm!";
            return View(model);
        }

        // Thiết lập Claims nhận diện người dùng
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, account.MaTaiKhoan.ToString()),
            new Claim(ClaimTypes.Name, account.TenDangNhap),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("FullName", account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap),
            new Claim("EmployeeId", account.MaNhanVien.ToString()),
            new Claim("Email", account.NhanVien?.Email ?? ""),
            new Claim("DepartmentId", account.NhanVien?.MaPhongBan.ToString() ?? "0")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        _logger.LogInformation("Người dùng {Username} ({Role}) đăng nhập thành công vào Web Admin.", account.TenDangNhap, roleName);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("Người dùng {Username} đã đăng xuất.", User.Identity?.Name);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
