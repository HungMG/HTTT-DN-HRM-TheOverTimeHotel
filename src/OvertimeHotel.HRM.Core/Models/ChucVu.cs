using OvertimeHotel.HRM.Core.Constants;

namespace OvertimeHotel.HRM.Core.Models;

public class ChucVu
{
    public int MaChucVu { get; set; }
    public int? MaPhongBan { get; set; }
    public string TenChucVu { get; set; } = string.Empty;
    public string? MoTa { get; set; }

    /// <summary>
    /// Chế độ làm việc gắn với chức danh: <c>XOAY_CA</c> (mặc định — trực ca 24/7)
    /// hoặc <c>HANH_CHINH</c> (khối văn phòng: Tổng Giám Đốc, HR, CNTT, Kế toán).
    ///
    /// <para>
    /// Nằm ở chức danh chứ không ở ca vì "làm giờ hành chính" là điều khoản của
    /// hợp đồng lao động, không phải thuộc tính của khung giờ. Cùng khung
    /// 08:00–16:00 có thể là ca xoay của lễ tân hoặc giờ hành chính của kế toán.
    /// </para>
    ///
    /// <para>
    /// Đối chiếu với <see cref="CaLamViec.LoaiCa"/> trong
    /// <c>ShiftSchedulingPolicy</c>: lệch chế độ thì chặn cứng khi phân ca.
    /// </para>
    /// </summary>
    public string CheDoLamViec { get; set; } = ShiftWorkModes.XOAY_CA;

    public virtual PhongBan? PhongBan { get; set; }
    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();

    /// <summary>
    /// Các dòng định biên tối thiểu theo ca áp cho chức vụ này (PRD 3.5). Một chức
    /// danh trải nhiều bộ phận (Trưởng Bộ Phận, Giám Sát Ca) có nhiều dòng ở đây —
    /// mỗi bộ phận một dòng, cộng thêm dòng <c>MaPhongBan = null</c> nếu có mức
    /// dùng chung.
    /// </summary>
    public virtual ICollection<DinhBienCa> DinhBienCas { get; set; } = new List<DinhBienCa>();

    /// <summary>Định biên riêng theo ngày cho chức vụ này.</summary>
    public virtual ICollection<DinhBienNgay> DinhBienNgays { get; set; } = new List<DinhBienNgay>();
}
