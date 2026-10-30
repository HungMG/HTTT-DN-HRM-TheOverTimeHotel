using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

public class ShiftSwapViewModel : INotifyPropertyChanged
{
    private readonly IShiftSwapService _service;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;

    private bool _isLoading;
    private bool _isSubmitting;
    private bool _hasError;
    private string _errorMessage = string.Empty;

    private bool _isCreateMode;
    private EligibleMyShiftDto? _selectedMyShift;
    private EligiblePartnerShiftDto? _selectedPartnerShift;
    private string _lyDo = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<EligibleMyShiftDto> EligibleMyShifts { get; } = [];
    public ObservableCollection<EligiblePartnerShiftDto> EligiblePartnerShifts { get; } = [];
    public ObservableCollection<ShiftSwapItemDto> MyShiftSwaps { get; } = [];

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SwitchModeCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand AcceptCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand WithdrawCommand { get; }
    public ICommand SelectMyShiftCommand { get; }
    public ICommand SelectPartnerShiftCommand { get; }
    public ICommand BackCommand { get; }

    public ShiftSwapViewModel(IShiftSwapService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        MyShiftSwaps.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsListVisible));
        };

        LoadCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        RefreshCommand = new Command(async () => await LoadAsync(isRefresh: true), () => !IsLoading && !IsSubmitting);
        SwitchModeCommand = new Command<string>((mode) =>
        {
            IsCreateMode = mode == "create";
        });
        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsSubmitting && CanSubmit);
        AcceptCommand = new Command<ShiftSwapItemDto>(async (item) => await AcceptAsync(item), (item) => !IsSubmitting);
        RejectCommand = new Command<ShiftSwapItemDto>(async (item) => await RejectAsync(item), (item) => !IsSubmitting);
        WithdrawCommand = new Command<ShiftSwapItemDto>(async (item) => await WithdrawAsync(item), (item) => !IsSubmitting);
        SelectMyShiftCommand = new Command<EligibleMyShiftDto>((shift) =>
        {
            SelectedMyShift = shift;
        });
        SelectPartnerShiftCommand = new Command<EligiblePartnerShiftDto>((shift) =>
        {
            SelectedPartnerShift = shift;
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
                OnPropertyChanged(nameof(IsListVisible));
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
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsCreateMode
    {
        get => _isCreateMode;
        set => SetProperty(ref _isCreateMode, value);
    }

    public EligibleMyShiftDto? SelectedMyShift
    {
        get => _selectedMyShift;
        set
        {
            if (SetProperty(ref _selectedMyShift, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
                ((Command)SubmitCommand).ChangeCanExecute();
            }
        }
    }

    public EligiblePartnerShiftDto? SelectedPartnerShift
    {
        get => _selectedPartnerShift;
        set
        {
            if (SetProperty(ref _selectedPartnerShift, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
                ((Command)SubmitCommand).ChangeCanExecute();
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
                OnPropertyChanged(nameof(CanSubmit));
                ((Command)SubmitCommand).ChangeCanExecute();
            }
        }
    }

    public bool CanSubmit =>
        SelectedMyShift != null &&
        SelectedPartnerShift != null &&
        !string.IsNullOrWhiteSpace(LyDo) &&
        LyDo.Trim().Length <= 500;

    public bool IsEmpty => !IsLoading && !HasError && MyShiftSwaps.Count == 0;
    public bool IsListVisible => !IsLoading && !HasError && MyShiftSwaps.Count > 0;

    public async Task LoadAsync(bool isRefresh = false)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var ct = _loadCancellation.Token;

        if (!isRefresh)
        {
            IsLoading = true;
        }
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var myShiftsTask = _service.GetEligibleMyShiftsAsync(ct);
            var partnerShiftsTask = _service.GetEligiblePartnerShiftsAsync(ct);
            var swapsTask = _service.GetMyShiftSwapsAsync(ct);

            await Task.WhenAll(myShiftsTask, partnerShiftsTask, swapsTask);

            if (version != _loadVersion) return;

            var myShifts = await myShiftsTask;
            var partnerShifts = await partnerShiftsTask;
            var swaps = await swapsTask;

            EligibleMyShifts.Clear();
            foreach (var s in myShifts) EligibleMyShifts.Add(s);

            EligiblePartnerShifts.Clear();
            foreach (var s in partnerShifts) EligiblePartnerShifts.Add(s);

            MyShiftSwaps.Clear();
            foreach (var item in swaps) MyShiftSwaps.Add(item);

            if (SelectedMyShift != null && !EligibleMyShifts.Any(s => s.MaPhanCa == SelectedMyShift.MaPhanCa))
            {
                SelectedMyShift = EligibleMyShifts.FirstOrDefault();
            }
            else if (SelectedMyShift == null && EligibleMyShifts.Count > 0)
            {
                SelectedMyShift = EligibleMyShifts[0];
            }

            if (SelectedPartnerShift != null && !EligiblePartnerShifts.Any(s => s.MaPhanCa == SelectedPartnerShift.MaPhanCa))
            {
                SelectedPartnerShift = EligiblePartnerShifts.FirstOrDefault();
            }
            else if (SelectedPartnerShift == null && EligiblePartnerShifts.Count > 0)
            {
                SelectedPartnerShift = EligiblePartnerShifts[0];
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Bỏ qua do hủy
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                HasError = true;
                ErrorMessage = ex is UnauthorizedAccessException or InvalidOperationException ? ex.Message : "Không thể tải dữ liệu đổi ca. Vui lòng thử lại.";
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    public void CancelLoad()
    {
        _loadCancellation?.Cancel();
    }

    public async Task SubmitAsync()
    {
        if (!CanSubmit || SelectedMyShift == null || SelectedPartnerShift == null) return;

        IsSubmitting = true;
        try
        {
            var req = new CreateShiftSwapRequest
            {
                MaPhanCaGui = SelectedMyShift.MaPhanCa,
                MaPhanCaNhan = SelectedPartnerShift.MaPhanCa,
                LyDo = LyDo.Trim()
            };

            var result = await _service.CreateShiftSwapAsync(req);
            if (result.Success)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Thành công", result.Message, "OK");
                }

                LyDo = string.Empty;
                IsCreateMode = false;
                await LoadAsync(isRefresh: true);
            }
            else
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Không thể gửi", result.Message, "Đóng");
                }
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public async Task AcceptAsync(ShiftSwapItemDto? item)
    {
        if (item == null || Application.Current?.MainPage == null) return;

        var confirm = await Application.Current.MainPage.DisplayAlert(
            "Xác nhận đổi ca",
            $"Bạn có chắc chắn muốn đồng ý đổi ca #{item.MaYeuCau} với {item.TenNguoiGui}?\nCa của bạn: {item.TenCaNhan} ({item.NgayCaNhan})\nCa nhận lại: {item.TenCaGui} ({item.NgayCaGui})",
            "Đồng ý đổi",
            "Xem lại");

        if (!confirm) return;

        IsSubmitting = true;
        try
        {
            var result = await _service.RespondShiftSwapAsync(item.MaYeuCau, new RespondShiftSwapRequest
            {
                DongY = true
            });

            if (result.Success)
            {
                await Application.Current.MainPage.DisplayAlert("Thành công", result.Message, "OK");
                await LoadAsync(isRefresh: true);
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Thất bại", result.Message, "Đóng");
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public async Task RejectAsync(ShiftSwapItemDto? item)
    {
        if (item == null || Application.Current?.MainPage == null) return;

        var reason = await Application.Current.MainPage.DisplayPromptAsync(
            "Từ chối đổi ca",
            "Nhập lý do bạn không thể đổi ca (bắt buộc):",
            "Xác nhận từ chối",
            "Hủy",
            placeholder: "Lý do từ chối...",
            maxLength: 500);

        if (string.IsNullOrWhiteSpace(reason)) return;

        IsSubmitting = true;
        try
        {
            var result = await _service.RespondShiftSwapAsync(item.MaYeuCau, new RespondShiftSwapRequest
            {
                DongY = false,
                LyDo = reason.Trim()
            });

            if (result.Success)
            {
                await Application.Current.MainPage.DisplayAlert("Đã từ chối", result.Message, "OK");
                await LoadAsync(isRefresh: true);
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Thất bại", result.Message, "Đóng");
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public async Task WithdrawAsync(ShiftSwapItemDto? item)
    {
        if (item == null || Application.Current?.MainPage == null) return;

        var confirm = await Application.Current.MainPage.DisplayAlert(
            "Rút yêu cầu",
            $"Bạn có chắc chắn muốn rút yêu cầu đổi ca #{item.MaYeuCau}?",
            "Rút yêu cầu",
            "Không");

        if (!confirm) return;

        IsSubmitting = true;
        try
        {
            var result = await _service.WithdrawShiftSwapAsync(item.MaYeuCau);
            if (result.Success)
            {
                await Application.Current.MainPage.DisplayAlert("Thành công", result.Message, "OK");
                await LoadAsync(isRefresh: true);
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Thất bại", result.Message, "Đóng");
            }
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
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
