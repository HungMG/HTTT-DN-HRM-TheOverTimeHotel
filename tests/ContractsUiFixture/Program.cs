using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Data.Context;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HopDongController).Assembly);
builder.Services.AddSingleton<AiContextTokenService>();
builder.Services.AddSingleton<AiChatbotUrlBuilder>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("ContractUiFixture"));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.PhongBans.AddRange(new PhongBan { MaPhongBan = 1, TenPhongBan = "Ban Giám Đốc" }, new PhongBan { MaPhongBan = 2, TenPhongBan = "Tiền sảnh" });
    db.ChucVus.AddRange(new ChucVu { MaChucVu = 1, MaPhongBan = 1, TenChucVu = "Tổng Giám Đốc" },
        new ChucVu { MaChucVu = 2, TenChucVu = "Trưởng Bộ Phận" },
        new ChucVu { MaChucVu = 4, MaPhongBan = 2, TenChucVu = "Lễ tân" });
    db.SaveChanges();
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath("../../src/OvertimeHotel.HRM.WebAdmin/wwwroot"))
});
app.MapControllers();
// Only the fixture's attribute routes are executable. Database uses isolated memory, never production startup.
app.Run();

[Route("HopDong")]
public sealed class ContractsFixtureController(AppDbContext db) : Controller
{
    private static HopDongFormViewModel Form(int id = 7) => new()
    {
        EmployeeId = 10, EmployeeName = "Nguyễn An", DepartmentId = 1, PositionId = 1,
        PersonalEmail = "an@example.test",
        DepartmentOptions = [new("Ban Giám Đốc", "1"), new("Tiền sảnh", "2")],
        PositionOptions = [new("Tổng Giám Đốc", "1")],
        Contract = new() { MaHopDong = id, MaNhanVien = 10, SoHopDong = "HDLD-2026/07/OTH",
            LoaiHopDong = "XAC_DINH_THOI_HAN_1_NAM", NgayBatDau = new(2026, 1, 1),
            NgayKetThuc = new(2027, 1, 1), LuongCoBan = 35000000 }
    };

    private void Prepare(HopDongFormViewModel model)
    {
        RouteData.Values["controller"] = "HopDong";
        ViewBag.DepartmentOptions = model.DepartmentOptions;
        ViewBag.PositionOptions = model.PositionOptions;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index(bool isPartial = false, bool occupiedGm = false)
    {
        if (occupiedGm) TempData["Error"] = "Hệ thống đã có Tổng Giám Đốc trong thời gian này. Không thể tạo thêm Tổng Giám Đốc.";
        var form = Form(); Prepare(form);
        var model = new HopDongIndexViewModel
        {
            CreateContractForm = Form(0), TotalContracts = 1, TotalFilteredContracts = 1,
            ActiveContracts = 1, PageSize = 10, PageNumber = 1, TotalPages = 1,
            Contracts = [new() { Contract = form.Contract, EmployeeName = form.EmployeeName,
                DepartmentName = "Ban Giám Đốc", PositionName = "Tổng Giám Đốc", State = "HIEU_LUC" }]
        };
        return isPartial ? PartialView("/Views/HopDong/_ContractTablePartial.cshtml", model)
            : View("/Views/HopDong/Index.cshtml", model);
    }

    [HttpGet("Edit/{id}")]
    public IActionResult Edit(int id, bool probation = false)
    {
        var model = Form(id);
        if (probation) { model.Contract.HinhThucHopDong = "THU_VIEC"; model.Contract.NgayKetThuc = new(2026, 1, 31); }
        Prepare(model);
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            ViewData["IsContractModal"] = true;
            return PartialView("/Views/HopDong/_EditContractModalContent.cshtml", model);
        }
        return View("/Views/HopDong/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(int id, HopDongFormViewModel model)
    {
        model.DepartmentOptions = Form().DepartmentOptions;
        model.PositionOptions = Form().PositionOptions;
        Prepare(model); ViewData["IsContractModal"] = true;
        if (model.Contract.SoHopDong == "TRUNG-MA")
        {
            ModelState.AddModelError("Contract.SoHopDong", "Mã hợp đồng đã tồn tại.");
            Response.StatusCode = 422;
            return PartialView("/Views/HopDong/_EditContractModalContent.cshtml", model);
        }
        return Json(new { success = true, message = "Đã cập nhật hợp đồng thử nghiệm." });
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id) => Json(new { success = true, message = "Đã xóa hợp đồng thử nghiệm." });

    [HttpGet("GenerateContractNumber")]
    public Task<IActionResult> Number(string? hinhThuc)
        => new HopDongController(db, NullLogger<HopDongController>.Instance).GenerateContractNumber(hinhThuc);

    [HttpGet("SalaryReviews")]
    public IActionResult Reviews(bool empty = false)
    {
        var rows = new List<HopDongController.SalaryReviewItem>
        {
            new(2333, "Nguyễn Ngọc Linh", 10, null, null, null, "Chưa có mức lương hợp lệ; cần kiểm tra hợp đồng/phụ lục."),
            new(2335, "Trần Minh Anh", 12, 8000000, 8000000, 10000000, "Đã đến mốc thâm niên xét lương.")
        };
        if (empty) rows.Clear();
        if (Request.Headers.XRequestedWith == "XMLHttpRequest") return PartialView("/Views/HopDong/_SalaryReviewList.cshtml", rows);
        return View("/Views/HopDong/SalaryReviews.cshtml", rows);
    }

    [HttpGet("SalaryAmendments")]
    public IActionResult Amendments(int id) => PartialView("/Views/HopDong/_SalaryAmendments.cshtml", new HopDongSalaryAmendmentsViewModel
    {
        ContractId = id, EmployeeId = 10, ContractNumber = "HDLD-2026/07/OTH", Education = "Trung cấp", PositionId = 4, DepartmentId = 2,
        DepartmentOptions = [new("Tiền sảnh", "2")], PositionOptions = [new("Lễ tân", "4")],
        Items = [new() { EffectiveDate = new(2026, 2, 1), Salary = 8000000, Reason = "Xét thâm niên", PositionName = "Lễ tân" }]
    });

}

public sealed class PositionsFixtureController(AppDbContext db) : Controller
{
    [HttpGet("/ChucVu/GetByDepartment")]
    public Task<IActionResult> Positions(int? departmentId)
    {
        var controller = new ChucVuController(db, NullLogger<ChucVuController>.Instance) { ControllerContext = ControllerContext };
        return controller.GetByDepartment(departmentId);
    }
}
