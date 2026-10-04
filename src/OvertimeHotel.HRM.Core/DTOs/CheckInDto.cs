namespace OvertimeHotel.HRM.Core.DTOs;

public record CheckInRequest(
    int MaPhanCa,
    DateTimeOffset Timestamp,
    string? PhuongThuc = "GPS",
    string? ToaDo = null,
    string? ThietBi = null
);

public record CheckInResponse(
    bool Success,
    string Message,
    int? MaChamCong = null,
    int PhutDiTre = 0,
    int PhutVeSom = 0,
    decimal GioOt = 0m,
    string? TrangThai = null,
    string? Code = null
);

/// <summary>
/// Ca làm việc được phân cho nhân viên trong ngày, kèm trạng thái đã quẹt thẻ hay chưa.
/// </summary>
public record TodayShiftDto(
    int MaPhanCa,
    string TenCa,
    DateTimeOffset GioBatDau,
    DateTimeOffset GioKetThuc,
    DateTimeOffset? DaCheckIn,
    DateTimeOffset? DaCheckOut,
    int PhutDiTre = 0,
    int PhutVeSom = 0,
    decimal GioOt = 0m,
    string? TrangThai = null
);

/// <summary>
/// Một dòng lịch sử chấm công trong tháng, dùng cho màn hình lịch sử trên Mobile App.
/// </summary>
public record MonthlyAttendanceDto(
    int MaPhanCa,
    DateOnly NgayLamViec,
    string TenCa,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    int PhutDiTre,
    int PhutVeSom,
    decimal GioOt,
    string TrangThai
);
