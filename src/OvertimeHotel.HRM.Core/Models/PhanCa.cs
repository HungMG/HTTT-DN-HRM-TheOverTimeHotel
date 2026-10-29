namespace OvertimeHotel.HRM.Core.Models;

public class PhanCa
{
    public int MaPhanCa { get; set; }
    public int MaNhanVien { get; set; }
    public int MaCa { get; set; }
    public DateOnly NgayLamViec { get; set; }
    public DateTimeOffset BatDauDuKien { get; set; }
    public DateTimeOffset KetThucDuKien { get; set; }
    public string TrangThai { get; set; } = "DA_PHAN";
    public bool DaCongBo { get; set; } = true;

    /// <summary>
    /// Số giờ làm thêm (OT) dự kiến được Quản lý phân công kéo dài sau ca (PRD 4.3).
    /// Tối đa 4.0 giờ/ngày theo Điều 107 Bộ luật Lao động 2019.
    /// </summary>
    public decimal GioTangCaDuKien { get; set; } = 0.00m;

    /// <summary>
    /// Lý do / công việc làm thêm tăng ca (ví dụ: Setup tiệc cưới, đón đoàn MICE).
    /// </summary>
    public string? GhiChuOt { get; set; }

    /// <summary>
    /// Ca có kế hoạch tăng ca hay không.
    /// </summary>
    public bool CoTangCa => GioTangCaDuKien > 0;

    /// <summary>
    /// Mốc thời gian kết thúc thực tế dự kiến (bao gồm cả giờ làm thêm OT).
    /// Dùng để tính toán khoảng nghỉ với ca tiếp theo trong ShiftSchedulingPolicy.
    /// </summary>
    public DateTimeOffset KetThucDuKienKemOt => KetThucDuKien.AddHours((double)GioTangCaDuKien);

    public virtual NhanVien? NhanVien { get; set; }
    public virtual CaLamViec? CaLamViec { get; set; }
    public virtual ChamCong? ChamCong { get; set; }

    /// <summary>Các yêu cầu đổi ca mà chính ca này là ca được nhường đi.</summary>
    public virtual ICollection<YeuCauDoiCa> YeuCauDoiCaGui { get; set; } = new List<YeuCauDoiCa>();

    /// <summary>Các yêu cầu đổi ca mà chính ca này là ca người khác nhận về.</summary>
    public virtual ICollection<YeuCauDoiCa> YeuCauDoiCaNhan { get; set; } = new List<YeuCauDoiCa>();
}
