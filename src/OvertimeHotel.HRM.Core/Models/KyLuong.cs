namespace OvertimeHotel.HRM.Core.Models;

public class KyLuong
{
    public int MaKyLuong { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public string TrangThai { get; set; } = "MO"; // MO, NHAP, CHO_PHAN_HOI, CHO_DUYET, DA_DUYET, TU_CHOI, KHOA_SO

    public DateTimeOffset? NgayPhatHanhNhap { get; set; }
    public string? NguoiPhatHanhNhap { get; set; }
    public DateTimeOffset? HanPhanHoiNhap { get; set; }

    public DateTimeOffset? NgayGuiDuyet { get; set; }
    public string? NguoiGuiDuyet { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? NguoiDuyet { get; set; }
    public string? LyDoTuChoi { get; set; }
    public DateTimeOffset? NgayKhoaSo { get; set; }
    public string? NguoiKhoaSo { get; set; }

    public virtual ICollection<PhieuLuong> PhieuLuongs { get; set; } = new List<PhieuLuong>();
}
