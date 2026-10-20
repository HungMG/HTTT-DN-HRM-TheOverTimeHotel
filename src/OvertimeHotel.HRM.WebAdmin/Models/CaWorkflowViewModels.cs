using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.WebAdmin.Models;

// ═══════════════════════════════════════════════════════════════════════════
//  PRD 3.4 — QUẢN LÝ ĐỔI CA
// ═══════════════════════════════════════════════════════════════════════════

public class YeuCauDoiCaIndexViewModel
{
    public string? TrangThai { get; set; }
    public string? Keyword { get; set; }

    public IReadOnlyList<SelectListItem> TrangThaiOptions { get; set; } = [];

    public IReadOnlyList<YeuCauDoiCaItemViewModel> Items { get; set; } = [];

    public int TongYeuCau { get; set; }
    public int ChoDuyet { get; set; }
    public int DaDuyet { get; set; }
    public int TuChoi { get; set; }
    public int DaHuy { get; set; }

    /// <summary>Người đang đăng nhập có quyền bấm duyệt/từ chối — Manager bộ phận, HR và Admin.</summary>
    public bool CoQuyenDuyet { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;
    public int TotalFilteredItems { get; set; }
}

public class YeuCauDoiCaItemViewModel
{
    public int MaYeuCau { get; set; }

    public int MaPhanCaGui { get; set; }
    public int MaPhanCaNhan { get; set; }

    public string TenNguoiGui { get; set; } = string.Empty;
    public string MaNguoiGuiFormat { get; set; } = string.Empty;
    public string TenPhongBanNguoiGui { get; set; } = string.Empty;

    public string TenNguoiNhan { get; set; } = string.Empty;
    public string MaNguoiNhanFormat { get; set; } = string.Empty;
    public string TenPhongBanNguoiNhan { get; set; } = string.Empty;

    public DateOnly NgayCaGui { get; set; }
    public string TenCaGui { get; set; } = string.Empty;
    public string GioCaGui { get; set; } = string.Empty;

    public DateOnly NgayCaNhan { get; set; }
    public string TenCaNhan { get; set; } = string.Empty;
    public string GioCaNhan { get; set; } = string.Empty;

    public string LyDo { get; set; } = string.Empty;
    public string TrangThai { get; set; } = "CHO_DUYET";

    public string? TenNguoiDuyet { get; set; }
    public DateTimeOffset NgayGui { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? PhanHoiDuyet { get; set; }

    public string TrangThaiNhan => TrangThai switch
    {
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        "DA_HUY" => "Đã huỷ",
        _ => TrangThai
    };

    /// <summary>Lớp Bootstrap của nhãn trạng thái, dùng chung bảng màu với màn Đơn từ.</summary>
    public string TrangThaiCss => TrangThai switch
    {
        "CHO_DUYET" => "bg-warning text-dark",
        "DA_DUYET" => "bg-success",
        "TU_CHOI" => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>Chỉ yêu cầu còn chờ mới có nút duyệt/từ chối.</summary>
    public bool CoTheXuLy => TrangThai == "CHO_DUYET";
}

public class YeuCauDoiCaCreateViewModel
{
    public int MaPhanCaGui { get; set; }
    public int MaPhanCaNhan { get; set; }
    public string LyDo { get; set; } = string.Empty;

    /// <summary>
    /// Ca đang có của chính người đăng nhập mà họ có thể đem nhường.
    /// Nạp theo đúng nhân viên trong phiên, không cho chọn người khác.
    /// </summary>
    public IReadOnlyList<SelectListItem> CaCuaToi { get; set; } = [];

    /// <summary>
    /// Ca của đồng nghiệp cùng phòng ban có thể nhận. Nạp theo bộ phận của người
    /// đăng nhập — đổi chéo khác bộ phận vẫn được nếu HR/Manager sắp sau, nhưng
    /// danh sách gợi ý chỉ trong bộ phận để tránh chọn nhầm người lạ.
    /// </summary>
    public IReadOnlyList<SelectListItem> CaDongNghiep { get; set; } = [];

    public bool CoTheGui => CaCuaToi.Count > 0 && CaDongNghiep.Count > 0;
}

// ═══════════════════════════════════════════════════════════════════════════
//  PRD 4.5 — GIẢI TRÌNH CHẤM CÔNG
// ═══════════════════════════════════════════════════════════════════════════

public class GiaiTrinhIndexViewModel
{
    public string? TrangThai { get; set; }
    public string? Keyword { get; set; }

    public IReadOnlyList<SelectListItem> TrangThaiOptions { get; set; } = [];

    public IReadOnlyList<GiaiTrinhItemViewModel> Items { get; set; } = [];

    public int TongGiaiTrinh { get; set; }
    public int ChoQuanLy { get; set; }
    public int ChoHr { get; set; }
    public int DaDuyet { get; set; }
    public int TuChoi { get; set; }

    /// <summary>Người đang đăng nhập xác minh được ở cấp Quản lý.</summary>
    public bool CoQuyenQuanLy { get; set; }

    /// <summary>Người đang đăng nhập xác nhận được ở cấp HR.</summary>
    public bool CoQuyenHr { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;
    public int TotalFilteredItems { get; set; }
}

public class GiaiTrinhItemViewModel
{
    public int MaGiaiTrinh { get; set; }
    public int MaPhanCa { get; set; }
    public int MaNhanVien { get; set; }

    public string TenNhanVien { get; set; } = string.Empty;
    public string MaNhanVienFormat => $"NV-{MaNhanVien}";
    public string TenPhongBan { get; set; } = string.Empty;

    public DateOnly NgayLamViec { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public string GioCa { get; set; } = string.Empty;

    public DateTimeOffset? GioDeXuatVao { get; set; }
    public DateTimeOffset? GioDeXuatRa { get; set; }

    public string? GioVaoHienTai { get; set; }
    public string? GioRaHienTai { get; set; }

    public string LyDo { get; set; } = string.Empty;
    public string? MinhChung { get; set; }

    public string TrangThai { get; set; } = "CHO_QUAN_LY";

    public string? TenQuanLyXacMinh { get; set; }
    public DateTimeOffset? NgayQuanLyXacMinh { get; set; }
    public string? YKienQuanLy { get; set; }

    public string? TenHrXacNhan { get; set; }
    public DateTimeOffset? NgayHrXacNhan { get; set; }
    public string? YKienHr { get; set; }

    public DateTimeOffset NgayGui { get; set; }

    public string GioDeXuatVaoDisplay => FormatGio(GioDeXuatVao);
    public string GioDeXuatRaDisplay => FormatGio(GioDeXuatRa);

    private static string FormatGio(DateTimeOffset? value) =>
        value?.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy") ?? "—";

    public string TrangThaiNhan => TrangThai switch
    {
        "CHO_QUAN_LY" => "Chờ Quản lý xác minh",
        "CHO_HR" => "Chờ HR xác nhận",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        _ => TrangThai
    };

    /// <summary>
    /// Cấp 1 và cấp 2 dùng hai sắc vàng khác nhau để nhìn là biết hồ sơ đang kẹt
    /// ở ai, không phải mở ra đọc.
    /// </summary>
    public string TrangThaiCss => TrangThai switch
    {
        "CHO_QUAN_LY" => "bg-warning text-dark",
        "CHO_HR" => "bg-info text-dark",
        "DA_DUYET" => "bg-success",
        _ => "bg-danger"
    };

    /// <summary>Cấp 1 còn chờ Quản lý xác minh.</summary>
    public bool ChoQuanLy => TrangThai == "CHO_QUAN_LY";

    /// <summary>Cấp 2 còn chờ HR xác nhận.</summary>
    public bool ChoHr => TrangThai == "CHO_HR";
}

/// <summary>
/// Form lập giải trình chấm công (PRD 4.5).
///
/// <para>
/// Hai mốc giờ dùng <see cref="DateTime"/> chứ không phải
/// <see cref="DateTimeOffset"/> vì ô <c>datetime-local</c> của trình duyệt không
/// kèm múi giờ. Bind thẳng vào <see cref="DateTimeOffset"/> sẽ để múi giờ của
/// <b>máy chủ</b> quyết định: trên máy chủ chạy UTC, nhập 08:00 sẽ thành 15:00
/// giờ Việt Nam. Controller tự gán độ lệch +07:00 cho hai mốc này.
/// </para>
/// </summary>
public class GiaiTrinhCreateViewModel
{
    public int MaPhanCa { get; set; }

    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;

    /// <summary>Mô tả ca đang chọn, chỉ để hiển thị lại sau khi có lỗi xác thực.</summary>
    public string MoTaCa { get; set; } = string.Empty;

    public string GioVaoHienTai { get; set; } = "—";
    public string GioRaHienTai { get; set; } = "—";

    public DateTime? GioDeXuatVao { get; set; }
    public DateTime? GioDeXuatRa { get; set; }

    public string LyDo { get; set; } = string.Empty;
    public string? MinhChung { get; set; }

    /// <summary>Các ca đã kết thúc trong thời hạn còn nhận giải trình.</summary>
    public IReadOnlyList<SelectListItem> CaOptions { get; set; } = [];

    public bool CoCaDeChon => CaOptions.Count > 0;
}

// ═══════════════════════════════════════════════════════════════════════════
//  PRD 4.4 — BẢNG TỔNG HỢP CÔNG THÁNG
// ═══════════════════════════════════════════════════════════════════════════

public class CongThangViewModel
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public int? PhongBanId { get; set; }

    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }

    public IReadOnlyList<SelectListItem> PhongBanOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ThangOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> NamOptions { get; set; } = [];

    /// <summary>
    /// Quản lý bộ phận bị khóa vào phòng ban của mình nên không được thấy ô lọc;
    /// cờ này do controller tính từ phạm vi dữ liệu, không đoán theo tên vai trò.
    /// </summary>
    public bool CoQuyenLocPhongBan { get; set; }

    public MonthlyAttendanceReportDto Report { get; set; } =
        new(DateOnly.MinValue, DateOnly.MinValue, []);

    public bool IsEmpty => Report.Rows.Count == 0;

    public string TieuDeKy => $"Tháng {Thang:00}/{Nam}";
}
