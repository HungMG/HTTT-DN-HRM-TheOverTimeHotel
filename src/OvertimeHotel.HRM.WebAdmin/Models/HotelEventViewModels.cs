using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Services;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class EventCalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly SelectedDate { get; set; }
    public IReadOnlyList<DuAn> Events { get; set; } = [];
    public bool CanCreate { get; set; }
    public bool DatabaseReady { get; set; } = true;
}

public class EventFormViewModel
{
    public bool IsPopup { get; set; }
    public EventInput Input { get; set; } = new();
    public IReadOnlyList<SelectListItem> Managers { get; set; } = [];
    public int ReturnYear { get; set; }
    public int ReturnMonth { get; set; }
    public DateOnly ReturnDate { get; set; }
}

public class EventDetailsViewModel
{
    public DuAn Event { get; set; } = new();
    public IReadOnlyList<NhanVien> Employees { get; set; } = [];
    public IReadOnlyList<PhanCa> Shifts { get; set; } = [];
    public IReadOnlyList<NhatKyQuanTri> History { get; set; } = [];
    public bool CanManage { get; set; }
    public bool CanAccept { get; set; }
    public bool CanEdit { get; set; }
    public EventAssignmentInput Assignment { get; set; } = new();
    public EventReviewInput Review { get; set; } = new();
    public int ReturnYear { get; set; }
    public int ReturnMonth { get; set; }
    public DateOnly ReturnDate { get; set; }
    public string Tab { get; set; } = "overview";
    public string? SelectedDepartment { get; set; }
    public OvertimeHotel.HRM.Core.Policies.EventStaffingSummary StaffingSummary =>
        OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetStaffingSummary(Event.LoaiDuAn, Event.PhanCongs);
    public IReadOnlyList<string> RequiredDepartments =>
        OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetRequiredDepartments(Event.LoaiDuAn);
}
