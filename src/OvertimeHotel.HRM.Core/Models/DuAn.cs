namespace OvertimeHotel.HRM.Core.Models;

public class DuAn
{
    public int MaDuAn { get; set; }
    public string TenDuAn { get; set; } = "";
    public string LoaiDuAn { get; set; } = "HOI_NGHI_MICE";
    public string DiaDiem { get; set; } = "";
    public DateTimeOffset NgayBatDau { get; set; }
    public DateTimeOffset NgayKetThuc { get; set; }
    public decimal NganSachPhuCap { get; set; }
    public bool TinhPhuCapKhiNghiemThu { get; set; }
    public int MaNguoiQuanLy { get; set; }
    public int SoKhachDuKien { get; set; }
    public string? MoTa { get; set; }
    public string? LyDoHuy { get; set; }
    public string TrangThai { get; set; } = "LEN_KE_HOACH";
    public long Version { get; set; } = 1;
    public NhanVien? NguoiQuanLy { get; set; }
    public ICollection<PhanCongDuAn> PhanCongs { get; set; } = new List<PhanCongDuAn>();
}
