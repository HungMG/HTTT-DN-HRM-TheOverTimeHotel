using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.ManageCatalog)]
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
        // Khi người dùng bấm trực tiếp vào /CaLamViec hoặc Mẫu ca, tự động chuyển về popup trong trang Ca & Lịch làm việc
        if (Request.Headers.XRequestedWith != "XMLHttpRequest")
        {
            return RedirectToAction("Index", "PhanCa", new { openShiftModal = "true" });
        }

        keyword = keyword?.Trim();
        var query = _context.CaLamViecs.AsNoTracking().Include(x => x.PhanCas).AsQueryable();
        query = query.Where(x => !x.TenCa.StartsWith("EVT-") && !x.TenCa.StartsWith("TEST-"));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.ToLower();
            query = query.Where(x => x.TenCa.ToLower().Contains(value));
        }

        ViewBag.Keyword = keyword;
        try
        {
            ViewBag.Total = await _context.CaLamViecs.CountAsync(x => !x.TenCa.StartsWith("EVT-") && !x.TenCa.StartsWith("TEST-"));
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
    public async Task<IActionResult> Create(CaLamViec model, string? returnUrl = null)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Dữ liệu ca làm việc không hợp lệ.";
                return LocalRedirect(returnUrl);
            }
            return View(model);
        }

        try
        {
            _context.CaLamViecs.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm ca làm việc “{model.TenCa}”.";
            if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo ca làm việc {ShiftName}.", model.TenCa);
            TempData["Error"] = "Không thể thêm ca làm việc. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.";
            if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
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
    public async Task<IActionResult> Edit(int id, CaLamViec model, string? returnUrl = null)
    {
        if (id != model.MaCa) return BadRequest();
        Normalize(model);
        await ValidateAsync(model, id);
        if (!ModelState.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Dữ liệu ca làm việc không hợp lệ.";
                return LocalRedirect(returnUrl);
            }
            return View(model);
        }

        var entity = await _context.CaLamViecs.FindAsync(id);
        if (entity == null) return NotFound();

        entity.TenCa = model.TenCa;
        entity.GioBatDau = model.GioBatDau;
        entity.GioKetThuc = model.GioKetThuc;
        entity.QuaDem = model.QuaDem;
        entity.GioLamChuan = model.GioLamChuan;
        entity.LoaiCa = model.LoaiCa;
        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật ca làm việc “{entity.TenCa}”.";
            if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể cập nhật ca làm việc {ShiftId}.", id);
            TempData["Error"] = "Không thể cập nhật ca làm việc. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.";
            if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật ca làm việc. Vui lòng kiểm tra dữ liệu hoặc kết nối CSDL.");
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl = null)
    {
        var entity = await _context.CaLamViecs.Include(x => x.PhanCas).FirstOrDefaultAsync(x => x.MaCa == id);
        if (entity == null) return NotFound();

        if (entity.PhanCas.Count != 0)
        {
            TempData["Error"] = $"Không thể xóa “{entity.TenCa}” vì đang có {entity.PhanCas.Count} lịch phân ca.";
            if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
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

        if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Dữ liệu dự phòng khi không kết nối được Supabase. Phải khớp với seed trong
    /// migration 20261027 — nếu lệch, màn hình khi mất mạng sẽ không có ca hành
    /// chính và người dùng tưởng tính năng chưa làm.
    /// </summary>
    private static List<CaLamViec> GetFallbackItems() =>
    [
        new() { MaCa = 1, TenCa = "Ca Sáng", GioBatDau = new TimeOnly(6, 0), GioKetThuc = new TimeOnly(14, 0), QuaDem = false, GioLamChuan = 8.00m, LoaiCa = ShiftWorkModes.XOAY_CA, PhanCas = new List<PhanCa> { new(), new() } },
        new() { MaCa = 2, TenCa = "Ca Chiều", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0), QuaDem = false, GioLamChuan = 8.00m, LoaiCa = ShiftWorkModes.XOAY_CA, PhanCas = new List<PhanCa> { new() } },
        new() { MaCa = 3, TenCa = "Ca Đêm", GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6, 0), QuaDem = true, GioLamChuan = 8.00m, LoaiCa = ShiftWorkModes.XOAY_CA, PhanCas = new List<PhanCa> { new() } },
        // 07:00–17:00 chiếm 10 tiếng nhưng chỉ 8 tiếng làm — 2 tiếng chênh là nghỉ trưa.
        new() { MaCa = 4, TenCa = "Ca Hành Chính", GioBatDau = new TimeOnly(7, 0), GioKetThuc = new TimeOnly(17, 0), QuaDem = false, GioLamChuan = 8.00m, LoaiCa = ShiftWorkModes.HANH_CHINH, PhanCas = new List<PhanCa>() }
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

        // Giờ làm chuẩn phải nằm trong (0, độ dài khung giờ]. Vượt khung nghĩa là
        // nghỉ âm — dữ liệu vô nghĩa. Chặn ở đây để người dùng thấy thông báo rõ
        // thay vì ăn lỗi 23514 của ràng buộc CSDL.
        if (model.GioLamChuan <= 0)
            ModelState.AddModelError(nameof(model.GioLamChuan), "Giờ làm chuẩn phải lớn hơn 0.");
        else if (model.GioLamChuan > model.DoDaiKhungGio)
            ModelState.AddModelError(nameof(model.GioLamChuan),
                $"Giờ làm chuẩn ({model.GioLamChuan:0.##} giờ) không được vượt quá độ dài khung giờ " +
                $"({model.DoDaiKhungGio:0.##} giờ). Phần chênh lệch chính là giờ nghỉ — " +
                "hãy khai số giờ thực sự làm việc.");

        if (!ShiftWorkModes.IsValid(model.LoaiCa))
            ModelState.AddModelError(nameof(model.LoaiCa), "Loại ca không hợp lệ.");
    }

    private static void Normalize(CaLamViec model)
    {
        model.TenCa = model.TenCa?.Trim() ?? string.Empty;
    }
}