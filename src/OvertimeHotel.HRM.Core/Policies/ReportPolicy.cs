using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Models;
using System.Linq.Expressions;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) cho phân hệ 10.0 — Báo cáo &amp; Thống kê Quản trị.
///
/// <para>
/// Gom toàn bộ công thức nghiệp vụ thuần (tỷ lệ thôi việc, xếp nhóm thâm niên, tỷ lệ chuyên cần,
/// đối soát ngân sách, chuẩn hóa học vấn) về một chỗ để đơn vị kiểm thử được mà không cần CSDL.
/// Controller chỉ truy vấn dữ liệu thô rồi gọi các hàm ở đây.
/// </para>
///
/// <para>
/// Hai quy tắc an toàn áp dụng cho toàn lớp:
/// <list type="bullet">
/// <item>Mọi phép chia đều đi qua đây để mẫu số bằng 0 không làm nổ truy vấn.</item>
/// <item>Mọi phép so khớp trạng thái đều nằm trong hằng/nhóm ở đây vì giá trị giữa các tầng
/// đang lệch nhau (xem <see cref="IsResigned"/> và <see cref="IsViolation"/>).</item>
/// </list>
/// </para>
/// </summary>
public static class ReportPolicy
{
    public static Expression<Func<DonTu, bool>> ApprovedLeaveFilter(DateOnly from, DateOnly to) => leave =>
        leave.TrangThai == "DA_DUYET" && leave.LoaiDon != null && leave.LoaiDon.LaNghiPhep
        && leave.TuNgay <= to && leave.DenNgay >= from;
    public static bool IsEmployedAt(NhanVien employee, DateOnly asOf) =>
        IsEmployedAt(employee.NgayVaoLam, employee.TrangThai, employee.NgayThoiViec, asOf);
    public static bool IsEmployedAt(DateOnly hired, string status, DateOnly? left, DateOnly asOf) =>
        hired <= asOf && (!IsResigned(status) || (left.HasValue && left.Value > asOf));
    public static Expression<Func<HopDong, bool>> ContractAtDate(DateOnly asOf) => h =>
        (h.TrangThai == "HIEU_LUC" || h.TrangThai == "HET_HAN" || h.TrangThai == "DA_KET_THUC"
         || h.TrangThai == "DA_CHAM_DUT" || h.TrangThai == "CHAM_DUT" || h.TrangThai == "DA_KY")
        && h.NgayBatDau <= asOf && (!h.NgayKetThuc.HasValue || h.NgayKetThuc >= asOf)
        && ((h.TrangThai != "DA_CHAM_DUT" && h.TrangThai != "CHAM_DUT") || h.NgayKetThuc != null);
    // An undated termination cannot be reconstructed as an indefinite active contract.
    public static Expression<Func<HopDong, bool>> ReferenceContractFilter => h =>
        (h.TrangThai == "HIEU_LUC" || h.TrangThai == "HET_HAN" || h.TrangThai == "DA_KET_THUC"
            || h.TrangThai == "DA_CHAM_DUT" || h.TrangThai == "CHAM_DUT")
        && ((h.TrangThai != "DA_CHAM_DUT" && h.TrangThai != "CHAM_DUT") || h.NgayKetThuc != null);

    /// <summary>Only published, completed shifts are assessed; missing punches and approved leave are explicit states.</summary>
    public static string? ResolveAttendanceStatus(bool published, string shiftStatus, DateTimeOffset endsAt,
        DateTimeOffset now, bool approvedLeave, ChamCong? punch)
    {
        if (!published || shiftStatus == "DA_HUY" || endsAt > now) return null;
        if (punch is null || (!punch.GioCheckIn.HasValue && !punch.GioCheckOut.HasValue))
            return approvedLeave ? "NGHI_DA_DUYET" : "VANG_MAT";
        if (!punch.GioCheckIn.HasValue || !punch.GioCheckOut.HasValue) return "THIEU_QUET";
        return shiftStatus == "VANG_MAT" ? "VANG_MAT" : punch.TrangThai;
    }

    /// <summary>Số tháng của một quý, dùng khi quy đổi khoảng kỳ báo cáo.</summary>
    private const int MonthsPerQuarter = 3;

    /// <summary>Số tháng của một năm tài chính.</summary>
    private const int MonthsPerYear = 12;

    /// <summary>Ngưỡng cảnh báo đỏ khi quỹ lương thực chi vượt định mức (PRD 10.4).</summary>
    public const decimal BudgetAlertThresholdPercent = 10m;

    /// <summary>Giá trị trình độ mà form nhập ghi khi người dùng chưa chọn bậc nào.</summary>
    public const string EducationNotUpdated = "Chưa cập nhật";

    /// <summary>Nhãn gom cho giá trị trình độ không khớp bậc chuẩn nào.</summary>
    public const string EducationOther = "Khác";

    // ═══════════════════════════════════════════════════════════════════════
    //  PHÂN QUYỀN &amp; PHẠM VI DỮ LIỆU
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phạm vi dữ liệu báo cáo đã giải quyết theo vai trò.
    /// <see cref="IsAllowed"/> là cổng vào (fail-closed); <see cref="IsWholeHotel"/> quyết định
    /// có phải lọc theo <see cref="DepartmentId"/> hay không.
    /// </summary>
    /// <param name="IsAllowed">Vai trò có được xem báo cáo hay không.</param>
    /// <param name="IsWholeHotel">Được xem toàn khách sạn (không lọc phòng ban).</param>
    /// <param name="DepartmentId">Phòng ban bị giới hạn; chỉ có giá trị khi <paramref name="IsWholeHotel"/> là false.</param>
    public readonly record struct BaoCaoScope(bool IsAllowed, bool IsWholeHotel, int? DepartmentId)
    {
        /// <summary>Phạm vi từ chối — dùng cho vai trò lạ, Employee, hoặc Quản lý thiếu claim phòng ban.</summary>
        public static readonly BaoCaoScope Denied = new(false, false, null);
    }

    /// <summary>
    /// Giải quyết phạm vi dữ liệu báo cáo từ vai trò và claim phòng ban.
    ///
    /// <para>
    /// Nguyên tắc Fail-Closed: chỉ Admin / HR / IT xem toàn khách sạn; Quản lý bị khóa trong
    /// bộ phận của mình; mọi trường hợp còn lại (Employee, vai trò lạ, thiếu claim) đều bị từ chối.
    /// </para>
    /// </summary>
    /// <param name="role">Tên vai trò thô từ claim <c>ClaimTypes.Role</c>.</param>
    /// <param name="claimDepartmentId">
    /// Mã phòng ban lấy từ claim <c>DepartmentId</c> của phiên đăng nhập.
    /// </param>
    public static BaoCaoScope ResolveScope(string? role, int? claimDepartmentId) =>
        LeaveApprovalPolicy.NormalizeRole(role) switch
        {
            RoleType.Admin or RoleType.HR or RoleType.IT => new BaoCaoScope(true, true, null),

            // Quản lý thiếu claim phòng ban thì không xem được gì: thà chặn nhầm còn hơn
            // để lọt số liệu toàn khách sạn ra ngoài phạm vi được phép.
            RoleType.Manager when claimDepartmentId is > 0 => new BaoCaoScope(true, false, claimDepartmentId),

            _ => BaoCaoScope.Denied
        };

    /// <summary>
    /// Vai trò được phép mở phân hệ báo cáo, theo ma trận PRD 02 mục 10.0
    /// (<c>REPORT_VIEW</c>: Admin / HR / Manager / IT — Employee bị loại).
    /// </summary>
    public static bool CanViewReports(string? role) => CanViewReports(role, 1);

    /// <summary>
    /// Bản dùng cho nơi đã có sẵn claim phòng ban (kiểm tra đầy đủ, không giả định claim tồn tại).
    /// </summary>
    /// <param name="role">Tên vai trò thô.</param>
    /// <param name="claimDepartmentId">Mã phòng ban từ claim của phiên đăng nhập.</param>
    public static bool CanViewReports(string? role, int? claimDepartmentId) =>
        ResolveScope(role, claimDepartmentId).IsAllowed;

    /// <summary>
    /// Báo cáo đối soát quỹ lương vs ngân sách (10.4) chỉ dành cho Nhân sự và Quản trị viên
    /// (PRD: BoD / HR / Kế toán). Quản lý bộ phận không thấy khối này vì quỹ lương là số liệu
    /// tài chính toàn khách sạn.
    /// </summary>
    public static bool CanViewPayrollFundReport(string? role) =>
        LeaveApprovalPolicy.NormalizeRole(role) is RoleType.Admin or RoleType.HR;

    // ═══════════════════════════════════════════════════════════════════════
    //  KỲ BÁO CÁO
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Quy đổi bộ lọc kỳ báo cáo (Năm / Quý / Tháng) thành khoảng ngày cụ thể.
    /// Ưu tiên Tháng trước Quý trước Năm: client gửi kèm cả ba thì lấy mức chi tiết nhất.
    /// </summary>
    /// <param name="year">Năm tài chính, ví dụ 2026.</param>
    /// <param name="quarter">Quý 1-4; bỏ trống nghĩa là không lọc theo quý.</param>
    /// <param name="month">Tháng 1-12; bỏ trống nghĩa là không lọc theo tháng.</param>
    /// <returns>Khoảng ngày bao trùm kỳ báo cáo kèm nhãn hiển thị tiếng Việt.</returns>
    public static (DateOnly From, DateOnly To, string Label) ResolvePeriodRange(int year, int? quarter, int? month)
    {
        // Tháng: từ ngày 1 đến ngày cuối tháng. DateTime.DaysInMonth lo năm nhuận nên
        // tháng 2 luôn cắt đúng 28 hoặc 29 ngày.
        if (month is >= 1 and <= 12)
        {
            var from = new DateOnly(year, month.Value, 1);
            var to = new DateOnly(year, month.Value, DateTime.DaysInMonth(year, month.Value));
            return (from, to, $"Tháng {month.Value:00}/{year}");
        }

        // Quý: tháng mở màn = (quý - 1) * 3 + 1, tháng khép lại = quý * 3.
        if (quarter is >= 1 and <= 4)
        {
            var startMonth = (quarter.Value - 1) * MonthsPerQuarter + 1;
            var endMonth = quarter.Value * MonthsPerQuarter;
            var from = new DateOnly(year, startMonth, 1);
            var to = new DateOnly(year, endMonth, DateTime.DaysInMonth(year, endMonth));
            return (from, to, $"Quý {quarter.Value}/{year}");
        }

        // Mặc định: cả năm tài chính.
        return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), $"Năm {year}");
    }

    /// <summary>
    /// Xác định khoảng ngày và nhãn của kỳ liền trước kỳ hiện tại (Month-over-Month hoặc Quarter-over-Quarter hoặc Year-over-Year).
    /// </summary>
    public static (DateOnly From, DateOnly To, string Label) ResolvePreviousPeriodRange(int year, int? quarter, int? month)
    {
        if (month is >= 1 and <= 12)
        {
            var prevYear = month == 1 ? year - 1 : year;
            var prevMonth = month == 1 ? 12 : month.Value - 1;
            return ResolvePeriodRange(prevYear, null, prevMonth);
        }

        if (quarter is >= 1 and <= 4)
        {
            var prevYear = quarter == 1 ? year - 1 : year;
            var prevQuarter = quarter == 1 ? 4 : quarter.Value - 1;
            return ResolvePeriodRange(prevYear, prevQuarter, null);
        }

        return ResolvePeriodRange(year - 1, null, null);
    }

    /// <summary>
    /// Xác định khoảng ngày và nhãn của cùng kỳ năm trước (Year-over-Year).
    /// </summary>
    public static (DateOnly From, DateOnly To, string Label) ResolveSamePeriodLastYearRange(int year, int? quarter, int? month) =>
        ResolvePeriodRange(year - 1, quarter, month);

    /// <summary>
    /// Tính biến động giữa kỳ hiện tại và kỳ so sánh.
    /// Trả về chênh lệch tuyệt đối, tỷ lệ % thay đổi (null nếu kỳ trước bằng 0) và hướng biến động.
    /// </summary>
    public static (decimal Absolute, decimal? Percent, string Trend) CalculateChange(decimal current, decimal previous)
    {
        var abs = current - previous;
        decimal? pct = null;
        if (previous != 0m)
        {
            pct = Math.Round(abs / Math.Abs(previous) * 100m, 1);
        }

        var trend = abs > 0 ? "tang" : (abs < 0 ? "giam" : "khong_doi");
        return (abs, pct, trend);
    }

    /// <summary>
    /// Số tháng của một kỳ báo cáo — dùng để quy đổi định mức ngân sách tháng sang cả kỳ.
    /// So quỹ lương một quý với định mức một tháng sẽ lệch đúng ba lần.
    /// </summary>
    public static int MonthsInPeriod(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            return 0;
        }

        var months = (to.Year - from.Year) * MonthsPerYear + (to.Month - from.Month) + 1;
        return Math.Max(1, months);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TRẠNG THÁI — nơi duy nhất mã hóa các giá trị đang lệch giữa các tầng
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nhân viên đã thôi việc hay chưa.
    ///
    /// <para>
    /// <b>Cố ý nhận cả hai cách viết:</b> ràng buộc CHECK của bảng <c>nhan_vien</c> chỉ cho
    /// <c>THOI_VIEC</c>, nhưng form nhập trên Web Admin lại ghi <c>DA_THOI_VIEC</c>. Bỏ sót một
    /// trong hai sẽ khiến mọi báo cáo biến động nhân sự đếm thiếu người đã nghỉ.
    /// </para>
    /// </summary>
    public static bool IsResigned(string? trangThai) => trangThai is "THOI_VIEC" or "DA_THOI_VIEC";

    /// <summary>Nhân viên đang làm việc (tính vào quân số hiện hữu).</summary>
    public static bool IsWorking(string? trangThai) => trangThai == "DANG_LAM";

    /// <summary>
    /// Lượt chấm công bị coi là vi phạm kỷ luật: đi trễ, về sớm, hoặc cả hai.
    /// Vắng mặt không nằm trong nhóm này — vắng là hành vi khác, tính riêng
    /// (xem <see cref="IsAbsent"/>).
    /// </summary>
    public static bool IsViolation(string? trangThai) => trangThai is "DI_TRE" or "VE_SOM" or "TRE_VA_SOM";

    /// <summary>
    /// Lượt chấm công bị đánh dấu vắng mặt.
    /// <para>
    /// Mã lưu trong CSDL là <c>VANG_MAT</c> — ràng buộc CHECK của bảng <c>cham_cong</c>
    /// chỉ cho đúng năm mã <c>DUNG_GIO / DI_TRE / VE_SOM / TRE_VA_SOM / VANG_MAT</c>.
    /// Gom về một chỗ để không nơi nào so sánh nhầm với <c>"VANG"</c> rồi luôn đếm ra 0.
    /// </para>
    /// </summary>
    public static bool IsAbsent(string? trangThai) => trangThai == "VANG_MAT";

    /// <summary>Lượt chấm công đúng giờ, dùng cho tử số của tỷ lệ chuyên cần.</summary>
    public static bool IsOnTime(string? trangThai) => trangThai == "DUNG_GIO";

    // ═══════════════════════════════════════════════════════════════════════
    //  MỨC LƯƠNG NHÂN SỰ
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Dải lương dưới 7 triệu (thử việc, bán thời gian, thực tập).</summary>
    public const string SalaryUnder7M = "Dưới 7 triệu";

    /// <summary>Dải lương từ 7 đến dưới 12 triệu (nhân viên tiêu chuẩn).</summary>
    public const string Salary7MTo12M = "Từ 7 đến dưới 12 triệu";

    /// <summary>Dải lương từ 12 đến dưới 20 triệu (tổ trưởng, chuyên viên, giám sát).</summary>
    public const string Salary12MTo20M = "Từ 12 đến dưới 20 triệu";

    /// <summary>Dải lương từ 20 triệu trở lên (trưởng bộ phận, ban giám đốc).</summary>
    public const string SalaryAbove20M = "Từ 20 triệu trở lên";

    /// <summary>Thứ tự hiển thị các dải mức lương.</summary>
    public static IReadOnlyList<string> SalaryBucketOrder { get; } =
    [
        SalaryUnder7M,
        Salary7MTo12M,
        Salary12MTo20M,
        SalaryAbove20M
    ];

    /// <summary>
    /// Xếp nhóm dải mức lương cơ bản của nhân sự theo định mức chuẩn khách sạn.
    /// </summary>
    public static string SalaryBucketLabel(decimal salary) => salary switch
    {
        < 7_000_000m => SalaryUnder7M,
        < 12_000_000m => Salary7MTo12M,
        < 20_000_000m => Salary12MTo20M,
        _ => SalaryAbove20M
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  THÂM NIÊN (PRD 10.3)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Nhóm thâm niên dưới 1 năm.</summary>
    public const string TenureUnderOneYear = "Dưới 1 năm";

    /// <summary>Nhóm thâm niên từ 1 đến 3 năm.</summary>
    public const string TenureOneToThreeYears = "Từ 1 - 3 năm";

    /// <summary>Nhóm thâm niên trên 3 năm.</summary>
    public const string TenureAboveThreeYears = "Trên 3 năm";

    /// <summary>
    /// Thâm niên công tác tính theo số tháng tròn giữa ngày vào làm và ngày mốc.
    /// Nhân viên chưa vào làm (mốc nằm trước ngày vào làm) trả về 0 thay vì số âm.
    /// </summary>
    public static int TenureMonths(DateOnly ngayVaoLam, DateOnly asOf) =>
        TenureMonths(ngayVaoLam, asOf, null);

    /// <summary>
    /// Thâm niên công tác có xét ngày thôi việc: nếu đã nghỉ trước ngày mốc, thâm niên dừng tại ngày thôi việc.
    /// </summary>
    public static int TenureMonths(DateOnly ngayVaoLam, DateOnly asOf, DateOnly? ngayThoiViec)
    {
        var cutoff = ngayThoiViec.HasValue && ngayThoiViec.Value < asOf ? ngayThoiViec.Value : asOf;
        if (ngayVaoLam > cutoff)
        {
            return 0;
        }

        var months = (cutoff.Year - ngayVaoLam.Year) * MonthsPerYear + (cutoff.Month - ngayVaoLam.Month);

        // Chưa tới ngày kỷ niệm trong tháng thì chưa tính là tròn tháng.
        if (cutoff.Day < ngayVaoLam.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    /// <summary>
    /// Xếp nhóm thâm niên theo PRD 10.3: dưới 1 năm, từ 1 đến 3 năm, trên 3 năm.
    /// Mốc 36 tháng được xem là đã qua ngưỡng 3 năm nên rơi vào nhóm "trên 3 năm".
    /// </summary>
    public static string TenureBucketLabel(int months) => months switch
    {
        < 12 => TenureUnderOneYear,
        < 36 => TenureOneToThreeYears,
        _ => TenureAboveThreeYears
    };

    /// <summary>Thứ tự hiển thị cố định của ba nhóm thâm niên.</summary>
    public static IReadOnlyList<string> TenureBucketOrder { get; } =
        [TenureUnderOneYear, TenureOneToThreeYears, TenureAboveThreeYears];

    // ═══════════════════════════════════════════════════════════════════════
    //  HỌC VẤN (PRD 10.3)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Sáu bậc trình độ chuẩn đang dùng trong ô chọn hồ sơ nhân viên.</summary>
    public static IReadOnlyList<string> EducationLevels { get; } =
    [
        "Sau Đại học (Thạc sĩ / Tiến sĩ)",
        "Đại học",
        "Cao đẳng",
        "Trung cấp",
        "Chứng chỉ nghề / Nghiệp vụ",
        "Phổ thông (THPT)"
    ];

    /// <summary>Thứ tự hiển thị trình độ: bậc cao trước, nhóm "Khác" xuống cuối.</summary>
    public static IReadOnlyList<string> EducationOrder { get; } = [.. EducationLevels, EducationOther];

    /// <summary>
    /// Chuẩn hóa giá trị trình độ tự do về một trong sáu bậc chuẩn, hoặc <see cref="EducationOther"/>.
    ///
    /// <para>
    /// <b>Phải khớp theo từ khóa, không khớp bằng đẳng thức:</b> cột <c>trinh_do</c> là văn bản tự
    /// do và dữ liệu thật là những chuỗi như <c>"Thạc sĩ Quản trị Khách sạn"</c>,
    /// <c>"Cao đẳng Lễ tân"</c>, <c>"Kỹ sư Cơ điện Bách Khoa"</c>. So khớp bằng đẳng thức với sáu
    /// nhãn chuẩn sẽ dồn gần như toàn bộ hồ sơ vào nhóm "Khác" và làm báo cáo vô nghĩa.
    /// </para>
    ///
    /// <para>
    /// Thứ tự kiểm tra có ý nghĩa: hai nhánh đầu phải đứng trước nhánh "Đại học", nếu không thì
    /// <c>"Đại học"</c> sẽ nuốt mất <c>"Sau Đại học"</c>.
    /// </para>
    /// </summary>
    public static string NormalizeEducation(string? trinhDo)
    {
        var value = (trinhDo ?? string.Empty).Trim();

        if (value.Length == 0 ||
            string.Equals(value, EducationNotUpdated, StringComparison.OrdinalIgnoreCase))
        {
            return EducationOther;
        }

        // Bậc sau đại học trước tiên — nếu xét "Đại học" trước sẽ nhận nhầm "Sau Đại học".
        if (Contains(value, "Thạc sĩ") || Contains(value, "Tiến sĩ") || Contains(value, "Sau Đại học"))
        {
            return EducationLevels[0];
        }

        // Kỹ sư xếp vào bậc đại học theo thông lệ tuyển dụng của khách sạn.
        if (Contains(value, "Kỹ sư") || Contains(value, "Đại học"))
        {
            return EducationLevels[1];
        }

        if (Contains(value, "Cao đẳng"))
        {
            return EducationLevels[2];
        }

        if (Contains(value, "Trung cấp"))
        {
            return EducationLevels[3];
        }

        if (Contains(value, "Chứng chỉ"))
        {
            return EducationLevels[4];
        }

        if (Contains(value, "Phổ thông") || Contains(value, "THPT"))
        {
            return EducationLevels[5];
        }

        return EducationOther;
    }

    /// <summary>
    /// Tỷ lệ nhân sự có trình độ từ đại học trở lên (kể cả sau đại học) — chỉ số chất lượng
    /// nhân sự phục vụ quy hoạch đào tạo và bổ nhiệm nội bộ (PRD 10.3).
    /// </summary>
    public static decimal HigherEducationRate(int higherEducation, int total)
    {
        if (total <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)higherEducation / total * 100m, 1);
    }

    /// <summary>So khớp chứa chuỗi con, không phân biệt hoa thường và không phụ thuộc văn hóa.</summary>
    private static bool Contains(string source, string keyword) =>
        source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    // ═══════════════════════════════════════════════════════════════════════
    //  BIẾN ĐỘNG NHÂN SỰ (PRD 10.1)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tỷ lệ thôi việc (Turnover Rate) theo PRD 10.1 = số thôi việc / quân số bình quân * 100.
    /// Quân số bình quân bằng 0 thì trả về 0 thay vì chia cho 0.
    /// </summary>
    public static decimal TurnoverRate(int averageHeadcount, int leavers)
    {
        if (averageHeadcount <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)leavers / averageHeadcount * 100m, 1);
    }

    /// <summary>
    /// Quân số bình quân trong kỳ = (đầu kỳ + cuối kỳ) / 2, làm tròn lên để không mất nửa người.
    /// </summary>
    public static int AverageHeadcount(int dauKy, int cuoiKy)
    {
        if (dauKy < 0 || cuoiKy < 0)
        {
            return 0;
        }

        return (int)Math.Ceiling((dauKy + cuoiKy) / 2m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CHUYÊN CẦN (PRD 10.5)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tỷ lệ chuyên cần theo PRD 10.5 = số ca đúng giờ / tổng ca * 100.
    /// Tổng ca bằng 0 thì trả về 0.
    /// </summary>
    public static decimal AttendanceRate(int onTime, int total)
    {
        if (total <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)onTime / total * 100m, 1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ĐỐI SOÁT QUỸ LƯƠNG (PRD 10.4)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Đối soát quỹ lương thực chi với định mức tham chiếu (PRD 10.4).
    /// </summary>
    /// <param name="actual">Tổng thực chi trong kỳ (đã quy đổi theo số tháng của kỳ).</param>
    /// <param name="budget">Định mức tham chiếu trong kỳ (đã quy đổi theo số tháng của kỳ).</param>
    /// <returns>
    /// Chênh lệch (thực chi - định mức), tỷ lệ giải ngân % (0 nếu định mức bằng 0)
    /// và cờ vượt định mức để giao diện bật cảnh báo đỏ.
    /// </returns>
    public static (decimal Variance, decimal Rate, bool Exceeded) BudgetVariance(decimal actual, decimal budget)
    {
        var variance = actual - budget;
        var rate = budget <= 0m ? 0m : Math.Round(actual / budget * 100m, 1);

        return (variance, rate, actual > budget);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BẢO MẬT XUẤT CSV
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Chuẩn hóa chuỗi an toàn cho CSV: bọc dấu nháy kép khi có ký tự đặc biệt,
    /// đồng thời vô hiệu hóa công thức nguy hiểm (CSV Formula Injection)
    /// bắt đầu bằng =, +, -, @, \t, \r bằng cách thêm tiền tố nháy đơn '.
    /// </summary>
    public static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;

        // Vô hiệu hóa tiền tố công thức bảng tính (CSV Formula Injection)
        if (text.Length > 0 && (text[0] == '=' || text[0] == '+' || text[0] == '-' || text[0] == '@' || text[0] == '\t' || text[0] == '\r'))
        {
            text = "'" + text;
        }

        if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
