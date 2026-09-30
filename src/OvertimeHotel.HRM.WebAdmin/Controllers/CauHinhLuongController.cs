using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.CalculatePayroll)]
public class CauHinhLuongController(AppDbContext context, IMemoryCache cache,
    ILogger<CauHinhLuongController> logger) : Controller
{
    private readonly AppDbContext _context = context;

    private IQueryable<CauHinhLuongRow> Query(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.TangCa => _context.CauHinhTangCas.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaLoaiOt, Name = x.TenLoaiOt, Type = "", Value = x.HeSo,
                Taxable = false, Description = x.MoTa, Used = x.ChiTietPhieuLuongs.Any() }),
        CauHinhLuongCategory.PhuCap => _context.CauHinhPhuCaps.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaPhuCap, Name = x.TenPhuCap, Type = x.LoaiTinh, Value = x.MucDinhMuc,
                Taxable = x.ChiuThueTncn, Description = x.MoTa, Used = x.ChiTietPhieuLuongs.Any() }),
        CauHinhLuongCategory.KhauTru => _context.CauHinhKhauTrus.AsNoTracking()
            .Select(x => new CauHinhLuongRow { Id = x.MaKhoanTru, Name = x.TenKhoanTru, Type = x.LoaiKhoanTru, Value = x.TyLeHoacDinhMuc,
                Taxable = false, Description = x.MoTa, Used = x.ChiTietPhieuLuongs.Any() }),
        CauHinhLuongCategory.Thuong => _context.CauHinhKhoanLuongs.AsNoTracking().Where(x => x.LoaiKhoan == "THUONG")
            .Select(x => new CauHinhLuongRow { Id = x.MaKhoan, Name = x.TenKhoan, Type = x.LoaiKhoan, Value = x.GiaTriMacDinh,
                Taxable = false, Description = x.MoTa, Used = x.ChiTietPhieuLuongs.Any() }),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    [HttpGet]
    public async Task<IActionResult> Index(CauHinhLuongCategory category = CauHinhLuongCategory.TangCa,
        string? keyword = null, string? type = null)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        keyword = keyword?.Trim();
        type = type?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(type) && !CauHinhLuongLabels.Types(category).ContainsKey(type))
            return BadRequest();
        var model = new CauHinhLuongListViewModel { Category = category, Keyword = keyword, Type = type };
        try
        {
            var query = Query(category);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.ToLowerInvariant();
                query = query.Where(x => x.Name.ToLower().Contains(term) ||
                    (x.Description != null && x.Description.ToLower().Contains(term)));
            }
            if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
            model.Items = await query.OrderBy(x => x.Name).ToListAsync();
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Không thể đọc danh mục cấu hình lương {Category}.", category);
            model.Error = "Không thể tải dữ liệu. Vui lòng kiểm tra kết nối và thử lại.";
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Create(CauHinhLuongCategory category = CauHinhLuongCategory.TangCa)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        return View("Form", new CauHinhLuongFormViewModel
        {
            Category = category,
            Value = category == CauHinhLuongCategory.TangCa ? 1.50m : 0,
            Type = CauHinhLuongLabels.Types(category).Keys.FirstOrDefault()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CauHinhLuongCategory category)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        var row = await Query(category).FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return NotFound();
        return View("Form", new CauHinhLuongFormViewModel
        {
            Id = row.Id, Category = category, Name = row.Name, Type = row.Type,
            Value = row.Value, Taxable = row.Taxable, Description = row.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CauHinhLuongFormViewModel model)
    {
        model.Id = 0;
        return await Save(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CauHinhLuongFormViewModel model)
    {
        if (id <= 0 || id != model.Id) return BadRequest();
        return await Save(model);
    }

    private async Task<IActionResult> Save(CauHinhLuongFormViewModel model)
    {
        if (!Enum.IsDefined(model.Category)) return BadRequest();
        model.Name = model.Name?.Trim() ?? "";
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        if (!ModelState.IsValid) return View("Form", model);
        try
        {
            var name = model.Name.ToLowerInvariant();
            if (await Query(model.Category).AnyAsync(x => x.Id != model.Id && x.Name.ToLower() == name) ||
                (model.Category == CauHinhLuongCategory.Thuong &&
                 await _context.CauHinhKhoanLuongs.AnyAsync(x => x.MaKhoan != model.Id && x.TenKhoan.ToLower() == name)))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên cấu hình đã tồn tại trong danh mục.");
                return View("Form", model);
            }
            if (!await SetEntity(model)) return NotFound();
            await _context.SaveChangesAsync();
            cache.Remove("HomeDashboardMetrics_V2");
            TempData["Success"] = "Đã lưu cấu hình. Phiếu lương đã chốt giữ nguyên dữ liệu.";
            return RedirectToAction(nameof(Index), new { category = model.Category });
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            logger.LogError(ex, "Không thể lưu cấu hình lương {Category}/{Id}.", model.Category, model.Id);
            ModelState.AddModelError(string.Empty, "Không thể lưu cấu hình. Kiểm tra tên trùng, dữ liệu và kết nối rồi thử lại.");
            return View("Form", model);
        }
    }

    private async Task<bool> SetEntity(CauHinhLuongFormViewModel model)
    {
        switch (model.Category)
        {
            case CauHinhLuongCategory.TangCa:
            {
                var e = model.Id == 0 ? new CauHinhTangCa() : await _context.CauHinhTangCas.FindAsync(model.Id);
                if (e == null) return false;
                e.TenLoaiOt = model.Name;
                e.HeSo = model.Value;
                e.MoTa = model.Description;
                
                if (model.Id == 0) _context.CauHinhTangCas.Add(e);
                break;
            }
            case CauHinhLuongCategory.PhuCap:
            {
                var e = model.Id == 0 ? new CauHinhPhuCap() : await _context.CauHinhPhuCaps.FindAsync(model.Id);
                if (e == null) return false;
                e.TenPhuCap = model.Name;
                e.MucDinhMuc = model.Value;
                e.MoTa = model.Description;
                e.LoaiTinh = model.Type!; e.ChiuThueTncn = model.Taxable;
                if (model.Id == 0) _context.CauHinhPhuCaps.Add(e);
                break;
            }
            case CauHinhLuongCategory.KhauTru:
            {
                var e = model.Id == 0 ? new CauHinhKhauTru() : await _context.CauHinhKhauTrus.FindAsync(model.Id);
                if (e == null) return false;
                e.TenKhoanTru = model.Name;
                e.TyLeHoacDinhMuc = model.Value;
                e.MoTa = model.Description;
                e.LoaiKhoanTru = model.Type!;
                if (model.Id == 0) _context.CauHinhKhauTrus.Add(e);
                break;
            }
            case CauHinhLuongCategory.Thuong:
            {
                var e = model.Id == 0 ? new CauHinhKhoanLuong() : await _context.CauHinhKhoanLuongs.FindAsync(model.Id);
                if (e == null || (model.Id != 0 && e.LoaiKhoan != "THUONG")) return false;
                e.TenKhoan = model.Name;
                e.GiaTriMacDinh = model.Value;
                e.MoTa = model.Description;
                e.LoaiKhoan = "THUONG";
                if (model.Id == 0) _context.CauHinhKhoanLuongs.Add(e);
                break;
            }
            default: return false;
        }
        return true;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CauHinhLuongCategory category)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(category)) return BadRequest();
        try
        {
            var row = await Query(category).FirstOrDefaultAsync(x => x.Id == id);
            if (row == null) return NotFound();
            if (row.Used)
            {
                TempData["Error"] = "Không thể xóa cấu hình đã được sử dụng trong phiếu lương.";
                return RedirectToAction(nameof(Index), new { category });
            }
            object? entity = category switch
            {
                CauHinhLuongCategory.TangCa => await _context.CauHinhTangCas.FindAsync(id),
                CauHinhLuongCategory.PhuCap => await _context.CauHinhPhuCaps.FindAsync(id),
                CauHinhLuongCategory.KhauTru => await _context.CauHinhKhauTrus.FindAsync(id),
                CauHinhLuongCategory.Thuong => await _context.CauHinhKhoanLuongs.FindAsync(id),
                _ => null
            };
            if (entity == null) return NotFound();
            _context.Remove(entity);
            await _context.SaveChangesAsync();
            cache.Remove("HomeDashboardMetrics_V2");
            TempData["Success"] = "Đã xóa cấu hình.";
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            logger.LogError(ex, "Không thể xóa cấu hình lương {Category}/{Id}.", category, id);
            TempData["Error"] = "Không thể xóa cấu hình. Dữ liệu có thể đang được sử dụng hoặc kết nối bị gián đoạn.";
        }
        return RedirectToAction(nameof(Index), new { category });
    }
}
