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

    /// <summary>
    /// Ca không có ai quẹt thẻ và đã qua giờ kết thúc. Đây vừa là trạng thái hiển thị trên
    /// nhật ký chấm công, vừa là giá trị được ghi vào <c>phan_ca.trang_thai</c> khi quản lý
    /// đánh dấu vắng — nên phải trùng đúng mã mà CHECK constraint của CSDL chấp nhận.
    /// Giá trị cũ "VANG" không có trong ràng buộc của CSDL cloud nên không ghi được.
    /// </summary>
    public const string STATUS_VANG = "VANG_MAT";

    /// <summary>
    /// Số phút tối đa cho phép quẹt thẻ vào trước giờ bắt đầu ca.
    /// Nhân viên không thể quẹt thẻ vào ca sớm hơn mốc này.
    /// </summary>
    public const int EARLY_CHECKIN_MINUTES_ALLOWED = 30;

    /// <summary>
    /// Số giờ tối đa sau khi kết thúc ca cho phép nhân viên quẹt thẻ ra.
    /// Quá thời gian này ca sẽ được khóa và yêu cầu quản lý xử lý thủ công.
    /// </summary>
    public const int MAX_CHECKOUT_HOURS_AFTER_SHIFT = 8;

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
    /// Ngưỡng tối thiểu để phần làm thêm ngoài giờ được tính là tăng ca (PRD 4.3).
    /// Vượt giờ kết thúc dưới 30 phút thì không tính OT — tránh trường hợp nấn ná vài
    /// phút cuối ca cũng thành tiền tăng ca.
    /// </summary>
    public const int MIN_OVERTIME_MINUTES = 30;

    /// <summary>Đơn vị làm tròn của giờ tăng ca: nửa giờ.</summary>
    public const decimal OVERTIME_ROUNDING_HOURS = 0.5m;

    /// <summary>
    /// Tính số giờ làm thêm (OT) là phần vượt quá giờ kết thúc dự kiến của ca.
    ///
    /// <para>
    /// <b>Luật tính (PRD 4.3):</b> chỉ tính khi vượt quá <see cref="MIN_OVERTIME_MINUTES"/>
    /// phút, sau đó <b>làm tròn xuống</b> bội số <see cref="OVERTIME_ROUNDING_HOURS"/>.
    /// Làm tròn xuống là có chủ ý: 45 phút → 0,5 giờ; 90 phút → 1,5 giờ; 105 phút → 1,5 giờ;
    /// 120 phút → 2,0 giờ. Phần lẻ dưới nửa giờ không quy thành tiền.
    /// </para>
    ///
    /// <para>
    /// Trước đây hàm này làm tròn 2 chữ số thập phân, khiến 45 phút thành 0,75 giờ — trả
    /// thừa so với đặc tả. Cột <c>gio_tang_ca_ot</c> là DECIMAL(4,2) nên 0,5 và 1,5 vẫn lưu
    /// vừa; chỉ công thức thay đổi, dữ liệu cũ giữ nguyên.
    /// </para>
    /// </summary>
    public static decimal CalculateOvertimeHours(DateTimeOffset ketThucDuKien, DateTimeOffset gioCheckOut)
    {
        if (gioCheckOut <= ketThucDuKien)
        {
            return 0m;
        }

        var overtimeMinutes = (gioCheckOut - ketThucDuKien).TotalMinutes;
        if (overtimeMinutes < MIN_OVERTIME_MINUTES)
        {
            return 0m;
        }

        // Floor về bội số nửa giờ. Cộng thêm một lượng epsilon rất nhỏ để phép chia
        // không bị tụt mất nửa giờ do sai số dấu phẩy động (ví dụ 89,9999999 phút).
        var steps = Math.Floor((decimal)overtimeMinutes / 60m / OVERTIME_ROUNDING_HOURS + 0.000001m);
        return steps * OVERTIME_ROUNDING_HOURS;
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

    /// <summary>
    /// Kiểm tra khung giờ hợp lệ cho việc quẹt thẻ vào ca (Check-in window validation).
    /// Quy tắc:
    /// 1. Không thể check-in trước giờ bắt đầu ca quá 30 phút.
    /// 2. Không thể check-in sau khi ca đã kết thúc (kết thúc dự kiến).
    /// </summary>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateCheckInWindow(
        DateTimeOffset batDauDuKien,
        DateTimeOffset ketThucDuKien,
        DateTimeOffset now)
    {
        var earliestAllowed = batDauDuKien.AddMinutes(-EARLY_CHECKIN_MINUTES_ALLOWED);
        if (now < earliestAllowed)
        {
            return (false, "TOO_EARLY", $"Chưa đến giờ mở ca. Bạn chỉ có thể quẹt thẻ trước giờ vào ca tối đa {EARLY_CHECKIN_MINUTES_ALLOWED} phút.");
        }

        if (now > ketThucDuKien)
        {
            return (false, "SHIFT_ENDED", "Ca làm việc này đã kết thúc. Bạn không thể quẹt thẻ vào ca nữa.");
        }

        return (true, null, null);
    }

    /// <summary>
    /// Kiểm tra khung giờ hợp lệ cho việc quẹt thẻ ra ca (Check-out window validation).
    /// Quy tắc:
    /// 1. Giờ quẹt ra phải sau giờ quẹt vào.
    /// 2. Giờ quẹt ra không được vượt quá 8 giờ sau khi ca kết thúc.
    /// </summary>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateCheckOutWindow(
        DateTimeOffset batDauDuKien,
        DateTimeOffset ketThucDuKien,
        DateTimeOffset gioCheckIn,
        DateTimeOffset now)
    {
        if (now < gioCheckIn)
        {
            return (false, "INVALID_TIME", "Thời gian quẹt thẻ ra không thể sớm hơn thời gian quẹt thẻ vào.");
        }

        var latestAllowed = ketThucDuKien.AddHours(MAX_CHECKOUT_HOURS_AFTER_SHIFT);
        if (now > latestAllowed)
        {
            return (false, "CHECKOUT_EXPIRED", $"Đã quá thời gian cho phép tự quẹt thẻ ra (tối đa {MAX_CHECKOUT_HOURS_AFTER_SHIFT} tiếng sau khi hết ca). Vui lòng liên hệ quản lý để được hỗ trợ.");
        }

        return (true, null, null);
    }
}