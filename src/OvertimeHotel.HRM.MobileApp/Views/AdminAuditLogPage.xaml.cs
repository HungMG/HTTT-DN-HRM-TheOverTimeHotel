using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class AdminAuditLogPage : ContentPage
{
    private readonly AdminAuditLogViewModel _viewModel;

    public AdminAuditLogPage(AdminAuditLogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadLogsAsync();
    }
}
