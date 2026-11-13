using OvertimeHotel.HRM.Core.Policies;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu chi tiết đơn từ và thao tác Phê duyệt / Từ chối (LeaveDetailPage).
/// Nhận dữ liệu truyền sang qua QueryProperty [Item] của .NET MAUI Shell.
/// Tuân thủ nguyên tắc Fail-Closed, async await an toàn và chỉ điều hướng khi server xác nhận thành công.
/// </summary>
[QueryProperty(nameof(RequestItem), "Item")]
public class LeaveDetailViewModel : INotifyPropertyChanged
{
    private readonly ILeaveApprovalService _leaveApprovalService;
    private readonly IAuthService _authService;

    private LeaveRequestItem? _requestItem;
    private string _rejectReason = string.Empty;
    private bool _isRejectModalVisible;
    private bool _hasRejectError;
    private string _rejectErrorMessage = string.Empty;
    private bool _isSubmitting;

    // Trạng thái Custom Luxury Modal Popup
    private bool _isCustomModalVisible;
    private string _modalTitle = string.Empty;
    private string _modalMessage = string.Empty;
    private string _modalIconText = "★";
    private string _modalIconTextColor = "#B45309";
    private string _modalIconBgColor = "#FFFBEB";
    private string _modalBorderColor = "#D4AF37";
    private string _modalConfirmButtonText = "ĐỒNG Ý";
    private bool _modalShowCancel;
    private Func<Task>? _modalConfirmAction;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OnRejectValidationError;

    public ICommand ApproveCommand { get; }
    public ICommand OpenRejectModalCommand { get; }
    public ICommand CloseRejectModalCommand { get; }
    public ICommand ConfirmRejectCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand CustomModalConfirmCommand { get; }
    public ICommand CustomModalCancelCommand { get; }

    public bool IsDemoMode => _leaveApprovalService.IsDemoMode;

    public LeaveDetailViewModel(ILeaveApprovalService leaveApprovalService, IAuthService authService)
    {
        _leaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        ApproveCommand = new Command(ExecuteApprovePrompt, () => CanAct);
        OpenRejectModalCommand = new Command(ExecuteOpenRejectModal, () => CanAct);
        CloseRejectModalCommand = new Command(ExecuteCloseRejectModal);
        ConfirmRejectCommand = new Command(async () => await ExecuteConfirmRejectAsync(), () => !IsSubmitting);
        BackCommand = new Command(async () => await ExecuteBackAsync());
        CustomModalConfirmCommand = new Command(async () => await ExecuteCustomModalConfirmAsync());
        CustomModalCancelCommand = new Command(ExecuteCustomModalCancel);
    }

    public LeaveRequestItem? RequestItem
    {
        get => _requestItem;
        set
        {
            if (SetProperty(ref _requestItem, value))
            {
                OnPropertyChanged(nameof(CanAct));
                OnPropertyChanged(nameof(IsReviewed));
                (ApproveCommand as Command)?.ChangeCanExecute();
                (OpenRejectModalCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool CanAct =>
        RequestItem is { TrangThai: "CHO_DUYET" } item &&
        _authService.CurrentUser is { IsLoggedIn: true } user &&
        user.CanReviewRequest(item.MaLoaiDon) &&
        // Truyền cả hai mã nhân viên để policy loại đơn của chính mình. Server vẫn
        // kiểm tra lại, nhưng ẩn nút ngay từ đây cho khỏi bấm phải mới báo lỗi.
        (!user.IsManager || (user.MaPhongBan.HasValue && user.MaPhongBan == item.MaPhongBan)) &&
        LeaveApprovalPolicy.CanReview(
            user.IsManager ? "HR" : user.TenVaiTro, user.MaPhongBan, item.MaPhongBan, item.MaLoaiDon,
            user.MaNhanVien, item.MaNhanVien) &&
        !IsSubmitting;

    public bool IsReviewed => RequestItem != null && RequestItem.TrangThai != "CHO_DUYET";

    public string RejectReason
    {
        get => _rejectReason;
        set
        {
            if (SetProperty(ref _rejectReason, value))
            {
                if (HasRejectError && !string.IsNullOrWhiteSpace(value))
                {
                    HasRejectError = false;
                    RejectErrorMessage = string.Empty;
                }
            }
        }
    }

    public bool IsRejectModalVisible
    {
        get => _isRejectModalVisible;
        set => SetProperty(ref _isRejectModalVisible, value);
    }

    public bool HasRejectError
    {
        get => _hasRejectError;
        set => SetProperty(ref _hasRejectError, value);
    }

    public string RejectErrorMessage
    {
        get => _rejectErrorMessage;
        set => SetProperty(ref _rejectErrorMessage, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                OnPropertyChanged(nameof(CanAct));
                (ApproveCommand as Command)?.ChangeCanExecute();
                (OpenRejectModalCommand as Command)?.ChangeCanExecute();
                (ConfirmRejectCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    // === PROPERTIES MODAL POPUP LUXURY ===

    public bool IsCustomModalVisible
    {
        get => _isCustomModalVisible;
        set => SetProperty(ref _isCustomModalVisible, value);
    }

    public string ModalTitle
    {
        get => _modalTitle;
        set => SetProperty(ref _modalTitle, value);
    }

    public string ModalMessage
    {
        get => _modalMessage;
        set => SetProperty(ref _modalMessage, value);
    }

    public string ModalIconText
    {
        get => _modalIconText;
        set => SetProperty(ref _modalIconText, value);
    }

    public string ModalIconTextColor
    {
        get => _modalIconTextColor;
        set => SetProperty(ref _modalIconTextColor, value);
    }

    public string ModalIconBgColor
    {
        get => _modalIconBgColor;
        set => SetProperty(ref _modalIconBgColor, value);
    }

    public string ModalBorderColor
    {
        get => _modalBorderColor;
        set => SetProperty(ref _modalBorderColor, value);
    }

    public string ModalConfirmButtonText
    {
        get => _modalConfirmButtonText;
        set => SetProperty(ref _modalConfirmButtonText, value);
    }

    public bool ModalShowCancel
    {
        get => _modalShowCancel;
        set => SetProperty(ref _modalShowCancel, value);
    }

    // === HÀNH ĐỘNG DUYỆT ĐƠN ===

    private void ExecuteApprovePrompt()
    {
        if (RequestItem == null || !CanAct) return;

        ShowCustomModal(
            title: "Xác Nhận Phê Duyệt",
            message: $"Bạn có chắc chắn muốn phê duyệt đơn [{RequestItem.TenLoaiDon}] của nhân viên {RequestItem.TenNhanVien} ({RequestItem.SoNgayNghiDisplay}) không?",
            iconText: "✓",
            iconTextColor: "#15803D",
            iconBgColor: "#DCFCE7",
            borderColor: "#16A34A",
            confirmText: "PHÊ DUYỆT",
            showCancel: true,
            onConfirm: async () => await PerformApproveAsync()
        );
    }

    private async Task PerformApproveAsync()
    {
        if (RequestItem == null || !CanAct) return;
        IsSubmitting = true;

        try
        {
            // Server tự giải quyết MaNguoiDuyet từ JWT, client không gửi
            var result = await _leaveApprovalService.ApproveAsync(RequestItem.MaDon).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsSubmitting = false;

                if (result.Success)
                {
                    RequestItem.TrangThai = "DA_DUYET";
                    RequestItem.NgayDuyet = DateTimeOffset.UtcNow;
                    OnPropertyChanged(nameof(CanAct));
                    OnPropertyChanged(nameof(IsReviewed));

                    ShowCustomModal(
                        title: "Duyệt Thành Công",
                        message: result.Message,
                        iconText: "✓",
                        iconTextColor: "#15803D",
                        iconBgColor: "#DCFCE7",
                        borderColor: "#16A34A",
                        confirmText: "HOÀN TẤT",
                        showCancel: false,
                        onConfirm: async () =>
                        {
                            if (Shell.Current != null)
                            {
                                await Shell.Current.GoToAsync("..");
                            }
                        }
                    );
                }
                else
                {
                    // Thất bại: Giữ nguyên màn hình cho phép thử lại
                    ShowCustomModal(
                        title: "Không Thể Phê Duyệt",
                        message: result.Message,
                        iconText: "✕",
                        iconTextColor: "#DC2626",
                        iconBgColor: "#FEE2E2",
                        borderColor: "#DC2626",
                        confirmText: "ĐÓNG",
                        showCancel: false,
                        onConfirm: () => Task.CompletedTask
                    );
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsSubmitting = false;
                ShowCustomModal(
                    title: "Lỗi Kết Nối",
                    message: ex.Message,
                    iconText: "✕",
                    iconTextColor: "#DC2626",
                    iconBgColor: "#FEE2E2",
                    borderColor: "#DC2626",
                    confirmText: "ĐÓNG",
                    showCancel: false,
                    onConfirm: () => Task.CompletedTask
                );
            });
        }
    }

    // === HÀNH ĐỘNG TỪ CHỐI ĐƠN ===

    private void ExecuteOpenRejectModal()
    {
        if (!CanAct) return;
        RejectReason = string.Empty;
        HasRejectError = false;
        RejectErrorMessage = string.Empty;
        IsRejectModalVisible = true;
    }

    private void ExecuteCloseRejectModal()
    {
        IsRejectModalVisible = false;
        RejectReason = string.Empty;
        HasRejectError = false;
        RejectErrorMessage = string.Empty;
    }

    private async Task ExecuteConfirmRejectAsync()
    {
        if (!CanAct) return;
        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            HasRejectError = true;
            RejectErrorMessage = "Vui lòng nhập lý do từ chối để thông báo đến nhân viên!";
            OnRejectValidationError?.Invoke();
            return;
        }

        if (RejectReason.Trim().Length > 255)
        {
            HasRejectError = true;
            RejectErrorMessage = "Lý do từ chối không được vượt quá 255 ký tự!";
            OnRejectValidationError?.Invoke();
            return;
        }

        if (RequestItem == null || !CanAct) return;
        IsSubmitting = true;

        try
        {
            var reason = RejectReason.Trim();
            var result = await _leaveApprovalService.RejectAsync(RequestItem.MaDon, reason).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsSubmitting = false;
                IsRejectModalVisible = false;

                if (result.Success)
                {
                    RequestItem.TrangThai = "TU_CHOI";
                    RequestItem.NgayDuyet = DateTimeOffset.UtcNow;
                    RequestItem.PhanHoiDuyet = reason;
                    OnPropertyChanged(nameof(CanAct));
                    OnPropertyChanged(nameof(IsReviewed));

                    ShowCustomModal(
                        title: "Đã Từ Chối Đơn",
                        message: result.Message,
                        iconText: "✕",
                        iconTextColor: "#DC2626",
                        iconBgColor: "#FEE2E2",
                        borderColor: "#DC2626",
                        confirmText: "HOÀN TẤT",
                        showCancel: false,
                        onConfirm: async () =>
                        {
                            if (Shell.Current != null)
                            {
                                await Shell.Current.GoToAsync("..");
                            }
                        }
                    );
                }
                else
                {
                    // Thất bại: Giữ nguyên màn hình cho phép thử lại
                    ShowCustomModal(
                        title: "Không Thể Từ Chối",
                        message: result.Message,
                        iconText: "!",
                        iconTextColor: "#DC2626",
                        iconBgColor: "#FEE2E2",
                        borderColor: "#DC2626",
                        confirmText: "ĐÓNG",
                        showCancel: false,
                        onConfirm: () => Task.CompletedTask
                    );
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsSubmitting = false;
                ShowCustomModal(
                    title: "Lỗi Kết Nối",
                    message: ex.Message,
                    iconText: "!",
                    iconTextColor: "#DC2626",
                    iconBgColor: "#FEE2E2",
                    borderColor: "#DC2626",
                    confirmText: "ĐÓNG",
                    showCancel: false,
                    onConfirm: () => Task.CompletedTask
                );
            });
        }
    }

    private async Task ExecuteBackAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    // === HỖ TRỢ CUSTOM MODAL POPUP ===

    private void ShowCustomModal(string title, string message, string iconText, string iconTextColor,
                                  string iconBgColor, string borderColor, string confirmText,
                                  bool showCancel, Func<Task>? onConfirm)
    {
        ModalTitle = title;
        ModalMessage = message;
        ModalIconText = iconText;
        ModalIconTextColor = iconTextColor;
        ModalIconBgColor = iconBgColor;
        ModalBorderColor = borderColor;
        ModalConfirmButtonText = confirmText;
        ModalShowCancel = showCancel;
        _modalConfirmAction = onConfirm;
        IsCustomModalVisible = true;
    }

    private async Task ExecuteCustomModalConfirmAsync()
    {
        IsCustomModalVisible = false;
        var action = _modalConfirmAction;
        _modalConfirmAction = null;
        if (action != null)
        {
            await action.Invoke();
        }
    }

    private void ExecuteCustomModalCancel()
    {
        IsCustomModalVisible = false;
        _modalConfirmAction = null;
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
