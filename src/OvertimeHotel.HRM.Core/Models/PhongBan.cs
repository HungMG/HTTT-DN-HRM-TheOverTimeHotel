namespace OvertimeHotel.HRM.Core.Models;

public class PhongBan
{
    public int MaPhongBan { get; set; }
    public string TenPhongBan { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
}
