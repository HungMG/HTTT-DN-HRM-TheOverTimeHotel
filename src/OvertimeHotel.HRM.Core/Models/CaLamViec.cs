namespace OvertimeHotel.HRM.Core.Models;

public class CaLamViec
{
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public TimeOnly GioBatDau { get; set; }
    public TimeOnly GioKetThuc { get; set; }
    public bool QuaDem { get; set; } = false;

    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();

    /// <summary>Định biên tối thiểu theo chức vụ cho ca này (PRD 3.5).</summary>
    public virtual ICollection<DinhBienCa> DinhBienCas { get; set; } = new List<DinhBienCa>();

    /// <summary>Định biên riêng theo từng ngày cho ca này (đè lên mức nền ở trên).</summary>
    public virtual ICollection<DinhBienNgay> DinhBienNgays { get; set; } = new List<DinhBienNgay>();
}
