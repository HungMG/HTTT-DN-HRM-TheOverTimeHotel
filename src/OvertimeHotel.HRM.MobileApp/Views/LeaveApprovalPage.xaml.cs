using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class LeaveApprovalPage : ContentPage
{
    private readonly LeaveApprovalViewModel _viewModel;

    public LeaveApprovalPage(LeaveApprovalViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Tự động nạp lại danh sách dữ liệu để cập nhật trạng thái khi quay lại từ LeaveDetailPage
        _viewModel.LoadDataCommand.Execute(null);

        // 2. Hoạt ảnh lướt nhẹ nhàng header
        headerBar.Opacity = 0;
        headerBar.TranslationY = -20;

        await Task.Delay(40);
        _ = Task.WhenAll(
            headerBar.FadeToAsync(1.0, 350, Easing.CubicOut),
            headerBar.TranslateToAsync(0, 0, 400, Easing.CubicOut)
        );
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
