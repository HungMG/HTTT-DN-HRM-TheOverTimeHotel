using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn phiếu lương của nhân viên: hiển thị danh sách phiếu và chi tiết 2 tầng Gross - Net.
/// </summary>
public partial class PayslipPage : ContentPage
{
    private readonly PayslipViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();

    public PayslipPage(PayslipViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Nhịp thở của khối skeleton khi tải danh sách phiếu.
    /// </summary>
    private void StartSkeletonLoop()
    {
        DisposeSkeleton();
        _skeletonCts = new CancellationTokenSource();
        _ = skeletonLayout.ShimmerLoopAsync(_skeletonCts.Token);
    }

    private void DisposeSkeleton()
    {
        if (_skeletonCts.IsCancellationRequested) return;

        _skeletonCts.Cancel();
        _skeletonCts.Dispose();
        _skeletonCts = new CancellationTokenSource();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartSkeletonLoop();
        headerBar.Opacity = 0;
        headerBar.TranslationY = -12;

        try
        {
            await Task.WhenAll(_viewModel.LoadAsync(), headerBar.FadeToAsync(1, 250),
                headerBar.TranslateToAsync(0, 0, 300, Easing.CubicOut));
        }
        finally
        {
            DisposeSkeleton();
        }
    }

    protected override void OnDisappearing()
    {
        DisposeSkeleton();
        _viewModel.CancelLoad();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();

        if (_viewModel.HasDetail)
        {
            _viewModel.CloseDetailCommand.Execute(null);
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsYearPickerOpen)
        {
            _viewModel.CloseYearPickerCommand.Execute(null);
            return true;
        }

        if (_viewModel.HasDetail)
        {
            _viewModel.CloseDetailCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
