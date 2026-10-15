using Microsoft.AspNetCore.Mvc.Rendering;

namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// ViewModel chính cho màn hình Đơn từ trực tuyến (xem danh sách, lọc, thống kê KPI).
/// </summary>
public class DonTuIndexViewModel
{
    /// <summary>Góc nhìn: "pending" (Chờ duyệt), "reviewed" (Đã xử lý / Xem lại), "all" (Tất cả)</summary>
    public string View { get; set; } = "pending";

    /// <summary>Lọc loại đơn: "all" hoặc "1" -> "5"</summary>
    public string Type { get; set; } = "all";

    /// <summary>Lọc theo phòng ban (dành cho Admin / HR)</summary>
    public int? PhongBanId { get; set; }

    /// <summary>Lọc trạng thái cụ thể: CHO_DUYET, DA_DUYET, TU_CHOI</summary>
    public string? Status { get; set; }

    /// <summary>Từ khóa tìm kiếm theo tên, mã NV, lý do</summary>
    public string? Keyword { get; set; }

    /// <summary>Khoảng ngày bắt đầu</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Khoảng ngày kết thúc</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Danh sách đơn từ hiển thị trên trang hiện tại</summary>
    public IReadOnlyList<DonTuItemViewModel> Items { get; set; } = [];

    // Chỉ số KPI
    public int TotalPendingCount { get; set; }
    public int TotalApprovedCount { get; set; }
    public int TotalRejectedCount { get; set; }
    public int TotalCount { get; set; }
    public int TotalFiltered { get; set; }

    // Danh sách chọn lọc
    public IReadOnlyList<SelectListItem> LoaiDonOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PhongBanOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];

    // Phân trang
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;

    // Phân quyền người dùng hiện tại
    public string CurrentRole { get; set; } = string.Empty;
    public int? CurrentEmployeeId { get; set; }
    public int? CurrentDepartmentId { get; set; }
}

/// <summary>
/// Thông tin từng đơn từ hiển thị trong bảng dữ liệu hoặc thẻ xem lại.
/// </summary>
public class DonTuItemViewModel
{
    public int MaDon { get; set; }
    public int MaNhanVien { get; set; }
    public string HoTenNhanVien { get; set; } = string.Empty;
    public string MaNhanVienCode => $"NV{MaNhanVien:D3}";
    public string AvatarInitial => string.IsNullOrWhiteSpace(HoTenNhanVien) ? "U" : HoTenNhanVien.Trim().Substring(HoTenNhanVien.Trim().LastIndexOf(' ') + 1)[0].ToString().ToUpperInvariant();
    public string? ChucVu { get; set; }
    public int? MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public string? EmailCongVu { get; set; }

    public int MaLoaiDon { get; set; }
    public string TenLoaiDon { get; set; } = string.Empty;
    public bool CoHuongLuong { get; set; }

    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int SoNgayNghi => Math.Max(1, DenNgay.DayNumber - TuNgay.DayNumber + 1);

    public string LyDo { get; set; } = string.Empty;
    public DateTimeOffset NgayGui { get; set; }

    public string TrangThai { get; set; } = "CHO_DUYET";
    public string TrangThaiLabel => TrangThai switch
    {
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        _ => TrangThai
    };

    public int? MaNguoiDuyet { get; set; }
    public string? HoTenNguoiDuyet { get; set; }
    public string? ChucVuNguoiDuyet { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? PhanHoiDuyet { get; set; }

    /// <summary>Liệu người dùng hiện tại có đủ thẩm quyền duyệt đơn này không</summary>
    public bool CanCurrentUserReview { get; set; }
    public string? CannotReviewReason { get; set; }

    // Dữ liệu bối cảnh bổ sung
    public int? PhepNamDaDung { get; set; }
    public int? PhepNamConLai { get; set; }
    public string? LoaiHopDong { get; set; }
    public int? SoNgayBaoTruocThucTe { get; set; }
    public int? SoNgayBaoTruocYeuCau { get; set; }
}

/// <summary>
/// Dữ liệu đầu vào khi thực hiện Phê duyệt hoặc Từ chối đơn từ.
/// </summary>
public class DonTuReviewInputModel
{
    public int MaDon { get; set; }
    public bool Approved { get; set; }
    public string? PhanHoi { get; set; }
}
