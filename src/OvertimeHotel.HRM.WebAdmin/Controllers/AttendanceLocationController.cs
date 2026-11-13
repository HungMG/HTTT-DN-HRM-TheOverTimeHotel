using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Quản lý cấu hình vị trí địa lý, toạ độ GPS, bán kính và Wi-Fi phục vụ chấm công khách sạn.
/// ĐẶC QUYỀN DUY NHẤT DÀNH CHO BỘ PHẬN CNTT (IT): Quản trị viên (Admin) và các vai trò khác tuyệt đối không có quyền can thiệp.
/// </summary>
[Authorize(Policy = PermissionCodes.ConfigureAttendanceLocation)]
public class AttendanceLocationController : Controller
{
    private readonly IAttendanceLocationService _locationService;
    private readonly ILogger<AttendanceLocationController> _logger;

    public AttendanceLocationController(
        IAttendanceLocationService locationService,
        ILogger<AttendanceLocationController> logger)
    {
        _locationService = locationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Rào chắn bảo mật 2 lớp: Chỉ duy nhất vai trò IT được truy cập màn hình này
        if (!User.IsInRole("IT"))
        {
            _logger.LogWarning("Người dùng {User} ({Role}) cố gắng truy cập trang cấu hình vị trí chấm công IT.",
                User.Identity?.Name, User.FindFirstValue(ClaimTypes.Role));
            return Forbid();
        }

        var model = await _locationService.GetLocationConfigAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AttendanceLocationViewModel model)
    {
        if (!User.IsInRole("IT"))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = User.Identity?.Name ?? "IT";
        int? accountId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId))
        {
            accountId = parsedId;
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var success = await _locationService.UpdateLocationConfigAsync(model, username, accountId, ipAddress);
        if (success)
        {
            TempData["Success"] = "Cập nhật toạ độ định vị và bán kính chấm công của khách sạn thành công!";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, "Không thể lưu cấu hình toạ độ. Vui lòng kiểm tra quyền ghi trên hệ thống.");
        return View(model);
    }
}
