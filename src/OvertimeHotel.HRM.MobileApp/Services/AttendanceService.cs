using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ chấm công gọi THẲNG Supabase PostgREST, đúng kiến trúc PRD 06.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao không gọi qua Web Admin.</b> Trước đây service dựng HttpClient trỏ
/// <c>ApiConfig.BaseUrl</c> (cổng API WebAdmin). Ở bản Release giá trị đó là
/// <c>https://api.overtimehotel.com</c> — domain không tồn tại, nên mọi lệnh gọi
/// rơi vào <c>HttpRequestException</c> rồi hiện chung "Lỗi Tải Dữ Liệu".
/// </para>
///
/// <para>
/// <b>Phép đối soát không tin số liệu thiết bị.</b> Điểm mấu chốt của
/// AttendanceCalculator là "tính trên dữ liệu ca của CSDL, không tin mốc thời
/// gian do client gửi lên". Vì vậy service chỉ dùng <c>DateTimeOffset.UtcNow</c>
/// làm mốc ghi nhận (gio_check_in/gio_check_out) và mọi phép tính phút trễ, về
/// sớm, OT đều chạy lại ở đây từ <c>bat_dau_du_kien</c>/<c>ket_thuc_du_kien</c>
/// đọc từ CSDL — đúng như WebAdmin làm. Số trả về là số đã đối soát, không
/// phải số client tự chế ra.
/// </para>
///
/// <para>
/// <b>Chặn quẹt hộ bằng RLS.</b> Mọi truy vấn đều khoá theo
/// <c>ma_nhan_vien=eq.{của chính mình}</c> lấy từ phiên, và policy
/// <c>them_cham_cong_cua_toi</c> trong RLS kiểm tra lại bằng EXISTS trên phan_ca —
/// nên một client sửa tay request vẫn không quẹt được thẻ hộ đồng nghiệp.
/// </para>
/// </remarks>
public class AttendanceService : IAttendanceService
{
    private readonly SupabaseRestClient _rest;
    private readonly IAuthService _authService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly ILogger<AttendanceService> _logger;

    /// <summary>
    /// Cấu hình phạm vi quẹt thẻ, nạp một lần rồi dùng lại. Toạ độ khách sạn
    /// không đổi trong suốt phiên chạy nên đọc file lại mỗi lần quẹt thẻ là
    /// thừa — và trên Android, mở stream từ APK khá chậm.
    /// </summary>
    private GeofenceConfig? _geofenceConfig;

    public bool IsDemoMode => false;

    public AttendanceService(
        SupabaseRestClient rest,
        IAuthService authService,
        IGeoLocationService geoLocationService,
        ILogger<AttendanceService> logger)
    {
        _rest = rest ?? throw new ArgumentNullException(nameof(rest));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _geoLocationService = geoLocationService ?? throw new ArgumentNullException(nameof(geoLocationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Quan hệ cham_cong là 1-1 với phan_ca (unique index trên ma_phan_ca) nên
    /// PostgREST trả về OBJECT. Đã kiểm chứng cú pháp sống bằng curl: truy vấn
    /// này trả 401, tức parse thành công và chỉ thiếu quyền của anon.
    /// </summary>
    private const string SelectPhanCa =
        "ma_phan_ca,ma_nhan_vien,ma_ca,ngay_lam_viec,bat_dau_du_kien,ket_thuc_du_kien," +
        "trang_thai,ca_lam_viec(ten_ca)," +
        "cham_cong(ma_cham_cong,gio_check_in,gio_check_out,phut_di_tre,phut_ve_som," +
        "gio_tang_ca_ot,trang_thai)";

    public async Task<TodayShiftDto> GetTodayShiftAsync()
    {
        var maNhanVien = RequireEmployeeId();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var json = await _rest.GetAsync(
            $"phan_ca?select={SelectPhanCa}&ma_nhan_vien=eq.{maNhanVien}" +
            $"&ngay_lam_viec=eq.{today:yyyy-MM-dd}&order=bat_dau_du_kien.asc&limit=1");

        var rows = PostgrstJson.ParseArray(json);
        if (rows.Length == 0)
        {
            // Không có ca hôm nay KHÔNG phải lỗi — WebAdmin cũng trả DTO rỗng.
            // Ném lỗi ở đây sẽ khiến popup "Lỗi Tải Dữ Liệu" hiện ra mỗi ngày
            // nghỉ, tệ hơn nhiều so với hiện thẻ "Hôm nay không có ca".
            return new TodayShiftDto(0, string.Empty, default, default, null, null);
        }

        var baseShift = ToTodayShift(rows[0]);

        try
        {
            var eventJson = await _rest.GetAsync(
                $"phan_cong_du_an?select=ma_phan_cong,vai_tro_du_an,trang_thai_tham_gia,du_an(ten_du_an),cong_su_kien(bat_dau,ket_thuc)" +
                $"&ma_phan_ca=eq.{baseShift.MaPhanCa}&trang_thai_tham_gia=neq.HUY_PHAN_CONG&limit=1");
            var eventRows = PostgrstJson.ParseArray(eventJson);
            if (eventRows.Length > 0)
            {
                var ev = eventRows[0];
                var tenDuAn = PostgrstJson.ReadEmbeddedString(ev, "du_an", "ten_du_an");
                var vaiTro = PostgrstJson.ReadNullableString(ev, "vai_tro_du_an");
                decimal? hours = null;
                if (ev.TryGetProperty("cong_su_kien", out var congProp) && congProp.ValueKind == JsonValueKind.Array)
                {
                    decimal totalHours = 0m;
                    foreach (var c in congProp.EnumerateArray())
                    {
                        var start = PostgrstJson.ReadTimestamp(c, "bat_dau");
                        var end = PostgrstJson.ReadTimestamp(c, "ket_thuc");
                        if (end > start)
                        {
                            totalHours += (decimal)(end - start).TotalHours;
                        }
                    }
                    if (totalHours > 0m)
                    {
                        hours = Math.Round(totalHours, 2);
                    }
                }

                return baseShift with
                {
                    TenSuKien = tenDuAn,
                    VaiTroSuKien = vaiTro,
                    SoGioSuKien = hours
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tải phân công sự kiện cho ca #{MaPhanCa}", baseShift.MaPhanCa);
        }

        return baseShift;
    }

    public Task<CheckInResponse> CheckInAsync(int maPhanCa, string? toaDo, string? thietBi) =>
        SendCheckAsync(isCheckIn: true, maPhanCa, toaDo, thietBi);

    public Task<CheckInResponse> CheckOutAsync(int maPhanCa, string? toaDo, string? thietBi) =>
        SendCheckAsync(isCheckIn: false, maPhanCa, toaDo, thietBi);

    public async Task<List<MonthlyAttendanceDto>> GetMonthlyAttendanceAsync(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new InvalidOperationException("Tháng hoặc năm không hợp lệ.");
        }

        var maNhanVien = RequireEmployeeId();
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1);

        // Khoá theo chính mình + khoảng ngày, để RLS chặn lần nữa ở tầng CSDL.
        var json = await _rest.GetAsync(
            $"phan_ca?select={SelectPhanCa}&ma_nhan_vien=eq.{maNhanVien}" +
            $"&ngay_lam_viec=gte.{firstDay:yyyy-MM-dd}&ngay_lam_viec=lt.{lastDay:yyyy-MM-dd}" +
            "&order=ngay_lam_viec.desc");

        return [.. PostgrstJson.ParseArray(json).Select(ToMonthlyRecord)];
    }

    /// <summary>
    /// Quẹt thẻ vào hoặc ra. Ghi bảng cham_cong rồi đối soát bằng
    /// <see cref="AttendanceCalculator"/> trên giờ dự kiến đọc từ CSDL.
    /// </summary>
    private async Task<CheckInResponse> SendCheckAsync(bool isCheckIn, int maPhanCa, string? toaDo, string? thietBi)
    {
        var maNhanVien = RequireEmployeeId();

        // RÀNG BUỘC HỢP ĐỒNG: Chỉ nhân viên còn hợp đồng lao động có hiệu lực mới được chấm công
        var contractCheck = await ValidateContractAsync(maNhanVien);
        if (!contractCheck.IsAllowed)
        {
            return Failure(contractCheck.ErrorCode ?? "CONTRACT_INVALID", contractCheck.ErrorMessage ?? ContractAttendancePolicy.MSG_NO_CONTRACT);
        }

        // PRD 4.1 — xác thực vị trí TRƯỚC khi ghi bất cứ thứ gì. Chặn ở đây
        // chứ không chặn ở ViewModel để không client nào (kể cả bản build cũ
        // chưa cập nhật) đi vòng qua được.
        var geofence = await ValidateLocationAsync(toaDo);
        if (!geofence.IsAllowed)
        {
            return Failure("OUT_OF_RANGE", geofence.ThongDiep ?? "Bạn đang ở ngoài phạm vi khách sạn.");
        }

        // Zero Trust: chỉ nhận phương thức nằm trong danh sách cho phép, không
        // lấy bừa giá trị client gửi lên. Phương thức lấy từ kết quả xác thực
        // (GPS khi toạ độ hợp lệ và trong bán kính, WIFI khi khớp SSID nội bộ).
        var phuongThuc = AttendanceCalculator.NormalizeMethod(geofence.Method)
            ?? throw new InvalidOperationException("Phương thức quẹt thẻ không hợp lệ.");

        var phanCa = await LoadOwnShiftAsync(maPhanCa, maNhanVien);
        if (phanCa is null)
        {
            return Failure("NOT_FOUND", "Không tìm thấy ca làm việc của bạn. Vui lòng liên hệ quản lý.");
        }

        var chamCong = PostgrstJson.ReadEmbedded(phanCa.Value.Row, "cham_cong");
        var maChamCong = ReadInt(chamCong, "ma_cham_cong");

        return isCheckIn
            ? await PunchInAsync(phanCa.Value, chamCong, maChamCong, phuongThuc, toaDo, thietBi)
            : await PunchOutAsync(phanCa.Value, chamCong, maChamCong);
    }

    /// <summary>
    /// Xác thực hai lớp vị trí quẹt thẻ theo PRD 4.1: GPS trong bán kính
    /// <b>hoặc</b> đang nối đúng Wi-Fi nội bộ.
    ///
    /// <para>
    /// Toạ độ do thiết bị gửi lên KHÔNG được tin tuyệt đối — nhưng đây là thứ
    /// duy nhất app có. Hệ quả: kẻ gian sửa được app vẫn qua được lớp GPS.
    /// Lớp phòng thủ thật là RLS ở CSDL (chỉ quẹt được cho chính mình) cộng với
    /// <c>toa_do_checkin</c> lưu lại làm bằng chứng để HR đối chiếu sau.
    /// </para>
    /// </summary>
    private async Task<GeofenceCheckResult> ValidateLocationAsync(string? toaDo)
    {
        _geofenceConfig ??= await GeofenceConfig.LoadAsync(_logger);

        // Chỉ tra SSID khi GPS không đủ kết luận. Đọc SSID trên Android cần quyền
        // vị trí và mất vài chục mili-giây; khi toạ độ đã nằm trong bán kính thì
        // lớp thứ hai không thể thay đổi kết quả.
        if (GeofencePolicy.TryParseCoordinate(toaDo, out var lat, out var lng))
        {
            var khoangCach = GeofencePolicy.CalculateDistanceMeters(
                lat, lng, _geofenceConfig.HotelLatitude, _geofenceConfig.HotelLongitude);

            if (khoangCach <= _geofenceConfig.RadiusMeters)
            {
                return new GeofenceCheckResult(true, "GPS", khoangCach, null);
            }
        }

        var ssid = await _geoLocationService.GetCurrentWifiSsidAsync();

        var result = GeofencePolicy.Evaluate(
            toaDo,
            ssid,
            _geofenceConfig.HotelLatitude,
            _geofenceConfig.HotelLongitude,
            _geofenceConfig.RadiusMeters,
            _geofenceConfig.WifiSsid);

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Chặn quẹt thẻ ngoài phạm vi. Khoảng cách: {KhoangCach} m, SSID: {Ssid}.",
                result.KhoangCachMet?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "không có",
                ssid ?? "không có");
        }

        return result;
    }

    private async Task<(bool IsAllowed, string? ErrorCode, string? ErrorMessage)> ValidateContractAsync(int maNhanVien)
    {
        try
        {
            var json = await _rest.GetAsync(
                $"hop_dong?select=ma_hop_dong,trang_thai,ngay_bat_dau,ngay_ket_thuc&ma_nhan_vien=eq.{maNhanVien}&order=ngay_bat_dau.desc");

            var rows = PostgrstJson.ParseArray(json);
            if (rows.Length == 0)
            {
                return (false, "NO_CONTRACT", ContractAttendancePolicy.MSG_NO_CONTRACT);
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var contracts = rows.Select(r => new Core.Models.HopDong
            {
                TrangThai = PostgrstJson.ReadString(r, "trang_thai") ?? string.Empty,
                NgayBatDau = PostgrstJson.ReadDate(r, "ngay_bat_dau"),
                NgayKetThuc = PostgrstJson.ReadNullableDate(r, "ngay_ket_thuc")
            }).ToList();

            return ContractAttendancePolicy.ValidateAttendanceContract(contracts, today);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kiểm tra hợp đồng qua Supabase REST cho nhân viên {MaNhanVien}", maNhanVien);
            return (true, null, null);
        }
    }

    private async Task<CheckInResponse> PunchInAsync(
        ShiftSnapshot phanCa,
        JsonElement? chamCong,
        int maChamCong,
        string phuongThuc,
        string? toaDo,
        string? thietBi)
    {
        if (chamCong is { } existing && PostgrstJson.ReadNullableTimestamp(existing, "gio_check_in") is not null)
        {
            return Failure("ALREADY_RECORDED", "Bạn đã quẹt thẻ vào ca này rồi.");
        }

        var now = AttendanceCalculator.ToUtc(DateTimeOffset.UtcNow);
        var windowValidation = AttendanceCalculator.ValidateCheckInWindow(
            phanCa.BatDauDuKien, phanCa.KetThucDuKien, now);
        if (!windowValidation.IsValid)
        {
            return Failure(windowValidation.ErrorCode ?? "INVALID_WINDOW",
                windowValidation.ErrorMessage ?? "Không thể quẹt thẻ vào ca lúc này.");
        }

        var result = AttendanceCalculator.EvaluateCheckIn(phanCa.BatDauDuKien, now);

        var body = JsonSerializer.Serialize(new CheckInPayload(
            MaPhanCa: phanCa.MaPhanCa,
            GioCheckIn: FormatTimestamp(now),
            PhutDiTre: result.PhutDiTre,
            PhutVeSom: 0,
            GioTangCaOt: 0m,
            TrangThai: result.TrangThai,
            PhuongThucCheckIn: phuongThuc,
            ToaDoCheckIn: Sanitize(toaDo, 50),
            ThietBiCheckIn: Sanitize(thietBi, 100)));

        if (chamCong is null)
        {
            // Ca được xếp trước nhưng chưa ai quẹt thẻ nên chưa có dòng cham_cong.
            // Unique index trên ma_phan_ca chặn tạo dòng thừa: nếu hai thiết bị
            // quẹt cùng lúc, PostgREST trả 23505 và SupabaseRestClient dịch thành
            // thông báo dữ liệu không hợp lệ — không tạo được bản ghi rác.
            await _rest.PostAsync("cham_cong", body);
        }
        else
        {
            await _rest.PatchAsync($"cham_cong?ma_cham_cong=eq.{maChamCong}", body);
        }

        if (_rest.NoRowsMatched)
        {
            return Failure("CONFLICT", "Thao tác này vừa được thực hiện. Vui lòng tải lại.");
        }

        _logger.LogInformation(
            "Nhân viên quẹt thẻ vào ca {MaPhanCa}, trễ {PhutDiTre} phút.",
            phanCa.MaPhanCa, result.PhutDiTre);

        return new CheckInResponse(
            Success: true,
            Message: BuildCheckInMessage(result, phanCa.TenCa),
            MaChamCong: maChamCong > 0 ? maChamCong : null,
            PhutDiTre: result.PhutDiTre,
            PhutVeSom: 0,
            GioOt: 0m,
            TrangThai: result.TrangThai);
    }

    private async Task<CheckInResponse> PunchOutAsync(
        ShiftSnapshot phanCa,
        JsonElement? chamCong,
        int maChamCong)
    {
        // Thu hẹp kiểu bằng pattern is { } — chamCong là JsonElement?, và truyền
        // thẳng biến nullable vào hàm nhận JsonElement sẽ không biên dịch.
        if (chamCong is not { } record || maChamCong <= 0)
        {
            return Failure("ALREADY_RECORDED", "Bạn chưa quẹt thẻ vào ca này.");
        }

        var gioCheckIn = PostgrstJson.ReadNullableTimestamp(record, "gio_check_in");
        if (gioCheckIn is null)
        {
            return Failure("ALREADY_RECORDED", "Bạn chưa quẹt thẻ vào ca này.");
        }

        if (PostgrstJson.ReadNullableTimestamp(record, "gio_check_out") is not null)
        {
            return Failure("ALREADY_RECORDED", "Bạn đã quẹt thẻ ra ca này rồi.");
        }

        var now = AttendanceCalculator.ToUtc(DateTimeOffset.UtcNow);
        var windowValidation = AttendanceCalculator.ValidateCheckOutWindow(
            phanCa.BatDauDuKien, phanCa.KetThucDuKien, gioCheckIn.Value, now);
        if (!windowValidation.IsValid)
        {
            return Failure(windowValidation.ErrorCode ?? "INVALID_WINDOW",
                windowValidation.ErrorMessage ?? "Không thể quẹt thẻ ra ca lúc này.");
        }

        var result = AttendanceCalculator.EvaluateCheckOut(
            phanCa.KetThucDuKien, gioCheckIn.Value, now, ReadInt(record, "phut_di_tre"));

        await _rest.PatchAsync(
            $"cham_cong?ma_cham_cong=eq.{maChamCong}",
            JsonSerializer.Serialize(new CheckOutPayload(
                GioCheckOut: FormatTimestamp(now),
                PhutVeSom: result.PhutVeSom,
                GioTangCaOt: result.GioOt,
                TrangThai: result.TrangThai)));

        if (_rest.NoRowsMatched)
        {
            return Failure("CONFLICT", "Thao tác này vừa được thực hiện. Vui lòng tải lại.");
        }

        // Ca đã quẹt đủ thẻ vào và thẻ ra thì đánh dấu hoàn thành trên lịch phân ca
        // — giữ nguyên hành vi của AttendanceController. Policy sua_phan_ca_cua_toi
        // cho phép nhân viên sửa đúng dòng phân ca của chính mình.
        //
        // Bọc try/catch vì đây là bước PHỤ đạt sau: thẻ ra đã ghi thành công ở
        // trên, nếu lỗi RLS hay mạng khiến lỗi nổi ra thì người dùng thấy popup
        // thất bại dù dữ liệu chấm công đã ghi đủ. Cảnh báo log rồi bỏ qua —
        // WebAdmin sẽ đồng bộ trạng thái này ở lần tổng hợp lương sau.
        try
        {
            await _rest.PatchAsync(
                $"phan_ca?ma_phan_ca=eq.{phanCa.MaPhanCa}",
                JsonSerializer.Serialize(new ShiftStatusPayload("HOAN_THANH")));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex, "Không cập nhật được trạng thái HOAN_THANH cho ca {MaPhanCa}.", phanCa.MaPhanCa);
        }

        _logger.LogInformation(
            "Nhân viên quẹt thẻ ra ca {MaPhanCa}, về sớm {PhutVeSom} phút, OT {GioOt} giờ.",
            phanCa.MaPhanCa, result.PhutVeSom, result.GioOt);

        return new CheckInResponse(
            Success: true,
            Message: BuildCheckOutMessage(result, phanCa.TenCa),
            MaChamCong: maChamCong,
            PhutDiTre: result.PhutDiTre,
            PhutVeSom: result.PhutVeSom,
            GioOt: result.GioOt,
            TrangThai: result.TrangThai);
    }

    /// <summary>
    /// Nạp ca theo mã và xác nhận ca đó thuộc về nhân viên đang đăng nhập —
    /// chặn quẹt thẻ hộ cho ca của người khác.
    /// </summary>
    private async Task<ShiftSnapshot?> LoadOwnShiftAsync(int maPhanCa, int maNhanVien)
    {
        var rows = PostgrstJson.ParseArray(
            await _rest.GetAsync(
                $"phan_ca?select={SelectPhanCa}&ma_phan_ca=eq.{maPhanCa}&ma_nhan_vien=eq.{maNhanVien}"));

        return rows.Length == 0 ? null : new ShiftSnapshot(
            MaPhanCa: maPhanCa,
            TenCa: PostgrstJson.ReadEmbeddedString(rows[0], "ca_lam_viec", "ten_ca"),
            BatDauDuKien: PostgrstJson.ReadTimestamp(rows[0], "bat_dau_du_kien"),
            KetThucDuKien: PostgrstJson.ReadTimestamp(rows[0], "ket_thuc_du_kien"),
            Row: rows[0]);
    }

    private static TodayShiftDto ToTodayShift(JsonElement row)
    {
        var chamCong = PostgrstJson.ReadEmbedded(row, "cham_cong");

        return new TodayShiftDto(
            MaPhanCa: ReadInt(row, "ma_phan_ca"),
            TenCa: PostgrstJson.ReadEmbeddedString(row, "ca_lam_viec", "ten_ca"),
            GioBatDau: PostgrstJson.ReadTimestamp(row, "bat_dau_du_kien"),
            GioKetThuc: PostgrstJson.ReadTimestamp(row, "ket_thuc_du_kien"),
            DaCheckIn: chamCong is { } c1 ? PostgrstJson.ReadNullableTimestamp(c1, "gio_check_in") : null,
            DaCheckOut: chamCong is { } c2 ? PostgrstJson.ReadNullableTimestamp(c2, "gio_check_out") : null,
            PhutDiTre: chamCong is { } c3 ? ReadInt(c3, "phut_di_tre") : 0,
            PhutVeSom: chamCong is { } c4 ? ReadInt(c4, "phut_ve_som") : 0,
            GioOt: chamCong is { } c5 ? PostgrstJson.ReadDecimal(c5, "gio_tang_ca_ot") : 0m,
            TrangThai: chamCong is { } c6 ? PostgrstJson.ReadNullableString(c6, "trang_thai") : null);
    }

    /// <summary>
    /// Dòng lịch sử tháng.
    ///
    /// <para>
    /// Ca chưa quẹt thẻ thì quan hệ cham_cong NULL. Trả <c>TrangThai</c> rỗng chứ
    /// KHÔNG mặc định "DUNG_GIO" — nếu không, lịch sử hiển thị nhầm là đã đi làm
    /// đúng giờ cho mọi ca chưa có dữ liệu. ViewModel rơi về "Vắng mặt" cho
    /// chuỗi rỗng, đúng nghĩa.
    /// </para>
    /// </summary>
    private static MonthlyAttendanceDto ToMonthlyRecord(JsonElement row)
    {
        var chamCong = PostgrstJson.ReadEmbedded(row, "cham_cong");

        return new MonthlyAttendanceDto(
            MaPhanCa: ReadInt(row, "ma_phan_ca"),
            NgayLamViec: PostgrstJson.ReadDate(row, "ngay_lam_viec"),
            TenCa: PostgrstJson.ReadEmbeddedString(row, "ca_lam_viec", "ten_ca"),
            CheckIn: chamCong is { } c1 ? PostgrstJson.ReadNullableTimestamp(c1, "gio_check_in") : null,
            CheckOut: chamCong is { } c2 ? PostgrstJson.ReadNullableTimestamp(c2, "gio_check_out") : null,
            PhutDiTre: chamCong is { } c3 ? ReadInt(c3, "phut_di_tre") : 0,
            PhutVeSom: chamCong is { } c4 ? ReadInt(c4, "phut_ve_som") : 0,
            GioOt: chamCong is { } c5 ? PostgrstJson.ReadDecimal(c5, "gio_tang_ca_ot") : 0m,
            TrangThai: chamCong is { } c6 ? PostgrstJson.ReadString(c6, "trang_thai") : string.Empty);
    }

    private int RequireEmployeeId()
    {
        var user = _authService.CurrentUser;
        if (user is null || !user.IsLoggedIn || user.MaNhanVien <= 0)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        return user.MaNhanVien;
    }

    /// <summary>Cắt chuỗi vừa độ dài cột trong CSDL — vượt quá sẽ bị PostgreSQL từ chối.</summary>
    private static string? Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    /// <summary>
    /// Định dạng mốc thời gian gửi lên PostgREST: đã quy về UTC, kèm chữ "Z"
    /// để PostgreSQL hiểu đây là timestamptz. Gửi mốc giờ địa phương trần trụi
    /// sẽ bị CSDL đọc thành UTC và mọi phép so sánh giờ ca lệch hẳn.
    /// </summary>
    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static int ReadInt(JsonElement? element, string column) =>
        element is { } e ? PostgrstJson.ReadNullableInt(e, column) ?? 0 : 0;

    private static CheckInResponse Failure(string code, string message) =>
        new(false, message, Code: code);

    private static string BuildCheckInMessage(AttendanceResult result, string tenCa)
    {
        var ca = string.IsNullOrWhiteSpace(tenCa) ? "ca làm việc" : $"ca {tenCa}";
        return result.PhutDiTre > 0
            ? $"Đã quẹt thẻ vào {ca} muộn {result.PhutDiTre} phút."
            : $"Đã quẹt thẻ vào {ca} đúng giờ.";
    }

    private static string BuildCheckOutMessage(AttendanceResult result, string tenCa)
    {
        var ca = string.IsNullOrWhiteSpace(tenCa) ? "ca làm việc" : $"ca {tenCa}";

        if (result.PhutVeSom > 0)
        {
            return $"Đã quẹt thẻ ra {ca} sớm {result.PhutVeSom} phút.";
        }

        return result.GioOt > 0m
            ? $"Đã quẹt thẻ ra {ca}. Giờ làm thêm: {result.GioOt:0.##} giờ."
            : $"Đã quẹt thẻ ra {ca} đúng giờ.";
    }

    /// <summary>Thông tin ca làm việc đủ để đối soát quẹt thẻ.</summary>
    private readonly record struct ShiftSnapshot(
        int MaPhanCa,
        string TenCa,
        DateTimeOffset BatDauDuKien,
        DateTimeOffset KetThucDuKien,
        JsonElement Row);

    /// <summary>
    /// Body POST/PATCH ghi vào CSDL — mỗi cột phải gắn <c>JsonPropertyName</c>
    /// vì PostgREST so khớp theo TÊN CỘT <c>snake_case</c>, không phải theo tên
    /// property C#.
    /// </summary>
    /// <remarks>
    /// Thiếu chú thích thì <c>System.Text.Json</c> gửi <c>MaPhanCa</c>, PostgREST
    /// trả <c>PGRST204: Could not find the 'MaPhanCa' column</c> và màn hình báo
    /// chung chung "Dữ liệu gửi lên không hợp lệ" dù giá trị hoàn toàn đúng.
    /// </remarks>
    private sealed record CheckInPayload(
        [property: JsonPropertyName("ma_phan_ca")] int MaPhanCa,
        [property: JsonPropertyName("gio_check_in")] string GioCheckIn,
        [property: JsonPropertyName("phut_di_tre")] int PhutDiTre,
        [property: JsonPropertyName("phut_ve_som")] int PhutVeSom,
        [property: JsonPropertyName("gio_tang_ca_ot")] decimal GioTangCaOt,
        [property: JsonPropertyName("trang_thai")] string TrangThai,
        [property: JsonPropertyName("phuong_thuc_checkin")] string PhuongThucCheckIn,
        [property: JsonPropertyName("toa_do_checkin")] string? ToaDoCheckIn,
        [property: JsonPropertyName("thiet_bi_checkin")] string? ThietBiCheckIn);

    private sealed record CheckOutPayload(
        [property: JsonPropertyName("gio_check_out")] string GioCheckOut,
        [property: JsonPropertyName("phut_ve_som")] int PhutVeSom,
        [property: JsonPropertyName("gio_tang_ca_ot")] decimal GioTangCaOt,
        [property: JsonPropertyName("trang_thai")] string TrangThai);

    private sealed record ShiftStatusPayload(
        [property: JsonPropertyName("trang_thai")] string TrangThai);
}