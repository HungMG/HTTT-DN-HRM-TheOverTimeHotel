namespace OvertimeHotel.HRM.Core.Models;

public class ChucVu
{
    public int MaChucVu { get; set; }
    public string TenChucVu { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
}
