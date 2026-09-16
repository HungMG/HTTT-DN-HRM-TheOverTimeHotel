using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class CaLamViecController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<CaLamViecController> _logger;

    public CaLamViecController(AppDbContext context, ILogger<CaLamViecController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword)
    {
        keyword = keyword?.Trim();
        var query = _context.CaLamViecs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(x => x.TenCa.ToLower().Contains(value));
        }

        ViewBag.Keyword = keyword;
        try
        {
            ViewBag.Total = await _context.CaLamViecs.CountAsync();
            return View(await query.OrderBy(x => x.GioBatDau).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL. Hiển thị dữ liệu ca làm việc minh họa.");
            var fallbackItems = GetFallbackItems();
            if (!string.IsNullOrWhiteSpace(keyword))
                fallbackItems = fallbackItems.Where(x =>
                    x.TenCa.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

            ViewBag.Total = GetFallbackItems().Count;
            ViewBag.IsDemoData = true;
            return View(fallbackItems);
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new CaLamViec
    {
        GioBatDau = new TimeOnly(8, 0),
        GioKetThuc = new TimeOnly(16, 0)
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CaLamViec model)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid) return View(model);

        try
        {
            _context.CaLamViecs.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm ca làm việc “{model.TenCa}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo ca làm việc {ShiftName}.", model.TenCa);
            ModelState.AddModelError(string.Empty, "Không thể thêm ca làm việc. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.CaLamViecs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCa == id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CaLamViec model)
    {
        if (id != model.MaCa) return BadRequest();
        Normalize(model);
        await ValidateAsync(model, id);
        if (!ModelState.IsValid) return View(model);

        var entity = await _context.CaLamViecs.FindAsync(id);
        if (entity == null) return NotFound();

        entity.TenCa = model.TenCa;
        entity.GioBatDau = model.GioBatDau;
        entity.GioKetThuc = model.GioKetThuc;
        entity.QuaDem = model.QuaDem;
        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật ca làm việc “{entity.TenCa}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật ca làm việc {ShiftId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật ca làm việc. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.CaLamViecs.Include(x => x.PhanCas).FirstOrDefaultAsync(x => x.MaCa == id);
        if (entity == null) return NotFound();

        if (entity.PhanCas.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{entity.TenCa}” vì đang có {entity.PhanCas.Count} lịch phân ca.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.CaLamViecs.Remove(entity);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa ca làm việc “{entity.TenCa}”.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa ca làm việc {ShiftId}.", id);
            TempData["Error"] = "Không thể xóa ca làm việc vì dữ liệu đang được sử dụng.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<CaLamViec> GetFallbackItems() =>
    [
        new() { MaCa = 1, TenCa = "Ca Sáng", GioBatDau = new TimeOnly(6, 0), GioKetThuc = new TimeOnly(14, 0), QuaDem = false },
        new() { MaCa = 2, TenCa = "Ca Chiều", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0), QuaDem = false },
        new() { MaCa = 3, TenCa = "Ca Đêm", GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6, 0), QuaDem = true }
    ];

    private async Task ValidateAsync(CaLamViec model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.TenCa))
            ModelState.AddModelError(nameof(model.TenCa), "Tên ca là bắt buộc.");
        else if (model.TenCa.Length > 50)
            ModelState.AddModelError(nameof(model.TenCa), "Tên ca không vượt quá 50 ký tự.");
        else if (await _context.CaLamViecs.AnyAsync(x =>
                     x.MaCa != currentId && x.TenCa.ToLower() == model.TenCa.ToLower()))
            ModelState.AddModelError(nameof(model.TenCa), "Tên ca đã tồn tại.");

        if (model.GioBatDau == model.GioKetThuc)
            ModelState.AddModelError(nameof(model.GioKetThuc), "Giờ kết thúc phải khác giờ bắt đầu.");

        var crossesMidnight = model.GioKetThuc < model.GioBatDau;
        if (crossesMidnight != model.QuaDem)
            ModelState.AddModelError(nameof(model.QuaDem),
                crossesMidnight
                    ? "Ca có giờ kết thúc nhỏ hơn giờ bắt đầu phải được đánh dấu qua đêm."
                    : "Ca không qua ngày mới không nên được đánh dấu qua đêm.");
    }

    private static void Normalize(CaLamViec model)
    {
        model.TenCa = model.TenCa?.Trim() ?? string.Empty;
    }
}