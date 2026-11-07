namespace OvertimeHotel.HRM.Core.Models;

public class TaiKhoan
{
    public int MaTaiKhoan { get; set; }
    public int MaNhanVien { get; set; }
    public int MaVaiTro { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauBam { get; set; } = string.Empty;
    public bool TrangThai { get; set; } = true;
    public DateTimeOffset PhienHopLeSau { get; set; } = DateTimeOffset.UnixEpoch;

    public virtual NhanVien? NhanVien { get; set; }
    public virtual VaiTro? VaiTro { get; set; }

    // YEU_CAU_TAI_KHOAN tham chiếu TAI_KHOAN ở HAI cột khác nhau nên phải đặt
    // tên phân biệt, nếu không hai navigation sẽ trùng nhau và EF không biết
    // cột nào ứng với collection nào.
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoanDaXuLy { get; set; } = new List<YeuCauTaiKhoan>();
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoanDuocTao { get; set; } = new List<YeuCauTaiKhoan>();
}
