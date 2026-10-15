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
    public string HinhThucHopDong { get; set; } = "CHINH_THUC";

    public virtual NhanVien? NhanVien { get; set; }

    /// <summary>
    /// Các yêu cầu cấp tài khoản gửi dựa trên hợp đồng này. Quan hệ 1-n,
    /// phía CSDL khai ON DELETE SET NULL nên xoá hợp đồng không xoá lịch sử IT.
    /// </summary>
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoans { get; set; } = new List<YeuCauTaiKhoan>();
}
