namespace OvertimeHotel.HRM.Core.Models;

public class ChiTietPhieuLuong
{
    public int MaChiTiet { get; set; }
    public int MaPhieuLuong { get; set; }
    public int? MaKhoan { get; set; }
    public int? MaLoaiOt { get; set; }
    public int? MaPhuCap { get; set; }
    public int? MaKhoanTru { get; set; }
    public string TenKhoanLuu { get; set; } = string.Empty;
    public string LoaiKhoanLuu { get; set; } = string.Empty;
    public string? LoaiKhoan { get; set; }
    public decimal SoTien { get; set; }
    public string? GhiChu { get; set; }

    public virtual PhieuLuong? PhieuLuong { get; set; }
    public virtual CauHinhKhoanLuong? CauHinhKhoanLuong { get; set; }
    public virtual CauHinhTangCa? CauHinhTangCa { get; set; }
    public virtual CauHinhPhuCap? CauHinhPhuCap { get; set; }
    public virtual CauHinhKhauTru? CauHinhKhauTru { get; set; }

    public virtual ICollection<ChiTietTruBaoHiem> ChiTietTruBaoHiems { get; set; } = new List<ChiTietTruBaoHiem>();
    public virtual ICollection<ChiTietTruThueTncn> ChiTietTruThueTncns { get; set; } = new List<ChiTietTruThueTncn>();
    public virtual ICollection<ChiTietTruViPhamTamUng> ChiTietTruViPhamTamUngs { get; set; } = new List<ChiTietTruViPhamTamUng>();
}
