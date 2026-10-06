using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel của màn hình Đổi Mật Khẩu. Tách khỏi <see cref="ProfileViewModel"/>
/// để Cài Đặt mở được từng màn một, mỗi màn một nhiệm vụ.
/// </summary>
public class ChangePasswordViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isPasswordHidden = true;
    private bool _isLoading;

    // Custom Modal Popup Feedback
    private bool _isPopupVisible;
    private string _popupTitle = string.Empty;
    private string _popupMessage = string.Empty;
    private string _popupButtonText = "ĐỒNG Ý";
    private string _popupIconText = "★";
    private string _popupIconBgColor = "#FFFBEB";
    private string _popupIconTextColor = "#B45309";
    private string _popupBorderColor = "#D4AF37";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChangePasswordViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        ChangePasswordCommand = new Command(async () => await ExecuteChangePasswordAsync(), () => !IsLoading);
        TogglePasswordCommand = new Command(ExecuteTogglePassword);
        ClosePopupCommand = new Command(ExecuteClosePopup);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set => SetProperty(ref _isPasswordHidden, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (ChangePasswordCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    // Custom Modal Popup Properties
    public bool IsPopupVisible
    {
        get => _isPopupVisible;
        set => SetProperty(ref _isPopupVisible, value);
    }

    public string PopupTitle
    {
        get => _popupTitle;
        set => SetProperty(ref _popupTitle, value);
    }

    public string PopupMessage
    {
        get => _popupMessage;
        set => SetProperty(ref _popupMessage, value);
    }

    public string PopupButtonText
    {
        get => _popupButtonText;
        set => SetProperty(ref _popupButtonText, value);
    }

    public string PopupIconText
    {
        get => _popupIconText;
        set => SetProperty(ref _popupIconText, value);
    }

    public string PopupIconBgColor
    {
        get => _popupIconBgColor;
        set => SetProperty(ref _popupIconBgColor, value);
    }

    public string PopupIconTextColor
    {
        get => _popupIconTextColor;
        set => SetProperty(ref _popupIconTextColor, value);
    }

    public string PopupBorderColor
    {
        get => _popupBorderColor;
        set => SetProperty(ref _popupBorderColor, value);
    }

    public ICommand ChangePasswordCommand { get; }
    public ICommand TogglePasswordCommand { get; }
    public ICommand ClosePopupCommand { get; }

    private async Task ExecuteChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ShowPopup("Thông Báo", "Vui lòng nhập mật khẩu hiện tại.", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ShowPopup("Thông Báo", "Vui lòng nhập mật khẩu mới.", false);
            return;
        }

        if (NewPassword.Length < 6)
        {
            ShowPopup("Thông Báo", "Mật khẩu mới phải có ít nhất 6 ký tự.", false);
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ShowPopup("Thông Báo", "Mật khẩu xác nhận không khớp với mật khẩu mới.", false);
            return;
        }

        IsLoading = true;

        try
        {
            var (success, message) = await _authService.ChangePasswordAsync(CurrentPassword, NewPassword);

            if (success)
            {
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;

                ShowPopup("Đổi Mật Khẩu Thành Công", message, true);
            }
            else
            {
                ShowPopup("Đổi Mật Khẩu Thất Bại", message, false);
            }
        }
        catch (Exception ex)
        {
            ShowPopup("Lỗi Hệ Thống", ex.Message, false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteTogglePassword()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    private void ShowPopup(string title, string message, bool isSuccess)
    {
        PopupTitle = title;
        PopupMessage = message;

        if (isSuccess)
        {
            PopupIconText = "✓";
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

    private void ExecuteClosePopup()
    {
        IsPopupVisible = false;
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}