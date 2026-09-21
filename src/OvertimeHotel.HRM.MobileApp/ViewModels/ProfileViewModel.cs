using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu và logic của màn hình Thông Tin Cá Nhân &amp; Đổi Mật Khẩu (Profile).
/// </summary>
public class ProfileViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    // Thông tin nhân viên
    private string _fullName = string.Empty;
    private string _username = string.Empty;
    private string _roleName = string.Empty;
    private string _departmentName = string.Empty;
    private string _positionName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private int _employeeId;
    private string _avatarLetter = "U";

    // Đổi mật khẩu
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
    private bool _isPasswordSuccess;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProfileViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        ChangePasswordCommand = new Command(async () => await ExecuteChangePasswordAsync(), () => !IsLoading);
        TogglePasswordCommand = new Command(ExecuteTogglePassword);
        ClosePopupCommand = new Command(ExecuteClosePopup);
        BackCommand = new Command(async () => await ExecuteBackAsync());
        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());

        LoadUserInfo();
    }

    public void LoadUserInfo()
    {
        var user = _authService.CurrentUser;
        if (user != null && user.IsLoggedIn)
        {
            FullName = user.HoTen;
            Username = user.TenDangNhap;
            RoleName = user.TenVaiTro;
            DepartmentName = user.TenPhongBan;
            PositionName = user.TenChucVu;
            Email = string.IsNullOrWhiteSpace(user.Email) ? $"{user.TenDangNhap}@overtimehotel.vn" : user.Email;
            Phone = string.IsNullOrWhiteSpace(user.DienThoai) ? "0908 123 456" : user.DienThoai;
            EmployeeId = user.MaNhanVien;

            if (!string.IsNullOrWhiteSpace(user.HoTen))
            {
                var parts = user.HoTen.Trim().Split(' ');
                AvatarLetter = parts[^1][0].ToString().ToUpper();
            }
            else
            {
                AvatarLetter = "U";
            }
        }
        else
        {
            FullName = "Ngô Thị Bích";
            Username = "ngothibich";
            RoleName = "Employee";
            DepartmentName = "Bộ phận Buồng phòng";
            PositionName = "Nhân viên Buồng phòng";
            Email = "bich.ngo@overtimehotel.vn";
            Phone = "0912 345 678";
            EmployeeId = 13;
            AvatarLetter = "B";
        }
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string RoleName
    {
        get => _roleName;
        set => SetProperty(ref _roleName, value);
    }

    public string DepartmentName
    {
        get => _departmentName;
        set => SetProperty(ref _departmentName, value);
    }

    public string PositionName
    {
        get => _positionName;
        set => SetProperty(ref _positionName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public int EmployeeId
    {
        get => _employeeId;
        set => SetProperty(ref _employeeId, value);
    }

    public string AvatarLetter
    {
        get => _avatarLetter;
        set => SetProperty(ref _avatarLetter, value);
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
    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }

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
            await Task.Delay(400); // Tạo hiệu ứng tải mượt mà

            var (success, message) = await _authService.ChangePasswordAsync(CurrentPassword, NewPassword);

            if (success)
            {
                _isPasswordSuccess = true;
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;

                ShowPopup("Đổi Mật Khẩu Thành Công", message, true);
            }
            else
            {
                _isPasswordSuccess = false;
                ShowPopup("Đổi Mật Khẩu Thất Bại", message, false);
            }
        }
        catch (Exception ex)
        {
            _isPasswordSuccess = false;
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

    private async Task ExecuteBackAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    private async Task ExecuteLogoutAsync()
    {
        await _authService.LogoutAsync();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//LoginPage");
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
