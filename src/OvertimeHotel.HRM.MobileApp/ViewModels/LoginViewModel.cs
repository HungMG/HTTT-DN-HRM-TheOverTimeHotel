using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu và hành động của màn hình Đăng nhập ứng dụng di động The OverTime Hotel.
/// Tuân thủ quy chuẩn: Skeleton Loading (thay cho spinner xoay) &amp; Custom Modal Popup (thay cho DisplayAlert thô).
/// </summary>
public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe = true;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private bool _isPasswordHidden = true;
    private string _selectedRole = "Employee";

    // Trạng thái Custom Luxury Modal Popup
    private bool _isPopupVisible;
    private string _popupTitle = string.Empty;
    private string _popupMessage = string.Empty;
    private string _popupButtonText = "Đồng Ý";
    private string _popupIconText = "★";
    private string _popupIconBgColor = "#FFFBEB";
    private string _popupIconTextColor = "#B45309";
    private string _popupBorderColor = "#D4AF37";
    private bool _popupIsSuccess;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoginCommand = new Command(async () => await ExecuteLoginAsync(), () => !IsLoading);
        TogglePasswordCommand = new Command(ExecuteTogglePassword);
        SelectRoleCommand = new Command<string>(ExecuteSelectRole);
        QuickFillCommand = new Command<string>(ExecuteQuickFill);
        BiometricLoginCommand = new Command(async () => await ExecuteBiometricLoginAsync(), () => !IsLoading);
        ClosePopupCommand = new Command(async () => await ExecuteClosePopupAsync());

        // Mặc định nạp sẵn tài khoản nhân viên buồng phòng để test nhanh
        ExecuteQuickFill("ngothibich");
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ClearError();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ClearError();
            }
        }
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                (LoginCommand as Command)?.ChangeCanExecute();
                (BiometricLoginCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool IsNotLoading => !IsLoading;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set => SetProperty(ref _isPasswordHidden, value);
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                OnPropertyChanged(nameof(IsEmployeeSelected));
                OnPropertyChanged(nameof(IsManagerSelected));
                OnPropertyChanged(nameof(RoleSubtitle));
            }
        }
    }

    public bool IsEmployeeSelected => SelectedRole == "Employee";
    public bool IsManagerSelected => SelectedRole == "Manager";
    public string RoleSubtitle => IsEmployeeSelected ? "Cổng dành cho Nhân viên ca 24/7" : "Cổng dành cho Quản lý bộ phận";

    // Custom Popup Properties
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

    public ICommand LoginCommand { get; }
    public ICommand TogglePasswordCommand { get; }
    public ICommand SelectRoleCommand { get; }
    public ICommand QuickFillCommand { get; }
    public ICommand BiometricLoginCommand { get; }
    public ICommand ClosePopupCommand { get; }

    public async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowCustomPopup("Thông Báo", "Vui lòng nhập tên đăng nhập hoặc mã nhân viên.", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowCustomPopup("Thông Báo", "Vui lòng nhập mật khẩu đăng nhập.", false);
            return;
        }

        // Bật hiệu ứng Skeleton Shimmer Loading (thay vì con xoay spinner)
        IsLoading = true;
        ClearError();

        try
        {
            // Tạm dừng 600ms để người dùng cảm nhận hiệu ứng Skeleton Shimmer sang trọng
            await Task.Delay(600);

            var result = await _authService.LoginAsync(Username, Password, RememberMe);

            if (result.Success)
            {
                // Khi đăng nhập thành công: Chuyển thẳng vào MainPage không cần bấm thêm bước xác nhận
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            else
            {
                _popupIsSuccess = false;
                ErrorMessage = result.Message;
                ShowCustomPopup("Đăng Nhập Thất Bại", result.Message, false);
            }
        }
        catch (Exception ex)
        {
            _popupIsSuccess = false;
            ErrorMessage = $"Lỗi: {ex.Message}";
            ShowCustomPopup("Lỗi Hệ Thống", ex.Message, false);
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

    private void ExecuteSelectRole(string? role)
    {
        if (!string.IsNullOrEmpty(role))
        {
            SelectedRole = role;
        }
    }

    public void ExecuteQuickFill(string? username)
    {
        switch (username?.ToLowerInvariant())
        {
            case "ngothibich":
                Username = "ngothibich";
                Password = "123456";
                SelectedRole = "Employee";
                break;
            case "dothanhdat":
                Username = "dothanhdat";
                Password = "123456";
                SelectedRole = "Manager";
                break;
            case "hr_sang":
                Username = "hr_sang";
                Password = "123456";
                SelectedRole = "Manager";
                break;
            case "admin":
                Username = "admin";
                Password = "123456";
                SelectedRole = "Manager";
                break;
        }
        ClearError();
    }

    private async Task ExecuteBiometricLoginAsync()
    {
        IsLoading = true;
        ClearError();

        try
        {
            // Giả lập quét sinh trắc học FaceID với Skeleton Shimmer
            await Task.Delay(800);
            ExecuteQuickFill("ngothibich");

            var result = await _authService.LoginAsync(Username, Password, RememberMe);

            if (result.Success)
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            else
            {
                _popupIsSuccess = false;
                ShowCustomPopup("Lỗi Sinh Trắc Học", "Không thể nhận diện khuôn mặt. Vui lòng thử lại.", false);
            }
        }
        catch (Exception ex)
        {
            _popupIsSuccess = false;
            ShowCustomPopup("Lỗi", ex.Message, false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowCustomPopup(string title, string message, bool isSuccess)
    {
        PopupTitle = title;
        PopupMessage = message;
        _popupIsSuccess = isSuccess;

        if (isSuccess)
        {
            PopupIconText = "★";
            PopupIconBgColor = "#FFFBEB"; // Vàng kim nhạt
            PopupIconTextColor = "#B45309"; // Vàng đậm
            PopupBorderColor = "#D4AF37"; // Vàng kim 5★
            PopupButtonText = "VÀO TRANG CHỦ";
        }
        else
        {
            PopupIconText = "!";
            PopupIconBgColor = "#FEF2F2"; // Đỏ nhạt
            PopupIconTextColor = "#DC2626"; // Đỏ tươi
            PopupBorderColor = "#FCA5A5";
            PopupButtonText = "ĐỒNG Ý";
        }

        IsPopupVisible = true;
    }

    private async Task ExecuteClosePopupAsync()
    {
        IsPopupVisible = false;

        // Nếu là popup đăng nhập thành công, điều hướng vào trang chủ
        if (_popupIsSuccess)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }

    private void ClearError()
    {
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            ErrorMessage = string.Empty;
        }
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
