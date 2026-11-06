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
        RebuildCalendar();
    }

    protected override void OnDisappearing()
    {
        DisposeSkeleton();
        detailOverlay.IsVisible = false;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CancelLoad();
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Days bị thay cả danh sách nên phải dựng lại thẻ, không chỉ reset
        // một thuộc tính. Mọi thay đổi khác không cần vẽ lại.
        if (e.PropertyName == nameof(ScheduleViewModel.Days))
            MainThread.BeginInvokeOnMainThread(() => { RebuildDayCards(); RebuildCalendar(); });
    }

    private void RebuildCalendar()
    {
        calendarGrid.Children.Clear();
        calendarGrid.RowDefinitions.Clear();
        var start = _viewModel.CalendarStart;
        var count = _viewModel.CalendarEnd.DayNumber - start.DayNumber + 1;
        for (var row = 0; row < count / 7; row++) calendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < count; i++)
        {
            var date = start.AddDays(i);
            var selected = date == _viewModel.SelectedDate;
            var hasShift = _viewModel.HasShift(date);
            var button = new Button
            {
                Text = date.Day.ToString(),
                FontSize = 14, Padding = 0, HeightRequest = 42, MinimumHeightRequest = 42,
                CornerRadius = 21,
                BackgroundColor = selected ? Color.FromArgb(_viewModel.HasEvent(date) ? "#DC2626" : "#1E3A8A") : Colors.Transparent,
                TextColor = selected ? Colors.White : date.Month == _viewModel.Month.Month ? Color.FromArgb("#334155") : Color.FromArgb("#CBD5E1")
            };
            SemanticProperties.SetDescription(button, $"Ngày {date:dd/MM/yyyy}" + (hasShift ? ", có ca làm" : "") + (_viewModel.HasEvent(date) ? ", có sự kiện" : ""));
            button.Clicked += async (_, _) => await _viewModel.SelectDateAsync(date);
            var cell = new Grid { WidthRequest = 42, HeightRequest = 42, HorizontalOptions = LayoutOptions.Center };
            cell.Add(button);
            if (hasShift)
                cell.Add(new Ellipse
                {
                    WidthRequest = 3, HeightRequest = 3, StrokeThickness = 0,
                    Fill = selected ? Colors.White : Color.FromArgb("#1E3A8A"),
                    HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, date.Day < 10 ? 11 : 7, 0), InputTransparent = true
                });
            calendarGrid.Add(cell, i % 7, i / 7);
        }
    }

    private void RebuildDayCards()
    {
        daysHost.Clear();
        foreach (var day in _viewModel.Days) daysHost.Add(BuildDayCard(day));
    }

    private Border BuildDayCard(ScheduleDayItem day)
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
                new Label { Text = $"{day.DateText}  {day.Weekday}", FontFamily = "SegoeUIBold", FontSize = 18, TextColor = Color.FromArgb("#0F172A") }
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

        foreach (var shift in day.Shifts)
        {
            body.Add(BuildShiftRow(shift));
            foreach (var eventItem in shift.Events)
            {
                body.Add(new VerticalStackLayout
                {
                    Spacing = 3,
                    Padding = new Thickness(12, 2, 0, 6),
                    Children =
                    {
                        new Label { Text = $"Sự kiện - {eventItem.Name}", FontFamily = "SegoeUIBold", FontSize = 14, TextColor = Color.FromArgb("#1E3A8A") },
                        new Label { Text = eventItem.TimeText, TextColor = Color.FromArgb("#64748B") }
                    }
                });
            }
        }
        body.Add(new Label { Text = "Nhấn để xem chi tiết ›", FontSize = 12, TextColor = Color.FromArgb("#1E3A8A") });
        card.Content = body;
        SemanticProperties.SetDescription(card, $"Xem chi tiết lịch ngày {day.DateText}, {day.Weekday}");
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => ShowDetails(day);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private void ShowDetails(ScheduleDayItem day)
    {
        detailTitle.Text = $"Chi tiết · {day.DateText} {day.Weekday}";
        detailContent.Clear();
        foreach (var shift in day.Shifts)
        {
            detailContent.Add(new Border
            {
                Stroke = Color.FromArgb("#DBEAFE"), BackgroundColor = Color.FromArgb("#EFF6FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 14 }, Padding = new Thickness(14),
                Content = new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label { Text = "CA LÀM CHÍNH", FontFamily = "SegoeUIBold", FontSize = 10, TextColor = Color.FromArgb("#1E3A8A") },
                        new Label { Text = shift.Name, FontFamily = "SegoeUIBold", FontSize = 15, TextColor = Color.FromArgb("#0F172A") },
                        new Label { Text = $"{shift.TimeText}  ·  {shift.StatusText}", FontSize = 13, TextColor = Color.FromArgb("#334155") },
                        new Label { Text = $"Vào: {shift.CheckInText}   •   Ra: {shift.CheckOutText}", FontSize = 12, TextColor = Color.FromArgb("#64748B") }
                    }
                }
            });
            foreach (var eventItem in shift.Events)
            {
                detailContent.Add(new Border
                {
                    Stroke = Color.FromArgb("#FECACA"), BackgroundColor = Color.FromArgb("#FEF2F2"),
                    StrokeShape = new RoundRectangle { CornerRadius = 14 }, Padding = new Thickness(14),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children =
                        {
                            new Label { Text = "SỰ KIỆN", FontFamily = "SegoeUIBold", FontSize = 10, TextColor = Color.FromArgb("#B91C1C") },
                            new Label { Text = eventItem.Name, FontFamily = "SegoeUIBold", FontSize = 14, TextColor = Color.FromArgb("#0F172A") },
                            new Label { Text = eventItem.TimeText, FontSize = 13, TextColor = Color.FromArgb("#B91C1C") }
                        }
                    }
                });
            }
        }
        detailOverlay.IsVisible = true;
    }

    private void OnCloseDetailClicked(object? sender, EventArgs e) => detailOverlay.IsVisible = false;

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