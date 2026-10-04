namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Kết quả đối soát chấm công của một lần quẹt thẻ.
/// </summary>
/// <param name="PhutDiTre">Số phút đi trễ so với giờ bắt đầu dự kiến của ca.</param>
/// <param name="PhutVeSom">Số phút về sớm so với giờ kết thúc dự kiến của ca.</param>
/// <param name="GioOt">Số giờ làm thêm (OT) vượt quá giờ kết thúc dự kiến.</param>
/// <param name="TrangThai">Mã trạng thái chấm công, khớp ràng buộc CHECK của bảng CHAM_CONG.</param>
public record AttendanceResult(int PhutDiTre, int PhutVeSom, decimal GioOt, string TrangThai);

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) định nghĩa quy tắc đối soát ca cho Task 17.
/// Toàn bộ phép tính chạy trên máy chủ từ dữ liệu PhanCa, không tin số liệu do thiết bị gửi lên.
/// </summary>
public static class AttendanceCalculator
{
    // Danh sách phương thức quẹt thẻ được phép ghi nhận, khớp cột phuong_thuc_checkin (VARCHAR(20)).
    public static readonly IReadOnlyList<string> AllowedMethods = new[] { "GPS", "WIFI", "MANUAL" };

    public const string STATUS_DUNG_GIO = "DUNG_GIO";
    public const string STATUS_DI_TRE = "DI_TRE";
    public const string STATUS_VE_SOM = "VE_SOM";
    public const string STATUS_TRE_VA_SOM = "TRE_VA_SOM";
    public const string STATUS_VANG = "VANG";

    /// <summary>
    /// Chuẩn hóa phương thức quẹt thẻ do thiết bị gửi lên.
    /// Trả về null nếu phương thức không nằm trong danh sách cho phép (Zero Trust: không lọc rác từ client).
    /// </summary>
    public static string? NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return null;
        }

        var clean = method.Trim().ToUpperInvariant();
        return AllowedMethods.Contains(clean) ? clean : null;
    }

    /// <summary>
    /// Chuẩn hóa mốc thời gian do thiết bị gửi lên về UTC.
    /// Npgsql chỉ ghi được DateTimeOffset có offset 0 vào cột <c>timestamp with time zone</c>,
    /// còn điện thoại gửi kèm offset múi giờ địa phương (ví dụ +07:00) nên bắt buộc phải quy về UTC.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTimeOffset value) => value.ToUniversalTime();

    /// <summary>
    /// Tính số phút đi trễ khi quẹt thẻ vào.
    /// Quẹt sớm hơn giờ bắt đầu ca không bị tính đi trễ.
    /// </summary>
    public static int CalculateLateMinutes(DateTimeOffset batDauDuKien, DateTimeOffset gioCheckIn)
    {
        if (gioCheckIn <= batDauDuKien)
        {
            return 0;
        }

        return (int)Math.Ceiling((gioCheckIn - batDauDuKien).TotalMinutes);
    }

    /// <summary>
    /// Tính số phút về sớm khi quẹt thẻ ra.
    /// Quẹt muộn hơn giờ kết thúc ca không bị tính về sớm.
    /// </summary>
    public static int CalculateEarlyMinutes(DateTimeOffset ketThucDuKien, DateTimeOffset gioCheckOut)
    {
        if (gioCheckOut >= ketThucDuKien)
        {
            return 0;
        }

        return (int)Math.Ceiling((ketThucDuKien - gioCheckOut).TotalMinutes);
    }

    /// <summary>
    /// Tính số giờ làm thêm (OT) là phần vượt quá giờ kết thúc dự kiến của ca.
    /// Dùng decimal để tránh sai số dấu phẩy động, làm tròn 2 chữ số theo độ chính xác DECIMAL(4,2).
    /// </summary>
    public static decimal CalculateOvertimeHours(DateTimeOffset ketThucDuKien, DateTimeOffset gioCheckOut)
    {
        if (gioCheckOut <= ketThucDuKien)
        {
            return 0m;
        }

        return Math.Round((decimal)(gioCheckOut - ketThucDuKien).TotalHours, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Quy đổi kết quả đối soát thành mã trạng thái lưu vào cột trang_thai của bảng CHAM_CONG.
    /// Đi trễ và về sớm cùng xảy ra thì gộp thành trạng thái TRE_VA_SOM.
    /// </summary>
    public static string ResolveStatus(int phutDiTre, int phutVeSom)
    {
        if (phutDiTre > 0 && phutVeSom > 0)
        {
            return STATUS_TRE_VA_SOM;
        }

        if (phutDiTre > 0)
        {
            return STATUS_DI_TRE;
        }

        if (phutVeSom > 0)
        {
            return STATUS_VE_SOM;
        }

        return STATUS_DUNG_GIO;
    }

    /// <summary>
    /// Đối soát lần quẹt thẻ vào, dùng khi ca chưa có bản ghi chấm công.
    /// </summary>
    public static AttendanceResult EvaluateCheckIn(DateTimeOffset batDauDuKien, DateTimeOffset gioCheckIn)
    {
        var phutDiTre = CalculateLateMinutes(batDauDuKien, gioCheckIn);

        return new AttendanceResult(
            PhutDiTre: phutDiTre,
            PhutVeSom: 0,
            GioOt: 0m,
            TrangThai: ResolveStatus(phutDiTre, 0));
    }

    /// <summary>
    /// Đối soát lần quẹt thẻ ra, giữ nguyên số phút đi trễ đã ghi nhận từ lần quẹt vào.
    /// </summary>
    public static AttendanceResult EvaluateCheckOut(
        DateTimeOffset ketThucDuKien,
        DateTimeOffset gioCheckIn,
        DateTimeOffset gioCheckOut,
        int phutDiTreDaGhiNhan)
    {
        var phutVeSom = CalculateEarlyMinutes(ketThucDuKien, gioCheckOut);
        var gioOt = CalculateOvertimeHours(ketThucDuKien, gioCheckOut);

        return new AttendanceResult(
            PhutDiTre: phutDiTreDaGhiNhan,
            PhutVeSom: phutVeSom,
            GioOt: gioOt,
            TrangThai: ResolveStatus(phutDiTreDaGhiNhan, phutVeSom));
    }
}