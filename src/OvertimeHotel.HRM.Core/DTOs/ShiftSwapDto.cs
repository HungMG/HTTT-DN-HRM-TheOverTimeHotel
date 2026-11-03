namespace OvertimeHotel.HRM.Core.DTOs;

public class EligibleMyShiftDto
{
    public int MaPhanCa { get; set; }
    public DateOnly NgayLamViec { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public string GioBatDau { get; set; } = string.Empty;
    public string GioKetThuc { get; set; } = string.Empty;
    public DateTimeOffset BatDauDuKien { get; set; }
    public DateTimeOffset KetThucDuKien { get; set; }

    public string DisplayText => $"{TenCa} ({GioBatDau} - {GioKetThuc}) · {NgayLamViec:dd/MM/yyyy}";
}

public class EligiblePartnerShiftDto
{
    public List<int> CompatibleSenderShiftIds { get; set; } = [];
    public int MaPhanCa { get; set; }
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public DateOnly NgayLamViec { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public string GioBatDau { get; set; } = string.Empty;
    public string GioKetThuc { get; set; } = string.Empty;
    public DateTimeOffset BatDauDuKien { get; set; }
    public DateTimeOffset KetThucDuKien { get; set; }

    public string DisplayText => $"{TenNhanVien} · {TenCa} ({GioBatDau} - {GioKetThuc}) · {NgayLamViec:dd/MM/yyyy}";
}

public class ShiftSwapItemDto
{
    public int MaYeuCau { get; set; }
    public int MaPhanCaGui { get; set; }
    public string TenCaGui { get; set; } = string.Empty;
    public string NgayCaGui { get; set; } = string.Empty;
    public string GioCaGui { get; set; } = string.Empty;

    public int MaNhanVienGui { get; set; }
    public string TenNguoiGui { get; set; } = string.Empty;

    public int MaPhanCaNhan { get; set; }
    public string TenCaNhan { get; set; } = string.Empty;
    public string NgayCaNhan { get; set; } = string.Empty;
    public string GioCaNhan { get; set; } = string.Empty;

    public int MaNhanVienNhan { get; set; }
    public string TenNguoiNhan { get; set; } = string.Empty;

    public string LyDo { get; set; } = string.Empty;
    public string TrangThai { get; set; } = "CHO_NGUOI_NHAN";

    public string? BenTuChoi { get; set; }
    public string? PhanHoiNguoiNhan { get; set; }
    public DateTimeOffset? NgayNguoiNhanPhanHoi { get; set; }

    public int? MaNguoiDuyet { get; set; }
    public string? TenNguoiDuyet { get; set; }
    public DateTimeOffset NgayGui { get; set; }
    public DateTimeOffset? NgayDuyet { get; set; }
    public string? PhanHoiDuyet { get; set; }

    // Helpers cho hiển thị Mobile UI
    public bool IsSender { get; set; }
    public bool IsReceiver { get; set; }

    public bool CanWithdraw => IsSender && (TrangThai == "CHO_NGUOI_NHAN" || TrangThai == "CHO_DUYET");
    public bool CanRespond => IsReceiver && TrangThai == "CHO_NGUOI_NHAN";

    public string StatusBadgeText => TrangThai switch
    {
        "CHO_NGUOI_NHAN" => IsReceiver ? "Cần bạn xác nhận" : "Chờ bạn đổi xác nhận",
        "CHO_DUYET" => "Chờ quản lý duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => BenTuChoi == "NGUOI_NHAN" ? "Bạn đổi từ chối" : "Quản lý từ chối",
        "DA_HUY" => "Đã huỷ",
        _ => TrangThai
    };

    public string StatusBadgeColor => TrangThai switch
    {
        "CHO_NGUOI_NHAN" => "#0284C7", // Cyan / Blue
        "CHO_DUYET" => "#F59E0B",      // Amber
        "DA_DUYET" => "#10B981",       // Emerald
        "TU_CHOI" => "#EF4444",        // Red
        "DA_HUY" => "#6B7280",         // Gray
        _ => "#9CA3AF"
    };

    public string StatusBadgeTextColor => "#FFFFFF";

    public bool HasPhanHoiNguoiNhan => !string.IsNullOrWhiteSpace(PhanHoiNguoiNhan);
    public bool HasPhanHoiDuyet => !string.IsNullOrWhiteSpace(PhanHoiDuyet);
}

public class CreateShiftSwapRequest
{
    public int MaPhanCaGui { get; set; }
    public int MaPhanCaNhan { get; set; }
    public string LyDo { get; set; } = string.Empty;
}

public class RespondShiftSwapRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("accept")]
    public bool DongY { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? LyDo { get; set; }
}

public class ShiftSwapOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? MaYeuCau { get; set; }
}
