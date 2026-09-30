namespace OvertimeHotel.HRM.Core.Models;

public class ChiTietTruBaoHiem
{
    public int MaChiTietBh { get; set; }
    public int MaChiTiet { get; set; }
    public decimal LuongDongBaoHiem { get; set; } = 0.00m;
    public decimal TyLeBhxh { get; set; } = 8.00m;
    public decimal TienBhxh { get; set; } = 0.00m;
    public decimal TyLeBhyt { get; set; } = 1.50m;
    public decimal TienBhyt { get; set; } = 0.00m;
    public decimal TyLeBhtn { get; set; } = 1.00m;
    public decimal TienBhtn { get; set; } = 0.00m;
    public decimal TongBaoHiem { get; set; } = 0.00m;

    public virtual ChiTietPhieuLuong? ChiTietPhieuLuong { get; set; }
}
