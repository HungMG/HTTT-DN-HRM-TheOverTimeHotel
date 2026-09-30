namespace OvertimeHotel.HRM.Core.Models;

public class ChiTietTruThueTncn
{
    public int MaChiTietThue { get; set; }
    public int MaChiTiet { get; set; }
    public decimal ThuNhapChiuThue { get; set; } = 0.00m;
    public decimal GiamTruBanThan { get; set; } = 11000000.00m;
    public int SoNguoiPhuThuoc { get; set; } = 0;
    public decimal GiamTruPhuThuoc { get; set; } = 0.00m;
    public decimal ThuNhapTinhThue { get; set; } = 0.00m;
    public decimal TienThue { get; set; } = 0.00m;

    public virtual ChiTietPhieuLuong? ChiTietPhieuLuong { get; set; }
}
