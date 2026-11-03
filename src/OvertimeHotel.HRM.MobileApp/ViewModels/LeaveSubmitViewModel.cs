using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// Cổng nộp đơn điện tử của nhân viên (TSK-18): chọn loại đơn, điền khoản ngày và
/// lý do, gửi điện tử, rồi xem lịch sử và trạng thái duyệt của chính mình.
///
/// <para>
/// Một ViewModel gộp cả hai màn hình (tạo đơn + lịch sử) vì người dùng luôn cần
/// thấy đơn vừa gửi nằm ở đâu trong danh sách — tách hai trang thì sau khi gửi
/// phải tự điều hướng, mất ngữ cảnh và dễ tưởng đơn gửi hỏng.
/// </para>
/// </summary>
public class LeaveSubmitViewModel : INotifyPropertyChanged
{
    private readonly ILeaveApprovalService _leaveService;

    private DateTime _tuNgay = DateTime.Today.AddDays(1);
    private DateTime _denNgay = DateTime.Today.AddDays(1);
    private string _lyDo = string.Empty;
    private int _selectedTypeId;
    private bool _isLoading;
    private bool _isSubmitting;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _fieldError = string.Empty;
    private string _annualLeaveInfo = string.Empty;
    private string _selectedTypeDescription = string.Empty;
    private int _usedAnnualLeaveDays;
    private bool _isHistoryOpen;
    private LeaveTypeOptionItem? _selectedType;

    public LeaveSubmitViewModel(ILeaveApprovalService leaveService)
    {
        _leaveService = leaveService ?? throw new ArgumentNullException(nameof(leaveService));

        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsSubmitting);
        SelectTypeCommand = new Command<LeaveTypeOptionItem>(SelectType);
        RetryCommand = new Command(async () => await LoadAsync());
        RefreshCommand = new Command(async () => await RefreshAsync());
        ResetCommand = new Command(() => ResetForm());
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));

        // Lịch sử đơn không kéo dài trang: mặc định chỉ một nút, bấm vào mới bung
        // ra popup. Trang chính vốn đã cuộn dài, thêm cả danh sách vào nữa thì
        // phần form nằm trên cùng bị đẩy xuống dưới màn hình.
        OpenHistoryCommand = new Command(() => IsHistoryOpen = true);
        CloseHistoryCommand = new Command(() => IsHistoryOpen = false);
        WithdrawCommand = new Command<LeaveRequestItem>(async (item) => await WithdrawAsync(item));
    }

    /// <summary>Lệnh rút đơn nghỉ phép khi còn chờ duyệt.</summary>
    public ICommand WithdrawCommand { get; }

    /// <summary>Lệnh quay lại trang trước.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Lệnh xoá nội dung form để viết lại từ đầu.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Lệnh mở popup lịch sử đơn.</summary>
    public ICommand OpenHistoryCommand { get; }

    /// <summary>Lệnh đóng popup lịch sử đơn.</summary>
    public ICommand CloseHistoryCommand { get; }

    /// <summary>Danh mục loại đơn đọc từ bảng LOAI_DON của CSDL.</summary>
    public ObservableCollection<LeaveTypeOptionItem> LeaveTypes { get; } = [];

    /// <summary>Lịch sử đơn của chính người đang đăng nhập.</summary>
    public ObservableCollection<LeaveRequestItem> MyRequests { get; } = [];

    /// <summary>Đơn đang chờ duyệt — tách riêng để người dùng thấy ngay trạng thái.</summary>
    public ObservableCollection<LeaveRequestItem> PendingRequests { get; } = [];

    public ICommand SubmitCommand { get; }

    public ICommand SelectTypeCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand RefreshCommand { get; }

    // ═══════════════════════════════════════════════════════════════
    //  Thuộc tính form
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Ngày bắt đầu nghỉ.</summary>
    public DateTime TuNgay
    {
        get => _tuNgay;
        set
        {
            if (!SetProperty(ref _tuNgay, value)) return;

            // DenNgay phải luôn >= TuNgay. Kéo TuNgay vượt quá DenNgay thì đẩy
            // DenNgay theo, vì để ngược lại sẽ tự sinh đơn 0 ngày.
            if (_denNgay < value)
            {
                DenNgay = value;
            }

            OnPropertyChanged(nameof(SoNgayNghiDisplay));
            OnPropertyChanged(nameof(CanSubmit));
        }
    }

    /// <summary>Ngày kết thúc nghỉ.</summary>
    public DateTime DenNgay
    {
        get => _denNgay;
        set
        {
            if (!SetProperty(ref _denNgay, value)) return;
            OnPropertyChanged(nameof(SoNgayNghiDisplay));
            OnPropertyChanged(nameof(CanSubmit));
        }
    }

    /// <summary>Lý do nghỉ — bắt buộc, dùng để người duyệt có căn cứ.</summary>
    public string LyDo
    {
        get => _lyDo;
        set
        {
            if (!SetProperty(ref _lyDo, value)) return;
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(LyDoCounterDisplay));
        }
    }

    /// <summary>Mã loại đơn đang chọn; 0 nghĩa là chưa chọn.</summary>
    public int SelectedTypeId
    {
        get => _selectedTypeId;
        private set => SetProperty(ref _selectedTypeId, value);
    }

    /// <summary>Loại đơn đang chọn, dùng cho DataTrigger tô viền ô đang chọn.</summary>
    public LeaveTypeOptionItem? SelectedType
    {
        get => _selectedType;
        private set => SetProperty(ref _selectedType, value);
    }

    /// <summary>
    /// Mô tả loại đơn đang chọn. Dải chip ngang chỉ còn chỗ cho tên ngắn, phần
    /// "hưởng lương / mô tả" bị dồn xuống dòng này để không mất thông tin.
    /// </summary>
    public string SelectedTypeDescription
    {
        get => _selectedTypeDescription;
        private set
        {
            if (SetProperty(ref _selectedTypeDescription, value))
            {
                OnPropertyChanged(nameof(HasSelectedType));
                OnPropertyChanged(nameof(HasNoSelectedType));
                OnPropertyChanged(nameof(SelectedTypeSalaryBadgeText));
                OnPropertyChanged(nameof(ShowSelectedTypeDetail));
            }
        }
    }

    public bool HasSelectedType => SelectedType is not null;

    /// <summary>Chưa chọn loại đơn — hiện dòng nhắc bên dưới dải chip.</summary>
    public bool HasNoSelectedType => !HasSelectedType;

    /// <summary>Nhãn hưởng lương của loại đang chọn, hiện cạnh mô tả.</summary>
    public string SelectedTypeSalaryBadgeText =>
        SelectedType?.SalaryBadgeText ?? string.Empty;

    public bool ShowSelectedTypeDetail => HasSelectedType && !string.IsNullOrWhiteSpace(SelectedTypeDescription);

    /// <summary>Số ngày nghỉ của khoảng đang chọn.</summary>
    public string SoNgayNghiDisplay =>
        $"{Math.Max(1, DenNgay.Date.Subtract(TuNgay.Date).Days + 1)} ngày";

    /// <summary>Đếm ký tự lý do, hiện cạnh ô nhập cho người dùng thấy giới hạn 500.</summary>
    public string LyDoCounterDisplay => $"{LyDo.Trim().Length}/500";

    /// <summary>Dòng nhắc quỹ phép năm còn lại, chỉ hiện với đơn loại 1.</summary>
    public string AnnualLeaveInfo
    {
        get => _annualLeaveInfo;
        private set
        {
            if (SetProperty(ref _annualLeaveInfo, value)) OnPropertyChanged(nameof(ShowAnnualLeaveInfo));
        }
    }

    public bool ShowAnnualLeaveInfo => !string.IsNullOrWhiteSpace(AnnualLeaveInfo);

    // ═══════════════════════════════════════════════════════════════
    //  Thuộc tính trạng thái
    // ═══════════════════════════════════════════════════════════════

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            if (SetProperty(ref _isSubmitting, value)) OnPropertyChanged(nameof(CanSubmit));
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Thông báo thành công sau khi gửi đơn.</summary>
    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value)) OnPropertyChanged(nameof(HasSuccess));
        }
    }

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    /// <summary>
    /// Popup lịch sử đang mở hay không. Chỉ một nút trên trang, bấm vào mới bung
    /// danh sách — giữ trang chính gọn và tránh phải cuộn qua cả đống đơn cũ
    /// mỗi lần muốn gửi đơn mới.
    /// </summary>
    public bool IsHistoryOpen
    {
        get => _isHistoryOpen;
        set => SetProperty(ref _isHistoryOpen, value);
    }

    /// <summary>Lỗi gắn đúng ô nhập (lý do/ngày), hiện ngay dưới ô đó.</summary>
    public string FieldError
    {
        get => _fieldError;
        private set => SetProperty(ref _fieldError, value);
    }

    public bool HasFieldError => !string.IsNullOrWhiteSpace(FieldError);

    /// <summary>Chỉ hiện danh sách lịch sử khi đã có đơn nào.</summary>
    public bool HasHistory => MyRequests.Count > 0;

    /// <summary>Nhãn trên nút mở lịch sử: có đơn thì kèm số đếm.</summary>
    public string HistoryButtonText => MyRequests.Count > 0
        ? $"LỊCH SỬ ĐƠN CỦA TÔI ({MyRequests.Count})"
        : "LỊCH SỬ ĐƠN CỦA TÔI";

    /// <summary>Chưa có đơn nào và đã tải xong — hiện trạng thái rỗng.</summary>
    public bool HasNoHistory => !IsLoading && !HasError && MyRequests.Count == 0;

    /// <summary>Đủ điều kiện bấm Gửi: đã chọn loại, có lý do, không đang gửi.</summary>
    public bool CanSubmit =>
        !IsSubmitting && !IsLoading && SelectedTypeId > 0 && !string.IsNullOrWhiteSpace(LyDo);

    /// <summary>Chế độ demo để hiện banner cảnh báo, khớp với trang duyệt đơn.</summary>
    public bool IsDemoMode => _leaveService.IsDemoMode;

    /// <summary>Số đơn đang chờ duyệt, dùng cho tiêu đề khối lịch sử.</summary>
    public int PendingCount => PendingRequests.Count;

    // ═══════════════════════════════════════════════════════════════
    //  Logic
    // ═══════════════════════════════════════════════════════════════

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Phát sự kiện khi cần hiện popup (thành công / lỗi hệ thống). Tham số là
    /// (tiêu đề, nội dung, làLỗi). Trang bắt sự kiện và gọi LuxuryPopup — quy ước
    /// không dùng DisplayAlert thô.
    /// </summary>
    public event Action<string, string, bool>? PopupRequested;

    /// <summary>
    /// Nạp danh mục loại đơn, quỹ phép năm và lịch sử đơn.
    /// </summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var types = await _leaveService.GetSubmittableTypesAsync();
            UsedAnnualLeaveDays = await _leaveService.GetUsedAnnualLeaveDaysAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                LeaveTypes.Clear();
                foreach (var t in types)
                {
                    LeaveTypes.Add(new LeaveTypeOptionItem
                    {
                        MaLoaiDon = t.MaLoaiDon,
                        TenLoaiDon = t.TenLoaiDon,
                        CoHuongLuong = t.CoHuongLuong,
                        MoTa = t.MoTa
                    });
                }

                UpdateAnnualLeaveInfo();
            });

            await RefreshHistoryAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HasError = true;
                ErrorMessage = ex.Message;
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async Task RefreshAsync()
    {
        await RefreshHistoryAsync();
        UsedAnnualLeaveDays = await _leaveService.GetUsedAnnualLeaveDaysAsync();
        UpdateAnnualLeaveInfo();
    }

    private async Task RefreshHistoryAsync()
    {
        var mine = await _leaveService.GetMyRequestsAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            MyRequests.Clear();
            foreach (var r in mine)
            {
                MyRequests.Add(r);
            }

            PendingRequests.Clear();
            foreach (var r in mine.Where(r => r.TrangThai == "CHO_DUYET"))
            {
                PendingRequests.Add(r);
            }

            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(HasNoHistory));
            OnPropertyChanged(nameof(HistoryButtonText));
            OnPropertyChanged(nameof(PendingCount));
        });
    }

    private void SelectType(LeaveTypeOptionItem? item)
    {
        if (item is null) return;

        // Bỏ cờ loại cũ trước rồi bật cho loại mới. Cờ nằm trên chính item nên
        // việc này tự phát PropertyChanged — không cần dựng lại cả CollectionView.
        foreach (var t in LeaveTypes)
        {
            t.IsSelected = ReferenceEquals(t, item);
        }

        SelectedType = item;
        SelectedTypeId = item.MaLoaiDon;
        SelectedTypeDescription = item.MoTa;
        FieldError = string.Empty;
        UpdateAnnualLeaveInfo();

        // Chọn lại loại thì các tín hiệu cũ không còn đúng: báo thành công và lỗi
        // trước đó phải mất, không thì người dùng thấy thông báo của đơn cũ.
        SuccessMessage = string.Empty;

        OnPropertyChanged(nameof(CanSubmit));
    }

    private int UsedAnnualLeaveDays
    {
        get => _usedAnnualLeaveDays;
        set
        {
            if (SetProperty(ref _usedAnnualLeaveDays, value)) UpdateAnnualLeaveInfo();
        }
    }

    /// <summary>
    /// Dòng nhắc quỹ phép. Chỉ hiện khi đang xin nghỉ phép năm — các loại đơn khác
    /// không trừ quỹ này nên nhắc chung sẽ gây hiểu nhầm.
    /// </summary>
    private void UpdateAnnualLeaveInfo()
    {
        if (SelectedTypeId != 1)
        {
            AnnualLeaveInfo = string.Empty;
            return;
        }

        var conLai = Math.Max(0, LeaveApprovalPolicy.AnnualLeaveQuotaDays - _usedAnnualLeaveDays);
        AnnualLeaveInfo =
            $"Quỹ phép năm còn lại {conLai}/{LeaveApprovalPolicy.AnnualLeaveQuotaDays} ngày " +
            $"(đã dùng {_usedAnnualLeaveDays} ngày).";
    }

    private async Task SubmitAsync()
    {
        if (IsSubmitting || !CanSubmit) return;

        IsSubmitting = true;
        FieldError = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var result = await _leaveService.SubmitRequestAsync(
                SelectedTypeId,
                DateOnly.FromDateTime(TuNgay),
                DateOnly.FromDateTime(DenNgay),
                LyDo);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsSubmitting = false;

                if (result.Success)
                {
                    SuccessMessage = result.Message;
                    ResetForm();
                    _ = RefreshAsync();
                    return;
                }

                // Lỗi người dùng sửa được (thiếu lý do, ngày sai, hết phép) hiện
                // tại chỗ; lỗi hệ thống mới mở popup — lỗi hệ thống thử lại cũng
                // vô nghĩa nên không nên đẩy người dùng vào đường cùng.
                var laLoiNguoiDung = result.Code is not ("FORBIDDEN" or "SERVER_ERROR" or "NETWORK_ERROR");

                if (laLoiNguoiDung)
                {
                    FieldError = result.Message;
                }
                else
                {
                    PopupRequested?.Invoke(
                        result.Code == "SUBMITTED" ? "★" : "⚠️",
                        result.Message,
                        result.Code != "SUBMITTED");
                }
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsSubmitting = false;
                PopupRequested?.Invoke("⚠️", ex.Message, false);
            });
        }
    }

    private async Task WithdrawAsync(LeaveRequestItem? item)
    {
        if (item is null || !item.CanWithdraw) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Xác nhận rút đơn",
            $"Bạn có chắc chắn muốn rút đơn {item.TenLoaiDon} (từ {item.TuNgay:dd/MM/yyyy} đến {item.DenNgay:dd/MM/yyyy}) không?",
            "Rút đơn",
            "Đóng");

        if (!confirm) return;

        IsSubmitting = true;
        try
        {
            var result = await _leaveService.WithdrawRequestAsync(item.MaDon);
            if (result.Success)
            {
                await Shell.Current.DisplayAlertAsync("Thành công", "Đã rút đơn nghỉ phép thành công.", "Đóng");
                await RefreshAsync();
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Không thể rút đơn", result.Message, "Đóng");
                await RefreshAsync();
            }
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("Lỗi", "Không thể rút đơn lúc này. Vui lòng thử lại sau.", "Đóng");
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>Xoá form sau khi gửi thành công nhưng GIỮ lựa chọn loại đơn.</summary>
    private void ResetForm()
    {
        LyDo = string.Empty;
        TuNgay = DateTime.Today.AddDays(1);
        DenNgay = DateTime.Today.AddDays(1);
        FieldError = string.Empty;
        OnPropertyChanged(nameof(LyDoCounterDisplay));
        OnPropertyChanged(nameof(SoNgayNghiDisplay));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}