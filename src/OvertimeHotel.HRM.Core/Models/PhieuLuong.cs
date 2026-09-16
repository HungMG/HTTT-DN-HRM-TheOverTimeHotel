namespace OvertimeHotel.HRM.Core.Models;

public class PhieuLuong
{
    public int MaPhieuLuong { get; set; }
    public int MaNhanVien { get; set; }
    public int MaKyLuong { get; set; }
    public decimal LuongCoBan { get; set; }
    public decimal NgayCongThucTe { get; set; } = 0.00m;
    public decimal TienLuongCong { get; set; } = 0.00m;
    public decimal TienTangCa { get; set; } = 0.00m;
    public decimal TongPhuCap { get; set; } = 0.00m;
    public decimal TongThuong { get; set; } = 0.00m;
    public decimal TongKhauTru { get; set; } = 0.00m;
    public decimal ThucNhan { get; set; }
    public string TrangThai { get; set; } = "NHAP";

    public virtual NhanVien? NhanVien { get; set; }
    public virtual KyLuong? KyLuong { get; set; }
    public virtual ICollection<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new List<ChiTietPhieuLuong>();
}
