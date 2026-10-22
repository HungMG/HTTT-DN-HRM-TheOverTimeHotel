using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        try
        {
            // Thực thi tuần tự trên cùng DbContext instance để đảm bảo an toàn đơn luồng (Thread-safe)
            var allContracts = await _context.HopDongs.AsNoTracking()
                .Include(c => c.NhanVien)
                    .ThenInclude(nv => nv!.PhongBan)
                .Include(c => c.NhanVien)
                    .ThenInclude(nv => nv!.ChucVu)
                .ToListAsync();
            var allEmployees = await _context.NhanViens.AsNoTracking()
                .Include(nv => nv.PhongBan)
                .Include(nv => nv.ChucVu)
                .OrderBy(item => item.Ten).ThenBy(item => item.Ho)
                .ToListAsync();
            var allDepartments = await _context.PhongBans.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .ToListAsync();
            var allPositions = await _context.ChucVus.AsNoTracking()
                .OrderBy(cv => cv.TenChucVu)
                .ToListAsync();

            // Lọc trên bộ nhớ máy chủ (0ms)
            var filtered = allContracts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var value = keyword.ToLower();
                // Đường dự phòng đọc trên bộ nhớ — vẫn cần vì dữ liệu cũ có thể
                // còn câu tiếng Việt trong cột thô.
                var maLoaiKhop = ContractTypes.MatchingCodes(keyword);
                filtered = filtered.Where(contract =>
                    contract.SoHopDong.ToLower().Contains(value) ||
                    maLoaiKhop.Contains(contract.LoaiHopDong) ||
                    contract.LoaiHopDong.ToLower().Contains(value) ||
                    (contract.NhanVien != null && (contract.NhanVien.Ho.ToLower().Contains(value) || contract.NhanVien.Ten.ToLower().Contains(value))));
            }

            var items = filtered.Select(contract =>
            {
                var days = contract.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null;
                var isRenewed = allContracts.Any(other => other.MaNhanVien == contract.MaNhanVien &&
                                                          other.MaHopDong != contract.MaHopDong &&
                                                          other.NgayBatDau >= (contract.NgayKetThuc ?? contract.NgayBatDau) &&
                                                          (other.TrangThai == "HIEU_LUC" || !other.NgayKetThuc.HasValue || other.NgayKetThuc.Value >= today));
                return new HopDongListItemViewModel
                {
                    Contract = contract,
                    EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên không tồn tại",
                    DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn",
                    PositionName = contract.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân bổ",
                    PersonalEmail = contract.NhanVien?.EmailCaNhan,
                    DaysRemaining = days,
                    State = ContractState.Classify(contract, today),
                    IsRenewed = isRenewed
                };
            });

            if (!string.IsNullOrWhiteSpace(status))
                items = items.Where(item => status switch
                {
                    "HIEU_LUC" => item.State is "HIEU_LUC" or "SAP_HET_HAN",
                    "HET_HAN" => item.State == "DA_HET_HAN",
                    "DA_CHAM_DUT" or "CHAM_DUT" => item.State == "DA_CHAM_DUT",
                    _ => false
                });

            if (string.IsNullOrWhiteSpace(contractFilter) && string.IsNullOrWhiteSpace(status))
            {
                // Mặc định ẩn hợp đồng đã hết hạn hoặc đã chấm dứt
                items = items.Where(item => item.State != "DA_HET_HAN" && item.State != "DA_CHAM_DUT");
            }
            else if (contractFilter == "DA_HET_HAN")
            {
                items = items.Where(item => item.State == "DA_HET_HAN");
            }
            else if (contractFilter == "SAP_HET_HAN")
            {
                items = items.Where(item => item.State == "SAP_HET_HAN");
            }
            else if (contractFilter == "HIEU_LUC")
            {
                items = items.Where(item => item.State == "HIEU_LUC" && (!item.DaysRemaining.HasValue || item.DaysRemaining.Value > 30));
            }
            // contractFilter == "TAT_CA" -> hiển thị toàn bộ không lọc trạng thái

            var employeeOptions = allEmployees.Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToList();
            var departmentOptions = allDepartments.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString())).ToList();
            var positionOptions = allPositions.Select(cv => new SelectListItem(cv.TenChucVu + (cv.MaPhongBan == null ? " (Chung)" : ""), cv.MaChucVu.ToString())).ToList();
            
            ViewBag.EmployeeOptions = employeeOptions;
            ViewBag.DepartmentOptions = departmentOptions;
            ViewBag.PositionOptions = positionOptions;
            var employeeItems = allEmployees.Select(e => new HopDongEmployeeOptionItem
            {
                Id = e.MaNhanVien,
                Code = $"NV-{e.MaNhanVien:D3}",
                FullName = e.HoTen,
                DepartmentId = e.MaPhongBan,
                DepartmentName = e.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
                PositionId = e.MaChucVu,
                PositionName = e.ChucVu?.TenChucVu ?? "Chưa phân bổ",
                PersonalEmail = e.EmailCaNhan ?? "Chưa cập nhật email cá nhân"
            }).ToList();
            ViewBag.EmployeeList = employeeItems;

            var orderedContracts = items.OrderByDescending(i => i.Contract.NgayBatDau).ToList();
            var totalFiltered = orderedContracts.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
            var pageNumber = Math.Clamp(page, 1, totalPages);
            var pagedContracts = orderedContracts.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var initialSoHopDong = await GenerateUniqueContractNumberAsync("CHINH_THUC");
            var model = new HopDongIndexViewModel
            {
                CreateContractForm = new HopDongFormViewModel
                {
                    Contract = new HopDong
                    {
                        SoHopDong = initialSoHopDong,
                        NgayBatDau = today,
                        NgayKetThuc = today.AddYears(1),
                        TrangThai = "HIEU_LUC",
                        LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM",
                        HinhThucHopDong = "CHINH_THUC"
                    },
                    DepartmentOptions = departmentOptions,
                    PositionOptions = positionOptions,
                    EmployeeOptions = employeeOptions,
                    EmployeeList = employeeItems
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
                ActiveContracts = allContracts.Count(contract => ContractState.Classify(contract, today) == "HIEU_LUC"),
                ExpiringContracts = allContracts.Count(contract => ContractState.Classify(contract, today) == "SAP_HET_HAN"),
                ExpiredContracts = allContracts.Count(contract => ContractState.Classify(contract, today) == "DA_HET_HAN")
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
                        SoHopDong = $"HDLD-{today.Year}/01/OTH",
                        NgayBatDau = today,
                        NgayKetThuc = today.AddYears(1),
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
        if (model.EmployeeId == 0) model.EmployeeId = null;
        ModelState.Remove("Contract.MaNhanVien");
        model.Contract.MaHopDong = 0;

        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        NhanVien? employee = null;
        if (model.EmployeeId.HasValue)
        {
            employee = await _context.NhanViens.FindAsync(model.EmployeeId.Value);
            if (employee == null) ModelState.AddModelError("EmployeeId", "Nhân viên đã chọn không tồn tại.");
        }
        else if (string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            ModelState.AddModelError("EmployeeName", "Vui lòng nhập họ và tên nhân viên.");
        }

        await ValidateOrganizationAsync(model);
        if (employee == null && !model.EmployeeId.HasValue && !string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            var parts = model.EmployeeName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ho = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : string.Empty;
            var ten = parts.Last();
            employee = new NhanVien
            {
                Ho = ho, Ten = ten, IsProfileComplete = false,
                EmailCongVu = await GenerateUniqueWorkEmailAsync(ho, ten),
                NgayVaoLam = model.Contract.NgayBatDau,
                TrangThai = "DANG_LAM"
            };
        }
        model.Contract.MaNhanVien = employee?.MaNhanVien ?? 0;

        // Tự động sinh mã hợp đồng nếu để trống theo hình thức hợp đồng
        if (string.IsNullOrWhiteSpace(model.Contract.SoHopDong))
        {
            model.Contract.SoHopDong = await GenerateUniqueContractNumberAsync(model.Contract.HinhThucHopDong);
        }

        // Tự động tính ngày kết thúc nếu để trống theo loại hợp đồng
        if (!model.Contract.NgayKetThuc.HasValue)
        {
            // So khớp qua ContractTypes thay vì so tuyệt đối với hai mã: dữ liệu
            // cũ còn câu tiếng Việt ("HĐLĐ Xác định thời hạn (3 năm)"), so tuyệt
            // đối thì rơi vào cả hai nhánh đều trượt và hợp đồng ra đời không có
            // ngày kết thúc — tức bị đọc thành vô thời hạn.
            switch (ContractTypes.Normalize(model.Contract.LoaiHopDong))
            {
                case ContractTypes.XAC_DINH_THOI_HAN_1_NAM:
                    model.Contract.NgayKetThuc = model.Contract.NgayBatDau.AddYears(1);
                    break;
                case ContractTypes.XAC_DINH_THOI_HAN_3_NAM:
                    model.Contract.NgayKetThuc = model.Contract.NgayBatDau.AddYears(3);
                    break;
            }
        }

        // Đảm bảo trạng thái hợp đồng mới tạo luôn là HIEU_LUC nếu chưa quá hạn kết thúc
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (string.IsNullOrWhiteSpace(model.Contract.TrangThai) ||
            (model.Contract.TrangThai == "HET_HAN" && (!model.Contract.NgayKetThuc.HasValue || model.Contract.NgayKetThuc.Value >= today)))
        {
            model.Contract.TrangThai = "HIEU_LUC";
        }

        await ValidateContractAsync(model.Contract);

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(item => item.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            if (employee != null)
            {
                employee.MaPhongBan = model.DepartmentId;
                employee.MaChucVu = model.PositionId;
                if (!string.IsNullOrWhiteSpace(model.PersonalEmail)) employee.EmailCaNhan = model.PersonalEmail.Trim();
                model.Contract.NhanVien = employee;
            }
            _context.HopDongs.Add(model.Contract);
            await _context.SaveChangesAsync();

            string emailNotice = "";
            var finalEmail = !string.IsNullOrWhiteSpace(model.PersonalEmail) ? model.PersonalEmail.Trim() : employee?.EmailCaNhan;
            if (model.EmailNotificationOption == "GUI_EMAIL")
            {
                if (!string.IsNullOrWhiteSpace(finalEmail))
                {
                    _logger.LogInformation("Đã gửi bản hợp đồng '{ContractNumber}' tới email cá nhân: {Email}", model.Contract.SoHopDong, finalEmail);
                    emailNotice = $" (Đã gửi văn bản hợp đồng tới email cá nhân: {finalEmail})";
                }
                else
                {
                    emailNotice = " (Chưa có email cá nhân để gửi thông báo hợp đồng)";
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
            .Include(item => item.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MaHopDong == id);

        if (contract == null) return NotFound();

        var empDeptId = contract.NhanVien?.MaPhongBan;
        var empPosId = contract.NhanVien?.MaChucVu;

        var allDepartments = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
        var departmentOptions = allDepartments.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString(), d.MaPhongBan == empDeptId)).ToList();
        ViewBag.DepartmentOptions = departmentOptions;

        var cvQuery = _context.ChucVus.AsNoTracking().AsQueryable();
        if (empDeptId.HasValue && empDeptId.Value > 0)
        {
            cvQuery = cvQuery.Where(x => x.MaPhongBan == empDeptId.Value || x.MaPhongBan == null);
        }
        var positionOptions = await cvQuery.OrderBy(x => x.TenChucVu)
            .Select(x => new SelectListItem(x.TenChucVu + (x.MaPhongBan == null ? " (Chung)" : ""), x.MaChucVu.ToString(), x.MaChucVu == empPosId))
            .ToListAsync();
        ViewBag.PositionOptions = positionOptions;

        return View(new HopDongFormViewModel
        {
            EmployeeId = contract.MaNhanVien,
            EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên",
            DepartmentId = contract.NhanVien?.MaPhongBan,
            DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
            PositionId = contract.NhanVien?.MaChucVu,
            PositionName = contract.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân bổ",
            PersonalEmail = contract.NhanVien?.EmailCaNhan,
            DepartmentOptions = departmentOptions,
            PositionOptions = positionOptions,
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

        model.Contract.MaNhanVien = model.EmployeeId ?? 0;
        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        await ValidateContractAsync(model.Contract, id);
        await ValidateOrganizationAsync(model);
        var employee = await _context.NhanViens
            .Include(nv => nv.PhongBan)
            .Include(nv => nv.ChucVu)
            .FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);

        if (employee == null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Vui lòng chọn nhân viên.");
        }
        else
        {
            model.EmployeeName = employee.HoTen;
            model.DepartmentName = employee.PhongBan?.TenPhongBan ?? string.Empty;
            model.PositionName = employee.ChucVu?.TenChucVu ?? string.Empty;

            // Cập nhật email cá nhân nếu người dùng nhập mới / chỉnh sửa
            if (!string.IsNullOrWhiteSpace(model.PersonalEmail) && employee.EmailCaNhan != model.PersonalEmail.Trim())
            {
                employee.EmailCaNhan = model.PersonalEmail.Trim();
            }

            // Cập nhật phòng ban nếu người dùng thay đổi trên form
            if (model.DepartmentId.HasValue && model.DepartmentId.Value > 0)
            {
                employee.MaPhongBan = model.DepartmentId.Value;
            }

            // Cập nhật chức vụ nếu người dùng thay đổi trên form
            if (model.PositionId.HasValue && model.PositionId.Value > 0)
            {
                employee.MaChucVu = model.PositionId.Value;
            }

        }

        if (!ModelState.IsValid)
        {
            var allDepts = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
            ViewBag.DepartmentOptions = allDepts.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString())).ToList();
            var cvQuery = _context.ChucVus.AsNoTracking().AsQueryable();
            if (model.DepartmentId > 0) cvQuery = cvQuery.Where(x => x.MaPhongBan == model.DepartmentId || x.MaPhongBan == null);
            ViewBag.PositionOptions = await cvQuery.OrderBy(x => x.TenChucVu).Select(x => new SelectListItem(x.TenChucVu, x.MaChucVu.ToString())).ToListAsync();
            return View(model);
        }

        var contract = await _context.HopDongs.FindAsync(id);
        if (contract == null)
            return NotFound();

        contract.MaNhanVien = model.EmployeeId ?? contract.MaNhanVien;
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
            var finalEmail = !string.IsNullOrWhiteSpace(model.PersonalEmail) ? model.PersonalEmail.Trim() : employee?.EmailCaNhan;
            if (model.EmailNotificationOption == "GUI_EMAIL")
            {
                if (!string.IsNullOrWhiteSpace(finalEmail))
                {
                    _logger.LogInformation("Gửi thông báo cập nhật hợp đồng '{ContractNumber}' tới email cá nhân: {Email}", contract.SoHopDong, finalEmail);
                    emailNotice = $" (Đã gửi thông báo tới email cá nhân: {finalEmail})";
                }
            }
            TempData["Success"] = $"Đã cập nhật hợp đồng “{contract.SoHopDong}”.{emailNotice}";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật hợp đồng {ContractId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật hợp đồng. Vui lòng kiểm tra dữ liệu.");
            var allDepts = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
            ViewBag.DepartmentOptions = allDepts.Select(d => new SelectListItem(d.TenPhongBan, d.MaPhongBan.ToString())).ToList();
            var cvQuery = _context.ChucVus.AsNoTracking().AsQueryable();
            if (model.DepartmentId > 0) cvQuery = cvQuery.Where(x => x.MaPhongBan == model.DepartmentId || x.MaPhongBan == null);
            ViewBag.PositionOptions = await cvQuery.OrderBy(x => x.TenChucVu).Select(x => new SelectListItem(x.TenChucVu, x.MaChucVu.ToString())).ToListAsync();
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

    [HttpGet]
    public async Task<IActionResult> GenerateContractNumber(string? hinhThuc)
    {
        var contractNumber = await GenerateUniqueContractNumberAsync(hinhThuc);
        return Json(new { success = true, contractNumber });
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(int id)
    {
        var contract = await _context.HopDongs
            .AsNoTracking()
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(c => c.MaHopDong == id);

        if (contract == null || contract.NhanVien == null)
            return NotFound("Không tìm thấy thông tin hợp đồng.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysRemaining = contract.NgayKetThuc.HasValue
            ? (contract.NgayKetThuc.Value.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days
            : (int?)null;

        var state = ContractState.Classify(contract, today);

        var isRenewed = await _context.HopDongs
            .AsNoTracking()
            .AnyAsync(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau >= (contract.NgayKetThuc ?? contract.NgayBatDau));

        var previousContract = await _context.HopDongs
            .AsNoTracking()
            .Where(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau < contract.NgayBatDau)
            .OrderByDescending(c => c.NgayBatDau)
            .FirstOrDefaultAsync();

        var nextContract = await _context.HopDongs
            .AsNoTracking()
            .Where(c => c.MaNhanVien == contract.MaNhanVien && c.MaHopDong != contract.MaHopDong && c.NgayBatDau > contract.NgayBatDau)
            .OrderBy(c => c.NgayBatDau)
            .FirstOrDefaultAsync();

        var model = new HopDongDetailsModalViewModel
        {
            Contract = contract,
            Employee = contract.NhanVien,
            DepartmentName = contract.NhanVien.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
            PositionName = contract.NhanVien.ChucVu?.TenChucVu ?? "Chưa phân bổ",
            DaysRemaining = daysRemaining,
            State = state,
            IsRenewed = isRenewed,
            PreviousContract = previousContract,
            NextContract = nextContract
        };

        return PartialView("_ContractDetailsModalContent", model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var contract = await _context.HopDongs
            .AsNoTracking()
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(c => c.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(c => c.MaHopDong == id);

        if (contract == null || contract.NhanVien == null)
            return NotFound("Không tìm thấy thông tin hợp đồng để in.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysRemaining = contract.NgayKetThuc.HasValue
            ? (contract.NgayKetThuc.Value.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days
            : (int?)null;

        var model = new HopDongDetailsModalViewModel
        {
            Contract = contract,
            Employee = contract.NhanVien,
            DepartmentName = contract.NhanVien.PhongBan?.TenPhongBan ?? "Khách sạn The OverTime",
            PositionName = contract.NhanVien.ChucVu?.TenChucVu ?? "Nhân viên",
            DaysRemaining = daysRemaining
        };

        return View("Print", model);
    }


    private async Task ValidateContractAsync(HopDong model, int? currentId = null)
    {
        model.LoaiHopDong = model.LoaiHopDong?.Trim() ?? string.Empty;
        model.HinhThucHopDong = model.HinhThucHopDong?.Trim() ?? "CHINH_THUC";
        
        // Tự động sinh mã hợp đồng nếu để trống
        if (string.IsNullOrWhiteSpace(model.SoHopDong))
        {
            model.SoHopDong = await GenerateUniqueContractNumberAsync(model.HinhThucHopDong);
        }
        else
        {
            model.SoHopDong = model.SoHopDong.Trim().ToUpper();
        }

        if (string.IsNullOrWhiteSpace(model.LoaiHopDong)) ModelState.AddModelError("Contract.LoaiHopDong", "Vui lòng nhập loại hợp đồng.");
        if (model.NgayKetThuc.HasValue && model.NgayKetThuc < model.NgayBatDau) ModelState.AddModelError("Contract.NgayKetThuc", "Ngày kết thúc phải sau ngày bắt đầu.");
        if (model.LuongCoBan < 0) ModelState.AddModelError("Contract.LuongCoBan", "Lương cơ bản không được âm.");
        if (await _context.HopDongs.AnyAsync(item => item.MaHopDong != currentId && item.SoHopDong.ToLower() == model.SoHopDong.ToLower()))
        {
            ModelState.AddModelError("Contract.SoHopDong", $"Mã / Số hợp đồng '{model.SoHopDong}' đã tồn tại trong hệ thống. Vui lòng bấm 'Tạo mã mới' để nhận mã ngẫu nhiên duy nhất.");
        }

        // Chặn trùng lặp / chồng chéo thời gian hợp đồng cho cùng 1 nhân sự
        if (model.MaNhanVien > 0)
        {
            var otherContracts = await _context.HopDongs
                .AsNoTracking()
                .Where(item => item.MaNhanVien == model.MaNhanVien && item.MaHopDong != currentId && item.TrangThai != "DA_CHAM_DUT")
                .ToListAsync();

            foreach (var other in otherContracts)
            {
                bool overlap = false;
                var mStart = model.NgayBatDau;
                var mEnd = model.NgayKetThuc;
                var oStart = other.NgayBatDau;
                var oEnd = other.NgayKetThuc;

                if (!mEnd.HasValue && !oEnd.HasValue)
                {
                    overlap = true;
                }
                else if (!mEnd.HasValue)
                {
                    overlap = oEnd >= mStart;
                }
                else if (!oEnd.HasValue)
                {
                    overlap = mEnd >= oStart;
                }
                else
                {
                    overlap = (mStart <= oEnd.Value) && (oStart <= mEnd.Value);
                }

                if (overlap)
                {
                    var oEndStr = other.NgayKetThuc.HasValue ? other.NgayKetThuc.Value.ToString("dd/MM/yyyy") : "Không thời hạn";
                    ModelState.AddModelError("Contract.NgayBatDau",
                        $"Nhân sự này đã có hợp đồng '{other.SoHopDong}' ({other.NgayBatDau:dd/MM/yyyy} - {oEndStr}) đang có hiệu lực. Một nhân sự không thể có 2 hợp đồng trùng lặp thời gian.");
                    break;
                }
            }
        }
    }

    private async Task<string> GenerateUniqueContractNumberAsync(string? hinhThucHopDong)
    {
        var prefix = hinhThucHopDong == "THU_VIEC" ? "HDTV" : "HDLD";
        var year = DateTime.Now.Year;
        var random = new Random();

        // 1. Thử sinh số ngẫu nhiên 2 chữ số (01-99) có kiểm tra tồn tại
        for (int i = 0; i < 50; i++)
        {
            var randomNum = random.Next(1, 100);
            var candidate = $"{prefix}-{year}/{randomNum:D2}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
        }

        // 2. Thử sinh số ngẫu nhiên 3 chữ số (100-999) có kiểm tra tồn tại
        for (int i = 0; i < 50; i++)
        {
            var randomNum = random.Next(100, 1000);
            var candidate = $"{prefix}-{year}/{randomNum:D3}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
        }

        // 3. Fallback an toàn tuyệt đối: quét tuần tự đảm bảo luôn tìm được mã duy nhất không trùng lặp
        int seq = 1;
        while (true)
        {
            var candidate = $"{prefix}-{year}/{seq:D3}/OTH";
            var exists = await _context.HopDongs.AnyAsync(h => h.SoHopDong.ToLower() == candidate.ToLower());
            if (!exists)
            {
                return candidate;
            }
            seq++;
        }
    }

    private async Task<IReadOnlyList<SelectListItem>> GetEmployeeOptionsAsync() =>
        await _context.NhanViens.AsNoTracking().OrderBy(item => item.Ten).ThenBy(item => item.Ho).Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToListAsync();

    private async Task ValidateOrganizationAsync(HopDongFormViewModel model)
    {
        if (!model.DepartmentId.HasValue || !await _context.PhongBans.AnyAsync(x => x.MaPhongBan == model.DepartmentId))
            ModelState.AddModelError("DepartmentId", "Vui lòng chọn phòng ban hợp lệ.");
        if (!model.PositionId.HasValue || !await _context.ChucVus.AnyAsync(x => x.MaChucVu == model.PositionId && (x.MaPhongBan == null || x.MaPhongBan == model.DepartmentId)))
            ModelState.AddModelError("PositionId", "Vui lòng chọn chức vụ thuộc phòng ban đã chọn.");
        if (!string.IsNullOrWhiteSpace(model.PersonalEmail) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.PersonalEmail))
            ModelState.AddModelError("PersonalEmail", "Email cá nhân không đúng định dạng.");
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

        var baseEmail = string.IsNullOrEmpty(cleanHo)
            ? $"{cleanTen}@overtimehotel.com"
            : $"{cleanTen}.{cleanHo}@overtimehotel.com";

        if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == baseEmail.ToLower()))
        {
            return baseEmail;
        }

        int counter = 1;
        while (true)
        {
            var candidate = string.IsNullOrEmpty(cleanHo)
                ? $"{cleanTen}{counter}@overtimehotel.com"
                : $"{cleanTen}.{cleanHo}{counter}@overtimehotel.com";
            if (!await _context.NhanViens.AnyAsync(e => e.EmailCongVu.ToLower() == candidate.ToLower()))
            {
                return candidate;
            }
            counter++;
        }
    }
}
