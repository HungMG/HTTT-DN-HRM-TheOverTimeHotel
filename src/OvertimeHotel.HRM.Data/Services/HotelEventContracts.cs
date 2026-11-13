using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Services;

public record EventActor(int EmployeeId, int AccountId, string Role, string Name, bool CanManageEvents = false, bool CanPublishSchedules = false)
{
    public bool IsOffice => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || Role.Equals("HR", StringComparison.OrdinalIgnoreCase);
    public bool IsManager => Role.Equals("Manager", StringComparison.OrdinalIgnoreCase);
}

public class EventInput
{
    public int Id { get; set; }
    public long Version { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "HOI_NGHI_MICE";
    public string Venue { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int ManagerId { get; set; }
    public int ExpectedGuests { get; set; }
    public decimal Budget { get; set; }
    public bool CalculateBudgetAtAcceptance { get; set; }
    public string? Description { get; set; }
    public bool HourlyCompensation { get; set; }
    // Kept for older clients; saving an event always fixes its location and clears both buffers.
    public int? VenueId { get; set; }
    public int SetupMinutes { get; set; }
    public int TeardownMinutes { get; set; }
}

public class EventAssignmentInput
{
    public long Version { get; set; }
    public int EmployeeId { get; set; }
    public string Role { get; set; } = "";
    public string? OtherRole { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal Allowance { get; set; }
    public int? ShiftId { get; set; }
}

public class EventReviewInput
{
    public long Version { get; set; }
    public int AssignmentId { get; set; }
    public string Rating { get; set; } = "HOAN_THANH";
    public decimal ActualAllowance { get; set; }
}

public class EventNeedInput
{
    public long Version { get; set; }
    public int NeedId { get; set; }
    public string Department { get; set; } = "";
    public string Role { get; set; } = "";
    public int Count { get; set; }
    public string Reason { get; set; } = "";
}

public class EventWorkInput
{
    public long Version { get; set; }
    public int AssignmentId { get; set; }
    public int WorkId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? Reason { get; set; }
}

public record EventPayRow(int EmployeeId, string EmployeeName, DateOnly WorkMonth, decimal Minutes,
    decimal Amount, IReadOnlyList<OvertimeHotel.HRM.Core.Policies.EventPaySlice> Basis, EventPayTransfer? Transfer = null);
public record EventPayTransfer(int PeriodId, int Month, int Year, string PeriodStatus, int? DetailId, int? PayslipId);
public record EventPayPreview(long Version, IReadOnlyList<EventPayRow> Rows, IReadOnlyList<string> Warnings, string Token)
{
    public decimal Total => Rows.Sum(r => r.Amount);
}

public class EventOperationException(string field, string message, bool forbidden = false) : Exception(message)
{
    public string Field { get; } = field;
    public bool Forbidden { get; } = forbidden;
}
