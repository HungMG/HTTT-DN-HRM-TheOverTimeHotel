namespace OvertimeHotel.HRM.Core.Models;

public class CauHinhKhauTru
{
    public int MaKhoanTru { get; set; }
    public string TenKhoanTru { get; set; } = string.Empty;
    public string LoaiKhoanTru { get; set; } = "BAO_HIEM"; // BAO_HIEM, THUE, VI_PHAM, TAM_UNG
    public decimal TyLeHoacDinhMuc { get; set; } = 0.00m;
    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new List<ChiTietPhieuLuong>();
}
