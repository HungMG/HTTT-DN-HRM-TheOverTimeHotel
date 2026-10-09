namespace OvertimeHotel.HRM.Core.Models;

/// <summary>
/// Yêu cầu cấp tài khoản đăng nhập — bảng TRUNG GIAN của quy trình
/// "Nhân sự duyệt hợp đồng lao động → Công nghệ Thông tin (IT) cấp tài khoản".
///
/// <para>
/// Trước khi có bảng này, hệ thống không có chỗ nào ghi lại tiến trình đó.
/// <c>HopDong.TrangThai</c> và <c>TaiKhoan.TrangThai</c> đều là trạng thái
/// CUỐI CÙNG: hợp đồng "HIEU_LUC" rồi thì coi như xong, tài khoản tồn tại thì
/// coi như đã cấp. Không có bảng nào trả lời được nhân viên nào đã có hợp
/// đồng mà vẫn chưa đăng nhập được, ai đã duyệt, IT đã nhận việc chưa — và
/// đó chính là các câu hỏi đầu tiên mà mọi sự cố đều dẫn tới.
/// </para>
///
/// <para>
/// Bốn trạng thái (so chuỗi, không dùng enum, đồng nhất với CHECK ở CSDL):
/// <list type="bullet">
///   <item><c>CHO_IT</c> — HR đã duyệt hợp đồng và gửi yêu cầu, đang chờ IT.</item>
///   <item><c>DANG_XU_LY</c> — IT đã nhận việc.</item>
///   <item><c>DA_CAP</c> — đã tạo được tài khoản + user trong <c>auth.users</c>.</item>
///   <item><c>TU_CHOI</c> — IT từ chối, bắt buộc có <see cref="GhiChuTuChoi"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// Bảng này KHÔNG thay thế <see cref="TaiKhoan"/>: khi yêu cầu ở trạng thái
/// <c>DA_CAP</c> thì dòng <c>tai_khoan</c> tương ứng cũng đã có, và
/// <see cref="MaTaiKhoan"/> trỏ sang nó. Ba CHECK ở CSDL giữ cho hai bảng không
/// bao giờ lệch nhau: từ chối mà không có ghi chú, hoặc khai "đã cấp" khi
/// <see cref="MaTaiKhoan"/> còn NULL.
/// </para>
/// </summary>
public class YeuCauTaiKhoan
{
    public int MaYeuCau { get; set; }

    /// <summary>Nhân viên được cấp tài khoản — khóa chính của yêu cầu.</summary>
    public int MaNhanVien { get; set; }

    /// <summary>
    /// Hợp đồng làm căn cứ gửi yêu cầu. Có thể NULL (nhân viên không cần hợp
    /// đồng vẫn cần đăng nhập). Trong CSDL khai báo ON DELETE SET NULL để xoá
    /// hợp đồng không xoá luôn lịch sử IT đã xử lý.
    /// </summary>
    public int? MaHopDong { get; set; }

    /// <summary>
    /// Tên đăng nhập dự kiến cấp. Chỉ có ràng buộc DUY NHẤT khi yêu cầu còn mở
    /// (<c>CHO_IT</c>/<c>DANG_XU_LY</c>) — yêu cầu đã kết thúc rồi gửi lại vẫn
    /// phải dùng được đúng tên này, nếu khoá cứng thì nhân viên không bao giờ
    /// sửa nổi cái lỗi khiến mình bị từ chối.
    /// </summary>
    public string TenDangNhap { get; set; } = string.Empty;

    /// <summary>Vai trò RBAC sẽ gán cho tài khoản khi yêu cầu được cấp.</summary>
    public int MaVaiTro { get; set; }

    /// <summary>
    /// Một trong bốn hằng số: CHO_IT / DANG_XU_LY / DA_CAP / TU_CHOI.
    /// Để mặc định "CHO_IT" khớp với DEFAULT của CSDL: tạo object trong C#
    /// mà quên gán trạng thái vẫn cho đúng nghĩa "đang chờ IT".
    /// </summary>
    public string TrangThai { get; set; } = "CHO_IT";

    public DateTimeOffset NgayGui { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Nhân viên HR đã duyệt hợp đồng và gửi yêu cầu này.</summary>
    public int MaNguoiGui { get; set; }

    /// <summary>
    /// Tài khoản IT đã xử lý yêu cầu — trỏ <c>tai_khoan</c> chứ không phải
    /// <c>nhan_vien</c>, vì đây là tài khoản hệ thống chứ không phải hồ sơ lao
    /// động. NULL khi còn <c>CHO_IT</c> hoặc khi tài khoản IT đã bị xoá
    /// (ON DELETE SET NULL): việc khóa tài khoản IT không được làm mất lịch
    /// sử xử lý.
    /// </summary>
    public int? MaNguoiXuLy { get; set; }

    /// <summary>Thời điểm IT kết thúc xử lý (chấp nhận hoặc từ chối).</summary>
    public DateTimeOffset? NgayXuLy { get; set; }

    /// <summary>
    /// Lý do từ chối. Bắt buộc có khi trạng thái là <c>TU_CHOI</c> — từ chối
    /// mà không giải thích thì người lao động không biết cần sửa gì để gửi lại.
    /// </summary>
    public string? GhiChuTuChoi { get; set; }

    /// <summary>
    /// Tài khoản được tạo ra cho nhân viên. Chỉ có giá trị khi
    /// <see cref="TrangThai"/> là <c>DA_CAP</c> — CSDL chặn ngược lại bằng
    /// CHECK nên không thể khai "đã cấp" mà để trống.
    /// </summary>
    public int? MaTaiKhoan { get; set; }

    public virtual NhanVien? NhanVien { get; set; }
    public virtual HopDong? HopDong { get; set; }
    public virtual VaiTro? VaiTro { get; set; }
    public virtual NhanVien? NguoiGui { get; set; }
    public virtual TaiKhoan? NguoiXuLy { get; set; }
    public virtual TaiKhoan? TaiKhoanDuocCap { get; set; }
}