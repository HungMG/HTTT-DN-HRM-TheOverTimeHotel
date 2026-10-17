using Microsoft.AspNetCore.Mvc.Rendering;

namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// Dữ liệu cho phân hệ Báo cáo &amp; Thống kê Quản trị (Phân hệ 10.0 — TSK-24).
///
/// <para>
/// Một view model gộp: phần bộ lọc đã chọn + sáu báo cáo con ứng với PRD 10.1 → 10.6.
/// Controller dựng, view render, và vùng <c>_BaoCaoBoard</c> được nạp lại tại chỗ khi
/// người dùng đổi kỳ / phòng ban / tab.
/// </para>
/// </summary>
public class BaoCaoViewModel
{
    // ── Bộ lọc ─────────────────────────────────────────────────────────────
    /// <summary>Kiểu kỳ đang chọn: <c>THANG</c>, <c>QUY</c> hoặc <c>NAM</c>.</summary>
    public string KyBaoCao { get; set; } = "THANG";

    /// <summary>Năm đang xem.</summary>
    public int Nam { get; set; }

    /// <summary>Tháng đang xem (1-12); chỉ có giá trị khi <see cref="KyBaoCao"/> là THANG.</summary>
    public int? Thang { get; set; }

    /// <summary>Quý đang xem (1-4); chỉ có giá trị khi <see cref="KyBaoCao"/> là QUY.</summary>
    public int? Quy { get; set; }

    /// <summary>Phòng ban đang lọc; null nghĩa là toàn khách sạn (chỉ Admin/HR/IT được dùng).</summary>
    public int? PhongBanId { get; set; }

    /// <summary>Nhãn kỳ để hiển thị, ví dụ "Tháng 10/2026" hoặc "Quý 4/2026".</summary>
    public string NhanKy { get; set; } = string.Empty;

    /// <summary>Ngày đầu kỳ báo cáo.</summary>
    public DateOnly TuNgay { get; set; }

    /// <summary>Ngày cuối kỳ báo cáo.</summary>
    public DateOnly DenNgay { get; set; }

    /// <summary>Tab đang mở, dùng cho deep-link <c>?tab=</c>.</summary>
    public string Tab { get; set; } = "bien-dong";

    // ── Phạm vi &amp; năng lực của người đang đăng nhập ────────────────────
    /// <summary>True nếu người đang xem được thấy số liệu toàn khách sạn.</summary>
    public bool IsWholeHotel { get; set; }

    /// <summary>Nhãn phạm vi hiển thị, ví dụ "Toàn khách sạn" hoặc "Bộ phận: Lễ tân".</summary>
    public string PhamViLabel { get; set; } = string.Empty;

    /// <summary>Nhãn vai trò đang đăng nhập, ví dụ "Quản trị viên".</summary>
    public string RoleLabel { get; set; } = string.Empty;

    /// <summary>Người đang xem có được thấy báo cáo quỹ lương (10.4) hay không.</summary>
    public bool CanViewPayrollFund { get; set; }

    /// <summary>Danh mục phòng ban cho ô lọc; rỗng khi bị khóa trong một bộ phận.</summary>
    public IReadOnlyList<SelectListItem> PhongBanOptions { get; set; } = [];

    /// <summary>Các năm có thể chọn (năm hiện tại lùi về trước vài năm).</summary>
    public IReadOnlyList<int> NamOptions { get; set; } = [];

    // ── Trạng thái ─────────────────────────────────────────────────────────
    /// <summary>Cho biết đã đọc được dữ liệu từ Supabase hay chưa.</summary>
    public bool IsDatabaseOnline { get; set; }

    /// <summary>Thời điểm dựng số liệu, hiển thị ở dòng "cập nhật lúc ...".</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>JSON payload cho ECharts; controller serialize, view nhúng vào thẻ script.</summary>
    public string ChartJson { get; set; } = "{}";

    // ── Sáu báo cáo ────────────────────────────────────────────────────────
    /// <summary>Báo cáo 10.1 — Biến động nhân sự.</summary>
    public BienDongNhanSuReport BienDong { get; set; } = new();

    /// <summary>Báo cáo 10.2 — Cơ cấu nhân lực.</summary>
    public CoCauNhanLucReport CoCau { get; set; } = new();

    /// <summary>Báo cáo 10.3 — Học vấn &amp; thâm niên.</summary>
    public HocVanThamNienReport HocVanThamNien { get; set; } = new();

    /// <summary>Báo cáo 10.4 — Đối soát quỹ lương vs ngân sách.</summary>
    public QuyLuongNganSachReport QuyLuong { get; set; } = new();

    /// <summary>Báo cáo 10.5 — Chuyên cần &amp; vi phạm.</summary>
    public ChuyenCanViPhamReport ChuyenCan { get; set; } = new();

    /// <summary>Báo cáo 10.6 — Hiệu quả nhân sự dự án/sự kiện.</summary>
    public HieuQuaSuKienReport SuKien { get; set; } = new();
}

/// <summary>Một lát cắt phân bổ dùng chung cho biểu đồ tròn: nhãn + giá trị + tỷ lệ phần trăm.</summary>
public class PhanBoItem
{
    /// <summary>Nhãn hiển thị của lát cắt.</summary>
    public string Nhan { get; set; } = string.Empty;

    /// <summary>Giá trị tuyệt đối (số người, số lượt...).</summary>
    public int GiaTri { get; set; }

    /// <summary>Tỷ lệ phần trăm trên tổng.</summary>
    public decimal TyLe { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.1 — BIẾN ĐỘNG NHÂN SỰ
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo biến động nhân sự theo PRD 10.1.</summary>
public class BienDongNhanSuReport
{
    /// <summary>Quân số đầu kỳ.</summary>
    public int DauKy { get; set; }

    /// <summary>Số nhân sự tuyển mới trong kỳ.</summary>
    public int TuyenMoi { get; set; }

    /// <summary>Số nhân sự thôi việc trong kỳ.</summary>
    public int NghiViec { get; set; }

    /// <summary>Quân số cuối kỳ.</summary>
    public int CuoiKy { get; set; }

    /// <summary>Tỷ lệ thôi việc (Turnover Rate) trên quân số bình quân, tính theo phần trăm.</summary>
    public decimal TyLeThoiViec { get; set; }

    /// <summary>Biến động ròng trong kỳ (tuyển mới - thôi việc).</summary>
    public int BienDongRong { get; set; }

    /// <summary>Chuỗi số liệu theo từng tháng trong kỳ.</summary>
    public IReadOnlyList<BienDongSeriesItem> Series { get; set; } = [];

    /// <summary>Bảng chi tiết tuyển mới / thôi việc theo phòng ban.</summary>
    public IReadOnlyList<BienDongPhongBanItem> TheoPhongBan { get; set; } = [];
}

/// <summary>Một điểm dữ liệu theo tháng cho biểu đồ biến động.</summary>
public class BienDongSeriesItem
{
    /// <summary>Nhãn tháng, ví dụ "T10".</summary>
    public string Nhan { get; set; } = string.Empty;

    /// <summary>Số tuyển mới trong tháng.</summary>
    public int TuyenMoi { get; set; }

    /// <summary>Số thôi việc trong tháng.</summary>
    public int NghiViec { get; set; }

    /// <summary>Quân số cuối tháng.</summary>
    public int CuoiKy { get; set; }
}

/// <summary>Biến động nhân sự của một phòng ban.</summary>
public class BienDongPhongBanItem
{
    /// <summary>Tên phòng ban.</summary>
    public string PhongBan { get; set; } = string.Empty;

    /// <summary>Quân số hiện tại của phòng ban.</summary>
    public int QuanSo { get; set; }

    /// <summary>Số tuyển mới trong kỳ.</summary>
    public int TuyenMoi { get; set; }

    /// <summary>Số thôi việc trong kỳ.</summary>
    public int NghiViec { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.2 — CƠ CẤU NHÂN LỰC
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo cơ cấu nhân lực theo PRD 10.2.</summary>
public class CoCauNhanLucReport
{
    /// <summary>Tổng quân số đang xét.</summary>
    public int TongNhanSu { get; set; }

    /// <summary>Số đang làm việc.</summary>
    public int DangLam { get; set; }

    /// <summary>Số đang nghỉ phép.</summary>
    public int NghiPhep { get; set; }

    /// <summary>Số đã thôi việc.</summary>
    public int DaThoiViec { get; set; }

    /// <summary>Cơ cấu theo phòng ban (biểu đồ tròn chính).</summary>
    public IReadOnlyList<PhanBoItem> TheoPhongBan { get; set; } = [];

    /// <summary>Cơ cấu theo trạng thái làm việc.</summary>
    public IReadOnlyList<PhanBoItem> TheoTrangThai { get; set; } = [];

    /// <summary>Cơ cấu theo giới tính.</summary>
    public IReadOnlyList<PhanBoItem> TheoGioiTinh { get; set; } = [];

    /// <summary>Cơ cấu theo chức vụ (tối đa 8 chức vụ đông nhất).</summary>
    public IReadOnlyList<PhanBoItem> TheoChucVu { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.3 — HỌC VẤN & THÂM NIÊN
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo phân tích học vấn &amp; thâm niên theo PRD 10.3.</summary>
public class HocVanThamNienReport
{
    /// <summary>Phân bổ trình độ đào tạo (sáu bậc chuẩn + nhóm "Khác").</summary>
    public IReadOnlyList<PhanBoItem> TheoTrinhDo { get; set; } = [];

    /// <summary>Phân bổ thâm niên theo ba nhóm của PRD.</summary>
    public IReadOnlyList<PhanBoItem> TheoThamNien { get; set; } = [];

    /// <summary>Tỷ lệ nhân sự có trình độ từ đại học trở lên, tính theo phần trăm.</summary>
    public decimal TyLeDaiHocTroLen { get; set; }

    /// <summary>Thâm niên trung bình của quân số đang xét, tính theo năm.</summary>
    public decimal ThamNienTrungBinh { get; set; }

    /// <summary>Số nhân sự có trình độ từ đại học trở lên.</summary>
    public int SoDaiHocTroLen { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.4 — ĐỐI SOÁT QUỸ LƯƠNG VS NGÂN SÁCH
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo đối soát quỹ lương vs ngân sách theo PRD 10.4.</summary>
public class QuyLuongNganSachReport
{
    /// <summary>Tổng thực chi theo nghĩa thực nhận (đã trừ khấu trừ) trong kỳ.</summary>
    public decimal ThucChi { get; set; }

    /// <summary>Tổng thực chi theo nghĩa gross (lương công + tăng ca + phụ cấp + thưởng).</summary>
    public decimal ThucChiGross { get; set; }

    /// <summary>Định mức tham chiếu đã quy đổi theo số tháng của kỳ.</summary>
    public decimal NganSachThamChieu { get; set; }

    /// <summary>Chênh lệch giữa thực chi (gross) và định mức tham chiếu.</summary>
    public decimal ChenhLech { get; set; }

    /// <summary>Tỷ lệ giải ngân so với định mức, tính theo phần trăm.</summary>
    public decimal PhanTramGiaiNgan { get; set; }

    /// <summary>True khi tỷ lệ vượt ngưỡng cảnh báo — giao diện bật cảnh báo đỏ.</summary>
    public bool VuotNguong { get; set; }

    /// <summary>Số tháng của kỳ báo cáo, dùng để quy đổi định mức.</summary>
    public int SoThangTrongKy { get; set; }

    /// <summary>Số hợp đồng đang hiệu lực dùng để suy định mức.</summary>
    public int SoHopDong { get; set; }

    /// <summary>Số phiếu lương đã tổng hợp trong kỳ.</summary>
    public int SoPhieuLuong { get; set; }

    /// <summary>Chuỗi số liệu theo tháng: định mức vs thực chi.</summary>
    public IReadOnlyList<QuyLuongSeriesItem> Series { get; set; } = [];

    /// <summary>Phân rã chi phí theo phòng ban.</summary>
    public IReadOnlyList<QuyLuongPhongBanItem> TheoPhongBan { get; set; } = [];

    /// <summary>Ghi chú phương pháp — nói rõ định mức là số suy ra, không phải ngân sách duyệt thật.</summary>
    public string GhiChuPhuongPhap { get; set; } = string.Empty;
}

/// <summary>Một điểm dữ liệu theo tháng cho biểu đồ cột định mức vs thực tế.</summary>
public class QuyLuongSeriesItem
{
    /// <summary>Nhãn tháng, ví dụ "T10".</summary>
    public string Nhan { get; set; } = string.Empty;

    /// <summary>Định mức tham chiếu của tháng.</summary>
    public decimal NganSach { get; set; }

    /// <summary>Thực chi của tháng.</summary>
    public decimal ThucChi { get; set; }
}

/// <summary>Chi phí lương của một phòng ban.</summary>
public class QuyLuongPhongBanItem
{
    /// <summary>Tên phòng ban.</summary>
    public string PhongBan { get; set; } = string.Empty;

    /// <summary>Định mức tham chiếu của phòng ban.</summary>
    public decimal NganSach { get; set; }

    /// <summary>Thực chi của phòng ban.</summary>
    public decimal ThucChi { get; set; }

    /// <summary>Chênh lệch thực chi - định mức.</summary>
    public decimal ChenhLech { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.5 — CHUYÊN CẦN & VI PHẠM
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo chuyên cần &amp; vi phạm theo PRD 10.5.</summary>
public class ChuyenCanViPhamReport
{
    /// <summary>Tổng số lượt chấm công trong kỳ.</summary>
    public int TongBanGhi { get; set; }

    /// <summary>Số lượt đúng giờ.</summary>
    public int DungGio { get; set; }

    /// <summary>Số lượt đi trễ.</summary>
    public int DiTre { get; set; }

    /// <summary>Số lượt về sớm.</summary>
    public int VeSom { get; set; }

    /// <summary>Số lượt vừa trễ vừa sớm.</summary>
    public int TreVaSom { get; set; }

    /// <summary>Số lượt vắng mặt.</summary>
    public int Vang { get; set; }

    /// <summary>Tỷ lệ chuyên cần = đúng giờ / tổng lượt, tính theo phần trăm.</summary>
    public decimal TyLeChuyenCan { get; set; }

    /// <summary>Tổng số phút đi trễ trong kỳ.</summary>
    public int TongPhutTre { get; set; }

    /// <summary>Tổng số phút về sớm trong kỳ.</summary>
    public int TongPhutVeSom { get; set; }

    /// <summary>Tổng giờ tăng ca OT trong kỳ.</summary>
    public decimal TongGioOt { get; set; }

    /// <summary>Diễn biến tỷ lệ chuyên cần theo từng tháng.</summary>
    public IReadOnlyList<ChuyenCanSeriesItem> Series { get; set; } = [];

    /// <summary>Bảng xếp hạng nhân sự vi phạm nhiều nhất (top 10).</summary>
    public IReadOnlyList<ViPhamNhanSuItem> XepHangViPham { get; set; } = [];

    /// <summary>Tỷ lệ chuyên cần theo từng phòng ban.</summary>
    public IReadOnlyList<PhanBoItem> TheoPhongBan { get; set; } = [];
}

/// <summary>Một điểm dữ liệu chuyên cần theo tháng.</summary>
public class ChuyenCanSeriesItem
{
    /// <summary>Nhãn tháng, ví dụ "T10".</summary>
    public string Nhan { get; set; } = string.Empty;

    /// <summary>Tỷ lệ chuyên cần của tháng, tính theo phần trăm.</summary>
    public decimal TyLeChuyenCan { get; set; }

    /// <summary>Số lượt vi phạm của tháng.</summary>
    public int ViPham { get; set; }
}

/// <summary>Một nhân sự trong bảng xếp hạng vi phạm kỷ luật.</summary>
public class ViPhamNhanSuItem
{
    /// <summary>Họ tên nhân viên.</summary>
    public string HoTen { get; set; } = string.Empty;

    /// <summary>Tên phòng ban.</summary>
    public string PhongBan { get; set; } = string.Empty;

    /// <summary>Số lượt vi phạm (trễ/sớm).</summary>
    public int SoLanViPham { get; set; }

    /// <summary>Tổng số phút đi trễ.</summary>
    public int TongPhutTre { get; set; }

    /// <summary>Tổng số phút về sớm.</summary>
    public int TongPhutVeSom { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  10.6 — HIỆU QUẢ NHÂN SỰ DỰ ÁN / SỰ KIỆN
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Báo cáo hiệu quả nhân sự dự án/sự kiện theo PRD 10.6.</summary>
public class HieuQuaSuKienReport
{
    /// <summary>Tổng số sự kiện trong kỳ.</summary>
    public int TongSuKien { get; set; }

    /// <summary>Số sự kiện đang diễn ra.</summary>
    public int DangDienRa { get; set; }

    /// <summary>Số sự kiện đã hoàn thành.</summary>
    public int DaHoanThanh { get; set; }

    /// <summary>Số sự kiện đã hủy.</summary>
    public int DaHuy { get; set; }

    /// <summary>Tổng số lượt phân công nhân sự cho các sự kiện.</summary>
    public int TongLuotPhanCong { get; set; }

    /// <summary>Tổng ngân sách phụ cấp dự kiến của các sự kiện trong kỳ.</summary>
    public decimal TongNganSach { get; set; }

    /// <summary>Tổng phụ cấp thực tế đã phân bổ cho nhân sự.</summary>
    public decimal TongPhuCapDaPhanBo { get; set; }

    /// <summary>Tỷ lệ phân công được đánh giá Xuất sắc, tính theo phần trăm.</summary>
    public decimal TyLeXuatSac { get; set; }

    /// <summary>Tỷ lệ phân công đã hoàn thành (gồm cả xuất sắc), tính theo phần trăm.</summary>
    public decimal TyLeHoanThanh { get; set; }

    /// <summary>Danh sách sự kiện kèm chi phí và kết quả nhân sự.</summary>
    public IReadOnlyList<SuKienItem> DanhSach { get; set; } = [];

    /// <summary>Cơ cấu sự kiện theo loại (biểu đồ tròn).</summary>
    public IReadOnlyList<PhanBoItem> TheoLoai { get; set; } = [];

    /// <summary>Cơ cấu đánh giá nhân sự tham gia sự kiện (biểu đồ tròn).</summary>
    public IReadOnlyList<PhanBoItem> TheoDanhGia { get; set; } = [];
}

/// <summary>Một sự kiện/dự án kèm hiệu quả sử dụng nhân sự.</summary>
public class SuKienItem
{
    /// <summary>Mã dự án.</summary>
    public int MaDuAn { get; set; }

    /// <summary>Tên sự kiện.</summary>
    public string TenDuAn { get; set; } = string.Empty;

    /// <summary>Nhãn loại sự kiện đã dịch sang tiếng Việt.</summary>
    public string LoaiLabel { get; set; } = string.Empty;

    /// <summary>Nhãn trạng thái đã dịch sang tiếng Việt.</summary>
    public string TrangThaiLabel { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu (theo giờ địa phương).</summary>
    public DateOnly TuNgay { get; set; }

    /// <summary>Ngày kết thúc (theo giờ địa phương).</summary>
    public DateOnly DenNgay { get; set; }

    /// <summary>Số nhân sự được điều động.</summary>
    public int SoNhanSu { get; set; }

    /// <summary>Ngân sách phụ cấp dự kiến.</summary>
    public decimal NganSach { get; set; }

    /// <summary>Tổng phụ cấp đã phân bổ cho nhân sự.</summary>
    public decimal PhuCapDaPhanBo { get; set; }

    /// <summary>Tỷ lệ sử dụng ngân sách, tính theo phần trăm.</summary>
    public decimal TyLeSuDungNganSach { get; set; }

    /// <summary>Số phân công được đánh giá xuất sắc.</summary>
    public int SoXuatSac { get; set; }

    /// <summary>Số phân công hoàn thành.</summary>
    public int SoHoanThanh { get; set; }

    /// <summary>Số phân công chưa được đánh giá.</summary>
    public int SoChuaDanhGia { get; set; }
}
