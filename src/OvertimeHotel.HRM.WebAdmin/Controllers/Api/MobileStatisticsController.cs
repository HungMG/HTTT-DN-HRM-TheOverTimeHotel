using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

[ApiController]
[Route("api/mobile/statistics")]
[Authorize(AuthenticationSchemes = "MobileBearer", Policy = PermissionCodes.ReportView)]
public sealed class MobileStatisticsController(AppDbContext db, BaoCaoReportService reports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = SupabaseClaims.GetMaNhanVien(User);
        var role = await db.TaiKhoans.AsNoTracking().Where(a => a.MaNhanVien == id && a.TrangThai)
            .Select(a => a.VaiTro!.TenVaiTro).SingleOrDefaultAsync();
        if (role == null) return Forbid();
        var scope = new ReportPolicy.BaoCaoScope(true, true, null);
        var model = await reports.BuildAsync(scope, role, "THANG", null, null, null, null, null, canViewPayroll: true);
        if (!model.IsDatabaseOnline) return StatusCode(503, new { message = "Không thể tải dữ liệu báo cáo. Vui lòng thử lại." });
        return Ok(new {
            period = model.NhanKy,
            totalEmployees = model.CoCau.DangLam,
            departments = model.CoCau.TheoPhongBan.Select(x => new { name = x.Nhan, count = x.GiaTri, rate = x.TyLe }),
            education = model.HocVanThamNien.TheoTrinhDo.Select(x => new { name = x.Nhan, count = x.GiaTri, rate = x.TyLe }),
            tenure = model.HocVanThamNien.TheoThamNien.Select(x => new { name = x.Nhan, count = x.GiaTri, rate = x.TyLe }),
            assessedShifts = model.ChuyenCan.TongBanGhi,
            punctualRate = model.ChuyenCan.TyLeChuyenCan,
            violationCount = model.ChuyenCan.DiTre + model.ChuyenCan.VeSom + model.ChuyenCan.TreVaSom,
            leaveCount = model.ChuyenCan.SoCaNghiDuocDuyet,
            missingPunchCount = model.ChuyenCan.SoCaThieuQuet,
            payrollGross = model.CanViewPayrollFund ? (decimal?)model.QuyLuong.ThucChiGross : null,
            payrollApproved = model.CanViewPayrollFund ? (decimal?)model.QuyLuong.ThucChiGrossDaDuyet : null,
            payrollDraft = model.CanViewPayrollFund ? (decimal?)model.QuyLuong.ThucChiGrossNhap : null
        });
    }
}
