namespace OvertimeHotel.HRM.Core.DTOs;

public record CheckInRequest(
    int MaPhanCa,
    DateTimeOffset Timestamp
);

public record CheckInResponse(
    bool Success,
    string Message,
    int? MaChamCong = null,
    int PhutDiTre = 0,
    int PhutVeSom = 0,
    decimal GioOt = 0m,
    string? TrangThai = null
);
