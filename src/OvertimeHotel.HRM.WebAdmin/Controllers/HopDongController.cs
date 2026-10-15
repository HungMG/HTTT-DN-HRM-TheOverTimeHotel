using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.ManageContracts)]
public class HopDongController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HopDongController> _logger;

    public HopDongController(AppDbContext context, ILogger<HopDongController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, string? status, string? contractFilter, int page = 1, int pageSize = 10, bool isPartial = false)
    {
        keyword = keyword?.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status;
        contractFilter = string.IsNullOrWhiteSpace(contractFilter) ? null : contractFilter;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var query = _context.HopDongs.AsNoTracking().Include(contract => contract.NhanVien).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(contract =>
                contract.SoHopDong.ToLower().Contains(value) ||
                contract.LoaiHopDong.ToLower().Contains(value) ||
                (contract.NhanVien != null && (contract.NhanVien.Ho.ToLower().Contains(value) || contract.NhanVien.Ten.ToLower().Contains(value))));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(contract => contract.TrangThai == status);

        var today = DateOnly.FromDateTime(DateTime.Today);
        try
        {
            // Thực thi tuần tự trên cùng DbContext instance để đảm bảo an toàn đơn luồng (Thread-safe)
            var allContracts = await _context.HopDongs.AsNoTracking()
                .Include(c => c.NhanVien)
                    .ThenInclude(nv => nv!.PhongBan)
                .ToListAsync();
            var allEmployees = await _context.NhanViens.AsNoTracking()
                .Include(nv => nv.PhongBan)
                .OrderBy(item => item.Ten).ThenBy(item => item.Ho)
                .ToListAsync();
            var allDepartments = await _context.PhongBans.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .ToListAsync();

            // Lọc trên bộ nhớ máy chủ (0ms)
            var filtered = allContracts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var value = keyword.ToLower();
                filtered = filtered.Where(contract =>
                    contract.SoHopDong.ToLower().Contains(value) ||
                    contract.LoaiHopDong.ToLower().Contains(value) ||
                    (contract.NhanVien != null && (contract.NhanVien.Ho.ToLower().Contains(value) || contract.NhanVien.Ten.ToLower().Contains(value))));
            }

            if (!string.IsNullOrWhiteSpace(status))
                filtered = filtered.Where(contract => contract.TrangThai == status);

            var items = filtered.Select(contract =>
            {
                var days = contract.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null;
                return new HopDongListItemViewModel
                {
                    Contract = contract,
                    EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên không tồn tại",
                    DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn",
                    PersonalEmail = contract.NhanVien?.EmailCaNhan,
                    DaysRemaining = days,
                    State = GetState(contract, days)
                };
            });

            if (string.IsNullOrWhiteSpace(contractFilter))
            {
                // Mặc định ẩn hợp đồng đã hết hạn hoặc đã chấm dứt
                items = items.Where(item => item.State != "DA_HET_HAN" && item.State != "DA_CHAM_DUT");
            }
            else if (contractFilter == "DA_HET_HAN")
            {
                items = items.Where(item => item.DaysRemaining < 0 || item.State == "DA_HET_HAN");
            }
            else if (contractFilter == "SAP_HET_HAN")
            {
                items = items.Where(item => item.DaysRemaining is >= 0 and <= 30);
            }
            else if (contractFilter == "HIEU_LUC")
            {
                items = items.Where(item => item.State == "HIEU_LUC");
            }
            // contractFilter == "TAT_CA" -> hiển thị toàn bộ không lọc trạng thái

            var allDays = allContracts.Select(contract => contract.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null).ToList();
            var employeeOptions = allEmployees.Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToList();
            var departmentOptions = allDepartments.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString())).ToList();
            
            ViewBag.EmployeeOptions = employeeOptions;
            ViewBag.DepartmentOptions = departmentOptions;
            ViewBag.EmployeeList = allEmployees.Select(e => new
            {
                Id = e.MaNhanVien,
                Code = $"NV-{e.MaNhanVien:D3}",
                FullName = e.HoTen,
                DepartmentId = e.MaPhongBan,
                DepartmentName = e.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
                PersonalEmail = e.EmailCaNhan ?? "Chưa cập nhật email cá nhân"
            }).ToList();

            var orderedContracts = items.OrderByDescending(i => i.Contract.NgayBatDau).ToList();
            var totalFiltered = orderedContracts.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
            var pageNumber = Math.Clamp(page, 1, totalPages);
            var pagedContracts = orderedContracts.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var model = new HopDongIndexViewModel
            {
                CreateContractForm = new HopDongFormViewModel
                {
                    Contract = new HopDong
                    {
                        NgayBatDau = today,
                        TrangThai = "HIEU_LUC",
                        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM",
                        HinhThucHopDong = "CHINH_THUC"
                    },
                    DepartmentOptions = departmentOptions,
                    EmployeeOptions = employeeOptions
                },
                Keyword = keyword,
                Status = status,
                ContractFilter = contractFilter,
                Contracts = pagedContracts,
                EmployeeOptions = employeeOptions,
                TotalContracts = allContracts.Count,
                TotalFilteredContracts = totalFiltered,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                ActiveContracts = allContracts.Count(contract => contract.TrangThai == "HIEU_LUC"),
                ExpiringContracts = allDays.Count(days => days is >= 0 and <= 30),
                ExpiredContracts = allDays.Count(days => days < 0)
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_ContractTablePartial", model);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy vấn danh sách hợp đồng từ CSDL Supabase.");
            TempData["Error"] = "Lỗi kết nối CSDL Supabase: " + ex.Message;
            ViewBag.EmployeeOptions = new List<SelectListItem>();
            ViewBag.DepartmentOptions = new List<SelectListItem>();
            ViewBag.EmployeeList = new List<object>();
            var errorModel = new HopDongIndexViewModel
            {
                CreateContractForm = new HopDongFormViewModel
                {
                    Contract = new HopDong
                    {
                        NgayBatDau = today,
                        TrangThai = "HIEU_LUC",
                        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM",
                        HinhThucHopDong = "CHINH_THUC"
                    }
                },
                Keyword = keyword,
                Status = status,
                ContractFilter = contractFilter,
                Contracts = new List<HopDongListItemViewModel>(),
                EmployeeOptions = new List<SelectListItem>()
            };

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_ContractTablePartial", errorModel);
            }

            return View(errorModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HopDongFormViewModel model)
    {
        ViewBag.EmployeeOptions = await GetEmployeeOptionsAsync();
        model.Contract.MaNhanVien = model.EmployeeId;
        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        await ValidateContractAsync(model.Contract);
        var employee = await _context.NhanViens.Include(nv => nv.PhongBan).AsNoTracking().FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);
        if (employee == null)
            ModelState.AddModelError(nameof(model.EmployeeId), "Vui lòng chọn nhân viên.");
        model.EmployeeName = employee?.HoTen ?? string.Empty;
        model.DepartmentName = employee?.PhongBan?.TenPhongBan ?? string.Empty;
        model.PersonalEmail = employee?.EmailCaNhan;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(item => item.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.HopDongs.Add(model.Contract);
            await _context.SaveChangesAsync();

            string emailNotice = "";
            if (model.EmailNotificationOption == "GUI_EMAIL")
            {
                if (!string.IsNullOrWhiteSpace(employee?.EmailCaNhan))
                {
                    _logger.LogInformation("Gửi thông báo hợp đồng mới '{ContractNumber}' tới email cá nhân: {Email}", model.Contract.SoHopDong, employee.EmailCaNhan);
                    emailNotice = $" (Đã gửi thông báo tới email cá nhân: {employee.EmailCaNhan})";
                }
                else
                {
                    emailNotice = " (Nhân viên chưa cập nhật email cá nhân để gửi thông báo)";
                }
            }

            TempData["Success"] = $"Đã lập hợp đồng “{model.Contract.SoHopDong}” cho {employee!.HoTen}.{emailNotice}";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo hợp đồng {ContractNumber}.", model.Contract.SoHopDong);
            TempData["Error"] = "Không thể lập hợp đồng. Mã / Số hợp đồng có thể đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewBag.EmployeeOptions = await GetEmployeeOptionsAsync();
        var contract = await _context.HopDongs
            .Include(item => item.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MaHopDong == id);

        if (contract == null) return NotFound();

        var allDepartments = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
        ViewBag.DepartmentOptions = allDepartments.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString())).ToList();

        return View(new HopDongFormViewModel
        {
            EmployeeId = contract.MaNhanVien,
            EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên",
            DepartmentId = contract.NhanVien?.MaPhongBan,
            DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
            PersonalEmail = contract.NhanVien?.EmailCaNhan,
            Contract = contract
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HopDongFormViewModel model)
    {
        ViewBag.EmployeeOptions = await GetEmployeeOptionsAsync();
        if (id != model.Contract.MaHopDong)
            return BadRequest();

        model.Contract.MaNhanVien = model.EmployeeId;
        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        await ValidateContractAsync(model.Contract, id);
        var employee = await _context.NhanViens.Include(nv => nv.PhongBan).AsNoTracking().FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);
        if (employee == null)
            ModelState.AddModelError(nameof(model.EmployeeId), "Vui lòng chọn nhân viên.");
        model.EmployeeName = employee?.HoTen ?? string.Empty;
        model.DepartmentName = employee?.PhongBan?.TenPhongBan ?? string.Empty;
        model.PersonalEmail = employee?.EmailCaNhan;

        if (!ModelState.IsValid)
            return View(model);

        var contract = await _context.HopDongs.FindAsync(id);
        if (contract == null)
            return NotFound();

        contract.MaNhanVien = model.EmployeeId;
        contract.SoHopDong = model.Contract.SoHopDong.Trim();
        contract.LoaiHopDong = model.Contract.LoaiHopDong.Trim();
        contract.HinhThucHopDong = model.Contract.HinhThucHopDong.Trim();
        contract.NgayBatDau = model.Contract.NgayBatDau;
        contract.NgayKetThuc = model.Contract.NgayKetThuc;
        contract.LuongCoBan = model.Contract.LuongCoBan;
        contract.TrangThai = model.Contract.TrangThai;

        try
        {
            await _context.SaveChangesAsync();
            string emailNotice = "";
            if (model.EmailNotificationOption == "GUI_EMAIL")
            {
                if (!string.IsNullOrWhiteSpace(employee?.EmailCaNhan))
                {
                    _logger.LogInformation("Gửi thông báo cập nhật hợp đồng '{ContractNumber}' tới email cá nhân: {Email}", contract.SoHopDong, employee.EmailCaNhan);
                    emailNotice = $" (Đã gửi thông báo tới email cá nhân: {employee.EmailCaNhan})";
                }
            }
            TempData["Success"] = $"Đã cập nhật hợp đồng “{contract.SoHopDong}”.{emailNotice}";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật hợp đồng {ContractId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật hợp đồng. Vui lòng kiểm tra dữ liệu.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var contract = await _context.HopDongs.FindAsync(id);
        if (contract == null)
            return NotFound();

        _context.HopDongs.Remove(contract);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa hợp đồng “{contract.SoHopDong}”.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateContractAsync(HopDong model, int? currentId = null)
    {
        model.SoHopDong = model.SoHopDong?.Trim() ?? string.Empty;
        model.LoaiHopDong = model.LoaiHopDong?.Trim() ?? string.Empty;
        model.HinhThucHopDong = model.HinhThucHopDong?.Trim() ?? "CHINH_THUC";
        if (string.IsNullOrWhiteSpace(model.SoHopDong)) ModelState.AddModelError("Contract.SoHopDong", "Vui lòng nhập mã / số hợp đồng.");
        if (string.IsNullOrWhiteSpace(model.LoaiHopDong)) ModelState.AddModelError("Contract.LoaiHopDong", "Vui lòng nhập loại hợp đồng.");
        if (model.NgayKetThuc.HasValue && model.NgayKetThuc < model.NgayBatDau) ModelState.AddModelError("Contract.NgayKetThuc", "Ngày kết thúc phải sau ngày bắt đầu.");
        if (model.LuongCoBan < 0) ModelState.AddModelError("Contract.LuongCoBan", "Lương cơ bản không được âm.");
        if (await _context.HopDongs.AnyAsync(item => item.MaHopDong != currentId && item.SoHopDong.ToLower() == model.SoHopDong.ToLower())) ModelState.AddModelError("Contract.SoHopDong", "Mã / Số hợp đồng đã tồn tại.");
    }

    private async Task<IReadOnlyList<SelectListItem>> GetEmployeeOptionsAsync() =>
        await _context.NhanViens.AsNoTracking().OrderBy(item => item.Ten).ThenBy(item => item.Ho).Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToListAsync();

    private static string GetState(HopDong contract, int? daysRemaining)
    {
        if (daysRemaining < 0) return "DA_HET_HAN";
        if (daysRemaining is >= 0 and <= 30) return "SAP_HET_HAN";
        return contract.TrangThai;
    }
}
