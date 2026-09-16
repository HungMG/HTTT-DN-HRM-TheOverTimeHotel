using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class CauHinhKhoanLuongController : Controller
{
    private static readonly string[] SupportedTypes = ["PHU_CAP", "THUONG", "KHAU_TRU"];
    private readonly AppDbContext _context;
    private readonly ILogger<CauHinhKhoanLuongController> _logger;

    public CauHinhKhoanLuongController(AppDbContext context, ILogger<CauHinhKhoanLuongController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, string? type)
    {
        keyword = keyword?.Trim();
        type = type?.Trim().ToUpperInvariant();
        var query = _context.CauHinhKhoanLuongs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(x => x.TenKhoan.ToLower().Contains(value) ||
                (x.MoTa != null && x.MoTa.ToLower().Contains(value)));
        }

        if (SupportedTypes.Contains(type))
            query = query.Where(x => x.LoaiKhoan == type);

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        try
        {
            ViewBag.Total = await _context.CauHinhKhoanLuongs.CountAsync();
            return View(await query.OrderBy(x => x.LoaiKhoan).ThenBy(x => x.TenKhoan).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL. Hiển thị dữ liệu khoản lương minh họa.");
            var fallbackItems = GetFallbackItems();
            if (!string.IsNullOrWhiteSpace(keyword))
                fallbackItems = fallbackItems.Where(x =>
                    x.TenKhoan.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (x.MoTa?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            if (SupportedTypes.Contains(type))
                fallbackItems = fallbackItems.Where(x => x.LoaiKhoan == type).ToList();

            ViewBag.Total = GetFallbackItems().Count;
            ViewBag.IsDemoData = true;
            return View(fallbackItems);
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new CauHinhKhoanLuong());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CauHinhKhoanLuong model)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid) return View(model);

        try
        {
            _context.CauHinhKhoanLuongs.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm khoản lương “{model.TenKhoan}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo khoản lương {PayrollItemName}.", model.TenKhoan);
            ModelState.AddModelError(string.Empty, "Không thể thêm khoản lương. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.CauHinhKhoanLuongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKhoan == id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CauHinhKhoanLuong model)
    {
        if (id != model.MaKhoan) return BadRequest();
        Normalize(model);
        await ValidateAsync(model, id);
        if (!ModelState.IsValid) return View(model);

        var entity = await _context.CauHinhKhoanLuongs.FindAsync(id);
        if (entity == null) return NotFound();

        entity.TenKhoan = model.TenKhoan;
        entity.LoaiKhoan = model.LoaiKhoan;
        entity.GiaTriMacDinh = model.GiaTriMacDinh;
        entity.MoTa = model.MoTa;
        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật khoản lương “{entity.TenKhoan}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật khoản lương {PayrollItemId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật khoản lương. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.CauHinhKhoanLuongs.Include(x => x.ChiTietPhieuLuongs)
            .FirstOrDefaultAsync(x => x.MaKhoan == id);
        if (entity == null) return NotFound();

        if (entity.ChiTietPhieuLuongs.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{entity.TenKhoan}” vì đã được sử dụng trong phiếu lương.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.CauHinhKhoanLuongs.Remove(entity);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa khoản lương “{entity.TenKhoan}”.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa khoản lương {PayrollItemId}.", id);
            TempData["Error"] = "Không thể xóa khoản lương vì dữ liệu đang được sử dụng.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<CauHinhKhoanLuong> GetFallbackItems() =>
    [
        new() { MaKhoan = 1, TenKhoan = "Phụ cấp ca đêm", LoaiKhoan = "PHU_CAP", GiaTriMacDinh = 500000m, MoTa = "Phụ cấp cho nhân viên làm ca qua đêm." },
        new() { MaKhoan = 2, TenKhoan = "Phụ cấp ăn ca", LoaiKhoan = "PHU_CAP", GiaTriMacDinh = 730000m, MoTa = "Hỗ trợ chi phí ăn uống trong ca làm việc." },
        new() { MaKhoan = 3, TenKhoan = "Thưởng chuyên cần", LoaiKhoan = "THUONG", GiaTriMacDinh = 1000000m, MoTa = "Áp dụng cho nhân viên đi làm đầy đủ trong tháng." },
        new() { MaKhoan = 4, TenKhoan = "Phạt đi trễ", LoaiKhoan = "KHAU_TRU", GiaTriMacDinh = 50000m, MoTa = "Khấu trừ theo mỗi lần đi trễ không có lý do." }
    ];

    private async Task ValidateAsync(CauHinhKhoanLuong model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.TenKhoan))
            ModelState.AddModelError(nameof(model.TenKhoan), "Tên khoản là bắt buộc.");
        else if (model.TenKhoan.Length > 150)
            ModelState.AddModelError(nameof(model.TenKhoan), "Tên khoản không vượt quá 150 ký tự.");
        else if (await _context.CauHinhKhoanLuongs.AnyAsync(x =>
                     x.MaKhoan != currentId && x.TenKhoan.ToLower() == model.TenKhoan.ToLower()))
            ModelState.AddModelError(nameof(model.TenKhoan), "Tên khoản lương đã tồn tại.");

        if (!SupportedTypes.Contains(model.LoaiKhoan))
            ModelState.AddModelError(nameof(model.LoaiKhoan), "Loại khoản không hợp lệ.");
        if (model.GiaTriMacDinh < 0)
            ModelState.AddModelError(nameof(model.GiaTriMacDinh), "Giá trị mặc định không được âm.");
        if (model.MoTa?.Length > 255)
            ModelState.AddModelError(nameof(model.MoTa), "Mô tả không vượt quá 255 ký tự.");
    }

    private static void Normalize(CauHinhKhoanLuong model)
    {
        model.TenKhoan = model.TenKhoan?.Trim() ?? string.Empty;
        model.LoaiKhoan = model.LoaiKhoan?.Trim().ToUpperInvariant() ?? string.Empty;
        model.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa.Trim();
    }
}