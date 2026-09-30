namespace OvertimeHotel.HRM.Core.Models;

public class CauHinhTangCa
{
    public int MaLoaiOt { get; set; }
    public string TenLoaiOt { get; set; } = string.Empty;
    public decimal HeSo { get; set; } = 1.50m;
    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new List<ChiTietPhieuLuong>();
}
