using System.ComponentModel.DataAnnotations.Schema;
using OvertimeHotel.HRM.Core.Constants;

namespace OvertimeHotel.HRM.Core.Models;

/// <summary>
/// Một khung ca làm việc của khách sạn (PRD 3.2).
///
/// <para>
/// <b>Hai khái niệm khác nhau, đừng đồng nhất:</b> <see cref="GioBatDau"/>–<see
/// cref="GioKetThuc"/> là khoảng ca <i>chiếm chỗ trên đồng hồ</i>;
/// <see cref="GioLamChuan"/> là số giờ <i>thực sự làm việc</i>. Với ba ca xoay
/// 24/7 hai con số bằng nhau (06:00–14:00 là 8 giờ làm). Với ca hành chính
/// 07:00–17:00 chúng khác nhau: chiếm 10 tiếng nhưng chỉ làm 8, phần chênh là
/// giờ nghỉ trưa.
/// </para>
///
/// <para>
/// Trước khi có <see cref="GioLamChuan"/>, mọi nơi tính giờ công đều lấy thẳng
/// hiệu số giờ quẹt thẻ vào/ra — thêm ca 07:00–17:00 là báo cáo tự động thổi
/// phồng 25% mà không ai thấy. Cột này là nơi duy nhất trả lời "ca này làm mấy
/// tiếng", nên mọi phép tính công và phụ cấp phải đọc nó thay vì hard-code 8.
/// </para>
/// </summary>
public class CaLamViec
{
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public TimeOnly GioBatDau { get; set; }
    public TimeOnly GioKetThuc { get; set; }
    public bool QuaDem { get; set; } = false;

    /// <summary>
    /// Số giờ làm thực tế của ca, do người dùng khai trên form ca làm việc.
    /// Mặc định 8.00 vì mọi ca xoay hiện có đều tròn 8 tiếng — dữ liệu cũ không
    /// cần backfill. Ca hành chính 07:00–17:00 khai 8.00 để giờ nghỉ trưa không
    /// bị tính thành giờ công.
    /// </summary>
    public decimal GioLamChuan { get; set; } = 8.00m;

    /// <summary>
    /// Loại ca: <c>XOAY_CA</c> (trực 24/7, xếp được cho mọi chức danh) hoặc
    /// <c>HANH_CHINH</c> (khung giờ văn phòng, chỉ xếp cho nhân sự có cùng chế
    /// độ làm việc). Đối chiếu với <see cref="ChucVu.CheDoLamViec"/>.
    /// </summary>
    public string LoaiCa { get; set; } = ShiftWorkModes.XOAY_CA;

    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();

    /// <summary>Định biên tối thiểu theo chức vụ cho ca này (PRD 3.5).</summary>
    public virtual ICollection<DinhBienCa> DinhBienCas { get; set; } = new List<DinhBienCa>();

    /// <summary>Định biên riêng theo từng ngày cho ca này (đè lên mức nền ở trên).</summary>
    public virtual ICollection<DinhBienNgay> DinhBienNgays { get; set; } = new List<DinhBienNgay>();

    /// <summary>
    /// Độ dài khung giờ trên đồng hồ, đã xử lý ca qua đêm (<c>22:00–06:00</c> ra
    /// 8 giờ chứ không phải −16).
    ///
    /// <para>
    /// Dùng để đối chiếu khi người dùng khai <see cref="GioLamChuan"/> — khai
    /// nhiều hơn khung giờ nghĩa là nghỉ âm, phải chặn. Còn khi tính tiền thì
    /// đọc <see cref="GioLamChuan"/>, không đọc thuộc tính này.
    /// </para>
    /// </summary>
    [NotMapped]
    public decimal DoDaiKhungGio
    {
        get
        {
            var phut = (decimal)(GioKetThuc - GioBatDau).TotalMinutes;
            if (phut < 0) phut += 24 * 60;
            return Math.Round(phut / 60m, 2);
        }
    }

    /// <summary>
    /// Sắc thái của ca để giao diện chọn màu/icon: <c>office</c>, <c>night</c>,
    /// <c>afternoon</c> hoặc <c>morning</c>.
    ///
    /// <para>
    /// Một nguồn duy nhất cho cả Razor lẫn JavaScript. Trước đây mỗi nơi tự dò
    /// chuỗi tên ca nên ba chỗ lệch nhau, và ca hành chính (tên không chứa
    /// "Sáng"/"Chiều"/"Đêm") bị tô màu ca đêm ở nơi này nhưng màu ca sáng ở nơi
    /// khác. <see cref="LoaiCa"/> quyết định trước — đó là dữ liệu, không phải
    /// suy đoán từ tên người dùng có thể đổi tuỳ ý.
    /// </para>
    /// </summary>
    [NotMapped]
    public string SacThaiCa
    {
        get
        {
            if (LoaiCa == ShiftWorkModes.HANH_CHINH) return "office";
            if (QuaDem || TenCa.Contains("Đêm", StringComparison.OrdinalIgnoreCase)) return "night";
            if (TenCa.Contains("Chiều", StringComparison.OrdinalIgnoreCase)) return "afternoon";
            return "morning";
        }
    }
}
