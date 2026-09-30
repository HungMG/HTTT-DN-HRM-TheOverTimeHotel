namespace OvertimeHotel.HRM.Core.Models;

public class CauHinhPhuCap
{
    public int MaPhuCap { get; set; }
    public string TenPhuCap { get; set; } = string.Empty;
    public string LoaiTinh { get; set; } = "THEO_NGAY_CONG"; // THEO_NGAY_CONG, CO_DINH_THANG
    public decimal MucDinhMuc { get; set; } = 0.00m;
    public bool ChiuThueTncn { get; set; } = false;
    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new List<ChiTietPhieuLuong>();
}
