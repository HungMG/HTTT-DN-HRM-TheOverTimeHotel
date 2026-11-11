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
    /// Kết quả tra cứu nhân sự theo từ khóa; chỉ có khi hỏi đúng phạm vi tra cứu và người hỏi
    /// đủ quyền. Không kèm khi từ khóa bị thiếu hoặc không khớp ai.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataDirectoryDto? Directory { get; set; }

    /// <summary>
    /// Kết quả tra cứu hợp đồng lao động theo từ khóa; chỉ có khi hỏi đúng phạm vi tra cứu
    /// hợp đồng và người hỏi đủ quyền.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataContractDto? Contract { get; set; }

    /// <summary>
    /// Kết quả tra cứu đơn từ theo từ khóa; chỉ có khi hỏi đúng phạm vi đơn từ và người hỏi
    /// đủ quyền.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataLeaveDto? Leave { get; set; }

    /// <summary>
    /// Kết quả tra cứu phân ca theo từ khóa; chỉ có khi hỏi đúng phạm vi phân ca và người hỏi
    /// đủ quyền.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataShiftDto? Shift { get; set; }

    /// <summary>
    /// Kết quả tra cứu chấm công theo từ khóa; chỉ có khi hỏi đúng phạm vi chấm công và người hỏi
    /// đủ quyền.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataAttendanceDto? Attendance { get; set; }

    /// <summary>
    /// Kết quả tra cứu sự kiện / dự án theo từ khóa; chỉ có khi hỏi đúng phạm vi sự kiện
    /// và người hỏi đủ quyền.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiDataEventDto? Event { get; set; }

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

/// <summary>
/// Kết quả tra cứu nhân sự theo từ khóa, đã lọc theo phạm vi quyền của người hỏi.
/// </summary>
public sealed class AiDataDirectoryDto
{
    /// <summary>Từ khóa đã tra cứu, để mô hình nhắc lại đúng ý người hỏi.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số nhân viên khớp từ khóa trong phạm vi được phép.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi số kết quả hiển thị đã bị cắt vì vượt giới hạn an toàn ngữ cảnh.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Danh sách nhân viên khớp từ khóa, tối đa 10 người mỗi lượt tra cứu.</summary>
    public IReadOnlyList<AiDataNhanVienDto> DanhSach { get; set; } = [];
}

/// <summary>Một nhân viên trong kết quả tra cứu — chỉ thông tin cơ bản, không dữ liệu cá nhân nhạy cảm.</summary>
public sealed class AiDataNhanVienDto
{
    /// <summary>Mã nhân viên trong hệ thống.</summary>
    public int MaNhanVien { get; set; }

    /// <summary>Họ tên đầy đủ (họ kèm tên).</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban đang công tác; null khi chưa gán phòng ban.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhongBan { get; set; }

    /// <summary>Chức danh hiện tại; null khi chưa gán chức vụ.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChucDanh { get; set; }

    /// <summary>Nhãn tiếng Việt của trạng thái làm việc (Đang làm việc / Đã thôi việc...).</summary>
    public string TrangThai { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả tra cứu hợp đồng lao động theo từ khóa, đã lọc theo phạm vi quyền của người hỏi.
/// </summary>
public sealed class AiDataContractDto
{
    /// <summary>Từ khóa đã tra cứu, để mô hình nhắc lại đúng ý người hỏi.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số hợp đồng khớp từ khóa trong phạm vi được phép.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi số kết quả hiển thị đã bị cắt vì vượt giới hạn an toàn ngữ cảnh.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Danh sách hợp đồng khớp từ khóa.</summary>
    public IReadOnlyList<AiDataHopDongItemDto> DanhSach { get; set; } = [];
}

/// <summary>Một hợp đồng trong kết quả tra cứu — thông tin lao động, không kèm CMND, SĐT, địa chỉ.</summary>
public sealed class AiDataHopDongItemDto
{
    /// <summary>Mã hợp đồng trong hệ thống.</summary>
    public int MaHopDong { get; set; }

    /// <summary>Số hợp đồng.</summary>
    public string SoHopDong { get; set; } = string.Empty;

    /// <summary>Tên nhân viên sở hữu hợp đồng.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban của nhân viên; null khi chưa gán.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhongBan { get; set; }

    /// <summary>Tên chức vụ của nhân viên; null khi chưa gán.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChucDanh { get; set; }

    /// <summary>Tên hiển thị của loại hợp đồng (ví dụ "Xác định thời hạn", "Không xác định thời hạn").</summary>
    public string LoaiHopDong { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu hiệu lực.</summary>
    public DateOnly NgayBatDau { get; set; }

    /// <summary>Ngày kết thúc hiệu lực; null nghĩa là không xác định thời hạn.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? NgayKetThuc { get; set; }

    /// <summary>Lương cơ bản ghi trên hợp đồng.</summary>
    public decimal LuongCoBan { get; set; }

    /// <summary>Lương theo hợp đồng/phụ lục có hiệu lực tại NgayThamChieuLuong; null nếu hợp đồng không bao phủ ngày đó.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LuongHieuLuc { get; set; }
    public DateOnly NgayThamChieuLuong { get; set; }

    /// <summary>Nhãn tiếng Việt của trạng thái hiệu lực tại thời điểm tra cứu, ví dụ "Đang hiệu lực", "Sắp hết hạn".</summary>
    public string TrangThai { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả tra cứu đơn từ (nghỉ phép, ốm đau, thai sản, thôi việc...) theo từ khóa, đã lọc
/// theo phạm vi quyền của người hỏi.
/// </summary>
public sealed class AiDataLeaveDto
{
    /// <summary>Từ khóa đã tra cứu, để mô hình nhắc lại đúng ý người hỏi.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số đơn từ khớp từ khóa trong phạm vi được phép.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi số kết quả hiển thị đã bị cắt vì vượt giới hạn an toàn ngữ cảnh.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Danh sách đơn từ khớp từ khóa.</summary>
    public IReadOnlyList<AiDataDonTuItemDto> DanhSach { get; set; } = [];
}

/// <summary>Một đơn từ trong kết quả tra cứu — chỉ thông tin trình duyệt, không dữ liệu nhạy cảm.</summary>
public sealed class AiDataDonTuItemDto
{
    /// <summary>Mã đơn trong hệ thống.</summary>
    public int MaDon { get; set; }

    /// <summary>Tên loại đơn (ví dụ "Nghỉ phép năm", "Nghỉ ốm đau / BHXH").</summary>
    public string LoaiDon { get; set; } = string.Empty;

    /// <summary>Tên nhân viên nộp đơn.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban của nhân viên nộp đơn; null khi chưa gán.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhongBan { get; set; }

    /// <summary>Ngày bắt đầu nghỉ.</summary>
    public DateOnly TuNgay { get; set; }

    /// <summary>Ngày kết thúc nghỉ.</summary>
    public DateOnly DenNgay { get; set; }

    /// <summary>Số ngày nghỉ tính từ khoảng thời gian (bao gồm cả hai đầu).</summary>
    public int SoNgayNghi { get; set; }

    /// <summary>Nhãn tiếng Việt của trạng thái trình duyệt (Chờ duyệt / Đã duyệt / Từ chối).</summary>
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Ngày gửi đơn.</summary>
    public DateOnly NgayGui { get; set; }

    /// <summary>Lý do nghỉ; null khi nhân viên không ghi.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LyDo { get; set; }

    /// <summary>Phản hồi của người duyệt khi đơn đã duyệt hoặc bị từ chối; null khi chưa duyệt.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhanHoiDuyet { get; set; }
}

/// <summary>
/// Kết quả tra cứu phân ca (lịch làm việc) theo từ khóa, đã lọc theo phạm vi quyền của người hỏi.
/// </summary>
public sealed class AiDataShiftDto
{
    /// <summary>Từ khóa đã tra cứu, để mô hình nhắc lại đúng ý người hỏi.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số ca trực khớp từ khóa trong phạm vi được phép.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi số kết quả hiển thị đã bị cắt vì vượt giới hạn an toàn ngữ cảnh.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Ngày bắt đầu của khoảng thời gian đã tra cứu.</summary>
    public DateOnly TuNgay { get; set; }

    /// <summary>Ngày kết thúc của khoảng thời gian đã tra cứu.</summary>
    public DateOnly DenNgay { get; set; }

    /// <summary>Danh sách ca trực khớp từ khóa.</summary>
    public IReadOnlyList<AiDataPhanCaItemDto> DanhSach { get; set; } = [];
}

/// <summary>Một ca trực trong kết quả tra cứu phân ca — chỉ lịch làm việc, không dữ liệu nhạy cảm.</summary>
public sealed class AiDataPhanCaItemDto
{
    /// <summary>Mã phân ca trong hệ thống.</summary>
    public int MaPhanCa { get; set; }

    /// <summary>Ngày làm việc.</summary>
    public DateOnly NgayLamViec { get; set; }

    /// <summary>Tên nhân viên trực ca.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban của nhân viên; null khi chưa gán.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhongBan { get; set; }

    /// <summary>Tên ca làm việc (ví dụ "Ca Sáng", "Ca Đêm").</summary>
    public string TenCa { get; set; } = string.Empty;

    /// <summary>Giờ bắt đầu ca.</summary>
    public string GioBatDau { get; set; } = string.Empty;

    /// <summary>Giờ kết thúc ca.</summary>
    public string GioKetThuc { get; set; } = string.Empty;

    /// <summary>True khi ca qua đêm (ví dụ ca 22:00 – 06:00).</summary>
    public bool QuaDem { get; set; }

    /// <summary>Nhãn tiếng Việt của trạng thái ca (Đã phân / Đã công bố...).</summary>
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Số giờ tăng ca dự kiến; 0 khi không có tăng ca.</summary>
    public decimal GioTangCaDuKien { get; set; }
}

/// <summary>
/// Kết quả tra cứu chấm công (quẹt thẻ) theo từ khóa — đã lọc theo phạm vi quyền.
/// Nhân viên chỉ thấy của chính mình; Manager chỉ bộ phận mình; Admin/HR/IT toàn khách sạn.
/// </summary>
public sealed class AiDataAttendanceDto
{
    /// <summary>Từ khóa tra cứu do router trích ra; rỗng khi hỏi chung.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số bản ghi chấm công khớp.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi danh sách bị cắt theo giới hạn mỗi lượt tra cứu.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Ngày bắt đầu của khoảng thời gian đã tra cứu (tuần hiện tại).</summary>
    public DateOnly TuNgay { get; set; }

    /// <summary>Ngày kết thúc của khoảng thời gian đã tra cứu (tuần hiện tại).</summary>
    public DateOnly DenNgay { get; set; }

    /// <summary>Danh sách chấm công đã rút gọn.</summary>
    public IReadOnlyList<AiDataChamCongItemDto> DanhSach { get; set; } = [];
}

/// <summary>
/// Một dòng chấm công đã rút gọn — chỉ giờ quẹt, phút trễ/sớm, giờ OT, trạng thái.
/// Không có tọa độ GPS, thiết bị quẹt, và không suy diễn lý do.
/// </summary>
public sealed class AiDataChamCongItemDto
{
    /// <summary>Mã phân ca (để đối chiếu với lịch phân ca).</summary>
    public int MaPhanCa { get; set; }

    /// <summary>Ngày làm việc.</summary>
    public DateOnly NgayLamViec { get; set; }

    /// <summary>Tên nhân viên.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban; null khi chưa gán.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhongBan { get; set; }

    /// <summary>Tên ca làm việc.</summary>
    public string TenCa { get; set; } = string.Empty;

    /// <summary>Giờ quẹt vào (HH:mm); null khi chưa quẹt.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GioCheckIn { get; set; }

    /// <summary>Giờ quẹt ra (HH:mm); null khi chưa quẹt.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GioCheckOut { get; set; }

    /// <summary>Số phút đi trễ.</summary>
    public int PhutDiTre { get; set; }

    /// <summary>Số phút về sớm.</summary>
    public int PhutVeSom { get; set; }

    /// <summary>Số giờ tăng ca (OT) thực tế.</summary>
    public decimal GioTangCaOt { get; set; }

    /// <summary>Nhãn tiếng Việt: Đúng giờ / Đi trễ / Về sớm / Trễ và sớm / Vắng mặt / Chưa quẹt thẻ.</summary>
    public string TrangThai { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả tra cứu sự kiện / dự án theo từ khóa — đã lọc theo phạm vi quyền.
/// Admin/HR/IT xem toàn khách sạn; Manager chỉ sự kiện có nhân sự bộ phận mình tham gia;
/// Employee chỉ xem sự kiện mình tham gia (hoặc được phân công).
/// </summary>
public sealed class AiDataEventDto
{
    /// <summary>Từ khóa tra cứu do router trích ra; rỗng khi hỏi chung.</summary>
    public string TuKhoa { get; set; } = string.Empty;

    /// <summary>Tổng số sự kiện khớp.</summary>
    public int TongSo { get; set; }

    /// <summary>True khi danh sách bị cắt theo giới hạn mỗi lượt tra cứu.</summary>
    public bool BiGioiHan { get; set; }

    /// <summary>Danh sách sự kiện đã rút gọn.</summary>
    public IReadOnlyList<AiDataSuKienItemTraCuuDto> DanhSach { get; set; } = [];
}

/// <summary>
/// Một sự kiện / dự án đã rút gọn — chỉ thông tin tra cứu cơ bản và nhân sự tham gia.
/// Không có ngân sách phụ cấp chi tiết, số tiền phụ cấp từng người, hay lý do hủy/đóng sự kiện.
/// </summary>
public sealed class AiDataSuKienItemTraCuuDto
{
    /// <summary>Mã sự kiện / dự án trong hệ thống.</summary>
    public int MaDuAn { get; set; }

    /// <summary>Tên sự kiện / dự án.</summary>
    public string TenDuAn { get; set; } = string.Empty;

    /// <summary>Nhãn tiếng Việt của loại sự kiện (Tiệc cưới, Hội nghị, Gala Dinner...).</summary>
    public string LoaiSuKien { get; set; } = string.Empty;

    /// <summary>Địa điểm tổ chức.</summary>
    public string DiaDiem { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu (giờ Việt Nam).</summary>
    public DateTimeOffset NgayBatDau { get; set; }

    /// <summary>Ngày kết thúc dự kiến (giờ Việt Nam).</summary>
    public DateTimeOffset NgayKetThuc { get; set; }

    /// <summary>Nhãn tiếng Việt của trạng thái (Lên kế hoạch / Đang diễn ra / ...).</summary>
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Số nhân sự đang tham gia sự kiện (không tính phân công đã hủy).</summary>
    public int SoNhanSuThamGia { get; set; }

    /// <summary>Tên các nhân sự tham gia; tối đa vài tên nổi bật, rỗng khi không có.</summary>
    public string NhanSuThamGia { get; set; } = string.Empty;
}
