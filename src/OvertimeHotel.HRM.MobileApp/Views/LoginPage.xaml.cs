using System.ComponentModel;
using System.Runtime.Versioning;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn hình đăng nhập của The OverTime Hotel.
/// Vai trò không cần chọn tay ở đây — CSDL quyết định sau khi xác thực tài khoản,
/// và <see cref="LoginViewModel"/> điều hướng sang nhánh trang chủ tương ứng.
/// </summary>
public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private CancellationTokenSource? _shimmerCts;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ApplyBrandStatusBar();

        // Hỏi máy chủ API còn sống không. Fire-and-forget để không đợi mạng: dù máy
        // chủ treo tới hết timeout thì phần đăng nhập vẫn hiện và vẫn gõ được.
        _ = _viewModel.InitializeServerStatusAsync();

        // Dải đầu và thẻ đăng nhập là hai khối nằm chồng nhau (thẻ nổi lên dải navy).
        // Hoạt ảnh riêng từng khối để không nhân đôi Transform trên cùng một cây.
        brandHeader.Opacity = 0;
        brandHeader.TranslationY = -40;

        formCardBorder.Opacity = 0;
        formCardBorder.TranslationY = 45;
        formCardBorder.Scale = 0.94;

        accountHintSection.Opacity = 0;
        accountHintSection.TranslationY = 30;

        await Task.Delay(80);
        _ = Task.WhenAll(
            brandHeader.FadeToAsync(1.0, 550, Easing.CubicOut),
            brandHeader.TranslateToAsync(0, 0, 600, Easing.CubicOut)
        );

        await Task.Delay(160);
        _ = Task.WhenAll(
            formCardBorder.FadeToAsync(1.0, 650, Easing.CubicOut),
            formCardBorder.TranslateToAsync(0, 0, 750, Easing.SpringOut),
            formCardBorder.ScaleToAsync(1.0, 750, Easing.SpringOut)
        );

        await Task.Delay(140);
        _ = Task.WhenAll(
            accountHintSection.FadeToAsync(1.0, 500, Easing.CubicOut),
            accountHintSection.TranslateToAsync(0, 0, 600, Easing.CubicOut)
        );
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _shimmerCts?.Cancel();
        _shimmerCts = null;
    }

    /// <summary>
    /// Ép biểu tượng thanh trạng thái (đồng hồ, pin, sóng) hiện màu TỐI.
    /// <para>
    /// Android 15 (API 35) đã bỏ hiệu lực <c>Window.SetStatusBarColor</c>, nên không thể
    /// tô nền thanh trạng thái theo dải navy của trang — hệ thống luôn vẽ một lớp
    /// scrim xám sáng (#E6E7E9). Trên nền sáng đó, biểu tượng mặc định màu trắng gần
    /// như chìm hẳn; đổi sang màu tối là cách duy nhất còn hiệu lực trên API 35.
    /// </para>
    /// </summary>
    private static void ApplyBrandStatusBar()
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.Android)
        {
            return;
        }

        // WindowInsetsController chỉ có từ Android 11 (API 30).
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            return;
        }

        // Android.App.Activity.Current không còn tồn tại trong .NET 10 for Android;
        // Platform.CurrentActivity là API chính thức của MAUI cho việc này.
        var window = Platform.CurrentActivity?.Window;
        if (window is null)
        {
            return;
        }

        // OnAppearing đã chạy trên UI thread nên gọi thẳng, không bọc RunOnUiThread:
        // trạng thái "đã kiểm tra API 30" không truyền được qua biên lambda, bọc vào
        // đó thì trình phân tích CA1416 lại mất dấu và cảnh báo quay về.
        SetDarkStatusBarIcons(window);
    }

    /// <summary>
    /// Bật cờ APPEARANCE_LIGHT_STATUS_BARS (tên cờ gây hiểu nhầm: nghĩa là biểu tượng
    /// thanh trạng thái hiện màu TỐI) để đồng hồ và các glyph nổi rõ trên lớp scrim
    /// xám sáng mặc định của Android 15.
    /// </summary>
    /// <remarks>
    /// Phải tách riêng một hàm và gắn <see cref="SupportedOSPlatformAttribute"/> để
    /// <c>SupportedOSPlatform</c> tự chứng minh tính hợp lệ thay vì bắt buộc mọi nơi
    /// gọi phải tự kiểm tra phiên bản — nhưng chỉ dùng được khi lệnh gọi nằm trực tiếp
    /// trong nhánh đã đối chiếu <c>OperatingSystem.IsAndroidVersionAtLeast(30)</c>.
    /// </remarks>
    [SupportedOSPlatform("android30.0")]
    private static void SetDarkStatusBarIcons(Android.Views.Window window)
    {
        const int lightBars = (int)Android.Views.WindowInsetsControllerAppearance.LightStatusBars;
        window.InsetsController?.SetSystemBarsAppearance(lightBars, lightBars);
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Skeleton shimmer thay cho vòng tròn xoay (quy chuẩn §2)
        if (e.PropertyName == nameof(LoginViewModel.IsLoading))
        {
            if (_viewModel.IsLoading)
            {
                _shimmerCts?.Cancel();
                _shimmerCts = new CancellationTokenSource();
                _ = skeletonLayout.ShimmerLoopAsync(_shimmerCts.Token);
            }
            else
            {
                _shimmerCts?.Cancel();
                _shimmerCts = null;
            }
        }

        // Rung lắc báo lỗi khi nhập sai mật khẩu (iOS Error Shake)
        if (e.PropertyName == nameof(LoginViewModel.HasError) && _viewModel.HasError)
        {
            await formCardBorder.ShakeAsync(8, 3);
        }

        // Popup lỗi: nảy lò xo từ tâm màn hình thay cho DisplayAlert thô
        if (e.PropertyName == nameof(LoginViewModel.IsPopupVisible) && _viewModel.IsPopupVisible)
        {
            popup.Show(
                title: _viewModel.PopupTitle,
                message: _viewModel.PopupMessage,
                iconText: _viewModel.PopupIconText,
                iconTextColor: _viewModel.PopupIconTextColor,
                iconBgColor: _viewModel.PopupIconBgColor,
                borderColor: _viewModel.PopupBorderColor,
                confirmText: _viewModel.PopupButtonText,
                showCancel: false,
                onConfirm: null);
        }
    }

    // Hiệu ứng nảy lò xo đàn hồi khi chạm nút
    private async void OnLoginButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            await btn.SpringTapAsync();
        }
    }

    private async void OnBiometricButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            await btn.SpringTapAsync();
        }
    }
}