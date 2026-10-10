using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn phiếu lương của nhân viên. Mở từ nút "Phiếu Lương" trong trang
/// Công &amp; nghỉ phép.
/// </summary>
/// <remarks>
/// <para>
/// Bấm một phiếu mở panel chi tiết ngay tại chỗ (không push trang con) để
/// người dùng vẫn thấy danh sách và chuyển sang phiếu khác không mất ngữ cảnh.
/// </para>
/// <para>
/// Các dòng thu chi được dựng vào <c>detailItemsHost</c> từ code-behind, cùng
/// lý do với <c>SchedulePage</c>: một <c>CollectionView</c> lồng trong
/// <c>CollectionView</c> khác cuộn độc lập và phá vỡ vùng cuộn chung trên Android.
/// Phiếu lương của một người chỉ vài chục dòng nên dựng tĩnh không tốn kém gì.
/// </para>
/// </remarks>
public partial class PayslipPage : ContentPage
{
    private readonly PayslipViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();

    public PayslipPage(PayslipViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Cố tình KHÔNG gọi SafeAreaInset.Track: Shell đã tự dành sẵn khoảng cho thanh
        // trạng thái, cộng thêm lần nữa sẽ sinh khoảng trắng dài ở đầu trang. Xem ghi
        // chú đo đạc trong SafeAreaInset.
        //
        // Khoảng trống đầu trang này có BA nguyên nhân độc lập, phải sửa cả ba thì
        // mới hết: SafeAreaInset (đã gỡ trước), RowSpacing của Grid gốc và Padding
        // trên 12 dp + Margin trên 4 dp của headerBar (đã gỡ). Xem chú thích ngay
        // trên thẻ Grid trong PayslipPage.xaml.
    }

    /// <summary>
    /// Nhịp thở của khối skeleton. Quy chuẩn §2 cấm ActivityIndicator — skeleton
    /// cho người dùng thấy trước bố cục trang thay vì nhìn vòng tròn quay trơ trọi.
    /// </summary>
    private void StartSkeletonLoop()
    {
        DisposeSkeleton();
        _skeletonCts = new CancellationTokenSource();
        _ = skeletonLayout.ShimmerLoopAsync(_skeletonCts.Token);
    }

    /// <summary>
    /// Dừng nhịp thở: animation nền vô ích vừa tốn pin vừa giữ CTS sống. Dispose
    /// rồi tạo mới để lần vào trang sau dùng lại được.
    /// </summary>
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        StartSkeletonLoop();
        headerBar.Opacity = 0;
        headerBar.TranslationY = -12;

        // finally để lỗi lúc nạp vẫn gỡ nhịp thở, không bỏ trạng thái treo.
        try
        {
            await Task.WhenAll(_viewModel.LoadAsync(), headerBar.FadeToAsync(1, 250),
                headerBar.TranslateToAsync(0, 0, 300, Easing.CubicOut));
        }
        finally
        {
            DisposeSkeleton();
        }

        RebuildDetailItems();
    }

    protected override void OnDisappearing()
    {
        DisposeSkeleton();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CancelLoad();
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // DetailItems bị thay cả danh sách nên phải dựng lại, không chỉ reset
        // một thuộc tính. Mọi thay đổi khác không cần vẽ lại.
        if (e.PropertyName != nameof(PayslipViewModel.DetailItems)) return;
        MainThread.BeginInvokeOnMainThread(RebuildDetailItems);
    }

    private void RebuildDetailItems()
    {
        detailItemsHost.Clear();
        for (var i = 0; i < _viewModel.DetailItems.Count; i++)
        {
            if (i > 0) detailItemsHost.Add(BuildSeparator());
            detailItemsHost.Add(BuildItemRow(_viewModel.DetailItems[i]));
        }
    }

    private static BoxView BuildSeparator() => new()
    {
        HeightRequest = 1,
        Color = Color.FromArgb("#E2E8F0")
    };

    private static Grid BuildItemRow(PayslipItemRowItem item)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 7)
        };
        row.Add(new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label { Text = item.Name, FontSize = 13, TextColor = Color.FromArgb("#0F172A") },
                new Label
                {
                    Text = item.Note,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#94A3B8"),
                    IsVisible = item.HasNote
                }
            }
        });
        row.Add(new Label
        {
            Text = item.AmountText,
            FontFamily = "SegoeUIBold",
            FontSize = 14,
            TextColor = Color.FromArgb(item.AmountColor),
            VerticalOptions = LayoutOptions.Center
        }, 1);
        return row;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }
}