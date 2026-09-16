namespace OvertimeHotel.HRM.Core.Models;

public class TaiKhoan
{
    public int MaTaiKhoan { get; set; }
    public int MaNhanVien { get; set; }
    public int MaVaiTro { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauBam { get; set; } = string.Empty;
    public bool TrangThai { get; set; } = true;

    public virtual NhanVien? NhanVien { get; set; }
    public virtual VaiTro? VaiTro { get; set; }
}
