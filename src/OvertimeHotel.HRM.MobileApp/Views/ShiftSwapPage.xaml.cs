using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class ShiftSwapPage : ContentPage
{
    private readonly ShiftSwapViewModel _viewModel;
    private CancellationTokenSource? _skeletonCts;

    public ShiftSwapPage(ShiftSwapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShiftSwapViewModel.IsLoading))
        {
            if (_viewModel.IsLoading)
            {
                StartSkeletonLoop();
            }
            else
            {
                StopSkeleton();
            }
        }
    }

    private void StartSkeletonLoop()
    {
        StopSkeleton();
        var cts = new CancellationTokenSource();
        _skeletonCts = cts;
        _ = SkeletonList.ShimmerLoopAsync(cts.Token);
    }

    private void StopSkeleton()
    {
        if (_skeletonCts != null)
        {
            if (!_skeletonCts.IsCancellationRequested)
            {
                try { _skeletonCts.Cancel(); } catch (ObjectDisposedException) { }
            }
            _skeletonCts.Dispose();
            _skeletonCts = null;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.IsLoading)
        {
            StartSkeletonLoop();
        }
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopSkeleton();
        _viewModel.CancelLoad();
    }
}
