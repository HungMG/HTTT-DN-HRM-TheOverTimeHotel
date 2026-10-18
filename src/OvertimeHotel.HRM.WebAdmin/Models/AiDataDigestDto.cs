using System.Text.Json.Serialization;

namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// Bản rút gọn của số liệu báo cáo, dành riêng cho Trợ lý AI đọc.
///
/// <para>
/// <b>Vì sao không trả thẳng <see cref="BaoCaoViewModel"/>:</b> view model mang theo cả
/// chuỗi dữ liệu biểu đồ, danh mục phòng ban và cờ giao diện — thừa với một mô hình ngôn ngữ,
/// và làm phình ngữ cảnh. Bản rút gọn chỉ giữ những con số thật sự có nghĩa để diễn giải.
/// </para>
///
/// <para>
/// <b>Nguyên tắc "vắng mặt, không phải số 0":</b> mục nào người hỏi không có quyền xem thì
/// <b>không xuất hiện</b> trong JSON, chứ không trả về một object toàn số 0. Trả số 0 sẽ khiến
/// mô hình đọc thành "thực chi 0 đồng" — một câu trả lời sai nhưng nghe rất hợp lý.
/// Vì vậy các thuộc tính ở đây đều khai báo nullable và được gán <c>null</c> khi bị bỏ,
/// kèm <see cref="JsonIgnoreCondition.WhenWritingNull"/> để chúng biến mất hẳn khỏi payload.
/// </para>
/// </summary>
public sealed class AiDataDigestDto
{
    /// <summary>Nhãn phạm vi dữ liệu, ví dụ "Toàn khách sạn" hoặc "Bộ phận: Lễ tân".</summary>
    public string ScopeLabel { get; set; } = string.Empty;

    /// <summary>Nhãn kỳ báo cáo, ví dụ "Tháng 10/2026".</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>Nhãn vai trò người đang hỏi, để mô hình giới hạn lời khuyên cho đúng.</summary>
    public string RoleLabel { get; set; } = string.Empty;

    /// <summary>Thời điểm dựng số liệu, để mô hình biết số liệu cũ hay mới.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>10.1 — Biến động nhân sự.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataBienDongDto? BienDong { get; set; }

    /// <summary>10.2 — Cơ cấu nhân lực.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataCoCauDto? CoCau { get; set; }

    /// <summary>10.3 — Học vấn &amp; thâm niên.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataHocVanDto? HocVan { get; set; }

    /// <summary>10.4 — Đối soát quỹ lương. Chỉ có với Admin/HR.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataQuyLuongDto? QuyLuong { get; set; }

    /// <summary>10.5 — Chuyên cần &amp; vi phạm.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataChuyenCanDto? ChuyenCan { get; set; }

    /// <summary>10.6 — Hiệu quả nhân sự sự kiện.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataSuKienDto? SuKien { get; set; }

    /// <summary>Phiếu lương của chính người đang hỏi; chỉ có khi hỏi đúng phạm vi cá nhân.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataCaNhanDto? CaNhan { get; set; }

    /// <summary>
    /// Ghi chú phương pháp cần mô hình nhắc lại khi diễn giải, để người dùng không hiểu nhầm
    /// các con số suy diễn thành số liệu chính thức.
    /// </summary>
    public IReadOnlyList<string> GhiChu { get; set; } = [];
}

/// <summary>10.1 — Số liệu biến động nhân sự đã rút gọn.</summary>
public sealed class AiDataBienDongDto
{
    /// <summary>Quân số đầu kỳ.</summary>
    public int DauKy { get; set; }

    /// <summary>Quân số cuối kỳ.</summary>
    public int CuoiKy { get; set; }

    /// <summary>Số tuyển mới trong kỳ.</summary>
    public int TuyenMoi { get; set; }

    /// <summary>Số thôi việc trong kỳ.</summary>
    public int ThoiViec { get; set; }

    /// <summary>Biến động ròng (tuyển mới trừ thôi việc).</summary>
    public int BienDongRong { get; set; }

    /// <summary>Tỷ lệ thôi việc tính trên quân số bình quân, theo phần trăm.</summary>
    public decimal TyLeThoiViec { get; set; }

    /// <summary>Quân số, tuyển mới và thôi việc của từng phòng ban.</summary>
    public IReadOnlyList<AiDataPhongBanDto> TheoPhongBan { get; set; } = [];
}

/// <summary>Số liệu biến động của một phòng ban.</summary>
public sealed class AiDataPhongBanDto
{
    /// <summary>Tên phòng ban.</summary>
    public string PhongBan { get; set; } = string.Empty;

    /// <summary>Quân số hiện tại.</summary>
    public int QuanSo { get; set; }

    /// <summary>Số tuyển mới trong kỳ.</summary>
    public int TuyenMoi { get; set; }

    /// <summary>Số thôi việc trong kỳ.</summary>
    public int ThoiViec { get; set; }
}

/// <summary>10.2 — Số liệu cơ cấu nhân lực đã rút gọn.</summary>
public sealed class AiDataCoCauDto
{
    /// <summary>Tổng quân số đang xét.</summary>
    public int TongNhanSu { get; set; }

    /// <summary>Số đang làm việc.</summary>
    public int DangLam { get; set; }

    /// <summary>Số đang nghỉ phép.</summary>
    public int NghiPhep { get; set; }

    /// <summary>Số đã thôi việc.</summary>
    public int DaThoiViec { get; set; }

    /// <summary>Phân bổ quân số theo phòng ban.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoPhongBan { get; set; } = [];

    /// <summary>Phân bổ quân số theo giới tính.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoGioiTinh { get; set; } = [];

    /// <summary>Các chức vụ đông nhân sự nhất.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoChucVu { get; set; } = [];
}

/// <summary>Một lát cắt phân bổ: nhãn, số lượng, tỷ lệ phần trăm.</summary>
public sealed class AiDataNhanDto
{
    /// <summary>Nhãn của lát cắt.</summary>
    public string Nhan { get; set; } = string.Empty;

    /// <summary>Giá trị tuyệt đối.</summary>
    public int GiaTri { get; set; }

    /// <summary>Tỷ lệ trên tổng, theo phần trăm.</summary>
    public decimal TyLe { get; set; }
}

/// <summary>10.3 — Số liệu học vấn &amp; thâm niên đã rút gọn.</summary>
public sealed class AiDataHocVanDto
{
    /// <summary>Tỷ lệ nhân sự có trình độ từ đại học trở lên, theo phần trăm.</summary>
    public decimal TyLeDaiHocTroLen { get; set; }

    /// <summary>Thâm niên trung bình, tính theo năm.</summary>
    public decimal ThamNienTrungBinh { get; set; }

    /// <summary>Phân bổ trình độ đào tạo.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoTrinhDo { get; set; } = [];

    /// <summary>Phân bổ thâm niên theo nhóm.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoThamNien { get; set; } = [];
}

/// <summary>10.4 — Số liệu đối soát quỹ lương đã rút gọn.</summary>
public sealed class AiDataQuyLuongDto
{
    /// <summary>Tổng thực chi theo nghĩa thực nhận (đã trừ khấu trừ), đơn vị đồng.</summary>
    public decimal ThucChiThucNhan { get; set; }

    /// <summary>Tổng thực chi gộp (lương công + tăng ca + phụ cấp + thưởng), đơn vị đồng.</summary>
    public decimal ThucChiGop { get; set; }

    /// <summary>Định mức tham chiếu suy ra từ hợp đồng đang hiệu lực, đơn vị đồng.</summary>
    public decimal DinhMucThamChieu { get; set; }

    /// <summary>Chênh lệch giữa thực chi gộp và định mức tham chiếu, đơn vị đồng.</summary>
    public decimal ChenhLech { get; set; }

    /// <summary>Tỷ lệ giải ngân so với định mức, theo phần trăm.</summary>
    public decimal TyLeGiaiNgan { get; set; }

    /// <summary>True khi tỷ lệ giải ngân vượt ngưỡng cảnh báo.</summary>
    public bool VuotNguong { get; set; }

    /// <summary>Số tháng của kỳ báo cáo, dùng để quy đổi định mức.</summary>
    public int SoThangTrongKy { get; set; }

    /// <summary>Chi phí lương của từng phòng ban.</summary>
    public IReadOnlyList<AiDataQuyLuongPhongBanDto> TheoPhongBan { get; set; } = [];
}

/// <summary>Chi phí lương của một phòng ban.</summary>
public sealed class AiDataQuyLuongPhongBanDto
{
    /// <summary>Tên phòng ban.</summary>
    public string PhongBan { get; set; } = string.Empty;

    /// <summary>Thực chi của phòng ban, đơn vị đồng.</summary>
    public decimal ThucChi { get; set; }

    /// <summary>Định mức tham chiếu của phòng ban, đơn vị đồng.</summary>
    public decimal DinhMuc { get; set; }
}

/// <summary>10.5 — Số liệu chuyên cần &amp; vi phạm đã rút gọn.</summary>
public sealed class AiDataChuyenCanDto
{
    /// <summary>Tổng số lượt chấm công trong kỳ.</summary>
    public int TongLuot { get; set; }

    /// <summary>Số lượt đúng giờ.</summary>
    public int DungGio { get; set; }

    /// <summary>Số lượt đi trễ.</summary>
    public int DiTre { get; set; }

    /// <summary>Số lượt về sớm.</summary>
    public int VeSom { get; set; }

    /// <summary>Số lượt vắng mặt.</summary>
    public int Vang { get; set; }

    /// <summary>Tỷ lệ chuyên cần trên tổng lượt, theo phần trăm.</summary>
    public decimal TyLeChuyenCan { get; set; }

    /// <summary>Tổng số phút đi trễ trong kỳ.</summary>
    public int TongPhutTre { get; set; }

    /// <summary>Tổng số phút về sớm trong kỳ.</summary>
    public int TongPhutVeSom { get; set; }

    /// <summary>Tổng giờ tăng ca OT trong kỳ.</summary>
    public decimal TongGioOt { get; set; }

    /// <summary>Tỷ lệ chuyên cần của từng phòng ban.</summary>
    public IReadOnlyList<AiDataNhanDto> TheoPhongBan { get; set; } = [];
}

/// <summary>10.6 — Số liệu hiệu quả nhân sự sự kiện đã rút gọn.</summary>
public sealed class AiDataSuKienDto
{
    /// <summary>Tổng số sự kiện trong kỳ.</summary>
    public int TongSuKien { get; set; }

    /// <summary>Số sự kiện đang diễn ra.</summary>
    public int DangDienRa { get; set; }

    /// <summary>Số sự kiện đã hoàn thành.</summary>
    public int DaHoanThanh { get; set; }

    /// <summary>Tổng số lượt phân công nhân sự.</summary>
    public int TongLuotPhanCong { get; set; }

    /// <summary>Tổng ngân sách phụ cấp dự kiến, đơn vị đồng.</summary>
    public decimal TongNganSach { get; set; }

    /// <summary>Tổng phụ cấp thực tế đã phân bổ, đơn vị đồng.</summary>
    public decimal TongPhuCapDaPhanBo { get; set; }

    /// <summary>Tỷ lệ phân công được đánh giá xuất sắc, theo phần trăm.</summary>
    public decimal TyLeXuatSac { get; set; }

    /// <summary>Danh sách sự kiện trong kỳ.</summary>
    public IReadOnlyList<AiDataSuKienItemDto> DanhSach { get; set; } = [];
}

/// <summary>Một sự kiện trong danh sách rút gọn.</summary>
public sealed class AiDataSuKienItemDto
{
    /// <summary>Tên sự kiện.</summary>
    public string Ten { get; set; } = string.Empty;

    /// <summary>Nhãn loại sự kiện.</summary>
    public string Loai { get; set; } = string.Empty;

    /// <summary>Nhãn trạng thái sự kiện.</summary>
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Số nhân sự được điều động.</summary>
    public int SoNhanSu { get; set; }

    /// <summary>Ngân sách phụ cấp dự kiến, đơn vị đồng.</summary>
    public decimal NganSach { get; set; }

    /// <summary>Tỷ lệ sử dụng ngân sách, theo phần trăm.</summary>
    public decimal TyLeSuDungNganSach { get; set; }
}

/// <summary>Phiếu lương và chấm công của chính người đang hỏi.</summary>
public sealed class AiDataCaNhanDto
{
    /// <summary>Số ngày có mặt thực tế trong kỳ.</summary>
    public int NgayCongThucTe { get; set; }

    /// <summary>Số ngày nghỉ phép có hưởng lương trong kỳ.</summary>
    public int NgayNghiPhep { get; set; }

    /// <summary>Phiếu lương của các kỳ gần nhất, mới nhất đứng đầu.</summary>
    public IReadOnlyList<AiDataPhieuLuongDto> PhieuLuong { get; set; } = [];
}

/// <summary>Một phiếu lương đã rút gọn.</summary>
public sealed class AiDataPhieuLuongDto
{
    /// <summary>Kỳ lương dạng "MM/yyyy".</summary>
    public string KyLuong { get; set; } = string.Empty;

    /// <summary>Lương theo ngày công thực tế, đơn vị đồng.</summary>
    public decimal TienLuongCong { get; set; }

    /// <summary>Tiền tăng ca, đơn vị đồng.</summary>
    public decimal TienTangCa { get; set; }

    /// <summary>Tổng phụ cấp, đơn vị đồng.</summary>
    public decimal TongPhuCap { get; set; }

    /// <summary>Tổng thưởng, đơn vị đồng.</summary>
    public decimal TongThuong { get; set; }

    /// <summary>Tổng khấu trừ (BHXH, thuế...), đơn vị đồng.</summary>
    public decimal TongKhauTru { get; set; }

    /// <summary>Số tiền thực nhận, đơn vị đồng.</summary>
    public decimal ThucNhan { get; set; }

    /// <summary>Trạng thái phiếu lương trong quy trình duyệt.</summary>
    public string TrangThai { get; set; } = string.Empty;
}
