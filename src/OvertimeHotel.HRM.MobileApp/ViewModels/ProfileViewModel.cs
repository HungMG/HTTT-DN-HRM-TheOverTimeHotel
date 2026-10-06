using System.ComponentModel;
using System.Runtime.CompilerServices;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel của màn hình Thông Tin Cá Nhân. Trang này chỉ hiển thị, không
/// có nút thao tác nào: đổi mật khẩu đã tách sang <see cref="ChangePasswordViewModel"/>,
/// đăng xuất nằm ở <see cref="SettingsViewModel"/> — Cài Đặt mở từng màn một.
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProfileViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoadUserInfo();
    }

    public void LoadUserInfo()
    {
        var user = _authService.CurrentUser;
        if (user != null && user.IsLoggedIn)
        {
            FullName = user.HoTen;
            Username = user.TenDangNhap;
            RoleName = HomeViewModelBase.LocalizeRole(user.TenVaiTro);
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
            FullName = "Phạm Thị Mai";
            Username = "employee";
            RoleName = "Nhân viên";
            DepartmentName = "Bộ Phận Tiền Sảnh (FO)";
            PositionName = "Nhân Viên Lễ Tân";
            Email = "mai.pham@overtimehotel.com";
            Phone = "0934567890";
            EmployeeId = 4;
            AvatarLetter = "M";
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