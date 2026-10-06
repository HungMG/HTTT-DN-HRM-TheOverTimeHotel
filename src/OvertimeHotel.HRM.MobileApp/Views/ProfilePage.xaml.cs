using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn Thông tin cá nhân. Mở từ dòng menu của tab Cài Đặt, hoặc từ thẻ
/// "Hồ Sơ" trên trang chủ — cả hai đều là trang push nên có nút quay lại.
/// Phần đổi mật khẩu đã tách sang <see cref="ChangePasswordPage"/>.
/// </summary>
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        SafeAreaInset.Track(this, pageBody, new Thickness(20, 0, 20, 32));

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUserInfo();

        // 1. Đặt vị trí ban đầu (Opacity đã là 0 trong XAML nên hoàn toàn không bị chớp hay giật)
        profileHeader.TranslationY = -25;
        identityCard.TranslationY = 35;

        await Task.Delay(50);

        // 2. Chạy chuỗi hoạt ảnh trượt vào êm ái bằng CubicOut (mượt mà 60 FPS, không khựng)
        _ = Task.WhenAll(
            profileHeader.FadeToAsync(1.0, 400, Easing.CubicOut),
            profileHeader.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        await Task.Delay(110);
        _ = Task.WhenAll(
            identityCard.FadeToAsync(1.0, 450, Easing.CubicOut),
            identityCard.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }
}
