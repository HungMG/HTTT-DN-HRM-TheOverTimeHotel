namespace OvertimeHotel.HRM.Core.Models;

public class ChamCong
{
    public int MaChamCong { get; set; }
    public int MaPhanCa { get; set; }
    public DateTimeOffset? GioCheckIn { get; set; }
    public DateTimeOffset? GioCheckOut { get; set; }
    public int PhutDiTre { get; set; } = 0;
    public int PhutVeSom { get; set; } = 0;
    public decimal GioTangCaOt { get; set; } = 0.00m;
    public string TrangThai { get; set; } = "DUNG_GIO";

    public virtual PhanCa? PhanCa { get; set; }
}
