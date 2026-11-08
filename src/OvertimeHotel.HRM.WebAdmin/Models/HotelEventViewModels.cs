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
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? ExistingType { get; set; }
    public IReadOnlyList<SelectListItem> Managers { get; set; } = [];
    /// <summary>Hệ số thưởng sự kiện theo giờ đang áp dụng (đọc từ cấu hình khoản lương).</summary>
    public decimal HourlyEventRate { get; set; } = OvertimeHotel.HRM.Core.Constants.SalaryConstants.EVENT_HOURLY_RATE;
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
    /// <summary>Hệ số thưởng sự kiện theo giờ đang áp dụng (đọc từ cấu hình khoản lương).</summary>
    public decimal HourlyEventRate { get; set; } = OvertimeHotel.HRM.Core.Constants.SalaryConstants.EVENT_HOURLY_RATE;
    public IReadOnlyList<NhanVien> Employees { get; set; } = [];
    public IReadOnlyList<PhanCa> Shifts { get; set; } = [];
    public bool CanManage { get; set; }
    public bool CanAccept { get; set; }
    public bool CanEdit { get; set; }
    public bool IsOffice { get; set; }
    public EventNeedInput Need { get; set; } = new();
    public EventWorkInput Work { get; set; } = new();
    public EventPayPreview? PayPreview { get; set; }
    public IReadOnlyList<KyLuong> PaymentPeriods { get; set; } = [];
    public int? PaymentPeriodId { get; set; }
    public IReadOnlyList<PhanCongDuAn> AcceptanceAssignments => Event.PhanCongs
        .Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY") || p.CongThucTes.Count > 0).ToList();
    public int AcceptanceStaffCount => AcceptanceAssignments.Select(p => p.MaNhanVien).Distinct().Count();
    public int ConfirmedStaffCount => AcceptanceAssignments.GroupBy(p => p.MaNhanVien).Count(g => g.All(p =>
        p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY")
        && (p.DanhGia == "KHONG_THAM_GIA" ? p.NgayDanhGia.HasValue && p.CongThucTes.Count == 0
            : Event.TrangThai == "HOAN_THANH" ? p.NgayDanhGia.HasValue && p.DanhGia is "HOAN_THANH" or "XUAT_SAC"
            : OvertimeHotel.HRM.Core.Policies.HourlyEventRules.AutomaticRating(p) != null)));
    public int MissingWorkStaffCount => AcceptanceAssignments.Where(p => p.DanhGia != "KHONG_THAM_GIA" && p.CongThucTes.Count == 0)
        .Select(p => p.MaNhanVien).Distinct().Count();
    public int UnreviewedStaffCount => AcceptanceAssignments.Where(p => Event.TrangThai == "HOAN_THANH" || p.DanhGia == "KHONG_THAM_GIA"
        ? !p.NgayDanhGia.HasValue || p.DanhGia == "CHUA_DANH_GIA"
        : OvertimeHotel.HRM.Core.Policies.HourlyEventRules.AutomaticRating(p) == null)
        .Select(p => p.MaNhanVien).Distinct().Count();
    public int AbsentStaffCount => AcceptanceAssignments.GroupBy(p => p.MaNhanVien).Count(g =>
        g.All(p => p.DanhGia == "KHONG_THAM_GIA" && p.NgayDanhGia.HasValue && p.CongThucTes.Count == 0));
    public IReadOnlyList<KyLuong> EligiblePaymentPeriods => PaymentPeriods.Where(k =>
        (k.TrangThai is "MO" or "NHAP" or "TU_CHOI") && !k.NgayDuyet.HasValue
        && (!k.NgayGuiDuyet.HasValue || !string.IsNullOrEmpty(k.LyDoTuChoi))
        && (PayPreview?.Rows.Count is not > 0 || new DateOnly(k.Nam, k.Thang, 1) >= PayPreview.Rows.Max(r => r.WorkMonth))).ToList();
    public bool CanFinalizeAcceptance => CanAccept && IsOffice && !StaffLoadFailed
        && Event.TrangThai == "CHO_NGHIEM_THU" && (Event.KetThucThucTe ?? Event.NgayKetThuc) <= DateTimeOffset.UtcNow
        && OvertimeHotel.HRM.Core.Policies.HotelEventRules.CountEffectiveStaff(Event.PhanCongs) > 0
        && PayPreview != null && PayPreview.Version == Event.Version && PayPreview.Warnings.Count == 0
        && ConfirmedStaffCount == AcceptanceStaffCount && EligiblePaymentPeriods.Count > 0;
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
        (Event.TinhTheoGio ? Enumerable.Empty<string>() : OvertimeHotel.HRM.Core.Policies.HotelEventRules.GetRequiredDepartments(Event.LoaiDuAn))
            .Concat(Event.NhuCaus.Select(n => n.MaPhongBan))
            .Concat(Event.PhanCongs.Select(p => p.MaPhongBanSuKien ?? OvertimeHotel.HRM.Core.Policies.HotelEventRules.ResolveDepartmentCode(p.NhanVien) ?? ""))
            .Where(code => code != "").Distinct().ToList();
}
