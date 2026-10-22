namespace OvertimeHotel.HRM.Core.Models;

public class PhanCongDuAn
{
    public int MaPhanCong { get; set; }
    public int MaDuAn { get; set; }
    public int MaNhanVien { get; set; }
    public string VaiTroDuAn { get; set; } = "";
    public string? MaPhongBanSuKien { get; set; }
    public string? NhiemVuCuThe { get; set; }
    public decimal PhuCapDuKien { get; set; }
    public decimal SoTienPhuCap { get; set; }
    public DateTimeOffset BatDau { get; set; }
    public DateTimeOffset KetThuc { get; set; }
    public string DanhGia { get; set; } = "CHUA_DANH_GIA";
    public string TrangThaiChiTra { get; set; } = "CHUA_CHI_TRA";
    public DateTimeOffset? NgayDanhGia { get; set; }
    public int? MaChiTiet { get; set; }
    public int? MaPhanCa { get; set; }
    public string TrangThaiThamGia { get; set; } = "DA_PHAN";
    public string? LyDoKetThuc { get; set; }
    public PhanCa? PhanCa { get; set; }
    public ICollection<CongSuKien> CongThucTes { get; set; } = new List<CongSuKien>();
    public DuAn? DuAn { get; set; }
    public NhanVien? NhanVien { get; set; }
}
