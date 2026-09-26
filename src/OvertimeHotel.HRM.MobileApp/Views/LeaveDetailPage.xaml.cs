using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class LeaveDetailPage : ContentPage
{
    private readonly LeaveDetailViewModel _viewModel;

    public LeaveDetailPage(LeaveDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;

        // Bắt sự kiện lỗi validation từ ViewModel để chạy animation rung lắc khung nhập
        _viewModel.OnRejectValidationError += OnRejectValidationErrorReceived;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Hoạt ảnh lướt nhẹ tiêu đề
        detailHeader.Opacity = 0;
        detailHeader.TranslationY = -15;

        await Task.Delay(30);
        _ = Task.WhenAll(
            detailHeader.FadeToAsync(1.0, 300, Easing.CubicOut),
            detailHeader.TranslateToAsync(0, 0, 350, Easing.CubicOut)
        );
    }

    private async void OnRejectValidationErrorReceived()
    {
        // Rung lắc viền ô nhập lý do từ chối (iOS Error Shake)
        if (rejectInputBorder != null)
        {
            await rejectInputBorder.ShakeAsync();
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private async void OnActionButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
    }
}
