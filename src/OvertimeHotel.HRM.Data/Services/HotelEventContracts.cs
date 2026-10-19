using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Services;

public record EventActor(int EmployeeId, int AccountId, string Role, string Name)
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
    public string? Description { get; set; }
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
}

public class EventReviewInput
{
    public long Version { get; set; }
    public int AssignmentId { get; set; }
    public string Rating { get; set; } = "HOAN_THANH";
    public decimal ActualAllowance { get; set; }
}

public class EventOperationException(string field, string message, bool forbidden = false) : Exception(message)
{
    public string Field { get; } = field;
    public bool Forbidden { get; } = forbidden;
}
