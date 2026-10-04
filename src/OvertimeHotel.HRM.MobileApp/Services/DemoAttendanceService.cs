using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ chấm công cho CHẾ ĐỘ DEMO.
/// Hoạt động 100% trên bộ nhớ In-Memory, tái sử dụng đúng bộ luật đối soát ca của máy chủ
/// (AttendanceCalculator) để kết quả Demo khớp với kết quả Production.
/// </summary>
public class DemoAttendanceService : IAttendanceService
{
    private readonly IAuthService _authService;
    private readonly ILogger<DemoAttendanceService> _logger;
    private readonly object _syncRoot = new();

    private int _maPhanCa;
    private string _tenCa = string.Empty;
    private DateTimeOffset _batDauDuKien;
    private DateTimeOffset _ketThucDuKien;
    private DateTimeOffset? _checkIn;
    private DateTimeOffset? _checkOut;
    private int _phutDiTre;
    private int _phutVeSom;
    private decimal _gioOt;
    private string _trangThai = AttendanceCalculator.STATUS_DUNG_GIO;

    public bool IsDemoMode => true;

    public DemoAttendanceService(IAuthService authService, ILogger<DemoAttendanceService> logger)
    {
        _authService = authService;
        _logger = logger;
        SeedTodayShift();
    }

    public Task<TodayShiftDto> GetTodayShiftAsync()
    {
        lock (_syncRoot)
        {
            return Task.FromResult(new TodayShiftDto(
                MaPhanCa: _maPhanCa,
                TenCa: _tenCa,
                GioBatDau: _batDauDuKien,
                GioKetThuc: _ketThucDuKien,
                DaCheckIn: _checkIn,
                DaCheckOut: _checkOut,
                PhutDiTre: _phutDiTre,
                PhutVeSom: _phutVeSom,
                GioOt: _gioOt,
                TrangThai: _trangThai));
        }
    }

    public Task<CheckInResponse> CheckInAsync(int maPhanCa, string? toaDo, string? thietBi)
    {
        var user = _authService.CurrentUser;
        if (user == null || !user.IsLoggedIn)
        {
            return Task.FromResult(new CheckInResponse(false, "Chưa đăng nhập.", Code: "UNAUTHENTICATED"));
        }

        lock (_syncRoot)
        {
            if (_maPhanCa == 0)
            {
                return Task.FromResult(new CheckInResponse(
                    false, "Hôm nay bạn không có ca làm việc.", Code: "NOT_FOUND"));
            }

            if (_checkIn.HasValue)
            {
                return Task.FromResult(new CheckInResponse(
                    false, "Bạn đã quẹt thẻ vào ca này rồi.", Code: "ALREADY_RECORDED"));
            }

            _checkIn = DateTimeOffset.Now;
            var result = AttendanceCalculator.EvaluateCheckIn(_batDauDuKien, _checkIn.Value);
            _phutDiTre = result.PhutDiTre;
            _phutVeSom = 0;
            _gioOt = 0m;
            _trangThai = result.TrangThai;

            _logger.LogInformation("[DEMO] Quẹt thẻ vào ca {MaPhanCa}, trễ {PhutDiTre} phút.", _maPhanCa, _phutDiTre);

            return Task.FromResult(new CheckInResponse(
                Success: true,
                Message: result.PhutDiTre > 0
                    ? $"Đã quẹt thẻ vào ca {_tenCa} muộn {_phutDiTre} phút."
                    : $"Đã quẹt thẻ vào ca {_tenCa} đúng giờ.",
                MaChamCong: 9001,
                PhutDiTre: _phutDiTre,
                PhutVeSom: 0,
                GioOt: 0m,
                TrangThai: _trangThai,
                Code: "OK"));
        }
    }

    public Task<CheckInResponse> CheckOutAsync(int maPhanCa, string? toaDo, string? thietBi)
    {
        lock (_syncRoot)
        {
            if (_maPhanCa == 0)
            {
                return Task.FromResult(new CheckInResponse(
                    false, "Hôm nay bạn không có ca làm việc.", Code: "NOT_FOUND"));
            }

            if (!_checkIn.HasValue)
            {
                return Task.FromResult(new CheckInResponse(
                    false, "Bạn chưa quẹt thẻ vào ca này.", Code: "ALREADY_RECORDED"));
            }

            if (_checkOut.HasValue)
            {
                return Task.FromResult(new CheckInResponse(
                    false, "Bạn đã quẹt thẻ ra ca này rồi.", Code: "ALREADY_RECORDED"));
            }

            _checkOut = DateTimeOffset.Now;
            var result = AttendanceCalculator.EvaluateCheckOut(
                _ketThucDuKien,
                _checkIn.Value,
                _checkOut.Value,
                _phutDiTre);

            _phutVeSom = result.PhutVeSom;
            _gioOt = result.GioOt;
            _trangThai = result.TrangThai;

            _logger.LogInformation("[DEMO] Quẹt thẻ ra ca {MaPhanCa}, OT {GioOt} giờ.", _maPhanCa, _gioOt);

            return Task.FromResult(new CheckInResponse(
                Success: true,
                Message: _phutVeSom > 0
                    ? $"Đã quẹt thẻ ra ca {_tenCa} sớm {_phutVeSom} phút."
                    : _gioOt > 0m
                        ? $"Đã quẹt thẻ ra ca {_tenCa}. Giờ làm thêm: {_gioOt:0.##} giờ."
                        : $"Đã quẹt thẻ ra ca {_tenCa} đúng giờ.",
                MaChamCong: 9001,
                PhutDiTre: _phutDiTre,
                PhutVeSom: _phutVeSom,
                GioOt: _gioOt,
                TrangThai: _trangThai,
                Code: "OK"));
        }
    }

    public Task<List<MonthlyAttendanceDto>> GetMonthlyAttendanceAsync(int year, int month)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstDay = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var records = new List<MonthlyAttendanceDto>();

        for (var day = daysInMonth; day >= 1; day--)
        {
            var date = firstDay.AddDays(day - 1);

            // Chỉ dựng dữ liệu minh hoạ cho các ngày đã qua và không phải chủ nhật.
            if (date > today || date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // Ngày hôm nay lấy đúng dữ liệu đang mô phỏng để khớp với màn hình quẹt thẻ.
            if (date == today)
            {
                records.Add(new MonthlyAttendanceDto(
                    MaPhanCa: _maPhanCa,
                    NgayLamViec: date,
                    TenCa: _tenCa,
                    CheckIn: _checkIn,
                    CheckOut: _checkOut,
                    PhutDiTre: _phutDiTre,
                    PhutVeSom: _phutVeSom,
                    GioOt: _gioOt,
                    TrangThai: _maPhanCa == 0 ? AttendanceCalculator.STATUS_VANG : _trangThai));
                continue;
            }

            var batDau = date.ToDateTime(new TimeOnly(6, 0));
            var ketThuc = date.ToDateTime(new TimeOnly(14, 0));

            // Dao động nhỏ quanh giờ chuẩn để lịch sử có cả đúng giờ, đi trễ và về sớm.
            var bienThe = Math.Abs(date.Day % 11) - 5;
            var checkIn = batDau.AddMinutes(bienThe <= 0 ? 0 : bienThe * 3);
            var checkOut = ketThuc.AddMinutes(bienThe >= 0 ? 0 : bienThe * 2);

            var result = AttendanceCalculator.EvaluateCheckOut(
                new DateTimeOffset(ketThuc),
                new DateTimeOffset(checkIn),
                new DateTimeOffset(checkOut),
                Math.Max(0, bienThe * 3));

            records.Add(new MonthlyAttendanceDto(
                MaPhanCa: 8000 + date.Day,
                NgayLamViec: date,
                TenCa: "Ca Sáng",
                CheckIn: new DateTimeOffset(checkIn),
                CheckOut: new DateTimeOffset(checkOut),
                PhutDiTre: result.PhutDiTre,
                PhutVeSom: result.PhutVeSom,
                GioOt: result.GioOt,
                TrangThai: result.TrangThai));
        }

        return Task.FromResult(records);
    }

    /// <summary>
    /// Dựng ca làm việc hôm nay. Nếu không rơi vào ca nào thì coi như hôm nay không có lịch.
    /// </summary>
    private void SeedTodayShift()
    {
        var now = DateTimeOffset.Now;
        var today = now.Date;

        for (var i = 0; i < CaMau.Count; i++)
        {
            var (tenCa, gioBatDau, gioKetThuc) = CaMau[i];

            var batDau = new DateTimeOffset(today.Add(gioBatDau));
            var ketThuc = new DateTimeOffset(today.Add(gioKetThuc));

            // Ca đêm kéo sang ngày hôm sau.
            if (gioKetThuc <= gioBatDau)
            {
                ketThuc = ketThuc.AddDays(1);
            }

            if (now >= batDau.AddHours(-2) && now <= ketThuc.AddHours(2))
            {
                _maPhanCa = 9001 + i;
                _tenCa = tenCa;
                _batDauDuKien = batDau;
                _ketThucDuKien = ketThuc;
                return;
            }
        }

        _maPhanCa = 0;
        _tenCa = string.Empty;
    }

    // Ba ca chuẩn 24/7 của khách sạn: Sáng 06:00-14:00, Chiều 14:00-22:00, Đêm 22:00-06:00 hôm sau.
    private static readonly List<(string TenCa, TimeSpan BatDau, TimeSpan KetThuc)> CaMau = new()
    {
        ("Ca Sáng", new TimeSpan(6, 0, 0), new TimeSpan(14, 0, 0)),
        ("Ca Chiều", new TimeSpan(14, 0, 0), new TimeSpan(22, 0, 0)),
        ("Ca Đêm", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0))
    };
}