using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class PhanCaIndexViewModel
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public int? SelectedPhongBanId { get; set; }
    public int? SelectedCaId { get; set; }

    public List<PhongBan> DanhSachPhongBan { get; set; } = new();
    public List<CaLamViec> DanhSachCa { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();

    public List<PhanCaMatrixRow> Rows { get; set; } = new();
    public List<PhanCa> DanhSachPhanCa { get; set; } = new();
    public List<PhanCa> CaTrucHomNay { get; set; } = new();

    // Thống kê Bento
    public int TongSoCa { get; set; }
    public int SoCaSang { get; set; }
    public int SoCaChieu { get; set; }
    public int SoCaDem { get; set; }
    public int SoNhanVienDuocPhan { get; set; }
    public int TongNhanVien { get; set; }
}

public class PhanCaMatrixRow
{
    public int MaNhanVien { get; set; }
    public int MaPhongBan { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string TenPhongBan { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#1e3a8a";

    // 7 ngày trong tuần: Thứ 2 -> Chủ nhật
    public List<PhanCaDayCell> Days { get; set; } = new();
}

public class PhanCaDayCell
{
    public DateOnly Date { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public bool IsToday { get; set; }
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
}

public class CreatePhanCaInputModel
{
    public int MaNhanVien { get; set; }
    public int MaCa { get; set; }
    public DateOnly NgayLamViec { get; set; }
    public string TrangThai { get; set; } = "DA_PHAN";
    public string? ReturnUrl { get; set; }
}
