namespace OvertimeHotel.HRM.MobileApp.Models;

public record ScheduleEventItem(int ShiftId, string Name, DateTimeOffset Start, DateTimeOffset End)
{
    public string TimeText => $"{Start.ToOffset(TimeSpan.FromHours(7)):HH:mm} – {End.ToOffset(TimeSpan.FromHours(7)):HH:mm}";
}
