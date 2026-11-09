namespace OvertimeHotel.HRM.Core.DTOs;

/// <summary>
/// Ca làm việc trong 45 ngày qua có thể nộp giải trình chấm công (quên quẹt, sai giờ).
/// </summary>
public record EligibleShiftDto(
    int MaPhanCa,
    DateOnly NgayLamViec,
    int MaCa,
    string TenCa,
    TimeOnly GioBatDau,
    TimeOnly GioKetThuc,
    DateTimeOffset? GioVaoThucTe,
    DateTimeOffset? GioRaThucTe,
    string? TrangThaiChamCong
)
{
    public string GioCaText => $"{GioBatDau:HH:mm} - {GioKetThuc:HH:mm}";
    public string GioVaoThucTeDisplay => GioVaoThucTe.HasValue ? GioVaoThucTe.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "chưa quẹt";
    public string GioRaThucTeDisplay => GioRaThucTe.HasValue ? GioRaThucTe.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "chưa quẹt";
    public bool HasGioVaoThucTe => GioVaoThucTe.HasValue;
    public bool HasGioRaThucTe => GioRaThucTe.HasValue;
}

/// <summary>
/// Dữ liệu một bản ghi giải trình chấm công trả về cho Mobile App và WebAdmin.
/// </summary>
public record AttendanceExplanationItemDto(
    int MaGiaiTrinh,
    int MaPhanCa,
    int MaNhanVien,
    string HoTenNhanVien,
    string? TenPhongBan,
    DateOnly NgayLamViec,
    string TenCa,
    DateTimeOffset? GioDeXuatVao,
    DateTimeOffset? GioDeXuatRa,
    DateTimeOffset? GioVaoThucTe,
    DateTimeOffset? GioRaThucTe,
    string LyDo,
    string? MinhChung,
    string TrangThai,
    string TrangThaiLabel,
    DateTimeOffset NgayGui,
    int? MaQuanLyXacMinh,
    string? HoTenQuanLy,
    DateTimeOffset? NgayQuanLyXacMinh,
    string? YKienQuanLy,
    int? MaHrXacNhan,
    string? HoTenHr,
    DateTimeOffset? NgayHrXacNhan,
    string? YKienHr,
    bool CanWithdraw
)
{
    public bool HasYKienQuanLy => !string.IsNullOrWhiteSpace(YKienQuanLy);
    public bool HasYKienHr => !string.IsNullOrWhiteSpace(YKienHr);

    public bool IsChoQuanLy => TrangThai == "CHO_QUAN_LY";
    public bool IsChoHr => TrangThai == "CHO_HR";
    public bool IsDaDuyet => TrangThai == "DA_DUYET";
    public bool IsTuChoi => TrangThai == "TU_CHOI";
    public bool IsDaHuy => TrangThai == "DA_HUY";

    public bool IsRejectedByManager => TrangThai == "TU_CHOI" && string.IsNullOrWhiteSpace(HoTenHr);
    public bool IsRejectedByHr => TrangThai == "TU_CHOI" && !string.IsNullOrWhiteSpace(HoTenHr);
    public bool HasManagerVerified => IsChoHr || IsDaDuyet || (!string.IsNullOrWhiteSpace(HoTenQuanLy) && !IsRejectedByManager);
    public bool HasMinhChung => !string.IsNullOrWhiteSpace(MinhChung);

    public string GioVaoThucTeDisplay => GioVaoThucTe.HasValue ? GioVaoThucTe.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "chưa quẹt";
    public string GioRaThucTeDisplay => GioRaThucTe.HasValue ? GioRaThucTe.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "chưa quẹt";
    public string GioDeXuatVaoDisplay => GioDeXuatVao.HasValue ? GioDeXuatVao.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "—";
    public string GioDeXuatRaDisplay => GioDeXuatRa.HasValue ? GioDeXuatRa.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm") : "—";
    public bool HasGioDeXuatVao => GioDeXuatVao.HasValue;
    public bool HasGioDeXuatRa => GioDeXuatRa.HasValue;

    public string NgayGuiDisplay => NgayGui.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm · dd/MM/yyyy");
    public string NgayQuanLyDisplay => NgayQuanLyXacMinh.HasValue ? NgayQuanLyXacMinh.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm · dd/MM") : string.Empty;
    public string NgayHrDisplay => NgayHrXacNhan.HasValue ? NgayHrXacNhan.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm · dd/MM") : string.Empty;

    public string StatusBadgeBgColor => TrangThai switch
    {
        "CHO_QUAN_LY" => "#FFFBEB",
        "CHO_HR" => "#F0F9FF",
        "DA_DUYET" => "#ECFDF5",
        "TU_CHOI" => "#FEF2F2",
        _ => "#F1F5F9"
    };

    public string StatusBadgeTextColor => TrangThai switch
    {
        "CHO_QUAN_LY" => "#B45309",
        "CHO_HR" => "#0284C7",
        "DA_DUYET" => "#059669",
        "TU_CHOI" => "#DC2626",
        _ => "#64748B"
    };

    public string StatusBadgeBorderColor => TrangThai switch
    {
        "CHO_QUAN_LY" => "#FDE68A",
        "CHO_HR" => "#BAE6FD",
        "DA_DUYET" => "#A7F3D0",
        "TU_CHOI" => "#FECACA",
        _ => "#E2E8F0"
    };

    public string StatusBadgeText => TrangThai switch
    {
        "CHO_QUAN_LY" => "Chờ QL xác minh (C1)",
        "CHO_HR" => "Chờ HR phê duyệt (C2)",
        "DA_DUYET" => "HR đã duyệt & chốt công",
        "TU_CHOI" => "Từ chối",
        "DA_HUY" => "Đã rút",
        _ => TrangThaiLabel
    };

    // --- CẤP 1: QUẢN LÝ BỘ PHẬN STEPPER HELPERS ---
    public string ManagerStepIcon => IsChoQuanLy ? "⏳" : (IsRejectedByManager ? "✕" : (HasManagerVerified ? "✓" : "•"));
    public string ManagerStepIconBg => IsChoQuanLy ? "#FEF3C7" : (IsRejectedByManager ? "#FEE2E2" : (HasManagerVerified ? "#DCFCE7" : "#F1F5F9"));
    public string ManagerStepIconColor => IsChoQuanLy ? "#D97706" : (IsRejectedByManager ? "#DC2626" : (HasManagerVerified ? "#16A34A" : "#94A3B8"));
    public string ManagerStepTitle => IsChoQuanLy
        ? "Cấp 1: Chờ Quản lý xác minh"
        : (IsRejectedByManager
            ? "Cấp 1: Quản lý từ chối"
            : (HasManagerVerified
                ? "Cấp 1: Quản lý đã xác minh"
                : "Cấp 1: Quản lý bộ phận"));
    public string ManagerStepSubtitle => !string.IsNullOrWhiteSpace(HoTenQuanLy)
        ? $"{HoTenQuanLy} {(NgayQuanLyXacMinh.HasValue ? $"· {NgayQuanLyDisplay}" : "")}"
        : (IsChoQuanLy
            ? "Kiểm tra thực tế hiện trường ca / sự kiện"
            : "Chờ tiếp nhận xử lý");

    // --- CẤP 2: PHÒNG NHÂN SỰ (HR) STEPPER HELPERS ---
    public string HrStepIcon => IsChoQuanLy ? "○" : (IsChoHr ? "⏳" : (IsDaDuyet ? "★" : (IsRejectedByHr ? "✕" : "•")));
    public string HrStepIconBg => IsChoQuanLy ? "#F1F5F9" : (IsChoHr ? "#E0F2FE" : (IsDaDuyet ? "#EFF6FF" : (IsRejectedByHr ? "#FEE2E2" : "#F1F5F9")));
    public string HrStepIconColor => IsChoQuanLy ? "#94A3B8" : (IsChoHr ? "#0284C7" : (IsDaDuyet ? "#1E3A8A" : (IsRejectedByHr ? "#DC2626" : "#94A3B8")));
    public string HrStepTitle => IsChoQuanLy
        ? "Cấp 2: Chờ Quản lý xác minh xong"
        : (IsChoHr
            ? "Cấp 2: Chờ HR phê duyệt & chốt công"
            : (IsDaDuyet
                ? "Cấp 2: HR đã duyệt & cập nhật công"
                : (IsRejectedByHr
                    ? "Cấp 2: HR từ chối hồ sơ"
                    : "Cấp 2: Phòng Nhân Sự (HR)")));
    public string HrStepSubtitle => !string.IsNullOrWhiteSpace(HoTenHr)
        ? $"{HoTenHr} {(NgayHrXacNhan.HasValue ? $"· {NgayHrDisplay}" : "")}"
        : (IsChoHr
            ? "Thẩm định quy chế & đối soát bảng công"
            : (IsChoQuanLy
                ? "Thẩm định sau khi Cấp 1 xác minh hiện trường"
                : "Chờ tiếp nhận hồ sơ"));
}

/// <summary>
/// Yêu cầu tạo giải trình chấm công mới từ Mobile App.
/// </summary>
public record CreateAttendanceExplanationRequest(
    int MaPhanCa,
    DateTimeOffset? GioDeXuatVao,
    DateTimeOffset? GioDeXuatRa,
    string LyDo,
    string? MinhChung
);

/// <summary>
/// Kết quả thao tác giải trình (nộp, rút, duyệt, từ chối).
/// </summary>
public record AttendanceExplanationOperationResult(
    bool Success,
    string Code,
    string Message,
    int? MaGiaiTrinh = null
);
