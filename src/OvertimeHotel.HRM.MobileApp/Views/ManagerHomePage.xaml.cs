using System.ComponentModel;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Views.Controls;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh QUẢN LÝ &amp; QUẢN TRỊ.
/// Bố cục kết hợp theo dõi hiện diện thực tế (đồng hồ &amp; GPS khách sạn) cùng trung
/// tâm điều hành: thẻ thẩm quyền quản lý, thông báo duyệt đơn khẩn, dải ca trực điều
/// hành hôm nay, các chỉ số KPI bộ phận và lưới thao tác quản trị nhanh.
/// </summary>
public partial class ManagerHomePage : ContentPage
{
    private readonly VisualElement[] _entranceBlocks;
    private readonly StatTile _pendingLeaveTile;
    private readonly StatTile _reviewedLeaveTile;
    private readonly ManagerHomeViewModel _viewModel;
    private readonly EmployeeDashboardViewModel _employeeDashboard;

    public ManagerHomePage(ManagerHomeViewModel viewModel, EmployeeDashboardViewModel employeeDashboard)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _employeeDashboard = employeeDashboard ?? throw new ArgumentNullException(nameof(employeeDashboard));

        BindingContext = _viewModel;
        locationClockPanel.Bind(_employeeDashboard);

        _pendingLeaveTile = pendingLeaveTile;
        _reviewedLeaveTile = reviewedLeaveTile;

        _entranceBlocks = new VisualElement[]
        {
            profileHeaderBar,
            locationClockPanel,
            managerScopeCard,
            shiftTimelineCard,
            statsGrid,
            featuresHeader,
            featuresGrid,
            footerLabel
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.FeatureRequested += OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden += OnAttendanceSummaryForbidden;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        await _viewModel.LoadAsync();
        ApplyViewModel(_viewModel);
        BindShiftTimeline(_viewModel);

        await RunEntranceAnimationAsync();
        await StartManagerPresenceAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden -= OnAttendanceSummaryForbidden;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Dừng timer đồng hồ và GPS khi rời tab quản lý để không hao pin
        _employeeDashboard.Stop();
    }

    /// <summary>
    /// Hiệu ứng các khối lướt vào theo nhịp so le mượt mà 60 FPS (SlideFadeIn).
    /// </summary>
    private async Task RunEntranceAnimationAsync() =>
        await _entranceBlocks.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 70, duration: 400);

    /// <summary>
    /// Bật đồng hồ số thật và nạp vị trí GPS giám sát tại khách sạn.
    /// </summary>
    private async Task StartManagerPresenceAsync()
    {
        await RequestLocationPermissionAsync();
        await _employeeDashboard.StartAsync();
    }

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
            // Thiết bị không có GPS: bản đồ hiển thị chờ, giao diện điều hành vẫn hoạt động
        }
    }

    /// <summary>Đẩy dữ liệu của ViewModel vào các thẻ điều hành trên màn hình.</summary>
    private void ApplyViewModel(ManagerHomeViewModel viewModel)
    {
        profileHeaderBar.Bind(viewModel);
        lblCommandRole.Text = viewModel.CommandRole;
        lblCommandScope.Text = viewModel.CommandScope;
        lblPermissionBadge.Text = viewModel.PermissionLabel;

        _pendingLeaveTile.Value = viewModel.PendingLeaveCount.ToString();
        _pendingLeaveTile.SubCaption = viewModel.PendingLeaveSub;

        _reviewedLeaveTile.Value = viewModel.ReviewedLeaveCount.ToString();
    }

    /// <summary>
    /// Gắn thông tin ca trực điều hành hôm nay của quản lý vào dải thời gian.
    /// </summary>
    private void BindShiftTimeline(ManagerHomeViewModel viewModel)
    {
        var parts = viewModel.ShiftTimeText.Split('–', StringSplitOptions.TrimEntries);

        lblTimelineStart.Text = parts.Length > 0 ? parts[0] : "--:--";
        lblTimelineEnd.Text = parts.Length > 1 ? parts[1] : "--:--";
        lblTimelineShiftName.Text = viewModel.ShiftTaskText;

        lblNightBadge.Text = string.IsNullOrEmpty(viewModel.NightBadgeText)
            ? "Trưởng Bộ Phận"
            : viewModel.NightBadgeText;
        nightBadgeBorder.IsVisible = viewModel.HasShiftToday;
    }

    /// <summary>
    /// Lắng nghe thay đổi dữ liệu bất đồng bộ từ CSDL để cập nhật giao diện trên UI thread.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ManagerHomeViewModel.CommandRole):
            case nameof(ManagerHomeViewModel.CommandScope):
            case nameof(ManagerHomeViewModel.PermissionLabel):
            case nameof(ManagerHomeViewModel.PendingLeaveCount):
            case nameof(ManagerHomeViewModel.PendingLeaveSub):
            case nameof(ManagerHomeViewModel.ReviewedLeaveCount):
                MainThread.BeginInvokeOnMainThread(() => ApplyViewModel(_viewModel));
                break;

            case nameof(ManagerHomeViewModel.ShiftTimeText):
            case nameof(ManagerHomeViewModel.ShiftTaskText):
            case nameof(ManagerHomeViewModel.NightBadgeText):
            case nameof(ManagerHomeViewModel.HasShiftToday):
                MainThread.BeginInvokeOnMainThread(() => BindShiftTimeline(_viewModel));
                break;
        }
    }

    private void OnFeatureRequested(object? sender, string featureName) =>
        popup.Show(
            title: "Chức Năng Đang Nâng Cấp",
            message: $"Tính năng [{featureName}] đang được kết nối với API Supabase ở các task tiếp theo.",
            iconText: "★",
            iconTextColor: "#B45309",
            iconBgColor: "#FFFBEB",
            borderColor: "#D4AF37",
            confirmText: "ĐỒNG Ý",
            showCancel: false,
            onConfirm: null);

    private void OnAttendanceSummaryForbidden(object? sender, EventArgs e) =>
        popup.Show(
            title: "Thông Báo",
            message: new AttendanceSummaryUnavailableException().Message,
            iconText: "!",
            iconTextColor: "#B91C1C",
            iconBgColor: "#FEF2F2",
            borderColor: "#1E3A8A",
            confirmText: "ĐÓNG",
            showCancel: false,
            onConfirm: null);
}
