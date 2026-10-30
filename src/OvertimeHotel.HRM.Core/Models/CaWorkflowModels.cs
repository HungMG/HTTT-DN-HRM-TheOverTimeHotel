namespace OvertimeHotel.HRM.Core.Models;

/// <summary>
/// Yêu cầu đổi ca giữa hai nhân viên (PRD 3.4).
///
/// <para>
/// Nhân viên không tự sửa được lịch của mình, nên khi bận đột xuất họ phải nhờ người
/// khác trực thay. Bản ghi này giữ lại cả hai đầu của giao dịch: ca nào đưa đi, ai
/// nhận, và quản lý đã duyệt hay chưa. Không có nó thì sau khi đổi xong lịch, không
/// ai trả lời được vì sao ca của A lại nằm tên B.
/// </para>
///
/// <para>
/// Trạng thái dùng mã chuỗi để code C# và SQL đọc được trực tiếp, không phải tra bảng số:
/// <c>CHO_DUYET</c> → <c>DA_DUYET</c> (lịch đã đổi) hoặc <c>TU_CHOI</c>, và
/// <c>DA_HUY</c> khi người gửi rút lại trước lúc duyệt.
/// </para>
/// </summary>
public class YeuCauDoiCa
{
    public int MaYeuCau { get; set; }

    /// <summary>Ca mà người gửi muốn nhường lại.</summary>
    public int MaPhanCaGui { get; set; }

    /// <summary>Ca mà người nhận sẽ trực thay.</summary>
    public int MaPhanCaNhan { get; set; }

    public int MaNhanVienGui { get; set; }
    public int MaNhanVienNhan { get; set; }

    public string LyDo { get; set; } = string.Empty;

    public string TrangThai { get; set; } = "CHO_NGUOI_NHAN";

    public string? BenTuChoi { get; set; }
    public string? PhanHoiNguoiNhan { get; set; }
    public DateTimeOffset? NgayNguoiNhanPhanHoi { get; set; }

    public int? MaNguoiDuyet { get; set; }
    public DateTimeOffset NgayGui { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? PhanHoiDuyet { get; set; }

    public virtual PhanCa? PhanCaGui { get; set; }
    public virtual PhanCa? PhanCaNhan { get; set; }
    public virtual NhanVien? NhanVienGui { get; set; }
    public virtual NhanVien? NhanVienNhan { get; set; }
    public virtual NhanVien? NguoiDuyet { get; set; }
}

/// <summary>
/// Định biên tối thiểu của một ca theo chức vụ (PRD 3.5).
///
/// <para>
/// Khách sạn 5 sao không thể để lễ tân một mình trực ca đêm hay phòng Buồng phòng
/// thiếu người vào giờ cao điểm. Bảng này ghi con số tối thiểu cho từng cặp
/// (ca, chức vụ); màn lịch phân ca quét lịch hằng ngày và bật cảnh báo đỏ khi số
/// người thực tế thấp hơn.
/// </para>
///
/// <para>
/// Để trong bảng chứ không hard-code vì định biên đổi theo mùa: cao điểm cần thêm
/// lễ tân, thấp điểm có thể giảm. Sửa số trong CSDL không cần build lại.
/// </para>
///
/// <para>
/// <see cref="MaPhongBan"/> là chiều thứ ba của khoá nghiệp vụ. Lý do: chức danh
/// giám sát ("Trưởng Bộ Phận", "Giám Sát Ca") mang <c>ma_phong_ban = NULL</c> ở
/// bảng <c>chuc_vu</c> — cùng một chức danh nhưng trải trên nhiều bộ phận. Thiếu
/// chiều này thì không đặt được "Bếp cần 1 Giám Sát Ca ca đêm", mà chỉ đặt được
/// một con số gộp cho toàn khách sạn.
/// </para>
///
/// <para>
/// <c>NULL</c> nghĩa là "dùng chung mọi bộ phận" — giữ nguyên hiệu lực của các
/// dòng cũ (lễ tân, an ninh, buồng phòng) mà không cần backfill. Chỉ chức danh
/// trải nhiều bộ phận mới cần tách thành nhiều dòng có <see cref="MaPhongBan"/>
/// cụ thể.
/// </para>
/// </summary>
public class DinhBienCa
{
    public int MaDinhBien { get; set; }
    public int MaCa { get; set; }
    public int MaChucVu { get; set; }

    /// <summary>Bộ phận áp dụng; <c>null</c> là dùng chung cho mọi bộ phận.</summary>
    public int? MaPhongBan { get; set; }

    public int SoNguoiToiThieu { get; set; }

    public virtual CaLamViec? CaLamViec { get; set; }
    public virtual ChucVu? ChucVu { get; set; }
    public virtual PhongBan? PhongBan { get; set; }
}

/// <summary>
/// Định biên riêng cho MỘT ngày cụ thể (PRD 3.5, mở rộng theo hướng A).
///
/// <para>
/// <see cref="DinhBienCa"/> là mức nền áp cho mọi ngày. Nhưng khách sạn có ngày
/// cần khác: cuối tuần đông khách cần thêm buồng phòng, ngày thấp điểm có thể
/// giảm lễ tân. Bảng này chỉ ghi những ô LỆCH so với mức nền — ô không có dòng
/// nào nghĩa là "vẫn theo mức chung", không phải "không cần ai".
/// </para>
///
/// <para>
/// Tách bảng thay vì thêm cột ngày vào <see cref="DinhBienCa"/> vì bảng cũ đang
/// bị ràng buộc <c>UNIQUE (ma_ca, ma_chuc_vu)</c> — chèn cột ngày vào sẽ phải bỏ
/// ràng buộc đó và viết lại toàn bộ truy vấn đang chạy. Tách bảng là thêm thuần,
/// dữ liệu mức nền cũ không đụng tới.
/// </para>
/// </summary>
public class DinhBienNgay
{
    public int MaDinhBienNgay { get; set; }

    /// <summary>Ngày áp dụng riêng.</summary>
    public DateOnly Ngay { get; set; }

    public int MaCa { get; set; }
    public int MaChucVu { get; set; }

    /// <summary>
    /// Bộ phận áp dụng; <c>null</c> là dùng chung cho mọi bộ phận. Cùng ngữ nghĩa
    /// với <see cref="DinhBienCa.MaPhongBan"/> — dòng đè theo ngày phải khớp đúng
    /// bộ phận của dòng mức nền mà nó đè lên, nếu không "Bếp" sẽ đè nhầm "Ẩm thực".
    /// </summary>
    public int? MaPhongBan { get; set; }

    /// <summary>Số người tối thiểu riêng cho ngày này (luôn &gt;= 1).</summary>
    public int SoNguoiToiThieu { get; set; }

    public virtual CaLamViec? CaLamViec { get; set; }
    public virtual ChucVu? ChucVu { get; set; }
    public virtual PhongBan? PhongBan { get; set; }
}

/// <summary>
/// Giải trình chấm công của nhân viên (PRD 4.5).
///
/// <para>
/// Khi nhân viên quên quẹt thẻ hoặc quẹt sai giờ, bản ghi chấm công đã nằm trong
/// CSDL và không sửa trực tiếp được — sửa thẳng sẽ mất dấu vết. Thay vào đó nhân
/// viên nộp giải trình kèm giờ vào/ra đề xuất và bằng chứng; quản lý xác minh rồi
/// HR xác nhận, cuối cùng hệ thống mới ghi đè bản ghi chấm công và để lại nhật ký.
/// </para>
///
/// <para>
/// Hai trạng thái chờ tương ứng hai cấp duyệt: <c>CHO_QUAN_LY</c> rồi <c>CHO_HR</c>.
/// Kết thúc là <c>DA_DUYET</c> hoặc <c>TU_CHOI</c>. Mỗi cấp lưu riêng người xử lý,
/// thời điểm và ý kiến để truy vết được ai đã đồng ý điều chỉnh.
/// </para>
/// </summary>
public class GiaiTrinhChamCong
{
    public int MaGiaiTrinh { get; set; }

    /// <summary>Ca trực bị sai giờ — gắn theo phân ca vì đây là đơn vị nghiệp vụ duy nhất.</summary>
    public int MaPhanCa { get; set; }

    public int MaNhanVien { get; set; }

    /// <summary>Giờ vào thực tế nhân viên đề nghị ghi nhận (null nếu chỉ xin bổ sung giờ ra).</summary>
    public DateTimeOffset? GioDeXuatVao { get; set; }

    /// <summary>Giờ ra thực tế nhân viên đề nghị ghi nhận (null nếu chỉ xin bổ sung giờ vào).</summary>
    public DateTimeOffset? GioDeXuatRa { get; set; }

    public string LyDo { get; set; } = string.Empty;

    /// <summary>Đường dẫn hoặc mô tả bằng chứng — ảnh chụp bảng phân ca, tin nhắn trao đổi…</summary>
    public string? MinhChung { get; set; }

    public string TrangThai { get; set; } = "CHO_QUAN_LY";

    public int? MaQuanLyXacMinh { get; set; }
    public DateTimeOffset? NgayQuanLyXacMinh { get; set; }
    public string? YKienQuanLy { get; set; }

    public int? MaHrXacNhan { get; set; }
    public DateTimeOffset? NgayHrXacNhan { get; set; }
    public string? YKienHr { get; set; }

    public DateTimeOffset NgayGui { get; set; } = DateTimeOffset.UtcNow;

    public virtual PhanCa? PhanCa { get; set; }
    public virtual NhanVien? NhanVien { get; set; }
    public virtual NhanVien? QuanLyXacMinh { get; set; }
    public virtual NhanVien? HrXacNhan { get; set; }
}
