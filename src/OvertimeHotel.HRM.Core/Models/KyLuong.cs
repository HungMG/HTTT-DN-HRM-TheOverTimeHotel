namespace OvertimeHotel.HRM.Core.Models;

public class KyLuong
{
    public int MaKyLuong { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public string TrangThai { get; set; } = "MO";

    public virtual ICollection<PhieuLuong> PhieuLuongs { get; set; } = new List<PhieuLuong>();
}
