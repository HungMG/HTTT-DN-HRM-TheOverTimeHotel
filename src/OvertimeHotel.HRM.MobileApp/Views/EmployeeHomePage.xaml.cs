using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh NHÂN VIÊN.
/// Bố cục ưu tiên thao tác trong ca: nút quẹt thẻ nằm ngay dưới thanh tiêu đề,
/// kèm đồng hồ thật và bản đồ GPS làm bằng chứng vị trí khi quẹt thẻ.
/// </summary>
public partial class EmployeeHomePage : ContentPage
{
    private readonly EmployeeHomeViewModel _viewModel;
    private readonly EmployeeDashboardViewModel _employeeDashboard;

    /// <summary>Các khối sẽ lướt vào theo nhịp so le khi trang vừa hiện.</summary>
    private readonly VisualElement[] _entranceBlocks;

    public EmployeeHomePage(EmployeeHomeViewModel viewModel, EmployeeDashboardViewModel employeeDashboard)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _employeeDashboard = employeeDashboard ?? throw new ArgumentNullException(nameof(employeeDashboard));

        SafeAreaInset.Track(this, pageBody, new Thickness(20, 0, 20, 32));

        BindingContext = _viewModel;
        locationClockPanel.Bind(_employeeDashboard);

        _entranceBlocks = new VisualElement[]
        {
            profileHeaderBar,
            shiftHeroCard,
            locationClockPanel,
            statsGrid,
            featuresHeader,
            featuresGrid,
            footerLabel
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        profileHeaderBar.ProfileTapped += OnProfileTapped;
        shiftHeroCard.CheckInRequested += OnCheckInRequested;
        _viewModel.FeatureRequested += OnFeatureRequested;

        await _viewModel.LoadAsync();
        profileHeaderBar.Bind(_viewModel);
        shiftHeroCard.Bind(_viewModel);

        await RunEntranceAnimationAsync();
        await StartEmployeeBranchAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        profileHeaderBar.ProfileTapped -= OnProfileTapped;
        shiftHeroCard.CheckInRequested -= OnCheckInRequested;
        _viewModel.FeatureRequested -= OnFeatureRequested;

        // Dừng đồng hồ và GPS để không tiêu hao pin khi người dùng rời trang chủ.
        _employeeDashboard.Stop();
    }

    /// <summary>
    /// Khối bố cục đã khai báo Opacity="0" ngay trong XAML nên chỉ cần trượt vào,
    /// không chạy lại hiệu ứng mờ chớp 1 khung hình.
    /// </summary>
    private async Task RunEntranceAnimationAsync() =>
        await _entranceBlocks.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 80, duration: 420);

    /// <summary>Xin quyền vị trí rồi mới chạy đồng hồ và nạp toạ độ GPS lần đầu.</summary>
    private async Task StartEmployeeBranchAsync()
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
            // Thiết bị không có GPS: bản đồ hiển thị thông báo chờ, quẹt thẻ vẫn chạy bình thường.
        }
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//MainTabs/Profile");
        }
    }

    private async void OnCheckInRequested(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//MainTabs/Attendance");
        }
    }

    private void OnFeatureRequested(object? sender, string featureName) => ShowComingSoonPopup(featureName);

    /// <summary>
    /// Hiện Custom Luxury Modal Popup cho tính năng chưa nối API.
    /// Tuyệt đối không dùng DisplayAlert thô theo quy chuẩn thiết kế §3.
    /// </summary>
    private void ShowComingSoonPopup(string featureName) =>
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
}