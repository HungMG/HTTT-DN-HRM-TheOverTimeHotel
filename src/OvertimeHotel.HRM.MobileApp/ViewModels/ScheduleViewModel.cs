using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// Lịch phân ca 24/7 theo tuần của chính người đang đăng nhập.
/// </summary>
/// <remarks>
/// Cùng khung vòng đời với <see cref="AttendanceSummaryViewModel"/>: khoá phiên bản
/// + <see cref="CancellationTokenSource"/> để bấm chuyển tuần liên tiếp không dồn
/// kết quả của lần tải cũ vào nhau.
/// </remarks>
public class ScheduleViewModel : INotifyPropertyChanged
{
    private readonly IScheduleService _service;
    private readonly IAuthService _authService;
    private readonly ILogger<ScheduleViewModel>? _logger;

    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private DateOnly _selectedDate = Today();
    private DateOnly _month = new(Today().Year, Today().Month, 1);
    private bool _showWeek;
    private IReadOnlyList<PhanCa> _shifts = [];
    private IReadOnlyList<ScheduleEventItem> _events = [];
    private bool _eventLoadFailed;
    private DateOnly _weekStart => StartOfWeek(_selectedDate);
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private bool _requiresLogin;
    private bool _hasData;
    private bool _isEmpty;
    private string _errorMessage = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ScheduleDayItem> Days { get; } = [];

    public Command RefreshCommand { get; }
    public Command PreviousWeekCommand { get; }
    public Command NextWeekCommand { get; }
    public Command CurrentWeekCommand { get; }
    public Command LoginCommand { get; }
    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command ShowWeekCommand { get; }

    public ScheduleViewModel(IScheduleService service, IAuthService authService, ILogger<ScheduleViewModel>? logger = null)
    {
        _service = service;
        _authService = authService;
        _logger = logger;
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsLoading);

        // Chuyển tuần CỐ Ý không khoá theo IsLoading. Lịch nạp qua mạng nên
        // vài trăm mili giây, khoá nút trong lúc đó khiến người dùng bấm không
        // ăn rồi tưởng app treo. An toàn khi bấm liên tay là do _loadVersion +
        // CancellationTokenSource: lần tải sau huỷ lần trước và loại kết quả
        // cũ khi trả về (xem LoadAsync).
        PreviousWeekCommand = new Command(async () => await MoveWeekAsync(-1));
        NextWeekCommand = new Command(async () => await MoveWeekAsync(1));
        CurrentWeekCommand = new Command(async () =>
        {
            _selectedDate = Today();
            _month = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
            _showWeek = true;
            NotifyWeekLabels();
            await LoadAsync();
        });
        PreviousMonthCommand = new Command(async () => await MoveMonthAsync(-1));
        NextMonthCommand = new Command(async () => await MoveMonthAsync(1));
        ShowWeekCommand = new Command(() => { _showWeek = true; UpdateVisibleDays(); });
        LoginCommand = new Command(async () =>
        {
            await _authService.LogoutAsync();
            if (Shell.Current is AppShell shell) shell.ClearRoleTabs();
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

    public bool EventLoadFailed { get => _eventLoadFailed; private set => Set(ref _eventLoadFailed, value); }

    public bool IsDemoMode => _service.IsDemoMode;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value)) UpdateCommands();
        }
    }
    public bool IsRefreshing { get => _isRefreshing; set => Set(ref _isRefreshing, value); }
    public bool HasError { get => _hasError; private set => Set(ref _hasError, value); }
    public bool RequiresLogin { get => _requiresLogin; private set => Set(ref _requiresLogin, value); }
    public string ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }
    public bool HasData { get => _hasData; private set => Set(ref _hasData, value); }

    /// <summary>Đã nạp xong mà không có ca nào trong tuần — dùng cho ô trạng thái rỗng.</summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => Set(ref _isEmpty, value);
    }

    public DateOnly SelectedDate => _selectedDate;
    public DateOnly Month => _month;
    public DateOnly CalendarStart => ScheduleCalendar.GridStart(_month);
    public DateOnly CalendarEnd => ScheduleCalendar.GridEnd(_month);
    public bool ShowWeek => _showWeek;
    public string MonthText => $"Tháng {_month.Month:00} / {_month.Year}";
    public string SelectionText => _showWeek ? $"Ca làm trong tuần · {PeriodText}" : $"Ca làm ngày {_selectedDate:dd/MM/yyyy}";
    public bool HasShift(DateOnly date) => _shifts.Any(s => s.NgayLamViec == date);
    public bool HasEvent(DateOnly date) => _shifts.Any(s => s.NgayLamViec == date && _events.Any(e => e.ShiftId == s.MaPhanCa));

    public async Task SelectDateAsync(DateOnly date)
    {
        _selectedDate = date;
        _showWeek = false;
        if (date.Month != _month.Month || date.Year != _month.Year)
        {
            _month = new DateOnly(date.Year, date.Month, 1);
            NotifyWeekLabels();
            await LoadAsync();
        }
        else UpdateVisibleDays();
    }

    private async Task MoveMonthAsync(int offset)
    {
        _month = _month.AddMonths(offset);
        _selectedDate = _month;
        _showWeek = false;
        NotifyWeekLabels();
        await LoadAsync();
    }

    public string PeriodText => $"{_weekStart:dd/MM} – {_weekStart.AddDays(6):dd/MM/yyyy}";

    // Nêu rõ khoảng thời gian đang xem: lịch trong CSDL rời rạc theo tháng, nên
    // gặp một tuần trống là chuyện thường gặp chứ không phải tải lỗi. Ghi kèm
    // mốc thời gian giúp người dùng tự biết mà bấm ‹ › thay vì nghĩ app kẹt.
    public string EmptyText => _showWeek ? "Không có ca làm trong tuần này." : "Không có ca làm trong ngày này.";

    public async Task LoadAsync()
    {
        // Cố tình KHÔNG chặn bằng `if (IsLoading) return;`. Lớp bảo vệ thật sự của
        // lớp này là _loadVersion + CancellationTokenSource: lần tải mới hủy lần
        // đang chạy và phiên bản cũ bị loại khi trả về, nên bấm chuyển tuần liên
        // tiếp vẫn an toàn. Chặn cứng sẽ nuốt mất lần bấm kèm — nhãn kỳ đã đổi
        // sang tuần mới trong khi dữ liệu vẫn là tuần cũ.
        var version = ++_loadVersion;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        IsLoading = true;
        HasError = false;
        RequiresLogin = false;
        ErrorMessage = "";
        HasData = false;
        IsEmpty = false;
        EventLoadFailed = false;
        _events = [];
        Days.Clear();
        try
        {
            if (_authService.CurrentUser?.IsLoggedIn != true)
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem lịch phân ca.");

            var shifts = await _service.GetScheduleAsync(CalendarStart, CalendarEnd, token);
            if (version != _loadVersion || token.IsCancellationRequested) return;
            _shifts = shifts;
            try
            {
                _events = await _service.GetEventsAsync(shifts, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                if (version != _loadVersion) return;
                EventLoadFailed = true;
            }
            if (version != _loadVersion || token.IsCancellationRequested) return;
            HasData = true;
            UpdateVisibleDays();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            _logger?.LogError(ex, "Không tải được lịch phân ca từ {From} đến {To}.", CalendarStart, CalendarEnd);
            HasError = true;
            RequiresLogin = ex is UnauthorizedAccessException;
            ErrorMessage = ex switch
            {
                UnauthorizedAccessException or InvalidOperationException => ex.Message,
                OperationCanceledException => "Kết nối quá lâu. Vui lòng thử lại.",
                HttpRequestException => "Không thể kết nối máy chủ. Kiểm tra mạng và thử lại.",
                JsonException => "Dữ liệu lịch phân ca chưa hợp lệ. Vui lòng thử lại sau.",
                _ => "Không thể tải lịch phân ca lúc này. Vui lòng thử lại."
            };
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                IsRefreshing = false;
                UpdateCommands();
            }
        }
    }

    public void CancelLoad()
    {
        ++_loadVersion;
        _loadCancellation?.Cancel();
        IsLoading = false;
        IsRefreshing = false;
        Days.Clear();
        HasData = false;
        IsEmpty = false;
    }

    private async Task MoveWeekAsync(int offset)
    {
        _selectedDate = _selectedDate.AddDays(offset * 7);
        _month = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        _showWeek = true;
        NotifyWeekLabels();
        await LoadAsync();
    }

    /// <summary>
    /// Báo lại các nhãn phái sinh từ tuần đang xem. Gộp vào một chỗ để không bỏ
    /// sót nhãn nào khi đổi tuần — bỏ sót thì nhãn hiện thuộc tuần trước trong khi
    /// dữ liệu đã sang tuần mới, người dùng tưởng app lỗi.
    /// </summary>
    private void UpdateVisibleDays()
    {
        Days.Clear();
        var from = _showWeek ? _weekStart : _selectedDate;
        var to = _showWeek ? _weekStart.AddDays(6) : _selectedDate;
        foreach (var group in _shifts.Where(s => s.NgayLamViec >= from && s.NgayLamViec <= to)
                     .GroupBy(s => s.NgayLamViec).OrderBy(g => g.Key))
            Days.Add(new ScheduleDayItem(group.Key, group.Select(s => new ScheduleShiftItem(s, _events.Where(e => e.ShiftId == s.MaPhanCa).ToArray())).ToArray(), group.Key == Today()));
        IsEmpty = Days.Count == 0;
        NotifyWeekLabels();
        OnPropertyChanged(nameof(Days));
    }

    private void NotifyWeekLabels()
    {
        OnPropertyChanged(nameof(MonthText));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(ShowWeek));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void UpdateCommands()
    {
        RefreshCommand.ChangeCanExecute();
    }

    // Khách sạn dùng múi giờ +7 cho cả lịch lẫn "hôm nay".
    private static readonly TimeZoneInfo HotelTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "OvertimeHotel", TimeSpan.FromHours(7), "Giờ khách sạn", "Giờ khách sạn");

    private static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, HotelTimeZone).DateTime);

    // Tuần bắt đầu từ thứ Hai theo thông lệ nhà khách sạn.
    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}