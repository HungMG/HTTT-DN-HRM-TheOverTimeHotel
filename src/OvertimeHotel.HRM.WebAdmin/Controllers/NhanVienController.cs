using OvertimeHotel.HRM.WebAdmin.Services;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.ManageEmployees)]
public class NhanVienController : Controller
{
    private static readonly string[] SupportedEmployeeStatuses = ["DANG_LAM", "DA_THOI_VIEC", "NGHI_PHEP", "CHO_HOP_DONG"];

    private readonly AppDbContext _context;
    private readonly ILogger<NhanVienController> _logger;

    public NhanVienController(AppDbContext context, ILogger<NhanVienController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, int? departmentId, int? positionId, string? status, string? contractFilter, int page = 1, int pageSize = 10, bool isPartial = false)
    {
        keyword = keyword?.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status;
        contractFilter = string.IsNullOrWhiteSpace(contractFilter) ? null : contractFilter;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        if (departmentId is not > 0) positionId = null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        try
        {
            // Thực thi tuần tự trên cùng DbContext instance để đảm bảo an toàn đơn luồng (Thread-safe)
            var depts = await this.SearchDataAsync("employees:departments", () => _context.PhongBans.AsNoTracking().OrderBy(item => item.TenPhongBan).ToListAsync());
            var poss = await this.SearchDataAsync("employees:positions", () => _context.ChucVus.AsNoTracking().OrderBy(item => item.TenChucVu).ToListAsync());
            var allEmployees = await this.SearchDataAsync("employees:list", () => _context.NhanViens.AsNoTracking().AsSplitQuery().Where(employee => employee.IsProfileComplete)
                .Include(employee => employee.PhongBan)
                .Include(employee => employee.ChucVu)
                .Include(employee => employee.HopDongs)
                .Include(employee => employee.TaiKhoan)
                    .ThenInclude(t => t!.VaiTro)
                .Include(employee => employee.YeuCauTaiKhoanCuaNhanVien.OrderByDescending(request => request.NgayGui)
                    .ThenByDescending(request => request.MaYeuCau).Take(1))
                .ToListAsync());

            // Lọc trên bản sao danh sách trong cache, không truy vấn lại theo keyword.
            var filtered = allEmployees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var value = keyword.ToLower();
                filtered = filtered.Where(employee =>
                    employee.Ho.ToLower().Contains(value) ||
                    employee.Ten.ToLower().Contains(value) ||
                    employee.EmailCongVu.ToLower().Contains(value) ||
                    (employee.EmailCaNhan != null && employee.EmailCaNhan.ToLower().Contains(value)) ||
                    employee.DienThoai.Contains(value));
            }

            if (departmentId is > 0)
            {
                filtered = filtered.Where(employee => employee.MaPhongBan == departmentId.Value);

                if (positionId is > 0)
                    filtered = filtered.Where(employee => employee.MaChucVu == positionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
                filtered = filtered.Where(employee => employee.TrangThai == status);
            else
                filtered = filtered.Where(employee => employee.TrangThai != "DA_THOI_VIEC");

            var items = filtered.Select(employee =>
            {
                var contract = employee.HopDongs
                    .OrderByDescending(item => item.NgayBatDau)
                    .FirstOrDefault();
                var days = contract?.NgayKetThuc is DateOnly endDate
                    ? endDate.DayNumber - today.DayNumber
                    : (int?)null;
                var state = GetContractState(contract, days);
                return new EmployeeListItemViewModel
                {
                    Employee = employee,
                    LatestContract = contract,
                    ContractDaysRemaining = days,
                    ContractState = state
                };
            });

            if (contractFilter == "SAP_HET_HAN")
                items = items.Where(item => item.ContractDaysRemaining is >= 0 and <= 30);
            else if (contractFilter == "DA_HET_HAN")
                items = items.Where(item => item.ContractDaysRemaining < 0);
            else if (contractFilter == "HIEU_LUC")
                items = items.Where(item => item.ContractState == "HIEU_LUC");

            var result = items.OrderBy(item => item.Employee.Ten).ThenBy(item => item.Employee.Ho).ToList();
            var totalFiltered = result.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
            var pageNumber = Math.Clamp(page, 1, totalPages);
            var pagedEmployees = result.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var allContractItems = allEmployees.Select(employee =>
            {
                var contract = employee.HopDongs.OrderByDescending(item => item.NgayBatDau).FirstOrDefault();
                var days = contract?.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null;
                return (contract, days);
            }).ToList();

            var deptOptions = depts.Select(item => new SelectListItem(item.TenPhongBan, item.MaPhongBan.ToString(), item.MaPhongBan == departmentId)).ToList();
            var posOptions = departmentId is > 0
                ? poss.Where(OrganizationPositionPolicy.ForDepartment(depts.FirstOrDefault(d => d.MaPhongBan == departmentId)).Compile())
                      .Select(item => new SelectListItem(item.TenChucVu, item.MaChucVu.ToString(), item.MaChucVu == positionId))
                      .ToList()
                : new List<SelectListItem>();

            var model = new NhanVienIndexViewModel
            {
                CreateEmployeeForm = new NhanVienFormViewModel
                {
                    Employee = new NhanVien
                    {
                        NgayVaoLam = DateOnly.FromDateTime(DateTime.Today),
                        TrangThai = "DANG_LAM"
                    },
                    EligibleContracts = await GetEligibleContractsAsync(),
                    DepartmentOptions = deptOptions,
                    PositionOptions = poss.Select(item => new SelectListItem(item.TenChucVu, item.MaChucVu.ToString())).ToList()
                },
                Keyword = keyword,
                DepartmentId = departmentId,
                PositionId = positionId,
                Status = status,
                ContractFilter = contractFilter,
                DepartmentOptions = deptOptions,
                PositionOptions = posOptions,
                Employees = pagedEmployees,
                TotalEmployees = allEmployees.Count,
                TotalFilteredEmployees = totalFiltered,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                WorkingEmployees = allEmployees.Count(employee => ContractAttendancePolicy.HasValidContractOn(employee, today)),
                ExpiringContracts = allContractItems.Count(item => item.days is >= 0 and <= 30),
                ExpiredContracts = allContractItems.Count(item => item.days < 0)
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_EmployeeTablePartial", model);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy vấn danh sách nhân viên từ CSDL Supabase.");
            TempData["Error"] = "Lỗi kết nối CSDL Supabase: " + ex.Message;
            var errorModel = new NhanVienIndexViewModel
            {
                Keyword = keyword,
                DepartmentId = departmentId,
                PositionId = positionId,
                Status = status,
                ContractFilter = contractFilter,
                DepartmentOptions = new List<SelectListItem>(),
                PositionOptions = new List<SelectListItem>(),
                Employees = new List<EmployeeListItemViewModel>(),
                CreateEmployeeForm = new NhanVienFormViewModel
                {
                    Employee = new NhanVien
                    {
                        NgayVaoLam = DateOnly.FromDateTime(DateTime.Today),
                        TrangThai = "DANG_LAM"
                    }
                }
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_EmployeeTablePartial", errorModel);
            }

            return View(errorModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(string entryMode = "FROM_CONTRACT", int? contractId = null)
    {
        if (entryMode != "FROM_CONTRACT") return BadRequest("Vui lòng chọn hợp đồng đã lập để hoàn thiện hồ sơ.");
        var model = new NhanVienFormViewModel { EntryMode = entryMode, ContractId = contractId };
        model.EligibleContracts = await GetEligibleContractsAsync();
        if (contractId.HasValue)
        {
            var selected = model.EligibleContracts.FirstOrDefault(x => x.MaHopDong == contractId);
            if (selected?.NhanVien is { } person)
                model.Employee = new NhanVien { Ho = person.Ho, Ten = person.Ten, MaPhongBan = person.MaPhongBan,
                    MaChucVu = person.MaChucVu, EmailCaNhan = person.EmailCaNhan, TrinhDo = person.TrinhDo, NgayVaoLam = person.NgayVaoLam };
        }
        model = await BuildEmployeeFormAsync(model);
        if (Request.Headers["X-Employee-Modal"] == "1") return PartialView("_EmployeeForm", model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NhanVienFormViewModel model)
    {
        FormInputMessages.LocalizeBindingErrors(ModelState);
        if (model.EntryMode != "FROM_CONTRACT") return BadRequest("Vui lòng chọn hợp đồng đã lập để hoàn thiện hồ sơ.");
        var contract = await _context.HopDongs.Include(x => x.NhanVien)
            .FirstOrDefaultAsync(x => x.MaHopDong == model.ContractId);
        var employee = contract?.NhanVien;
        // These fields belong to the selected contract, never to the submitted form.
        model.Employee.Ho = employee?.Ho ?? string.Empty;
        model.Employee.Ten = employee?.Ten ?? string.Empty;
        model.Employee.MaPhongBan = employee?.MaPhongBan;
        model.Employee.MaChucVu = employee?.MaChucVu;
        model.Employee.EmailCaNhan = string.IsNullOrWhiteSpace(model.Employee.EmailCaNhan) ? employee?.EmailCaNhan : model.Employee.EmailCaNhan;
        model.Employee.NgayVaoLam = employee?.NgayVaoLam;
        if (!string.IsNullOrWhiteSpace(employee?.TrinhDo) && employee.TrinhDo != "Chưa cập nhật")
            model.Employee.TrinhDo = employee.TrinhDo;
        foreach (var field in new[] { "Ho", "Ten", "MaPhongBan", "MaChucVu", "EmailCaNhan", "NgayVaoLam", "TrinhDo" })
            ModelState.Remove($"Employee.{field}");

        model.Employee.TrangThai = "DANG_LAM";
        model.Employee.NgayThoiViec = null;
        Normalize(model.Employee);
        NormalizeEmployeeStatus(model.Employee);

        // Bỏ qua lỗi bắt buộc mặc định của ModelBinder với EmailCongVu vì trường này không còn trên form thêm mới
        ModelState.Remove("Employee.EmailCongVu");
        ModelState.Remove("Employee.NgayThoiViec");

        if (employee == null || employee.IsProfileComplete || ContractState.Classify(contract, DateOnly.FromDateTime(DateTime.Today)) is "DA_CHAM_DUT" or "DA_HET_HAN")
            ModelState.AddModelError("ContractId", "Vui lòng chọn hợp đồng chưa có hồ sơ hoàn chỉnh và còn phù hợp để tiếp nhận.");
        model.Employee.MaNhanVien = employee?.MaNhanVien ?? 0;
        model.Employee.EmailCongVu = employee?.EmailCongVu ?? string.Empty;
        await ValidateEmployeeAsync(model.Employee, employee?.MaNhanVien);
        if (!ModelState.IsValid)
        {
            return await InvalidCompletionFormAsync(model);
        }

        try
        {
            employee!.NgaySinh = model.Employee.NgaySinh;
            employee.GioiTinh = model.Employee.GioiTinh;
            employee.DienThoai = model.Employee.DienThoai;
            employee.DiaChi = model.Employee.DiaChi;
            employee.EmailCaNhan = model.Employee.EmailCaNhan;
            employee.TrinhDo = model.Employee.TrinhDo;
            // Preserve original joining date across subsequent contracts.
            employee.SoNguoiPhuThuoc = model.Employee.SoNguoiPhuThuoc;
            employee.IsProfileComplete = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm hồ sơ nhân viên “{model.Employee.HoTen}”.";
            if (Request.Headers["X-Employee-Modal"] == "1") return Json(new { success = true });
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo hồ sơ nhân viên {Email}.", model.Employee.EmailCongVu);
            ModelState.AddModelError(string.Empty, "Không thể hoàn thiện hồ sơ. Hợp đồng có thể đã được sử dụng; vui lòng kiểm tra lại.");
            return await InvalidCompletionFormAsync(model);
        }
    }

    private async Task<IActionResult> InvalidCompletionFormAsync(NhanVienFormViewModel model)
    {
        model.Employee.MaNhanVien = 0;
        ModelState.Remove("Employee.MaNhanVien");
        model.EligibleContracts = await GetEligibleContractsAsync();
        model = await BuildEmployeeFormAsync(model);
        if (Request.Headers["X-Employee-Modal"] == "1")
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_EmployeeForm", model);
        }
        return View("Create", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _context.NhanViens.AsNoTracking().FirstOrDefaultAsync(item => item.MaNhanVien == id);
        return employee == null || !employee.IsProfileComplete ? NotFound() : View(await BuildEmployeeFormAsync(new NhanVienFormViewModel { Employee = employee }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NhanVienFormViewModel model)
    {
        FormInputMessages.LocalizeBindingErrors(ModelState);
        if (id != model.Employee.MaNhanVien)
            return BadRequest();
        if (!await _context.NhanViens.AnyAsync(x => x.MaNhanVien == id && x.IsProfileComplete)) return NotFound();

        // Keep the original joining date tracked throughout validation; a concurrent first signature must reject this edit.
        var original = await _context.NhanViens.FirstAsync(x => x.MaNhanVien == id);
        var awaitingContract = original.TrangThai == "CHO_HOP_DONG" && !await _context.HopDongs.AnyAsync(x => x.MaNhanVien == id);
        if (awaitingContract)
        {
            model.Employee.TrangThai = "CHO_HOP_DONG";
            model.Employee.NgayVaoLam = null;
            foreach (var field in new[] { "TrangThai", "NgayVaoLam" }) ModelState.Remove($"Employee.{field}");
        }
        else if (model.Employee.TrangThai == "CHO_HOP_DONG")
            ModelState.AddModelError("Employee.TrangThai", "Nhân viên đã có quá trình làm việc không thể chuyển về hồ sơ chờ ký.");
        Normalize(model.Employee);
        NormalizeEmployeeStatus(model.Employee);

        if (model.Employee.TrangThai != "DA_THOI_VIEC")
        {
            model.Employee.NgayThoiViec = null;
        }

        ModelState.Remove("Employee.EmailCongVu");

        // Nếu form không gửi Email công vụ (do readonly), nạp lại email công vụ hiện tại
        if (string.IsNullOrWhiteSpace(model.Employee.EmailCongVu))
        {
            var existingEmail = await _context.NhanViens.Where(e => e.MaNhanVien == id).Select(e => e.EmailCongVu).FirstOrDefaultAsync();
            model.Employee.EmailCongVu = existingEmail ?? await GenerateUniqueWorkEmailAsync(model.Employee.Ho, model.Employee.Ten);
        }

        await ValidateEmployeeAsync(model.Employee, id);
        if (!ModelState.IsValid)
            return View(await BuildEmployeeFormAsync(model));

        var employee = await _context.NhanViens.FindAsync(id);
        if (employee == null)
            return NotFound();

        if (!awaitingContract && (employee.MaPhongBan != model.Employee.MaPhongBan || employee.MaChucVu != model.Employee.MaChucVu || employee.NgayVaoLam != model.Employee.NgayVaoLam))
        {
            ModelState.AddModelError(string.Empty, "Đổi chức vụ/phòng ban qua phụ lục hợp đồng; ngày vào làm gốc được giữ để tính thâm niên.");
            return View(await BuildEmployeeFormAsync(model));
        }
        employee.MaPhongBan = model.Employee.MaPhongBan;
        employee.MaChucVu = model.Employee.MaChucVu;
        employee.Ho = model.Employee.Ho;
        employee.Ten = model.Employee.Ten;
        employee.NgaySinh = model.Employee.NgaySinh;
        employee.GioiTinh = model.Employee.GioiTinh;
        employee.DienThoai = model.Employee.DienThoai;
        if (!string.IsNullOrWhiteSpace(model.Employee.EmailCongVu))
        {
            employee.EmailCongVu = model.Employee.EmailCongVu;
        }
        employee.EmailCaNhan = model.Employee.EmailCaNhan;
        employee.DiaChi = model.Employee.DiaChi;
        employee.TrinhDo = model.Employee.TrinhDo;
        employee.NgayVaoLam = model.Employee.NgayVaoLam;
        employee.NgayThoiViec = model.Employee.NgayThoiViec;
        employee.TrangThai = model.Employee.TrangThai;
        employee.SoNguoiPhuThuoc = model.Employee.SoNguoiPhuThuoc;

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật hồ sơ “{employee.HoTen}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Hồ sơ nhân viên {EmployeeId} vừa được cập nhật hoặc ký hợp đồng.", id);
            _context.ChangeTracker.Clear();
            ModelState.AddModelError(string.Empty, "Hồ sơ vừa được cập nhật hoặc ký hợp đồng ở phiên khác. Vui lòng tải lại hồ sơ trước khi sửa tiếp.");
            return View(await BuildEmployeeFormAsync(model));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật hồ sơ nhân viên {EmployeeId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật hồ sơ. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(await BuildEmployeeFormAsync(model));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        IActionResult Failure(string message, int status, string? filter = null)
        {
            if (isAjax) return new JsonResult(new { success = false, message }) { StatusCode = status };
            TempData["Error"] = message;
            return RedirectToAction(nameof(Index), new { status = filter });
        }
        var employee = await _context.NhanViens
            .Include(item => item.TaiKhoan)
            .Include(item => item.PhieuLuongs)
            .FirstOrDefaultAsync(item => item.MaNhanVien == id);
        if (employee == null)
            return isAjax ? new JsonResult(new { success = false, message = "Không tìm thấy nhân viên." }) { StatusCode = 404 } : NotFound();

        if (employee.TrangThai == "CHO_HOP_DONG")
        {
            return Failure("Hồ sơ chờ ký chưa có quá trình làm việc để thôi việc. Dùng nút Xóa vĩnh viễn để kiểm tra và xác nhận xóa hồ sơ nếu không tiếp nhận nữa.", 409, "CHO_HOP_DONG");
        }
        if (employee.TrangThai == "DA_THOI_VIEC")
        {
            return Failure("Hồ sơ đã nghỉ việc. Dùng nút Xóa vĩnh viễn để kiểm tra dữ liệu liên quan.", 409, "DA_THOI_VIEC");
        }

        try
        {
            employee.TrangThai = "DA_THOI_VIEC";
            employee.NgayThoiViec = DateOnly.FromDateTime(DateTime.Today);
            await _context.SaveChangesAsync();
            var message = $"Đã ẩn hồ sơ nhân viên “{employee.HoTen}”. Có thể xem lại trong bộ lọc “Đã nghỉ việc”.";
            if (isAjax) return Json(new { success = true, message });
            TempData["Success"] = message;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể ẩn hồ sơ nhân viên {EmployeeId}.", id);
            return Failure("Không thể ẩn hồ sơ nhân viên. Vui lòng kiểm tra kết nối CSDL.", 409);
        }

        return RedirectToAction(nameof(Index));
    }

    private EmployeeDeletionService DeletionService => new(_context,
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>());

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DeletePreview(int id)
    {
        try
        {
            var preview = await DeletionService.PreviewAsync(id);
            return preview == null ? NotFound(new { message = "Hồ sơ nhân viên không còn tồn tại." }) : Json(preview);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
        {
            _logger.LogError(ex, "Không thể kiểm tra xóa nhân viên {EmployeeId}.", id);
            return StatusCode(503, new { message = "Không thể kiểm tra dữ liệu liên quan. Vui lòng thử lại." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePermanently(int id, string? confirmationToken, bool confirmed = false)
    {
        try
        {
            var result = await DeletionService.DeleteAsync(id, confirmationToken, confirmed);
            return result.Deleted ? Json(new { success = true, message = result.Message })
                : Conflict(new { success = false, message = result.Message });
        }
        catch (Exception ex) when (ex is DbUpdateException or Npgsql.NpgsqlException)
        {
            _logger.LogError(ex, "Không thể xóa vĩnh viễn nhân viên {EmployeeId}.", id);
            return Conflict(new { success = false, message = "Chưa xác nhận được kết quả xóa. Hãy tải lại danh sách và mở lại cảnh báo để kiểm tra dữ liệu liên quan trước khi thử lại." });
        }
    }

    private async Task<NhanVienFormViewModel> BuildEmployeeFormAsync(NhanVienFormViewModel model)
    {
        model.DepartmentOptions = await _context.PhongBans.AsNoTracking().OrderBy(item => item.TenPhongBan).Select(item => new SelectListItem(item.TenPhongBan, item.MaPhongBan.ToString(), item.MaPhongBan == model.Employee.MaPhongBan)).ToListAsync();

        var department = await _context.PhongBans.AsNoTracking().FirstOrDefaultAsync(x => x.MaPhongBan == model.Employee.MaPhongBan);
        var cvQuery = _context.ChucVus.AsNoTracking().Where(OrganizationPositionPolicy.ForDepartment(department));

        model.PositionOptions = await cvQuery.OrderBy(item => item.TenChucVu).Select(item => new SelectListItem(item.TenChucVu, item.MaChucVu.ToString(), item.MaChucVu == model.Employee.MaChucVu)).ToListAsync();
        return model;
    }

    private async Task<IReadOnlyList<SelectListItem>> GetDepartmentOptionsAsync(int? selectedId) =>
        await _context.PhongBans.AsNoTracking().OrderBy(item => item.TenPhongBan).Select(item => new SelectListItem(item.TenPhongBan, item.MaPhongBan.ToString(), item.MaPhongBan == selectedId)).ToListAsync();

    private async Task ValidateEmployeeAsync(NhanVien model, int? currentId = null)
    {
        var emailFormat = new EmailAddressAttribute();
        foreach (var (field, label, value, limit) in new (string, string, string?, int)[]
        {
            ("Ho", "Họ", model.Ho, 100),
            ("Ten", "Tên", model.Ten, 50),
            ("EmailCongVu", "Email công vụ", model.EmailCongVu, 100),
            ("EmailCaNhan", "Email cá nhân", model.EmailCaNhan, 100),
            ("DiaChi", "Địa chỉ", model.DiaChi, 255),
            ("TrinhDo", "Trình độ", model.TrinhDo, 50)
        })
        {
            if (value?.Length > limit)
                ModelState.AddModelError($"Employee.{field}", $"{label} không được vượt quá {limit} ký tự.");
        }
        if (string.IsNullOrWhiteSpace(model.Ho)) ModelState.AddModelError("Employee.Ho", "Vui lòng nhập họ.");
        if (string.IsNullOrWhiteSpace(model.Ten)) ModelState.AddModelError("Employee.Ten", "Vui lòng nhập tên.");
        foreach (var (field, value) in new[] { ("Ho", model.Ho), ("Ten", model.Ten) })
            if (!string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, NhanVienFormViewModel.PersonNamePattern))
                ModelState.AddModelError($"Employee.{field}", "Họ và tên chỉ gồm chữ, khoảng trắng, dấu nối hoặc dấu nháy. Ví dụ: Nguyễn Văn An.");
        var department = await _context.PhongBans.AsNoTracking().FirstOrDefaultAsync(item => item.MaPhongBan == model.MaPhongBan);
        if (department == null) ModelState.AddModelError("Employee.MaPhongBan", "Vui lòng chọn phòng ban hợp lệ.");
        if (!await _context.ChucVus.Where(OrganizationPositionPolicy.ForDepartment(department)).AnyAsync(item => item.MaChucVu == model.MaChucVu))
            ModelState.AddModelError("Employee.MaChucVu", "Vui lòng chọn chức vụ thuộc phòng ban đã chọn.");
        if (!model.NgaySinh.HasValue || model.NgaySinh == DateOnly.MinValue)
            ModelState.AddModelError("Employee.NgaySinh", "Vui lòng nhập ngày sinh.");
        else if (model.NgaySinh > DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
            ModelState.AddModelError("Employee.NgaySinh", "Nhân viên phải đủ 18 tuổi trở lên.");
        else if (model.NgayVaoLam != default && model.NgaySinh >= model.NgayVaoLam)
            ModelState.AddModelError("Employee.NgaySinh", "Ngày sinh phải trước ngày vào làm ghi trên hợp đồng.");
        if (model.SoNguoiPhuThuoc is < 0 or > 20)
            ModelState.AddModelError("Employee.SoNguoiPhuThuoc", "Số người phụ thuộc phải là số nguyên từ 0 đến 20.");
        if (model.GioiTinh is not ("NAM" or "NU" or "KHAC")) ModelState.AddModelError("Employee.GioiTinh", "Vui lòng chọn giới tính.");
        if (string.IsNullOrWhiteSpace(model.DienThoai)) ModelState.AddModelError("Employee.DienThoai", "Vui lòng nhập điện thoại.");
        else if (model.DienThoai.Length > 12 || !Regex.IsMatch(model.DienThoai, @"\A" + NhanVienFormViewModel.MobilePhonePattern + @"\z", RegexOptions.CultureInvariant))
            ModelState.AddModelError("Employee.DienThoai", "Số điện thoại phải có dạng 0912345678 hoặc +84912345678, không chứa khoảng trắng hay ký tự khác.");
        if (string.IsNullOrWhiteSpace(model.DiaChi)) ModelState.AddModelError("Employee.DiaChi", "Vui lòng nhập địa chỉ.");
        if (string.IsNullOrWhiteSpace(model.TrinhDo) || model.TrinhDo == "Chưa cập nhật")
            ModelState.AddModelError("Employee.TrinhDo", "Vui lòng chọn trình độ chuyên môn.");
        if (model.TrangThai != "CHO_HOP_DONG" && (!model.NgayVaoLam.HasValue || model.NgayVaoLam == DateOnly.MinValue)) ModelState.AddModelError("Employee.NgayVaoLam", "Vui lòng nhập ngày vào làm.");
        if (model.NgayThoiViec.HasValue && model.NgayThoiViec < model.NgayVaoLam) ModelState.AddModelError("Employee.NgayThoiViec", "Ngày thôi việc không được trước ngày vào làm.");
        if (!SupportedEmployeeStatuses.Contains(model.TrangThai)) ModelState.AddModelError("Employee.TrangThai", "Trạng thái nhân viên không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(model.EmailCongVu))
        {
            if (!emailFormat.IsValid(model.EmailCongVu))
            {
                ModelState.AddModelError("Employee.EmailCongVu", "Email công vụ không đúng định dạng.");
            }
            else if (model.EmailCongVu.Length <= 100 && await _context.NhanViens.AnyAsync(item => item.MaNhanVien != currentId && item.EmailCongVu.ToLower() == model.EmailCongVu.ToLower()))
            {
                ModelState.AddModelError("Employee.EmailCongVu", "Email công vụ đã được dùng cho nhân viên khác.");
            }
        }

        // Email cá nhân là tuỳ chọn: để trống cũng được, nhưng đã nhập thì phải
        // đúng định dạng. Không kiểm tra trùng vì CSDL không đặt UNIQUE cho cột này.
        if (string.IsNullOrWhiteSpace(model.EmailCaNhan))
            ModelState.AddModelError("Employee.EmailCaNhan", "Vui lòng nhập email cá nhân, ví dụ ten@gmail.com.");
        else if (!emailFormat.IsValid(model.EmailCaNhan))
        {
            ModelState.AddModelError("Employee.EmailCaNhan", "Email cá nhân không đúng định dạng.");
        }
    }

    private static void NormalizeEmployeeStatus(NhanVien model)
    {
        model.TrangThai = model.TrangThai switch
        {
            "NGHI_VIEC" => "DA_THOI_VIEC",
            "TAM_NGHI" => "NGHI_PHEP",
            _ => model.TrangThai
        };
    }

    private static void Normalize(NhanVien model)
    {
        model.Ho = model.Ho?.Trim() ?? string.Empty;
        model.Ten = model.Ten?.Trim() ?? string.Empty;
        model.EmailCongVu = model.EmailCongVu?.Trim() ?? string.Empty;
        model.EmailCaNhan = string.IsNullOrWhiteSpace(model.EmailCaNhan) ? null : model.EmailCaNhan.Trim();
        model.DienThoai = model.DienThoai?.Trim() ?? string.Empty;
        model.DiaChi = string.IsNullOrWhiteSpace(model.DiaChi) ? null : model.DiaChi.Trim();
        model.TrinhDo = model.TrinhDo?.Trim() ?? string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> ProfileModal(int id)
    {
        var employee = await _context.NhanViens
            .AsNoTracking()
            .Include(e => e.PhongBan)
            .Include(e => e.ChucVu)
            .Include(e => e.HopDongs)
            .Include(e => e.TaiKhoan)
                .ThenInclude(t => t!.VaiTro)
            .Include(e => e.YeuCauTaiKhoanCuaNhanVien)
            .FirstOrDefaultAsync(e => e.MaNhanVien == id);

        if (employee == null)
        {
            return NotFound("<div class='p-4 text-center text-danger'>Không tìm thấy thông tin nhân viên này trong hệ thống.</div>");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var contracts = employee.HopDongs.OrderByDescending(h => h.NgayBatDau).ToList();
        var latestContract = contracts.FirstOrDefault();
        var daysRemaining = latestContract?.NgayKetThuc is DateOnly endDate
            ? endDate.DayNumber - today.DayNumber
            : (int?)null;
        var contractState = GetContractState(latestContract, daysRemaining);

        var model = new NhanVienProfileModalViewModel
        {
            Employee = employee,
            LatestContract = latestContract,
            ContractHistory = contracts,
            Account = employee.TaiKhoan,
            ContractDaysRemaining = daysRemaining,
            ContractState = contractState
        };

        return PartialView("_EmployeeProfileModalContent", model);
    }

    private static string GetContractState(HopDong? contract, int? daysRemaining) =>
        ContractState.Classify(contract, DateOnly.FromDateTime(DateTime.Today));

    private async Task<IReadOnlyList<HopDong>> GetEligibleContractsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await this.SearchDataAsync($"employees:eligible-contracts:{today:yyyyMMdd}", () => _context.HopDongs.AsNoTracking().Include(x => x.NhanVien)
            .Where(x => x.NhanVien != null && !x.NhanVien.IsProfileComplete && x.TrangThai != "DA_CHAM_DUT" && x.TrangThai != "CHAM_DUT" && (x.NgayKetThuc == null || x.NgayKetThuc >= today))
            .OrderBy(x => x.SoHopDong).ToListAsync());
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }

    private async Task<string> GenerateUniqueWorkEmailAsync(string ho, string ten)
    {
        var cleanTen = RemoveDiacritics(ten).Trim().ToLowerInvariant();
        var cleanHo = RemoveDiacritics(ho).Trim().ToLowerInvariant().Replace(" ", "");
        cleanTen = Regex.Replace(cleanTen, @"[^a-z0-9]", "");
        cleanHo = Regex.Replace(cleanHo, @"[^a-z0-9]", "");
        if (string.IsNullOrEmpty(cleanTen)) cleanTen = "nhanvien";

        var localPart = string.IsNullOrEmpty(cleanHo) ? cleanTen : $"{cleanTen}.{cleanHo}";
        localPart = localPart[..Math.Min(localPart.Length, 60)].TrimEnd('.');
        var baseEmail = $"{localPart}@overtimehotel.com";

        if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == baseEmail.ToLower()))
        {
            return baseEmail;
        }

        int counter = 1;
        while (true)
        {
            var candidate = $"{localPart}{counter}@overtimehotel.com";
            if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == candidate.ToLower()))
            {
                return candidate;
            }
            counter++;
        }
    }
}
