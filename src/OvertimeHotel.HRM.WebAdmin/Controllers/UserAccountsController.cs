using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize]
public class UserAccountsController : Controller
{
    // Vai trò RBAC mà màn hình này cho phép cấp. "IT" là bộ phận Công nghệ
    // Thông tin — chủ công cấp phát tài khoản đăng nhập cho nhân viên.
    private static readonly string[] SupportedRoles = ["Admin", "HR", "Manager", "Employee", "IT"];
    private const int AccountPageSize = 7;
    private const int AuditPageSize = 5;

    private readonly AppDbContext _context;
    private readonly ILogger<UserAccountsController> _logger;
    private readonly ISupabaseUserProvisioningService _supabaseUserProvisioning;

    public UserAccountsController(
        AppDbContext context,
        ILogger<UserAccountsController> logger,
        ISupabaseUserProvisioningService supabaseUserProvisioning)
    {
        _context = context;
        _logger = logger;
        _supabaseUserProvisioning = supabaseUserProvisioning;
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> Index(string? keyword, int? roleId, bool? isActive, DateOnly? auditFrom, DateOnly? auditTo, int page = 1, int auditPage = 1)
    {
        var normalizedKeyword = keyword?.Trim();
        var auditDateValidationMessage = GetAuditDateValidationMessage(auditFrom, auditTo);
        try
        {
            var allAccounts = await this.SearchDataAsync("accounts:list", () => _context.TaiKhoans
                .AsNoTracking()
                .Include(account => account.NhanVien)!
                    .ThenInclude(employee => employee!.PhongBan)
                .Include(account => account.NhanVien)!
                    .ThenInclude(employee => employee!.ChucVu)
                .Include(account => account.VaiTro).ToListAsync());
            var query = allAccounts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                var loweredKeyword = normalizedKeyword.ToLower();
                query = query.Where(account =>
                    account.TenDangNhap.ToLower().Contains(loweredKeyword) ||
                    (account.NhanVien != null &&
                     (account.NhanVien.Ho.ToLower().Contains(loweredKeyword) ||
                      account.NhanVien.Ten.ToLower().Contains(loweredKeyword) ||
                      account.NhanVien.EmailCongVu.ToLower().Contains(loweredKeyword))));
            }

            if (roleId.HasValue)
            {
                query = query.Where(account => account.MaVaiTro == roleId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(account => account.TrangThai == isActive.Value);
            }

            var totalFilteredAccounts = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredAccounts / (double)AccountPageSize));
            var pageNumber = Math.Clamp(page, 1, totalPages);
            var accounts = query
                .OrderByDescending(account => account.TrangThai)
                .ThenBy(account => account.TenDangNhap)
                .Skip((pageNumber - 1) * AccountPageSize)
                .Take(AccountPageSize)
                .Select(account => new AccountListItemViewModel
                {
                    AccountId = account.MaTaiKhoan,
                    EmployeeId = account.MaNhanVien,
                    Username = account.TenDangNhap,
                    EmployeeName = account.NhanVien == null
                        ? "Chưa liên kết nhân viên"
                        : (account.NhanVien.Ho + " " + account.NhanVien.Ten).Trim(),
                    Email = account.NhanVien == null ? string.Empty : account.NhanVien.EmailCongVu,
                    Department = account.NhanVien == null || account.NhanVien.PhongBan == null
                        ? "—"
                        : account.NhanVien.PhongBan.TenPhongBan,
                    Position = account.NhanVien == null || account.NhanVien.ChucVu == null
                        ? "—"
                        : account.NhanVien.ChucVu.TenChucVu,
                    RoleName = account.VaiTro == null ? "Chưa cấp" : account.VaiTro.TenVaiTro,
                    IsActive = account.TrangThai
                })
                .ToList();

            var roleOptions = await GetRoleOptionsAsync(roleId);
            var auditLogPage = new AuditLogPageResult([], 0, 1, 1);
            var auditLogAvailable = true;
            try
            {
                if (auditDateValidationMessage == null)
                {
                    auditLogPage = await GetRecentAuditLogsAsync(auditFrom, auditTo, auditPage);
                }
            }
            catch (Exception ex)
            {
                auditLogAvailable = false;
                _logger.LogWarning(ex, "Không thể tải nhật ký quản trị tài khoản.");
            }

            var model = new AccountIndexViewModel
            {
                Keyword = normalizedKeyword,
                RoleId = roleId,
                IsActive = isActive,
                AuditFrom = auditFrom,
                AuditTo = auditTo,
                AuditDateValidationMessage = auditDateValidationMessage,
                Accounts = accounts,
                RoleOptions = roleOptions,
                TotalAccounts = allAccounts.Count,
                ActiveAccounts = allAccounts.Count(account => account.TrangThai),
                LockedAccounts = allAccounts.Count(account => !account.TrangThai),
                AdminAccounts = allAccounts.Count(account => account.VaiTro != null && account.VaiTro.TenVaiTro == "Admin"),
                TotalFilteredAccounts = totalFilteredAccounts,
                PageNumber = pageNumber,
                PageSize = AccountPageSize,
                TotalPages = totalPages,
                AuditLogs = auditLogPage.Logs,
                TotalAuditLogs = auditLogPage.TotalLogs,
                AuditPageNumber = auditLogPage.PageNumber,
                AuditPageSize = AuditPageSize,
                TotalAuditPages = auditLogPage.TotalPages,
                AuditLogAvailable = auditLogAvailable
            };

            if (IsAjaxRequest())
            {
                Response.Headers["X-Total-Accounts"] = model.TotalAccounts.ToString();
                Response.Headers["X-Active-Accounts"] = model.ActiveAccounts.ToString();
                Response.Headers["X-Locked-Accounts"] = model.LockedAccounts.ToString();
                Response.Headers["X-Admin-Accounts"] = model.AdminAccounts.ToString();
                Response.Headers["X-Result-Count"] = model.TotalFilteredAccounts.ToString();
                return PartialView("_AccountTable", model);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể kết nối CSDL khi tải danh sách tài khoản. Chuyển sang hiển thị danh sách tài khoản mẫu.");
            var fallbackAccounts = GetDevSeedAccountList();
            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                var kw = normalizedKeyword.ToLower();
                fallbackAccounts = fallbackAccounts.Where(a =>
                    a.Username.ToLower().Contains(kw) ||
                    a.EmployeeName.ToLower().Contains(kw) ||
                    a.Email.ToLower().Contains(kw)).ToList();
            }
            if (roleId.HasValue)
            {
                var roleMap = new Dictionary<int, string>
                {
                    { 1, "Admin" }, { 2, "HR" }, { 3, "Manager" }, { 4, "Employee" }, { 5, "IT" }
                };
                if (roleMap.TryGetValue(roleId.Value, out var targetRole))
                {
                    fallbackAccounts = fallbackAccounts.Where(a => a.RoleName == targetRole).ToList();
                }
            }
            if (isActive.HasValue)
            {
                fallbackAccounts = fallbackAccounts.Where(a => a.IsActive == isActive.Value).ToList();
            }

            var fallbackTotalAccounts = fallbackAccounts.Count;
            var fallbackTotalPages = Math.Max(1, (int)Math.Ceiling(fallbackTotalAccounts / (double)AccountPageSize));
            var fallbackPageNumber = Math.Clamp(page, 1, fallbackTotalPages);
            var fallbackPageAccounts = fallbackAccounts
                .Skip((fallbackPageNumber - 1) * AccountPageSize)
                .Take(AccountPageSize)
                .ToList();

            var fallbackModel = new AccountIndexViewModel
            {
                Keyword = normalizedKeyword,
                RoleId = roleId,
                IsActive = isActive,
                AuditFrom = auditFrom,
                AuditTo = auditTo,
                AuditDateValidationMessage = auditDateValidationMessage,
                Accounts = fallbackPageAccounts,
                RoleOptions = GetDefaultRoleOptions(roleId),
                TotalAccounts = 4,
                ActiveAccounts = 4,
                LockedAccounts = 0,
                AdminAccounts = 1,
                TotalFilteredAccounts = fallbackTotalAccounts,
                PageNumber = fallbackPageNumber,
                PageSize = AccountPageSize,
                TotalPages = fallbackTotalPages,
                AuditLogAvailable = false
            };

            if (IsAjaxRequest())
            {
                Response.Headers["X-Total-Accounts"] = fallbackModel.TotalAccounts.ToString();
                Response.Headers["X-Active-Accounts"] = fallbackModel.ActiveAccounts.ToString();
                Response.Headers["X-Locked-Accounts"] = fallbackModel.LockedAccounts.ToString();
                Response.Headers["X-Admin-Accounts"] = fallbackModel.AdminAccounts.ToString();
                Response.Headers["X-Result-Count"] = fallbackModel.TotalFilteredAccounts.ToString();
                return PartialView("_AccountTable", fallbackModel);
            }

            return View(fallbackModel);
        }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public IActionResult Create()
    {
        return RedirectToAction("Create", "YeuCauTaiKhoan");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public Task<IActionResult> Create(CreateAccountViewModel model)
    {
        return Task.FromResult<IActionResult>(RedirectToAction("Create", "YeuCauTaiKhoan"));
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManagePermissions)]
    public async Task<IActionResult> RolePermissions(int? roleId, bool popup = false)
    {
        var roleOptions = await GetConfigurableRoleOptionsAsync(roleId);
        if (roleOptions.Count == 0) return NotFound();
        var selectedRoleId = roleId ?? int.Parse(roleOptions[0].Value!);
        var role = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(item => item.MaVaiTro == selectedRoleId);
        if (role == null || role.TenVaiTro == "IT" || !SupportedRoles.Contains(role.TenVaiTro)) return NotFound();

        var model = new RolePermissionsViewModel
        {
            RoleId = role.MaVaiTro,
            RoleName = GetVietnameseRoleName(role.TenVaiTro),
            IsManagerRole = role.TenVaiTro == "Manager",
            AffectedAccountCount = await _context.TaiKhoans.CountAsync(account => account.MaVaiTro == role.MaVaiTro),
            RoleOptions = roleOptions,
            PermissionOptions = await GetRolePermissionOptionsAsync(role.MaVaiTro)
        };
        ViewData["RolePopup"] = popup;
        return popup ? PartialView("_RolePermissionsForm", model) : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManagePermissions)]
    public async Task<IActionResult> SaveRolePermissions(RolePermissionsViewModel model, bool popup = false)
    {
        var role = await _context.VaiTros.FirstOrDefaultAsync(item => item.MaVaiTro == model.RoleId);
        if (role == null || role.TenVaiTro == "IT" || !SupportedRoles.Contains(role.TenVaiTro)) return NotFound();
        var editableCodes = PermissionConfiguration.VisibleCodes;
        var permissions = await _context.Quyens.AsNoTracking()
            .Where(item => editableCodes.Contains(item.MaQuyenCode)).ToListAsync();
        var allowed = permissions.ToDictionary(item => item.MaQuyen, item => item.TenQuyen);
        var allowedIds = allowed.Keys.ToHashSet();
        var requested = model.PermissionIds.Distinct().ToHashSet();
        if (!requested.IsSubsetOf(allowedIds) || string.IsNullOrWhiteSpace(model.ChangeReason) || model.ChangeReason.Length > 500)
            return BadRequest("Dữ liệu quyền không hợp lệ.");
        var existing = await _context.VaiTroQuyens.Where(item => item.MaVaiTro == role.MaVaiTro && allowedIds.Contains(item.MaQuyen)).ToListAsync();
        var existingIds = existing.Select(item => item.MaQuyen).ToHashSet();
        var selectedCodes = permissions.Where(p => requested.Contains(p.MaQuyen)).Select(p => p.MaQuyenCode).ToHashSet();
        var existingCodes = permissions.Where(p => existingIds.Contains(p.MaQuyen)).Select(p => p.MaQuyenCode).ToHashSet();
        foreach (var (action, view) in PermissionConfiguration.RequiresView)
            if (selectedCodes.Contains(action) && !selectedCodes.Contains(view) && existingCodes.Contains(action))
                return BadRequest("Cấu hình cũ có quyền thao tác nhưng thiếu quyền xem. Hãy chọn quyền xem hoặc bỏ quyền thao tác trước khi lưu.");
        PermissionConfiguration.AddRequiredPermissions(selectedCodes, role.TenVaiTro);
        if (selectedCodes.Any(code => !permissions.Any(p => p.MaQuyenCode == code)))
            return BadRequest("Danh mục quyền chưa đầy đủ. Cần cập nhật cơ sở dữ liệu trước khi lưu.");
        requested = permissions.Where(p => selectedCodes.Contains(p.MaQuyenCode)).Select(p => p.MaQuyen).ToHashSet();
        var reportViewId = permissions.FirstOrDefault(p => p.MaQuyenCode == PermissionCodes.ReportView)?.MaQuyen;
        var reportExportId = permissions.FirstOrDefault(p => p.MaQuyenCode == PermissionCodes.ReportExport)?.MaQuyen;
        if (reportViewId is int viewId && reportExportId is int exportId)
        {
            var reportChanged = model.ReportPermissionChanged || requested.Contains(viewId) != existingIds.Contains(viewId);
            if (reportChanged && requested.Contains(viewId)) requested.Add(exportId);
            else if (!reportChanged && existingIds.Contains(exportId)) requested.Add(exportId);
        }
        var add = requested.Except(existingIds).ToArray();
        var remove = existingIds.Except(requested).ToArray();
        if (add.Length == 0 && remove.Length == 0)
        {
            TempData["Success"] = $"Quyền vai trò {GetVietnameseRoleName(role.TenVaiTro)} không thay đổi.";
            return RedirectToAction(nameof(RolePermissions), new { roleId = role.MaVaiTro, popup });
        }
        _context.VaiTroQuyens.RemoveRange(existing.Where(item => remove.Contains(item.MaQuyen)));
        _context.VaiTroQuyens.AddRange(add.Select(permissionId => new VaiTroQuyen { MaVaiTro = role.MaVaiTro, MaQuyen = permissionId }));
        var currentAccountId = GetCurrentAccountId();
        if (currentAccountId is int accountId)
        {
            var currentAccount = await _context.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(item => item.MaTaiKhoan == accountId);
            if (currentAccount != null)
            {
                _context.NhatKyQuanTris.Add(CreateAuditLog(
                    currentAccount.MaTaiKhoan,
                    currentAccount.TenDangNhap,
                    "UPDATE_ROLE_PERMISSION",
                    $"Cập nhật quyền cho toàn bộ vai trò {GetVietnameseRoleName(role.TenVaiTro)}.",
                    remove.Length == 0 ? null : $"Thu hồi: {string.Join(", ", remove.Select(id => allowed[id]))}",
                    add.Length == 0 ? null : $"Cấp thêm: {string.Join(", ", add.Select(id => allowed[id]))}",
                    model.ChangeReason));
            }
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã cập nhật quyền vai trò {GetVietnameseRoleName(role.TenVaiTro)} cho toàn bộ tài khoản thuộc vai trò này. Họ cần đăng xuất và đăng nhập lại.";
        return RedirectToAction(nameof(RolePermissions), new { roleId = role.MaVaiTro, popup });
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
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
            EmployeeEmail = account.NhanVien?.EmailCongVu ?? string.Empty,
            Username = account.TenDangNhap,
            RoleId = account.MaVaiTro,
            IsActive = account.TrangThai,
            RoleOptions = await GetRoleOptionsAsync(account.MaVaiTro),
            PermissionOptions = await GetRolePermissionOptionsAsync(account.MaVaiTro)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
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
        model.EmployeeEmail = account.NhanVien?.EmailCongVu ?? string.Empty;
        model.IsActive = account.TrangThai;

        var requestedRole = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(role => role.MaVaiTro == model.RoleId);
        if (requestedRole == null || !SupportedRoles.Contains(requestedRole.TenVaiTro) ||
            (model.RoleId != account.MaVaiTro && (requestedRole.TenVaiTro == "IT" || account.VaiTro?.TenVaiTro == "IT")))
            ModelState.AddModelError(nameof(model.RoleId), "Không thể thay đổi vai trò IT.");

        var oldUsername = account.TenDangNhap;

        if (await _context.TaiKhoans.AnyAsync(item =>
                item.MaTaiKhoan != id && item.TenDangNhap.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã được sử dụng.");
        }

        if (string.IsNullOrWhiteSpace(model.NewPassword))
        {
            ModelState.Remove(nameof(model.NewPassword));
            ModelState.Remove(nameof(model.ConfirmNewPassword));
        }

        var usernameChanged = !string.Equals(oldUsername, model.Username, StringComparison.Ordinal);
        var passwordChanged = !string.IsNullOrWhiteSpace(model.NewPassword);
        var roleChanged = model.RoleId != account.MaVaiTro;
        var hasChanges = usernameChanged || passwordChanged || roleChanged;

        if (hasChanges && string.IsNullOrWhiteSpace(model.ChangeReason))
        {
            ModelState.AddModelError(nameof(model.ChangeReason), "Vui lòng nhập lý do thay đổi để lưu nhật ký quản trị.");
        }

        if (!ModelState.IsValid)
        {
            model.RoleOptions = await GetRoleOptionsAsync(model.RoleId);
            return View(model);
        }

        if (!hasChanges)
        {
            TempData["Success"] = "Không có thông tin nào thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        var oldValues = new List<string>();
        var newValues = new List<string>();
        var changedFields = new List<string>();

        if (roleChanged)
        {
            oldValues.Add($"Vai trò: {account.VaiTro?.TenVaiTro}");
            newValues.Add($"Vai trò: {requestedRole!.TenVaiTro}");
            changedFields.Add("vai trò");
            account.MaVaiTro = model.RoleId;
        }

        if (usernameChanged)
        {
            oldValues.Add($"Username: {oldUsername}");
            newValues.Add($"Username: {model.Username}");
            changedFields.Add("tên đăng nhập");
        }

        if (passwordChanged)
        {
            oldValues.Add("Mật khẩu: giá trị cũ được bảo mật");
            newValues.Add("Mật khẩu: đã được đặt lại");
            changedFields.Add("mật khẩu");
        }

        account.TenDangNhap = model.Username;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            account.MatKhauBam = PasswordHasher.Hash(model.NewPassword);
        }

        try
        {
            _context.NhatKyQuanTris.Add(CreateAuditLog(
                account.MaTaiKhoan,
                account.TenDangNhap,
                passwordChanged && !usernameChanged ? "RESET_PASSWORD" : "UPDATE_ACCOUNT",
                $"Cập nhật {string.Join(", ", changedFields)} của tài khoản.",
                string.Join("; ", oldValues),
                string.Join("; ", newValues),
                model.ChangeReason));

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
    [Authorize(Policy = PermissionCodes.ManagePermissions)]
    public async Task<IActionResult> UpdateRolePermissions(AddRolePermissionsViewModel model)
    {
        TempData["Error"] = "Vui lòng sử dụng trang phân quyền vai trò để cập nhật quyền.";
        return RedirectToAction(nameof(RolePermissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> DeleteAccount(int id, string? reason)
    {
        reason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return ToggleStatusFailure("Vui lòng nhập lý do xóa tài khoản, tối đa 500 ký tự.");

        var account = await _context.TaiKhoans.Include(a => a.VaiTro)
            .Include(a => a.NhanVien)!.ThenInclude(n => n!.ChucVu)
            .Include(a => a.NhanVien)!.ThenInclude(n => n!.PhongBan)
            .FirstOrDefaultAsync(a => a.MaTaiKhoan == id);
        if (account == null) return ToggleStatusFailure("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        var denied = AccountDeletionPolicy.GetDenial(User.FindFirstValue(ClaimTypes.Role), GetCurrentAccountId(), id,
            account.VaiTro?.TenVaiTro, account.NhanVien?.PhongBan?.TenPhongBan, account.NhanVien?.ChucVu?.TenChucVu);
        if (denied != null) return ToggleStatusFailure(denied, StatusCodes.Status403Forbidden);

        var email = account.NhanVien?.EmailCongVu?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return ToggleStatusFailure("Nhân viên chưa có email công vụ; cần đối soát tài khoản đăng nhập trước khi xóa.");
        if (await _context.NhanViens.AnyAsync(n => n.MaNhanVien != account.MaNhanVien && n.EmailCongVu.ToLower() == email.ToLower()))
            return ToggleStatusFailure("Email công vụ đang được dùng bởi nhân viên khác; cần đối soát trước khi xóa.");

        // The internal account does not store the Auth UUID; resolve the exact email first.
        var authIds = new List<Guid>();
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = 'public.tai_khoan'::regclass AND tgname = 'hrm_archive_deleted_account_requests' AND tgenabled <> 'D')";
            if (await command.ExecuteScalarAsync() is not true)
                return ToggleStatusFailure("Cơ sở dữ liệu chưa được cập nhật để lưu lịch sử yêu cầu; chưa xóa tài khoản.");
            command.CommandText = "SELECT id FROM auth.users WHERE lower(email) = lower(@email) LIMIT 2";
            var parameter = command.CreateParameter(); parameter.ParameterName = "email"; parameter.Value = email; command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) authIds.Add(reader.GetGuid(0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể đối soát Supabase Auth cho tài khoản {AccountId}", id);
            return ToggleStatusFailure("Không thể đối soát tài khoản đăng nhập; chưa xóa tài khoản. Vui lòng thử lại.");
        }
        if (authIds.Count > 1) return ToggleStatusFailure("Có nhiều tài khoản đăng nhập trùng email; cần đối soát trước khi xóa.");
        if (authIds.Count == 1 && !_supabaseUserProvisioning.IsConfigured)
            return ToggleStatusFailure("Chưa cấu hình dịch vụ xác thực để xóa tài khoản; vui lòng liên hệ quản trị hệ thống.");

        // Persist the lock first, so a partial failure leaves the account unable to sign in.
        if (account.TrangThai)
        {
            account.TrangThai = false;
            _context.NhatKyQuanTris.Add(CreateAuditLog(id, account.TenDangNhap, "LOCK_ACCOUNT", "Khóa tài khoản để xóa.", "Hoạt động", "Đã khóa", reason));
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Không thể khóa tài khoản {AccountId}", id); return ToggleStatusFailure("Không thể khóa tài khoản; chưa thực hiện xóa."); }
        }
        if (authIds.Count == 1 && !await _supabaseUserProvisioning.DeleteUserAsync(authIds[0].ToString(), CancellationToken.None))
            return ToggleStatusFailure("Không thể xóa tài khoản đăng nhập trên Supabase. Tài khoản ứng dụng đã được khóa; vui lòng thử lại.");

        var username = account.TenDangNhap;
        var audit = CreateAuditLog(id, username, "DELETE_ACCOUNT", "Xóa tài khoản đăng nhập; giữ hồ sơ nhân viên.", $"Vai trò: {account.VaiTro?.TenVaiTro}; nhân viên: #{account.MaNhanVien}", "Đã xóa", reason);
        audit.MaTaiKhoanBiTacDong = null;
        _context.NhatKyQuanTris.Add(audit);
        _context.TaiKhoans.Remove(account);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể hoàn tất xóa tài khoản {AccountId}", id);
            return ToggleStatusFailure("Chưa hoàn tất xóa tài khoản ứng dụng. Tài khoản đã được khóa; vui lòng thử lại.");
        }
        var message = $"Đã xóa tài khoản '{username}'. Hồ sơ nhân viên được giữ lại.";
        if (IsAjaxRequest()) return Json(new { success = true, message });
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> ToggleStatus(int id, string? reason)
    {
        reason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ToggleStatusFailure("Vui lòng nhập lý do khóa hoặc mở khóa tài khoản.");
        }

        if (reason.Length > 500)
        {
            return ToggleStatusFailure("Lý do khóa hoặc mở khóa không được vượt quá 500 ký tự.");
        }

        var account = await _context.TaiKhoans
            .Include(item => item.VaiTro)
            .FirstOrDefaultAsync(item => item.MaTaiKhoan == id);

        if (account == null)
        {
            return IsAjaxRequest()
                ? NotFound(new { success = false, message = "Không tìm thấy tài khoản cần cập nhật." })
                : NotFound();
        }

        if (GetCurrentAccountId() == account.MaTaiKhoan)
        {
            return ToggleStatusFailure("Bạn không thể tự khóa tài khoản đang đăng nhập.");
        }

        if (account.TrangThai && account.VaiTro?.TenVaiTro == "Admin" &&
            await CountOtherActiveAdminsAsync(account.MaTaiKhoan) == 0)
        {
            return ToggleStatusFailure("Không thể khóa Admin cuối cùng đang hoạt động.");
        }

        var wasActive = account.TrangThai;
        account.TrangThai = !account.TrangThai;

        var auditLog = CreateAuditLog(
            account.MaTaiKhoan,
            account.TenDangNhap,
            account.TrangThai ? "UNLOCK_ACCOUNT" : "LOCK_ACCOUNT",
            account.TrangThai ? "Mở khóa tài khoản." : "Khóa tài khoản.",
            $"Trạng thái: {(wasActive ? "Hoạt động" : "Đã khóa")}",
            $"Trạng thái: {(account.TrangThai ? "Hoạt động" : "Đã khóa")}",
            reason);
        _context.NhatKyQuanTris.Add(auditLog);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể thay đổi trạng thái tài khoản {AccountId}.", account.MaTaiKhoan);
            return ToggleStatusFailure(
                "Không thể thay đổi trạng thái tài khoản. Vui lòng kiểm tra kết nối CSDL và thử lại.",
                StatusCodes.Status500InternalServerError);
        }

        var successMessage = account.TrangThai
            ? $"Đã mở khóa tài khoản '{account.TenDangNhap}'."
            : $"Đã khóa tài khoản '{account.TenDangNhap}'.";

        if (IsAjaxRequest())
        {
            var activeAccounts = await _context.TaiKhoans.CountAsync(item => item.TrangThai);
            var lockedAccounts = await _context.TaiKhoans.CountAsync(item => !item.TrangThai);
            var vietnamTime = auditLog.ThoiGian.ToOffset(TimeSpan.FromHours(7));

            return Json(new
            {
                success = true,
                message = successMessage,
                accountId = account.MaTaiKhoan,
                isActive = account.TrangThai,
                activeAccounts,
                lockedAccounts,
                auditLog = new
                {
                    actionCode = auditLog.HanhDong,
                    actionName = GetAuditActionName(auditLog.HanhDong),
                    description = auditLog.NoiDung,
                    actorName = auditLog.TenNguoiThucHien,
                    targetUsername = auditLog.TenTaiKhoanBiTacDong,
                    oldValue = auditLog.GiaTriCu,
                    newValue = auditLog.GiaTriMoi,
                    reason = auditLog.LyDo,
                    ipAddress = auditLog.DiaChiIp,
                    occurredAt = auditLog.ThoiGian,
                    displayTime = vietnamTime.ToString("dd/MM/yyyy · HH:mm")
                }
            });
        }

        TempData["Success"] = successMessage;

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> AuditLogs(DateOnly? auditFrom, DateOnly? auditTo, int auditPage = 1)
    {
        var validationMessage = GetAuditDateValidationMessage(auditFrom, auditTo);
        if (validationMessage != null)
        {
            return BadRequest(new { message = validationMessage });
        }

        try
        {
            var auditLogPage = await GetRecentAuditLogsAsync(auditFrom, auditTo, auditPage);
            Response.Headers["X-Audit-Count"] = auditLogPage.TotalLogs.ToString();

            return PartialView("_AuditLogList", new AccountIndexViewModel
            {
                AuditFrom = auditFrom,
                AuditTo = auditTo,
                AuditLogs = auditLogPage.Logs,
                TotalAuditLogs = auditLogPage.TotalLogs,
                AuditPageNumber = auditLogPage.PageNumber,
                AuditPageSize = AuditPageSize,
                TotalAuditPages = auditLogPage.TotalPages,
                AuditLogAvailable = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lọc nhật ký quản trị tài khoản.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tải nhật ký quản trị. Vui lòng kiểm tra kết nối CSDL và thử lại."
            });
        }
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
                Text = (employee.Ho + " " + employee.Ten).Trim() + " · " + employee.EmailCongVu,
                Selected = employee.MaNhanVien == model.EmployeeId
            })
            .ToListAsync();

        model.RoleOptions = await GetRoleOptionsAsync(model.RoleId == 0 ? null : model.RoleId);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetRoleOptionsAsync(int? selectedRoleId)
    {
        var roles = await this.SearchDataAsync("accounts:roles", () => _context.VaiTros.AsNoTracking()
            .Where(role => SupportedRoles.Contains(role.TenVaiTro)).OrderBy(role => role.MaVaiTro).ToListAsync());
        return roles.Select(role => new SelectListItem
        {
            Value = role.MaVaiTro.ToString(),
            Text = GetVietnameseRoleName(role.TenVaiTro),
            Selected = selectedRoleId == role.MaVaiTro
        }).ToList();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetConfigurableRoleOptionsAsync(int? selectedRoleId)
    {
        var roles = await _context.VaiTros
            .AsNoTracking()
            .Where(role => SupportedRoles.Contains(role.TenVaiTro) && role.TenVaiTro != "IT")
            .OrderBy(role => role.MaVaiTro)
            .Select(role => new SelectListItem
            {
                Value = role.MaVaiTro.ToString(),
                Text = role.TenVaiTro,
                Selected = selectedRoleId.HasValue && role.MaVaiTro == selectedRoleId.Value
            })
            .ToListAsync();

        foreach (var role in roles)
        {
            role.Text = GetVietnameseRoleName(role.Text);
        }

        return roles;
    }

    private async Task<IReadOnlyList<RolePermissionOptionViewModel>> GetRolePermissionOptionsAsync(int roleId)
    {
        var grantedIds = await _context.VaiTroQuyens
            .AsNoTracking()
            .Where(item => item.MaVaiTro == roleId)
            .Select(item => item.MaQuyen)
            .ToHashSetAsync();
        var permissions = await _context.Quyens
            .AsNoTracking()
            .Where(permission => PermissionConfiguration.VisibleCodes.Contains(permission.MaQuyenCode))
            .OrderBy(permission => permission.TenQuyen)
            .ToListAsync();

        var isIt = await _context.VaiTros.AsNoTracking().AnyAsync(r => r.MaVaiTro == roleId && r.TenVaiTro == "IT");

        return permissions.Where(permission => permission.MaQuyenCode != PermissionCodes.ReportExport).Select(permission => new RolePermissionOptionViewModel
        {
            PermissionId = permission.MaQuyen,
            Code = permission.MaQuyenCode,
            Name = permission.TenQuyen,
            Description = permission.MoTa,
            Granted = grantedIds.Contains(permission.MaQuyen) || (isIt && PermissionConfiguration.FixedItCodes.Contains(permission.MaQuyenCode)),
            PartiallyGranted = permission.MaQuyenCode == PermissionCodes.ReportView && grantedIds.Contains(permission.MaQuyen)
                && !permissions.Any(p => p.MaQuyenCode == PermissionCodes.ReportExport && grantedIds.Contains(p.MaQuyen))
        }).ToList();
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

    private async Task<AuditLogPageResult> GetRecentAuditLogsAsync(DateOnly? auditFrom, DateOnly? auditTo, int page)
    {
        var query = _context.NhatKyQuanTris.AsNoTracking().AsQueryable();
        var vietnamOffset = TimeSpan.FromHours(7);

        if (auditFrom.HasValue)
        {
            var startOfDay = new DateTimeOffset(auditFrom.Value.ToDateTime(TimeOnly.MinValue), vietnamOffset).ToUniversalTime();
            query = query.Where(log => log.ThoiGian >= startOfDay);
        }

        if (auditTo.HasValue)
        {
            var startOfNextDay = new DateTimeOffset(auditTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), vietnamOffset).ToUniversalTime();
            query = query.Where(log => log.ThoiGian < startOfNextDay);
        }

        var auditRows = await this.SearchDataAsync($"accounts:audit:{auditFrom:yyyyMMdd}:{auditTo:yyyyMMdd}", () => query.ToListAsync());
        query = auditRows.AsQueryable();

        var totalLogs = query.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalLogs / (double)AuditPageSize));
        var pageNumber = Math.Clamp(page, 1, totalPages);
        var logs = query
            .OrderByDescending(log => log.ThoiGian)
            .Skip((pageNumber - 1) * AuditPageSize)
            .Take(AuditPageSize)
            .ToList();

        var items = logs.Select(log => new AdminAuditLogViewModel
        {
            AuditLogId = log.MaNhatKy,
            ActorName = log.TenNguoiThucHien,
            TargetUsername = log.TenTaiKhoanBiTacDong,
            ActionCode = log.HanhDong,
            ActionName = GetAuditActionName(log.HanhDong),
            Description = log.NoiDung,
            OldValue = log.GiaTriCu,
            NewValue = log.GiaTriMoi,
            Reason = log.LyDo,
            IpAddress = log.DiaChiIp,
            OccurredAt = log.ThoiGian
        }).ToList();

        return new AuditLogPageResult(items, totalLogs, pageNumber, totalPages);
    }

    private sealed record AuditLogPageResult(
        IReadOnlyList<AdminAuditLogViewModel> Logs,
        int TotalLogs,
        int PageNumber,
        int TotalPages);

    private static string? GetAuditDateValidationMessage(DateOnly? auditFrom, DateOnly? auditTo)
    {
        return auditFrom.HasValue && auditTo.HasValue && auditFrom > auditTo
            ? "Ngày bắt đầu không được sau ngày kết thúc."
            : null;
    }

    private NhatKyQuanTri CreateAuditLog(
        int targetAccountId,
        string targetUsername,
        string action,
        string description,
        string? oldValue,
        string? newValue,
        string? reason)
    {
        return new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = GetCurrentAccountId(),
            MaTaiKhoanBiTacDong = targetAccountId,
            TenNguoiThucHien = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Không xác định",
            TenTaiKhoanBiTacDong = targetUsername,
            HanhDong = action,
            NoiDung = description,
            GiaTriCu = string.IsNullOrWhiteSpace(oldValue) ? null : oldValue,
            GiaTriMoi = string.IsNullOrWhiteSpace(newValue) ? null : newValue,
            LyDo = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ThoiGian = DateTimeOffset.UtcNow
        };
    }

    private static string GetAuditActionName(string action)
    {
        return action switch
        {
            "CREATE_ACCOUNT" => "Tạo tài khoản",
            "CREATE_ACCOUNT_AUTH" => "Cấp tài khoản đăng nhập Mobile",
            "UPDATE_ACCOUNT" => "Cập nhật tài khoản",
            "RESET_PASSWORD" => "Đặt lại mật khẩu",
            "DELETE_ACCOUNT" => "Xóa tài khoản",
            "LOCK_ACCOUNT" => "Khóa tài khoản",
            "UNLOCK_ACCOUNT" => "Mở khóa tài khoản",
            _ => "Thao tác quản trị"
        };
    }

    private static string GetVietnameseRoleName(string? roleName)
    {
        return roleName switch
        {
            "Admin" => "Quản trị viên",
            "HR" => "Nhân sự",
            "Manager" => "Quản lý",
            "Employee" => "Nhân viên",
            "IT" => "Công nghệ thông tin",
            _ => roleName ?? "Chưa cấp"
        };
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
               Request.GetTypedHeaders().Accept?.Any(value =>
                   string.Equals(value.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private IActionResult ToggleStatusFailure(string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        if (IsAjaxRequest())
        {
            return StatusCode(statusCode, new { success = false, message });
        }

        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
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

    private static List<AccountListItemViewModel> GetDevSeedAccountList()
    {
        return
        [
            new AccountListItemViewModel
            {
                AccountId = 1,
                EmployeeId = 1,
                Username = "admin",
                EmployeeName = "Nguyễn Đình Cường",
                Email = "cuong.nguyen@overtimehotel.com",
                Department = "Ban Giám Đốc",
                Position = "Tổng Giám Đốc",
                RoleName = "Admin",
                IsActive = true
            },
            new AccountListItemViewModel
            {
                AccountId = 2,
                EmployeeId = 2,
                Username = "hr_sang",
                EmployeeName = "Võ Huỳnh Minh Sang",
                Email = "sang.vo@overtimehotel.com",
                Department = "Phòng Nhân Sự",
                Position = "Trưởng Phòng Nhân Sự",
                RoleName = "HR",
                IsActive = true
            },
            new AccountListItemViewModel
            {
                AccountId = 3,
                EmployeeId = 3,
                Username = "mgr_long",
                EmployeeName = "Nguyễn Hoàng Long",
                Email = "long.nguyen@overtimehotel.com",
                Department = "Bộ Phận Tiền Sảnh",
                Position = "Trưởng Bộ Phận Tiền Sảnh",
                RoleName = "Manager",
                IsActive = true
            },
            new AccountListItemViewModel
            {
                AccountId = 4,
                EmployeeId = 4,
                Username = "emp_bao",
                EmployeeName = "Châu Quốc Bảo",
                Email = "bao.chau@overtimehotel.com",
                Department = "Bộ Phận Tiền Sảnh",
                Position = "Trưởng Bộ Phận",
                RoleName = "Manager",
                IsActive = true
            },
            new AccountListItemViewModel
            {
                AccountId = 12,
                EmployeeId = 20,
                Username = "emp_linh",
                EmployeeName = "Dương Thùy Linh",
                Email = "linh.duong@overtimehotel.com",
                Department = "Bộ Phận Tiền Sảnh",
                Position = "Nhân Viên Lễ Tân",
                RoleName = "Employee",
                IsActive = true
            }
        ];
    }

    private static List<SelectListItem> GetDefaultRoleOptions(int? selectedRoleId)
    {
        return
        [
            new SelectListItem { Value = "1", Text = "Quản trị viên", Selected = selectedRoleId == 1 },
            new SelectListItem { Value = "2", Text = "Nhân sự", Selected = selectedRoleId == 2 },
            new SelectListItem { Value = "3", Text = "Quản lý", Selected = selectedRoleId == 3 },
            new SelectListItem { Value = "4", Text = "Nhân viên", Selected = selectedRoleId == 4 },
            new SelectListItem { Value = "5", Text = "Công nghệ thông tin", Selected = selectedRoleId == 5 }
        ];
    }
}
