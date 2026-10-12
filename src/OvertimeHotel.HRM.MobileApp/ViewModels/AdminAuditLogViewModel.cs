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

    public AdminAuditLogViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        RefreshCommand = new Command(async () => await LoadLogsAsync());
        FilterCommand = new Command<string>(filter => ApplyFilter(filter));
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadLogsAsync()
    {
        IsRefreshing = true;

        try
        {
            await Task.Delay(250); // Phản hồi mượt mà cho UI

            // Dữ liệu nhật ký mẫu chuẩn hóa khớp với CSDL NHAT_KY_QUAN_TRI
            var sampleLogs = new List<AdminAuditLogItem>
            {
                new()
                {
                    AuditLogId = 101,
                    ActionCode = "APPROVE",
                    ActionName = "Duyệt đơn nghỉ phép",
                    Description = "Phê duyệt đơn nghỉ phép năm số #102 cho nhân viên NV-042 (Lễ tân)",
                    ActorName = "Thao.Nguyen (HR)",
                    TargetUsername = "NV-042",
                    OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-25)
                },
                new()
                {
                    AuditLogId = 102,
                    ActionCode = "SHIFT_CHANGE",
                    ActionName = "Điều phối đổi ca trực",
                    Description = "Đổi ca trực Lễ tân FO từ Ca 1 (Sáng) sang Ca 2 (Chiều) ngày hôm nay",
                    ActorName = "Dang.Le (Manager FO)",
                    TargetUsername = "NV-019",
                    OccurredAt = DateTimeOffset.UtcNow.AddHours(-1)
                },
                new()
                {
                    AuditLogId = 103,
                    ActionCode = "USER_LOCK",
                    ActionName = "Khóa tài khoản tạm thời",
                    Description = "Tạm khóa tài khoản do phát hiện 5 lần nhập sai mã PIN liên tiếp",
                    ActorName = "IT-Admin (Hệ thống)",
                    TargetUsername = "NV-088",
                    OccurredAt = DateTimeOffset.UtcNow.AddHours(-5)
                },
                new()
                {
                    AuditLogId = 104,
                    ActionCode = "USER_CREATE",
                    ActionName = "Cấp tài khoản mới",
                    Description = "Kích hoạt tài khoản nhân viên mới bộ phận Buồng phòng Housekeeping",
                    ActorName = "IT-Admin",
                    TargetUsername = "NV-156",
                    OccurredAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new()
                {
                    AuditLogId = 105,
                    ActionCode = "APPROVE",
                    ActionName = "Duyệt đơn nghỉ ốm BHXH",
                    Description = "Xác nhận giấy nghỉ ốm hưởng bảo hiểm xã hội 2 ngày",
                    ActorName = "Thao.Nguyen (HR)",
                    TargetUsername = "NV-031",
                    OccurredAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(-3)
                }
            };

            _allLogs.Clear();
            _allLogs.AddRange(sampleLogs);
            TotalLogs = _allLogs.Count;

            ApplyFilter(ActiveFilter);
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
            "ACCOUNT" => _allLogs.Where(x => x.ActionCode.Contains("USER") || x.ActionCode.Contains("ACCOUNT")),
            "LEAVE" => _allLogs.Where(x => x.ActionCode == "APPROVE" || x.ActionCode == "REJECT"),
            "SHIFT" => _allLogs.Where(x => x.ActionCode == "SHIFT_CHANGE"),
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
