using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class PhanCaIndexViewModel
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public int? SelectedPhongBanId { get; set; }
    public int? SelectedCaId { get; set; }

    // Thông tin hiển thị Cuốn Lịch (Calendar Notebook)
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly SelectedDate { get; set; }
    public List<CalendarDaySummary> CalendarDays { get; set; } = new();
    public AgendaDay SelectedAgenda { get; set; } = new();

    public List<PhongBan> DanhSachPhongBan { get; set; } = new();
    public List<CaLamViec> DanhSachCa { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();

    public List<PhanCaMatrixRow> Rows { get; set; } = new();
    public List<PhanCa> DanhSachPhanCa { get; set; } = new();
    public List<PhanCa> AllGridPhanCa { get; set; } = new();
    public List<PhanCa> CaTrucHomNay { get; set; } = new();

    // Thống kê Bento
    public int TongSoCa { get; set; }
    public int SoCaSang { get; set; }
    public int SoCaChieu { get; set; }
    public int SoCaDem { get; set; }
    public int SoNhanVienDuocPhan { get; set; }
    public int TongNhanVien { get; set; }

    /// <summary>Số ca trong ngày đang chọn chưa đủ định biên tối thiểu (PRD 3.5).</summary>
    public int TongCaThieuDinhBien { get; set; }

    /// <summary>Dữ liệu cho popup cấu hình định biên (PRD 3.5).</summary>
    public DinhBienModalViewModel DinhBien { get; set; } = new();
}

public class CalendarDaySummary
{
    public DateOnly Date { get; set; }
    public int Sang { get; set; }
    public int Chieu { get; set; }
    public int Dem { get; set; }
    public int Tong => Sang + Chieu + Dem;
    public bool IsOtherMonth { get; set; }
    public bool IsToday { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsPast { get; set; }
    public bool IsSelected { get; set; }
    public bool HasContractIssue { get; set; }
    /// <summary>Số lượt phân ca trong ngày thuộc nhân sự đã hết hạn / chưa tới hiệu lực HĐLĐ.</summary>
    public int ExpiredContractCount { get; set; }
}

public class AgendaShiftGroup
{
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public string Gio { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = string.Empty;
    public List<AgendaEmployeeItem> Employees { get; set; } = new();

    /// <summary>
    /// Kết quả đối chiếu định biên tối thiểu theo chức vụ cho ca này (PRD 3.5).
    /// Rỗng nghĩa là ca chưa được khai báo định biên — không kết luận thiếu người.
    /// </summary>
    public List<DinhBienCheckItem> DinhBien { get; set; } = new();

    /// <summary>Ca có ít nhất một chức vụ chưa đủ số người tối thiểu.</summary>
    public bool ThieuDinhBien => DinhBien.Any(d => d.ConThieu > 0);

    /// <summary>Tổng số người còn thiếu trên mọi chức vụ của ca.</summary>
    public int TongConThieu => DinhBien.Sum(d => d.ConThieu);
}

/// <summary>
/// Một dòng đối chiếu định biên: chức vụ này trong ca này cần bao nhiêu người,
/// hiện có bao nhiêu, còn thiếu bao nhiêu.
/// </summary>
public class DinhBienCheckItem
{
    public int MaChucVu { get; set; }
    public string TenChucVu { get; set; } = string.Empty;

    /// <summary>
    /// Bộ phận của dòng định biên; <c>null</c> là mức dùng chung. Cùng một chức danh
    /// trải nhiều bộ phận sinh nhiều mục đối chiếu — "Thiếu định biên: Giám Sát Ca
    /// 0/1 (Bếp)" phải chỉ đúng nơi thiếu, không gộp chung toàn khách sạn.
    /// </summary>
    public int? MaPhongBan { get; set; }

    /// <summary>Tên bộ phận hiển thị; rỗng khi là mức dùng chung.</summary>
    public string TenPhongBan { get; set; } = string.Empty;

    public int SoNguoiToiThieu { get; set; }
    public int SoNguoiHienCo { get; set; }
    public int ConThieu => Math.Max(0, SoNguoiToiThieu - SoNguoiHienCo);

    /// <summary>
    /// Con số này lấy từ bảng đè theo ngày thay vì mức nền chung.
    /// </summary>
    /// <remarks>
    /// Người xếp ca cần phân biệt được "thiếu vì mức chung" với "thiếu vì hôm nay
    /// đặt riêng cao hơn" — sửa nhầm sang mức chung sẽ làm cả tháng bị đổi theo.
    /// </remarks>
    public bool LaMucRiengNgay { get; set; }
}

public class AgendaEmployeeItem
{
    public int MaPhanCa { get; set; }
    public int MaNhanVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public string TenPhongBan { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#1e3a8a";
    public bool HasActiveContract { get; set; } = true;
    public string? DisabledReason { get; set; }
}

public class AgendaDay
{
    public DateOnly Date { get; set; }
    public List<AgendaShiftGroup> Shifts { get; set; } = new();
    public int TongNhanSu => Shifts.Sum(s => s.Employees.Count);
}

public class PhanCaMatrixRow
{
    public int MaNhanVien { get; set; }
    public int MaPhongBan { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string TenPhongBan { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#1e3a8a";
    public bool HasActiveContract { get; set; } = true;

    // 7 ngày trong tuần: Thứ 2 -> Chủ nhật
    public List<PhanCaDayCell> Days { get; set; } = new();
}

public class PhanCaDayCell
{
    public DateOnly Date { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool CanAssign { get; set; } = true;
    public string? DisabledReason { get; set; }
    public List<PhanCaItemDto> Shifts { get; set; } = new();
}

public class PhanCaItemDto
{
    public int MaPhanCa { get; set; }
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public TimeOnly GioBatDau { get; set; }
    public TimeOnly GioKetThuc { get; set; }
    public bool QuaDem { get; set; }
    public string TrangThai { get; set; } = "DA_PHAN";
    public bool HasAttendance { get; set; }
    public bool IsCheckedIn { get; set; }
}

public class CreatePhanCaInputModel
{
    public int MaNhanVien { get; set; }
    public List<int> DanhSachMaNhanVien { get; set; } = new();
    public int MaCa { get; set; }
    public DateOnly NgayLamViec { get; set; }
    public List<DateOnly> DanhSachNgayLamViec { get; set; } = new();
    public string TrangThai { get; set; } = "DA_PHAN";
    public string? ReturnUrl { get; set; }
}

public class QuickAssignAjaxModel
{
    public int MaNhanVien { get; set; }
    public string NgayLamViec { get; set; } = string.Empty;
    public int MaCa { get; set; }
}

/// <summary>
/// Một dòng của lưới định biên trong popup cấu hình: một chức vụ, kèm số người
/// tối thiểu theo từng ca.
///
/// <para>
/// Dùng hai từ điển <c>maCa → số người</c> thay vì ma trận hai chiều vì số ca
/// thay đổi được (danh mục mẫu ca cho phép thêm ca mới), còn Razor chỉ cần tra
/// theo mã ca là đủ. Ca không có mặt trong từ điển nghĩa là ca đó chưa đặt định
/// biên cho chức vụ này.
/// </para>
/// </summary>
public class DinhBienMatrixRow
{
    public int MaChucVu { get; set; }
    public string TenChucVu { get; set; } = string.Empty;

    /// <summary>
    /// Bộ phận của dòng này; <c>null</c> là dòng dùng chung mọi bộ phận. Chức danh
    /// trải nhiều bộ phận (Trưởng Bộ Phận, Giám Sát Ca) sinh ra một dòng cho mỗi bộ
    /// phận, nhờ đó mới đặt được mức riêng cho từng nơi.
    /// </summary>
    public int? MaPhongBan { get; set; }

    public string TenPhongBan { get; set; } = string.Empty;

    /// <summary>Mức nền lấy từ bảng <c>dinh_bien_ca</c>.</summary>
    public Dictionary<int, int> MucNenTheoCa { get; set; } = new();

    /// <summary>Mức riêng của ngày đang xem, lấy từ bảng <c>dinh_bien_ngay</c>.</summary>
    public Dictionary<int, int> MucNgayTheoCa { get; set; } = new();

    /// <summary>Số người hiện có theo ca, để quản lý thấy ngay đang thiếu ở đâu.</summary>
    public Dictionary<int, int> HienCoTheoCa { get; set; } = new();
}

/// <summary>
/// Dữ liệu cho popup "Cấu hình định biên ca" (PRD 3.5).
/// </summary>
public class DinhBienModalViewModel
{
    /// <summary>Ngày đang được xem trên lịch; popup mở ở chế độ riêng ngày này.</summary>
    public DateOnly Ngay { get; set; }

    public List<CaLamViec> DanhSachCa { get; set; } = new();

    /// <summary>
    /// Danh mục bộ phận, để dropdown "Thêm dòng theo bộ phận" có dữ liệu chọn.
    /// </summary>
    public List<PhongBan> DanhSachPhongBan { get; set; } = new();

    public List<DinhBienMatrixRow> Rows { get; set; } = new();

    /// <summary>Tuần đang xem, để sau khi lưu quay lại đúng chỗ.</summary>
    public string ReturnWeek { get; set; } = string.Empty;

    /// <summary>Chỉ người có quyền xếp ca mới thấy nút lưu; còn lại chỉ xem.</summary>
    public bool CoQuyenSua { get; set; }

    /// <summary>Số ô đang bị đè riêng cho ngày này.</summary>
    public int SoOCoDinhBienNgay => Rows.Sum(r => r.MucNgayTheoCa.Count);
}

/// <summary>
/// Payload lưu lưới định biên. <see cref="Ngay"/> rỗng nghĩa là lưu mức nền;
/// có ngày nghĩa là lưu mức riêng cho đúng ngày đó.
/// </summary>
public class DinhBienSaveInput
{
    public List<DinhBienCellInput> Cells { get; set; } = new();
    public string? Ngay { get; set; }
    public string? ReturnWeek { get; set; }
}

public class DinhBienCellInput
{
    public int MaCa { get; set; }
    public int MaChucVu { get; set; }

    /// <summary>
    /// Bộ phận của ô; <c>null</c> là ô dùng chung mọi bộ phận. Phải có trong payload
    /// vì cùng một chức danh trải nhiều bộ phận sinh nhiều ô — thiếu nó thì mọi ô
    /// ghi đè lên nhau ở dòng "dùng chung".
    /// </summary>
    public int? MaPhongBan { get; set; }

    /// <summary>0 nghĩa là xoá dòng (không đặt định biên), không phải ghi số 0.</summary>
    public int SoNguoi { get; set; }
}

