using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Views.Controls;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh QUẢN LÝ BỘ PHẬN (Executive Manager Dashboard).
/// Bố cục chuẩn Hospitality 5 sao: Bảng điều hành bộ phận Navy &amp; Gold,
/// cảnh báo duyệt đơn khẩn cấp, giám sát quân số ca trực hôm nay (ai đang làm việc,
/// ai đi trễ, ai nghỉ phép), và phím tắt nghiệp vụ điều hành nhanh.
/// </summary>
public partial class ManagerHomePage : ContentPage
{
    private readonly VisualElement[] _entranceBlocks;
    private readonly ManagerHomeViewModel _viewModel;
    private readonly EmployeeDashboardViewModel _employeeDashboard;

    public ManagerHomePage(ManagerHomeViewModel viewModel, EmployeeDashboardViewModel employeeDashboard)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _employeeDashboard = employeeDashboard ?? throw new ArgumentNullException(nameof(employeeDashboard));

        BindingContext = _viewModel;
        locationClockPanel.Bind(_employeeDashboard);

        _entranceBlocks = new VisualElement[]
        {
            profileHeaderBar,
            managerScopeCard,
            locationClockPanel,
            shiftTimelineCard,
            urgentApprovalCard,
            allReviewedCard,
            statsGrid,
            staffRosterCard,
            footerLabel
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.FeatureRequested += OnFeatureRequested;

        _viewModel.AttendanceSummaryForbidden -= OnAttendanceSummaryForbidden;
        _viewModel.AttendanceSummaryForbidden += OnAttendanceSummaryForbidden;

        _viewModel.ShowNotificationDialog -= OnShowNotificationDialog;
        _viewModel.ShowNotificationDialog += OnShowNotificationDialog;

        await _viewModel.LoadAsync();
        profileHeaderBar.Bind(_viewModel);

        await RunEntranceAnimationAsync();
        await StartManagerPresenceAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden -= OnAttendanceSummaryForbidden;
        _viewModel.ShowNotificationDialog -= OnShowNotificationDialog;

        // Dừng timer đồng hồ và GPS khi rời tab quản lý để không hao pin
        _employeeDashboard.Stop();
    }

    /// <summary>
    /// Hiệu ứng các khối lướt vào theo nhịp so le mượt mà 60 FPS (SlideFadeIn).
    /// </summary>
    private async Task RunEntranceAnimationAsync() =>
        await _entranceBlocks.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 60, duration: 380);

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

    private void OnShowNotificationDialog(object? sender, string message) =>
        popup?.Show(
            title: "Thông Báo Quản Lý",
            message: message,
            iconText: "i",
            iconTextColor: "#1E3A8A",
            iconBgColor: "#EFF6FF",
            borderColor: "#D4AF37",
            confirmText: "ĐÃ HIỂU",
            showCancel: false,
            onConfirm: null);

    private void OnFeatureRequested(object? sender, string featureName) =>
        popup?.Show(
            title: "Chức Năng Điều Hành",
            message: $"Tính năng [{featureName}] đang được đồng bộ và cập nhật với API hệ thống.",
            iconText: "★",
            iconTextColor: "#B45309",
            iconBgColor: "#FFFBEB",
            borderColor: "#D4AF37",
            confirmText: "ĐỒNG Ý",
            showCancel: false,
            onConfirm: null);

    private void OnAttendanceSummaryForbidden(object? sender, EventArgs e) =>
        popup?.Show(
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
