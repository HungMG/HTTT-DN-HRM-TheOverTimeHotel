using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

public class AttendanceSummaryViewModel : INotifyPropertyChanged
{
    private readonly IMobileAttendanceSummaryService _service;
    private readonly IAuthService _authService;
    private AttendanceSummaryDto? _summary;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private int? _employeeId;
    private DateOnly _period = CurrentMonth();
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private bool _requiresLogin;
    private bool _onlyLeave;
    private string _errorMessage = "";
    private string? _routeError;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<AttendanceDayItem> Days { get; } = [];
    public Command RefreshCommand { get; }
    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command CurrentMonthCommand { get; }
    public Command<string> FilterCommand { get; }
    public Command LoginCommand { get; }

    public AttendanceSummaryViewModel(IMobileAttendanceSummaryService service, IAuthService authService)
    {
        _service = service;
        _authService = authService;
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        PreviousMonthCommand = new Command(async () => await MoveMonthAsync(-1),
            () => !IsLoading && _period > DateOnly.MinValue);
        NextMonthCommand = new Command(async () => await MoveMonthAsync(1),
            () => !IsLoading && (_period.Year < 9999 || _period.Month < 12));
        CurrentMonthCommand = new Command(async () =>
        {
            _period = CurrentMonth();
            OnPropertyChanged(nameof(PeriodText));
            await LoadAsync();
        }, () => !IsLoading);
        FilterCommand = new Command<string>(filter =>
        {
            _onlyLeave = filter == "leave";
            ApplyFilter();
        }, _ => !IsLoading && HasData);
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
    public bool HasData => _summary != null && !HasError;
    public string EmployeeName => _summary?.EmployeeName ?? "Bảng công theo tháng";
    public string PeriodText => $"Tháng {_period.Month:00} / {_period.Year:0000}";
    public string TotalText => _summary?.TotalCreditedDays.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string WorkedText => _summary?.WorkedDays.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string PaidLeaveText => _summary?.PaidLeaveDays.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string ScheduledText => _summary?.ScheduledDays.ToString(CultureInfo.InvariantCulture) ?? "—";
    public bool HasReviewDays => _summary?.ReviewDays > 0;
    public string ReviewText => $"Có {_summary?.ReviewDays ?? 0} ngày vừa có đơn phép vừa có chấm công. Cần đối chiếu với quản lý; mỗi ngày chỉ tính một lần.";
    public string EmptyText => _onlyLeave ? "Chưa có đơn nghỉ hưởng lương đã duyệt trong tháng này." :
        "Tháng này chưa có lịch phân ca hoặc đơn nghỉ hưởng lương đã duyệt.";
    public string AllFilterBackground => !_onlyLeave ? "#1E3A8A" : "#FFFFFF";
    public string AllFilterColor => !_onlyLeave ? "#FFFFFF" : "#475569";
    public string LeaveFilterBackground => _onlyLeave ? "#047857" : "#FFFFFF";
    public string LeaveFilterColor => _onlyLeave ? "#FFFFFF" : "#475569";

    public void Configure(IDictionary<string, object> query)
    {
        _routeError = null;
        _employeeId = null;
        if (query.TryGetValue("employeeId", out var employee))
        {
            if (int.TryParse(Convert.ToString(employee, CultureInfo.InvariantCulture), out var id) && id > 0)
                _employeeId = id;
            else _routeError = "Mã nhân viên không hợp lệ.";
        }
        if (query.ContainsKey("year") || query.ContainsKey("month"))
        {
            if (query.TryGetValue("year", out var y) && query.TryGetValue("month", out var m) &&
                int.TryParse(Convert.ToString(y, CultureInfo.InvariantCulture), out var year) &&
                int.TryParse(Convert.ToString(m, CultureInfo.InvariantCulture), out var month) &&
                year is >= 1 and <= 9999 && month is >= 1 and <= 12)
                _period = new DateOnly(year, month, 1);
            else _routeError = "Kỳ xem công không hợp lệ.";
        }
        OnPropertyChanged(nameof(PeriodText));
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        var version = ++_loadVersion;
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        IsLoading = true;
        HasError = false;
        RequiresLogin = false;
        ErrorMessage = "";
        _summary = null;
        Days.Clear();
        NotifySummary();
        try
        {
            if (_routeError != null) throw new InvalidOperationException(_routeError);
            if (_authService.CurrentUser?.IsLoggedIn != true)
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem bảng công.");
            var result = await _service.GetSummaryAsync(_period.Year, _period.Month, _employeeId, token);
            if (version != _loadVersion || token.IsCancellationRequested) return;
            _summary = result;
            ApplyFilter();
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
                JsonException => "Dữ liệu bảng công chưa hợp lệ. Vui lòng thử lại sau.",
                _ => "Không thể tải bảng công lúc này. Vui lòng thử lại."
            };
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                IsRefreshing = false;
                NotifySummary();
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
        _summary = null;
        Days.Clear();
        NotifySummary();
    }

    private async Task MoveMonthAsync(int offset)
    {
        _period = _period.AddMonths(offset);
        OnPropertyChanged(nameof(PeriodText));
        await LoadAsync();
    }

    private void ApplyFilter()
    {
        Days.Clear();
        foreach (var day in _summary?.Days ?? [])
            if (!_onlyLeave || day.HasPaidLeave) Days.Add(new AttendanceDayItem(day));
        foreach (var property in new[] { nameof(EmptyText), nameof(AllFilterBackground), nameof(AllFilterColor),
                     nameof(LeaveFilterBackground), nameof(LeaveFilterColor) }) OnPropertyChanged(property);
    }

    private void NotifySummary()
    {
        foreach (var property in new[] { nameof(HasData), nameof(EmployeeName), nameof(TotalText), nameof(WorkedText),
                     nameof(PaidLeaveText), nameof(ScheduledText), nameof(HasReviewDays), nameof(ReviewText) })
            OnPropertyChanged(property);
    }

    private void UpdateCommands()
    {
        RefreshCommand.ChangeCanExecute();
        PreviousMonthCommand.ChangeCanExecute();
        NextMonthCommand.ChangeCanExecute();
        CurrentMonthCommand.ChangeCanExecute();
        FilterCommand.ChangeCanExecute();
    }

    private static DateOnly CurrentMonth()
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        return new DateOnly(today.Year, today.Month, 1);
    }

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
