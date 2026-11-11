namespace OvertimeHotel.HRM.Core.Models;

public class PhuLucLuong
{
    public long MaPhuLuc { get; set; }
    public int MaHopDong { get; set; }
    public DateOnly NgayHieuLuc { get; set; }
    public decimal LuongCoBanMoi { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public string NguoiTao { get; set; } = string.Empty;
    public DateTimeOffset TaoLuc { get; set; } = DateTimeOffset.UtcNow;
    // Both values are present for an organizational move; null means salary only.
    public int? MaChucVuMoi { get; set; }
    public int? MaPhongBanMoi { get; set; }
    public virtual HopDong? HopDong { get; set; }
}
