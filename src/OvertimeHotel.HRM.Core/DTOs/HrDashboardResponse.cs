namespace OvertimeHotel.HRM.Core.DTOs;

public sealed class HrDashboardResponse
{
    public DateTimeOffset UpdatedAt { get; set; }
    public List<HrDashboardStaff> Staff { get; set; } = [];
    public HrShiftSummary Morning { get; set; } = new();
    public HrShiftSummary Afternoon { get; set; } = new();
    public HrShiftSummary Night { get; set; } = new();
    public int OnDutyCount { get; set; }
    public int LeaveCount { get; set; }
    public int AbsentCount { get; set; }
    public int PendingCount { get; set; }
    public int ReviewedCount { get; set; }
    public int ExpiringContractCount { get; set; }
    public string ContractDescription { get; set; } = "";
    public List<HrDashboardEvent> Events { get; set; } = [];
}
public sealed class HrShiftSummary
{
    public int CheckedIn { get; set; }
    public int Scheduled { get; set; }
    public string Display => $"{CheckedIn}/{Scheduled}";
    public string Status => Scheduled == 0 ? "Chưa có lịch" : "Đã vào / được xếp";
}
public sealed class HrDashboardStaff
{
    public string StaffCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Position { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WorkStatus { get; set; } = "";
    public string WorkStatusDetail { get; set; } = "";
}
public sealed class HrDashboardEvent
{
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
}
