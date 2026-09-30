namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// Dữ liệu cho bảng điều khiển quản trị tại <c>Home/Index</c>.
/// Chỉ chứa số liệu nghiệp vụ thật lấy từ CSDL: chấm công, đơn từ, hợp đồng và lương.
/// </summary>
public class HomeDashboardViewModel
{
    // ── Chỉ số điều hành ────────────────────────────────────────────────────
    /// <summary>Số nhân viên đang làm việc tại khách sạn.</summary>
    public int NhanVienDangLam { get; set; }

    /// <summary>Số nhân viên đang làm việc tại kỳ lương liền trước, dùng để tính biến động.</summary>
    public int NhanVienKyTruoc { get; set; }

    /// <summary>Số bản ghi chấm công của ngày hôm nay.</summary>
    public int BanGhiChamCongHomNay { get; set; }

    /// <summary>Số bản ghi chấm công hôm nay đã quẹt thẻ đúng giờ.</summary>
    public int ChamCongDungGioHomNay { get; set; }

    /// <summary>Số bản ghi chấm công hôm nay đi trễ hoặc về sớm.</summary>
    public int ChamCongDiTreHomNay { get; set; }

    /// <summary>Tỷ lệ đúng giờ của 7 ngày liền trước, tính theo phần trăm.</summary>
    public decimal TyLeDungGioKyTruoc { get; set; }

    /// <summary>Số đơn từ đang chờ người đang đăng nhập phê duyệt.</summary>
    public int DonTuChoDuyet { get; set; }

    /// <summary>Tổng thực nhận của kỳ lương đang mở.</summary>
    public decimal QuyLuongKyHienTai { get; set; }

    /// <summary>Nhãn kỳ lương đang mở, ví dụ "09/2026".</summary>
    public string KyLuongHienTai { get; set; } = string.Empty;

    /// <summary>Số phiếu lương đã phát hành trong kỳ hiện tại.</summary>
    public int SoPhieuLuongDaDuyet { get; set; }

    /// <summary>Tổng số phiếu lương của kỳ hiện tại.</summary>
    public int SoPhieuLuongKyHienTai { get; set; }

    /// <summary>Số hợp đồng sắp hết hạn trong vòng 30 ngày tới.</summary>
    public int HopDongSapHetHan { get; set; }

    // ── Hàng đợi công việc chờ xử lý ────────────────────────────────────────
    /// <summary>Các công việc tồn đọng đã được sắp xếp theo độ gấp.</summary>
    public IReadOnlyList<PendingActionItem> ChoXuLy { get; set; } = [];

    // ── Đội hình ca trực hôm nay ───────────────────────────────────────────
    /// <summary>Số nhân viên có mặt theo từng ca trong ngày.</summary>
    public IReadOnlyList<ShiftSummaryItem> CaTrucHomNay { get; set; } = [];

    /// <summary>Ca đang diễn ra tại thời điểm hiển thị.</summary>
    public string CaDangChay { get; set; } = string.Empty;

    // ── Trạng thái hệ thống ────────────────────────────────────────────────
    /// <summary>Cho biết đã đọc được dữ liệu từ Supabase hay chưa.</summary>
    public bool IsDatabaseOnline { get; set; }

    /// <summary>Thời điểm dựng dữ liệu, dùng để hiển thị "cập nhật lúc ...".</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Một công việc tồn đọng trong khối "Cần bạn xử lý".</summary>
public class PendingActionItem
{
    /// <summary>Mã duy nhất phục vụ render khóa danh sách.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Nhóm việc: ĐƠN_TỪ, HỢP_ĒNG hoặc PHIẾU_LƯƠNG.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Nhãn hiển thị của nhóm việc.</summary>
    public string CategoryLabel { get; set; } = string.Empty;

    /// <summary>Tiêu đề chính, ví dụ tên nhân viên hoặc kỳ lương.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả phụ, ví dụ khoảng thời gian nghỉ hoặc số hợp đồng.</summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Số ngày đã chờ, dùng để sắp xếp độ gấp.</summary>
    public int WaitingDays { get; set; }

    /// <summary>Nhãn mức độ gấp: danger, warning hoặc info.</summary>
    public string Severity { get; set; } = "info";

    /// <summary>Đường dẫn để mở màn hình xử lý chi tiết.</summary>
    public string? ActionUrl { get; set; }
}

/// <summary>Tóm tắt số lượng nhân sự theo một khung ca.</summary>
public class ShiftSummaryItem
{
    /// <summary>Tên ca, ví dụ "Ca Sáng".</summary>
    public string TenCa { get; set; } = string.Empty;

    /// <summary>Khoảng giờ của ca.</summary>
    public string Gio { get; set; } = string.Empty;

    /// <summary>Số người được xếp ca.</summary>
    public int TongSo { get; set; }

    /// <summary>Số người đã quẹt thẻ check-in.</summary>
    public int DaCheckIn { get; set; }

    /// <summary>Đánh dấu đây là ca đang diễn ra.</summary>
    public bool IsCurrent { get; set; }
}
