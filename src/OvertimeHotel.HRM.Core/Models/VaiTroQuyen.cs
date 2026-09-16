namespace OvertimeHotel.HRM.Core.Models;

public class VaiTroQuyen
{
    public int MaVaiTro { get; set; }
    public int MaQuyen { get; set; }

    public virtual VaiTro? VaiTro { get; set; }
    public virtual Quyen? Quyen { get; set; }
}
