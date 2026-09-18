namespace OvertimeHotel.HRM.Core.Models;

public class NhatKyQuanTri
{
    public long MaNhatKy { get; set; }
    public int? MaTaiKhoanThucHien { get; set; }
    public int? MaTaiKhoanBiTacDong { get; set; }
    public string TenNguoiThucHien { get; set; } = string.Empty;
    public string TenTaiKhoanBiTacDong { get; set; } = string.Empty;
    public string HanhDong { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public string? GiaTriCu { get; set; }
    public string? GiaTriMoi { get; set; }
    public string? LyDo { get; set; }
    public string? DiaChiIp { get; set; }
    public DateTimeOffset ThoiGian { get; set; } = DateTimeOffset.UtcNow;
}
