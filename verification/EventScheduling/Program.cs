using OvertimeHotel.HRM.Core.Policies;

static void Check(DateTimeOffset now, DateOnly eventDate, bool expected)
{
    var actual = HotelEventRules.CanCreateOn(eventDate, now);
    if (actual != expected)
        throw new Exception($"{now:O}, event {eventDate}: expected {expected}, got {actual}");
}

// Sunday 1 November 2026, at the cutoff in Vietnam (UTC+7).
var before = new DateTimeOffset(2026, 11, 1, 9, 59, 59, TimeSpan.Zero);
var cutoff = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
Check(before, new DateOnly(2026, 11, 1), false);
Check(before, new DateOnly(2026, 11, 2), true);
Check(before, new DateOnly(2026, 11, 8), true);
Check(cutoff, new DateOnly(2026, 11, 8), false);
Check(cutoff, new DateOnly(2026, 11, 9), true);
Check(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), new DateOnly(2026, 11, 8), false);
Check(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), new DateOnly(2026, 11, 9), true);
Check(new DateTimeOffset(2026, 12, 27, 10, 0, 0, TimeSpan.Zero), new DateOnly(2027, 1, 4), true);
Console.WriteLine("Event scheduling cutoff checks passed.");
