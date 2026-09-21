using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUserInfo();

        // 1. Đặt vị trí ban đầu (Opacity đã là 0 trong XAML nên hoàn toàn không bị chớp hay giật)
        profileHeader.TranslationY = -25;
        identityCard.TranslationY = 35;
        passwordCard.TranslationY = 35;

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

        await Task.Delay(110);
        _ = Task.WhenAll(
            passwordCard.FadeToAsync(1.0, 450, Easing.CubicOut),
            passwordCard.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileViewModel.IsPopupVisible) && _viewModel.IsPopupVisible)
        {
            profileModalOverlay.Opacity = 0;
            profileModalCard.Scale = 0.75;
            profileModalCard.Opacity = 0;

            _ = profileModalOverlay.FadeToAsync(1, 180, Easing.CubicOut);
            await profileModalCard.BounceInAsync(360);
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
    }

    private async void OnLogoutButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
    }

    private async void OnChangePasswordButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
    }

    private async void OnPopupDismissClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
    }
}
