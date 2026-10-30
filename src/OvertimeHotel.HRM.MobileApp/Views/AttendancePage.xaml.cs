using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn hình quẹt thẻ chấm công (Check-in / Check-out) và xem lịch sử chấm công trong tháng.
/// </summary>
public partial class AttendancePage : ContentPage
{
    private readonly AttendanceViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();

    public AttendancePage(AttendanceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        BindingContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Nạp dữ liệu ca hôm nay & lịch sử tháng (ViewModel tự bật/tắt skeleton).
        await _viewModel.LoadDataAsync();

        StartSkeletonLoop();

        // 1. Đặt vị trí ban đầu (Opacity đã khai báo 0 trong XAML nên không bị chớp giật).
        pageHeader.TranslationY = -22;
        shiftCard.TranslationY = 30;
        noShiftCard.TranslationY = 30;
        todayCard.TranslationY = 30;
        historySection.TranslationY = 30;
        historyCard.TranslationY = 30;

        await Task.Delay(60);

        // 2. Gửi từng thẻ vào vị trí theo nhịp so le bằng CubicOut (không vỡ khung hình).
        _ = Task.WhenAll(
            pageHeader.FadeToAsync(1.0, 380, Easing.CubicOut),
            pageHeader.TranslateToAsync(0, 0, 420, Easing.CubicOut)
        );

        await Task.Delay(90);

        var stateCards = new List<VisualElement> { shiftCard, noShiftCard };
        var visibleCards = stateCards.Where(c => c.IsVisible).ToList();
        await visibleCards.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 70, duration: 380);

        await Task.Delay(90);

        _ = Task.WhenAll(
            todayCard.FadeToAsync(1.0, 400, Easing.CubicOut),
            todayCard.TranslateToAsync(0, 0, 440, Easing.CubicOut),
            historySection.FadeToAsync(1.0, 400, Easing.CubicOut),
            historySection.TranslateToAsync(0, 0, 440, Easing.CubicOut),
            historyCard.FadeToAsync(1.0, 400, Easing.CubicOut),
            historyCard.TranslateToAsync(0, 0, 440, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Dừng Animation khi rời trang để không tiêu tốn pin và khung hình vô ích.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DisposeSkeleton();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AttendanceViewModel.IsPopupVisible):
                if (_viewModel.IsPopupVisible)
                {
                    popupOverlay.InputTransparent = false;
                    _ = popupOverlay.FadeToAsync(1, 180, Easing.CubicOut);
                    _ = popupCard.BounceInAsync(360);
                }
                else
                {
                    popupOverlay.InputTransparent = true;
                    _ = popupOverlay.FadeToAsync(0, 160, Easing.CubicOut);
                }
                break;

            case nameof(AttendanceViewModel.IsDetailPopupVisible):
                if (_viewModel.IsDetailPopupVisible)
                {
                    detailPopupOverlay.InputTransparent = false;
                    _ = detailPopupOverlay.FadeToAsync(1, 180, Easing.CubicOut);
                    _ = detailPopupCard.BounceInAsync(360);
                }
                else
                {
                    detailPopupOverlay.InputTransparent = true;
                    _ = detailPopupOverlay.FadeToAsync(0, 160, Easing.CubicOut);
                }
                break;

            case nameof(AttendanceViewModel.IsLoading):
                if (_viewModel.IsLoading)
                {
                    StartSkeletonLoop();
                }
                else
                {
                    DisposeSkeleton();
                }
                break;

            case nameof(AttendanceViewModel.ActionButtonText):
                _ = lblPunchHint.MorphTextAsync(_viewModel.DaCheckOut
                    ? "Bạn đã hoàn thành ca làm việc hôm nay"
                    : _viewModel.DaCheckIn
                        ? "Chạm để kết thúc ca và ghi giờ làm thêm"
                        : "Chạm để quẹt thẻ vào ca");
                break;

            case nameof(AttendanceViewModel.HasNoShiftToday):
                // Ca hôm nay vừa đổi trạng thái (nạp lại): đưa thẻ tương ứng vào vị trí.
                _ = AnimateStateCardsAsync();
                break;
        }
    }

    /// <summary>
    /// Hiệu ứng nâng nhẹ thẻ ca làm khi trạng thái hôm nay thay đổi sau khi quẹt thẻ.
    /// </summary>
    private async Task AnimateStateCardsAsync()
    {
        var target = _viewModel.HasShiftToday ? shiftCard : noShiftCard;
        await target.ElevateCardAsync(true);
        await target.ElevateCardAsync(false);
    }

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

    // ==================== SỰ KIỆN GIAO DIỆN ====================

    private async void OnPunchClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();

        if (!_viewModel.CanPunchCard)
        {
            return;
        }

        // Xin quyền vị trí ngay tại thao tác quẹt thẻ (không xin lúc mở app để không gây phiền).
        await RequestLocationPermissionAsync();

        // Đã quẹt đủ cả vào và ra thì nút bị vô hiệu hoá, không cần gọi.
        if (_viewModel.DaCheckOut)
        {
            return;
        }

        if (_viewModel.DaCheckIn)
        {
            _viewModel.CheckOutCommand.Execute(null);
            return;
        }

        _viewModel.CheckInCommand.Execute(null);
    }

    private async void OnPreviousMonthClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        _viewModel.PreviousMonthCommand.Execute(null);
    }

    private async void OnNextMonthClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        _viewModel.NextMonthCommand.Execute(null);
    }

    private async void OnPopupConfirmClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        _viewModel.ClosePopupCommand.Execute(null);
    }

    private async void OnCloseDetailPopupClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        _viewModel.CloseDetailPopupCommand.Execute(null);
    }

    /// <summary>
    /// Xin quyền truy cập vị trí khi người dùng chủ động quẹt thẻ.
    /// Nhân viên vẫn quẹt thẻ được nếu từ chối — chỉ mất toạ độ đối chiếu.
    /// </summary>
    private static async Task RequestLocationPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
        }
        catch (FeatureNotSupportedException)
        {
            // Thiết bị không hỗ trợ định vị: bỏ qua, vẫn quẹt thẻ bình thường.
        }
    }

    /// <summary>Mở màn lịch phân ca 24/7 (nút dưới header tab Chấm Công).</summary>
    private async void OnScheduleTapped(object? sender, EventArgs e)
    {
        if (sender is VisualElement card) await card.SpringTapAsync();
        await Shell.Current.GoToAsync("SchedulePage");
    }

    /// <summary>Mở màn bảng công tháng (nút bên cạnh Lịch ca và Phiếu lương).</summary>
    private async void OnAttendanceSummaryTapped(object? sender, EventArgs e)
    {
        if (sender is VisualElement card) await card.SpringTapAsync();
        await Shell.Current.GoToAsync("AttendanceSummaryPage");
    }

    /// <summary>Mở màn phiếu lương (nút dưới nút Lịch Ca 24/7).</summary>
    private async void OnPayslipTapped(object? sender, EventArgs e)
    {
        if (sender is VisualElement card) await card.SpringTapAsync();
        await Shell.Current.GoToAsync("PayslipPage");
    }
}