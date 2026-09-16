using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin")]
public class UserAccountsController : Controller
{
    private static readonly string[] SupportedRoles = ["Admin", "HR", "Manager", "Employee"];

    private readonly AppDbContext _context;
    private readonly ILogger<UserAccountsController> _logger;

    public UserAccountsController(AppDbContext context, ILogger<UserAccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, int? roleId, bool? isActive)
    {
        var normalizedKeyword = keyword?.Trim();
        var query = _context.TaiKhoans
            .AsNoTracking()
            .Include(account => account.NhanVien)!
                .ThenInclude(employee => employee!.PhongBan)
            .Include(account => account.NhanVien)!
                .ThenInclude(employee => employee!.ChucVu)
            .Include(account => account.VaiTro)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            var loweredKeyword = normalizedKeyword.ToLower();
            query = query.Where(account =>
                account.TenDangNhap.ToLower().Contains(loweredKeyword) ||
                (account.NhanVien != null &&
                 (account.NhanVien.Ho.ToLower().Contains(loweredKeyword) ||
                  account.NhanVien.Ten.ToLower().Contains(loweredKeyword) ||
                  account.NhanVien.Email.ToLower().Contains(loweredKeyword))));
        }

        if (roleId.HasValue)
        {
            query = query.Where(account => account.MaVaiTro == roleId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(account => account.TrangThai == isActive.Value);
        }

        var accounts = await query
            .OrderByDescending(account => account.TrangThai)
            .ThenBy(account => account.TenDangNhap)
            .Select(account => new AccountListItemViewModel
            {
                AccountId = account.MaTaiKhoan,
                EmployeeId = account.MaNhanVien,
                Username = account.TenDangNhap,
                EmployeeName = account.NhanVien == null
                    ? "Chưa liên kết nhân viên"
                    : (account.NhanVien.Ho + " " + account.NhanVien.Ten).Trim(),
                Email = account.NhanVien == null ? string.Empty : account.NhanVien.Email,
                Department = account.NhanVien == null || account.NhanVien.PhongBan == null
                    ? "—"
                    : account.NhanVien.PhongBan.TenPhongBan,
                Position = account.NhanVien == null || account.NhanVien.ChucVu == null
                    ? "—"
                    : account.NhanVien.ChucVu.TenChucVu,
                RoleName = account.VaiTro == null ? "Chưa cấp" : account.VaiTro.TenVaiTro,
                IsActive = account.TrangThai
            })
            .ToListAsync();

        var roleOptions = await GetRoleOptionsAsync(roleId);
        var model = new AccountIndexViewModel
        {
            Keyword = normalizedKeyword,
            RoleId = roleId,
            IsActive = isActive,
            Accounts = accounts,
            RoleOptions = roleOptions,
            TotalAccounts = await _context.TaiKhoans.CountAsync(),
            ActiveAccounts = await _context.TaiKhoans.CountAsync(account => account.TrangThai),
            LockedAccounts = await _context.TaiKhoans.CountAsync(account => !account.TrangThai),
            AdminAccounts = await _context.TaiKhoans.CountAsync(account => account.VaiTro != null && account.VaiTro.TenVaiTro == "Admin")
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateAccountViewModel();
        await LoadCreateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAccountViewModel model)
    {
        model.Username = NormalizeUsername(model.Username);

        if (await _context.TaiKhoans.AnyAsync(account => account.TenDangNhap.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã được sử dụng.");
        }

        if (!await _context.NhanViens.AnyAsync(employee => employee.MaNhanVien == model.EmployeeId))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên được chọn không tồn tại.");
        }
        else if (await _context.TaiKhoans.AnyAsync(account => account.MaNhanVien == model.EmployeeId))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đã có tài khoản.");
        }

        if (!await IsSupportedRoleAsync(model.RoleId))
        {
            ModelState.AddModelError(nameof(model.RoleId), "Vai trò được chọn không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync(model);
            return View(model);
        }

        var account = new TaiKhoan
        {
            MaNhanVien = model.EmployeeId,
            MaVaiTro = model.RoleId,
            TenDangNhap = model.Username,
            MatKhauBam = PasswordHasher.Hash(model.Password),
            TrangThai = model.IsActive
        };

        try
        {
            _context.TaiKhoans.Add(account);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo tài khoản '{account.TenDangNhap}' thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo tài khoản {Username}.", account.TenDangNhap);
            ModelState.AddModelError(string.Empty, "Không thể tạo tài khoản do dữ liệu bị trùng hoặc kết nối CSDL gặp lỗi.");
            await LoadCreateOptionsAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var account = await _context.TaiKhoans
            .AsNoTracking()
            .Include(item => item.NhanVien)
            .FirstOrDefaultAsync(item => item.MaTaiKhoan == id);

        if (account == null)
        {
            return NotFound();
        }

        var model = new EditAccountViewModel
        {
            AccountId = account.MaTaiKhoan,
            EmployeeId = account.MaNhanVien,
            EmployeeName = account.NhanVien == null
                ? "Chưa liên kết nhân viên"
                : (account.NhanVien.Ho + " " + account.NhanVien.Ten).Trim(),
            EmployeeEmail = account.NhanVien?.Email ?? string.Empty,
            Username = account.TenDangNhap,
            RoleId = account.MaVaiTro,
            IsActive = account.TrangThai,
            RoleOptions = await GetRoleOptionsAsync(account.MaVaiTro)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditAccountViewModel model)
    {
        if (id != model.AccountId)
        {
            return BadRequest();
        }

        var account = await _context.TaiKhoans
            .Include(item => item.NhanVien)
            .Include(item => item.VaiTro)
            .FirstOrDefaultAsync(item => item.MaTaiKhoan == id);

        if (account == null)
        {
            return NotFound();
        }

        model.Username = NormalizeUsername(model.Username);
        model.EmployeeId = account.MaNhanVien;
        model.EmployeeName = account.NhanVien == null
            ? "Chưa liên kết nhân viên"
            : (account.NhanVien.Ho + " " + account.NhanVien.Ten).Trim();
        model.EmployeeEmail = account.NhanVien?.Email ?? string.Empty;
        model.IsActive = account.TrangThai;

        if (await _context.TaiKhoans.AnyAsync(item =>
                item.MaTaiKhoan != id && item.TenDangNhap.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã được sử dụng.");
        }

        if (!await IsSupportedRoleAsync(model.RoleId))
        {
            ModelState.AddModelError(nameof(model.RoleId), "Vai trò được chọn không hợp lệ.");
        }

        var currentAccountId = GetCurrentAccountId();
        if (currentAccountId == account.MaTaiKhoan && model.RoleId != account.MaVaiTro)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Bạn không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
        }

        if (account.VaiTro?.TenVaiTro == "Admin" && model.RoleId != account.MaVaiTro &&
            await CountOtherActiveAdminsAsync(account.MaTaiKhoan) == 0)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Phải duy trì ít nhất một tài khoản Admin đang hoạt động.");
        }

        if (!ModelState.IsValid)
        {
            model.RoleOptions = await GetRoleOptionsAsync(model.RoleId);
            return View(model);
        }

        account.TenDangNhap = model.Username;
        account.MaVaiTro = model.RoleId;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            account.MatKhauBam = PasswordHasher.Hash(model.NewPassword);
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật tài khoản '{account.TenDangNhap}'.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật tài khoản {AccountId}.", account.MaTaiKhoan);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật tài khoản do dữ liệu bị trùng hoặc kết nối CSDL gặp lỗi.");
            model.RoleOptions = await GetRoleOptionsAsync(model.RoleId);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var account = await _context.TaiKhoans
            .Include(item => item.VaiTro)
            .FirstOrDefaultAsync(item => item.MaTaiKhoan == id);

        if (account == null)
        {
            return NotFound();
        }

        if (GetCurrentAccountId() == account.MaTaiKhoan)
        {
            TempData["Error"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        if (account.TrangThai && account.VaiTro?.TenVaiTro == "Admin" &&
            await CountOtherActiveAdminsAsync(account.MaTaiKhoan) == 0)
        {
            TempData["Error"] = "Không thể khóa Admin cuối cùng đang hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        account.TrangThai = !account.TrangThai;
        await _context.SaveChangesAsync();

        TempData["Success"] = account.TrangThai
            ? $"Đã mở khóa tài khoản '{account.TenDangNhap}'."
            : $"Đã khóa tài khoản '{account.TenDangNhap}'.";

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCreateOptionsAsync(CreateAccountViewModel model)
    {
        model.EmployeeOptions = await _context.NhanViens
            .AsNoTracking()
            .Where(employee => !_context.TaiKhoans.Any(account => account.MaNhanVien == employee.MaNhanVien))
            .OrderBy(employee => employee.Ten)
            .ThenBy(employee => employee.Ho)
            .Select(employee => new SelectListItem
            {
                Value = employee.MaNhanVien.ToString(),
                Text = (employee.Ho + " " + employee.Ten).Trim() + " · " + employee.Email,
                Selected = employee.MaNhanVien == model.EmployeeId
            })
            .ToListAsync();

        model.RoleOptions = await GetRoleOptionsAsync(model.RoleId == 0 ? null : model.RoleId);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetRoleOptionsAsync(int? selectedRoleId)
    {
        return await _context.VaiTros
            .AsNoTracking()
            .Where(role => SupportedRoles.Contains(role.TenVaiTro))
            .OrderBy(role => role.MaVaiTro)
            .Select(role => new SelectListItem
            {
                Value = role.MaVaiTro.ToString(),
                Text = role.TenVaiTro,
                Selected = selectedRoleId.HasValue && role.MaVaiTro == selectedRoleId.Value
            })
            .ToListAsync();
    }

    private Task<bool> IsSupportedRoleAsync(int roleId)
    {
        return _context.VaiTros.AnyAsync(role =>
            role.MaVaiTro == roleId && SupportedRoles.Contains(role.TenVaiTro));
    }

    private Task<int> CountOtherActiveAdminsAsync(int accountId)
    {
        return _context.TaiKhoans.CountAsync(account =>
            account.MaTaiKhoan != accountId &&
            account.TrangThai &&
            account.VaiTro != null &&
            account.VaiTro.TenVaiTro == "Admin");
    }

    private int? GetCurrentAccountId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out var accountId) ? accountId : null;
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
