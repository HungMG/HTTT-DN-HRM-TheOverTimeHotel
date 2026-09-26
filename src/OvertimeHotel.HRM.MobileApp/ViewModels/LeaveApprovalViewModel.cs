using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu và tương tác màn hình danh sách duyệt đơn (LeaveApprovalPage).
/// Hỗ trợ 2 Tab (Chờ duyệt / Đã xử lý), thanh lọc 5 loại đơn, pull-to-refresh và đếm badge.
/// </summary>
public class LeaveApprovalViewModel : INotifyPropertyChanged
{
    private readonly ILeaveApprovalService _leaveApprovalService;
    private readonly IAuthService _authService;

    private List<LeaveRequestItem> _allLoadedRequests = new();
    private bool _isLoading;
    private bool _isRefreshing;
    private string _selectedTab = "CHO_DUYET"; // "CHO_DUYET" hoặc "DA_XU_LY"
    private string _selectedTypeFilter = "ALL"; // "ALL", "1", "2", "3", "4", "5"
    private int _pendingCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LeaveRequestItem> FilteredRequests { get; } = new();

    public ICommand LoadDataCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand FilterTypeCommand { get; }
    public ICommand ViewDetailCommand { get; }

    public LeaveApprovalViewModel(ILeaveApprovalService leaveApprovalService, IAuthService authService)
    {
        _leaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoadDataCommand = new Command(async () => await LoadDataAsync());
        RefreshCommand = new Command(async () => await RefreshAsync());
        SwitchTabCommand = new Command<string>(async (tab) => await SwitchTabAsync(tab));
        FilterTypeCommand = new Command<string>(ApplyTypeFilter);
        ViewDetailCommand = new Command<LeaveRequestItem>(async (item) => await ViewDetailAsync(item));
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasNoData));
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsPendingTabSelected));
                OnPropertyChanged(nameof(IsReviewedTabSelected));
            }
        }
    }

    public bool IsPendingTabSelected => SelectedTab == "CHO_DUYET";
    public bool IsReviewedTabSelected => SelectedTab == "DA_XU_LY";

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set => SetProperty(ref _selectedTypeFilter, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        set => SetProperty(ref _pendingCount, value);
    }

    public bool HasNoData => !IsLoading && FilteredRequests.Count == 0;

    public string RoleDisplayName
    {
        get
        {
            var role = _authService.CurrentUser?.TenVaiTro ?? "Manager";
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return "Quản Trị Đơn Từ — Ban Quản Trị";
            if (role.Equals("HR", StringComparison.OrdinalIgnoreCase))
                return "Phê Duyệt Đơn — Phòng Nhân Sự";
            return "Phê Duyệt Đơn — Quản Lý Bộ Phận";
        }
    }

    public string DeptDisplayName => _authService.CurrentUser?.TenPhongBan ?? "The OverTime Hotel";

    public async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var user = _authService.CurrentUser;
            var role = user?.TenVaiTro ?? "Manager";
            var deptId = user?.MaPhongBan;

            // 1. Cập nhật số lượng đơn chờ duyệt cho badge
            PendingCount = await _leaveApprovalService.GetPendingCountAsync(role, deptId).ConfigureAwait(false);

            // 2. Nạp dữ liệu theo Tab hiện tại
            List<LeaveRequestItem> items;
            if (SelectedTab == "CHO_DUYET")
            {
                items = await _leaveApprovalService.GetPendingRequestsAsync(role, deptId).ConfigureAwait(false);
            }
            else
            {
                items = await _leaveApprovalService.GetReviewedRequestsAsync(role, deptId).ConfigureAwait(false);
            }

            _allLoadedRequests = items;

            // 3. Lọc theo Chip loại đơn hiện tại và cập nhật danh sách hiển thị
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyCurrentFilter();
                IsLoading = false;
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
            });
        }
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDataAsync().ConfigureAwait(false);
        IsRefreshing = false;
    }

    private async Task SwitchTabAsync(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab) || SelectedTab == tab) return;

        SelectedTab = tab;
        // Reset bộ lọc loại đơn về "Tất cả" khi đổi Tab
        SelectedTypeFilter = "ALL";
        await LoadDataAsync().ConfigureAwait(false);
    }

    private void ApplyTypeFilter(string? typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return;
        SelectedTypeFilter = typeId;
        ApplyCurrentFilter();
    }

    private void ApplyCurrentFilter()
    {
        FilteredRequests.Clear();

        IEnumerable<LeaveRequestItem> query = _allLoadedRequests;

        if (SelectedTypeFilter != "ALL" && int.TryParse(SelectedTypeFilter, out var typeId))
        {
            query = query.Where(r => r.MaLoaiDon == typeId);
        }

        foreach (var item in query)
        {
            FilteredRequests.Add(item);
        }

        OnPropertyChanged(nameof(HasNoData));
    }

    private async Task ViewDetailAsync(LeaveRequestItem? item)
    {
        if (item == null) return;

        var parameters = new Dictionary<string, object>
        {
            ["Item"] = item
        };

        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("LeaveDetailPage", parameters);
        }
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
