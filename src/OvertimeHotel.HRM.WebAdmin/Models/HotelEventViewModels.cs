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
    public IReadOnlyList<DiaDiemSuKien> Venues { get; set; } = [];
    public string VenueName { get; set; } = "";
    public int VenueCapacity { get; set; }
    public int ReturnYear { get; set; }
    public int ReturnMonth { get; set; }
    public DateOnly ReturnDate { get; set; }
}

public class EventDetailsViewModel
{
    public bool IsPopup { get; set; }
    public bool StaffLoadFailed { get; set; }
    public bool NeedsLateConfirmationWarning => OvertimeHotel.HRM.Core.Policies.HotelEventRules.NeedsLateConfirmationWarning(Event, DateTimeOffset.UtcNow);
    public DuAn Event { get; set; } = new();
    public IReadOnlyList<NhanVien> Employees { get; set; } = [];
    public IReadOnlyList<PhanCa> Shifts { get; set; } = [];
    public IReadOnlyList<NhatKyQuanTri> History { get; set; } = [];
    public bool CanManage { get; set; }
    public bool CanAccept { get; set; }
    public bool CanEdit { get; set; }
    public bool IsOffice { get; set; }
    public EventNeedInput Need { get; set; } = new();
    public EventWorkInput Work { get; set; } = new();
    public EventPayPreview? PayPreview { get; set; }
    public IReadOnlyList<KyLuong> PaymentPeriods { get; set; } = [];
    public EventAssignmentInput Assignment { get; set; } = new();
    public List<EventAssignmentInput> Assignments { get; set; } = [];
    public EventReviewInput Review { get; set; } = new();
    public int ReturnYear { get; set; }
    public int ReturnMonth { get; set; }
    public DateOnly ReturnDate { get; set; }
    public string Tab { get; set; } = "overview";
    public string? SelectedDepartment { get; set; }
    public OvertimeHotel.HRM.Core.Policies.EventStaffingSummary StaffingSummary =>
        Event.TrangThai is "HOAN_THANH" or "HUY"
            ? OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetStaffingSummary(Event.LoaiDuAn, Event.PhanCongs)
            : OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetStaffingSummary(Event);
    public IReadOnlyList<OvertimeHotel.HRM.Core.Policies.EventRoleStaffingStatus> AssignmentRoles =>
        OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetStaffingSummary(Event).Departments
            .FirstOrDefault(d => d.Code == SelectedDepartment)?.Roles ?? [];
    public IReadOnlyList<string> RequiredDepartments =>
        Event.TinhTheoGio ? Event.NhuCaus.Select(n => n.MaPhongBan)
            .Concat(Event.PhanCongs.Select(p => p.MaPhongBanSuKien ?? OvertimeHotel.HRM.Core.Policies.HotelEventRules.ResolveDepartmentCode(p.NhanVien) ?? ""))
            .Where(code => code != "").Distinct().ToList()
            : OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetRequiredDepartments(Event.LoaiDuAn);
}
