using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// Danh sách phiếu lương của người đang đăng nhập, và chi tiết của một phiếu.
/// </summary>
/// <remarks>
/// Cùng khung phiên bản + huỷ lệnh với <see cref="ScheduleViewModel"/>: bấm phiếu
/// khác khi đang tải phải bỏ kết quả cũ, không để hai lần tải dồn lên nhau.
/// </remarks>
public class PayslipViewModel : INotifyPropertyChanged
{
    private readonly IPayslipService _service;
    private readonly IAuthService _authService;

    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _detailCancellation;
    private int _loadVersion;
    private int _detailVersion;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasError;
    private bool _requiresLogin;
    private bool _hasData;
    private bool _isEmpty;
    private bool _isDetailLoading;
    private string _errorMessage = "";
    private string _employeeName = string.Empty;
    private PayslipSummaryDto? _summary;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PayslipRowItem> Payslips { get; } = [];
    public ObservableCollection<PayslipItemRowItem> DetailItems { get; } = [];

    public Command RefreshCommand { get; }
    public Command<int> SelectPayslipCommand { get; }
    public Command CloseDetailCommand { get; }
    public Command LoginCommand { get; }

    public PayslipViewModel(IPayslipService service, IAuthService authService)
    {
        _service = service;
        _authService = authService;
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        SelectPayslipCommand = new Command<int>(async id => await LoadDetailAsync(id));
        CloseDetailCommand = new Command(ClearDetail);
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
            if (Set(ref _isLoading, value)) RefreshCommand.ChangeCanExecute();
        }
    }
    public bool IsRefreshing { get => _isRefreshing; set => Set(ref _isRefreshing, value); }
    public bool HasError { get => _hasError; private set => Set(ref _hasError, value); }
    public bool RequiresLogin { get => _requiresLogin; private set => Set(ref _requiresLogin, value); }
    public bool HasData { get => _hasData; private set => Set(ref _hasData, value); }
    public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }
    public bool IsDetailLoading { get => _isDetailLoading; private set => Set(ref _isDetailLoading, value); }
    public string ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }
    public string EmployeeName
    {
        get => _employeeName;
        private set => Set(ref _employeeName, value);
    }

    public bool HasDetail => _summary != null;

    /// <summary>Phiếu đang mở mà không có dòng thu chi nào để hiện.</summary>
    public bool IsEmptyDetail => _summary != null && DetailItems.Count == 0 && !IsDetailLoading;

    public string DetailPeriodText => _summary == null ? string.Empty : new PayslipRowItem(_summary).PeriodText;
    public string DetailNetText => _summary == null ? string.Empty : PayslipRowItem.Format(_summary.ThucNhan);
    public string DetailEarnedText => _summary == null ? string.Empty : PayslipRowItem.Format(
        _summary.TienLuongCong + _summary.TienTangCa + _summary.TongPhuCap + _summary.TongThuong);
    public string DetailDeductionText => _summary == null ? string.Empty : PayslipRowItem.Format(_summary.TongKhauTru);
    public string DetailWorkDayText => _summary == null ? string.Empty : $"{_summary.NgayCongThucTe:0.##} ngày công";

    public string EmptyText =>
        "Chưa có phiếu lương nào được lập. Phiếu lương do khách sạn chốt sau kỳ tính lương.";

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
        HasData = false;
        IsEmpty = false;
        Payslips.Clear();
        ClearDetail();
        try
        {
            if (_authService.CurrentUser?.IsLoggedIn != true)
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem phiếu lương.");

            var slips = await _service.GetPayslipsAsync(token);
            if (version != _loadVersion || token.IsCancellationRequested) return;

            foreach (var slip in slips)
            {
                Payslips.Add(new PayslipRowItem(slip));
                // Tên lấy từ hồ sơ nên giống nhau ở mọi phiếu — chỉ đọc dòng đầu.
                if (Payslips.Count == 1) EmployeeName = slip.HoTen;
            }
            HasData = true;
            IsEmpty = Payslips.Count == 0;
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
                JsonException => "Dữ liệu phiếu lương chưa hợp lệ. Vui lòng thử lại sau.",
                _ => "Không thể tải phiếu lương lúc này. Vui lòng thử lại."
            };
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                IsRefreshing = false;
                RefreshCommand.ChangeCanExecute();
            }
        }
    }

    public void CancelLoad()
    {
        ++_loadVersion;
        _loadCancellation?.Cancel();
        ++_detailVersion;
        _detailCancellation?.Cancel();
        IsLoading = false;
        IsRefreshing = false;
        IsDetailLoading = false;
        Payslips.Clear();
        HasData = false;
        IsEmpty = false;
        ClearDetail();
    }

    private async Task LoadDetailAsync(int maPhieuLuong)
    {
        if (IsDetailLoading || maPhieuLuong <= 0) return;
        // Phiên bản RIÊNG cho tải chi tiết: dùng chung _loadVersion với tải danh
        // sách sẽ vô hiệu hoá lần tải danh sách đang dở, khiến danh sách trống.
        var version = ++_detailVersion;
        _detailCancellation?.Dispose();
        _detailCancellation = new CancellationTokenSource();
        var token = _detailCancellation.Token;
        IsDetailLoading = true;
        try
        {
            var items = await _service.GetPayslipItemsAsync(maPhieuLuong, token);
            if (version != _detailVersion || token.IsCancellationRequested) return;

            _summary = Payslips.FirstOrDefault(p => p.MaPhieuLuong == maPhieuLuong)?.Source;
            DetailItems.Clear();
            foreach (var item in items) DetailItems.Add(new PayslipItemRowItem(item));
            OnPropertyChanged(nameof(HasDetail));
            NotifyDetail();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version != _detailVersion) return;
            ErrorMessage = ex is InvalidOperationException or UnauthorizedAccessException
                ? ex.Message
                : "Không thể tải chi tiết phiếu lương. Vui lòng thử lại.";
        }
        finally
        {
            if (version == _detailVersion)
            {
                IsDetailLoading = false;
                NotifyDetail();
            }
        }
    }

    private void ClearDetail()
    {
        _summary = null;
        DetailItems.Clear();
        OnPropertyChanged(nameof(HasDetail));
        NotifyDetail();
    }

    private void NotifyDetail()
    {
        foreach (var property in new[] { nameof(DetailPeriodText), nameof(DetailNetText),
                     nameof(DetailEarnedText), nameof(DetailDeductionText), nameof(DetailWorkDayText),
                     nameof(IsEmptyDetail) })
            OnPropertyChanged(property);
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