using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
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
        OvertimeHotel.HRM.Core.Models.TaiKhoan? account = null;
        try
        {
            account = await _context.TaiKhoans
                .Include(t => t.NhanVien)
                .Include(t => t.VaiTro)
                .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == model.Username.Trim().ToLower() && t.MatKhauBam == inputHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối CSDL Supabase khi người dùng {Username} đăng nhập. Chuyển sang cơ chế xác thực tài khoản mẫu.", model.Username);
            account = GetDevSeedAccounts().FirstOrDefault(t => t.TenDangNhap.ToLower() == model.Username.Trim().ToLower() && t.MatKhauBam == inputHash);
            if (account == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác (hoặc CSDL Supabase chưa sẵn sàng).");
                return View(model);
            }
        }

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

        var permissionCodes = Array.Empty<string>();
        try
        {
            permissionCodes = await _context.VaiTroQuyens
                .AsNoTracking()
                .Where(item => item.MaVaiTro == account.MaVaiTro)
                .Select(item => item.Quyen!.MaQuyenCode)
                .ToArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể nạp quyền của vai trò {RoleId} khi đăng nhập.", account.MaVaiTro);
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
        claims.AddRange(permissionCodes.Select(code => new Claim(PermissionCodes.ClaimType, code)));

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

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var username = User.Identity?.Name ?? "";
        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(n => n!.PhongBan)
            .Include(t => t.NhanVien)
                .ThenInclude(n => n!.ChucVu)
            .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == username.ToLower());

        if (account == null)
        {
            return NotFound();
        }

        var model = new UserProfileViewModel
        {
            AccountId = account.MaTaiKhoan,
            EmployeeId = account.MaNhanVien,
            Username = account.TenDangNhap,
            FullName = account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap,
            Email = account.NhanVien?.Email ?? "",
            Phone = account.NhanVien?.DienThoai ?? "",
            Address = account.NhanVien?.DiaChi ?? "Chưa cập nhật",
            Department = account.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn",
            Position = account.NhanVien?.ChucVu?.TenChucVu ?? "Nhân viên",
            RoleName = account.VaiTro?.TenVaiTro ?? "User",
            StartDate = account.NhanVien?.NgayVaoLam ?? DateOnly.FromDateTime(DateTime.Today),
            IsActive = account.TrangThai
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(UserProfileViewModel input)
    {
        var username = User.Identity?.Name ?? "";
        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(n => n!.PhongBan)
            .Include(t => t.NhanVien)
                .ThenInclude(n => n!.ChucVu)
            .FirstOrDefaultAsync(t => t.TenDangNhap.ToLower() == username.ToLower());

        if (account == null) return NotFound();

        // Validate
        if (string.IsNullOrWhiteSpace(input.ChangePassword.CurrentPassword))
        {
            ModelState.AddModelError("ChangePassword.CurrentPassword", "Vui lòng nhập mật khẩu hiện tại.");
        }
        else
        {
            var currentHash = PasswordHasher.Hash(input.ChangePassword.CurrentPassword);
            if (account.MatKhauBam != currentHash)
            {
                ModelState.AddModelError("ChangePassword.CurrentPassword", "Mật khẩu hiện tại không chính xác.");
            }
        }

        if (string.IsNullOrWhiteSpace(input.ChangePassword.NewPassword) || input.ChangePassword.NewPassword.Length < 6)
        {
            ModelState.AddModelError("ChangePassword.NewPassword", "Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        if (input.ChangePassword.NewPassword != input.ChangePassword.ConfirmPassword)
        {
            ModelState.AddModelError("ChangePassword.ConfirmPassword", "Xác nhận mật khẩu mới không khớp.");
        }

        if (!ModelState.IsValid)
        {
            input.AccountId = account.MaTaiKhoan;
            input.EmployeeId = account.MaNhanVien;
            input.Username = account.TenDangNhap;
            input.FullName = account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap;
            input.Email = account.NhanVien?.Email ?? "";
            input.Phone = account.NhanVien?.DienThoai ?? "";
            input.Address = account.NhanVien?.DiaChi ?? "Chưa cập nhật";
            input.Department = account.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn";
            input.Position = account.NhanVien?.ChucVu?.TenChucVu ?? "Nhân viên";
            input.RoleName = account.VaiTro?.TenVaiTro ?? "User";
            input.StartDate = account.NhanVien?.NgayVaoLam ?? DateOnly.FromDateTime(DateTime.Today);
            input.IsActive = account.TrangThai;
            return View("Profile", input);
        }

        // Cập nhật mật khẩu mới trên Supabase
        account.MatKhauBam = PasswordHasher.Hash(input.ChangePassword.NewPassword);

        // Ghi nhật ký quản trị
        try
        {
            _context.NhatKyQuanTris.Add(new NhatKyQuanTri
            {
                MaTaiKhoanThucHien = account.MaTaiKhoan,
                MaTaiKhoanBiTacDong = account.MaTaiKhoan,
                TenNguoiThucHien = account.TenDangNhap,
                TenTaiKhoanBiTacDong = account.TenDangNhap,
                HanhDong = "DOI_MAT_KHAU",
                NoiDung = $"Người dùng '{account.TenDangNhap}' tự đổi mật khẩu cá nhân.",
                GiaTriCu = "***",
                GiaTriMoi = "***",
                LyDo = "Người dùng đổi mật khẩu định kỳ",
                DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                ThoiGian = DateTimeOffset.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi nhật ký đổi mật khẩu.");
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Đổi mật khẩu thành công! Mật khẩu mới đã có hiệu lực.";
        return RedirectToAction(nameof(Profile));
    }

    private static List<TaiKhoan> GetDevSeedAccounts()
    {
        return
        [
            new TaiKhoan
            {
                MaTaiKhoan = 1,
                MaNhanVien = 1,
                MaVaiTro = 1,
                TenDangNhap = "admin",
                MatKhauBam = "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7",
                TrangThai = true,
                VaiTro = new VaiTro { MaVaiTro = 1, TenVaiTro = "Admin" },
                NhanVien = new NhanVien { MaNhanVien = 1, Ho = "Nguyễn Đình", Ten = "Cường", Email = "cuong.nguyen@overtimehotel.com", MaPhongBan = 1 }
            },
            new TaiKhoan
            {
                MaTaiKhoan = 2,
                MaNhanVien = 2,
                MaVaiTro = 2,
                TenDangNhap = "hr_sang",
                MatKhauBam = "d2dcec9f7289f448fcf9f6e8c722961e61b60f8d540bbd633332431b57a7869c",
                TrangThai = true,
                VaiTro = new VaiTro { MaVaiTro = 2, TenVaiTro = "HR" },
                NhanVien = new NhanVien { MaNhanVien = 2, Ho = "Võ Huỳnh Minh", Ten = "Sang", Email = "sang.vo@overtimehotel.com", MaPhongBan = 2 }
            },
            new TaiKhoan
            {
                MaTaiKhoan = 3,
                MaNhanVien = 3,
                MaVaiTro = 3,
                TenDangNhap = "mgr_long",
                MatKhauBam = "e8392925a98c9c22795d1fc5d0dfee5b9a6943f6b768ec5a2a0c077e5ed119cf",
                TrangThai = true,
                VaiTro = new VaiTro { MaVaiTro = 3, TenVaiTro = "Manager" },
                NhanVien = new NhanVien { MaNhanVien = 3, Ho = "Nguyễn Hoàng", Ten = "Long", Email = "long.nguyen@overtimehotel.com", MaPhongBan = 3 }
            },
            new TaiKhoan
            {
                MaTaiKhoan = 4,
                MaNhanVien = 4,
                MaVaiTro = 4,
                TenDangNhap = "emp_bao",
                MatKhauBam = "ca35068eabfd1dc3ba09cbe6255036fb1e69e3e0b1e95a52f1494638bc759071",
                TrangThai = true,
                VaiTro = new VaiTro { MaVaiTro = 4, TenVaiTro = "Employee" },
                NhanVien = new NhanVien { MaNhanVien = 4, Ho = "Châu Quốc", Ten = "Bảo", Email = "bao.chau@overtimehotel.com", MaPhongBan = 4 }
            }
        ];
    }
}
