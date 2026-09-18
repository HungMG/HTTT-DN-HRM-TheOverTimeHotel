using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR")]
public class SystemCatalogController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Gộp phân hệ Danh mục vào Trung tâm Điều hành Dashboard chính
        return RedirectToAction("Index", "Home");
    }
}
