using System;

namespace OvertimeHotel.HRM.MobileApp.Models;

public static class ScheduleCalendar
{
    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static DateOnly GridStart(DateOnly date) => StartOfWeek(new DateOnly(date.Year, date.Month, 1));
    public static DateOnly GridEnd(DateOnly date) => StartOfWeek(new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))).AddDays(6);
}
