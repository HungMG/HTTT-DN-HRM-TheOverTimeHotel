namespace OvertimeHotel.HRM.Core.Models;

public class HopDong
{
    public int MaHopDong { get; set; }
    public int MaNhanVien { get; set; }
    public string SoHopDong { get; set; } = string.Empty;
    public string LoaiHopDong { get; set; } = string.Empty;
    public DateOnly NgayBatDau { get; set; }
    public DateOnly? NgayKetThuc { get; set; }
    public decimal LuongCoBan { get; set; }
    public string TrangThai { get; set; } = "HIEU_LUC";

    public virtual NhanVien? NhanVien { get; set; }
}
