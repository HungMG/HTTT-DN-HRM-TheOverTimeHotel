using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
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
    public async Task<IActionResult> Index(string? keyword)
    {
        keyword = keyword?.Trim();
        var query = _context.ChucVus.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(x => x.TenChucVu.ToLower().Contains(value) ||
                (x.MoTa != null && x.MoTa.ToLower().Contains(value)));
        }

        ViewBag.Keyword = keyword;
        try
        {
            ViewBag.Total = await _context.ChucVus.CountAsync();
            return View(await query.OrderBy(x => x.TenChucVu).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL. Hiển thị dữ liệu chức vụ minh họa.");
            var fallbackItems = GetFallbackItems();
            if (!string.IsNullOrWhiteSpace(keyword))
                fallbackItems = fallbackItems.Where(x =>
                    x.TenChucVu.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (x.MoTa?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            ViewBag.Total = GetFallbackItems().Count;
            ViewBag.IsDemoData = true;
            return View(fallbackItems);
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new ChucVu());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChucVu model)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid) return View(model);

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
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.ChucVus.AsNoTracking().FirstOrDefaultAsync(x => x.MaChucVu == id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ChucVu model)
    {
        if (id != model.MaChucVu) return BadRequest();
        Normalize(model);
        await ValidateAsync(model, id);
        if (!ModelState.IsValid) return View(model);

        var entity = await _context.ChucVus.FindAsync(id);
        if (entity == null) return NotFound();

        entity.TenChucVu = model.TenChucVu;
        entity.MoTa = model.MoTa;
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
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
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

    private static List<ChucVu> GetFallbackItems() =>
    [
        new() { MaChucVu = 1, TenChucVu = "Tổng Giám Đốc", MoTa = "Điều hành toàn bộ hoạt động khách sạn." },
        new() { MaChucVu = 2, TenChucVu = "Trưởng Phòng Nhân Sự", MoTa = "Quản lý nhân sự và chính sách lao động." },
        new() { MaChucVu = 3, TenChucVu = "Trưởng Bộ Phận Tiền Sảnh", MoTa = "Điều phối hoạt động tiền sảnh." },
        new() { MaChucVu = 4, TenChucVu = "Nhân Viên Lễ Tân", MoTa = "Đón tiếp và hỗ trợ khách hàng." },
        new() { MaChucVu = 5, TenChucVu = "Nhân Viên Buồng Phòng", MoTa = "Đảm bảo chất lượng phòng lưu trú." }
    ];

    private async Task ValidateAsync(ChucVu model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.TenChucVu))
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ là bắt buộc.");
        else if (model.TenChucVu.Length > 100)
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ không vượt quá 100 ký tự.");
        else if (await _context.ChucVus.AnyAsync(x =>
                     x.MaChucVu != currentId && x.TenChucVu.ToLower() == model.TenChucVu.ToLower()))
            ModelState.AddModelError(nameof(model.TenChucVu), "Tên chức vụ đã tồn tại.");

        if (model.MoTa?.Length > 255)
            ModelState.AddModelError(nameof(model.MoTa), "Mô tả không vượt quá 255 ký tự.");
    }

    private static void Normalize(ChucVu model)
    {
        model.TenChucVu = model.TenChucVu?.Trim() ?? string.Empty;
        model.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa.Trim();
    }
}