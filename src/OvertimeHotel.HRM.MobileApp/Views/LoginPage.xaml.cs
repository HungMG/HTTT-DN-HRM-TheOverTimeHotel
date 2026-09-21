using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

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

        // 1. KHỞI TẠO VỊ TRÍ ĐỂ CHẠY HIỆU ỨNG CÁC KHỐI BAY VÀO CHẬM RÃI & MƯỢT MÀ
        brandSection.Opacity = 0;
        brandSection.TranslationY = -45;

        roleSelectorBorder.Opacity = 0;
        roleSelectorBorder.TranslationY = 35;

        formCardBorder.Opacity = 0;
        formCardBorder.TranslationY = 65;
        formCardBorder.Scale = 0.90;

        quickChipsSection.Opacity = 0;
        quickChipsSection.TranslationY = 40;

        // 2. CHẠY CHUỖI HOẠT ẢNH BAY CÁC KHỐI NỐI TIẾP VỚI ĐỘ NẢY LÒ XO (SPRING PHYSICS)
        await Task.Delay(80);
        _ = Task.WhenAll(
            brandSection.FadeToAsync(1.0, 550, Easing.CubicOut),
            brandSection.TranslateToAsync(0, 0, 600, Easing.CubicOut)
        );

        await Task.Delay(160);
        _ = Task.WhenAll(
            roleSelectorBorder.FadeToAsync(1.0, 500, Easing.CubicOut),
            roleSelectorBorder.TranslateToAsync(0, 0, 550, Easing.CubicOut)
        );

        await Task.Delay(160);
        _ = Task.WhenAll(
            formCardBorder.FadeToAsync(1.0, 650, Easing.CubicOut),
            formCardBorder.TranslateToAsync(0, 0, 750, Easing.SpringOut),
            formCardBorder.ScaleToAsync(1.0, 750, Easing.SpringOut)
        );

        await Task.Delay(140);
        _ = Task.WhenAll(
            quickChipsSection.FadeToAsync(1.0, 500, Easing.CubicOut),
            quickChipsSection.TranslateToAsync(0, 0, 600, Easing.SpringOut)
        );
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Hiệu ứng Popup Modal nảy lò xo khi có lỗi
        if (e.PropertyName == nameof(LoginViewModel.IsPopupVisible) && _viewModel.IsPopupVisible)
        {
            popupOverlay.Opacity = 0;
            popupCard.Scale = 0.75;
            popupCard.Opacity = 0;

            _ = popupOverlay.FadeToAsync(1, 180, Easing.CubicOut);
            await popupCard.BounceInAsync(360);
        }

        // Hiệu ứng nhịp thở lướt sáng cho Skeleton (Breathing Shimmer Loop)
        if (e.PropertyName == nameof(LoginViewModel.IsLoading))
        {
            if (_viewModel.IsLoading)
            {
                _shimmerCts = new CancellationTokenSource();
                _ = skeletonLayout.ShimmerLoopAsync(_shimmerCts.Token);
            }
            else
            {
                _shimmerCts?.Cancel();
            }
        }

        // Hiệu ứng rung lắc báo lỗi khi nhập sai mật khẩu (iOS Error Shake)
        if (e.PropertyName == nameof(LoginViewModel.HasError) && _viewModel.HasError)
        {
            await formCardBorder.ShakeAsync(8, 3);
        }

        // Hiệu ứng chuyển biến chữ chú thích vai trò (Text Morphing) và nảy lò xo nút chọn
        if (e.PropertyName == nameof(LoginViewModel.SelectedRole))
        {
            _ = lblRoleSubtitle.MorphTextAsync(_viewModel.RoleSubtitle);
            var activeBtn = _viewModel.IsEmployeeSelected ? btnRoleEmployee : btnRoleManager;
            if (activeBtn != null)
            {
                _ = activeBtn.SpringTapAsync(0.95, 50, 150);
            }
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

    private async void OnRoleButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            await btn.SpringTapAsync();
        }
    }

    private async void OnChipButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            await btn.SpringTapAsync();
        }
    }

    private async void OnPopupConfirmClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn)
        {
            await btn.SpringTapAsync();
        }
    }
}
