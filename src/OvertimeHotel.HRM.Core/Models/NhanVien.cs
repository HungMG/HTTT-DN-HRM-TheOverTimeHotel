namespace OvertimeHotel.HRM.Core.Models;

public class NhanVien
{
    public int MaNhanVien { get; set; }
    public int MaPhongBan { get; set; }
    public int MaChucVu { get; set; }
    public string Ho { get; set; } = string.Empty;
    public string Ten { get; set; } = string.Empty;
    public DateOnly NgaySinh { get; set; }
    public string GioiTinh { get; set; } = "NAM";
    public string DienThoai { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DiaChi { get; set; }
    public string TrinhDo { get; set; } = string.Empty;
    public DateOnly NgayVaoLam { get; set; }
    public DateOnly? NgayThoiViec { get; set; }
    public string TrangThai { get; set; } = "DANG_LAM";

    // Tiện ích tính nhanh họ tên hiển thị
    public string HoTen => $"{Ho} {Ten}".Trim();

    public virtual PhongBan? PhongBan { get; set; }
    public virtual ChucVu? ChucVu { get; set; }
    public virtual TaiKhoan? TaiKhoan { get; set; }
    public virtual ICollection<HopDong> HopDongs { get; set; } = new List<HopDong>();
    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();
    public virtual ICollection<DonTu> DonTus { get; set; } = new List<DonTu>();
    public virtual ICollection<DonTu> DonTuDaDuyets { get; set; } = new List<DonTu>();
    public virtual ICollection<PhieuLuong> PhieuLuongs { get; set; } = new List<PhieuLuong>();
}
