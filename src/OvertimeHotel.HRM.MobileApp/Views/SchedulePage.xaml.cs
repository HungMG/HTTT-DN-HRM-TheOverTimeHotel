using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn lịch phân ca 24/7 của nhân viên. Mở từ nút trên header tab Chấm Công.
/// </summary>
/// <remarks>
/// <para>
/// Vài ngày trong một tuần là dữ liệu nhỏ, nên thẻ ngày được dựng thẳng vào
/// <c>daysHost</c> thay vì đặt <c>CollectionView</c> trong XAML: một
/// <c>CollectionView</c> lồng trong <c>CollectionView</c> khác là lỗi đã
/// gặp trên Android — danh sách con cuộn độc lập và phá vỡ vùng cuộn chung.
/// </para>
/// </remarks>
public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();

    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Cố tình KHÔNG gọi SafeAreaInset.Track: Shell đã tự dành khoảng cho thanh
        // trạng thái, cộng thêm lần nữa sẽ sinh khoảng trắng dài ở đầu trang — đo
        // thực tế trên AVD API 35 thì trang có Track bắt đầu ở ~69 dp so với ~37.5 dp
        // khi không có. Xem ghi chú đo đạc trong SafeAreaInset.
        //
        // Khoảng trống đầu trang này có BA nguyên nhân độc lập, phải sửa cả ba thì
        // mới hết: SafeAreaInset (đã gỡ trước), RowSpacing của Grid gốc và Padding
        // trên 12 dp + Margin trên 4 dp của headerBar (đã gỡ). Xem chú thích ngay
        // trên thẻ Grid trong SchedulePage.xaml.
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

        RebuildDayCards();
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
        // Days bị thay cả danh sách nên phải dựng lại thẻ, không chỉ reset
        // một thuộc tính. Mọi thay đổi khác không cần vẽ lại.
        if (e.PropertyName != nameof(ScheduleViewModel.Days)) return;
        MainThread.BeginInvokeOnMainThread(RebuildDayCards);
    }

    private void RebuildDayCards()
    {
        daysHost.Clear();
        foreach (var day in _viewModel.Days) daysHost.Add(BuildDayCard(day));
    }

    private static Border BuildDayCard(ScheduleDayItem day)
    {
        var card = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var body = new VerticalStackLayout { Spacing = 10 };

        var heading = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        heading.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = day.DateText, FontFamily = "SegoeUIBold", FontSize = 18, TextColor = Color.FromArgb("#0F172A") },
                new Label { Text = day.Weekday, FontSize = 11 }
            }
        });
        if (day.IsToday)
        {
            heading.Add(new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb("#1E3A8A"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(10, 6),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = "HÔM NAY", FontFamily = "SegoeUIBold", TextColor = Colors.White, FontSize = 10 }
            }, 1);
        }
        body.Add(heading);
        body.Add(new Label { Text = day.SummaryText, FontFamily = "SegoeUIBold", TextColor = Color.FromArgb("#334155") });

        foreach (var shift in day.Shifts) body.Add(BuildShiftRow(shift));
        card.Content = body;
        return card;
    }

    private static Grid BuildShiftRow(ScheduleShiftItem shift)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 6)
        };
        row.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = shift.Name, FontFamily = "SegoeUIBold", FontSize = 14, TextColor = Color.FromArgb("#0F172A") },
                new Label { Text = shift.TimeText, TextColor = Color.FromArgb("#64748B") }
            }
        });

        var badges = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        badges.Add(new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(shift.StatusBackground),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(8, 4),
            Content = new Label { Text = shift.StatusText, FontFamily = "SegoeUIBold", TextColor = Color.FromArgb(shift.StatusColor), FontSize = 10 }
        });
        if (shift.IsNightShift)
            badges.Add(new Label
            {
                Text = "★ Ca đêm +30%",
                TextColor = Color.FromArgb("#B45309"),
                FontFamily = "SegoeUIBold",
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.End
            });
        row.Add(badges, 1);
        return row;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }
}