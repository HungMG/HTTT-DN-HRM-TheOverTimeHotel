namespace OvertimeHotel.HRM.Core.Models;

/// <summary>
/// Thực thể Ngân sách lương được phê duyệt theo năm/tháng/phòng ban (PRD 10.4 / TSK-24).
/// </summary>
public class NganSachLuong
{
    public Guid MaNganSach { get; set; }
    public long Version { get; set; } = 1;
    public int Nam { get; set; }
    public int? Thang { get; set; }
    public int? MaPhongBan { get; set; }
    public decimal SoTienNganSach { get; set; }
    public string TrangThai { get; set; } = "DRAFT";
    public int? NguoiTao { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    public int? NguoiDuyet { get; set; }
    public DateTime? NgayDuyet { get; set; }
    public string? GhiChu { get; set; }

    public PhongBan? PhongBan { get; set; }
    public NhanVien? NhanVienDuyet { get; set; }
}
