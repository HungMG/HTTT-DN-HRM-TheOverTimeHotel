namespace OvertimeHotel.HRM.Core.Models;

public class Quyen
{
    public int MaQuyen { get; set; }
    public string MaQuyenCode { get; set; } = string.Empty;
    public string TenQuyen { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    public virtual ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = new List<VaiTroQuyen>();
}
