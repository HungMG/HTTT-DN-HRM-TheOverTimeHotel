namespace OvertimeHotel.HRM.Core.DTOs;

/// <summary>
/// Một lượt phân ca đã quy về đủ dữ liệu để tổng hợp công tháng (PRD 4.4).
///
/// <para>
/// Tách khỏi entity <c>PhanCa</c> để tầng tính toán không phụ thuộc EF: nơi gọi
/// chiếu thẳng từ truy vấn ra đúng các cột này, không Include navigation rồi mới
/// đọc — vừa nhẹ vừa giữ <c>OvertimeHotel.HRM.Core</c> thuần logic như quy ước.
/// </para>
/// </summary>
public record MonthlyAttendanceShiftInput(
    int EmployeeId,
    DateOnly NgayLamViec,
    DateTimeOffset BatDauDuKien,
    DateTimeOffset KetThucDuKien,
    string? PhanCaTrangThai,
    DateTimeOffset? GioCheckIn,
    DateTimeOffset? GioCheckOut,
    int PhutDiTre,
    int PhutVeSom,
    decimal GioTangCaOt,
    string? ChamCongTrangThai);

/// <summary>Tổng hợp công của một nhân viên trong kỳ (PRD 4.4).</summary>
public record MonthlyAttendanceRowDto(
    int EmployeeId,
    string EmployeeName,
    string? DepartmentName,
    string? PositionName,
    int TongCaPhanCong,
    int SoCaDaKetThuc,
    int SoCaCoMat,
    int SoCaDungGio,
    int SoCaDiTre,
    int SoCaVeSom,
    int SoCaTreVaSom,
    int SoCaVangMat,
    int SoCaChuaCheckIn,
    int TongPhutTre,
    int TongPhutVeSom,
    decimal TongGioOt,
    decimal TongGioLamThucTe)
{
    /// <summary>
    /// Tỷ lệ chuyên cần (%), làm tròn hai chữ số.
    ///
    /// <para>
    /// Tử số là số ca đúng giờ, mẫu số là <b>số ca đã kết thúc</b> chứ không phải
    /// toàn bộ ca được phân trong tháng. PRD 4.4 ghi "tổng số ca phân công", nhưng
    /// áp nguyên văn giữa tháng sẽ khiến mọi ca của những ngày còn lại bị tính là
    /// không đúng giờ — nhân viên làm đúng 100% vẫn hiện 40%. Ca chưa tới giờ
    /// không chứng minh được điều gì nên không thuộc mẫu số.
    /// </para>
    /// </summary>
    public decimal TyLeChuyenCan => SoCaDaKetThuc == 0
        ? 0m
        : Math.Round(SoCaDungGio * 100m / SoCaDaKetThuc, 2);

    /// <summary>Số ca vắng trên số ca đã kết thúc (%), dùng cho bảng xếp hạng kỷ luật.</summary>
    public decimal TyLeVang => SoCaDaKetThuc == 0
        ? 0m
        : Math.Round(SoCaVangMat * 100m / SoCaDaKetThuc, 2);

    public string MaNhanVienFormat => $"NV-{EmployeeId}";
}

/// <summary>Toàn bộ bảng tổng hợp công tháng của một phòng ban hoặc toàn khách sạn.</summary>
public record MonthlyAttendanceReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<MonthlyAttendanceRowDto> Rows)
{
    public int TongNhanVien => Rows.Count;

    public int TongCaPhanCong => Rows.Sum(r => r.TongCaPhanCong);
    public int TongCaDaKetThuc => Rows.Sum(r => r.SoCaDaKetThuc);
    public int TongCaDungGio => Rows.Sum(r => r.SoCaDungGio);
    public int TongCaVangMat => Rows.Sum(r => r.SoCaVangMat);
    public int TongPhutTre => Rows.Sum(r => r.TongPhutTre);
    public int TongPhutVeSom => Rows.Sum(r => r.TongPhutVeSom);
    public decimal TongGioOt => Rows.Sum(r => r.TongGioOt);
    public decimal TongGioLamThucTe => Rows.Sum(r => r.TongGioLamThucTe);

    /// <summary>Tỷ lệ chuyên cần toàn khách sạn, cùng cách tính với từng dòng.</summary>
    public decimal TyLeChuyenCan => TongCaDaKetThuc == 0
        ? 0m
        : Math.Round(TongCaDungGio * 100m / TongCaDaKetThuc, 2);
}
