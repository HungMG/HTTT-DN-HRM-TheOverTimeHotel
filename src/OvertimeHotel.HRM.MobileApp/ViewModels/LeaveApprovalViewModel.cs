using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu và tương tác màn hình danh sách duyệt đơn (LeaveApprovalPage).
/// Tích hợp cấu trúc phản hồi gộp (items + pendingCount), cơ chế Fail-Closed và nhận diện Demo Mode.
/// </summary>
public class LeaveApprovalViewModel : INotifyPropertyChanged
{
    private readonly ILeaveApprovalService _leaveApprovalService;
    private readonly IAuthService _authService;

    private const int PageSize = 40;
    private readonly HashSet<int> _loadedIds = new();
    private Task<List<DepartmentOption>>? _departmentOptionsTask;
    private bool _departmentsLoaded;
    private DepartmentOption _selectedDepartment;
    private int _loadVersion;
    private int _nextOffset;
    private bool _hasMore;
    private bool _isLoadingMore;
    private string _loadMoreError = string.Empty;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _selectedTab = "CHO_DUYET"; // "CHO_DUYET" hoặc "DA_XU_LY"
    private string _selectedTypeFilter = "ALL"; // "ALL", "1", "2", "3", "4", "5"
    private int _pendingCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LeaveRequestItem> FilteredRequests { get; } = new();
    public ObservableCollection<DepartmentOption> DepartmentOptions { get; } =
        new() { new(null, "Tất cả phòng ban") };

    public ICommand LoadDataCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand FilterTypeCommand { get; }
    public ICommand ViewDetailCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand ToggleDepartmentDropdownCommand { get; }
    public ICommand SelectDepartmentCommand { get; }
    public ICommand CloseDepartmentDropdownCommand { get; }

    public bool IsDemoMode => _leaveApprovalService.IsDemoMode;

    public LeaveApprovalViewModel(ILeaveApprovalService leaveApprovalService, IAuthService authService)
    {
        _leaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _selectedDepartment = DepartmentOptions[0];

        LoadDataCommand = new Command(async () => await LoadDataAsync());
        RefreshCommand = new Command(async () => await RefreshAsync());
        SwitchTabCommand = new Command<string>(async (tab) => await SwitchTabAsync(tab));
        FilterTypeCommand = new Command<string>(async type => await ApplyTypeFilterAsync(type));
        ViewDetailCommand = new Command<LeaveRequestItem>(async (item) => await ViewDetailAsync(item));
        RetryCommand = new Command(async () => await LoadDataAsync());
        ResetFiltersCommand = new Command(async () => await ResetFiltersAsync());
        LoadMoreCommand = new Command(async () => await LoadMoreAsync());
        ToggleDepartmentDropdownCommand = new Command(() =>
        {
            if (IsDepartmentPickerEnabled)
                IsDepartmentDropdownOpen = !IsDepartmentDropdownOpen;
        });
        SelectDepartmentCommand = new Command<DepartmentOption>(option =>
        {
            if (option == null) return;
            SelectedDepartment = option;
            IsDepartmentDropdownOpen = false;
        });
        CloseDepartmentDropdownCommand = new Command(() =>
        {
            IsDepartmentDropdownOpen = false;
        });
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasNoData));
                OnPropertyChanged(nameof(ResultsSummary));
                OnPropertyChanged(nameof(IsListVisible));
                OnPropertyChanged(nameof(IsDepartmentPickerEnabled));
                OnPropertyChanged(nameof(CanLoadMore));
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(HasNoData));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
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
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value)) NotifyFiltersChanged();
        }
    }

    public bool CanReviewAnnualLeave => _authService.CurrentUser?.CanReviewRequest(1) == true;
    public bool CanReviewSickLeave => _authService.CurrentUser?.CanReviewRequest(2) == true;
    public bool CanReviewMaternityLeave => _authService.CurrentUser?.CanReviewRequest(3) == true;
    public bool CanReviewUnpaidLeave => _authService.CurrentUser?.CanReviewRequest(4) == true;
    public bool CanReviewResignation => _authService.CurrentUser?.CanReviewRequest(5) == true;

    public bool CanFilterDepartments => _authService.CurrentUser is { } user && (user.IsAdmin || user.IsHR);
    public bool IsDepartmentPickerEnabled => !IsLoading && !IsLoadingMore;

    private bool _isDepartmentDropdownOpen;
    public bool IsDepartmentDropdownOpen
    {
        get => _isDepartmentDropdownOpen;
        set
        {
            if (SetProperty(ref _isDepartmentDropdownOpen, value))
            {
                OnPropertyChanged(nameof(DropdownChevron));
            }
        }
    }
    public string DropdownChevron => IsDepartmentDropdownOpen ? "▲" : "▼";

    public DepartmentOption SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (value == null || !SetProperty(ref _selectedDepartment, value)) return;
            IsDepartmentDropdownOpen = false;
            NotifyFiltersChanged();
            _ = LoadDataAsync();
        }
    }
    public bool HasFilters => SelectedDepartment.MaPhongBan.HasValue || SelectedTypeFilter != "ALL";
    public bool HasMore
    {
        get => _hasMore;
        private set { if (SetProperty(ref _hasMore, value)) OnPropertyChanged(nameof(CanLoadMore)); }
    }
    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set
        {
            if (SetProperty(ref _isLoadingMore, value))
            {
                OnPropertyChanged(nameof(CanLoadMore));
                OnPropertyChanged(nameof(IsDepartmentPickerEnabled));
            }
        }
    }
    public string LoadMoreError
    {
        get => _loadMoreError;
        private set { if (SetProperty(ref _loadMoreError, value)) OnPropertyChanged(nameof(HasLoadMoreError)); }
    }
    public bool HasLoadMoreError => !string.IsNullOrEmpty(LoadMoreError);
    public bool CanLoadMore => HasMore && !IsLoading && !IsLoadingMore;
    public bool IsListVisible => !IsLoading && !HasError && FilteredRequests.Count > 0;
    public string ResultsSummary => IsLoading ? "Đang tải đơn…"
        : HasError ? "Chưa tải được danh sách"
        : HasMore ? $"{FilteredRequests.Count} đơn đã tải · Còn đơn khác"
        : $"{FilteredRequests.Count} đơn phù hợp";
    public string EmptyMessage => SelectedDepartment.MaPhongBan.HasValue
        ? $"Không có đơn phù hợp ở {SelectedDepartment.TenPhongBan}. Bạn có thể đổi phòng ban hoặc bỏ bộ lọc."
        : "Không có đơn phù hợp với trạng thái và loại đơn đang chọn.";

    public int PendingCount
    {
        get => _pendingCount;
        set => SetProperty(ref _pendingCount, value);
    }

    public bool HasNoData => !IsLoading && !HasError && FilteredRequests.Count == 0;

    public string RoleDisplayName
    {
        get
        {
            var user = _authService.CurrentUser;
            if (user == null) return "Chưa đăng nhập";

            if (user.IsAdmin) return "Quản Trị Đơn Từ — Ban Quản Trị";
            if (user.IsHR) return "Phê Duyệt Đơn — Phòng Nhân Sự";
            if (user.IsManager) return "Phê Duyệt Đơn — Quản Lý Bộ Phận";
            return "Duyệt Đơn Từ";
        }
    }

    public string DeptDisplayName => _authService.CurrentUser?.TenPhongBan ?? "The OverTime Hotel";

    public async Task LoadDataAsync()
    {
        // A newer filter wins even if the previous network response arrives last.
        var version = ++_loadVersion;
        IsLoading = true;
        IsLoadingMore = false;
        HasError = false;
        ErrorMessage = string.Empty;
        LoadMoreError = string.Empty;
        HasMore = false;
        _nextOffset = 0;
        _loadedIds.Clear();
        FilteredRequests.Clear();
        OnPropertyChanged(nameof(CanFilterDepartments));

        try
        {
            await _authService.RefreshPermissionsAsync();
            if (version != _loadVersion) return;
            var user = _authService.CurrentUser;
            OnPropertyChanged(nameof(CanReviewAnnualLeave));
            OnPropertyChanged(nameof(CanReviewSickLeave));
            OnPropertyChanged(nameof(CanReviewMaternityLeave));
            OnPropertyChanged(nameof(CanReviewUnpaidLeave));
            OnPropertyChanged(nameof(CanReviewResignation));
            if (SelectedTypeFilter != "ALL" && (!int.TryParse(SelectedTypeFilter, out var selectedType)
                || user?.CanReviewRequest(selectedType) != true))
                SelectedTypeFilter = "ALL";
            // Fail-closed: Kiểm tra quyền đăng nhập và vai trò
            if (user == null || !user.CanApproveLeave)
            {
                HasError = true;
                ErrorMessage = "Tài khoản của bạn không có thẩm quyền truy cập màn hình duyệt đơn.";
                IsLoading = false;
                return;
            }

            if (CanFilterDepartments && !_departmentsLoaded)
            {
                var departments = await (_departmentOptionsTask ??= _leaveApprovalService.GetDepartmentOptionsAsync());
                if (version != _loadVersion) return;
                foreach (var department in departments) DepartmentOptions.Add(department);
                _departmentsLoaded = true;
            }

            var response = await GetPageAsync(0);
            if (version != _loadVersion) return;
            PendingCount = response.PendingCount;
            AddPage(response);
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            _departmentOptionsTask = null;
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                NotifyResultsChanged();
            }
        }
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDataAsync();
        IsRefreshing = false;
    }

    private async Task SwitchTabAsync(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab) || SelectedTab == tab) return;

        IsDepartmentDropdownOpen = false;
        SelectedTab = tab;
        await LoadDataAsync();
    }

    private async Task ApplyTypeFilterAsync(string? typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId) || SelectedTypeFilter == typeId) return;
        if (typeId != "ALL" && (!int.TryParse(typeId, out var requestType)
            || _authService.CurrentUser?.CanReviewRequest(requestType) != true)) return;
        IsDepartmentDropdownOpen = false;
        SelectedTypeFilter = typeId;
        await LoadDataAsync();
    }

    private async Task ResetFiltersAsync()
    {
        IsDepartmentDropdownOpen = false;
        SelectedTypeFilter = "ALL";
        SetProperty(ref _selectedDepartment, DepartmentOptions[0], nameof(SelectedDepartment));
        NotifyFiltersChanged();
        await LoadDataAsync();
    }

    private Task<LeaveRequestListResponse> GetPageAsync(int offset) =>
        _leaveApprovalService.GetLeaveRequestsAsync(IsPendingTabSelected ? "pending" : "reviewed",
            SelectedTypeFilter, CanFilterDepartments ? SelectedDepartment.MaPhongBan : null, offset, PageSize);

    public async Task LoadMoreAsync()
    {
        if (!CanLoadMore || HasError) return;
        var version = _loadVersion;
        IsLoadingMore = true;
        LoadMoreError = string.Empty;
        try
        {
            var response = await GetPageAsync(_nextOffset);
            if (version == _loadVersion) AddPage(response);
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) LoadMoreError = ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoadingMore = false;
                NotifyResultsChanged();
            }
        }
    }

    private void AddPage(LeaveRequestListResponse response)
    {
        foreach (var item in MapDtoListToItems(response.Items))
            if (_loadedIds.Add(item.MaDon)) FilteredRequests.Add(item);
        _nextOffset += response.Items.Count;
        HasMore = response.HasMore;
        NotifyResultsChanged();
    }

    private void NotifyResultsChanged()
    {
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(ResultsSummary));
    }

    private void NotifyFiltersChanged()
    {
        OnPropertyChanged(nameof(HasFilters));
        OnPropertyChanged(nameof(EmptyMessage));
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

    private static List<LeaveRequestItem> MapDtoListToItems(List<LeaveRequestListItemDto> dtos)
    {
        return dtos.Select(d => new LeaveRequestItem
        {
            MaDon = d.MaDon,
            MaNhanVien = d.MaNhanVien,
            TenNhanVien = d.HoTenNhanVien,
            TenChucVu = d.ChucVu ?? "",
            MaPhongBan = d.MaPhongBan,
            TenPhongBan = d.TenPhongBan ?? "",
            MaLoaiDon = d.MaLoaiDon,
            TenLoaiDon = d.TenLoaiDon,
            CoHuongLuong = d.CoHuongLuong,
            TuNgay = d.TuNgay,
            DenNgay = d.DenNgay,
            LyDo = d.LyDo,
            NgayGui = d.NgayGui,
            TrangThai = d.TrangThai,
            PhanHoiDuyet = d.PhanHoiDuyet,
            NgayDuyet = d.NgayDuyet,
            NguoiDuyetTen = d.HoTenNguoiDuyet
        }).ToList();
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
