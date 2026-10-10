using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private DateOnly _weekStart = StartOfWeek(Today());
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

    public ScheduleViewModel(IScheduleService service, IAuthService authService)
    {
        _service = service;
        _authService = authService;
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
            _weekStart = StartOfWeek(Today());
            NotifyWeekLabels();
            await LoadAsync();
        });
        LoginCommand = new Command(async () =>
        {
            await _authService.LogoutAsync();
            if (Shell.Current is AppShell shell) shell.ClearRoleTabs();
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

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

    public string PeriodText => $"{_weekStart:dd/MM} – {_weekStart.AddDays(6):dd/MM/yyyy}";

    // Nêu rõ khoảng thời gian đang xem: lịch trong CSDL rời rạc theo tháng, nên
    // gặp một tuần trống là chuyện thường gặp chứ không phải tải lỗi. Ghi kèm
    // mốc thời gian giúp người dùng tự biết mà bấm ‹ › thay vì nghĩ app kẹt.
    public string EmptyText =>
        $"Không có ca nào trong tuần {_weekStart:dd/MM} – {_weekStart.AddDays(6):dd/MM/yyyy}. " +
        "Dùng ‹ › để xem tuần khác, hoặc liên hệ quản lý nếu bạn cho rằng lịch đang thiếu.";

    public async Task LoadAsync()
    {
        // Cố tình KHÔNG chặn bằng `if (IsLoading) return;`. Lớp bảo vệ thật sự của
        // lớp này là _loadVersion + CancellationTokenSource: lần tải mới hủy lần
        // đang chạy và phiên bản cũ bị loại khi trả về, nên bấm chuyển tuần liên
        // tiếp vẫn an toàn. Chặn cứng sẽ nuốt mất lần bấm kèm — nhãn kỳ đã đổi
        // sang tuần mới trong khi dữ liệu vẫn là tuần cũ.
        var version = ++_loadVersion;
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        IsLoading = true;
        HasError = false;
        RequiresLogin = false;
        ErrorMessage = "";
        HasData = false;
        IsEmpty = false;
        Days.Clear();
        try
        {
            if (_authService.CurrentUser?.IsLoggedIn != true)
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem lịch phân ca.");

            var to = _weekStart.AddDays(6);
            var shifts = await _service.GetScheduleAsync(_weekStart, to, token);
            if (version != _loadVersion || token.IsCancellationRequested) return;

            var today = Today();
            foreach (var group in shifts
                         .Where(s => s.NgayLamViec >= _weekStart && s.NgayLamViec <= to)
                         .GroupBy(s => s.NgayLamViec)
                         .OrderBy(g => g.Key))
                Days.Add(new ScheduleDayItem(group.Key,
                    group.Select(s => new ScheduleShiftItem(s)).ToArray(),
                    group.Key == today));
            HasData = true;
            IsEmpty = Days.Count == 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
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
        _weekStart = _weekStart.AddDays(offset * 7);
        NotifyWeekLabels();
        await LoadAsync();
    }

    /// <summary>
    /// Báo lại các nhãn phái sinh từ tuần đang xem. Gộp vào một chỗ để không bỏ
    /// sót nhãn nào khi đổi tuần — bỏ sót thì nhãn hiện thuộc tuần trước trong khi
    /// dữ liệu đã sang tuần mới, người dùng tưởng app lỗi.
    /// </summary>
    private void NotifyWeekLabels()
    {
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