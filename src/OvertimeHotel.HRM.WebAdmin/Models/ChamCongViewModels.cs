using Microsoft.AspNetCore.Mvc.Rendering;

namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// ViewModel chính cho màn hình Nhật ký Chấm công & Quẹt thẻ Check-in / Check-out 24/7
/// </summary>
public class ChamCongIndexViewModel
{
    // Khoảng ngày lọc
    public DateOnly? NgayChon { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public string RangeType { get; set; } = "today"; // today, yesterday, week, month, custom

    // Bộ lọc khác
    public int? PhongBanId { get; set; }
    public int? CaId { get; set; }
    public string? TrangThai { get; set; }
    public string? Keyword { get; set; }

    // Dropdowns
    public IReadOnlyList<SelectListItem> PhongBanOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> CaOptions { get; set; } = [];

    // Chỉ số KPI Thống kê
    public int TongSoCaPhan { get; set; }
    public int SoDaCheckIn { get; set; }
    public int SoDangLamViec { get; set; }
    public int SoDungGio { get; set; }
    public int SoDiTre { get; set; }
    public int SoVeSom { get; set; }
    public int SoVangMat { get; set; }
    public decimal TongGioLamThucTe { get; set; }
    public decimal TongGioOt { get; set; }
    public decimal TongGioSuKien { get; set; }

    // Phân trang
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalPages { get; set; } = 1;
    public int TotalFilteredItems { get; set; }

    // Danh sách bản ghi chấm công
    public IReadOnlyList<ChamCongItemViewModel> Items { get; set; } = [];
}

/// <summary>
/// Chi tiết một bản ghi chấm công ca trực của nhân viên
/// </summary>
public class ChamCongItemViewModel
{
    public int MaPhanCa { get; set; }
    public int? MaChamCong { get; set; }
    public int MaNhanVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string MaNhanVienFormat => $"NV-{MaNhanVien}";
    public string TenPhongBan { get; set; } = string.Empty;
    public string TenChucVu { get; set; } = string.Empty;
    public string AvatarText => !string.IsNullOrEmpty(HoTen) ? HoTen.Substring(0, 1).ToUpperInvariant() : "N";

    public DateOnly NgayLamViec { get; set; }
    public int MaCa { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public TimeOnly GioBatDauCa { get; set; }
    public TimeOnly GioKetThucCa { get; set; }
    public bool QuaDem { get; set; }
    public DateTimeOffset BatDauDuKien { get; set; }
    public DateTimeOffset KetThucDuKien { get; set; }

    public DateTimeOffset? GioCheckIn { get; set; }
    public DateTimeOffset? GioCheckOut { get; set; }
    public int PhutDiTre { get; set; }
    public int PhutVeSom { get; set; }
    public decimal GioTangCaOt { get; set; }
    public string? PhuongThucCheckIn { get; set; }
    public string? ToaDoCheckIn { get; set; }
    public string? ThietBiCheckIn { get; set; }

    public string TrangThai { get; set; } = "CHUA_CHECKIN";

    public bool DaCheckIn => GioCheckIn.HasValue;
    public bool DaCheckOut => GioCheckOut.HasValue;
    public bool DangLamViec => DaCheckIn && !DaCheckOut;

    public string GioCheckInDisplay => GioCheckIn.HasValue
        ? GioCheckIn.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm")
        : "—";

    public string GioCheckOutDisplay => GioCheckOut.HasValue
        ? GioCheckOut.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm")
        : (DaCheckIn ? "Đang trực" : "—");

    public string GioBatDauCaDisplay => GioBatDauCa.ToString("HH:mm");
    public string GioKetThucCaDisplay => GioKetThucCa.ToString("HH:mm");

    public double? SoGioLamThucTe
    {
        get
        {
            if (GioCheckIn.HasValue && GioCheckOut.HasValue)
            {
                var diff = GioCheckOut.Value - GioCheckIn.Value;
                return Math.Max(0, Math.Round(diff.TotalHours, 2));
            }
            return null;
        }
    }

    public string ThoiGianLamDisplay
    {
        get
        {
            if (GioCheckIn.HasValue && GioCheckOut.HasValue)
            {
                var diff = GioCheckOut.Value - GioCheckIn.Value;
                var totalMinutes = (int)Math.Max(0, diff.TotalMinutes);
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;
                return $"{hours}h {minutes:D2}p";
            }
            if (GioCheckIn.HasValue)
            {
                var diff = DateTimeOffset.UtcNow - GioCheckIn.Value;
                var totalMinutes = (int)Math.Max(0, diff.TotalMinutes);
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;
                return $"{hours}h {minutes:D2}p (Đang tính)";
            }
            return "0h 00p";
        }
    }

    // Phân công sự kiện theo giờ (Hourly Event Work)
    public bool CoSuKien { get; set; }
    public string? TenSuKien { get; set; }
    public string? VaiTroSuKien { get; set; }
    public decimal? SoGioSuKienThucTe { get; set; }
}
