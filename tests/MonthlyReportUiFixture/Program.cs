using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(BaoCaoController).Assembly);
var app = builder.Build();
app.MapGet("/fixture-script.js", () => Results.File(Path.GetFullPath("../../src/OvertimeHotel.HRM.WebAdmin/wwwroot/js/baocao.js"), "text/javascript"));
app.MapGet("/fixture-interactive", () => Results.Content("""
<!doctype html><html><body><div data-baocao-root data-baocao-details-endpoint="/fixture/details">
<div data-baocao-board-host></div><button data-baocao-drill="thong-tin-nhan-su">Danh sách</button>
<div id="baocaoDetailsModal"><div id="baocaoDetailsContent"></div></div></div>
<script src="/fixture-script.js"></script></body></html>
""", "text/html"));
app.MapControllers();
app.Run();

public sealed class FixtureController : Controller
{
    [HttpGet("/fixture/details")]
    public IActionResult Details(string type)
    {
        RouteData.Values["controller"] = "BaoCao";
        var model = new BaoCaoChiTietViewModel
        {
            LoaiChiTiet = type, TieuDe = type,
            DanhSach = [new() { TenDoiTuong = "An", BoPhan = "Lễ tân" }, new() { TenDoiTuong = "Bình", BoPhan = "Kỹ thuật" }]
        };
        return PartialView("/Views/BaoCao/_BaoCaoDetails.cshtml", model);
    }
    [HttpGet("/fixture/{tab}")]
    public IActionResult ViewFixture(string tab, bool manager = false, bool print = false)
    {
        RouteData.Values["controller"] = "BaoCao";
        var model = new BaoCaoViewModel
        {
            CanViewPayrollFund = !manager,
            QuyLuong = new() { SoNhanSuThieuCanCuLuong = 1, GhiChuPhuongPhap = "Có 1 hồ sơ lương chưa hợp lệ; định mức chưa đầy đủ." },
            Tab = tab, Nam = 2026, Thang = 10, NhanKy = "Tháng 10/2026",
            TuNgay = new(2026, 10, 1), DenNgay = new(2026, 10, 31), IsDatabaseOnline = true,
            CungKyLabel = "Tháng 10/2025", SoSanhCungKy = [new() { Label = "Nhân sự cuối tháng", Unit = "người", Current = 12, Previous = 10 }],
            BienDong = new() { CuoiKy = 12, NghiViec = 2 },
            CoCau = new() { CoNghiPhepTrongKy = 3 },
            HocVanThamNien = new() { CanViewSalaryInfo = !manager, SoNhanSuCoLuong = 0, SoNhanSuThieuLuong = 12,
                TheoTrinhDo = [new() { Nhan = "Đại học", GiaTri = 1 }, new() { Nhan = "Cao đẳng", GiaTri = 1 }] }
        };
        return print ? View("/Views/BaoCao/Print.cshtml", model) : PartialView("/Views/BaoCao/_BaoCaoBoard.cshtml", model);
    }
}
