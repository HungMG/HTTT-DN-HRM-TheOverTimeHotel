namespace OvertimeHotel.HRM.Core.DTOs;

public record LeaveRequestCreateDto(
    int MaNhanVien,
    int MaLoaiDon,
    DateOnly TuNgay,
    DateOnly DenNgay,
    string LyDo
);

public record LeaveRequestReviewDto(
    int MaDon,
    int MaNguoiDuyet,
    bool Approved,
    string? PhanHoi
);
