using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class PhongBanController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<PhongBanController> _logger;

    public PhongBanController(AppDbContext context, ILogger<PhongBanController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword)
    {
        var query = _context.PhongBans.AsNoTracking().Include(item => item.NhanViens).AsQueryable();
        keyword = keyword?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.ToLower();
            query = query.Where(item =>
                item.TenPhongBan.ToLower().Contains(normalizedKeyword) ||
                (item.MoTa != null && item.MoTa.ToLower().Contains(normalizedKeyword)));
        }

        ViewBag.Keyword = keyword;

        try
        {
            ViewBag.Total = await _context.PhongBans.CountAsync();
            return View(await query.OrderBy(item => item.TenPhongBan).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL. Hiển thị dữ liệu phòng ban minh họa.");
            var fallbackItems = GetFallbackItems();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                fallbackItems = fallbackItems.Where(item =>
                    item.TenPhongBan.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (item.MoTa?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            ViewBag.Total = GetFallbackItems().Count;
            ViewBag.IsDemoData = true;
            return View(fallbackItems);
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new PhongBan());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PhongBan model)
    {
        Normalize(model);
        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            _context.PhongBans.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm phòng ban “{model.TenPhongBan}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo phòng ban {DepartmentName}.", model.TenPhongBan);
            ModelState.AddModelError(string.Empty, "Không thể thêm phòng ban. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _context.PhongBans.AsNoTracking()
            .FirstOrDefaultAsync(item => item.MaPhongBan == id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PhongBan model)
    {
        if (id != model.MaPhongBan)
        {
            return BadRequest();
        }

        Normalize(model);
        await ValidateAsync(model, id);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _context.PhongBans.FindAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        entity.TenPhongBan = model.TenPhongBan;
        entity.MoTa = model.MoTa;

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật phòng ban “{entity.TenPhongBan}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật phòng ban {DepartmentId}.", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật phòng ban. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.PhongBans
            .Include(item => item.NhanViens)
            .FirstOrDefaultAsync(item => item.MaPhongBan == id);

        if (entity == null)
        {
            return NotFound();
        }

        if (entity.NhanViens.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{entity.TenPhongBan}” vì đang có {entity.NhanViens.Count} nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.PhongBans.Remove(entity);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa phòng ban “{entity.TenPhongBan}”.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa phòng ban {DepartmentId}.", id);
            TempData["Error"] = "Không thể xóa phòng ban vì dữ liệu đang được sử dụng.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<PhongBan> GetFallbackItems() =>
    [
        new() { MaPhongBan = 1, TenPhongBan = "Ban Giám Đốc", MoTa = "Điều hành chiến lược và hoạt động toàn khách sạn.", NhanViens = new List<NhanVien> { new() { Ho = "Võ", Ten = "Sang" }, new() { Ho = "Nguyễn", Ten = "Cường" } } },
        new() { MaPhongBan = 2, TenPhongBan = "Phòng Nhân Sự", MoTa = "Quản trị nhân sự, hợp đồng và chính sách lao động.", NhanViens = new List<NhanVien> { new() { Ho = "Trần", Ten = "Hoa" }, new() { Ho = "Lê", Ten = "Nam" }, new() { Ho = "Đỗ", Ten = "Mai" } } },
        new() { MaPhongBan = 3, TenPhongBan = "Bộ Phận Tiền Sảnh", MoTa = "Lễ tân, đặt phòng và chăm sóc khách hàng.", NhanViens = new List<NhanVien> { new() { Ho = "Phạm", Ten = "Hương" }, new() { Ho = "Hoàng", Ten = "Long" }, new() { Ho = "Ngô", Ten = "Bảo" }, new() { Ho = "Vũ", Ten = "Tú" } } },
        new() { MaPhongBan = 4, TenPhongBan = "Bộ Phận Buồng Phòng", MoTa = "Đảm bảo tiêu chuẩn vệ sinh và chất lượng phòng.", NhanViens = new List<NhanVien> { new() { Ho = "Bùi", Ten = "Minh" }, new() { Ho = "Đinh", Ten = "Loan" }, new() { Ho = "Lý", Ten = "Quân" } } },
        new() { MaPhongBan = 5, TenPhongBan = "Bộ Phận Ẩm Thực", MoTa = "Vận hành nhà hàng, bếp và dịch vụ tiệc.", NhanViens = new List<NhanVien> { new() { Ho = "Dương", Ten = "Tuấn" }, new() { Ho = "Lâm", Ten = "Trí" }, new() { Ho = "Đoàn", Ten = "Hậu" }, new() { Ho = "Phan", Ten = "Việt" } } }
    ];

    private async Task ValidateAsync(PhongBan model, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(model.TenPhongBan))
        {
            ModelState.AddModelError(nameof(model.TenPhongBan), "Tên phòng ban là bắt buộc.");
        }
        else if (model.TenPhongBan.Length > 150)
        {
            ModelState.AddModelError(nameof(model.TenPhongBan), "Tên phòng ban không vượt quá 150 ký tự.");
        }
        else if (await _context.PhongBans.AnyAsync(item =>
                     item.MaPhongBan != currentId && item.TenPhongBan.ToLower() == model.TenPhongBan.ToLower()))
        {
            ModelState.AddModelError(nameof(model.TenPhongBan), "Tên phòng ban đã tồn tại.");
        }

        if (model.MoTa?.Length > 255)
        {
            ModelState.AddModelError(nameof(model.MoTa), "Mô tả không vượt quá 255 ký tự.");
        }
    }

    private static void Normalize(PhongBan model)
    {
        model.TenPhongBan = model.TenPhongBan?.Trim() ?? string.Empty;
        model.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa.Trim();
    }
}