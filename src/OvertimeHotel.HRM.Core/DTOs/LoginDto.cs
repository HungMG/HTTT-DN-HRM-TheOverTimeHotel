namespace OvertimeHotel.HRM.Core.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    bool Success,
    string Message,
    int? MaTaiKhoan = null,
    int? MaNhanVien = null,
    string? HoTen = null,
    string? TenDangNhap = null,
    string? TenVaiTro = null,
    string? Token = null
);
