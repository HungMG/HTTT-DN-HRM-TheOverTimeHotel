using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class RequestsPage : ContentPage
{
    private readonly RequestsViewModel _viewModel;
    private CancellationTokenSource? _skeletonCts;

    public RequestsPage(RequestsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RequestsViewModel.IsLoading))
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

    /// <summary>
    /// Nhịp thở của khối skeleton shimmer (Quy chuẩn UI §2: cấm ActivityIndicator).
    /// Gắn trực tiếp với trạng thái IsLoading của ViewModel.
    /// </summary>
    private void StartSkeletonLoop()
    {
        StopSkeleton();
        var cts = new CancellationTokenSource();
        _skeletonCts = cts;
        _ = skeletonLayout.ShimmerLoopAsync(cts.Token);
    }

    /// <summary>
    /// Dừng nhịp thở khi nạp xong hoặc khi rời trang để tiết kiệm tài nguyên và pin.
    /// </summary>
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
