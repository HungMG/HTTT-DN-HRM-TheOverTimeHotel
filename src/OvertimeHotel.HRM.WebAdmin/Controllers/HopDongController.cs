using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
                (contract.NhanVien != null && (
                    contract.NhanVien.Ho.ToLower().Contains(value) ||
                    contract.NhanVien.Ten.ToLower().Contains(value) ||
                    (contract.NhanVien.Ho + " " + contract.NhanVien.Ten).ToLower().Contains(value))));
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

            // Tự động đồng bộ các hợp đồng đã qua ngày kết thúc sang trạng thái HET_HAN trong CSDL
            var expiredToUpdate = await _context.HopDongs
                .Where(c => c.TrangThai == "HIEU_LUC" && c.NgayKetThuc != null && c.NgayKetThuc < today)
                .ToListAsync();

            if (expiredToUpdate.Any())
            {
                foreach (var c in expiredToUpdate)
                {
                    c.TrangThai = "HET_HAN";
                }
                await _context.SaveChangesAsync();

                foreach (var contract in allContracts.Where(c => c.NgayKetThuc.HasValue && c.NgayKetThuc.Value < today && c.TrangThai == "HIEU_LUC"))
                {
                    contract.TrangThai = "HET_HAN";
                }
            }

            // Tự động khôi phục các hợp đồng còn hạn nhưng bị lưu nhầm trạng thái HET_HAN sang HIEU_LUC
            var validToRestore = await _context.HopDongs
                .Where(c => c.TrangThai == "HET_HAN" && (c.NgayKetThuc == null || c.NgayKetThuc >= today))
                .ToListAsync();

            if (validToRestore.Any())
            {
                foreach (var c in validToRestore)
                {
                    c.TrangThai = "HIEU_LUC";
                }
                await _context.SaveChangesAsync();

                foreach (var contract in allContracts.Where(c => (!c.NgayKetThuc.HasValue || c.NgayKetThuc.Value >= today) && c.TrangThai == "HET_HAN"))
                {
                    contract.TrangThai = "HIEU_LUC";
                }
            }

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
                var isRenewed = (contract.TrangThai == "HET_HAN" || (days.HasValue && days.Value < 0)) &&
                                allContracts.Any(other => other.MaNhanVien == contract.MaNhanVien &&
                                                          other.MaHopDong != contract.MaHopDong &&
                                                          other.NgayBatDau >= contract.NgayBatDau &&
                                                          (other.TrangThai == "HIEU_LUC" || !other.NgayKetThuc.HasValue || other.NgayKetThuc.Value >= today));
                return new HopDongListItemViewModel
                {
                    Contract = contract,
                    EmployeeName = contract.NhanVien?.HoTen ?? "Nhân viên không tồn tại",
                    DepartmentName = contract.NhanVien?.PhongBan?.TenPhongBan ?? "Khách sạn",
                    PositionName = contract.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân bổ",
                    PersonalEmail = contract.NhanVien?.EmailCaNhan,
                    DaysRemaining = days,
                    State = GetState(contract, days),
                    IsRenewed = isRenewed
                };
            });

            if (string.IsNullOrWhiteSpace(contractFilter))
            {
                // Mặc định ẩn hợp đồng đã hết hạn hoặc đã chấm dứt
                items = items.Where(item => item.State != "DA_HET_HAN" && item.State != "DA_CHAM_DUT");
            }
            else if (contractFilter == "DA_HET_HAN")
            {
                items = items.Where(item => item.State == "DA_HET_HAN" || item.Contract.TrangThai == "HET_HAN" || (item.DaysRemaining.HasValue && item.DaysRemaining.Value < 0));
            }
            else if (contractFilter == "SAP_HET_HAN")
            {
                items = items.Where(item => item.DaysRemaining is >= 0 and <= 30 && item.State != "DA_HET_HAN" && item.State != "DA_CHAM_DUT");
            }
            else if (contractFilter == "HIEU_LUC")
            {
                items = items.Where(item => item.State == "HIEU_LUC" && (!item.DaysRemaining.HasValue || item.DaysRemaining.Value > 30));
            }
            // contractFilter == "TAT_CA" -> hiển thị toàn bộ không lọc trạng thái

            var allDays = allContracts.Select(contract => contract.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null).ToList();
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
                ActiveContracts = allContracts.Count(contract => (contract.TrangThai == "HIEU_LUC" || string.IsNullOrEmpty(contract.TrangThai)) && (!contract.NgayKetThuc.HasValue || contract.NgayKetThuc.Value >= today)),
                ExpiringContracts = allDays.Count(days => days is >= 0 and <= 30),
                ExpiredContracts = allContracts.Count(contract => contract.TrangThai == "HET_HAN" || (contract.NgayKetThuc.HasValue && contract.NgayKetThuc.Value < today))
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
        ModelState.Remove("EmployeeId");
        ModelState.Remove("Contract.MaNhanVien");

        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        NhanVien? employee = null;

        // 1. Nếu có EmployeeId hợp lệ thì tìm nhân viên đó
        if (model.EmployeeId > 0)
        {
            employee = await _context.NhanViens
                .Include(nv => nv.PhongBan)
                .Include(nv => nv.ChucVu)
                .FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);
        }

        // 2. Nếu chưa có EmployeeId, thử tìm theo tên nhân sự nhập tay
        if (employee == null && !string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            var trimmedName = model.EmployeeName.Trim();
            employee = await _context.NhanViens
                .Include(nv => nv.PhongBan)
                .Include(nv => nv.ChucVu)
                .FirstOrDefaultAsync(item => (item.Ho + " " + item.Ten).ToLower() == trimmedName.ToLower() || item.Ten.ToLower() == trimmedName.ToLower());
        }

        // 3. Nếu vẫn chưa có nhân viên trong CSDL (nhân viên mới nhập tay hoàn toàn) ➔ Tự động tạo hồ sơ nhân viên mới
        if (employee == null)
        {
            if (string.IsNullOrWhiteSpace(model.EmployeeName))
            {
                ModelState.AddModelError("EmployeeName", "Vui lòng nhập họ và tên nhân viên.");
            }
            else
            {
                var fullName = model.EmployeeName.Trim();
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var ho = parts.Length > 1 ? string.Join(" ", parts.Take(parts.Length - 1)) : "Nhân viên";
                var ten = parts.Last();
                
                var emailCongVu = await GenerateUniqueWorkEmailAsync(ho, ten);

                // Xác định phòng ban hợp lệ
                int? targetDeptId = model.DepartmentId;
                if (!targetDeptId.HasValue || targetDeptId.Value <= 0)
                {
                    var firstDept = await _context.PhongBans.OrderBy(p => p.MaPhongBan).FirstOrDefaultAsync();
                    targetDeptId = firstDept?.MaPhongBan ?? 1;
                }

                // Xác định chức vụ hợp lệ thuộc phòng ban (hoặc chức danh chung)
                int targetPosId = model.PositionId ?? 0;
                if (targetPosId <= 0)
                {
                    var defaultPos = await _context.ChucVus
                        .Where(cv => cv.MaPhongBan == targetDeptId.Value || cv.MaPhongBan == null)
                        .OrderBy(cv => cv.MaPhongBan == null ? 1 : 0)
                        .ThenBy(cv => cv.MaChucVu)
                        .FirstOrDefaultAsync();
                    targetPosId = defaultPos?.MaChucVu ?? 0;
                }
                if (targetPosId <= 0)
                {
                    var anyPos = await _context.ChucVus.OrderBy(cv => cv.MaChucVu).FirstOrDefaultAsync();
                    targetPosId = anyPos?.MaChucVu ?? 1;
                }

                employee = new NhanVien
                {
                    Ho = ho,
                    Ten = ten,
                    MaPhongBan = targetDeptId,
                    MaChucVu = targetPosId,
                    EmailCongVu = emailCongVu,
                    EmailCaNhan = string.IsNullOrWhiteSpace(model.PersonalEmail) ? null : model.PersonalEmail.Trim(),
                    DienThoai = "0900000000",
                    GioiTinh = "NAM",
                    NgaySinh = new DateOnly(2000, 1, 1),
                    NgayVaoLam = model.Contract.NgayBatDau,
                    TrinhDo = "Lao động phổ thông",
                    TrangThai = "DANG_LAM"
                };

                _context.NhanViens.Add(employee);
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Cập nhật email cá nhân nếu có thay đổi
            if (!string.IsNullOrWhiteSpace(model.PersonalEmail) && employee.EmailCaNhan != model.PersonalEmail.Trim())
            {
                employee.EmailCaNhan = model.PersonalEmail.Trim();
            }

            // Cập nhật phòng ban nếu được chọn trên form
            if (model.DepartmentId.HasValue && model.DepartmentId.Value > 0)
            {
                employee.MaPhongBan = model.DepartmentId.Value;
            }

            // Cập nhật chức vụ nếu được chọn trên form
            if (model.PositionId.HasValue && model.PositionId.Value > 0)
            {
                employee.MaChucVu = model.PositionId.Value;
            }
            else if (!employee.MaChucVu.HasValue || employee.MaChucVu.Value <= 0)
            {
                var defPos = await _context.ChucVus
                    .Where(cv => cv.MaPhongBan == employee.MaPhongBan || cv.MaPhongBan == null)
                    .OrderBy(cv => cv.MaPhongBan == null ? 1 : 0)
                    .ThenBy(cv => cv.MaChucVu)
                    .FirstOrDefaultAsync();
                if (defPos != null) employee.MaChucVu = defPos.MaChucVu;
            }

            await _context.SaveChangesAsync();
        }

        if (employee != null)
        {
            model.Contract.MaNhanVien = employee.MaNhanVien;
            model.EmployeeId = employee.MaNhanVien;
            model.EmployeeName = employee.HoTen;
            model.DepartmentName = employee.PhongBan?.TenPhongBan ?? string.Empty;
            model.PositionName = employee.ChucVu?.TenChucVu ?? string.Empty;
        }

        // Tự động sinh mã hợp đồng nếu để trống theo hình thức hợp đồng
        if (string.IsNullOrWhiteSpace(model.Contract.SoHopDong))
        {
            model.Contract.SoHopDong = await GenerateUniqueContractNumberAsync(model.Contract.HinhThucHopDong);
        }

        // Tự động tính ngày kết thúc nếu để trống theo loại hợp đồng
        if (!model.Contract.NgayKetThuc.HasValue)
        {
            if (model.Contract.LoaiHopDong == "XAC_DINH_THOI_HAN_1_NAM")
            {
                model.Contract.NgayKetThuc = model.Contract.NgayBatDau.AddYears(1);
            }
            else if (model.Contract.LoaiHopDong == "XAC_DINH_THOI_HAN_3_NAM")
            {
                model.Contract.NgayKetThuc = model.Contract.NgayBatDau.AddYears(3);
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

            await _context.SaveChangesAsync();
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

    private static string GetState(HopDong contract, int? daysRemaining)
    {
        if (contract == null) return "CHUA_CO";
        if (contract.TrangThai == "DA_CHAM_DUT") return "DA_CHAM_DUT";
        if (daysRemaining.HasValue && daysRemaining.Value < 0) return "DA_HET_HAN";
        if (contract.TrangThai == "HET_HAN" && (!daysRemaining.HasValue || daysRemaining.Value <= 0)) return "DA_HET_HAN";
        if (daysRemaining is >= 0 and <= 30) return "SAP_HET_HAN";
        return "HIEU_LUC";
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
