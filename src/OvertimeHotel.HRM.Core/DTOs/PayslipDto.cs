namespace OvertimeHotel.HRM.Core.DTOs;

public record PayslipSummaryDto(
    int MaPhieuLuong,
    int MaNhanVien,
    string HoTen,
    string TenPhongBan,
    string TenChucVu,
    int Thang,
    int Nam,
    decimal LuongCoBan,
    decimal NgayCongThucTe,
    decimal TienLuongCong,
    decimal TienTangCa,
    decimal TongPhuCap,
    decimal TongThuong,
    decimal TongKhauTru,
    decimal ThucNhan,
    string TrangThai
);

public record PayslipItemDto(
    int MaChiTiet,
    string TenKhoan,
    string LoaiKhoan,
    decimal SoTien,
    string? GhiChu
);

public record PayslipDetailDto(
    PayslipSummaryDto Summary,
    List<PayslipItemDto> Items
);
