namespace OvertimeHotel.HRM.WebAdmin.Models;

public class HomeDashboardViewModel
{
    // Task 15: Nhân viên & HĐLĐ
    public int TongNhanVien { get; set; } = 20;
    public int NhanVienDangLam { get; set; } = 20;
    public int TongHopDong { get; set; } = 20;
    public int HopDongHieuLuc { get; set; } = 20;
    public int HopDongSapHetHan { get; set; } = 2;

    // Task 14: Danh mục hệ thống
    public int TongPhongBan { get; set; } = 8;
    public int TongNhanVienPhongBan { get; set; } = 20;
    public int TongChucVu { get; set; } = 10;
    public int TongCaLamViec { get; set; } = 3;
    public int SoCaQuaDem { get; set; } = 1;
    public int TongKhoanLuong { get; set; }
    public int SoLoaiTangCa { get; set; }
    public int SoKhoanPhuCap { get; set; }
    public int SoKhoanThuong { get; set; }
    public int SoKhoanKhauTru { get; set; }

    // Task 13: Quản trị tài khoản
    public int TongTaiKhoan { get; set; } = 6;
    public int TaiKhoanHoatDong { get; set; } = 6;

    // Task 16: Phân ca 24/7
    public int TongPhanCa { get; set; } = 4;
    public int SoCaDem { get; set; } = 1;

    // Trạng thái hệ thống
    public bool IsDatabaseOnline { get; set; } = true;
}
