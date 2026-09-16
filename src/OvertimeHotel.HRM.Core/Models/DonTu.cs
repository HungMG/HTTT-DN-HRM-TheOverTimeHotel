namespace OvertimeHotel.HRM.Core.Models;

public class DonTu
{
    public int MaDon { get; set; }
    public int MaNhanVien { get; set; }
    public int MaLoaiDon { get; set; }
    public int? MaNguoiDuyet { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public DateTimeOffset NgayGui { get; set; } = DateTimeOffset.UtcNow;
    public string TrangThai { get; set; } = "CHO_DUYET";
    public string? PhanHoiDuyet { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }

    public virtual NhanVien? NhanVien { get; set; }
    public virtual LoaiDon? LoaiDon { get; set; }
    public virtual NhanVien? NguoiDuyet { get; set; }
}
