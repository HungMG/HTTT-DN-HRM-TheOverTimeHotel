namespace OvertimeHotel.HRM.Core.Models;

public class CaLamViec
{
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public TimeOnly GioBatDau { get; set; }
    public TimeOnly GioKetThuc { get; set; }
    public bool QuaDem { get; set; } = false;

    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();
}
