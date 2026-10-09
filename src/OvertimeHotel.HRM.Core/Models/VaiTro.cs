namespace OvertimeHotel.HRM.Core.Models;

public class VaiTro
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    public virtual ICollection<TaiKhoan> TaiKhoans { get; set; } = new List<TaiKhoan>();
    public virtual ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = new List<VaiTroQuyen>();
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoans { get; set; } = new List<YeuCauTaiKhoan>();
}
