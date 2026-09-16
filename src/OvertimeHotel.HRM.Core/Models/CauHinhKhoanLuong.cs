namespace OvertimeHotel.HRM.Core.Models;

public class CauHinhKhoanLuong
{
    public int MaKhoan { get; set; }
    public string TenKhoan { get; set; } = string.Empty;
    public string LoaiKhoan { get; set; } = "PHU_CAP"; // PHU_CAP, THUONG, KHAU_TRU
    public decimal GiaTriMacDinh { get; set; } = 0.00m;
    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new List<ChiTietPhieuLuong>();
}
