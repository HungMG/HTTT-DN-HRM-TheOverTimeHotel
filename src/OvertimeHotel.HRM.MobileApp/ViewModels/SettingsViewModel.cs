using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel của tab Cài Đặt. Tab này chỉ là menu dẫn tới ba màn khác nhau —
/// thông tin cá nhân, đổi mật khẩu và đăng xuất — nên không tự giữ nội dung,
/// chỉ giữ tên người dùng để hiển thị trên dải đầu trang.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    private string _fullName = "Người dùng";
    private string _roleDepartmentLine = string.Empty;
    private string _username = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LogoutCommand = new Command(async () => await ExecuteLogoutAsync());

        LoadUserInfo();
    }

    /// <summary>Họ tên người đang đăng nhập.</summary>
    public string FullName
    {
        get => _fullName;
        private set => SetProperty(ref _fullName, value);
    }

    /// <summary>Dòng phụ "Phòng ban · Chức vụ (Vai trò)" dưới tên.</summary>
    public string RoleDepartmentLine
    {
        get => _roleDepartmentLine;
        private set => SetProperty(ref _roleDepartmentLine, value);
    }

    /// <summary>Tên tài khoản đăng nhập.</summary>
    public string Username
    {
        get => _username;
        private set => SetProperty(ref _username, value);
    }

    public ICommand LogoutCommand { get; }

    /// <summary>Nạp lại thông tin người dùng. Gọi mỗi lần tab hiện ra.</summary>
    public void LoadUserInfo()
    {
        var user = _authService.CurrentUser;

        if (user is null || !user.IsLoggedIn)
        {
            FullName = "Người dùng";
            RoleDepartmentLine = "The OverTime Hotel";
            Username = string.Empty;
            return;
        }

        FullName = string.IsNullOrWhiteSpace(user.HoTen) ? user.TenDangNhap : user.HoTen;
        RoleDepartmentLine = HomeViewModelBase.BuildRoleDepartmentLine(user);
        Username = user.TenDangNhap;
    }

    private async Task ExecuteLogoutAsync()
    {
        await _authService.LogoutAsync();

        if (Shell.Current is AppShell shell)
        {
            // Gỡ tab trước khi rời phiên: nếu không, lần đăng nhập sau sẽ dựng chồng
            // tab lên cây cũ và vai trò mới có thể thừa hưởng tab của vai trò cũ.
            shell.ClearRoleTabs();
            await shell.GoToAsync("//LoginPage");
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}