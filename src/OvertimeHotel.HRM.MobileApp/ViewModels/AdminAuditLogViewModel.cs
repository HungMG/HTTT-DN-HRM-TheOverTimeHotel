using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel cho Tab Nhật Ký Quản Trị Hệ Thống (dành riêng cho vai trò Admin).
/// </summary>
public class AdminAuditLogViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private readonly SupabaseRestClient _restClient;
    private bool _isRefreshing;
    private string _activeFilter = "ALL";
    private int _totalLogs;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AdminAuditLogItem> FilteredLogs { get; } = new();
    private readonly List<AdminAuditLogItem> _allLogs = new();

    public ICommand RefreshCommand { get; }
    public ICommand FilterCommand { get; }
    public ICommand BackCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string ActiveFilter
    {
        get => _activeFilter;
        set => SetProperty(ref _activeFilter, value);
    }

    public int TotalLogs
    {
        get => _totalLogs;
        set => SetProperty(ref _totalLogs, value);
    }

    public AdminAuditLogViewModel(IAuthService authService, SupabaseRestClient restClient)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));

        RefreshCommand = new Command(async () => await LoadLogsAsync());
        FilterCommand = new Command<string>(filter => ApplyFilter(filter));
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadLogsAsync()
    {
        IsRefreshing = true;

        try
        {
            var logs = await AdminAuditLogPostgrstMapper.FetchRecentAsync(_restClient, withinDays: 30, limit: 50);

            _allLogs.Clear();
            _allLogs.AddRange(logs);
            TotalLogs = _allLogs.Count;

            ApplyFilter(ActiveFilter);
        }
        catch (Exception ex)
        {
            _allLogs.Clear();
            _allLogs.Add(new AdminAuditLogItem
            {
                AuditLogId = 0,
                ActionCode = "REJECT",
                ActionName = "Lỗi tải",
                Description = $"Không thể tải nhật ký kiểm toán: {ex.Message}",
                ActorName = "System",
                TargetUsername = "BOD",
                OccurredAt = DateTimeOffset.Now
            });
            TotalLogs = 0;
            ApplyFilter("ALL");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyFilter(string? filter)
    {
        ActiveFilter = filter ?? "ALL";
        FilteredLogs.Clear();

        var query = ActiveFilter switch
        {
            "ACCOUNT" => _allLogs.Where(x =>
                x.ActionCode.Contains("ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("USER", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("PROMOT", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("UPGRADE", StringComparison.OrdinalIgnoreCase)),
            "PAYROLL" => _allLogs.Where(x =>
                x.ActionCode.Contains("LUONG", StringComparison.OrdinalIgnoreCase)),
            "LEAVE" => _allLogs.Where(x =>
                x.ActionCode.Contains("DON_TU", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("GIAI_TRINH", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode is "APPROVE" or "REJECT"),
            "SHIFT" => _allLogs.Where(x =>
                x.ActionCode.Contains("DOI_CA", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("CA_MAU", StringComparison.OrdinalIgnoreCase) ||
                x.ActionCode.Contains("SU_KIEN", StringComparison.OrdinalIgnoreCase)),
            _ => _allLogs
        };

        foreach (var log in query)
        {
            FilteredLogs.Add(log);
        }
    }

    protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
