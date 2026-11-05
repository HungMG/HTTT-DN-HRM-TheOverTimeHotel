using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class AttendanceSummaryPage : ContentPage, IQueryAttributable
{
    private readonly AttendanceSummaryViewModel _viewModel;
    private int _appearingVersion;
    private CancellationTokenSource? _skeletonCts;

    public AttendanceSummaryPage(AttendanceSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Nhịp thở của khối skeleton cho một lượt xuất hiện cụ thể.
    /// Trả về CancellationTokenSource để gắn chặt vòng đời theo từng lượt.
    /// </summary>
    private CancellationTokenSource StartSkeletonLoop()
    {
        StopSkeleton();
        var cts = new CancellationTokenSource();
        _skeletonCts = cts;
        _ = skeletonLayout.ShimmerLoopAsync(cts.Token);
        return cts;
    }

    /// <summary>
    /// Dừng nhịp thở khi rời trang hoặc bắt đầu lượt mới.
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

    /// <summary>
    /// Dừng nhịp thở gắn với đúng lượt xuất hiện tương ứng.
    /// </summary>
    private void StopSkeletonForAppearance(int version, CancellationTokenSource cts)
    {
        if (!cts.IsCancellationRequested)
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }
        cts.Dispose();

        if (version == _appearingVersion && ReferenceEquals(_skeletonCts, cts))
        {
            _skeletonCts = null;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.Configure(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var version = ++_appearingVersion;
        var cts = StartSkeletonLoop();
        headerBar.Opacity = 0;
        headerBar.TranslationY = -12;
        // Dừng nhịp thở ngay khi nạp xong chứ không chờ rời trang: khối skeleton đã
        // ẩn đi (IsLoading=false) nên nhịp thở chạy tiếp chỉ là tốn pin vô ích.
        // finally để lỗi lúc nạp vẫn gỡ nhịp thở, không bỏ trạng thái treo.
        try
        {
            await Task.WhenAll(_viewModel.LoadAsync(), headerBar.FadeToAsync(1, 250),
                headerBar.TranslateToAsync(0, 0, 300, Easing.CubicOut));
        }
        finally
        {
            StopSkeletonForAppearance(version, cts);
        }

        if (_viewModel.HasError && _viewModel.IsAdminSelfAccess)
        {
            popup.Show(
                title: "Thông Báo",
                message: _viewModel.ErrorMessage,
                iconText: "!",
                iconTextColor: "#B91C1C",
                iconBgColor: "#FEF2F2",
                borderColor: "#1E3A8A",
                confirmText: "ĐÓNG",
                showCancel: false,
                onConfirm: null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ++_appearingVersion;
        StopSkeleton();
        _viewModel.CancelLoad();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Mở cổng nộp đơn. Sau khi gỡ khối chức năng khỏi trang chủ nhánh nhân viên
    /// (2026-10-10), đây là đường vào DUY NHẤT của LeaveSubmitPage.
    /// </summary>
    private async void OnLeaveSubmitTapped(object? sender, EventArgs e)
    {
        if (sender is VisualElement card) await card.SpringTapAsync();
        await Shell.Current.GoToAsync("LeaveSubmitPage");
    }
}
