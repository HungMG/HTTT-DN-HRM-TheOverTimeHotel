using System;
using OvertimeHotel.HRM.MobileApp.Models;

var feb = new DateOnly(2026, 2, 1);
if (ScheduleCalendar.StartOfWeek(feb) != new DateOnly(2026, 1, 26)) throw new Exception("Week must start Monday across months");
if (ScheduleCalendar.GridStart(feb) != new DateOnly(2026, 1, 26)) throw new Exception("Calendar must align Monday");
if (ScheduleCalendar.GridEnd(feb) != new DateOnly(2026, 3, 1)) throw new Exception("Calendar must include full last week");
if (ScheduleCalendar.GridEnd(new DateOnly(2024, 2, 15)) != new DateOnly(2024, 3, 3)) throw new Exception("Leap month must include full last week");
Console.WriteLine("Schedule calendar checks passed.");
