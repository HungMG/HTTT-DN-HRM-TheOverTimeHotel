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
    private EligibleShiftDto? _selectedShift;
    private TimeSpan? _gioDeXuatVaoTime;
    private TimeSpan? _gioDeXuatRaTime;
    private bool _useGioVao = true;
    private bool _useGioRa = true;
    private string _lyDo = string.Empty;
    private string _minhChung = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<EligibleShiftDto> EligibleShifts { get; } = [];
    public ObservableCollection<AttendanceExplanationItemDto> MyExplanations { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SwitchModeCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand WithdrawCommand { get; }
    public ICommand SelectShiftCommand { get; }
    public ICommand BackCommand { get; }

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
        WithdrawCommand = new Command<AttendanceExplanationItemDto>(async (item) => await WithdrawAsync(item));
        SelectShiftCommand = new Command<EligibleShiftDto>((shift) =>
        {
            SelectedShift = shift;
        });
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
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
                NotifyCanSubmitChanged();
            }
        }
    }

    public bool HasSelectedShift => SelectedShift != null;
    public string SelectedShiftDateText => SelectedShift is { } shift ? $"Ngày: {shift.NgayLamViec:dd/MM/yyyy}" : string.Empty;
    public string SelectedShiftStartText => SelectedShift is { } shift ? $"Giờ ca: {shift.GioBatDau:HH:mm}" : string.Empty;

    public bool UseGioVao
    {
        get => _useGioVao;
        set
        {
            if (SetProperty(ref _useGioVao, value))
            {
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
                await Shell.Current.DisplayAlert("Thành công", "Đã gửi giải trình chấm công. Đang chờ Quản lý bộ phận xác minh.", "Đóng");
                LyDo = string.Empty;
                MinhChung = string.Empty;
                IsCreateMode = false;
                await LoadAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Lỗi", result.Message, "Đóng");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không thể gửi giải trình lúc này. Vui lòng thử lại sau.", "Đóng");
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task WithdrawAsync(AttendanceExplanationItemDto? item)
    {
        if (item == null || !item.CanWithdraw) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Xác nhận rút giải trình",
            $"Bạn có chắc chắn muốn rút giải trình cho ca {item.TenCa} ngày {item.NgayLamViec:dd/MM/yyyy} không?",
            "Rút giải trình",
            "Đóng");

        if (!confirm) return;

        IsSubmitting = true;
        try
        {
            var result = await _service.WithdrawExplanationAsync(item.MaGiaiTrinh);
            if (result.Success)
            {
                await Shell.Current.DisplayAlert("Thành công", "Đã rút giải trình chấm công thành công.", "Đóng");
                await LoadAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Không thể rút", result.Message, "Đóng");
                await LoadAsync();
            }
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không thể rút giải trình lúc này. Vui lòng thử lại sau.", "Đóng");
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
