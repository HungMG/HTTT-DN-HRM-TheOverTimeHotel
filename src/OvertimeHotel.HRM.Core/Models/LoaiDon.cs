namespace OvertimeHotel.HRM.Core.Models;

public class LoaiDon
{
    public int MaLoaiDon { get; set; }
    public string TenLoaiDon { get; set; } = string.Empty;
    public bool CoHuongLuong { get; set; } = false;
    public string? MoTa { get; set; }

    public virtual ICollection<DonTu> DonTus { get; set; } = new List<DonTu>();
}
