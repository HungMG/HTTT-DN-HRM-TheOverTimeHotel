using System.ComponentModel;
using System.Net.Http;
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

    // Nhãn trạng thái máy chủ: khởi tạo ở trạng thái "đang kiểm tra" và chuyển sang
    // xanh/đỏ sau khi ping xong, để không bao giờ hiện xanh khi chưa biết gì.
    private string _serverStatusText = "● Đang kiểm tra...";
    private Color _serverStatusColor = Color.FromArgb("#94A3B8");

    // Trạng thái Custom Luxury Modal Popup
    private bool _isPopupVisible;
    private string _popupTitle = string.Empty;
    private string _popupMessage = string.Empty;
    private string _popupButtonText = "Đồng Ý";
    private string _popupIconText = "★";
    private string _popupIconBgColor = "#FFFBEB";
    private string _popupIconTextColor = "#B45309";
    private string _popupBorderColor = "#D4AF37";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoginCommand = new Command(async () => await ExecuteLoginAsync(), () => !IsLoading);
        TogglePasswordCommand = new Command(ExecuteTogglePassword);
        BiometricLoginCommand = new Command(async () => await ExecuteBiometricLoginAsync(), () => !IsLoading);

        // Mặc định nạp sẵn tài khoản nhân viên để demo nhanh
        ExecuteQuickFill("employee");
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

    // Trạng thái kết nối máy chủ hiển thị cạnh ô "Ghi nhớ đăng nhập". Trước đây nhãn
    // này là chữ tĩnh trong XAML nên luôn sáng xanh dù máy chủ có thể chết — người
    // dùng tưởng hệ thống sẵn sàng rồi mới gõ mật khẩu. Giờ nó phản ánh đúng kết
    // quả ping: xám khi đang hỏi, xanh khi thấy máy chủ, đỏ khi không kết nối được.
    public string ServerStatusText
    {
        get => _serverStatusText;
        private set => SetProperty(ref _serverStatusText, value);
    }

    public Color ServerStatusColor
    {
        get => _serverStatusColor;
        private set => SetProperty(ref _serverStatusColor, value);
    }

    /// <summary>
    /// Hỏi máy chủ API còn sống không, rồi cập nhật nhãn trạng thái cho khớp.
    ///
    /// Gọi HEAD tới gốc chứ không gọi mốc nghiệp vụ nào: cần phân biệt "máy chủ chết"
    /// với "tài khoản sai", nên không được gửi kèm Bearer token — ping phải chạy được
    /// cả lúc chưa đăng nhập. HEAD cũng không sinh phụ thuộc vào CSDL nên hỏng CSDL
    /// vẫn báo xanh, đúng ý nghĩa "máy chủ trả lời".
    ///
    /// Mọi lỗi bị nuốt và chỉ phản ánh qua màu chữ: màn hình đăng nhập không được
    /// chết vì một cuộc ping thất bại.
    /// </summary>
    public async Task InitializeServerStatusAsync()
    {
        ServerStatusText = "● Đang kiểm tra...";
        ServerStatusColor = Color.FromArgb("#94A3B8");

        // Timeout ngắn (4s) để máy chủ chết hiện trạng thái đỏ nhanh thay vì treo.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, ApiConfig.BaseUrl);
            using var response = await client.SendAsync(request);

            // Máy chủ trả 401/404 vẫn nghĩa là nó đang sống và nói chuyện được với mình;
            // chỉ coi là chết khi không nhận được bất kỳ câu trả lời HTTP nào.
            ServerStatusText = "● Máy chủ sẵn sàng";
            ServerStatusColor = Color.FromArgb("#059669");
        }
        catch (HttpRequestException)
        {
            ServerStatusText = "● Máy chủ không phản hồi";
            ServerStatusColor = Color.FromArgb("#DC2626");
        }
        catch (TaskCanceledException)
        {
            // HttpClient ném TaskCanceledException khi hết Timeout.
            ServerStatusText = "● Máy chủ không phản hồi";
            ServerStatusColor = Color.FromArgb("#DC2626");
        }
        catch (Exception)
        {
            // DNS lỗi, sai cấu hình BaseUrl... đều quy về cùng một trạng thái hiển thị.
            ServerStatusText = "● Máy chủ không phản hồi";
            ServerStatusColor = Color.FromArgb("#DC2626");
        }
    }

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
    public ICommand BiometricLoginCommand { get; }

    public async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowCustomPopup("Thông Báo", "Vui lòng nhập tên đăng nhập hoặc mã nhân viên.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowCustomPopup("Thông Báo", "Vui lòng nhập mật khẩu đăng nhập.");
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
                await NavigateToBranchAsync();
            }
            else
            {
                ErrorMessage = result.Message;
                ShowCustomPopup("Đăng Nhập Thất Bại", result.Message);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi: {ex.Message}";
            ShowCustomPopup("Lỗi Hệ Thống", ex.Message);
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

    public void ExecuteQuickFill(string? username)
    {
        switch (username?.ToLowerInvariant())
        {
            // Tài khoản mẫu phải khớp đúng bộ seed trong DemoAuthService, nếu không
            // người dùng sẽ gõ đúng nhưng vẫn bị từ chối. admin và hr có mật khẩu riêng
            // nên không điền sẵn — tránh điền sai gây ảo giác lỗi.
            case "employee":
                Username = "employee";
                Password = "emp123";
                break;
            case "manager":
                Username = "manager";
                Password = "manager123";
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
            ExecuteQuickFill("employee");

            var result = await _authService.LoginAsync(Username, Password, RememberMe);

            if (result.Success)
            {
                await NavigateToBranchAsync();
            }
            else
            {
                ShowCustomPopup("Lỗi Sinh Trắc Học", "Không thể nhận diện khuôn mặt. Vui lòng thử lại.");
            }
        }
        catch (Exception ex)
        {
            ShowCustomPopup("Lỗi", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Chuẩn bị nội dung cho Custom Luxury Modal Popup trạng thái lỗi/cảnh báo.
    /// Không dùng DisplayAlert thô theo quy chuẩn thiết kế §3.
    /// </summary>
    private void ShowCustomPopup(string title, string message)
    {
        PopupTitle = title;
        PopupMessage = message;

        PopupIconText = "!";
        PopupIconBgColor = "#FEF2F2"; // Đỏ nhạt
        PopupIconTextColor = "#DC2626"; // Đỏ tươi
        PopupBorderColor = "#FCA5A5";
        PopupButtonText = "ĐỒNG Ý";

        IsPopupVisible = true;
    }

    /// <summary>
    /// Vào phần sau đăng nhập. AppShell tự dựng thanh tab đúng theo vai trò do CSDL
    /// trả về: nhân viên có tab trang chủ của mình, còn lại (Quản lý, HR, Quản trị
    /// viên) có tab quản lý — mỗi bên không thấy tab của bên kia.
    /// </summary>
    private async Task NavigateToBranchAsync()
    {
        if (Shell.Current is AppShell shell)
        {
            await shell.OpenHomeAsync();
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
