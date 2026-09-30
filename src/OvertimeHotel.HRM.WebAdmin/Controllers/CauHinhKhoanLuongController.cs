using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

// Giữ các liên kết cũ; CRUD mới dùng danh mục riêng, bảng cũ chỉ quản lý thưởng.
[Authorize(Policy = PermissionCodes.CalculatePayroll)]
public class CauHinhKhoanLuongController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "CauHinhLuong");

    [HttpGet]
    public IActionResult Create() => RedirectToAction("Create", "CauHinhLuong",
        new { category = CauHinhLuongCategory.Thuong });

    [HttpGet]
    public IActionResult Edit(int id) => RedirectToAction("Edit", "CauHinhLuong",
        new { id, category = CauHinhLuongCategory.Thuong });
}
