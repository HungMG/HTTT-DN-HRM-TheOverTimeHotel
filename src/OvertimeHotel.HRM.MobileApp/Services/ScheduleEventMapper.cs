using System.Text.Json;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

public static class ScheduleEventMapper
{
    public static ScheduleEventItem Parse(JsonElement row)
    {
        var shiftId = PostgrstJson.ReadNullableInt(row, "ma_phan_ca") ?? throw new JsonException("Thiếu ca sự kiện.");
        var name = PostgrstJson.ReadString(row, "ten_du_an");
        var start = PostgrstJson.ReadNullableTimestamp(row, "bat_dau");
        var end = PostgrstJson.ReadNullableTimestamp(row, "ket_thuc");
        if (string.IsNullOrWhiteSpace(name) || start is null || end is null || end <= start)
            throw new JsonException("Dữ liệu sự kiện không hợp lệ.");
        return new ScheduleEventItem(shiftId, name, start.Value, end.Value);
    }
}
