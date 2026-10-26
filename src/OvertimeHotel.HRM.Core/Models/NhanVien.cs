namespace OvertimeHotel.HRM.Core.Models;

public class NhanVien
{
    public int MaNhanVien { get; set; }
    public bool IsProfileComplete { get; set; } = true;
    public int? MaPhongBan { get; set; }
    public int? MaChucVu { get; set; }
    public string Ho { get; set; } = string.Empty;
    public string Ten { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string DienThoai { get; set; } = string.Empty;

    /// <summary>
    /// Email công vụ — đây là ĐỊNH DANH ĐĂNG NHẬP của Mobile App, không phải
    /// email liên hệ. <c>custom_access_token_hook</c> đối chiếu
    /// <c>claims.email</c> với cột này (không phân biệt hoa thường); không khớp
    /// thì hook không cấp claim vai trò và app fail-closed. Vì vậy sửa giá trị ở
    /// đây là làm hỏng đăng nhập của chính nhân viên đó.
    ///
    /// <para>
    /// Cột trong CSDL vẫn tên là <c>email</c> — đổi tên thành <c>email_cong_vu</c>
    /// sẽ kéo theo phải sửa cả hai bản hook đang chạy trên Cloud, mà hook là
    /// điểm cấp quyền duy nhất của app.
    /// </para>
    /// </summary>
    public string EmailCongVu { get; set; } = string.Empty;

    /// <summary>
    /// Email cá nhân — chỉ để lưu hồ sơ, KHÔNG dùng để đăng nhập ở bất kỳ đâu.
    /// Có thể để trống (nullable trong CSDL).
    /// </summary>
    public string? EmailCaNhan { get; set; }

    public string? DiaChi { get; set; }
    public string TrinhDo { get; set; } = string.Empty;
    public DateOnly NgayVaoLam { get; set; }
    public DateOnly? NgayThoiViec { get; set; }
    public string TrangThai { get; set; } = "DANG_LAM";

    /// <summary>
    /// Số người phụ thuộc đăng ký giảm trừ gia cảnh thuế TNCN.
    /// Mặc định là 0 (chưa có người phụ thuộc).
    /// </summary>
    public int SoNguoiPhuThuoc { get; set; } = 0;

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

    // Hai bản ghi YEU_CAU_TAI_KHOAN cùng trỏ về NhanVien nhưng khác vai trò, nên
    // phải đặt tên phân biệt — gộp chung một ICollection sẽ mất thông tin về
    // việc ai là người gửi, ai là người được cấp tài khoản.
    // (Bản ghi thứ ba — ma_nguoi_xu_ly — trỏ TAI_KHOAN chứ không phải NHAN_VIEN,
    // nên nó nằm trên TaiKhoan chứ không ở đây.)
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoanCuaNhanVien { get; set; } = new List<YeuCauTaiKhoan>();
    public virtual ICollection<YeuCauTaiKhoan> YeuCauTaiKhoanDaGui { get; set; } = new List<YeuCauTaiKhoan>();

    // Đổi ca (PRD 3.4): một người có thể vừa là bên nhường ca, vừa là bên nhận
    // ca, vừa là quản lý duyệt — ba vai trò khác nhau nên phải tách ba tập.
    public virtual ICollection<YeuCauDoiCa> YeuCauDoiCaDaGui { get; set; } = new List<YeuCauDoiCa>();
    public virtual ICollection<YeuCauDoiCa> YeuCauDoiCaDaNhan { get; set; } = new List<YeuCauDoiCa>();
    public virtual ICollection<YeuCauDoiCa> YeuCauDoiCaDaDuyet { get; set; } = new List<YeuCauDoiCa>();

    // Giải trình chấm công (PRD 4.5): người nộp, quản lý xác minh, HR xác nhận.
    public virtual ICollection<GiaiTrinhChamCong> GiaiTrinhChamCongDaGui { get; set; } = new List<GiaiTrinhChamCong>();
    public virtual ICollection<GiaiTrinhChamCong> GiaiTrinhQuanLyXacMinh { get; set; } = new List<GiaiTrinhChamCong>();
    public virtual ICollection<GiaiTrinhChamCong> GiaiTrinhHrXacNhan { get; set; } = new List<GiaiTrinhChamCong>();
}
