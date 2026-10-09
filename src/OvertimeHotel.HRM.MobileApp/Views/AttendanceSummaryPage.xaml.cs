using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class AttendanceSummaryPage : ContentPage, IQueryAttributable
{
    private readonly AttendanceSummaryViewModel _viewModel;

    public AttendanceSummaryPage(AttendanceSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SafeAreaInset.Track(this, summaryGrid, new Thickness(16, 12, 16, 0));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.Configure(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        headerBar.Opacity = 0;
        headerBar.TranslationY = -12;
        await Task.WhenAll(_viewModel.LoadAsync(), headerBar.FadeToAsync(1, 250),
            headerBar.TranslateToAsync(0, 0, 300, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        _viewModel.CancelLoad();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }
}
