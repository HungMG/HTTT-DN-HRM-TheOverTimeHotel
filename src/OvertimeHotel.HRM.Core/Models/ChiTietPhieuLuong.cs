namespace OvertimeHotel.HRM.Core.Models;

public class ChiTietPhieuLuong
{
    public int MaChiTiet { get; set; }
    public int MaPhieuLuong { get; set; }
    public int MaKhoan { get; set; }
    public string TenKhoanLuu { get; set; } = string.Empty;
    public string LoaiKhoanLuu { get; set; } = string.Empty;
    public decimal SoTien { get; set; }
    public string? GhiChu { get; set; }

    public virtual PhieuLuong? PhieuLuong { get; set; }
    public virtual CauHinhKhoanLuong? CauHinhKhoanLuong { get; set; }
}
