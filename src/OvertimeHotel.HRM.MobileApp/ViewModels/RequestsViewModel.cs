using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel chính cho tab Đơn từ (thay thế tab Giấy Tờ cũ của nhân viên).
/// Quản lý và theo dõi các luồng đơn từ: Nghỉ phép, Giải trình chấm công, Yêu cầu đổi ca.
/// </summary>
public class RequestsViewModel : INotifyPropertyChanged
{
    private readonly ILeaveApprovalService _leaveService;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private string _errorMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LeaveRequestItem> RecentRequests { get; } = [];

    public Command LoadCommand { get; }
    public Command RefreshCommand { get; }
    public Command CreateLeaveCommand { get; }
    public Command CreateExplanationCommand { get; }
    public Command CreateShiftSwapCommand { get; }
    public Command<LeaveRequestItem> SelectRequestCommand { get; }
    public Command<LeaveRequestItem> WithdrawCommand { get; }

    public RequestsViewModel(ILeaveApprovalService leaveService)
    {
        _leaveService = leaveService ?? throw new ArgumentNullException(nameof(leaveService));

        LoadCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        RefreshCommand = new Command(async () => await LoadAsync(isRefresh: true), () => !IsLoading && !IsRefreshing);
        CreateLeaveCommand = new Command(async () => await Shell.Current.GoToAsync("LeaveSubmitPage"));
        CreateExplanationCommand = new Command(async () => await Shell.Current.GoToAsync("AttendanceExplanationPage"));
        CreateShiftSwapCommand = new Command(async () => await Shell.Current.GoToAsync("ShiftSwapPage"));
        SelectRequestCommand = new Command<LeaveRequestItem>(async (item) => await OnSelectRequestAsync(item));
        WithdrawCommand = new Command<LeaveRequestItem>(async (item) => await OnWithdrawRequestAsync(item));
    }

    public bool IsDemoMode => _leaveService.IsDemoMode;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasRequests));
                OnPropertyChanged(nameof(HasPendingRequests));
                OnPropertyChanged(nameof(IsEmpty));
                LoadCommand.ChangeCanExecute();
                RefreshCommand.ChangeCanExecute();
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                RefreshCommand.ChangeCanExecute();
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasRequests => !IsLoading && RecentRequests.Count > 0;
    public bool HasPendingRequests => !IsLoading && PendingCount > 0;
    public bool IsEmpty => !IsLoading && !HasError && RecentRequests.Count == 0;
    public int PendingCount => RecentRequests.Count(r => r.TrangThai == "CHO_DUYET");

    public async Task LoadAsync(bool isRefresh = false)
    {
        if (IsLoading || (isRefresh && IsRefreshing)) return;

        var version = ++_loadVersion;
        if (_loadCancellation != null)
        {
            try { _loadCancellation.Cancel(); } catch (ObjectDisposedException) { }
            _loadCancellation.Dispose();
        }
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;

        if (isRefresh)
        {
            IsRefreshing = true;
        }
        else
        {
            IsLoading = true;
        }

        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var requests = await _leaveService.GetMyRequestsAsync(chiChoDuyet: false, token);

            if (version != _loadVersion || token.IsCancellationRequested) return;

            RecentRequests.Clear();
            foreach (var req in requests)
            {
                RecentRequests.Add(req);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Bị hủy bởi lượt tải mới hơn hoặc người dùng rời trang, không coi là lỗi
        }
        catch (Exception)
        {
            if (version != _loadVersion || token.IsCancellationRequested) return;
            HasError = true;
            ErrorMessage = "Không thể tải danh sách đơn từ. Vui lòng kéo xuống để thử lại.";
        }
        finally
        {
            if (version == _loadVersion && !token.IsCancellationRequested)
            {
                IsLoading = false;
                IsRefreshing = false;
                OnPropertyChanged(nameof(HasRequests));
                OnPropertyChanged(nameof(HasPendingRequests));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(PendingCount));
            }
        }
    }

    public void CancelLoad()
    {
        ++_loadVersion;
        if (_loadCancellation != null)
        {
            try { _loadCancellation.Cancel(); } catch (ObjectDisposedException) { }
            _loadCancellation.Dispose();
            _loadCancellation = null;
        }
        IsLoading = false;
        IsRefreshing = false;
        OnPropertyChanged(nameof(HasRequests));
        OnPropertyChanged(nameof(HasPendingRequests));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static async Task OnSelectRequestAsync(LeaveRequestItem? item)
    {
        if (item == null) return;
        await Shell.Current.GoToAsync("LeaveSubmitPage");
    }

    private async Task OnWithdrawRequestAsync(LeaveRequestItem? item)
    {
        if (item == null || !item.CanWithdraw) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Xác nhận rút đơn",
            $"Bạn có chắc chắn muốn rút đơn {item.TenLoaiDon} (từ {item.TuNgay:dd/MM/yyyy} đến {item.DenNgay:dd/MM/yyyy}) không?",
            "Rút đơn",
            "Đóng");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            var result = await _leaveService.WithdrawRequestAsync(item.MaDon);
            if (result.Success)
            {
                await Shell.Current.DisplayAlertAsync("Thành công", "Đã rút đơn nghỉ phép thành công.", "Đóng");
                await LoadAsync(isRefresh: false);
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Không thể rút đơn", result.Message, "Đóng");
                await LoadAsync(isRefresh: false);
            }
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("Lỗi", "Không thể rút đơn lúc này. Vui lòng thử lại sau.", "Đóng");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
