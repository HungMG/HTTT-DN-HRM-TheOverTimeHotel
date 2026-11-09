using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

public class AttendanceExplanationViewModel : INotifyPropertyChanged
{
    private readonly IAttendanceExplanationService _service;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;

    private bool _isLoading;
    private bool _isSubmitting;
    private bool _hasError;
    private string _errorMessage = string.Empty;

    private bool _isCreateMode = true;
    private bool _isShiftDropdownOpen;
    private EligibleShiftDto? _selectedShift;
    private TimeSpan? _gioDeXuatVaoTime;
    private TimeSpan? _gioDeXuatRaTime;
    private bool _useGioVao = true;
    private bool _useGioRa = true;
    private string _lyDo = string.Empty;
    private string _minhChung = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string, string, bool>? PopupRequested;
    public event Action<string, string, string, Action>? ConfirmRequested;

    public ObservableCollection<EligibleShiftDto> EligibleShifts { get; } = [];
    public ObservableCollection<AttendanceExplanationItemDto> MyExplanations { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SwitchModeCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand WithdrawCommand { get; }
    public ICommand SelectShiftCommand { get; }
    public ICommand ToggleShiftDropdownCommand { get; }
    public ICommand CloseShiftDropdownCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ToggleGioVaoCommand { get; }
    public ICommand ToggleGioRaCommand { get; }
    public ICommand SelectReasonSuggestionCommand { get; }
    public ObservableCollection<string> ReasonSuggestions { get; } =
    [
        "Quên quẹt thẻ ra",
        "Quên quẹt thẻ vào",
        "Máy vân tay lỗi / không nhận",
        "Tăng ca theo điều phối",
        "Hỗ trợ sự kiện đột xuất",
        "Công tác / Đón đoàn VIP"
    ];

    public AttendanceExplanationViewModel(IAttendanceExplanationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        LoadCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        RefreshCommand = new Command(async () => await LoadAsync(isRefresh: true), () => !IsLoading && !IsSubmitting);
        SwitchModeCommand = new Command<string>((mode) =>
        {
            IsCreateMode = mode != "history";
        });
        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsSubmitting && CanSubmit);
        WithdrawCommand = new Command<AttendanceExplanationItemDto>((item) => Withdraw(item));
        ToggleShiftDropdownCommand = new Command(() => IsShiftDropdownOpen = !IsShiftDropdownOpen);
        CloseShiftDropdownCommand = new Command(() => IsShiftDropdownOpen = false);
        SelectShiftCommand = new Command<EligibleShiftDto>((shift) =>
        {
            SelectedShift = shift;
            IsShiftDropdownOpen = false;
        });
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ToggleGioVaoCommand = new Command(() => UseGioVao = !UseGioVao);
        ToggleGioRaCommand = new Command(() => UseGioRa = !UseGioRa);
        SelectReasonSuggestionCommand = new Command<string>((reason) =>
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                LyDo = reason;
            }
        });
    }

    public bool IsDemoMode => _service.IsDemoMode;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                ((Command)LoadCommand).ChangeCanExecute();
                ((Command)RefreshCommand).ChangeCanExecute();
            }
        }
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                ((Command)SubmitCommand).ChangeCanExecute();
                ((Command)RefreshCommand).ChangeCanExecute();
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsCreateMode
    {
        get => _isCreateMode;
        set
        {
            if (SetProperty(ref _isCreateMode, value))
            {
                OnPropertyChanged(nameof(IsHistoryMode));
                OnPropertyChanged(nameof(CreateTabBackground));
                OnPropertyChanged(nameof(CreateTabColor));
                OnPropertyChanged(nameof(HistoryTabBackground));
                OnPropertyChanged(nameof(HistoryTabColor));
            }
        }
    }

    public bool IsHistoryMode => !IsCreateMode;
    public string CreateTabBackground => IsCreateMode ? "#1E3A8A" : "#FFFFFF";
    public string CreateTabColor => IsCreateMode ? "#FFFFFF" : "#64748B";
    public string HistoryTabBackground => !IsCreateMode ? "#1E3A8A" : "#FFFFFF";
    public string HistoryTabColor => !IsCreateMode ? "#FFFFFF" : "#64748B";

    public int PendingCount => MyExplanations.Count(x => x.IsChoQuanLy || x.IsChoHr);
    public int ApprovedCount => MyExplanations.Count(x => x.IsDaDuyet);
    public string HistoryTabTitle => MyExplanations.Count > 0 ? $"Lịch sử ({MyExplanations.Count})" : "Lịch sử giải trình";

    public bool IsShiftDropdownOpen
    {
        get => _isShiftDropdownOpen;
        set => SetProperty(ref _isShiftDropdownOpen, value);
    }

    public EligibleShiftDto? SelectedShift
    {
        get => _selectedShift;
        set
        {
            if (SetProperty(ref _selectedShift, value))
            {
                if (value != null)
                {
                    GioDeXuatVaoTime = value.GioVaoThucTe.HasValue
                        ? value.GioVaoThucTe.Value.ToLocalTime().TimeOfDay
                        : value.GioBatDau.ToTimeSpan();

                    GioDeXuatRaTime = value.GioRaThucTe.HasValue
                        ? value.GioRaThucTe.Value.ToLocalTime().TimeOfDay
                        : value.GioKetThuc.ToTimeSpan();
                }
                OnPropertyChanged(nameof(HasSelectedShift));
                OnPropertyChanged(nameof(SelectedShiftDateText));
                OnPropertyChanged(nameof(SelectedShiftStartText));
                OnPropertyChanged(nameof(SelectedShiftDisplayText));
                OnPropertyChanged(nameof(GioVaoThucTeDisplayText));
                OnPropertyChanged(nameof(HasGioVaoThucTe));
                OnPropertyChanged(nameof(GioRaThucTeDisplayText));
                OnPropertyChanged(nameof(HasGioRaThucTe));
                OnPropertyChanged(nameof(GioDeXuatVaoDisplayText));
                OnPropertyChanged(nameof(GioDeXuatRaDisplayText));
                NotifyCanSubmitChanged();
            }
        }
    }

    public string SelectedShiftDisplayText => SelectedShift is { } shift
        ? $"{shift.TenCa} · {shift.NgayLamViec:dd/MM/yyyy} ({shift.GioBatDau:HH:mm} - {shift.GioKetThuc:HH:mm})"
        : "-- Chọn ca trực trong 45 ngày qua --";

    public bool HasSelectedShift => SelectedShift != null;
    public string SelectedShiftDateText => SelectedShift is { } shift ? $"Ngày: {shift.NgayLamViec:dd/MM/yyyy}" : string.Empty;
    public string SelectedShiftStartText => SelectedShift is { } shift ? $"Giờ ca: {shift.GioBatDau:HH:mm} - {shift.GioKetThuc:HH:mm}" : string.Empty;

    public string GioVaoThucTeDisplayText => SelectedShift?.GioVaoThucTe.HasValue == true
        ? $"✓ {SelectedShift.GioVaoThucTe.Value.ToLocalTime():HH:mm} (Đã quẹt)"
        : "✕ Chưa quẹt thẻ vào";

    public bool HasGioVaoThucTe => SelectedShift?.GioVaoThucTe.HasValue == true;

    public string GioRaThucTeDisplayText => SelectedShift?.GioRaThucTe.HasValue == true
        ? $"✓ {SelectedShift.GioRaThucTe.Value.ToLocalTime():HH:mm} (Đã quẹt)"
        : "✕ Chưa quẹt thẻ ra";

    public bool HasGioRaThucTe => SelectedShift?.GioRaThucTe.HasValue == true;

    public string GioDeXuatVaoDisplayText => GioDeXuatVaoTime.HasValue
        ? $"{GioDeXuatVaoTime.Value.Hours:D2}:{GioDeXuatVaoTime.Value.Minutes:D2}"
        : "--:--";

    public string GioDeXuatRaDisplayText => GioDeXuatRaTime.HasValue
        ? $"{GioDeXuatRaTime.Value.Hours:D2}:{GioDeXuatRaTime.Value.Minutes:D2}"
        : "--:--";

    public bool IsGioVaoOff => !UseGioVao;
    public bool IsGioRaOff => !UseGioRa;

    public bool UseGioVao
    {
        get => _useGioVao;
        set
        {
            if (SetProperty(ref _useGioVao, value))
            {
                OnPropertyChanged(nameof(IsGioVaoOff));
                NotifyCanSubmitChanged();
            }
        }
    }

    public bool UseGioRa
    {
        get => _useGioRa;
        set
        {
            if (SetProperty(ref _useGioRa, value))
            {
                OnPropertyChanged(nameof(IsGioRaOff));
                NotifyCanSubmitChanged();
            }
        }
    }

    public TimeSpan? GioDeXuatVaoTime
    {
        get => _gioDeXuatVaoTime;
        set
        {
            if (SetProperty(ref _gioDeXuatVaoTime, value))
            {
                OnPropertyChanged(nameof(GioDeXuatVaoDisplayText));
                NotifyCanSubmitChanged();
            }
        }
    }

    public TimeSpan? GioDeXuatRaTime
    {
        get => _gioDeXuatRaTime;
        set
        {
            if (SetProperty(ref _gioDeXuatRaTime, value))
            {
                OnPropertyChanged(nameof(GioDeXuatRaDisplayText));
                NotifyCanSubmitChanged();
            }
        }
    }

    public string LyDo
    {
        get => _lyDo;
        set
        {
            if (SetProperty(ref _lyDo, value))
            {
                OnPropertyChanged(nameof(LyDoLengthText));
                NotifyCanSubmitChanged();
            }
        }
    }

    public string MinhChung
    {
        get => _minhChung;
        set
        {
            if (SetProperty(ref _minhChung, value))
            {
                OnPropertyChanged(nameof(MinhChungLengthText));
            }
        }
    }

    public string LyDoLengthText => $"{LyDo.Length}/500";
    public string MinhChungLengthText => $"{MinhChung.Length}/500";

    private void NotifyCanSubmitChanged()
    {
        OnPropertyChanged(nameof(CanSubmit));
        ((Command)SubmitCommand).ChangeCanExecute();
    }

    public bool CanSubmit =>
        SelectedShift != null &&
        !string.IsNullOrWhiteSpace(LyDo) &&
        LyDo.Trim().Length <= 500 &&
        (UseGioVao || UseGioRa) &&
        (!UseGioVao || GioDeXuatVaoTime.HasValue) &&
        (!UseGioRa || GioDeXuatRaTime.HasValue);

    public bool IsEmpty => !IsLoading && !HasError && MyExplanations.Count == 0;

    public async Task LoadAsync(bool isRefresh = false)
    {
        if (IsLoading || (isRefresh && IsSubmitting)) return;

        var version = ++_loadVersion;
        if (_loadCancellation != null)
        {
            try { _loadCancellation.Cancel(); } catch (ObjectDisposedException) { }
            _loadCancellation.Dispose();
        }
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var shiftsTask = _service.GetEligibleShiftsAsync(token);
            var explanationsTask = _service.GetMyExplanationsAsync(token);

            await Task.WhenAll(shiftsTask, explanationsTask);

            if (version != _loadVersion || token.IsCancellationRequested) return;

            EligibleShifts.Clear();
            foreach (var s in shiftsTask.Result)
            {
                EligibleShifts.Add(s);
            }

            if (SelectedShift == null && EligibleShifts.Count > 0)
            {
                SelectedShift = EligibleShifts[0];
            }

            MyExplanations.Clear();
            foreach (var e in explanationsTask.Result)
            {
                MyExplanations.Add(e);
            }
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(ApprovedCount));
            OnPropertyChanged(nameof(HistoryTabTitle));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (version != _loadVersion || token.IsCancellationRequested) return;
            HasError = true;
            ErrorMessage = ex is UnauthorizedAccessException or InvalidOperationException ? ex.Message : "Không thể tải dữ liệu giải trình. Vui lòng thử lại.";
        }
        finally
        {
            if (version == _loadVersion && !token.IsCancellationRequested)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(ApprovedCount));
                OnPropertyChanged(nameof(HistoryTabTitle));
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
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit || SelectedShift == null) return;

        IsSubmitting = true;
        try
        {
            DateTimeOffset? vao = null;
            if (UseGioVao && GioDeXuatVaoTime.HasValue)
            {
                var dt = SelectedShift.NgayLamViec.ToDateTime(TimeOnly.FromTimeSpan(GioDeXuatVaoTime.Value));
                vao = new DateTimeOffset(dt, TimeSpan.FromHours(7));
            }

            DateTimeOffset? ra = null;
            if (UseGioRa && GioDeXuatRaTime.HasValue)
            {
                var dt = SelectedShift.NgayLamViec.ToDateTime(TimeOnly.FromTimeSpan(GioDeXuatRaTime.Value));
                // Nếu ca đêm kết thúc vào ngày hôm sau
                if (SelectedShift.GioKetThuc < SelectedShift.GioBatDau && GioDeXuatRaTime.Value < GioDeXuatVaoTime.GetValueOrDefault())
                {
                    dt = dt.AddDays(1);
                }
                ra = new DateTimeOffset(dt, TimeSpan.FromHours(7));
            }

            var request = new CreateAttendanceExplanationRequest(
                MaPhanCa: SelectedShift.MaPhanCa,
                GioDeXuatVao: vao,
                GioDeXuatRa: ra,
                LyDo: LyDo.Trim(),
                MinhChung: string.IsNullOrWhiteSpace(MinhChung) ? null : MinhChung.Trim()
            );

            var result = await _service.SubmitExplanationAsync(request);
            if (result.Success)
            {
                PopupRequested?.Invoke(
                    "Gửi giải trình thành công",
                    "Đã gửi giải trình chấm công vào hệ thống. Hồ sơ chuyển đến Quản lý bộ phận xác minh hiện trường (Cấp 1) trước khi chuyển lên HR thẩm định & chốt công.",
                    false);
                LyDo = string.Empty;
                MinhChung = string.Empty;
                IsCreateMode = false;
                await LoadAsync();
            }
            else
            {
                PopupRequested?.Invoke("Không thể gửi giải trình", result.Message, true);
            }
        }
        catch (Exception)
        {
            PopupRequested?.Invoke("Lỗi hệ thống", "Không thể gửi giải trình lúc này. Vui lòng thử lại sau.", true);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void Withdraw(AttendanceExplanationItemDto? item)
    {
        if (item == null || !item.CanWithdraw) return;

        ConfirmRequested?.Invoke(
            "Xác nhận rút giải trình",
            $"Bạn có chắc chắn muốn rút giải trình cho ca {item.TenCa} ngày {item.NgayLamViec:dd/MM/yyyy} không?",
            "RÚT GIẢI TRÌNH",
            () => _ = ExecuteWithdrawAsync(item));
    }

    private async Task ExecuteWithdrawAsync(AttendanceExplanationItemDto item)
    {
        IsSubmitting = true;
        try
        {
            var result = await _service.WithdrawExplanationAsync(item.MaGiaiTrinh);
            if (result.Success)
            {
                PopupRequested?.Invoke("Rút giải trình thành công", "Đã rút giải trình chấm công thành công.", false);
                await LoadAsync();
            }
            else
            {
                PopupRequested?.Invoke("Không thể rút", result.Message, true);
                await LoadAsync();
            }
        }
        catch (Exception)
        {
            PopupRequested?.Invoke("Lỗi hệ thống", "Không thể rút giải trình lúc này. Vui lòng thử lại sau.", true);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
