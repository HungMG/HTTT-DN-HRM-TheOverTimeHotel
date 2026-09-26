namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Model hiển thị thông tin đơn từ trên Mobile App (.NET MAUI).
/// Đã tính toán sẵn các thuộc tính chuỗi và màu sắc giúp XAML DataBinding nhanh và chuẩn WCAG AA.
/// </summary>
public class LeaveRequestItem
{
    public int MaDon { get; set; }
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public int? MaPhongBan { get; set; }
    public string TenPhongBan { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public int MaLoaiDon { get; set; }
    public string TenLoaiDon { get; set; } = string.Empty;
    public bool CoHuongLuong { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public DateTimeOffset NgayGui { get; set; }
    public string TrangThai { get; set; } = "CHO_DUYET";
    public int? MaNguoiDuyet { get; set; }
    public string? NguoiDuyetTen { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? PhanHoiDuyet { get; set; }

    // === CÁC THUỘC TÍNH TÍNH TOÁN HIỂN THỊ (DISPLAY GETTERS) ===

    public string MaNhanVienDisplay => $"NV-{MaNhanVien:D4}";

    public string PeriodDisplay => $"{TuNgay:dd/MM/yyyy} ➔ {DenNgay:dd/MM/yyyy}";

    public string NgayGuiDisplay => NgayGui.ToLocalTime().ToString("HH:mm dd/MM/yyyy");

    public string NgayDuyetDisplay => NgayDuyet?.ToLocalTime().ToString("HH:mm dd/MM/yyyy") ?? "--";

    public int SoNgayNghi => Math.Max(1, DenNgay.DayNumber - TuNgay.DayNumber + 1);

    public string SoNgayNghiDisplay => $"{SoNgayNghi} ngày";

    public string SalaryBadgeText => CoHuongLuong ? "Có hưởng lương" : "Nghỉ không lương";

    public string SalaryBadgeBg => CoHuongLuong ? "#ECFDF5" : "#F1F5F9";

    public string SalaryBadgeTextColor => CoHuongLuong ? "#047857" : "#475569";

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TenNhanVien)) return "?";
            var parts = TenNhanVien.Trim().Split(' ');
            return parts[^1][..1].ToUpper();
        }
    }

    public string StatusText => TrangThai switch
    {
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        _ => TrangThai
    };

    public string StatusColor => TrangThai switch
    {
        "CHO_DUYET" => "#B45309", // Amber-700
        "DA_DUYET" => "#15803D",  // Green-700
        "TU_CHOI" => "#B91C1C",   // Red-700
        _ => "#64748B"
    };

    public string StatusBgColor => TrangThai switch
    {
        "CHO_DUYET" => "#FEF3C7", // Amber-100
        "DA_DUYET" => "#DCFCE7",  // Green-100
        "TU_CHOI" => "#FEE2E2",   // Red-100
        _ => "#F1F5F9"
    };

    public string TypeBgColor => MaLoaiDon switch
    {
        1 => "#ECFDF5", // Phép năm (Emerald-50)
        2 => "#FFF7ED", // Nghỉ ốm (Orange-50)
        3 => "#EEF2FF", // Thai sản (Indigo-50)
        4 => "#F1F5F9", // Việc riêng (Slate-100)
        5 => "#FFF1F2", // Thôi việc (Rose-50)
        _ => "#F8FAFC"
    };

    public string TypeColor => MaLoaiDon switch
    {
        1 => "#047857", // Emerald-700
        2 => "#C2410C", // Orange-700
        3 => "#4338CA", // Indigo-700
        4 => "#475569", // Slate-600
        5 => "#BE123C", // Rose-700
        _ => "#334155"
    };
}
