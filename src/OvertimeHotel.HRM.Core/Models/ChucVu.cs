namespace OvertimeHotel.HRM.Core.Models;

public class ChucVu
{
    public int MaChucVu { get; set; }
    public int? MaPhongBan { get; set; }
    public string TenChucVu { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    public virtual PhongBan? PhongBan { get; set; }
    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();

    /// <summary>Các dòng định biên tối thiểu theo ca áp cho chức vụ này (PRD 3.5).</summary>
    public virtual ICollection<DinhBienCa> DinhBienCas { get; set; } = new List<DinhBienCa>();

    /// <summary>Định biên riêng theo ngày cho chức vụ này.</summary>
    public virtual ICollection<DinhBienNgay> DinhBienNgays { get; set; } = new List<DinhBienNgay>();
}
