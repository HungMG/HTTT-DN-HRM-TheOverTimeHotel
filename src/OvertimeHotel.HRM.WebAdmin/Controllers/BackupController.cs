using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.BackupDatabase)]
public class BackupController : Controller
{
    private readonly IBackupService _backupService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupController> _logger;

    public BackupController(IBackupService backupService, IConfiguration configuration, ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.SupabaseProjectUrl = _configuration["Supabase:Url"] ?? "https://supabase.com";
        ViewBag.DbHost = "aws-0-ap-southeast-1.pooler.supabase.com (Port 6543)";

        Dictionary<string, int> stats = new();
        try
        {
            stats = await _backupService.GetTableStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Chưa kết nối trực tiếp được CSDL: {Message}", ex.Message);
            ViewBag.DbWarning = "Không thể lấy số lượng bản ghi trực tiếp (vui lòng kiểm tra mật khẩu database trong appsettings.json).";
        }

        return View(stats);
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        try
        {
            var json = await _backupService.ExportDatabaseToJsonAsync();
            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"OvertimeHotel_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

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
                TempData["Success"] = $"{message} (Đã nạp {restoredRecords} bản ghi).";
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
