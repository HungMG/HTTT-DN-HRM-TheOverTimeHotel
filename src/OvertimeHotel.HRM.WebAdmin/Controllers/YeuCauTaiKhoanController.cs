using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Quy trình "Nhân sự duyệt hợp đồng lao động → bộ phận CNTT cấp tài khoản đăng nhập".
///
/// HR và Admin gửi yêu cầu tài khoản nhân viên từ hồ sơ; IT xử lý hàng đợi và trực tiếp cấp tài khoản.
/// </summary>
[Authorize]
public class YeuCauTaiKhoanController : Controller
{
    /// <summary>
    /// Vai trò được phép gán khi gửi yêu cầu. Khớp với danh sách vai trò mà
    /// <c>UserAccountsController</c> cấp phát được — không sinh ra vai trò mới.
    /// </summary>
    private static readonly string[] SupportedRoles = ["Admin", "HR", "Manager", "Employee", "IT"];

    /// <summary>
    /// Bốn trạng thái của bảng <c>yeu_cau_tai_khoan</c>, dùng chuỗi chứ không
    /// dùng enum để khớp đúng CHECK constraint ở CSDL. Hằng số thay vì literal
    /// rải rác trong truy vấn: đổi tên trạng thái là đổi một chỗ.
    /// </summary>
    private const string STATUS_CHO_IT = "CHO_IT";
    private const string STATUS_DANG_XU_LY = "DANG_XU_LY";
    private const string STATUS_DA_CAP = "DA_CAP";
    private const string STATUS_TU_CHOI = "TU_CHOI";

    private const int PageSize = 7;

    private readonly AppDbContext _context;
    private readonly ILogger<YeuCauTaiKhoanController> _logger;
    private readonly ISupabaseUserProvisioningService _supabaseUserProvisioning;
    private readonly IAccountRequestLock _requestLock;

    public YeuCauTaiKhoanController(
        AppDbContext context,
        ILogger<YeuCauTaiKhoanController> logger,
        ISupabaseUserProvisioningService supabaseUserProvisioning,
        IAccountRequestLock? requestLock = null)
    {
        _context = context;
        _logger = logger;
        _supabaseUserProvisioning = supabaseUserProvisioning;
        _requestLock = requestLock ?? new AccountRequestLock(context);
    }

    // =====================================================================
    //  INDEX — HỘP THƯ CỦA HR VÀ IT
    // =====================================================================
    [HttpGet]
    [Authorize(Policy = "AccountRequestAccess")]
    public async Task<IActionResult> Index(string? keyword, string? trangThai, int page = 1)
    {
        keyword = keyword?.Trim();
        trangThai = IsKnownStatus(trangThai) ? trangThai : null;
        page = Math.Max(1, page);

        var employeeId = GetCurrentEmployeeId();
        var role = GetCurrentRoleName();
        var canProcess = User.HasPermission(PermissionCodes.ProvisionMobileAccount);

        var baseQuery = BuildVisibleQuery(canProcess, employeeId);
        var visibleItems = await this.SearchDataAsync($"account-requests:process:{canProcess}:sender:{employeeId}", () => baseQuery.ToListAsync());
        baseQuery = visibleItems.AsQueryable();
        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(item =>
                item.TenDangNhap.ToLower().Contains(value) ||
                (item.NhanVien != null &&
                 (item.NhanVien.Ho.ToLower().Contains(value) ||
                  item.NhanVien.Ten.ToLower().Contains(value) ||
                  item.NhanVien.EmailCongVu.ToLower().Contains(value))));
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = trangThai == STATUS_CHO_IT
                ? query.Where(item => item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY)
                : query.Where(item => item.TrangThai == trangThai);
        }

        var totalFiltered = query.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)PageSize));
        var pageNumber = Math.Clamp(page, 1, totalPages);

        var requests = query
            .OrderByDescending(item => item.NgayGui)
            .ThenByDescending(item => item.MaYeuCau)
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(item => new YeuCauTaiKhoanListItemViewModel
            {
                RequestId = item.MaYeuCau,
                EmployeeId = item.MaNhanVien,
                Username = item.TenDangNhap,
                EmployeeName = item.NhanVien == null
                    ? "Nhân viên không tồn tại"
                    : (item.NhanVien.Ho + " " + item.NhanVien.Ten).Trim(),
                EmployeeEmail = item.NhanVien == null ? null : item.NhanVien.EmailCongVu,
                Department = item.NhanVien == null || item.NhanVien.PhongBan == null
                    ? null
                    : item.NhanVien.PhongBan.TenPhongBan,
                RoleName = item.VaiTro == null ? "Chưa cấp" : item.VaiTro.TenVaiTro,
                Status = item.TrangThai,
                StatusLabel = GetStatusLabel(item.TrangThai),
                IsPromotion = item.GhiChuTuChoi != null && item.GhiChuTuChoi.StartsWith("[THANG_CHUC]"),
                ContractNo = item.HopDong == null ? null : item.HopDong.SoHopDong,
                SenderName = item.NguoiGui == null
                    ? "Không xác định"
                    : (item.NguoiGui.Ho + " " + item.NguoiGui.Ten).Trim(),
                SenderRole = item.NguoiGui != null && item.NguoiGui.TaiKhoan != null && item.NguoiGui.TaiKhoan.VaiTro != null
                    ? item.NguoiGui.TaiKhoan.VaiTro.TenVaiTro : null,
                HandlerUsername = item.NguoiXuLy == null ? null : item.NguoiXuLy.TenDangNhap,
                RejectionNote = item.GhiChuTuChoi,
                SentAt = item.NgayGui,
                HandledAt = item.NgayXuLy,
                GrantedUsername = item.TaiKhoanDuocCap == null ? null : item.TaiKhoanDuocCap.TenDangNhap
            })
            .ToList();

        // Đếm KPI trên tập đã phân tát quyền, không phải trên toàn bảng — nếu
        // không thì số của HR sẽ phản ánh cả hộp thư của IT.
        var model = new YeuCauTaiKhoanIndexViewModel
        {
            Keyword = keyword,
            TrangThai = trangThai,
            StatusOptions = BuildStatusOptions(trangThai),
            Requests = requests,
            TotalRequests = baseQuery.Count(),
            PendingRequests = baseQuery.Count(item => item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY),
            GrantedRequests = baseQuery.Count(item => item.TrangThai == STATUS_DA_CAP),
            RejectedRequests = baseQuery.Count(item => item.TrangThai == STATUS_TU_CHOI),
            TotalFilteredRequests = totalFiltered,
            PageNumber = pageNumber,
            PageSize = PageSize,
            TotalPages = totalPages,
            CanSubmitRequest = LeaveApprovalPolicy.CanSubmitAccountRequest(role)
        };

        if (IsAjaxRequest())
        {
            Response.Headers["X-Total-Requests"] = model.TotalRequests.ToString();
            Response.Headers["X-Pending-Requests"] = model.PendingRequests.ToString();
            Response.Headers["X-Granted-Requests"] = model.GrantedRequests.ToString();
            Response.Headers["X-Rejected-Requests"] = model.RejectedRequests.ToString();
            Response.Headers["X-Result-Count"] = model.TotalFilteredRequests.ToString();
            return PartialView("_YeuCauTaiKhoanTable", model);
        }

        return View(model);
    }

    /// <summary>
    /// Phân tát quyền NGAY TRONG TRUY VẤN, không lọc ở tầng hiển thị.
    ///
    /// <para>
    /// IT và Admin thấy toàn bộ yêu cầu của khách sạn. HR chỉ thấy yêu cầu do
    /// CHÍNH MÌNH gửi (<c>ma_nguoi_gui = mã nhân viên của phiên</c>).
    /// </para>
    ///
    /// <para>
    /// Đây là ranh giới bảo mật, không phải lựa chọn hiển thị: HR không được
    /// nhìn thấy hộp thư của người khác, và người đọc CSDL không thấy gì ngoài tập này.
    /// </para>
    /// </summary>
    private IQueryable<YeuCauTaiKhoan> BuildVisibleQuery(bool canProcess, int? employeeId)
    {
        var query = _context.YeuCauTaiKhoans
            .AsNoTracking()
            .Include(item => item.NhanVien)!.ThenInclude(employee => employee!.PhongBan)
            .Include(item => item.HopDong)
            .Include(item => item.VaiTro)
            .Include(item => item.NguoiGui)
            .Include(item => item.NguoiXuLy)
            .Include(item => item.TaiKhoanDuocCap)
            .AsQueryable();

        if (canProcess)
        {
            return query;
        }

        // Không xác định được mã nhân viên trong phiên thì KHÔNG trả về gì cả
        // (fail-closed) — đừng trả về toàn bộ bảng chỉ vì thiếu một claim.
        if (!employeeId.HasValue)
        {
            return query.Where(item => false);
        }

        return query.Where(item => item.MaNguoiGui == employeeId.Value);
    }

    // =====================================================================
    //  CREATE — GỬI YÊU CẦU
    // =====================================================================
    [HttpGet]
    [Authorize(Policy = "AccountRequestCreate")]
    public async Task<IActionResult> Create(bool popup = false, int? employeeId = null)
    {
        var role = GetCurrentRoleName();
        if (!User.HasPermission(PermissionCodes.ManageEmployees) && !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            if (popup)
            {
                return Forbid();
            }
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền gửi yêu cầu cấp tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        if (popup && employeeId > 0 && !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            var employee = await _context.NhanViens.AsNoTracking()
                .Where(item => item.MaNhanVien == employeeId)
                .Select(item => new { Position = item.ChucVu != null ? item.ChucVu.TenChucVu : null,
                    Department = item.PhongBan != null ? item.PhongBan.TenPhongBan : null })
                .SingleOrDefaultAsync();
            var targetRole = employee == null ? null : ResolveRoleFromPositionAndDepartment(employee.Position, employee.Department).RoleName;
            if (targetRole != null && targetRole != "Employee" && !(LeaveApprovalPolicy.IsHR(role) && targetRole == "Manager"))
                return PartialView("_AccountRequestUnavailable");
        }

        var model = new CreateYeuCauTaiKhoanViewModel { IsPopup = popup, EmployeeId = employeeId ?? 0 };
        await LoadCreateOptionsAsync(model);
        return popup ? PartialView("_CreateRequestPopup", model) : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AccountRequestCreate")]
    public async Task<IActionResult> Create(CreateYeuCauTaiKhoanViewModel model, bool popup = false)
    {
        var role = GetCurrentRoleName();
        if (!User.HasPermission(PermissionCodes.ManageEmployees) && !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            if (popup)
            {
                return Forbid();
            }
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền gửi yêu cầu cấp tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        model.Username = NormalizeUsername(model.Username);

        var senderEmployeeId = GetCurrentEmployeeId();
        if (!senderEmployeeId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Không xác định được nhân viên đang đăng nhập. Vui lòng đăng nhập lại.");
            await LoadCreateOptionsAsync(model);
            if (popup || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("_CreateRequestPopup", model);
            }
            return View(model);
        }

        // Kiểm tra nhân viên: đang làm việc, có email công vụ, chưa có tài khoản,
        // và KHÔNG có yêu cầu nào đang chờ hoặc đang xử lý.
        var employee = await _context.NhanViens
            .AsNoTracking()
            .Where(item => item.MaNhanVien == model.EmployeeId)
            .Select(item => new { item.MaNhanVien, item.TrangThai, item.EmailCongVu, item.Ho, item.Ten,
                Position = item.ChucVu != null ? item.ChucVu.TenChucVu : null,
                Department = item.PhongBan != null ? item.PhongBan.TenPhongBan : null })
            .SingleOrDefaultAsync();

        if (employee == null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên được chọn không tồn tại.");
        }
        else if (await _context.TaiKhoans.AnyAsync(account => account.MaNhanVien == model.EmployeeId))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đã có tài khoản rồi nên không cần gửi yêu cầu cấp mới.");
        }
        else if (!string.Equals(employee.TrangThai, "DANG_LAM", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Chỉ nhân viên đang làm việc mới được gửi yêu cầu cấp tài khoản.");
        }
        else if (string.IsNullOrWhiteSpace(employee.EmailCongVu))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên chưa có email công vụ nên không thể đăng nhập Mobile App.");
        }
        else if (await _context.YeuCauTaiKhoans.AnyAsync(req =>
                     req.MaNhanVien == model.EmployeeId &&
                     (req.TrangThai == STATUS_CHO_IT || req.TrangThai == STATUS_DANG_XU_LY)))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đang có yêu cầu cấp tài khoản chờ hoặc đang được xử lý.");
        }

        // Tên đăng nhập phải duy nhất trong hệ thống và trong các yêu cầu đang mở.
        if (await _context.TaiKhoans.AnyAsync(account => account.TenDangNhap.ToLower() == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã thuộc về một tài khoản đang tồn tại.");
        }
        else if (await _context.YeuCauTaiKhoans.AnyAsync(item =>
                     (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY) &&
                     item.TenDangNhap.ToLower() == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đang có một yêu cầu chưa xử lý xong.");
        }

        // Vai trò phải do máy chủ xác định, không tin giá trị gửi từ trình duyệt.
        var roleName = employee == null ? null :
            ResolveRoleFromPositionAndDepartment(employee.Position, employee.Department).RoleName;
        if (!User.HasPermission(PermissionCodes.ProvisionMobileAccount) && roleName != null && roleName != "Employee" &&
            !(LeaveApprovalPolicy.IsHR(role) && roleName == "Manager"))
            ModelState.AddModelError(nameof(model.EmployeeId), "HR chỉ được yêu cầu cấp tài khoản Nhân viên và Quản lý; các vai trò khác do IT xử lý.");
        model.RoleId = roleName == null ? 0 : await _context.VaiTros.AsNoTracking()
            .Where(item => item.TenVaiTro == roleName)
            .Select(item => item.MaVaiTro)
            .SingleOrDefaultAsync();
        ModelState.Remove(nameof(model.RoleId));

        if (model.RoleId == 0 || roleName == null || !SupportedRoles.Contains(roleName))
        {
            ModelState.AddModelError(nameof(model.RoleId), "Không xác định được vai trò phù hợp trong hệ thống.");
        }
        else if (string.Equals(roleName, "Admin", StringComparison.Ordinal) &&
                 !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            ModelState.AddModelError(nameof(model.RoleId), "Chỉ Quản trị viên mới được đề xuất cấp vai trò Quản trị viên.");
        }

        // Kiểm tra hợp đồng: bắt buộc, của đúng nhân viên, trạng thái Hiệu lực, đã bắt đầu và chưa hết hạn.
        if (!model.ContractId.HasValue || model.ContractId.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.ContractId), "Vui lòng chọn hợp đồng lao động làm căn cứ cấp tài khoản.");
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var contract = await _context.HopDongs
                .AsNoTracking()
                .Where(c => c.MaHopDong == model.ContractId.Value && c.MaNhanVien == model.EmployeeId)
                .Select(c => new { c.TrangThai, c.NgayBatDau, c.NgayKetThuc })
                .SingleOrDefaultAsync();

            if (contract == null || !LeaveApprovalPolicy.IsValidContractForAccountRequest(contract.TrangThai, contract.NgayBatDau, contract.NgayKetThuc, today))
            {
                ModelState.AddModelError(nameof(model.ContractId), "Hợp đồng không hợp lệ hoặc đã hết hạn (chỉ chấp nhận HĐ đang hiệu lực, đã bắt đầu và chưa hết hạn).");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync(model);
            if (popup || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("_CreateRequestPopup", model);
            }
            return View(model);
        }

        var request = new YeuCauTaiKhoan
        {
            MaNhanVien = model.EmployeeId,
            MaHopDong = model.ContractId,
            TenDangNhap = model.Username,
            MaVaiTro = model.RoleId,
            TrangThai = STATUS_CHO_IT,
            NgayGui = DateTimeOffset.UtcNow,
            MaNguoiGui = senderEmployeeId.Value
        };

        var created = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Serialize submissions for this employee, including requests with different usernames.
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(2026101601, {model.EmployeeId})");
            if (await _context.YeuCauTaiKhoans.AnyAsync(item => item.MaNhanVien == model.EmployeeId &&
                (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY))) return false;

            _context.YeuCauTaiKhoans.Add(request);
            _context.NhatKyQuanTris.Add(CreateAuditLog(
                targetAccountId: null,
                targetUsername: model.Username,
                action: "CREATE_ACCOUNT_REQUEST",
                description: "Gửi yêu cầu cấp tài khoản đăng nhập cho nhân viên.",
                oldValue: null,
                newValue: $"Nhân viên #{model.EmployeeId}; Hợp đồng #{model.ContractId}; Vai trò: {GetVietnameseRoleName(roleName)}; Tên đăng nhập: {model.Username}",
                reason: "Nhân sự duyệt hợp đồng và gửi yêu cầu cấp tài khoản."));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
        if (!created)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đang có yêu cầu cấp tài khoản chờ hoặc đang được xử lý.");
            await LoadCreateOptionsAsync(model);
            if (popup || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("_CreateRequestPopup", model);
            }
            return View(model);
        }

        if (User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            return await Complete(request.MaYeuCau);
        }

        var successMessage = $"Đã gửi yêu cầu cấp tài khoản '{model.Username}'. Bộ phận Công nghệ Thông tin sẽ xử lý.";
        if (popup || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = true, message = successMessage });
        }

        TempData["Success"] = successMessage;
        return RedirectToAction("Index", "NhanVien");
    }

    // =====================================================================
    //  CREATE PROMOTION — GỬI YÊU CẦU THĂNG CHỨC LÊN QUẢN LÝ
    // =====================================================================
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> CreatePromotion(int? employeeId = null)
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanSubmitPromotionRequest(role))
        {
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền đề xuất thăng chức cho nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CreatePromotionRequestViewModel();
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            model.EmployeeId = employeeId.Value;
        }

        await LoadPromotionOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> CreatePromotion(CreatePromotionRequestViewModel model)
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanSubmitPromotionRequest(role))
        {
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền đề xuất thăng chức cho nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        var senderEmployeeId = GetCurrentEmployeeId();
        if (!senderEmployeeId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Không xác định được nhân viên đang đăng nhập. Vui lòng đăng nhập lại.");
            await LoadPromotionOptionsAsync(model);
            return View(model);
        }

        var managerRole = await _context.VaiTros
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenVaiTro == "Manager");
        if (managerRole == null)
        {
            ModelState.AddModelError(string.Empty, "Hệ thống chưa thiết lập vai trò Quản lý (Manager).");
            await LoadPromotionOptionsAsync(model);
            return View(model);
        }

        model.TargetRoleId = managerRole.MaVaiTro;

        var employee = await _context.NhanViens
            .AsNoTracking()
            .Include(item => item.TaiKhoan)!.ThenInclude(tk => tk!.VaiTro)
            .Include(item => item.ChucVu)
            .FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);

        if (employee == null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên được chọn không tồn tại.");
        }
        else if (employee.TaiKhoan == null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên chưa có tài khoản. Vui lòng gửi yêu cầu cấp mới tài khoản trước khi thăng chức.");
        }
        else if (!HopDongController.IsManagerPosition(employee.ChucVu?.TenChucVu))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Chỉ gửi yêu cầu cho nhân viên đã lập hợp đồng Quản lý.");
        }
        else if (!string.Equals(employee.TrangThai, "DANG_LAM", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Chỉ nhân viên đang làm việc mới được đề xuất thăng chức.");
        }
        else if (!LeaveApprovalPolicy.CanPromoteRole(employee.TaiKhoan.VaiTro?.TenVaiTro, "Manager"))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Quy định hệ thống: Chỉ nhân viên (Employee) mới có thể được đề xuất thăng chức lên Quản lý (Manager).");
        }
        else if (await _context.YeuCauTaiKhoans.AnyAsync(req =>
                     req.MaNhanVien == model.EmployeeId &&
                     (req.TrangThai == STATUS_CHO_IT || req.TrangThai == STATUS_DANG_XU_LY)))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đang có một yêu cầu chờ hoặc đang được xử lý.");
        }

        if (!model.ContractId.HasValue || model.ContractId.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.ContractId), "Vui lòng chọn hợp đồng lao động làm căn cứ thăng chức.");
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var contract = await _context.HopDongs
                .AsNoTracking()
                .Where(c => c.MaHopDong == model.ContractId.Value && c.MaNhanVien == model.EmployeeId)
                .Select(c => new { c.TrangThai, c.NgayBatDau, c.NgayKetThuc })
                .SingleOrDefaultAsync();

            if (contract == null || contract.TrangThai != "HIEU_LUC" || (contract.NgayKetThuc.HasValue && contract.NgayKetThuc.Value < today))
            {
                ModelState.AddModelError(nameof(model.ContractId), "Hợp đồng không hợp lệ hoặc đã hết hạn (chỉ chấp nhận HĐ đang hiệu lực, đã bắt đầu và chưa hết hạn).");
            }
        }

        if (model.ContractId.HasValue && await _context.HopDongs.AnyAsync(c => c.MaNhanVien == model.EmployeeId &&
            c.TrangThai == "HIEU_LUC" && _context.HopDongs.Any(selected => selected.MaHopDong == model.ContractId &&
                selected.MaNhanVien == model.EmployeeId && c.NgayBatDau > selected.NgayBatDau)))
            ModelState.AddModelError(nameof(model.ContractId), "Hãy chọn hợp đồng mới nhất của nhân viên.");

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Vui lòng nhập lý do đề xuất thăng chức.");
        }

        if (!ModelState.IsValid)
        {
            await LoadPromotionOptionsAsync(model);
            return View(model);
        }

        var promotionNote = $"[THANG_CHUC] Lý do: {model.Reason.Trim()}";
        if (promotionNote.Length > 500)
        {
            promotionNote = promotionNote[..500];
        }

        var request = new YeuCauTaiKhoan
        {
            MaNhanVien = model.EmployeeId,
            MaHopDong = model.ContractId,
            TenDangNhap = employee!.TaiKhoan!.TenDangNhap,
            MaVaiTro = managerRole.MaVaiTro,
            TrangThai = STATUS_CHO_IT,
            NgayGui = DateTimeOffset.UtcNow,
            MaNguoiGui = senderEmployeeId.Value,
            GhiChuTuChoi = promotionNote
        };

        var created = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(2026101601, {model.EmployeeId})");
            if (await _context.YeuCauTaiKhoans.AnyAsync(item => item.MaNhanVien == model.EmployeeId &&
                (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY))) return false;

            _context.YeuCauTaiKhoans.Add(request);
            _context.NhatKyQuanTris.Add(CreateAuditLog(
                targetAccountId: employee.TaiKhoan.MaTaiKhoan,
                targetUsername: employee.TaiKhoan.TenDangNhap,
                action: "CREATE_PROMOTION_REQUEST",
                description: $"Đề xuất thăng chức nhân viên #{model.EmployeeId} lên Quản lý (Manager).",
                oldValue: $"Vai trò hiện tại: {employee.TaiKhoan.VaiTro?.TenVaiTro ?? "Employee"}",
                newValue: $"Vai trò đề xuất: Quản lý (Manager); Hợp đồng #{model.ContractId}",
                reason: model.Reason));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });

        if (!created)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên này đang có yêu cầu chờ hoặc đang được xử lý.");
            await LoadPromotionOptionsAsync(model);
            return View(model);
        }

        TempData["Success"] = $"Đã gửi yêu cầu thăng chức cho nhân viên '{employee.HoTen}' lên Quản lý. Bộ phận IT hoặc Admin sẽ phê duyệt.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Gửi nhanh yêu cầu thăng chức từ popup khi lập hợp đồng thăng chức mới thành công.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageEmployees)]
    public async Task<IActionResult> QuickSubmitPromotion(int employeeId, int contractId)
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanSubmitPromotionRequest(role))
        {
            return Json(new { success = false, message = "Bạn không có thẩm quyền gửi yêu cầu thăng chức." });
        }

        var senderEmployeeId = GetCurrentEmployeeId();
        if (!senderEmployeeId.HasValue)
        {
            return Json(new { success = false, message = "Không xác định được danh tính nhân sự đang đăng nhập." });
        }

        var employee = await _context.NhanViens
            .Include(e => e.TaiKhoan)!.ThenInclude(t => t!.VaiTro)
            .Include(e => e.ChucVu)
            .FirstOrDefaultAsync(e => e.MaNhanVien == employeeId);

        if (employee == null || employee.TaiKhoan == null)
        {
            return Json(new { success = false, message = "Nhân viên chưa có tài khoản để thăng chức." });
        }

        if (employee.TrangThai != "DANG_LAM" ||
            employee.TaiKhoan.VaiTro?.TenVaiTro != "Employee" ||
            !HopDongController.IsManagerPosition(employee.ChucVu?.TenChucVu))
        {
            return Json(new { success = false, message = "Chỉ gửi yêu cầu cho nhân viên đã lập hợp đồng Quản lý." });
        }

        var managerRole = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(r => r.TenVaiTro == "Manager");
        if (managerRole == null)
        {
            return Json(new { success = false, message = "Không tìm thấy vai trò Quản lý trong hệ thống." });
        }

        var contract = await _context.HopDongs.FindAsync(contractId);
        if (contract == null || contract.MaNhanVien != employeeId || contract.TrangThai != "HIEU_LUC" ||
            (contract.NgayKetThuc.HasValue && contract.NgayKetThuc.Value < DateOnly.FromDateTime(DateTime.Today)) ||
            await _context.HopDongs.AnyAsync(c => c.MaNhanVien == employeeId && c.MaHopDong != contractId &&
                c.TrangThai == "HIEU_LUC" && c.NgayBatDau > contract.NgayBatDau))
        {
            return Json(new { success = false, message = "Hợp đồng không hợp lệ." });
        }

        // Kiểm tra xem đã có yêu cầu thăng chức đang chờ xử lý chưa
        var hasPending = await _context.YeuCauTaiKhoans.AnyAsync(r => r.MaNhanVien == employeeId &&
            (r.TrangThai == STATUS_CHO_IT || r.TrangThai == STATUS_DANG_XU_LY));

        if (hasPending)
        {
            return Json(new { success = false, message = "Nhân viên này đã có một yêu cầu cấp/thăng chức tài khoản đang chờ IT xử lý." });
        }

        var request = new YeuCauTaiKhoan
        {
            MaNhanVien = employeeId,
            MaHopDong = contractId,
            TenDangNhap = employee.TaiKhoan.TenDangNhap,
            MaVaiTro = managerRole.MaVaiTro,
            TrangThai = STATUS_CHO_IT,
            NgayGui = DateTimeOffset.UtcNow,
            MaNguoiGui = senderEmployeeId.Value,
            GhiChuTuChoi = $"[THANG_CHUC] Thăng chức lên {employee.ChucVu?.TenChucVu ?? "Quản lý"} theo hợp đồng {contract.SoHopDong}"
        };

        var created = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(2026101601, {employeeId})");
            if (await _context.YeuCauTaiKhoans.AnyAsync(r => r.MaNhanVien == employeeId &&
                (r.TrangThai == STATUS_CHO_IT || r.TrangThai == STATUS_DANG_XU_LY))) return false;
            _context.YeuCauTaiKhoans.Add(request);
            _context.NhatKyQuanTris.Add(CreateAuditLog(employee.TaiKhoan.MaTaiKhoan, employee.TaiKhoan.TenDangNhap,
                "CREATE_PROMOTION_REQUEST", "Gửi yêu cầu thăng chức lên Quản lý.", "Employee", "Manager",
                $"Hợp đồng #{contractId}"));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
        if (!created)
            return Json(new { success = false, message = "Nhân viên đã có yêu cầu đang chờ IT xử lý." });

        return Json(new { success = true, message = $"Đã gửi yêu cầu thăng chức cho {employee.HoTen} sang bộ phận IT thành công!" });
    }

    /// <summary>
    /// Nạp danh sách nhân viên vai trò Employee và hợp đồng còn hiệu lực cho form thăng chức.
    /// </summary>
    private async Task LoadPromotionOptionsAsync(CreatePromotionRequestViewModel model)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var employeeRole = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(r => r.TenVaiTro == "Employee");
        var managerRole = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(r => r.TenVaiTro == "Manager");
        var employeeRoleId = employeeRole?.MaVaiTro ?? 0;
        model.TargetRoleId = managerRole?.MaVaiTro ?? 0;

        model.EligibleEmployees = await _context.NhanViens
            .AsNoTracking()
            .Where(employee =>
                employee.TrangThai == "DANG_LAM" &&
                employee.EmailCongVu != null &&
                employee.EmailCongVu != string.Empty &&
                _context.TaiKhoans.Any(account => account.MaNhanVien == employee.MaNhanVien && account.MaVaiTro == employeeRoleId) &&
                !_context.YeuCauTaiKhoans.Any(req =>
                    req.MaNhanVien == employee.MaNhanVien &&
                    (req.TrangThai == STATUS_CHO_IT || req.TrangThai == STATUS_DANG_XU_LY)))
            .OrderBy(employee => employee.Ho)
            .ThenBy(employee => employee.Ten)
            .Select(employee => new SelectListItem
            {
                Value = employee.MaNhanVien.ToString(),
                Text = (employee.Ho + " " + employee.Ten).Trim() + " (" + employee.EmailCongVu + ")",
                Selected = employee.MaNhanVien == model.EmployeeId
            })
            .ToListAsync();

        // Nạp về bộ nhớ rồi mới ghép nhãn: ContractTypes.ToDisplayName không dịch
        // được sang SQL. Nếu chiếu thẳng cột vào Label như trước, người dùng đọc
        // thấy mã "XAC_DINH_THOI_HAN_1_NAM" thay vì tên tiếng Việt.
        var hopDongHieuLuc = await _context.HopDongs
            .AsNoTracking()
            .Where(contract =>
                contract.TrangThai == "HIEU_LUC" &&
                (contract.NgayKetThuc == null || contract.NgayKetThuc.Value >= today))
            .OrderBy(contract => contract.NgayKetThuc ?? DateOnly.MaxValue)
            .ThenBy(contract => contract.SoHopDong)
            .Select(contract => new
            {
                contract.MaHopDong,
                contract.MaNhanVien,
                contract.SoHopDong,
                contract.LoaiHopDong,
                contract.NgayKetThuc
            })
            .ToListAsync();

        model.ActiveContracts = hopDongHieuLuc.Select(contract => new ActiveContractOptionViewModel
        {
            ContractId = contract.MaHopDong,
            EmployeeId = contract.MaNhanVien,
            ContractNo = contract.SoHopDong,
            Label = contract.SoHopDong + " · " +
                    ContractTypes.ToDisplayName(contract.LoaiHopDong) + " · " +
                    (contract.NgayKetThuc == null
                        ? "không xác định thời hạn"
                        : "đến " + contract.NgayKetThuc.Value.ToString("dd/MM/yyyy"))
        }).ToList();
    }

    /// <summary>
    /// Nạp danh sách nhân viên, vai trò và hợp đồng còn hiệu lực cho form gửi yêu cầu.
    /// </summary>
    private async Task LoadCreateOptionsAsync(CreateYeuCauTaiKhoanViewModel model)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Chỉ hiện nhân viên đang làm việc, có email công vụ, chưa có tài khoản,
        // và KHÔNG có yêu cầu nào đang chờ hoặc đang xử lý.
        var eligibleEmployees = await _context.NhanViens
            .AsNoTracking()
            .Include(employee => employee.ChucVu)
            .Include(employee => employee.PhongBan)
            .Where(employee =>
                employee.TrangThai == "DANG_LAM" &&
                employee.EmailCongVu != null &&
                employee.EmailCongVu != string.Empty &&
                !_context.TaiKhoans.Any(account => account.MaNhanVien == employee.MaNhanVien) &&
                !_context.YeuCauTaiKhoans.Any(req =>
                    req.MaNhanVien == employee.MaNhanVien &&
                    (req.TrangThai == STATUS_CHO_IT || req.TrangThai == STATUS_DANG_XU_LY)))
            .OrderBy(employee => employee.Ho)
            .ThenBy(employee => employee.Ten)
            .ToListAsync();

        model.EmployeeOptions = eligibleEmployees.Select(employee => new SelectListItem
        {
            Value = employee.MaNhanVien.ToString(),
            Text = (employee.Ho + " " + employee.Ten).Trim() + " · " + employee.EmailCongVu,
            Selected = employee.MaNhanVien == model.EmployeeId
        }).ToList();

        // IT có thể cấp vai trò Admin cho người thuộc Ban Giám đốc.
        var senderIsAdmin = User.HasPermission(PermissionCodes.ProvisionMobileAccount);
        var roleNames = SupportedRoles
            .Where(roleName => senderIsAdmin || !string.Equals(roleName, "Admin", StringComparison.Ordinal))
            .ToArray();

        var dbRoles = await _context.VaiTros
            .AsNoTracking()
            .Where(role => roleNames.Contains(role.TenVaiTro))
            .OrderBy(role => role.MaVaiTro)
            .ToListAsync();

        model.RoleOptions = dbRoles.Select(role => new SelectListItem
        {
            Value = role.MaVaiTro.ToString(),
            Text = GetVietnameseRoleName(role.TenVaiTro),
            Selected = role.MaVaiTro == model.RoleId
        }).ToList();

        var roleMapByName = dbRoles.ToDictionary(r => r.TenVaiTro, r => r.MaVaiTro, StringComparer.OrdinalIgnoreCase);
        int GetRoleId(string rName)
        {
            if (roleMapByName.TryGetValue(rName, out var id)) return id;
            return 0;
        }

        model.EmployeeRoleMap = new Dictionary<int, EmployeeContractRoleInfo>();
        foreach (var emp in eligibleEmployees)
        {
            var pName = emp.ChucVu?.TenChucVu ?? "Chưa phân bổ";
            var dName = emp.PhongBan?.TenPhongBan ?? "Khách sạn";
            var (rName, rDisplay) = ResolveRoleFromPositionAndDepartment(pName, dName);
            model.EmployeeRoleMap[emp.MaNhanVien] = new EmployeeContractRoleInfo
            {
                EmployeeId = emp.MaNhanVien,
                RoleId = GetRoleId(rName),
                RoleName = rName,
                RoleDisplayName = rDisplay,
                PositionName = pName,
                DepartmentName = dName
            };
        }

        // 3. Chỉ hợp đồng HIỆU LỰC, ĐÃ BẮT ĐẦU và CHƯA HẾT HẠN (chấp nhận hết hạn hôm nay hoặc không thời hạn)
        var hopDongHieuLuc = await _context.HopDongs
            .AsNoTracking()
            .Include(contract => contract.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .Include(contract => contract.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Where(contract =>
                contract.TrangThai == "HIEU_LUC" &&
                contract.NgayBatDau <= today &&
                (contract.NgayKetThuc == null || contract.NgayKetThuc.Value >= today))
            .OrderBy(contract => contract.NgayKetThuc ?? DateOnly.MaxValue)
            .ThenBy(contract => contract.SoHopDong)
            .ToListAsync();

        model.ActiveContracts = hopDongHieuLuc.Select(contract =>
        {
            var pName = contract.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân bổ";
            var dName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn";
            var (rName, rDisplay) = ResolveRoleFromPositionAndDepartment(pName, dName);
            return new ActiveContractOptionViewModel
            {
                ContractId = contract.MaHopDong,
                EmployeeId = contract.MaNhanVien,
                ContractNo = contract.SoHopDong,
                RoleId = GetRoleId(rName),
                RoleName = rName,
                RoleDisplayName = rDisplay,
                PositionName = pName,
                DepartmentName = dName,
                Label = contract.SoHopDong + " · " +
                        ContractTypes.ToDisplayName(contract.LoaiHopDong) + " · " +
                        (contract.NgayKetThuc == null
                            ? "không xác định thời hạn"
                            : "đến " + contract.NgayKetThuc.Value.ToString("dd/MM/yyyy"))
            };
        }).ToList();

        if (model.EmployeeId > 0 && model.EmployeeRoleMap.TryGetValue(model.EmployeeId, out var selectedInfo))
        {
            model.RoleId = selectedInfo.RoleId;
        }
        else if (model.RoleId == 0)
        {
            var firstEmp = eligibleEmployees.FirstOrDefault();
            if (firstEmp != null && model.EmployeeRoleMap.TryGetValue(firstEmp.MaNhanVien, out var firstInfo))
            {
                model.RoleId = firstInfo.RoleId;
            }
            else
            {
                var defaultRole = model.RoleOptions.FirstOrDefault(option => option.Text == GetVietnameseRoleName("Employee"));
                model.RoleId = defaultRole == null ? 0 : int.Parse(defaultRole.Value!);
            }
        }
    }

    // =====================================================================
    //  COMPLETE — IT CẤP TÀI KHOẢN
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public async Task<IActionResult> Complete(int id)
    {
        await using var requestLock = await _requestLock.TryAcquireAsync(id, HttpContext.RequestAborted);
        if (requestLock == null)
            return ProcessingFailure("Yêu cầu này đang được xử lý. Vui lòng thử lại sau.", StatusCodes.Status409Conflict);
        return await CompleteLockedAsync(id);
    }

    private async Task<IActionResult> CompleteLockedAsync(int id)
    {
        var role = GetCurrentRoleName();
        if (!User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            return ProcessingFailure("Chỉ Quản trị viên và bộ phận Công nghệ Thông tin mới có quyền cấp tài khoản.", StatusCodes.Status403Forbidden);
        }

        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return ProcessingFailure("Không xác định được tài khoản đang đăng nhập.", StatusCodes.Status401Unauthorized);
        }

        var request = await _context.YeuCauTaiKhoans
            .AsNoTracking()
            .Include(item => item.NhanVien)
            .Include(item => item.HopDong)
            .Include(item => item.VaiTro)
            .Include(item => item.NguoiGui)!.ThenInclude(sender => sender!.TaiKhoan)!.ThenInclude(account => account!.VaiTro)
            .FirstOrDefaultAsync(item => item.MaYeuCau == id);

        if (request == null)
        {
            return ProcessingFailure("Không tìm thấy yêu cầu cần cấp tài khoản.", StatusCodes.Status404NotFound);
        }

        var isManagerPromotion = request.VaiTro?.TenVaiTro == "Manager" &&
            request.GhiChuTuChoi?.StartsWith("[THANG_CHUC]", StringComparison.Ordinal) == true;
        if (request.NguoiGui?.TaiKhoan?.VaiTro?.TenVaiTro == "HR" && !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
            return ProcessingFailure("Chỉ IT được duyệt yêu cầu do HR gửi.", StatusCodes.Status403Forbidden);
        if (isManagerPromotion && !User.HasPermission(PermissionCodes.ProvisionMobileAccount))
            return ProcessingFailure("Chỉ IT được duyệt yêu cầu thăng chức Quản lý.", StatusCodes.Status403Forbidden);
        if (!isManagerPromotion && request.VaiTro?.TenVaiTro != "Employee" &&
            !(request.VaiTro?.TenVaiTro == "Manager" && request.NguoiGui?.TaiKhoan?.VaiTro?.TenVaiTro == "HR" && User.HasPermission(PermissionCodes.ProvisionMobileAccount)) &&
            request.NguoiGui?.TaiKhoan?.VaiTro?.TenVaiTro != "IT")
            return ProcessingFailure("Yêu cầu cũ cho vai trò quản lý không còn hợp lệ. IT cần cấp hoặc đổi vai trò trực tiếp.", StatusCodes.Status403Forbidden);

        if (request.TrangThai is not (STATUS_CHO_IT or STATUS_DANG_XU_LY))
        {
            if (string.Equals(request.TrangThai, STATUS_DA_CAP, StringComparison.Ordinal))
            {
                return ProcessingSuccess(
                    $"Yêu cầu này đã được cấp tài khoản thành công ({request.TenDangNhap}).",
                    await GetStatusOptionsForRefresh(role));
            }

            return ProcessingFailure(
                "Yêu cầu này không còn chờ cấp tài khoản.",
                StatusCodes.Status409Conflict);
        }

        if (request.NhanVien?.TrangThai != "DANG_LAM")
        {
            return ProcessingFailure("Nhân viên không còn ở trạng thái đang làm việc. Không thể cấp tài khoản.", StatusCodes.Status400BadRequest);
        }

        var contract = request.HopDong;
        if (contract == null || contract.MaNhanVien != request.MaNhanVien ||
            !LeaveApprovalPolicy.IsValidContractForAccountRequest(contract.TrangThai, contract.NgayBatDau, contract.NgayKetThuc, DateOnly.FromDateTime(DateTime.Today)))
        {
            return ProcessingFailure("Hợp đồng của nhân viên không còn hiệu lực. Không thể cấp tài khoản.", StatusCodes.Status400BadRequest);
        }

        // Kiểm tra xem đây có phải là yêu cầu thăng chức (nhân viên đã có tài khoản) hay không
        var existingAccount = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .FirstOrDefaultAsync(t => t.MaNhanVien == request.MaNhanVien);

        if (isManagerPromotion && existingAccount == null)
            return ProcessingFailure("Không tìm thấy tài khoản nhân viên cần thăng chức.", StatusCodes.Status409Conflict);

        if (existingAccount != null)
        {
            if (isManagerPromotion && (existingAccount.VaiTro?.TenVaiTro != "Employee" ||
                !HopDongController.IsManagerPosition((await _context.ChucVus.FindAsync(request.NhanVien!.MaChucVu))?.TenChucVu)))
                return ProcessingFailure("Tài khoản hoặc chức vụ không còn phù hợp với yêu cầu thăng chức.", StatusCodes.Status409Conflict);
            var isPromotion = (request.GhiChuTuChoi != null && request.GhiChuTuChoi.StartsWith("[THANG_CHUC]")) || existingAccount.MaVaiTro != request.MaVaiTro;
            if (!isPromotion)
            {
                return ProcessingFailure("Nhân viên đã có tài khoản với đúng vai trò này. Không cần cấp lại.", StatusCodes.Status409Conflict);
            }

            var targetRole = await _context.VaiTros.FindAsync(request.MaVaiTro);
            if (targetRole == null)
            {
                return ProcessingFailure("Vai trò chỉ định không tồn tại trong hệ thống.", StatusCodes.Status400BadRequest);
            }

            var oldRoleName = existingAccount.VaiTro?.TenVaiTro ?? "Chưa rõ";
            try
            {
                var executionStrategy = _context.Database.CreateExecutionStrategy();
                await executionStrategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    existingAccount.MaVaiTro = request.MaVaiTro;
                    _context.TaiKhoans.Update(existingAccount);

                    _context.NhatKyQuanTris.Add(CreateAuditLog(
                        targetAccountId: existingAccount.MaTaiKhoan,
                        targetUsername: existingAccount.TenDangNhap,
                        action: "UPGRADE_ACCOUNT_ROLE",
                        description: $"Nâng vai trò tài khoản từ '{oldRoleName}' lên '{targetRole.TenVaiTro}' theo yêu cầu thăng chức #{request.MaYeuCau}.",
                        oldValue: $"Vai trò: {oldRoleName}",
                        newValue: $"Vai trò: {targetRole.TenVaiTro}",
                        reason: request.GhiChuTuChoi ?? "Phê duyệt thăng chức theo hợp đồng lao động mới."));

                    await _context.SaveChangesAsync();

                    var completed = await _context.YeuCauTaiKhoans
                        .Where(item => item.MaYeuCau == id && (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY))
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(item => item.TrangThai, STATUS_DA_CAP)
                            .SetProperty(item => item.MaTaiKhoan, existingAccount.MaTaiKhoan)
                            .SetProperty(item => item.MaNguoiXuLy, accountId.Value)
                            .SetProperty(item => item.NgayXuLy, DateTimeOffset.UtcNow));

                    if (completed == 0)
                    {
                        throw new InvalidOperationException($"Yêu cầu #{id} đã chuyển trạng thái trước khi hoàn tất.");
                    }

                    await transaction.CommitAsync();
                });

                return ProcessingSuccess($"Đã phê duyệt thăng chức! Tài khoản '{existingAccount.TenDangNhap}' đã được nâng quyền lên vai trò {targetRole.TenVaiTro}.", await GetStatusOptionsForRefresh(role));
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Không thể nâng vai trò cho tài khoản của yêu cầu #{RequestId}.", id);
                return ProcessingFailure("Không thể cập nhật vai trò tài khoản. Vui lòng thử lại sau.", StatusCodes.Status500InternalServerError);
            }
        }

        var workEmail = request.NhanVien?.EmailCongVu;
        if (string.IsNullOrWhiteSpace(workEmail))
        {
            return ProcessingFailure(
                "Nhân viên này chưa có email công vụ nên không thể đăng nhập Mobile App. Cần bổ sung email công vụ trước khi cấp.",
                StatusCodes.Status400BadRequest);
        }

        var username = request.TenDangNhap;

        const string initialPassword = "Overtime@2026";
        var provisionResult = await _supabaseUserProvisioning.CreateUserAsync(
            workEmail, initialPassword, HttpContext.RequestAborted);
        if (!provisionResult.Success)
        {
            _logger.LogWarning("Không cấp được Supabase Auth cho yêu cầu #{RequestId}: {ErrorCode}", id, provisionResult.ErrorCode);
            return ProcessingFailure(provisionResult.ErrorMessage ?? "Không tạo được tài khoản đăng nhập trên Mobile App.", StatusCodes.Status502BadGateway);
        }
        var supabaseUserId = provisionResult.SupabaseUserId!;

        var account = new TaiKhoan
        {
            MaNhanVien = request.MaNhanVien,
            MaVaiTro = request.MaVaiTro,
            TenDangNhap = username,
            MatKhauBam = PasswordHasher.Hash(initialPassword),
            TrangThai = true
        };

        try
        {
            // EnableRetryOnFailure cần execution strategy bọc toàn bộ transaction.
            // Nếu không, Npgsql sẽ từ chối transaction do người dùng tự khởi tạo.
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.TaiKhoans.Add(account);
                await _context.SaveChangesAsync();

                _context.NhatKyQuanTris.Add(CreateAuditLog(
                    targetAccountId: account.MaTaiKhoan,
                    targetUsername: username,
                    action: "CREATE_ACCOUNT",
                    description: "Tạo tài khoản từ yêu cầu cấp tài khoản do Nhân sự gửi.",
                    oldValue: $"Yêu cầu #{request.MaYeuCau} · trạng thái {request.TrangThai}",
                    newValue: $"Tài khoản #{account.MaTaiKhoan} · trạng thái Hoạt động",
                    reason: "Cấp tài khoản theo yêu cầu đã duyệt."));

                if (!string.IsNullOrWhiteSpace(supabaseUserId))
                {
                    _context.NhatKyQuanTris.Add(CreateAuditLog(
                        targetAccountId: account.MaTaiKhoan,
                        targetUsername: username,
                        action: "CREATE_ACCOUNT_AUTH",
                        description: "Cấp tài khoản Supabase Auth để nhân viên đăng nhập Mobile App bằng email công vụ.",
                        oldValue: null,
                        newValue: $"Email công vụ: {workEmail}; Supabase User ID: {supabaseUserId}",
                        reason: "Hoàn tất cấp quyền đăng nhập cho yêu cầu."));
                }

                await _context.SaveChangesAsync();

                // Chỉ hoàn tất nếu yêu cầu vẫn chưa được cấp hoặc từ chối.
                var completed = await _context.YeuCauTaiKhoans
                    .Where(item => item.MaYeuCau == id && (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.TrangThai, STATUS_DA_CAP)
                        .SetProperty(item => item.MaTaiKhoan, account.MaTaiKhoan)
                        .SetProperty(item => item.MaNguoiXuLy, accountId.Value)
                        .SetProperty(item => item.NgayXuLy, DateTimeOffset.UtcNow));

                if (completed == 0)
                {
                    throw new InvalidOperationException(
                        $"Yêu cầu #{id} đã chuyển trạng thái trước khi hoàn tất cấp tài khoản.");
                }

                await transaction.CommitAsync();
            });
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Không thể lưu tài khoản cho yêu cầu #{RequestId}.", id);
            try
            {
                // A lost commit response does not prove the transaction rolled back.
                var committed = account.MaTaiKhoan > 0 && await _context.YeuCauTaiKhoans.AsNoTracking().AnyAsync(item =>
                    item.MaYeuCau == id && item.TrangThai == STATUS_DA_CAP && item.MaTaiKhoan == account.MaTaiKhoan);
                if (committed)
                    return ProcessingSuccess($"Đã cấp tài khoản '{username}'. Mật khẩu mặc định: {initialPassword}.", await GetStatusOptionsForRefresh(role));

                if (!await _supabaseUserProvisioning.DeleteUserAsync(supabaseUserId, CancellationToken.None))
                {
                    _logger.LogError("Cần kiểm tra Supabase user {SupabaseUserId} của yêu cầu #{RequestId}: không gỡ được sau lỗi lưu CSDL.", supabaseUserId, id);
                    return ProcessingFailure("Không lưu được tài khoản. Quản trị viên cần kiểm tra tài khoản Supabase trước khi thử lại.", StatusCodes.Status502BadGateway);
                }
            }
            catch (Exception verificationError)
            {
                _logger.LogError(verificationError, "Chưa xác định kết quả cấp yêu cầu #{RequestId}, Supabase user {SupabaseUserId}.", id, supabaseUserId);
                return ProcessingFailure("Chưa xác định được kết quả cấp tài khoản. Vui lòng liên hệ quản trị viên để kiểm tra trước khi thử lại.", StatusCodes.Status503ServiceUnavailable);
            }
            return ProcessingFailure(
                "Không thể tạo tài khoản do dữ liệu bị trùng hoặc kết nối CSDL gặp lỗi.",
                StatusCodes.Status500InternalServerError);
        }

        var successMessage = $"Đã cấp tài khoản '{username}'. Mật khẩu mặc định: {initialPassword}.";

        return ProcessingSuccess(
            successMessage,
            await GetStatusOptionsForRefresh(GetCurrentRoleName()));
    }

    // =====================================================================
    //  REJECT — IT HOẶC ADMIN TỪ CHỐI
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public async Task<IActionResult> Reject(int id, string? lyDo)
    {
        await using var requestLock = await _requestLock.TryAcquireAsync(id, HttpContext.RequestAborted);
        if (requestLock == null)
            return ProcessingFailure("Yêu cầu này đang được xử lý. Vui lòng thử lại sau.", StatusCodes.Status409Conflict);
        return await RejectLockedAsync(id, lyDo);
    }

    private async Task<IActionResult> RejectLockedAsync(int id, string? lyDo)
    {
        var role = GetCurrentRoleName();
        if (!User.HasPermission(PermissionCodes.ProvisionMobileAccount))
        {
            return ProcessingFailure("Chỉ Quản trị viên và bộ phận Công nghệ Thông tin mới có quyền từ chối yêu cầu.", StatusCodes.Status403Forbidden);
        }

        lyDo = lyDo?.Trim();

        // CHECK chk_yeu_cau_tu_choi_can_co_ghi_chu ở CSDL sẽ ném 23514 nếu để
        // null — bắt ở tầng Web để người dùng thấy lý do bằng tiếng Việt.
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            return ProcessingFailure("Bắt buộc phải nhập lý do từ chối.");
        }

        if (lyDo.Length > 500)
        {
            return ProcessingFailure("Lý do từ chối không được vượt quá 500 ký tự.");
        }

        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return ProcessingFailure("Không xác định được tài khoản đang đăng nhập.", StatusCodes.Status401Unauthorized);
        }

        var request = await _context.YeuCauTaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MaYeuCau == id);

        if (request == null)
        {
            return ProcessingFailure("Không tìm thấy yêu cầu cần từ chối.", StatusCodes.Status404NotFound);
        }

        if (request.TrangThai is not (STATUS_CHO_IT or STATUS_DANG_XU_LY))
        {
            return ProcessingFailure("Yêu cầu này không còn chờ xử lý.", StatusCodes.Status409Conflict);
        }

        var affected = await _context.YeuCauTaiKhoans
            .Where(item => item.MaYeuCau == id && (item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.TrangThai, STATUS_TU_CHOI)
                .SetProperty(item => item.GhiChuTuChoi, request.GhiChuTuChoi != null && request.GhiChuTuChoi.StartsWith("[THANG_CHUC]") ? (request.GhiChuTuChoi.Length + lyDo.Length + 14 <= 500 ? $"{request.GhiChuTuChoi} | Từ chối: {lyDo}" : $"[THANG_CHUC] Từ chối: {lyDo}") : lyDo)
                .SetProperty(item => item.MaNguoiXuLy, accountId.Value)
                .SetProperty(item => item.NgayXuLy, DateTimeOffset.UtcNow));

        if (affected == 0)
        {
            _logger.LogWarning("Xung đột từ chối yêu cầu #{RequestId}: yêu cầu không còn chờ xử lý.", id);
            return ProcessingFailure(
                "Yêu cầu này không còn chờ xử lý. Vui lòng tải lại danh sách.",
                StatusCodes.Status409Conflict);
        }

        _context.NhatKyQuanTris.Add(CreateAuditLog(
            targetAccountId: null,
            targetUsername: $"YEU_CAU_#{id}",
            action: "REJECT_ACCOUNT_REQUEST",
            description: "Từ chối cấp tài khoản đăng nhập.",
            oldValue: $"Trạng thái: {request.TrangThai}",
            newValue: $"Trạng thái: {STATUS_TU_CHOI}; Người xử lý: {accountId.Value}",
            reason: lyDo));

        await _context.SaveChangesAsync();

        return ProcessingSuccess(
            $"Đã từ chối yêu cầu #{id}. Người gửi có thể chỉnh sửa và gửi lại.",
            await GetStatusOptionsForRefresh(role));
    }

    // =====================================================================
    //  HÀM PHỤ
    // =====================================================================
    private NhatKyQuanTri CreateAuditLog(
        int? targetAccountId,
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

    /// <summary>
    /// Chuẩn hoá tên đăng nhập y hệt <c>UserAccountsController.NormalizeUsername</c>:
    /// cắt khoảng trắng và đưa về chữ thường. Hai màn hình phải cho ra cùng một
    /// giá trị, nếu không cùng một người có thể đăng bằng "NV.An" ở đây và
    /// "nv.an" ở kia mà vẫn không đụng unique index.
    /// </summary>
    private static string NormalizeUsername(string? username)
    {
        return username?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static bool IsKnownStatus(string? status)
    {
        return status is STATUS_CHO_IT or STATUS_DANG_XU_LY or STATUS_DA_CAP or STATUS_TU_CHOI;
    }

    private static IReadOnlyList<SelectListItem> BuildStatusOptions(string? selectedStatus)
    {
        return
        [
            new SelectListItem { Value = string.Empty, Text = "-- Tất cả trạng thái --", Selected = string.IsNullOrWhiteSpace(selectedStatus) },
            new SelectListItem { Value = STATUS_CHO_IT, Text = "Chờ bộ phận IT xử lí", Selected = selectedStatus == STATUS_CHO_IT },
            new SelectListItem { Value = STATUS_DA_CAP, Text = "Đã cấp", Selected = selectedStatus == STATUS_DA_CAP },
            new SelectListItem { Value = STATUS_TU_CHOI, Text = "Từ chối", Selected = selectedStatus == STATUS_TU_CHOI }
        ];
    }

    /// <summary>
    /// Số đếm theo trạng thái trả về client sau khi xử lý xong, để KPI card đếm
    /// lại bằng đúng phép tính của màn hình thay vì tự tính ở JS.
    /// </summary>
    private async Task<object> GetStatusOptionsForRefresh(string? role)
    {
        var canProcess = User.HasPermission(PermissionCodes.ProvisionMobileAccount);
        var query = BuildVisibleQuery(canProcess, GetCurrentEmployeeId());

        return new
        {
            statusOptions = BuildStatusOptions(null),
            totalRequests = await query.CountAsync(),
            pendingRequests = await query.CountAsync(item => item.TrangThai == STATUS_CHO_IT || item.TrangThai == STATUS_DANG_XU_LY),
            grantedRequests = await query.CountAsync(item => item.TrangThai == STATUS_DA_CAP),
            rejectedRequests = await query.CountAsync(item => item.TrangThai == STATUS_TU_CHOI)
        };
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            STATUS_CHO_IT => "Chờ bộ phận IT xử lí",
            STATUS_DANG_XU_LY => "Chờ bộ phận IT xử lí",
            STATUS_DA_CAP => "Đã cấp",
            STATUS_TU_CHOI => "Từ chối",
            _ => status
        };
    }

    private static (string RoleName, string RoleDisplayName) ResolveRoleFromPositionAndDepartment(
        string? positionName,
        string? departmentName)
    {
        var pos = (positionName ?? string.Empty).Trim();
        var dept = (departmentName ?? string.Empty).Trim();

        if (dept.Contains("Ban Giám Đốc", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Giám đốc", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("General Manager", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Director", StringComparison.OrdinalIgnoreCase))
            return ("Admin", GetVietnameseRoleName("Admin"));

        // 1. CNTT / IT
        if (System.Text.RegularExpressions.Regex.IsMatch(dept, @"\bIT\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            dept.Contains("Công nghệ thông tin", StringComparison.OrdinalIgnoreCase) ||
            dept.Contains("CNTT", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(pos, @"\bIT\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            pos.Contains("Công nghệ thông tin", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("CNTT", StringComparison.OrdinalIgnoreCase))
        {
            return ("IT", GetVietnameseRoleName("IT"));
        }

        // 2. Nhân sự (HR) & Kế toán / Tài chính
        if (dept.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(dept, @"\bHR\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            dept.Contains("Kế toán", StringComparison.OrdinalIgnoreCase) ||
            dept.Contains("Tài chính", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(pos, @"\bHR\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            pos.Contains("Kế toán", StringComparison.OrdinalIgnoreCase))
        {
            return ("HR", GetVietnameseRoleName("HR"));
        }

        // 4. Quản lý / Trưởng bộ phận
        if (pos.Contains("Trưởng Bộ Phận", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Trưởng phòng", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Department Head", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Bếp Trưởng", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Quản lý", StringComparison.OrdinalIgnoreCase) ||
            pos.Contains("Manager", StringComparison.OrdinalIgnoreCase))
        {
            return ("Manager", GetVietnameseRoleName("Manager"));
        }

        // 5. Mặc định là Nhân viên (Employee)
        return ("Employee", GetVietnameseRoleName("Employee"));
    }

    private static string GetVietnameseRoleName(string? roleCode)
    {
        return roleCode switch
        {
            "Admin" => "Quản trị viên",
            "HR" => "Nhân sự",
            "Manager" => "Quản lý",
            "Employee" => "Nhân viên",
            "IT" => "Công nghệ thông tin",
            _ => roleCode ?? "Chưa cấp"
        };
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
               Request.GetTypedHeaders().Accept?.Any(value =>
                   string.Equals(value.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private IActionResult ProcessingSuccess(string message, object payload)
    {
        if (IsAjaxRequest())
        {
            return Json(new { success = true, message, data = payload });
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ProcessingFailure(
        string message,
        int statusCode = StatusCodes.Status400BadRequest)
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
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : null;
    }

    private int? GetCurrentEmployeeId()
    {
        return int.TryParse(User.FindFirstValue("EmployeeId"), out var employeeId) ? employeeId : null;
    }

    private string? GetCurrentRoleName()
    {
        return User.FindFirstValue(ClaimTypes.Role);
    }
}
