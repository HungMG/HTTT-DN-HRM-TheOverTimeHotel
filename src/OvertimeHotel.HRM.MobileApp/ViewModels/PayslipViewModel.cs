using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Utilities;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// Danh sách phiếu lương của người đang đăng nhập, và chi tiết 2 tầng Gross - Net của một phiếu.
/// </summary>
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
    public ObservableCollection<PayslipItemRowItem> IncomeItems { get; } = [];
    public ObservableCollection<PayslipItemRowItem> DeductionItems { get; } = [];

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
    public bool HasListVisible => !HasDetail && HasData;
    public bool HasIncomeItems => IncomeItems.Count > 0;
    public bool HasDeductionItems => DeductionItems.Count > 0;
    public bool HasNoDeductions => _summary != null && DeductionItems.Count == 0 && !IsDetailLoading;
    public bool IsEmptyDetail => _summary != null && DetailItems.Count == 0 && !IsDetailLoading;

    public decimal TotalGross => _summary == null
        ? 0m
        : (_summary.TienLuongCong + _summary.TienTangCa + _summary.TongPhuCap + _summary.TongThuong);

    public string DetailPeriodText => _summary == null ? string.Empty : new PayslipRowItem(_summary).PeriodText;
    public string DetailSlipIdText => _summary == null ? string.Empty : $"#{_summary.MaPhieuLuong:D4}";

    public string DetailStatusText => _summary == null ? string.Empty : new PayslipRowItem(_summary).StatusText;
    public string DetailStatusColor => _summary == null ? "#047857" : new PayslipRowItem(_summary).StatusColor;
    public string DetailStatusBackground => _summary == null ? "#ECFDF5" : new PayslipRowItem(_summary).StatusBackground;

    public string DetailDepartmentRole
    {
        get
        {
            if (_summary == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(_summary.TenPhongBan) && !string.IsNullOrWhiteSpace(_summary.TenChucVu))
                return $"{_summary.TenPhongBan} • {_summary.TenChucVu}";

            var user = _authService.CurrentUser;
            if (user != null && !string.IsNullOrWhiteSpace(user.TenPhongBan))
                return $"{user.TenPhongBan} • {user.TenChucVu}";

            return "Khách Sạn The OverTime";
        }
    }

    public string DetailNetText => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(_summary.ThucNhan)} ₫";

    public string DetailNetInWords => _summary == null
        ? string.Empty
        : VietnameseNumberToWords.ConvertToWords(_summary.ThucNhan);

    public string DetailGrossText => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(TotalGross)} ₫";
    public string DetailDeductionText => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(_summary.TongKhauTru)} ₫";
    public string DetailDeductionMinusText => _summary == null ? "0 ₫" : $"- {PayslipRowItem.Format(_summary.TongKhauTru)} ₫";

    public string DetailWorkDayBadge => _summary == null ? string.Empty : $"{_summary.NgayCongThucTe:0.##} / 26 công";

    public string DetailBaseSalaryText => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(_summary.LuongCoBan)} ₫";
    public string DetailWorkDayText => _summary == null ? "0 công" : $"{_summary.NgayCongThucTe:0.##} công";

    public string DetailDailyRateText => _summary == null
        ? "0 ₫/ngày"
        : $"{PayslipRowItem.Format(Math.Round(_summary.LuongCoBan / 26m, MidpointRounding.AwayFromZero))} ₫/ngày";

    public string DetailHourlyRateText => _summary == null
        ? "0 ₫/h"
        : $"{PayslipRowItem.Format(Math.Round(_summary.LuongCoBan / (26m * 8m), MidpointRounding.AwayFromZero))} ₫/h";

    public string DetailWageSalaryText => _summary == null ? "0 ₫" : $"+ {PayslipRowItem.Format(_summary.TienLuongCong)} ₫";

    public string DetailWageFormulaText => _summary == null
        ? string.Empty
        : $"({PayslipRowItem.Format(_summary.LuongCoBan)} / 26) × {_summary.NgayCongThucTe:0.##} ngày";

    public string DetailOvertimeSalaryText => _summary == null ? "0 ₫" : $"+ {PayslipRowItem.Format(_summary.TienTangCa)} ₫";
    public bool DetailHasOvertimeSalary => _summary != null && _summary.TienTangCa > 0;

    public string DetailGrossTotalText => _summary == null ? "+ 0 ₫" : $"+ {PayslipRowItem.Format(TotalGross)} ₫";
    public string DetailTotalDeductionText => _summary == null ? "- 0 ₫" : $"- {PayslipRowItem.Format(_summary.TongKhauTru)} ₫";

    public string DetailFormulaGross => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(TotalGross)} ₫";
    public string DetailFormulaDeduction => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(_summary.TongKhauTru)} ₫";
    public string DetailFormulaNet => _summary == null ? "0 ₫" : $"{PayslipRowItem.Format(_summary.ThucNhan)} ₫";

    public string EmptyText =>
        "Chưa có phiếu lương nào được gửi. Phiếu lương sẽ tự động hiển thị tại đây sau khi bảng lương được duyệt và khóa sổ trên hệ thống.";

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
                if (Payslips.Count == 1) EmployeeName = slip.HoTen;
            }
            HasData = true;
            IsEmpty = Payslips.Count == 0;
            NotifyDetail();
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
                NotifyDetail();
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
        var version = ++_detailVersion;
        _detailCancellation?.Dispose();
        _detailCancellation = new CancellationTokenSource();
        var token = _detailCancellation.Token;
        IsDetailLoading = true;
        try
        {
            _summary = Payslips.FirstOrDefault(p => p.MaPhieuLuong == maPhieuLuong)?.Source;
            DetailItems.Clear();
            IncomeItems.Clear();
            DeductionItems.Clear();
            NotifyDetail();

            var items = await _service.GetPayslipItemsAsync(maPhieuLuong, token);
            if (version != _detailVersion || token.IsCancellationRequested) return;

            foreach (var item in items)
            {
                var rowItem = new PayslipItemRowItem(item);
                DetailItems.Add(rowItem);
                if (rowItem.IsDeduction)
                    DeductionItems.Add(rowItem);
                else
                    IncomeItems.Add(rowItem);
            }
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
        IncomeItems.Clear();
        DeductionItems.Clear();
        NotifyDetail();
    }

    private void NotifyDetail()
    {
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(HasListVisible));
        OnPropertyChanged(nameof(TotalGross));
        OnPropertyChanged(nameof(DetailPeriodText));
        OnPropertyChanged(nameof(DetailSlipIdText));
        OnPropertyChanged(nameof(DetailStatusText));
        OnPropertyChanged(nameof(DetailStatusColor));
        OnPropertyChanged(nameof(DetailStatusBackground));
        OnPropertyChanged(nameof(DetailDepartmentRole));
        OnPropertyChanged(nameof(DetailNetText));
        OnPropertyChanged(nameof(DetailNetInWords));
        OnPropertyChanged(nameof(DetailGrossText));
        OnPropertyChanged(nameof(DetailDeductionText));
        OnPropertyChanged(nameof(DetailDeductionMinusText));
        OnPropertyChanged(nameof(DetailWorkDayBadge));
        OnPropertyChanged(nameof(DetailBaseSalaryText));
        OnPropertyChanged(nameof(DetailWorkDayText));
        OnPropertyChanged(nameof(DetailDailyRateText));
        OnPropertyChanged(nameof(DetailHourlyRateText));
        OnPropertyChanged(nameof(DetailWageSalaryText));
        OnPropertyChanged(nameof(DetailWageFormulaText));
        OnPropertyChanged(nameof(DetailOvertimeSalaryText));
        OnPropertyChanged(nameof(DetailHasOvertimeSalary));
        OnPropertyChanged(nameof(DetailGrossTotalText));
        OnPropertyChanged(nameof(DetailTotalDeductionText));
        OnPropertyChanged(nameof(DetailFormulaGross));
        OnPropertyChanged(nameof(DetailFormulaDeduction));
        OnPropertyChanged(nameof(DetailFormulaNet));
        OnPropertyChanged(nameof(HasIncomeItems));
        OnPropertyChanged(nameof(HasDeductionItems));
        OnPropertyChanged(nameof(HasNoDeductions));
        OnPropertyChanged(nameof(IsEmptyDetail));
        OnPropertyChanged(nameof(DetailItems));
        OnPropertyChanged(nameof(IncomeItems));
        OnPropertyChanged(nameof(DeductionItems));
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
