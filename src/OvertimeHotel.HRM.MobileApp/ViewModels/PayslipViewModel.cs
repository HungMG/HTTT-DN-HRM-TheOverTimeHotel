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
/// Phần tử đại diện cho một mốc Năm trong bộ lọc năm của màn hình phiếu lương.
/// </summary>
public class YearFilterItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Year { get; }
    public string Label { get; }
    public string Subtitle { get; }
    public bool IsCurrentYear { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubtitleColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckmarkVisibility)));
            }
        }
    }

    public string BackgroundColor => IsSelected ? "#1E3A8A" : "#FFFFFF";
    public string TextColor => IsSelected ? "#FFFFFF" : "#0F172A";
    public string SubtitleColor => IsSelected ? "#BFDBFE" : "#64748B";
    public string BorderColor => IsSelected ? "#D4AF37" : "#CBD5E1";
    public bool CheckmarkVisibility => IsSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public YearFilterItem(int year, string label, bool isSelected = false, string subtitle = "")
    {
        Year = year;
        Label = label;
        _isSelected = isSelected;
        IsCurrentYear = (year == DateTime.Now.Year);
        Subtitle = string.IsNullOrWhiteSpace(subtitle)
            ? (IsCurrentYear ? "Năm hiện tại" : "Lịch sử quyết toán")
            : subtitle;
    }
}

/// <summary>
/// Danh sách phiếu lương của người đang đăng nhập, bộ lọc năm, xuất PDF năm và chi tiết 2 tầng Gross - Net.
/// </summary>
public class PayslipViewModel : INotifyPropertyChanged
{
    private readonly IPayslipService _service;
    private readonly IAuthService _authService;
    private readonly IPayslipPdfExportService _pdfExportService;
    private readonly List<PayslipItemDto> _detailRawItems = [];
    private readonly List<PayslipRowItem> _allPayslips = [];

    private int _selectedYear = DateTime.Now.Year;
    private bool _isYearPickerOpen;
    private bool _isExportingPdf;
    private bool _isExportingYearlyPdf;
    private string _exportStatusText = string.Empty;

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
    public ObservableCollection<YearFilterItem> YearFilters { get; } = [];
    public ObservableCollection<PayslipItemRowItem> DetailItems { get; } = [];
    public ObservableCollection<PayslipItemRowItem> IncomeItems { get; } = [];
    public ObservableCollection<PayslipItemRowItem> DeductionItems { get; } = [];

    public Command RefreshCommand { get; }
    public Command<int> SelectPayslipCommand { get; }
    public Command<int> SelectYearCommand { get; }
    public Command PreviousYearCommand { get; }
    public Command NextYearCommand { get; }
    public Command OpenYearPickerCommand { get; }
    public Command CloseYearPickerCommand { get; }
    public Command CloseDetailCommand { get; }
    public Command LoginCommand { get; }
    public Command ExportPdfCommand { get; }
    public Command ExportYearlyPdfCommand { get; }
    public Command NavigateToAttendanceExplanationCommand { get; }

    public PayslipViewModel(IPayslipService service, IAuthService authService, IPayslipPdfExportService pdfExportService)
    {
        _service = service;
        _authService = authService;
        _pdfExportService = pdfExportService;
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        SelectPayslipCommand = new Command<int>(async id => await LoadDetailAsync(id));
        SelectYearCommand = new Command<int>(SelectYear);
        PreviousYearCommand = new Command(GoPreviousYear, () => CanGoPreviousYear);
        NextYearCommand = new Command(GoNextYear, () => CanGoNextYear);
        OpenYearPickerCommand = new Command(() => IsYearPickerOpen = true);
        CloseYearPickerCommand = new Command(() => IsYearPickerOpen = false);
        CloseDetailCommand = new Command(ClearDetail);
        ExportPdfCommand = new Command(async () => await ExportPdfAsync(), () => _summary != null && !IsDetailLoading && !IsExportingPdf);
        ExportYearlyPdfCommand = new Command(async () => await ExportYearlyPdfAsync(), () => CanExportYearlyPdf);
        NavigateToAttendanceExplanationCommand = new Command(async () => await NavigateToAttendanceExplanationAsync());
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
            if (Set(ref _isLoading, value))
            {
                RefreshCommand.ChangeCanExecute();
                ExportYearlyPdfCommand.ChangeCanExecute();
                OnPropertyChanged(nameof(CanExportYearlyPdf));
            }
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

    public bool IsYearPickerOpen
    {
        get => _isYearPickerOpen;
        set => Set(ref _isYearPickerOpen, value);
    }

    public int SelectedYear
    {
        get => _selectedYear;
        private set
        {
            if (value <= 0) return;
            if (Set(ref _selectedYear, value))
            {
                OnPropertyChanged(nameof(SelectedYearDisplay));
                OnPropertyChanged(nameof(SelectedYearText));
                OnPropertyChanged(nameof(YearSubtitleText));
                OnPropertyChanged(nameof(ExportYearlyButtonText));
                OnPropertyChanged(nameof(CanExportYearlyPdf));
                OnPropertyChanged(nameof(CanGoPreviousYear));
                OnPropertyChanged(nameof(CanGoNextYear));
                OnPropertyChanged(nameof(PreviousYearBgColor));
                OnPropertyChanged(nameof(PreviousYearTextColor));
                OnPropertyChanged(nameof(NextYearBgColor));
                OnPropertyChanged(nameof(NextYearTextColor));
                PreviousYearCommand.ChangeCanExecute();
                NextYearCommand.ChangeCanExecute();
                ExportYearlyPdfCommand.ChangeCanExecute();
            }
        }
    }

    public string SelectedYearDisplay => $"Năm {_selectedYear}";
    public string YearSubtitleText => _selectedYear == DateTime.Now.Year ? "Năm hiện tại" : "Lịch sử quyết toán";
    public string SelectedYearText => $"Năm {_selectedYear}";

    public bool CanGoPreviousYear => YearFilters.Any(y => y.Year < _selectedYear);
    public bool CanGoNextYear => YearFilters.Any(y => y.Year > _selectedYear);

    public string PreviousYearBgColor => CanGoPreviousYear ? "#FFFFFF" : "#F1F5F9";
    public string PreviousYearTextColor => CanGoPreviousYear ? "#1E3A8A" : "#94A3B8";

    public string NextYearBgColor => CanGoNextYear ? "#FFFFFF" : "#F1F5F9";
    public string NextYearTextColor => CanGoNextYear ? "#1E3A8A" : "#94A3B8";

    public bool HasDetail => _summary != null;
    public bool HasListVisible => !HasDetail && HasData;
    public bool IsListHeaderVisible => !HasDetail;
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

    public bool IsDetailDraft => _summary?.IsDraft ?? false;

    public string DetailDraftDeadlineText => _summary?.HanPhanHoi.HasValue == true
        ? $"Hạn đối soát: {_summary.HanPhanHoi.Value.ToOffset(TimeSpan.FromHours(7)):HH:mm 'ngày' dd/MM/yyyy}"
        : "Đang mở tiếp nhận ý kiến đối soát";

    public string DetailDraftReviewNote => _summary?.HanPhanHoi.HasValue == true
        ? $"Phiếu lương nháp kỳ này đang được mở để nhân viên kiểm tra đối chiếu trước khi trình Ban Giám Đốc phê duyệt. Hạn chót phản hồi: {_summary.HanPhanHoi.Value.ToOffset(TimeSpan.FromHours(7)):HH:mm 'ngày' dd/MM/yyyy}. Nếu phát hiện sai sót giờ công hoặc phụ cấp, vui lòng bấm nút bên dưới để gửi đơn giải trình chấm công."
        : "Phiếu lương nháp kỳ này đang được mở để nhân viên kiểm tra đối chiếu trước khi trình Ban Giám Đốc phê duyệt. Nếu phát hiện sai sót giờ công hoặc phụ cấp, vui lòng bấm nút bên dưới để gửi đơn giải trình chấm công.";

    public string DetailDepartmentRole
    {
        get
        {
            if (_summary != null)
            {
                if (!string.IsNullOrWhiteSpace(_summary.TenPhongBan) && !string.IsNullOrWhiteSpace(_summary.TenChucVu))
                    return $"{_summary.TenPhongBan} • {_summary.TenChucVu}";
            }

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

    public string DetailBalanceText => IsDetailDraft ? "Bản nháp đang mở đối soát · Chưa khóa sổ chính thức" : "Bất biến cân bằng 100% · Đã khóa sổ & phát hành chính thức";
    public string DetailBalanceColor => IsDetailDraft ? "#B45309" : "#047857";
    public string DetailBalanceIcon => IsDetailDraft ? "⏱" : "✔";

    public bool IsExportingPdf
    {
        get => _isExportingPdf;
        private set
        {
            if (Set(ref _isExportingPdf, value))
            {
                ExportPdfCommand.ChangeCanExecute();
                OnPropertyChanged(nameof(ExportButtonText));
            }
        }
    }

    public bool IsExportingYearlyPdf
    {
        get => _isExportingYearlyPdf;
        private set
        {
            if (Set(ref _isExportingYearlyPdf, value))
            {
                ExportYearlyPdfCommand.ChangeCanExecute();
                OnPropertyChanged(nameof(ExportYearlyButtonText));
                OnPropertyChanged(nameof(CanExportYearlyPdf));
            }
        }
    }

    public string ExportStatusText { get => _exportStatusText; private set => Set(ref _exportStatusText, value); }
    public bool HasExportStatus => !string.IsNullOrWhiteSpace(_exportStatusText);

    public string ExportButtonText => IsExportingPdf ? "Đang xuất PDF..." : "⤓ Xuất PDF";
    public bool CanExportPdf => HasDetail && !IsDetailLoading && !IsExportingPdf;

    public string ExportYearlyButtonText => IsExportingYearlyPdf
        ? "Đang in..."
        : $"⤓ Bảng Lương {_selectedYear}";

    public bool CanExportYearlyPdf =>
        HasListVisible &&
        !IsLoading &&
        !IsExportingYearlyPdf &&
        _selectedYear > 0 &&
        _allPayslips.Any(p => p.Source.Nam == _selectedYear);

    public string EmptyText => _selectedYear > 0
        ? $"Chưa có phiếu lương được quyết toán trong năm {_selectedYear}. Phiếu lương sẽ tự động hiển thị tại đây sau khi bảng lương được duyệt và khóa sổ trên hệ thống."
        : "Chưa có phiếu lương nào phù hợp trong năm được chọn.";

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
        _allPayslips.Clear();
        ClearDetail();
        try
        {
            if (_authService.CurrentUser?.IsLoggedIn != true)
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem phiếu lương.");

            if (!string.IsNullOrWhiteSpace(_authService.CurrentUser.HoTen))
                EmployeeName = _authService.CurrentUser.HoTen;

            var slips = await _service.GetPayslipsAsync(token);
            if (version != _loadVersion || token.IsCancellationRequested) return;

            foreach (var slip in slips)
            {
                var rowItem = new PayslipRowItem(slip);
                _allPayslips.Add(rowItem);
                if (string.IsNullOrWhiteSpace(EmployeeName) && !string.IsNullOrWhiteSpace(slip.HoTen))
                    EmployeeName = slip.HoTen;
            }

            // Xây dựng danh sách mốc Năm lọc động từ dữ liệu thực tế (chỉ năm cụ thể, tuyệt đối không có "Tất cả")
            var distinctYears = _allPayslips
                .Where(p => p.Source.Nam > 0)
                .Select(p => p.Source.Nam)
                .Distinct()
                .ToList();

            var currentYear = DateTime.Now.Year;
            if (!distinctYears.Contains(currentYear))
            {
                distinctYears.Add(currentYear);
            }

            var sortedYears = distinctYears.OrderByDescending(y => y).ToList();

            // Mặc định luôn là năm hiện tại theo yêu cầu người dùng
            if (_selectedYear <= 0 || !sortedYears.Contains(_selectedYear))
            {
                _selectedYear = sortedYears.Contains(currentYear) ? currentYear : sortedYears[0];
            }

            YearFilters.Clear();
            // Tuyệt đối không thêm "Tất cả" - mỗi lựa chọn là một năm cụ thể
            foreach (var y in sortedYears)
            {
                YearFilters.Add(new YearFilterItem(
                    y,
                    $"Năm {y}",
                    _selectedYear == y,
                    y == currentYear ? "Năm hiện tại" : "Lịch sử quyết toán"));
            }

            ApplyYearFilter();
            HasData = true;
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
                ExportYearlyPdfCommand.ChangeCanExecute();
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
        _allPayslips.Clear();
        HasData = false;
        IsEmpty = false;
        ClearDetail();
    }

    public void SelectYear(int year)
    {
        if (year <= 0) return;
        SelectedYear = year;
        foreach (var item in YearFilters)
        {
            item.IsSelected = (item.Year == year);
        }
        IsYearPickerOpen = false;
        ApplyYearFilter();
    }

    private void GoPreviousYear()
    {
        var prev = YearFilters
            .Where(y => y.Year < _selectedYear)
            .OrderByDescending(y => y.Year)
            .FirstOrDefault();

        if (prev != null)
        {
            SelectYear(prev.Year);
        }
    }

    private void GoNextYear()
    {
        var next = YearFilters
            .Where(y => y.Year > _selectedYear)
            .OrderBy(y => y.Year)
            .FirstOrDefault();

        if (next != null)
        {
            SelectYear(next.Year);
        }
    }

    private void ApplyYearFilter()
    {
        Payslips.Clear();
        var filtered = _allPayslips.Where(p => p.Source.Nam == _selectedYear);

        foreach (var item in filtered)
        {
            Payslips.Add(item);
        }

        IsEmpty = Payslips.Count == 0;
        OnPropertyChanged(nameof(EmptyText));
        ExportYearlyPdfCommand.ChangeCanExecute();
        OnPropertyChanged(nameof(CanExportYearlyPdf));
        OnPropertyChanged(nameof(CanGoPreviousYear));
        OnPropertyChanged(nameof(CanGoNextYear));
        OnPropertyChanged(nameof(PreviousYearBgColor));
        OnPropertyChanged(nameof(PreviousYearTextColor));
        OnPropertyChanged(nameof(NextYearBgColor));
        OnPropertyChanged(nameof(NextYearTextColor));
        PreviousYearCommand.ChangeCanExecute();
        NextYearCommand.ChangeCanExecute();
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
            _summary = _allPayslips.FirstOrDefault(p => p.MaPhieuLuong == maPhieuLuong)?.Source;
            _detailRawItems.Clear();
            ExportStatusText = string.Empty;
            DetailItems.Clear();
            IncomeItems.Clear();
            DeductionItems.Clear();
            NotifyDetail();
            ExportPdfCommand.ChangeCanExecute();

            var items = await _service.GetPayslipItemsAsync(maPhieuLuong, token);
            if (version != _detailVersion || token.IsCancellationRequested) return;

            _detailRawItems.Clear();
            foreach (var item in items)
            {
                _detailRawItems.Add(item);
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

    private async Task NavigateToAttendanceExplanationAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("AttendanceExplanationPage");
        }
    }

    public void ClearDetail()
    {
        _summary = null;
        _detailRawItems.Clear();
        ExportStatusText = string.Empty;
        DetailItems.Clear();
        IncomeItems.Clear();
        DeductionItems.Clear();
        NotifyDetail();
        ExportPdfCommand.ChangeCanExecute();
        ExportYearlyPdfCommand.ChangeCanExecute();
    }

    private async Task ExportPdfAsync()
    {
        if (_summary == null || IsDetailLoading || IsExportingPdf) return;
        var summary = _summary!;
        var items = _detailRawItems.ToArray();
        IsExportingPdf = true;
        ExportStatusText = "Đang tạo file PDF phiếu lương...";
        NotifyDetail();
        try
        {
            var path = await _pdfExportService.ExportAsync(summary, items);
            var fileName = Path.GetFileName(path);
            ExportStatusText = $"Đã tạo PDF: {fileName} (Đã lưu vào mục Tải về)";

            try
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Phiếu lương tháng {summary.Thang:00}/{summary.Nam:0000}",
                    File = new ShareFile(path, "application/pdf"),
                });
            }
            catch (OperationCanceledException)
            {
                // Người dùng không chọn chia sẻ — file PDF vẫn đã được tạo và lưu
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PayslipPdf] Lỗi xuất PDF: {ex}");
            ExportStatusText = $"Lỗi tạo PDF: {ex.Message}";
        }
        finally
        {
            IsExportingPdf = false;
            NotifyDetail();
            ExportPdfCommand.ChangeCanExecute();
        }
    }

    private async Task ExportYearlyPdfAsync()
    {
        if (IsLoading || IsExportingYearlyPdf || _allPayslips.Count == 0) return;

        var targetYear = SelectedYear > 0
            ? SelectedYear
            : (_allPayslips.FirstOrDefault(p => p.Source.Nam > 0)?.Source.Nam ?? DateTime.Now.Year);

        var slipsInYear = _allPayslips
            .Where(p => p.Source.Nam == targetYear)
            .Select(p => p.Source)
            .ToList();

        if (slipsInYear.Count == 0)
        {
            slipsInYear = _allPayslips.Select(p => p.Source).ToList();
        }

        IsExportingYearlyPdf = true;
        ExportStatusText = $"Đang tạo bảng thu nhập năm {targetYear}...";
        NotifyDetail();

        try
        {
            var empName = !string.IsNullOrWhiteSpace(EmployeeName)
                ? EmployeeName
                : (_authService.CurrentUser?.HoTen ?? "Nhân viên");
            var deptRole = DetailDepartmentRole;

            var path = await _pdfExportService.ExportYearlyAsync(targetYear, empName, deptRole, slipsInYear);
            var fileName = Path.GetFileName(path);
            ExportStatusText = $"Đã tạo bảng thu nhập năm {targetYear}: {fileName} (Đã lưu vào mục Tải về)";

            try
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Bảng tổng hợp thu nhập năm {targetYear} - {empName}",
                    File = new ShareFile(path, "application/pdf"),
                });
            }
            catch (OperationCanceledException)
            {
                // Người dùng hủy bảng chia sẻ
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[YearlyPayslipPdf] Lỗi xuất PDF năm: {ex}");
            ExportStatusText = $"Lỗi tạo PDF năm: {ex.Message}";
        }
        finally
        {
            IsExportingYearlyPdf = false;
            NotifyDetail();
            ExportYearlyPdfCommand.ChangeCanExecute();
        }
    }

    private void NotifyDetail()
    {
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(HasListVisible));
        OnPropertyChanged(nameof(IsListHeaderVisible));
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
        OnPropertyChanged(nameof(CanExportPdf));
        OnPropertyChanged(nameof(CanExportYearlyPdf));
        OnPropertyChanged(nameof(ExportYearlyButtonText));
        OnPropertyChanged(nameof(SelectedYearText));
        OnPropertyChanged(nameof(SelectedYearDisplay));
        OnPropertyChanged(nameof(YearSubtitleText));
        OnPropertyChanged(nameof(CanGoPreviousYear));
        OnPropertyChanged(nameof(CanGoNextYear));
        OnPropertyChanged(nameof(PreviousYearBgColor));
        OnPropertyChanged(nameof(PreviousYearTextColor));
        OnPropertyChanged(nameof(NextYearBgColor));
        OnPropertyChanged(nameof(NextYearTextColor));
        OnPropertyChanged(nameof(HasExportStatus));
        OnPropertyChanged(nameof(DetailItems));
        OnPropertyChanged(nameof(IncomeItems));
        OnPropertyChanged(nameof(DeductionItems));
        OnPropertyChanged(nameof(IsDetailDraft));
        OnPropertyChanged(nameof(DetailDraftDeadlineText));
        OnPropertyChanged(nameof(DetailDraftReviewNote));
        OnPropertyChanged(nameof(DetailBalanceText));
        OnPropertyChanged(nameof(DetailBalanceColor));
        OnPropertyChanged(nameof(DetailBalanceIcon));
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
