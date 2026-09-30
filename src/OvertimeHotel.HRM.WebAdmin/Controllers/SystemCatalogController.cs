using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.ManageCatalog)]
public class SystemCatalogController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Gộp phân hệ Danh mục vào Trung tâm Điều hành Dashboard chính
        return RedirectToAction("Index", "Home");
    }
}
