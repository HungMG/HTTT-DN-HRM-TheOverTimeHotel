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
/// ViewModel quản lý màn hình Chấm công Check-in/Check-out (Task 17).
/// Mọi thông báo đều đi qua Custom Modal Popup, không dùng DisplayAlert của hệ điều hành.
/// </summary>
public class AttendanceViewModel : INotifyPropertyChanged
{
    private readonly IAttendanceService _attendanceService;
    private readonly IGeoLocationService _geoLocationService;

    // Trạng thái ca làm việc hôm nay
    private int _maPhanCa;
    private bool _hasShiftToday;
    private string _shiftName = string.Empty;
    private string _shiftTimeText = string.Empty;
    private bool _daCheckIn;
    private bool _daCheckOut;
    private string _checkInTimeText = "--:--";
    private string _checkOutTimeText = "--:--";
    private int _phutDiTre;
    private int _phutVeSom;
    private decimal _gioOt;
    private string _statusText = string.Empty;
    private string _statusBgColor = "#F1F5F9";
    private string _statusTextColor = "#475569";

    // Trạng thái tải và thao tác
    private bool _isLoading = true;
    private bool _isBusy;
    private string _actionButtonText = "QUẸT THẺ VÀO";
    private string _actionButtonIcon = "◉";
    private string _actionButtonColor = "#1E3A8A";

    // Custom Modal Popup
    private bool _isPopupVisible;
    private string _popupTitle = string.Empty;
    private string _popupMessage = string.Empty;
    private string _popupButtonText = "ĐỒNG Ý";
    private string _popupIconText = "★";
    private string _popupIconBgColor = "#FFFBEB";
    private string _popupIconTextColor = "#B45309";
    private string _popupBorderColor = "#D4AF37";

    private int _monthLabelYear = DateTime.Now.Year;
    private int _monthLabelMonth = DateTime.Now.Month;

    public AttendanceViewModel(IAttendanceService attendanceService, IGeoLocationService geoLocationService)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        _geoLocationService = geoLocationService ?? throw new ArgumentNullException(nameof(geoLocationService));

        CheckInCommand = new Command(async () => await ExecuteCheckInAsync(), () => CanPunchCard);
        CheckOutCommand = new Command(async () => await ExecuteCheckOutAsync(), () => CanPunchCard);
        RefreshCommand = new Command(async () => await LoadDataAsync(), () => !IsBusy);
        ClosePopupCommand = new Command(ExecuteClosePopup);
        PreviousMonthCommand = new Command(() => ShiftMonth(-1));
        NextMonthCommand = new Command(() => ShiftMonth(1));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Thuộc tính ràng buộc

    public int MaPhanCa
    {
        get => _maPhanCa;
        private set => SetProperty(ref _maPhanCa, value);
    }

    public bool HasShiftToday
    {
        get => _hasShiftToday;
        private set
        {
            if (SetProperty(ref _hasShiftToday, value))
            {
                OnPropertyChanged(nameof(HasNoShiftToday));
                OnPropertyChanged(nameof(CanPunchCard));
                RefreshCommandState();
            }
        }
    }

    /// <summary>Trạng thái nghịch đảo, dùng để ẩn khối giao diện khi hôm nay không có ca.</summary>
    public bool HasNoShiftToday => !HasShiftToday;

    public string ShiftName
    {
        get => _shiftName;
        private set => SetProperty(ref _shiftName, value);
    }

    public string ShiftTimeText
    {
        get => _shiftTimeText;
        private set => SetProperty(ref _shiftTimeText, value);
    }

    public bool DaCheckIn
    {
        get => _daCheckIn;
        private set => SetProperty(ref _daCheckIn, value);
    }

    public bool DaCheckOut
    {
        get => _daCheckOut;
        private set => SetProperty(ref _daCheckOut, value);
    }

    public string CheckInTimeText
    {
        get => _checkInTimeText;
        private set => SetProperty(ref _checkInTimeText, value);
    }

    public string CheckOutTimeText
    {
        get => _checkOutTimeText;
        private set => SetProperty(ref _checkOutTimeText, value);
    }

    public int PhutDiTre
    {
        get => _phutDiTre;
        private set
        {
            if (SetProperty(ref _phutDiTre, value))
            {
                OnPropertyChanged(nameof(PhutDiTreText));
            }
        }
    }

    public int PhutVeSom
    {
        get => _phutVeSom;
        private set
        {
            if (SetProperty(ref _phutVeSom, value))
            {
                OnPropertyChanged(nameof(PhutVeSomText));
            }
        }
    }

    public decimal GioOt
    {
        get => _gioOt;
        private set
        {
            if (SetProperty(ref _gioOt, value))
            {
                OnPropertyChanged(nameof(GioOtText));
            }
        }
    }

    public string PhutDiTreText => PhutDiTre > 0 ? $"{PhutDiTre} phút" : "0 phút";

    public string PhutVeSomText => PhutVeSom > 0 ? $"{PhutVeSom} phút" : "0 phút";

    public string GioOtText => GioOt > 0m ? $"{GioOt:0.##} giờ" : "0 giờ";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusBgColor
    {
        get => _statusBgColor;
        private set => SetProperty(ref _statusBgColor, value);
    }

    public string StatusTextColor
    {
        get => _statusTextColor;
        private set => SetProperty(ref _statusTextColor, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanPunchCard));
                RefreshCommandState();
            }
        }
    }

    /// <summary>Chỉ cho phép quẹt thẻ khi có ca hôm nay và không đang bận xử lý yêu cầu.</summary>
    public bool CanPunchCard => HasShiftToday && !IsBusy;

    public string ActionButtonText
    {
        get => _actionButtonText;
        private set => SetProperty(ref _actionButtonText, value);
    }

    public string ActionButtonIcon
    {
        get => _actionButtonIcon;
        private set => SetProperty(ref _actionButtonIcon, value);
    }

    public string ActionButtonColor
    {
        get => _actionButtonColor;
        private set => SetProperty(ref _actionButtonColor, value);
    }

    public bool IsPopupVisible
    {
        get => _isPopupVisible;
        private set => SetProperty(ref _isPopupVisible, value);
    }

    public string PopupTitle
    {
        get => _popupTitle;
        private set => SetProperty(ref _popupTitle, value);
    }

    public string PopupMessage
    {
        get => _popupMessage;
        private set => SetProperty(ref _popupMessage, value);
    }

    public string PopupButtonText
    {
        get => _popupButtonText;
        private set => SetProperty(ref _popupButtonText, value);
    }

    public string PopupIconText
    {
        get => _popupIconText;
        private set => SetProperty(ref _popupIconText, value);
    }

    public string PopupIconBgColor
    {
        get => _popupIconBgColor;
        private set => SetProperty(ref _popupIconBgColor, value);
    }

    public string PopupIconTextColor
    {
        get => _popupIconTextColor;
        private set => SetProperty(ref _popupIconTextColor, value);
    }

    public string PopupBorderColor
    {
        get => _popupBorderColor;
        private set => SetProperty(ref _popupBorderColor, value);
    }

    public ObservableCollection<AttendanceRecord> MonthlyRecords { get; } = new();

    public bool HasMonthlyRecords => MonthlyRecords.Count > 0;

    /// <summary>Trạng thái nghịch đảo, dùng để ẩn CollectionView khi tháng chưa có dữ liệu.</summary>
    public bool HasNoMonthlyRecords => MonthlyRecords.Count == 0;

    public bool CanGoNextMonth => _monthLabelYear < DateTime.Now.Year
                                  || (_monthLabelYear == DateTime.Now.Year && _monthLabelMonth < DateTime.Now.Month);

    public string MonthLabel => $"Tháng {_monthLabelMonth:00}/{_monthLabelYear}";

    #endregion

    #region Lệnh

    public ICommand CheckInCommand { get; }
    public ICommand CheckOutCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClosePopupCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }

    #endregion

    /// <summary>
    /// Nạp ca làm việc hôm nay và lịch sử chấm công của tháng đang xem.
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsLoading = true;

        try
        {
            await LoadTodayShiftAsync();
            await LoadMonthlyAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowPopup("Phiên Đăng Nhập Hết Hạn", ex.Message, false);
        }
        catch (Exception ex)
        {
            ShowPopup("Lỗi Tải Dữ Liệu", ex.Message, false);
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    private async Task LoadTodayShiftAsync()
    {
        var shift = await _attendanceService.GetTodayShiftAsync();

        MaPhanCa = shift.MaPhanCa;
        HasShiftToday = shift.MaPhanCa > 0;

        if (!HasShiftToday)
        {
            ShiftName = "Không có ca làm việc";
            ShiftTimeText = "Hôm nay bạn không được phân ca. Vui lòng liên hệ quản lý.";
            DaCheckIn = false;
            DaCheckOut = false;
            ApplyStatusStyle(null, hasCheckedIn: false);
            UpdateActionButton();
            return;
        }

        ShiftName = string.IsNullOrWhiteSpace(shift.TenCa) ? "Ca Làm Việc" : shift.TenCa;
        ShiftTimeText = $"{FormatTime(shift.GioBatDau)} – {FormatTime(shift.GioKetThuc)}";

        DaCheckIn = shift.DaCheckIn.HasValue;
        DaCheckOut = shift.DaCheckOut.HasValue;
        CheckInTimeText = shift.DaCheckIn.HasValue ? FormatTime(shift.DaCheckIn.Value) : "--:--";
        CheckOutTimeText = shift.DaCheckOut.HasValue ? FormatTime(shift.DaCheckOut.Value) : "--:--";

        PhutDiTre = shift.PhutDiTre;
        PhutVeSom = shift.PhutVeSom;
        GioOt = shift.GioOt;

        ApplyStatusStyle(DaCheckOut ? shift.TrangThai : AttendanceCalculator.STATUS_DUNG_GIO, hasCheckedIn: DaCheckIn);
        UpdateActionButton();
    }

    private async Task LoadMonthlyAsync()
    {
        var records = await _attendanceService.GetMonthlyAttendanceAsync(_monthLabelYear, _monthLabelMonth);

        MonthlyRecords.Clear();
        foreach (var record in records)
        {
            MonthlyRecords.Add(ToAttendanceRecord(record));
        }

        OnPropertyChanged(nameof(HasMonthlyRecords));
        OnPropertyChanged(nameof(HasNoMonthlyRecords));
    }

    /// <summary>
    /// Quẹt thẻ vào ca, tự lấy toạ độ và tên thiết bị trước khi gửi lên máy chủ.
    /// </summary>
    private async Task ExecuteCheckInAsync()
    {
        if (!CanPunchCard)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var toaDo = await _geoLocationService.GetCurrentCoordinateAsync();
            var thietBi = await _geoLocationService.GetDeviceNameAsync();

            var response = await _attendanceService.CheckInAsync(MaPhanCa, toaDo, thietBi);

            if (!response.Success)
            {
                ShowPopup("Quẹt Thẻ Vào Thất Bại", response.Message, false);
                return;
            }

            DaCheckIn = true;
            CheckInTimeText = FormatTime(DateTimeOffset.Now);
            PhutDiTre = response.PhutDiTre;
            PhutVeSom = 0;
            GioOt = 0m;

            ApplyStatusStyle(response.TrangThai, hasCheckedIn: true);
            UpdateActionButton();

            await LoadMonthlyAsync();

            ShowPopup("Quẹt Thẻ Vào Thành Công", BuildCheckInDetailMessage(toaDo), true);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowPopup("Phiên Đăng Nhập Hết Hạn", ex.Message, false);
        }
        catch (Exception ex)
        {
            ShowPopup("Lỗi Quẹt Thẻ Vào", ex.Message, false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Quẹt thẻ ra ca, máy chủ tự đối soát phút về sớm và giờ làm thêm.
    /// </summary>
    private async Task ExecuteCheckOutAsync()
    {
        if (!CanPunchCard)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var toaDo = await _geoLocationService.GetCurrentCoordinateAsync();
            var thietBi = await _geoLocationService.GetDeviceNameAsync();

            var response = await _attendanceService.CheckOutAsync(MaPhanCa, toaDo, thietBi);

            if (!response.Success)
            {
                ShowPopup("Quẹt Thẻ Ra Thất Bại", response.Message, false);
                return;
            }

            DaCheckOut = true;
            CheckOutTimeText = FormatTime(DateTimeOffset.Now);
            PhutDiTre = response.PhutDiTre;
            PhutVeSom = response.PhutVeSom;
            GioOt = response.GioOt;

            ApplyStatusStyle(response.TrangThai, hasCheckedIn: true);
            UpdateActionButton();

            await LoadMonthlyAsync();

            ShowPopup("Quẹt Thẻ Ra Thành Công", BuildCheckOutDetailMessage(toaDo), true);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowPopup("Phiên Đăng Nhập Hết Hạn", ex.Message, false);
        }
        catch (Exception ex)
        {
            ShowPopup("Lỗi Quẹt Thẻ Ra", ex.Message, false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Chuyển tháng trong lịch sử chấm công, không cho xem quá tháng hiện tại.
    /// </summary>
    private void ShiftMonth(int delta)
    {
        var target = new DateOnly(_monthLabelYear, _monthLabelMonth, 1).AddMonths(delta);
        var now = DateTime.Now;

        if (target.Year > now.Year || (target.Year == now.Year && target.Month > now.Month))
        {
            return;
        }

        _monthLabelYear = target.Year;
        _monthLabelMonth = target.Month;

        OnPropertyChanged(nameof(MonthLabel));
        OnPropertyChanged(nameof(CanGoNextMonth));

        _ = LoadMonthlyAsync();
    }

    private void ExecuteClosePopup()
    {
        IsPopupVisible = false;
    }

    private void UpdateActionButton()
    {
        if (!HasShiftToday)
        {
            ActionButtonText = "KHÔNG CÓ CA LÀM VIỆC";
            ActionButtonIcon = "—";
            ActionButtonColor = "#94A3B8";
            return;
        }

        if (DaCheckOut)
        {
            ActionButtonText = "ĐÃ QUẸT THẺ ĐỦ";
            ActionButtonIcon = "✓";
            ActionButtonColor = "#059669";
            return;
        }

        if (DaCheckIn)
        {
            ActionButtonText = "QUẸT THẺ RA";
            ActionButtonIcon = "◉";
            ActionButtonColor = "#B45309";
            return;
        }

        ActionButtonText = "QUẸT THẺ VÀO";
        ActionButtonIcon = "◉";
        ActionButtonColor = "#1E3A8A";
    }

    /// <summary>
    /// Áp dụng màu badge theo trạng thái chấm công.
    /// Ca chưa quẹt thẻ vào thì hiển thị "CHỜ QUẸT THẺ" thay vì trạng thái của máy chủ.
    /// </summary>
    private void ApplyStatusStyle(string? trangThai, bool hasCheckedIn)
    {
        if (!hasCheckedIn)
        {
            StatusText = "CHỜ QUẸT THẺ";
            StatusBgColor = "#FEF3C7";
            StatusTextColor = "#B45309";
            return;
        }

        switch (trangThai)
        {
            case AttendanceCalculator.STATUS_DUNG_GIO:
                StatusText = "ĐÚNG GIỜ";
                StatusBgColor = "#ECFDF5";
                StatusTextColor = "#059669";
                break;
            case AttendanceCalculator.STATUS_DI_TRE:
                StatusText = "ĐI TRỄ";
                StatusBgColor = "#FEF3C7";
                StatusTextColor = "#B45309";
                break;
            case AttendanceCalculator.STATUS_VE_SOM:
                StatusText = "VỀ SỚM";
                StatusBgColor = "#EFF6FF";
                StatusTextColor = "#1D4ED8";
                break;
            case AttendanceCalculator.STATUS_TRE_VA_SOM:
                StatusText = "TRỄ VÀ SỚM";
                StatusBgColor = "#FEF2F2";
                StatusTextColor = "#DC2626";
                break;
            default:
                StatusText = "VẮNG MẶT";
                StatusBgColor = "#F1F5F9";
                StatusTextColor = "#64748B";
                break;
        }
    }

    private static AttendanceRecord ToAttendanceRecord(MonthlyAttendanceDto dto)
    {
        var record = new AttendanceRecord
        {
            NgayLamViec = dto.NgayLamViec.ToString("dd/MM/yyyy"),
            TenCa = string.IsNullOrWhiteSpace(dto.TenCa) ? "Ca Làm Việc" : dto.TenCa,
            CheckInText = dto.CheckIn.HasValue ? FormatTime(dto.CheckIn.Value) : "--:--",
            CheckOutText = dto.CheckOut.HasValue ? FormatTime(dto.CheckOut.Value) : "--:--",
            PhutDiTreText = dto.PhutDiTre > 0 ? $"{dto.PhutDiTre} phút" : "0 phút",
            PhutVeSomText = dto.PhutVeSom > 0 ? $"{dto.PhutVeSom} phút" : "0 phút",
            GioOtText = dto.GioOt > 0m ? $"{dto.GioOt:0.##} giờ" : "0 giờ",
            TrangThaiCode = dto.TrangThai
        };

        (record.TrangThaiText, record.TrangThaiBgColor, record.TrangThaiTextColor) = dto.TrangThai switch
        {
            AttendanceCalculator.STATUS_DUNG_GIO => ("Đúng giờ", "#ECFDF5", "#059669"),
            AttendanceCalculator.STATUS_DI_TRE => ("Đi trễ", "#FEF3C7", "#B45309"),
            AttendanceCalculator.STATUS_VE_SOM => ("Về sớm", "#EFF6FF", "#1D4ED8"),
            AttendanceCalculator.STATUS_TRE_VA_SOM => ("Trễ và sớm", "#FEF2F2", "#DC2626"),
            _ => ("Vắng mặt", "#F1F5F9", "#64748B")
        };

        return record;
    }

    private string BuildCheckInDetailMessage(string? toaDo)
    {
        var lines = new List<string>
        {
            $"Thời gian: {FormatTime(DateTimeOffset.Now)}",
            $"Ca làm việc: {ShiftName}",
            $"Đi trễ: {PhutDiTreText}"
        };

        if (toaDo is not null)
        {
            lines.Add($"Toạ độ: {toaDo}");
        }

        return string.Join('\n', lines);
    }

    private string BuildCheckOutDetailMessage(string? toaDo)
    {
        var lines = new List<string>
        {
            $"Thời gian: {FormatTime(DateTimeOffset.Now)}",
            $"Ca làm việc: {ShiftName}",
            $"Đi trễ: {PhutDiTreText}",
            $"Về sớm: {PhutVeSomText}",
            $"Giờ làm thêm: {GioOtText}"
        };

        if (toaDo is not null)
        {
            lines.Add($"Toạ độ: {toaDo}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatTime(DateTimeOffset value) => value.ToString("HH:mm");

    /// <summary>
    /// Hiển thị thông báo qua Custom Modal Popup chuẩn 5 sao.
    /// </summary>
    private void ShowPopup(string title, string message, bool isSuccess)
    {
        PopupTitle = title;
        PopupMessage = message;

        if (isSuccess)
        {
            PopupIconText = "★";
            PopupIconBgColor = "#ECFDF5";
            PopupIconTextColor = "#059669";
            PopupBorderColor = "#10B981";
            PopupButtonText = "HOÀN TẤT";
        }
        else
        {
            PopupIconText = "!";
            PopupIconBgColor = "#FEF2F2";
            PopupIconTextColor = "#DC2626";
            PopupBorderColor = "#FCA5A5";
            PopupButtonText = "ĐỒNG Ý";
        }

        IsPopupVisible = true;
    }

    private void RefreshCommandState()
    {
        (RefreshCommand as Command)?.ChangeCanExecute();
        (CheckInCommand as Command)?.ChangeCanExecute();
        (CheckOutCommand as Command)?.ChangeCanExecute();
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}