namespace OvertimeHotel.HRM.Core.DTOs;

/// <summary>
/// Ca làm việc trong 45 ngày qua có thể nộp giải trình chấm công (quên quẹt, sai giờ).
/// </summary>
public record EligibleShiftDto(
    int MaPhanCa,
    DateOnly NgayLamViec,
    int MaCa,
    string TenCa,
    TimeOnly GioBatDau,
    TimeOnly GioKetThuc,
    DateTimeOffset? GioVaoThucTe,
    DateTimeOffset? GioRaThucTe,
    string? TrangThaiChamCong
);

/// <summary>
/// Dữ liệu một bản ghi giải trình chấm công trả về cho Mobile App và WebAdmin.
/// </summary>
public record AttendanceExplanationItemDto(
    int MaGiaiTrinh,
    int MaPhanCa,
    int MaNhanVien,
    string HoTenNhanVien,
    string? TenPhongBan,
    DateOnly NgayLamViec,
    string TenCa,
    DateTimeOffset? GioDeXuatVao,
    DateTimeOffset? GioDeXuatRa,
    DateTimeOffset? GioVaoThucTe,
    DateTimeOffset? GioRaThucTe,
    string LyDo,
    string? MinhChung,
    string TrangThai,
    string TrangThaiLabel,
    DateTimeOffset NgayGui,
    int? MaQuanLyXacMinh,
    string? HoTenQuanLy,
    DateTimeOffset? NgayQuanLyXacMinh,
    string? YKienQuanLy,
    int? MaHrXacNhan,
    string? HoTenHr,
    DateTimeOffset? NgayHrXacNhan,
    string? YKienHr,
    bool CanWithdraw
)
{
    public bool HasYKienQuanLy => !string.IsNullOrWhiteSpace(YKienQuanLy);
    public bool HasYKienHr => !string.IsNullOrWhiteSpace(YKienHr);
}

/// <summary>
/// Yêu cầu tạo giải trình chấm công mới từ Mobile App.
/// </summary>
public record CreateAttendanceExplanationRequest(
    int MaPhanCa,
    DateTimeOffset? GioDeXuatVao,
    DateTimeOffset? GioDeXuatRa,
    string LyDo,
    string? MinhChung
);

/// <summary>
/// Kết quả thao tác giải trình (nộp, rút, duyệt, từ chối).
/// </summary>
public record AttendanceExplanationOperationResult(
    bool Success,
    string Code,
    string Message,
    int? MaGiaiTrinh = null
);
