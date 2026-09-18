using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, IMemoryCache cache, ILogger<HomeController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        const string cacheKey = "HomeDashboardMetrics_V1";
        if (!_cache.TryGetValue(cacheKey, out HomeDashboardViewModel? model) || model == null)
        {
            model = new HomeDashboardViewModel();
            try
            {
                model.TongNhanVien = await _context.NhanViens.CountAsync();
                model.TongNhanVienPhongBan = model.TongNhanVien;
                model.NhanVienDangLam = await _context.NhanViens.CountAsync(n => n.TrangThai == "DANG_LAM");
                model.TongHopDong = await _context.HopDongs.CountAsync();
                model.HopDongHieuLuc = await _context.HopDongs.CountAsync(h => h.TrangThai == "HIEU_LUC");

                var today = DateOnly.FromDateTime(DateTime.Today);
                var nextMonth = today.AddDays(30);
                model.HopDongSapHetHan = await _context.HopDongs.CountAsync(h => h.NgayKetThuc.HasValue && h.NgayKetThuc.Value >= today && h.NgayKetThuc.Value <= nextMonth);

                model.TongPhongBan = await _context.PhongBans.CountAsync();
                model.TongChucVu = await _context.ChucVus.CountAsync();
                model.TongCaLamViec = await _context.CaLamViecs.CountAsync();
                model.SoCaQuaDem = await _context.CaLamViecs.CountAsync(x => x.QuaDem);

                model.TongKhoanLuong = await _context.CauHinhKhoanLuongs.CountAsync();
                model.SoKhoanPhuCap = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "PHU_CAP");
                model.SoKhoanThuong = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "THUONG");
                model.SoKhoanKhauTru = await _context.CauHinhKhoanLuongs.CountAsync(x => x.LoaiKhoan == "KHAU_TRU");

                model.TongTaiKhoan = await _context.TaiKhoans.CountAsync();
                model.TaiKhoanHoatDong = await _context.TaiKhoans.CountAsync(t => t.TrangThai);
                model.TongPhanCa = await _context.PhanCas.CountAsync();
                model.IsDatabaseOnline = true;

                // Cache 60 giây để trang phản hồi tức thì 0ms, giải phóng tài nguyên mạng
                _cache.Set(cacheKey, model, TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi nạp số liệu Dashboard từ CSDL. Dùng giá trị thống kê chuẩn.");
                model.IsDatabaseOnline = false;
            }
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
