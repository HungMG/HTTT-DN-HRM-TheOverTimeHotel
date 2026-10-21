using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize]
public class SuKienController(AppDbContext db, IHotelEventService events, ILogger<SuKienController> logger) : Controller
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

    private async Task<EventActor?> ActorAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var account = await db.TaiKhoans.AsNoTracking().Include(x => x.VaiTro).Include(x => x.NhanVien)
            .SingleOrDefaultAsync(x => x.MaTaiKhoan == id && x.TrangThai);
        var role = account?.VaiTro?.TenVaiTro.ToUpperInvariant() switch
        {
            "ADMIN" => "Admin", "HR" => "HR", "MANAGER" => "Manager", _ => ""
        };
        return account == null || role == "" || account.NhanVien?.TrangThai != "DANG_LAM"
            ? null : new EventActor(account.MaNhanVien, account.MaTaiKhoan, role, account.TenDangNhap);
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, DateOnly? date)
    {
        var actor = await ActorAsync();
        if (actor == null) return Forbid();
        var selected = date ?? Today;
        var y = Math.Clamp(year ?? selected.Year, 1900, 2100);
        var m = Math.Clamp(month ?? selected.Month, 1, 12);
        var first = new DateOnly(y, m, 1);
        var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        if (selected < gridStart || selected >= gridStart.AddDays(42)) selected = first;
        var model = new EventCalendarViewModel { Year = y, Month = m, SelectedDate = selected, CanCreate = actor.IsOffice };
        try
        {
            model.Events = await events.ListAsync(actor, HotelEventRules.ToUtc(gridStart.ToDateTime(TimeOnly.MinValue)),
                HotelEventRules.ToUtc(gridStart.AddDays(42).ToDateTime(TimeOnly.MinValue)));
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Không thể tải lịch sự kiện.");
            model.DatabaseReady = false;
            model.CanCreate = false;
            TempData["Error"] = "Chưa thể tải lịch sự kiện. Vui lòng kiểm tra kết nối và migration 20261015_hotel_events.sql.";
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(DateOnly? date, int? returnYear, int? returnMonth, DateOnly? returnDate, DateOnly? endDate, bool popup = false)
    {
        var actor = await ActorAsync();
        if (actor?.IsOffice != true) return Forbid();
        var day = date ?? Today;
        if (!HotelEventRules.CanCreateOn(day, DateTimeOffset.UtcNow))
            return UnprocessableEntity("Chỉ được tạo sự kiện từ hôm nay trở đi (giờ Việt Nam).");
        var model = new EventFormViewModel
        {
            IsPopup = popup,
            Input = new EventInput { Start = day.ToDateTime(new TimeOnly(9, 0)), End = (endDate is { } last && last >= day ? last : day).ToDateTime(new TimeOnly(17, 0)) }
        };
        SetContext(model, returnYear, returnMonth, returnDate ?? day);
        await FormAsync(model);
        return popup ? PartialView("_CreateEventPopup", model) : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(EventFormViewModel model)
    {
        model.Input.Id = 0;
        model.Input.Version = 0;
        model.Input.CalculateBudgetAtAcceptance = true;
        return SaveAsync(model, "Create");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int? returnYear, int? returnMonth, DateOnly? returnDate)
    {
        var actor = await ActorAsync();
        if (actor?.IsOffice != true) return Forbid();
        var item = await events.GetAsync(actor, id);
        if (item == null) return NotFound();
        if (item.TrangThai != "LEN_KE_HOACH") return Forbid();
        var model = new EventFormViewModel
        {
            Input = new EventInput
            {
                Id = id, Version = item.Version, Name = item.TenDuAn, Type = item.LoaiDuAn, Venue = item.DiaDiem,
                Start = HotelEventRules.Local(item.NgayBatDau), End = HotelEventRules.Local(item.NgayKetThuc),
                ManagerId = item.MaNguoiQuanLy, ExpectedGuests = item.SoKhachDuKien, Budget = item.NganSachPhuCap,
                CalculateBudgetAtAcceptance = item.TinhPhuCapKhiNghiemThu, Description = item.MoTa
            }
        };
        SetContext(model, returnYear, returnMonth, returnDate ?? DateOnly.FromDateTime(model.Input.Start));
        return View(await FormAsync(model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, EventFormViewModel model) => id != model.Input.Id
        ? Task.FromResult<IActionResult>(BadRequest()) : SaveAsync(model, "Edit");

    private async Task<IActionResult> SaveAsync(EventFormViewModel model, string view)
    {
        var actor = await ActorAsync();
        if (actor?.IsOffice != true) return Forbid();
        SetContext(model, model.ReturnYear, model.ReturnMonth, model.ReturnDate);
        if (ModelState.IsValid)
        {
            try
            {
                await events.SaveAsync(actor, model.Input);
                TempData["Success"] = "Đã lưu sự kiện.";
                var selectedStart = model.ReturnDate.ToDateTime(TimeOnly.MinValue);
                var day = DateOnly.FromDateTime(model.Input.Start);
                var returnFirst = new DateOnly(model.ReturnYear, model.ReturnMonth, 1);
                var returnGrid = returnFirst.AddDays(-(((int)returnFirst.DayOfWeek + 6) % 7));
                var keepContext = model.ReturnDate >= returnGrid && model.ReturnDate < returnGrid.AddDays(42)
                    && model.Input.Start < selectedStart.AddDays(1) && model.Input.End > selectedStart;
                var redirectUrl = Url.Action(nameof(Index), new {
                    year = keepContext ? model.ReturnYear : day.Year,
                    month = keepContext ? model.ReturnMonth : day.Month,
                    date = (keepContext ? model.ReturnDate : day).ToString("yyyy-MM-dd")
                });
                return model.IsPopup ? Json(new { redirectUrl }) : Redirect(redirectUrl!);
            }
            catch (EventOperationException ex)
            {
                if (ex.Forbidden) return Forbid();
                ModelState.AddModelError(ex.Field, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Không thể lưu sự kiện.");
                ModelState.AddModelError("", "Không thể lưu sự kiện. Dữ liệu có thể đã thay đổi; vui lòng tải lại và kiểm tra tên hoặc lịch địa điểm.");
            }
        }
        await FormAsync(model);
        return model.IsPopup ? PartialView("_CreateEventPopup", model) : View(view, model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int? returnYear, int? returnMonth, DateOnly? returnDate, string tab = "overview", bool popup = false)
    {
        var actor = await ActorAsync();
        if (actor == null) return Forbid();
        var model = await DetailsAsync(actor, id, null, returnYear, returnMonth, returnDate, tab);
        if (model == null) return NotFound();
        model.IsPopup = popup;
        return popup ? PartialView("_EventDetailsContent", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Assignment(int id, int[] employeeIds, string department,
        int? returnYear, int? returnMonth, DateOnly? returnDate)
    {
        var actor = await ActorAsync();
        if (actor == null) return Forbid();
        var model = await DetailsAsync(actor, id, null, returnYear, returnMonth, returnDate, "staff");
        if (model == null) return NotFound();
        if (!model.CanManage || model.Event.TrangThai is not ("LEN_KE_HOACH" or "DANG_DIEN_RA")) return Forbid();
        if (employeeIds.Length == 0 || employeeIds.Distinct().Count() != employeeIds.Length
            || !model.RequiredDepartments.Contains(department)
            || employeeIds.Any(employeeId => !model.Employees.Any(n => n.MaNhanVien == employeeId
                && HotelEventRules.ResolveDepartmentCode(n) == department)
                || model.Event.PhanCongs.Any(p => p.MaNhanVien == employeeId))) return BadRequest();
        model.IsPopup = true;
        model.SelectedDepartment = department;
        model.Assignments = employeeIds.Select(employeeId => new EventAssignmentInput { EmployeeId = employeeId }).ToList();
        return PartialView("_AssignmentPopup", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(int id, EventDetailsViewModel model) => MutateAsync(id, model, "staff", a => events.AssignAsync(a, id, model.Assignment), assignmentPopup: true);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AssignMany(int id, EventDetailsViewModel model)
    {
        foreach (var input in model.Assignments)
        {
            input.Start = model.Assignment.Start;
            input.End = model.Assignment.End;
            input.Allowance = model.Assignment.Allowance;
        }
        return MutateAsync(id, model, "staff", a => events.AssignManyAsync(a, id, model.Assignment.Version, model.Assignments), assignmentPopup: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Review(int id, EventDetailsViewModel model) => MutateAsync(id, model, "acceptance", a => events.ReviewAsync(a, id, model.Review));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveAssignment(int id, int assignmentId, long version, EventDetailsViewModel model) =>
        MutateAsync(id, model, "staff", a => events.RemoveAssignmentAsync(a, id, assignmentId, version));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Status(int id, long version, string status, string? reason, EventDetailsViewModel model) =>
        MutateAsync(id, model, "overview", a => events.ChangeStatusAsync(a, id, version, status, reason));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Accept(int id, long version, EventDetailsViewModel model) =>
        MutateAsync(id, model, "acceptance", a => events.AcceptAsync(a, id, version));

    private async Task<IActionResult> MutateAsync(int id, EventDetailsViewModel model, string tab, Func<EventActor, Task> operation, bool assignmentPopup = false)
    {
        var actor = await ActorAsync();
        if (actor == null) return Forbid();
        // Scope is checked before loading staff/history or attempting a mutation.
        if (await events.GetAsync(actor, id) == null) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await operation(actor);
                if (model.IsPopup)
                {
                    ModelState.Clear();
                    var updated = await DetailsAsync(actor, id, null, model.ReturnYear, model.ReturnMonth, model.ReturnDate, tab);
                    if (updated == null) return NotFound();
                    updated.IsPopup = true;
                    ViewData["EventSuccess"] = "Đã cập nhật sự kiện.";
                    return PartialView("_EventDetailsContent", updated);
                }
                TempData["Success"] = "Đã cập nhật sự kiện.";
                return RedirectToAction(nameof(Details), new { id, model.ReturnYear, model.ReturnMonth, returnDate = model.ReturnDate.ToString("yyyy-MM-dd"), tab });
            }
            catch (EventOperationException ex)
            {
                if (ex.Forbidden) return Forbid();
                ModelState.AddModelError(ex.Field, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Không thể cập nhật sự kiện {EventId}.", id);
                ModelState.AddModelError("", "Dữ liệu sự kiện vừa thay đổi hoặc có lịch trùng. Vui lòng tải lại trước khi thử lại.");
            }
        }
        var result = await DetailsAsync(actor, id, model, model.ReturnYear, model.ReturnMonth, model.ReturnDate, tab);
        if (result == null) return NotFound();
        result.IsPopup = model.IsPopup;
        return model.IsPopup ? PartialView(assignmentPopup ? "_AssignmentPopup" : "_EventDetailsContent", result) : View("Details", result);
    }

    private async Task<EventFormViewModel> FormAsync(EventFormViewModel model)
    {
        model.Managers = (await events.ManagersAsync()).Select(x => new SelectListItem(x.HoTen, x.MaNhanVien.ToString())).ToList();
        return model;
    }

    private async Task<EventDetailsViewModel?> DetailsAsync(EventActor actor, int id, EventDetailsViewModel? previous,
        int? returnYear, int? returnMonth, DateOnly? returnDate, string tab)
    {
        var item = await events.GetAsync(actor, id);
        if (item == null) return null;
        var mutable = item.TrangThai is not ("HOAN_THANH" or "HUY");
        var model = new EventDetailsViewModel
        {
            Event = item, CanManage = mutable, CanEdit = actor.IsOffice && item.TrangThai == "LEN_KE_HOACH",
            CanAccept = actor.IsOffice && item.TrangThai == "CHO_NGHIEM_THU",
            Employees = mutable ? await events.EmployeesAsync() : [], Shifts = await events.ShiftsAsync(item),
            History = await events.HistoryAsync(id), Tab = tab is "staff" or "acceptance" ? tab : "overview",
            Assignment = previous?.Assignment ?? new EventAssignmentInput
            {
                Start = HotelEventRules.Local(item.NgayBatDau), End = HotelEventRules.Local(item.NgayKetThuc), Version = item.Version
            },
            SelectedDepartment = previous?.SelectedDepartment,
            Assignments = previous?.Assignments ?? [],
            Review = previous?.Review ?? new EventReviewInput { Version = item.Version },
            ReturnYear = Math.Clamp(returnYear is > 0 ? returnYear.Value : HotelEventRules.Local(item.NgayBatDau).Year, 1900, 2100),
            ReturnMonth = Math.Clamp(returnMonth is > 0 ? returnMonth.Value : HotelEventRules.Local(item.NgayBatDau).Month, 1, 12),
            ReturnDate = returnDate is { Year: > 1 } ? returnDate.Value : DateOnly.FromDateTime(HotelEventRules.Local(item.NgayBatDau))
        };
        if (model.Assignment.EmployeeId > 0)
        {
            var emp = model.Employees.FirstOrDefault(e => e.MaNhanVien == model.Assignment.EmployeeId);
            model.SelectedDepartment = HotelEventRules.ResolveDepartmentCode(emp);
            ModelState.Remove(nameof(model.SelectedDepartment));
        }
        return model;
    }

    private static void SetContext(EventFormViewModel model, int? year, int? month, DateOnly? date)
    {
        var selected = date is { Year: >= 1900 and <= 2100 } ? date.Value : Today;
        model.ReturnYear = Math.Clamp(year is > 0 ? year.Value : selected.Year, 1900, 2100);
        model.ReturnMonth = Math.Clamp(month is > 0 ? month.Value : selected.Month, 1, 12);
        model.ReturnDate = selected;
    }
}
