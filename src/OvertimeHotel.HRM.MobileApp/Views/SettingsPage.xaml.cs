using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Tab Cài Đặt: menu ba dòng dẫn tới Thông tin cá nhân, Đổi mật khẩu và Đăng xuất.
/// Mỗi dòng là một trang push riêng nên cả hai màn con đều tự có nút quay lại.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Đổi mật khẩu xong hoặc quay lại từ màn con thì tên người dùng trên dải
        // đầu phải khớp lại với phiên hiện tại.
        _viewModel.LoadUserInfo();

        // 1. Đặt vị trí ban đầu (Opacity đã khai 0 trong XAML nên không chớp 1 khung hình)
        pageHeader.TranslationY = -25;
        identityStrip.TranslationY = 35;

        await Task.Delay(50);

        // 2. Trượt vào so le bằng CubicOut — hãm phấm êm, không dùng SpringOut để tránh vọt lố
        _ = Task.WhenAll(
            pageHeader.FadeToAsync(1.0, 400, Easing.CubicOut),
            pageHeader.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        await Task.Delay(110);
        _ = Task.WhenAll(
            identityStrip.FadeToAsync(1.0, 450, Easing.CubicOut),
            identityStrip.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        // 3. Các dòng menu trượt vào lần lượt theo nhịp so le
        await Task.Delay(90);
        _ = Task.WhenAll(
            accountHeader.FadeToAsync(1.0, 350, Easing.CubicOut),
            accountHeader.TranslateToAsync(0, 0, 400, Easing.CubicOut)
        );

        await Task.Delay(60);
        _ = Task.WhenAll(
            profileRow.FadeToAsync(1.0, 380, Easing.CubicOut),
            profileRow.TranslateToAsync(0, 0, 430, Easing.CubicOut)
        );

        await Task.Delay(60);
        _ = Task.WhenAll(
            passwordRow.FadeToAsync(1.0, 380, Easing.CubicOut),
            passwordRow.TranslateToAsync(0, 0, 430, Easing.CubicOut)
        );

        await Task.Delay(60);
        _ = Task.WhenAll(
            sessionHeader.FadeToAsync(1.0, 350, Easing.CubicOut),
            sessionHeader.TranslateToAsync(0, 0, 400, Easing.CubicOut)
        );

        await Task.Delay(60);
        _ = Task.WhenAll(
            logoutRow.FadeToAsync(1.0, 380, Easing.CubicOut),
            logoutRow.TranslateToAsync(0, 0, 430, Easing.CubicOut)
        );

        await Task.Delay(60);
        _ = Task.WhenAll(
            versionLabel.FadeToAsync(1.0, 400, Easing.CubicOut),
            versionLabel.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );
    }

    private async void OnProfileRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement row) await row.SpringTapAsync();
        await Shell.Current.GoToAsync("ProfilePage");
    }

    private async void OnPayslipTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("PayslipPage");

    private async void OnChangePasswordRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement row) await row.SpringTapAsync();
        await Shell.Current.GoToAsync("ChangePasswordPage");
    }

    private async void OnLogoutRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement row) await row.SpringTapAsync();
        if (_viewModel.LogoutCommand.CanExecute(null))
        {
            _viewModel.LogoutCommand.Execute(null);
        }
    }
}