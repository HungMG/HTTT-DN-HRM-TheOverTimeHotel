using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class AdminHomePage : ContentPage
{
    private readonly AdminHomeViewModel _viewModel;

    public AdminHomePage(AdminHomeViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.ShowNotificationDialog += OnShowNotificationDialog;

        await _viewModel.LoadAsync();
        profileHeaderBar.Bind(_viewModel);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ShowNotificationDialog -= OnShowNotificationDialog;
    }

    private void OnShowNotificationDialog(object? sender, string message)
    {
        popup.Show(
            title: "Thông Báo Hệ Thống",
            message: message,
            iconText: "i",
            iconTextColor: "#1E3A8A",
            iconBgColor: "#EFF6FF",
            borderColor: "#D4AF37",
            confirmText: "ĐÃ HIỂU",
            showCancel: false,
            onConfirm: null);
    }
}
