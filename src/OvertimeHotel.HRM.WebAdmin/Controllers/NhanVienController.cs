using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class NhanVienController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<NhanVienController> _logger;

    public NhanVienController(AppDbContext context, ILogger<NhanVienController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, int? departmentId, string? status, string? contractFilter)
    {
        keyword = keyword?.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status;
        contractFilter = string.IsNullOrWhiteSpace(contractFilter) ? null : contractFilter;

        var query = _context.NhanViens
            .AsNoTracking()
            .Include(employee => employee.PhongBan)
            .Include(employee => employee.ChucVu)
            .Include(employee => employee.HopDongs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(employee =>
                employee.Ho.ToLower().Contains(value) ||
                employee.Ten.ToLower().Contains(value) ||
                employee.Email.ToLower().Contains(value) ||
                employee.DienThoai.Contains(value));
        }

        if (departmentId is > 0)
            query = query.Where(employee => employee.MaPhongBan == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(employee => employee.TrangThai == status);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var employees = await query
            .OrderBy(employee => employee.Ten)
            .ThenBy(employee => employee.Ho)
            .ToListAsync();

        var items = employees.Select(employee =>
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

        var result = items.ToList();
        var allEmployees = await _context.NhanViens.AsNoTracking().Include(employee => employee.HopDongs).ToListAsync();
        var allContractItems = allEmployees.Select(employee =>
        {
            var contract = employee.HopDongs.OrderByDescending(item => item.NgayBatDau).FirstOrDefault();
            var days = contract?.NgayKetThuc is DateOnly endDate ? endDate.DayNumber - today.DayNumber : (int?)null;
            return (contract, days);
        }).ToList();

        var model = new NhanVienIndexViewModel
        {
            CreateEmployeeForm = await BuildEmployeeFormAsync(new NhanVienFormViewModel
            {
                Employee = new NhanVien
                {
                    NgaySinh = DateOnly.FromDateTime(DateTime.Today.AddYears(-25)),
                    NgayVaoLam = DateOnly.FromDateTime(DateTime.Today),
                    TrangThai = "DANG_LAM"
                }
            }),
            Keyword = keyword,
            DepartmentId = departmentId,
            Status = status,
            ContractFilter = contractFilter,
            DepartmentOptions = await GetDepartmentOptionsAsync(departmentId),
            Employees = result,
            TotalEmployees = allEmployees.Count,
            WorkingEmployees = allEmployees.Count(employee => employee.TrangThai == "DANG_LAM"),
            ExpiringContracts = allContractItems.Count(item => item.days is >= 0 and <= 30),
            ExpiredContracts = allContractItems.Count(item => item.days < 0)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NhanVienFormViewModel model)
    {
        Normalize(model.Employee);
        await ValidateEmployeeAsync(model.Employee);
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(item => item.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin hồ sơ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.NhanViens.Add(model.Employee);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm hồ sơ nhân viên “{model.Employee.HoTen}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo hồ sơ nhân viên {Email}.", model.Employee.Email);
            TempData["Error"] = "Không thể thêm hồ sơ. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _context.NhanViens.AsNoTracking().FirstOrDefaultAsync(item => item.MaNhanVien == id);
        return employee == null ? NotFound() : View(await BuildEmployeeFormAsync(new NhanVienFormViewModel { Employee = employee }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NhanVienFormViewModel model)
    {
        if (id != model.Employee.MaNhanVien)
            return BadRequest();

        Normalize(model.Employee);
        await ValidateEmployeeAsync(model.Employee, id);
        if (!ModelState.IsValid)
            return View(await BuildEmployeeFormAsync(model));

        var employee = await _context.NhanViens.FindAsync(id);
        if (employee == null)
            return NotFound();

        employee.MaPhongBan = model.Employee.MaPhongBan;
        employee.MaChucVu = model.Employee.MaChucVu;
        employee.Ho = model.Employee.Ho;
        employee.Ten = model.Employee.Ten;
        employee.NgaySinh = model.Employee.NgaySinh;
        employee.GioiTinh = model.Employee.GioiTinh;
        employee.DienThoai = model.Employee.DienThoai;
        employee.Email = model.Employee.Email;
        employee.DiaChi = model.Employee.DiaChi;
        employee.TrinhDo = model.Employee.TrinhDo;
        employee.NgayVaoLam = model.Employee.NgayVaoLam;
        employee.NgayThoiViec = model.Employee.NgayThoiViec;
        employee.TrangThai = model.Employee.TrangThai;

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật hồ sơ “{employee.HoTen}”.";
            return RedirectToAction(nameof(Index));
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
        var employee = await _context.NhanViens
            .Include(item => item.TaiKhoan)
            .Include(item => item.PhieuLuongs)
            .FirstOrDefaultAsync(item => item.MaNhanVien == id);
        if (employee == null)
            return NotFound();

        if (employee.TaiKhoan != null || employee.PhieuLuongs.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{employee.HoTen}” vì hồ sơ đã phát sinh tài khoản hoặc phiếu lương. Hãy chuyển trạng thái sang ngừng làm việc.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.NhanViens.Remove(employee);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa hồ sơ “{employee.HoTen}”.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa hồ sơ nhân viên {EmployeeId}.", id);
            TempData["Error"] = "Không thể xóa hồ sơ vì dữ liệu đang được sử dụng.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<NhanVienFormViewModel> BuildEmployeeFormAsync(NhanVienFormViewModel model)
    {
        model.DepartmentOptions = await _context.PhongBans.AsNoTracking().OrderBy(item => item.TenPhongBan).Select(item => new SelectListItem(item.TenPhongBan, item.MaPhongBan.ToString(), item.MaPhongBan == model.Employee.MaPhongBan)).ToListAsync();
        model.PositionOptions = await _context.ChucVus.AsNoTracking().OrderBy(item => item.TenChucVu).Select(item => new SelectListItem(item.TenChucVu, item.MaChucVu.ToString(), item.MaChucVu == model.Employee.MaChucVu)).ToListAsync();
        return model;
    }

    private async Task<IReadOnlyList<SelectListItem>> GetDepartmentOptionsAsync(int? selectedId) =>
        await _context.PhongBans.AsNoTracking().OrderBy(item => item.TenPhongBan).Select(item => new SelectListItem(item.TenPhongBan, item.MaPhongBan.ToString(), item.MaPhongBan == selectedId)).ToListAsync();

    private async Task ValidateEmployeeAsync(NhanVien model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.Ho)) ModelState.AddModelError("Employee.Ho", "Vui lòng nhập họ.");
        if (string.IsNullOrWhiteSpace(model.Ten)) ModelState.AddModelError("Employee.Ten", "Vui lòng nhập tên.");
        if (model.MaPhongBan <= 0 || !await _context.PhongBans.AnyAsync(item => item.MaPhongBan == model.MaPhongBan)) ModelState.AddModelError("Employee.MaPhongBan", "Vui lòng chọn phòng ban hợp lệ.");
        if (model.MaChucVu <= 0 || !await _context.ChucVus.AnyAsync(item => item.MaChucVu == model.MaChucVu)) ModelState.AddModelError("Employee.MaChucVu", "Vui lòng chọn chức vụ hợp lệ.");
        if (model.NgaySinh >= DateOnly.FromDateTime(DateTime.Today)) ModelState.AddModelError("Employee.NgaySinh", "Ngày sinh phải trước ngày hiện tại.");
        if (model.NgayThoiViec.HasValue && model.NgayThoiViec < model.NgayVaoLam) ModelState.AddModelError("Employee.NgayThoiViec", "Ngày thôi việc không được trước ngày vào làm.");
        if (await _context.NhanViens.AnyAsync(item => item.MaNhanVien != currentId && item.Email.ToLower() == model.Email.ToLower())) ModelState.AddModelError("Employee.Email", "Email nhân viên đã tồn tại.");
    }

    private static void Normalize(NhanVien model)
    {
        model.Ho = model.Ho?.Trim() ?? string.Empty;
        model.Ten = model.Ten?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.DienThoai = model.DienThoai?.Trim() ?? string.Empty;
        model.DiaChi = string.IsNullOrWhiteSpace(model.DiaChi) ? null : model.DiaChi.Trim();
        model.TrinhDo = model.TrinhDo?.Trim() ?? string.Empty;
    }

    private static string GetContractState(HopDong? contract, int? daysRemaining)
    {
        if (contract == null) return "CHUA_CO";
        if (daysRemaining < 0) return "DA_HET_HAN";
        if (daysRemaining <= 30) return "SAP_HET_HAN";
        return contract.TrangThai;
    }

}
