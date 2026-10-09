using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
/// <para>
/// Cửa ở <c>[Authorize(Policy = ManageAccounts)]</c> đúng như màn hình Quản trị
/// tài khoản: ai quản lý hồ sơ tài khoản thì thấy quy trình. Nhưng người NHÌN và
/// người XỬ LÝ là hai quyền khác nhau, nên từng action gắn thêm policy riêng —
/// xem <see cref="Index"/> thì HR và IT cùng vào được, còn nhấn "Cấp tài khoản"
/// thì chỉ Admin và IT với mã quyền <see cref="PermissionCodes.ProvisionMobileAccount"/>.
/// Đó là cùng ranh giới với <see cref="LeaveApprovalPolicy.CanProcessAccountRequest"/>.
/// </para>
/// </summary>
[Authorize(Policy = PermissionCodes.ManageAccounts)]
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

    /// <summary>
    /// Mật khẩu khởi tạo khi IT bấm "Cấp tài khoản". Không cho người dùng gõ vì
    /// yêu cầu là của HR, mật khẩu là của IT; nhân viên sẽ đổi sau lần đăng nhập
    /// đầu. IT cần đặt lại thì dùng màn hình Quản trị tài khoản.
    /// </summary>
    private const string InitialPassword = "Overtime@2026";

    private readonly AppDbContext _context;
    private readonly ILogger<YeuCauTaiKhoanController> _logger;
    private readonly ISupabaseUserProvisioningService _supabaseUserProvisioning;

    public YeuCauTaiKhoanController(
        AppDbContext context,
        ILogger<YeuCauTaiKhoanController> logger,
        ISupabaseUserProvisioningService supabaseUserProvisioning)
    {
        _context = context;
        _logger = logger;
        _supabaseUserProvisioning = supabaseUserProvisioning;
    }

    // =====================================================================
    //  INDEX — HỘP THƯ CỦA HR VÀ IT
    // =====================================================================
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> Index(string? keyword, string? trangThai, int page = 1)
    {
        keyword = keyword?.Trim();
        trangThai = IsKnownStatus(trangThai) ? trangThai : null;
        page = Math.Max(1, page);

        var employeeId = GetCurrentEmployeeId();
        var role = GetCurrentRoleName();
        var canProcess = LeaveApprovalPolicy.CanProcessAccountRequest(role);

        var query = BuildVisibleQuery(canProcess, employeeId);

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
            query = query.Where(item => item.TrangThai == trangThai);
        }

        var totalFiltered = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)PageSize));
        var pageNumber = Math.Clamp(page, 1, totalPages);

        var requests = await query
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
                ContractNo = item.HopDong == null ? null : item.HopDong.SoHopDong,
                SenderName = item.NguoiGui == null
                    ? "Không xác định"
                    : (item.NguoiGui.Ho + " " + item.NguoiGui.Ten).Trim(),
                HandlerUsername = item.NguoiXuLy == null ? null : item.NguoiXuLy.TenDangNhap,
                RejectionNote = item.GhiChuTuChoi,
                SentAt = item.NgayGui,
                HandledAt = item.NgayXuLy,
                GrantedUsername = item.TaiKhoanDuocCap == null ? null : item.TaiKhoanDuocCap.TenDangNhap
            })
            .ToListAsync();

        // Đếm KPI trên tập đã phân tát quyền, không phải trên toàn bảng — nếu
        // không thì số của HR sẽ phản ánh cả hộp thư của IT.
        var model = new YeuCauTaiKhoanIndexViewModel
        {
            Keyword = keyword,
            TrangThai = trangThai,
            StatusOptions = BuildStatusOptions(trangThai),
            Requests = requests,
            TotalRequests = await query.CountAsync(),
            PendingRequests = await query.CountAsync(item => item.TrangThai == STATUS_CHO_IT),
            ProcessingRequests = await query.CountAsync(item => item.TrangThai == STATUS_DANG_XU_LY),
            GrantedRequests = await query.CountAsync(item => item.TrangThai == STATUS_DA_CAP),
            RejectedRequests = await query.CountAsync(item => item.TrangThai == STATUS_TU_CHOI),
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
            Response.Headers["X-Processing-Requests"] = model.ProcessingRequests.ToString();
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
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> Create()
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanSubmitAccountRequest(role))
        {
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền gửi yêu cầu cấp tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CreateYeuCauTaiKhoanViewModel();
        await LoadCreateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageAccounts)]
    public async Task<IActionResult> Create(CreateYeuCauTaiKhoanViewModel model)
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanSubmitAccountRequest(role))
        {
            TempData["Error"] = "Chỉ Quản trị viên và Nhân sự mới có thẩm quyền gửi yêu cầu cấp tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        model.Username = NormalizeUsername(model.Username);

        var senderEmployeeId = GetCurrentEmployeeId();
        if (!senderEmployeeId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Không xác định được nhân viên đang đăng nhập. Vui lòng đăng nhập lại.");
            await LoadCreateOptionsAsync(model);
            return View(model);
        }

        // Ba kiểm tra này lặp lại đúng điều kiện của LoadCreateOptionsAsync,
        // nhưng phải chạy LẠI lúc POST: người dùng có thể mở form rồi để mở,
        // hoặc gửi thẳng giá trị đã sửa trong devtools. Lọc ở dropdown chỉ cho
        // trải nghiệm đẹp, còn đây mới là chốt chặn.
        var employee = await _context.NhanViens
            .AsNoTracking()
            .Where(item => item.MaNhanVien == model.EmployeeId)
            .Select(item => new { item.MaNhanVien, item.TrangThai, item.EmailCongVu, item.Ho, item.Ten })
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
            // Không có email công vụ thì hook không cấp claim: nhân viên đăng
            // nhập được app nhưng vào trong không có quyền nào. Chặn ngay tại
            // đây để không tạo một yêu cầu chắc chắn sẽ cấp không xong.
            ModelState.AddModelError(nameof(model.EmployeeId), "Nhân viên chưa có email công vụ nên không thể đăng nhập Mobile App.");
        }

        // Tên đăng nhập phải trùng khai báo với yêu cầu đang mở, vì index
        // uq_yeu_cau_ten_dang_nhap_dang_mo khoá đúng hai trạng thái này.
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

        var roleName = await _context.VaiTros
            .AsNoTracking()
            .Where(role => role.MaVaiTro == model.RoleId)
            .Select(role => role.TenVaiTro)
            .SingleOrDefaultAsync();

        if (roleName == null || !SupportedRoles.Contains(roleName))
        {
            ModelState.AddModelError(nameof(model.RoleId), "Vai trò được chọn không hợp lệ.");
        }
        else if (string.Equals(roleName, "Admin", StringComparison.Ordinal) &&
                 !LeaveApprovalPolicy.IsAdmin(GetCurrentRoleName()))
        {
            // Tự ghi yêu cầu cấp tài khoản quản trị là leo quyền: HR có thể
            // gửi yêu cầu, nhưng quyền "gửi" không được kéo theo quyền "tạo Admin".
            ModelState.AddModelError(nameof(model.RoleId), "Chỉ Quản trị viên mới được gán vai trò quản trị cho tài khoản mới.");
        }

        if (model.ContractId.HasValue &&
            !await _context.HopDongs.AnyAsync(contract =>
                contract.MaHopDong == model.ContractId.Value &&
                contract.MaNhanVien == model.EmployeeId &&
                contract.TrangThai == "HIEU_LUC"))
        {
            ModelState.AddModelError(nameof(model.ContractId), "Hợp đồng không hợp lệ với nhân viên đã chọn.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync(model);
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

        _context.YeuCauTaiKhoans.Add(request);
        _context.NhatKyQuanTris.Add(CreateAuditLog(
            targetAccountId: GetCurrentAccountId(),
            targetUsername: model.Username,
            action: "CREATE_ACCOUNT_REQUEST",
            description: "Gửi yêu cầu cấp tài khoản đăng nhập cho nhân viên.",
            oldValue: null,
            newValue: $"Nhân viên #{model.EmployeeId}; Vai trò: {GetVietnameseRoleName(roleName)}; Tên đăng nhập: {model.Username}",
            reason: "Nhân sự duyệt hợp đồng và gửi yêu cầu cấp tài khoản."));

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã gửi yêu cầu cấp tài khoản '{model.Username}'. Bộ phận Công nghệ Thông tin sẽ xử lý.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Nạp hai danh sách cho form gửi yêu cầu.
    ///
    /// <para>
    /// Điều kiện lọc nhân viên CỐ TÝ giống hệt trigger <c>chan_yeu_cau_tai_khoan_trung</c>
    /// ở CSDL: chưa có tài khoản, đang làm việc, và có email công vụ. Ba điều kiện
    /// đó chính là thứ trigger chặn — lọt một điều kiện nào thì người dùng thấy
    /// lỗi 23000 của PostgreSQL thay vì thông báo tiếng Việt. Lọc ở tầng Web để
    /// không tạo ra trải nghiệm lỗi kỹ thuật; trigger vẫn giữ làm lướp an toàn
    /// cho mọi đường ghi khác.
    /// </para>
    /// </summary>
    private async Task LoadCreateOptionsAsync(CreateYeuCauTaiKhoanViewModel model)
    {
        model.EmployeeOptions = await _context.NhanViens
            .AsNoTracking()
            .Where(employee =>
                employee.TrangThai == "DANG_LAM" &&
                employee.EmailCongVu != null &&
                employee.EmailCongVu != string.Empty &&
                !_context.TaiKhoans.Any(account => account.MaNhanVien == employee.MaNhanVien))
            .OrderBy(employee => employee.Ho)
            .ThenBy(employee => employee.Ten)
            .Select(employee => new SelectListItem
            {
                Value = employee.MaNhanVien.ToString(),
                Text = (employee.Ho + " " + employee.Ten).Trim() + " · " + employee.EmailCongVu,
                Selected = employee.MaNhanVien == model.EmployeeId
            })
            .ToListAsync();

        // Admin chỉ hiện trong danh sách khi chính người gửi là Admin — xem
        // kiểm tra ở action Create.
        var senderIsAdmin = LeaveApprovalPolicy.IsAdmin(GetCurrentRoleName());
        var roleNames = SupportedRoles
            .Where(roleName => senderIsAdmin || !string.Equals(roleName, "Admin", StringComparison.Ordinal))
            .ToArray();

        model.RoleOptions = await _context.VaiTros
            .AsNoTracking()
            .Where(role => roleNames.Contains(role.TenVaiTro))
            .OrderBy(role => role.MaVaiTro)
            .Select(role => new SelectListItem
            {
                Value = role.MaVaiTro.ToString(),
                Text = role.TenVaiTro,
                Selected = role.MaVaiTro == model.RoleId
            })
            .ToListAsync();

        foreach (var roleOption in model.RoleOptions)
        {
            roleOption.Text = GetVietnameseRoleName(roleOption.Text);
        }

        // Mặc định Employee: đây là vai trò phổ biến nhất và cũng là vai trò
        // ít quyền nhất, nên "quên chọn" cũng không dẫn tới leo quyền.
        if (model.RoleId == 0)
        {
            var defaultRole = model.RoleOptions.FirstOrDefault(option => option.Text == GetVietnameseRoleName("Employee"));
            model.RoleId = defaultRole == null ? 0 : int.Parse(defaultRole.Value!);
        }

        // Chỉ hợp đồng HIỆU_LỰC mới là căn cứ hợp lệ — hợp đồng đã hết hạn thì
        // nhân viên không còn đủ tư cách đi làm, cấp tài khoản cho họ là cấp
        // quyền vào hệ thống không có việc làm.
        model.ActiveContracts = await _context.HopDongs
            .AsNoTracking()
            .Where(contract => contract.TrangThai == "HIEU_LUC")
            .OrderBy(contract => contract.NgayKetThuc ?? DateOnly.MaxValue)
            .ThenBy(contract => contract.SoHopDong)
            .Select(contract => new ActiveContractOptionViewModel
            {
                ContractId = contract.MaHopDong,
                EmployeeId = contract.MaNhanVien,
                Label = contract.SoHopDong + " · " +
                        (contract.LoaiHopDong ?? string.Empty) + " · " +
                        (contract.NgayKetThuc == null
                            ? "không xác định thời hạn"
                            : "đến " + contract.NgayKetThuc.Value.ToString("dd/MM/yyyy"))
            })
            .ToListAsync();
    }

    // =====================================================================
    //  CLAIM — IT NHẬN VIỆC
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public async Task<IActionResult> Claim(int id)
    {
        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return ProcessingFailure("Không xác định được tài khoản đang đăng nhập.", StatusCodes.Status401Unauthorized);
        }

        // Optimistic concurrency: điều kiện trạng thái nằm TRONG chính câu
        // UPDATE chứ không kiểm tra rồi mới ghi — nếu không thì hai người cùng
        // bấm "Nhận việc" thì cả hai đều đọc được CHO_IT và cả hai đều ghi
        // thành công, kết quả là yêu cầu bị hai tài khoản cùng xử lý.
        var affected = await _context.YeuCauTaiKhoans
            .Where(item => item.MaYeuCau == id && item.TrangThai == STATUS_CHO_IT)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.TrangThai, STATUS_DANG_XU_LY)
                .SetProperty(item => item.MaNguoiXuLy, accountId.Value)
                .SetProperty(item => item.NgayXuLy, DateTimeOffset.UtcNow));

        if (affected == 0)
        {
            _logger.LogWarning("Xung đột nhận việc yêu cầu #{RequestId}: yêu cầu đã chuyển trạng thái.", id);
            return ProcessingFailure(
                "Yêu cầu này vừa được người khác nhận hoặc đã chuyển trạng thái. Vui lòng tải lại danh sách.",
                StatusCodes.Status409Conflict);
        }

        _context.NhatKyQuanTris.Add(CreateAuditLog(
            accountId,
            $"YEU_CAU_#{id}",
            "CLAIM_ACCOUNT_REQUEST",
            "Bộ phận CNTT nhận xử lý yêu cầu cấp tài khoản.",
            oldValue: $"Trạng thái: {STATUS_CHO_IT}",
            newValue: $"Trạng thái: {STATUS_DANG_XU_LY}",
            reason: "Nhận việc để cấp tài khoản đăng nhập Mobile App."));

        await _context.SaveChangesAsync();

        return ProcessingSuccess(
            $"Đã nhận yêu cầu #{id}. Bạn có thể cấp tài khoản hoặc từ chối.",
            await GetStatusOptionsForRefresh(GetCurrentRoleName()));
    }

    // =====================================================================
    //  COMPLETE — IT CẤP TÀI KHOẢN
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public async Task<IActionResult> Complete(int id)
    {
        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return ProcessingFailure("Không xác định được tài khoản đang đăng nhập.", StatusCodes.Status401Unauthorized);
        }

        var request = await _context.YeuCauTaiKhoans
            .AsNoTracking()
            .Include(item => item.NhanVien)
            .FirstOrDefaultAsync(item => item.MaYeuCau == id);

        if (request == null)
        {
            return ProcessingFailure("Không tìm thấy yêu cầu cần cấp tài khoản.", StatusCodes.Status404NotFound);
        }

        // Chỉ nhận việc mới cấp được: nếu chỉ chặn ở CSDL thì người chưa nhận
        // việc vẫn tạo được tài khoản, và ma_nguoi_xu_ly bị ghi đè mất người
        // đang xử lý thật — hậu quả là không truy được ai đã cấp.
        if (!string.Equals(request.TrangThai, STATUS_DANG_XU_LY, StringComparison.Ordinal))
        {
            return ProcessingFailure(
                "Chỉ cấp được tài khoản cho yêu cầu đã nhận việc (trạng thái Đang xử lý).",
                StatusCodes.Status409Conflict);
        }

        // Email công vụ là điều kiện tiên quyết: custom_access_token_hook đối
        // chiếu claims.email với nhan_vien.email mới cấp claim vai trò. Không
        // có email thì nhân viên đăng nhập được app nhưng vào trong không có
        // quyền nào — đúng cái bẫy đã gặp thật ở module tài khoản.
        var workEmail = request.NhanVien?.EmailCongVu;
        if (string.IsNullOrWhiteSpace(workEmail))
        {
            return ProcessingFailure(
                "Nhân viên này chưa có email công vụ nên không thể đăng nhập Mobile App. Cần bổ sung email công vụ trước khi cấp.",
                StatusCodes.Status400BadRequest);
        }

        var username = request.TenDangNhap;
        var account = new TaiKhoan
        {
            MaNhanVien = request.MaNhanVien,
            MaVaiTro = request.MaVaiTro,
            TenDangNhap = username,
            MatKhauBam = PasswordHasher.Hash(InitialPassword),
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
                    accountId,
                    username,
                    "CREATE_ACCOUNT",
                    "Tạo tài khoản từ yêu cầu cấp tài khoản do Nhân sự gửi.",
                    oldValue: $"Yêu cầu #{request.MaYeuCau} · trạng thái {STATUS_DANG_XU_LY}",
                    newValue: $"Tài khoản #{account.MaTaiKhoan} · trạng thái Hoạt động",
                    reason: "Bộ phận CNTT cấp tài khoản theo yêu cầu đã duyệt."));

                await _context.SaveChangesAsync();

                // Chuyển yêu cầu sang DA_CAP chỉ khi vẫn còn DANG_XU_LY — cùng
                // cơ chế optimistic concurrency như Claim: giữa lúc đọc và
                // lúc ghi có thể xảy ra thao tác khác.
                var completed = await _context.YeuCauTaiKhoans
                    .Where(item => item.MaYeuCau == id && item.TrangThai == STATUS_DANG_XU_LY)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.TrangThai, STATUS_DA_CAP)
                        .SetProperty(item => item.MaTaiKhoan, account.MaTaiKhoan)
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
            // Bỏ dòng tai_khoan đã Add vào change tracker: giữ lại thì lần
            // SaveChanges kế tiếp sẽ thử ghi lại nó và chết theo.
            _context.Entry(account).State = EntityState.Detached;
            _logger.LogError(ex, "Không thể tạo tài khoản cho yêu cầu #{RequestId}.", id);
            return ProcessingFailure(
                "Không thể tạo tài khoản do dữ liệu bị trùng hoặc kết nối CSDL gặp lỗi.",
                StatusCodes.Status500InternalServerError);
        }

        // Cấp user Supabase Auth SAU KHI transaction đã commit. Gọi HTTP bên
        // trong transaction PostgreSQL đang mở là sai: giữ connection lâu, và
        // nếu HTTP thành công nhưng commit hỏng thì tài khoản app đã có mà
        // tài khoản CSDL thì không — đúng trạng thái "đăng được app nhưng không
        // có quyền nào" mà kế hoạch đã cảnh báo.
        var provisionResult = await _supabaseUserProvisioning.CreateUserAsync(
            workEmail,
            InitialPassword,
            HttpContext.RequestAborted);

        if (!provisionResult.Success)
        {
            _logger.LogWarning(
                "Cấp tài khoản Supabase thất bại cho yêu cầu #{RequestId} ({Username}): {ErrorCode}",
                id,
                username,
                provisionResult.ErrorCode);

            await RollBackGrantedAccountAsync(account, id);

            return ProcessingFailure(
                provisionResult.ErrorMessage
                ?? "Không tạo được tài khoản đăng nhập trên Mobile App.",
                StatusCodes.Status502BadGateway);
        }

        _context.NhatKyQuanTris.Add(CreateAuditLog(
            accountId,
            username,
            "CREATE_ACCOUNT_AUTH",
            "Cấp tài khoản Supabase Auth để nhân viên đăng nhập Mobile App bằng email công vụ.",
            oldValue: null,
            newValue: $"Email công vụ: {workEmail}; Supabase User ID: {provisionResult.SupabaseUserId}",
            reason: "Hoàn tất bước cấp quyền đăng nhập cho yêu cầu đã nhận việc."));

        await _context.SaveChangesAsync();

        return ProcessingSuccess(
            $"Đã cấp tài khoản '{username}' và quyền đăng nhập Mobile App bằng email công vụ '{workEmail}'.",
            await GetStatusOptionsForRefresh(GetCurrentRoleName()));
    }

    /// <summary>
    /// Gỡ dòng <c>tai_khoan</c> vừa tạo khi cấp user Supabase Auth thất bại, và
    /// trả yêu cầu về trạng thái <c>DANG_XU_LY</c> kèm lý do HTTP.
    ///
    /// <para>
    /// <b>Vì sao KHÔNG xoá yêu cầu.</b> Yêu cầu đã tồn tại từ trước, do Nhân
    /// sự gửi và là dấu vết tiến trình mà CSDL phải truy được. Xoá nó là mất
    /// dấu vết ai đã gửi, và nhân viên sẽ không bao giờ biết vì sao mình chưa
    /// được cấp tài khoản. Giữ nguyên yêu cầu ở DANG_XU_LY còn đúng nghĩa
    /// "IT đang xử lý, nhưng chưa xong" — và người xử lý thấy ngay lý do để làm
    /// lại.
    /// </para>
    ///
    /// <para>
    /// <b>Vì sao gỡ dòng tai_khoan.</b> Trigger <c>chan_yeu_cau_tai_khoan_trung</c>
    /// chặn đúng cái trạng thái để lại: một dòng <c>tai_khoan</c> mà không có
    /// user trong <c>auth.users</c> là tài khoản đăng nhập được Web nhưng không
    /// đăng nhập được app, và lần sau gửi lại sẽ bị trigger chặn với thông báo
    /// "đã có tài khoản" — nhân viên không tự khắc phục được.
    /// </para>
    /// </summary>
    private async Task RollBackGrantedAccountAsync(TaiKhoan account, int requestId)
    {
        var failureNote = "Cấp tài khoản Supabase Auth thất bại. Yêu cầu được giữ ở trạng thái Đang xử lý để bộ phận CNTT thử lại.";

        // Trả yêu cầu về DANG_XU_LY TRƯỚC, để nó không còn giữ ma_tai_khoan
        // trỏ tới tài khoản sắp bị xoá — nếu làm ngược thì CHECK
        // chk_yeu_cau_da_cap_can_co_tai_khoan và ON DELETE SET NULL của cột
        // ma_tai_khoan sẽ kéo trạng thái về một giá trị không hợp lệ.
        // Ngày xử lý đặt lại null vì yêu cầu chưa kết thúc; thời điểm thất bại
        // nằm ở nhật ký quản trị chứ không nằm ở cột này.
        await _context.YeuCauTaiKhoans
            .Where(item => item.MaYeuCau == requestId && item.MaTaiKhoan == account.MaTaiKhoan)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.TrangThai, STATUS_DANG_XU_LY)
                .SetProperty(item => item.MaTaiKhoan, (int?)null)
                .SetProperty(item => item.GhiChuTuChoi, failureNote)
                .SetProperty(item => item.NgayXuLy, (DateTimeOffset?)null));

        _context.NhatKyQuanTris.RemoveRange(
            _context.NhatKyQuanTris.Where(log => log.MaTaiKhoanBiTacDong == account.MaTaiKhoan));
        _context.TaiKhoans.Remove(account);
        await _context.SaveChangesAsync();
    }

    // =====================================================================
    //  REJECT — IT TỪ CHỐI
    // =====================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ProvisionMobileAccount)]
    public async Task<IActionResult> Reject(int id, string? lyDo)
    {
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

        // Cột ma_nguoi_xu_ly CỐ Ý không được chạm vào ở đây: nó đã được Claim
        // ghi khi nhận việc, và CHECK chk_yeu_cau_da_xu_ly_can_co_nguoi bảo đảm
        // nó luôn khác NULL ở mọi trạng thái ngoài CHO_IT. Ghi đè lại chỉ để
        // "ghi cho có" mà là mất dấu vết người thực sự nhận việc.
        var affected = await _context.YeuCauTaiKhoans
            .Where(item => item.MaYeuCau == id && item.TrangThai == STATUS_DANG_XU_LY)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.TrangThai, STATUS_TU_CHOI)
                .SetProperty(item => item.GhiChuTuChoi, lyDo)
                .SetProperty(item => item.NgayXuLy, DateTimeOffset.UtcNow));

        if (affected == 0)
        {
            _logger.LogWarning("Xung đột từ chối yêu cầu #{RequestId}: yêu cầu không còn ở trạng thái Đang xử lý.", id);
            return ProcessingFailure(
                "Yêu cầu này không còn ở trạng thái Đang xử lý. Vui lòng tải lại danh sách.",
                StatusCodes.Status409Conflict);
        }

        _context.NhatKyQuanTris.Add(CreateAuditLog(
            accountId,
            $"YEU_CAU_#{id}",
            "REJECT_ACCOUNT_REQUEST",
            "Bộ phận CNTT từ chối cấp tài khoản đăng nhập.",
            oldValue: $"Trạng thái: {STATUS_DANG_XU_LY}",
            newValue: $"Trạng thái: {STATUS_TU_CHOI}",
            reason: lyDo));

        await _context.SaveChangesAsync();

        return ProcessingSuccess(
            $"Đã từ chối yêu cầu #{id}. Nhân sự có thể sửa và gửi lại.",
            await GetStatusOptionsForRefresh(GetCurrentRoleName()));
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
            new SelectListItem { Value = string.Empty, Text = "Tất cả trạng thái", Selected = string.IsNullOrWhiteSpace(selectedStatus) },
            new SelectListItem { Value = STATUS_CHO_IT, Text = "Chờ CNTT", Selected = selectedStatus == STATUS_CHO_IT },
            new SelectListItem { Value = STATUS_DANG_XU_LY, Text = "Đang xử lý", Selected = selectedStatus == STATUS_DANG_XU_LY },
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
        var canProcess = LeaveApprovalPolicy.CanProcessAccountRequest(role);
        var query = BuildVisibleQuery(canProcess, GetCurrentEmployeeId());

        return new
        {
            statusOptions = BuildStatusOptions(null),
            totalRequests = await query.CountAsync(),
            pendingRequests = await query.CountAsync(item => item.TrangThai == STATUS_CHO_IT),
            processingRequests = await query.CountAsync(item => item.TrangThai == STATUS_DANG_XU_LY),
            grantedRequests = await query.CountAsync(item => item.TrangThai == STATUS_DA_CAP),
            rejectedRequests = await query.CountAsync(item => item.TrangThai == STATUS_TU_CHOI)
        };
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            STATUS_CHO_IT => "Chờ CNTT",
            STATUS_DANG_XU_LY => "Đang xử lý",
            STATUS_DA_CAP => "Đã cấp",
            STATUS_TU_CHOI => "Từ chối",
            _ => status
        };
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