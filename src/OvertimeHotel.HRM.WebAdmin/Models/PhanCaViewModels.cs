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

