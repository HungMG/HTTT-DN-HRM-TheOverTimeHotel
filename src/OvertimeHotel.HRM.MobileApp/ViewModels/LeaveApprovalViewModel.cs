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

    private List<LeaveRequestItem> _allLoadedRequests = new();
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private string _errorMessage = string.Empty;
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
    public ICommand RetryCommand { get; }

    public bool IsDemoMode => _leaveApprovalService.IsDemoMode;

    public LeaveApprovalViewModel(ILeaveApprovalService leaveApprovalService, IAuthService authService)
    {
        _leaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoadDataCommand = new Command(async () => await LoadDataAsync());
        RefreshCommand = new Command(async () => await RefreshAsync());
        SwitchTabCommand = new Command<string>(async (tab) => await SwitchTabAsync(tab));
        FilterTypeCommand = new Command<string>(ApplyTypeFilter);
        ViewDetailCommand = new Command<LeaveRequestItem>(async (item) => await ViewDetailAsync(item));
        RetryCommand = new Command(async () => await LoadDataAsync());
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

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(HasNoData));
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
        set => SetProperty(ref _selectedTypeFilter, value);
    }

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
        if (IsLoading) return;
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var user = _authService.CurrentUser;
            // Fail-closed: Kiểm tra quyền đăng nhập và vai trò
            if (user == null || !user.CanApproveLeave)
            {
                HasError = true;
                ErrorMessage = "Tài khoản của bạn không có thẩm quyền truy cập màn hình duyệt đơn.";
                IsLoading = false;
                return;
            }

            var viewParam = SelectedTab == "CHO_DUYET" ? "pending" : "reviewed";
            var typeParam = SelectedTypeFilter == "ALL" ? "all" : SelectedTypeFilter;

            // Gọi API duy nhất trả về cả danh sách và số lượng pending
            var response = await _leaveApprovalService.GetLeaveRequestsAsync(viewParam, typeParam).ConfigureAwait(false);

            PendingCount = response.PendingCount;
            _allLoadedRequests = MapDtoListToItems(response.Items);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyCurrentFilter();
                IsLoading = false;
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsLoading = false;
                HasError = true;
                ErrorMessage = ex.Message;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsLoading = false;
                HasError = true;
                ErrorMessage = ex.Message;
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
