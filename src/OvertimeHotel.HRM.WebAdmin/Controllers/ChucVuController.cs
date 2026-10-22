using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize]
public class ChucVuController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ChucVuController> _logger;

    public ChucVuController(AppDbContext context, ILogger<ChucVuController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Index(string? keyword, int? departmentId)
    {
        keyword = keyword?.Trim();
        var query = _context.ChucVus.AsNoTracking()
            .Include(x => x.PhongBan)
            .Include(x => x.NhanViens)
            .AsQueryable();

        if (departmentId is > 0)
        {
            query = query.Where(x => x.MaPhongBan == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(x => x.TenChucVu.ToLower().Contains(value) ||
                (x.MoTa != null && x.MoTa.ToLower().Contains(value)) ||
                (x.PhongBan != null && x.PhongBan.TenPhongBan.ToLower().Contains(value)));
        }

        ViewBag.Keyword = keyword;
        ViewBag.DepartmentId = departmentId;
        await LoadDepartmentsToViewBagAsync(departmentId);

        try
        {
            ViewBag.Total = await _context.ChucVus.CountAsync();
            return View(await query.OrderBy(x => x.TenChucVu).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL. Hiển thị dữ liệu chức vụ minh họa.");
            var fallbackItems = GetFallbackItems();
            if (departmentId is > 0)
                fallbackItems = fallbackItems.Where(x => x.MaPhongBan == departmentId.Value).ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
                fallbackItems = fallbackItems.Where(x =>
                    x.TenChucVu.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (x.MoTa?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.PhongBan?.TenPhongBan.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            ViewBag.Total = GetFallbackItems().Count;
            ViewBag.IsDemoData = true;
            return View(fallbackItems);
        }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Create()
    {
        await LoadDepartmentsToViewBagAsync();
        return View(new ChucVu());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Create(ChucVu model)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadDepartmentsToViewBagAsync(model.MaPhongBan);
            return View(model);
        }

        try
        {
            _context.ChucVus.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm chức vụ “{model.TenChucVu}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo chức vụ {PositionName}.", model.TenChucVu);
            ModelState.AddModelError(string.Empty, "Không thể thêm chức vụ. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            await LoadDepartmentsToViewBagAsync(model.MaPhongBan);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.ChucVus.AsNoTracking().FirstOrDefaultAsync(x => x.MaChucVu == id);
        if (model == null) return NotFound();
        await LoadDepartmentsToViewBagAsync(model.MaPhongBan);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Edit(int id, ChucVu model)
    {
        if (id != model.MaChucVu) return BadRequest();
        Normalize(model);
        await ValidateAsync(model, id);
        if (!ModelState.IsValid)
        {
            await LoadDepartmentsToViewBagAsync(model.MaPhongBan);
            return View(model);
        }

        var entity = await _context.ChucVus.FindAsync(id);
        if (entity == null) return NotFound();

        entity.TenChucVu = model.TenChucVu;
        entity.MaPhongBan = model.MaPhongBan;
        entity.MoTa = model.MoTa;
        entity.CheDoLamViec = model.CheDoLamViec;
        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật chức vụ “{entity.TenChucVu}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật chức vụ {PositionId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật chức vụ. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            await LoadDepartmentsToViewBagAsync(model.MaPhongBan);
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.ManageCatalog)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.ChucVus.Include(x => x.NhanViens).FirstOrDefaultAsync(x => x.MaChucVu == id);
        if (entity == null) return NotFound();

        if (entity.NhanViens.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{entity.TenChucVu}” vì đang có {entity.NhanViens.Count} nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.ChucVus.Remove(entity);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa chức vụ “{entity.TenChucVu}”.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa chức vụ {PositionId}.", id);
            TempData["Error"] = "Không thể xóa chức vụ vì dữ liệu đang được sử dụng.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// API cung cấp danh sách chức vụ theo phòng ban phục vụ Cascading Dropdown
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByDepartment(int? departmentId)
    {
        try
        {
            var query = _context.ChucVus.AsNoTracking().AsQueryable();
            if (departmentId is > 0)
            {
                // Trả về các chức vụ thuộc phòng ban được chọn hoặc chức danh dùng chung (MaPhongBan == null)
                query = query.Where(x => x.MaPhongBan == departmentId.Value || x.MaPhongBan == null);
            }

            var list = await query
                .OrderBy(x => x.TenChucVu)
                .Select(x => new
                {
                    maChucVu = x.MaChucVu,
                    tenChucVu = x.TenChucVu,
                    maPhongBan = x.MaPhongBan,
                    tenPhongBan = x.PhongBan != null ? x.PhongBan.TenPhongBan : null
                })
                .ToListAsync();

            return Json(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tải danh sách chức vụ theo phòng ban {DepartmentId}.", departmentId);
            return Json(GetFallbackItems()
                .Where(x => !departmentId.HasValue || x.MaPhongBan == departmentId || x.MaPhongBan == null)
                .Select(x => new
                {
                    maChucVu = x.MaChucVu,
                    tenChucVu = x.TenChucVu,
                    maPhongBan = x.MaPhongBan,
                    tenPhongBan = x.PhongBan?.TenPhongBan
                }));
        }
    }

    private async Task LoadDepartmentsToViewBagAsync(int? selectedId = null)
    {
        try
        {
            var depts = await _context.PhongBans.AsNoTracking().OrderBy(x => x.TenPhongBan).ToListAsync();
            ViewBag.PhongBans = depts;
        }
        catch
        {
            ViewBag.PhongBans = new List<PhongBan>
            {
                new() { MaPhongBan = 1, TenPhongBan = "Ban Giám Đốc" },
                new() { MaPhongBan = 2, TenPhongBan = "Bộ phận Tiền Sảnh" },
                new() { MaPhongBan = 3, TenPhongBan = "Bộ phận Buồng Phòng" },
                new() { MaPhongBan = 4, TenPhongBan = "Bộ phận Ẩm Thực & Bếp" },
                new() { MaPhongBan = 5, TenPhongBan = "Bộ phận Kỹ Thuật" },
                new() { MaPhongBan = 6, TenPhongBan = "Bộ phận An Ninh" },
                new() { MaPhongBan = 7, TenPhongBan = "Bộ phận Nhân Sự" },
                new() { MaPhongBan = 8, TenPhongBan = "Bộ phận Kế Toán" }
            };
        }
    }

    private static List<ChucVu> GetFallbackItems()
    {
        var pb1 = new PhongBan { MaPhongBan = 1, TenPhongBan = "Ban Giám Đốc" };
        var pb2 = new PhongBan { MaPhongBan = 2, TenPhongBan = "Bộ phận Tiền Sảnh" };
        var pb3 = new PhongBan { MaPhongBan = 3, TenPhongBan = "Bộ phận Buồng Phòng" };
        var pb7 = new PhongBan { MaPhongBan = 7, TenPhongBan = "Bộ phận Nhân Sự" };

        return
        [
            new() { MaChucVu = 1, MaPhongBan = 1, PhongBan = pb1, TenChucVu = "Tổng Giám Đốc", MoTa = "Điều hành toàn bộ hoạt động khách sạn.", NhanViens = new List<NhanVien> { new() { Ho = "Võ", Ten = "Sang" } } },
            new() { MaChucVu = 2, MaPhongBan = 7, PhongBan = pb7, TenChucVu = "Trưởng Phòng Nhân Sự", MoTa = "Quản lý nhân sự và chính sách lao động.", NhanViens = new List<NhanVien> { new() { Ho = "Nguyễn", Ten = "Cường" } } },
            new() { MaChucVu = 3, MaPhongBan = 2, PhongBan = pb2, TenChucVu = "Trưởng Bộ Phận Tiền Sảnh", MoTa = "Điều phối hoạt động tiền sảnh.", NhanViens = new List<NhanVien> { new() { Ho = "Phạm", Ten = "Hương" } } },
            new() { MaChucVu = 4, MaPhongBan = 2, PhongBan = pb2, TenChucVu = "Nhân Viên Lễ Tân", MoTa = "Đón tiếp và hỗ trợ khách hàng.", NhanViens = new List<NhanVien> { new() { Ho = "Hoàng", Ten = "Long" }, new() { Ho = "Ngô", Ten = "Bảo" }, new() { Ho = "Vũ", Ten = "Tú" } } },
            new() { MaChucVu = 5, MaPhongBan = 3, PhongBan = pb3, TenChucVu = "Nhân Viên Buồng Phòng", MoTa = "Đảm bảo chất lượng phòng lưu trú.", NhanViens = new List<NhanVien> { new() { Ho = "Bùi", Ten = "Minh" }, new() { Ho = "Đinh", Ten = "Loan" } } }
        ];
    }

    private async Task ValidateAsync(ChucVu model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.TenChucVu))
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ là bắt buộc.");
        else if (model.TenChucVu.Length > 100)
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ không vượt quá 100 ký tự.");
        else if (await _context.ChucVus.AnyAsync(x =>
                     x.MaChucVu != currentId &&
                     x.TenChucVu.ToLower() == model.TenChucVu.ToLower() &&
                     x.MaPhongBan == model.MaPhongBan))
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ này đã tồn tại trong cùng phòng ban.");

        if (model.MaPhongBan.HasValue && !await _context.PhongBans.AnyAsync(x => x.MaPhongBan == model.MaPhongBan.Value))
            ModelState.AddModelError(nameof(model.MaPhongBan), "Phòng ban được chọn không tồn tại.");

        if (model.MoTa?.Length > 255)
            ModelState.AddModelError(nameof(model.MoTa), "Mô tả không vượt quá 255 ký tự.");

        // Đổi chế độ làm việc sẽ khoá/mở việc xếp ca của toàn bộ nhân sự giữ chức
        // danh này, nên phải là giá trị hợp lệ — không để chuỗi lạ lọt xuống CSDL.
        if (!ShiftWorkModes.IsValid(model.CheDoLamViec))
            ModelState.AddModelError(nameof(model.CheDoLamViec), "Chế độ làm việc không hợp lệ.");
    }

    private static void Normalize(ChucVu model)
    {
        model.TenChucVu = model.TenChucVu?.Trim() ?? string.Empty;
        model.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa.Trim();
    }
}