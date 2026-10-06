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
            locationClockPanel,
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        await _viewModel.LoadAsync();
        profileHeaderBar.Bind(_viewModel);
        BindShiftTimeline(_viewModel);

        await RunEntranceAnimationAsync();
        await StartEmployeeBranchAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

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

    /// <summary>
    /// Đẩy ca hôm nay của chính mình vào dải hối lịch trực. Chỉ hiện ca của bản
    /// thân — lịch của đồng nghiệp cùng ca do quản lý phân, nhân viên không có
    /// quyền đọc (RLS <c>doc_phan_ca</c> giới hạn theo <c>ma_nhan_vien</c>).
    /// </summary>
    private void BindShiftTimeline(EmployeeHomeViewModel viewModel)
    {
        // ShiftTimeText có dạng "HH:mm – HH:mm"; tách ra hai đầu dải thời gian.
        var parts = viewModel.ShiftTimeText.Split('–', StringSplitOptions.TrimEntries);

        lblTimelineStart.Text = parts.Length > 0 ? parts[0] : "--:--";
        lblTimelineEnd.Text = parts.Length > 1 ? parts[1] : "--:--";
        lblTimelineShiftName.Text = viewModel.ShiftTaskText;

        lblNightBadge.Text = string.IsNullOrEmpty(viewModel.NightBadgeText)
            ? "Ca Làm Việc"
            : viewModel.NightBadgeText;
        nightBadgeBorder.IsVisible = viewModel.HasShiftToday;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EmployeeHomeViewModel.ShiftTimeText):
            case nameof(EmployeeHomeViewModel.ShiftTaskText):
            case nameof(EmployeeHomeViewModel.NightBadgeText):
            case nameof(EmployeeHomeViewModel.HasShiftToday):
                MainThread.BeginInvokeOnMainThread(() => BindShiftTimeline(_viewModel));
                break;
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