using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.BackupDatabase)]
public class BackupController : Controller
{
    private readonly IBackupService _backupService;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly ILogger<BackupController> _logger;

    private const string StatsCacheKey = "Backup_Table_Statistics_Cache";

    public BackupController(
        IBackupService backupService,
        AppDbContext context,
        IConfiguration configuration,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _context = context;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IActionResult> Index(bool refresh = false)
    {
        ViewBag.SupabaseProjectUrl = _configuration["Supabase:Url"] ?? "https://supabase.com";
        ViewBag.DbHost = "aws-0-ap-southeast-1.pooler.supabase.com (Port 6543)";

        if (refresh)
        {
            _cache.Remove(StatsCacheKey);
        }

        if (!_cache.TryGetValue(StatsCacheKey, out Dictionary<string, int>? stats) || stats == null)
        {
            try
            {
                stats = await _backupService.GetTableStatisticsAsync();
                _cache.Set(StatsCacheKey, stats, TimeSpan.FromMinutes(3));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Chưa kết nối trực tiếp được CSDL: {Message}", ex.Message);
                ViewBag.DbWarning = "Không thể lấy số lượng bản ghi trực tiếp (vui lòng kiểm tra mật khẩu database trong appsettings.json).";
                stats = new Dictionary<string, int>();
            }
        }

        ViewBag.TotalRecords = stats.Values.Sum();
        ViewBag.TotalTables = stats.Count;

        return View(stats);
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        try
        {
            var json = await _backupService.ExportDatabaseToJsonAsync();
            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"OvertimeHotel_Backup_30Tables_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

            var username = User.Identity?.Name ?? "IT";
            int? accountId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId))
            {
                accountId = parsedId;
            }

            try
            {
                _context.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    MaTaiKhoanThucHien = accountId,
                    MaTaiKhoanBiTacDong = accountId,
                    TenNguoiThucHien = username,
                    TenTaiKhoanBiTacDong = "DATABASE_SNAPSHOT",
                    HanhDong = "SAO_LUU_CSDL",
                    NoiDung = $"Người dùng '{username}' đã tải xuống bản sao lưu JSON toàn diện ({fileName}, {bytes.Length / 1024.0:F1} KB).",
                    GiaTriCu = null,
                    GiaTriMoi = fileName,
                    LyDo = "Sao lưu dữ liệu định kỳ an toàn thông tin doanh nghiệp",
                    DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    ThoiGian = DateTimeOffset.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi nhật ký xuất sao lưu.");
            }

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất bản sao lưu CSDL");
            TempData["Error"] = $"Không thể xuất bản sao lưu: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(IFormFile? backupFile)
    {
        if (backupFile == null || backupFile.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn file sao lưu JSON hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var reader = new StreamReader(backupFile.OpenReadStream(), Encoding.UTF8);
            var jsonContent = await reader.ReadToEndAsync();

            var (success, message, restoredRecords) = await _backupService.RestoreDatabaseFromJsonAsync(jsonContent);
            if (success)
            {
                var username = User.Identity?.Name ?? "IT";
                int? accountId = null;
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId))
                {
                    accountId = parsedId;
                }

                try
                {
                    _cache.Remove(StatsCacheKey);
                    _context.NhatKyQuanTris.Add(new NhatKyQuanTri
                    {
                        MaTaiKhoanThucHien = accountId,
                        MaTaiKhoanBiTacDong = accountId,
                        TenNguoiThucHien = username,
                        TenTaiKhoanBiTacDong = "DATABASE_RESTORE",
                        HanhDong = "KHOI_PHUC_CSDL",
                        NoiDung = $"Người dùng '{username}' đã khôi phục thành công {restoredRecords} bản ghi từ file '{backupFile.FileName}'.",
                        GiaTriCu = null,
                        GiaTriMoi = backupFile.FileName,
                        LyDo = "Phục hồi CSDL từ bản sao lưu dự phòng",
                        DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                        ThoiGian = DateTimeOffset.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể ghi nhật ký khôi phục sao lưu.");
                }

                TempData["Success"] = $"{message} (Đã nạp {restoredRecords} bản ghi vào hệ thống).";
            }
            else
            {
                TempData["Error"] = message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi khôi phục bản sao lưu");
            TempData["Error"] = $"Lỗi khôi phục dữ liệu: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
