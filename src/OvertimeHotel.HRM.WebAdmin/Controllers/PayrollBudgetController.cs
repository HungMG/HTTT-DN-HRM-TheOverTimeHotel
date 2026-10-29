using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy=PermissionCodes.ReportView)]
public sealed class PayrollBudgetController(AppDbContext db, PayrollBudgetService budgets) : Controller
{
    private bool Allowed => ReportPolicy.CanViewPayrollFundReport(User.FindFirstValue(ClaimTypes.Role));
    private int Actor => int.TryParse(User.FindFirstValue("EmployeeId"),out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(int? nam)
    {
        if (!Allowed || Actor<=0) return Forbid();
        var year=nam is >=2000 and <=2200 ? nam.Value : DateTime.UtcNow.AddHours(7).Year;
        var rows=await PayrollBudgetService.ReadAsync(db,year);
        var departments=await db.PhongBans.AsNoTracking().OrderBy(p=>p.TenPhongBan)
            .Select(p=>new SelectListItem(p.TenPhongBan,p.MaPhongBan.ToString())).ToArrayAsync();
        return View(new PayrollBudgetViewModel {Nam=year,Available=rows!=null,ActorId=Actor,
            Rows=rows?.OrderBy(b=>b.Thang).ThenBy(b=>b.MaPhongBan).ToArray() ?? [], Departments=departments});
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PayrollBudgetInput input)
    {
        if (!Allowed || Actor<=0) return Forbid();
        if (!ModelState.IsValid) return BadRequest("Dữ liệu ngân sách không hợp lệ: năm, tháng, phòng ban, tiền và ghi chú.");
        try { await budgets.SaveAsync(input,Actor); TempData["Success"]="Đã lưu ngân sách nháp."; }
        catch (InvalidOperationException e) { TempData["Error"]=e.Message; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException {SqlState:PostgresErrorCodes.UniqueViolation})
        { TempData["Error"]="Kỳ/phòng ban đã có ngân sách. Hãy sửa bản nháp hiện có."; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException pg && PayrollBudgetService.MissingTable(pg))
        { return StatusCode(503,"Chưa triển khai bảng ngân sách. Cần DBA duyệt migration."); }
        catch (PostgresException e) when (PayrollBudgetService.MissingTable(e)) { return StatusCode(503,"Chưa triển khai bảng ngân sách. Cần DBA duyệt migration."); }
        return RedirectToAction(nameof(Index),new {nam=input.Nam});
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(Guid id, string action, int nam, long version)
    {
        if (!Allowed || Actor<=0) return Forbid();
        try { await budgets.TransitionAsync(id,action,Actor,version); TempData["Success"]="Đã cập nhật trạng thái ngân sách."; }
        catch (InvalidOperationException e) { TempData["Error"]=e.Message; }
        catch (PostgresException e) when (PayrollBudgetService.MissingTable(e)) { return StatusCode(503,"Chưa triển khai bảng ngân sách."); }
        return RedirectToAction(nameof(Index),new {nam});
    }
}
