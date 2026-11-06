using System;
using System.Text.Json;
using OvertimeHotel.HRM.MobileApp.Services;

using var doc = JsonDocument.Parse("""{"ma_phan_ca":42,"bat_dau":"2026-11-02T02:00:00Z","ket_thuc":"2026-11-02T04:00:00Z","ten_du_an":"Tiệc cưới"}""");
var item = ScheduleEventMapper.Parse(doc.RootElement);
if (item.ShiftId != 42 || item.Name != "Tiệc cưới" || item.TimeText != "09:00 – 11:00") throw new Exception("Event must retain its shift, name, and hotel-local time");
Console.WriteLine("Schedule event checks passed.");
