namespace OvertimeHotel.HRM.Core.Models;

public class ChiTietTruViPhamTamUng
{
    public int MaChiTietVp { get; set; }
    public int MaChiTiet { get; set; }
    public string LoaiTru { get; set; } = "DI_TRE"; // DI_TRE, VE_SOM, KY_LUAT, LAM_HONG, TAM_UNG
    public int SoPhutViPham { get; set; } = 0;
    public decimal SoTienTru { get; set; } = 0.00m;
    public string LyDoChiTiet { get; set; } = string.Empty;

    public virtual ChiTietPhieuLuong? ChiTietPhieuLuong { get; set; }
}
