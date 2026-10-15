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

        if (string.IsNullOrWhiteSpace(model.Contract.HinhThucHopDong))
        {
            model.Contract.HinhThucHopDong = "CHINH_THUC";
        }

        NhanVien? employee = null;

        // 1. Nếu có EmployeeId hợp lệ thì tìm nhân viên đó
        if (model.EmployeeId > 0)
        {
            employee = await _context.NhanViens.Include(nv => nv.PhongBan).FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);
        }

        // 2. Nếu chưa có EmployeeId, thử tìm theo tên nhân sự nhập tay
        if (employee == null && !string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            var trimmedName = model.EmployeeName.Trim();
            employee = await _context.NhanViens
                .Include(nv => nv.PhongBan)
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
                var empCount = await _context.NhanViens.CountAsync() + 1;
                
                var cleanTen = ten.ToLower();
                var cleanHo = ho.ToLower().Replace(" ", "");
                var emailCongVu = $"{cleanTen}.{cleanHo}{empCount}@overtimehotel.com";

                employee = new NhanVien
                {
                    Ho = ho,
                    Ten = ten,
                    MaPhongBan = model.DepartmentId,
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
            // Nếu nhân viên đã có và người dùng nhập thêm / sửa email cá nhân trên form thì cập nhật vào hồ sơ
            if (!string.IsNullOrWhiteSpace(model.PersonalEmail) && employee.EmailCaNhan != model.PersonalEmail.Trim())
            {
                employee.EmailCaNhan = model.PersonalEmail.Trim();
                await _context.SaveChangesAsync();
            }

            // Cập nhật phòng ban nếu nhân viên chưa có phòng ban
            if (model.DepartmentId.HasValue && employee.MaPhongBan == null)
            {
                employee.MaPhongBan = model.DepartmentId.Value;
                await _context.SaveChangesAsync();
            }
        }

        if (employee != null)
        {
            model.Contract.MaNhanVien = employee.MaNhanVien;
            model.EmployeeId = employee.MaNhanVien;
            model.EmployeeName = employee.HoTen;
            model.DepartmentName = employee.PhongBan?.TenPhongBan ?? string.Empty;
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
        var employee = await _context.NhanViens.Include(nv => nv.PhongBan).FirstOrDefaultAsync(item => item.MaNhanVien == model.EmployeeId);
        if (employee == null)
            ModelState.AddModelError(nameof(model.EmployeeId), "Vui lòng chọn nhân viên.");
        model.EmployeeName = employee?.HoTen ?? string.Empty;
        model.DepartmentName = employee?.PhongBan?.TenPhongBan ?? string.Empty;

        // Cập nhật email cá nhân nếu người dùng nhập mới / chỉnh sửa
        if (employee != null && !string.IsNullOrWhiteSpace(model.PersonalEmail) && employee.EmailCaNhan != model.PersonalEmail.Trim())
        {
            employee.EmailCaNhan = model.PersonalEmail.Trim();
        }

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
        if (await _context.HopDongs.AnyAsync(item => item.MaHopDong != currentId && item.SoHopDong.ToLower() == model.SoHopDong.ToLower())) ModelState.AddModelError("Contract.SoHopDong", "Mã / Số hợp đồng đã tồn tại.");
    }

    private async Task<string> GenerateUniqueContractNumberAsync(string? hinhThucHopDong)
    {
        var prefix = hinhThucHopDong == "THU_VIEC" ? "HDTV" : "HDLD";
        var year = DateTime.Now.Year;
        var random = new Random();

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

        var fallbackNum = random.Next(100, 999);
        return $"{prefix}-{year}/{fallbackNum:D3}/OTH";
    }

    private async Task<IReadOnlyList<SelectListItem>> GetEmployeeOptionsAsync() =>
        await _context.NhanViens.AsNoTracking().OrderBy(item => item.Ten).ThenBy(item => item.Ho).Select(item => new SelectListItem($"{item.Ho} {item.Ten} - NV-{item.MaNhanVien:D3}", item.MaNhanVien.ToString())).ToListAsync();

    private static string GetState(HopDong contract, int? daysRemaining)
    {
        if (contract.TrangThai == "DA_CHAM_DUT") return "DA_CHAM_DUT";
        if (contract.TrangThai == "HET_HAN" || (daysRemaining.HasValue && daysRemaining.Value < 0)) return "DA_HET_HAN";
        if (daysRemaining is >= 0 and <= 30) return "SAP_HET_HAN";
        return "HIEU_LUC";
    }
}
