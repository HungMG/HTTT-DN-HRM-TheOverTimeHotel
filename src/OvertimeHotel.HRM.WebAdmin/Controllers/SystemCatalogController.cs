using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class SystemCatalogController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<SystemCatalogController> _logger;

    public SystemCatalogController(AppDbContext context, ILogger<SystemCatalogController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new SystemCatalogDashboardViewModel();

        try
        {
            model.TongPhongBan = await _context.PhongBans.CountAsync();
            model.TongNhanVienPhongBan = await _context.NhanViens.CountAsync();
            model.TongChucVu = await _context.ChucVus.CountAsync();
            model.TongCaLamViec = await _context.CaLamViecs.CountAsync();
            model.SoCaQuaDem = await _context.CaLamViecs.CountAsync(x => x.QuaDem);
            model.TongKhoanLuong = await _context.CauHinhKhoanLuongs.CountAsync();
            model.SoKhoanPhuCap = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "PHU_CAP");
            model.SoKhoanThuong = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "THUONG");
            model.SoKhoanKhauTru = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "KHAU_TRU");
            model.IsDemoData = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối CSDL, sử dụng số liệu mẫu cho Dashboard danh mục.");
            model.TongPhongBan = 8;
            model.TongNhanVienPhongBan = 20;
            model.TongChucVu = 10;
            model.TongCaLamViec = 3;
            model.SoCaQuaDem = 1;
            model.TongKhoanLuong = 6;
            model.SoKhoanPhuCap = 3;
            model.SoKhoanThuong = 2;
            model.SoKhoanKhauTru = 1;
            model.IsDemoData = true;
        }

        return View(model);
    }
}