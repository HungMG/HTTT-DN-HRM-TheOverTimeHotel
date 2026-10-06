using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn hình đổi mật khẩu, mở từ tab Cài Đặt. Đây là trang push nên giữ nút quay lại.
/// </summary>
public partial class ChangePasswordPage : ContentPage
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordPage(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        SafeAreaInset.Track(this, pageBody, new Thickness(20, 0, 20, 32));

        BindingContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Đặt vị trí ban đầu (Opacity đã khai 0 trong XAML nên không bị chớp 1 khung hình)
        pageHeader.TranslationY = -25;
        passwordCard.TranslationY = 35;

        await Task.Delay(50);

        // 2. Trượt vào so le bằng CubicOut — hãm phấm êm, không dùng SpringOut để tránh vọt lố
        _ = Task.WhenAll(
            pageHeader.FadeToAsync(1.0, 400, Easing.CubicOut),
            pageHeader.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        await Task.Delay(110);
        _ = Task.WhenAll(
            passwordCard.FadeToAsync(1.0, 450, Easing.CubicOut),
            passwordCard.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChangePasswordViewModel.IsPopupVisible) && _viewModel.IsPopupVisible)
        {
            changePasswordModalOverlay.Opacity = 0;
            changePasswordModalCard.Scale = 0.75;
            changePasswordModalCard.Opacity = 0;

            _ = changePasswordModalOverlay.FadeToAsync(1, 180, Easing.CubicOut);
            await changePasswordModalCard.BounceInAsync(360);
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
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