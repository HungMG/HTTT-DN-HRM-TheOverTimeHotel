using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Views.Controls;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh QUẢN LÝ &amp; QUẢN TRỊ.
/// Bố cục ưu tiên duyệt đơn và theo dõi phạm vi quản lý — không có bản đồ GPS vì
/// thẻ chấm công thuộc về nhân viên, không phải người duyệt đơn.
/// </summary>
public partial class ManagerHomePage : ContentPage
{
    /// <summary>Các khối sẽ lướt vào theo nhịp so le khi trang vừa hiện.</summary>
    private readonly VisualElement[] _entranceBlocks;

    /// <summary>Các thẻ KPI nạp bất đồng bộ nên phải tự cập nhật theo ViewModel.</summary>
    private readonly StatTile _pendingLeaveTile;

    /// <summary>Thẻ đếm đơn đã xử lý trong phạm vi quyền hạn.</summary>
    private readonly StatTile _reviewedLeaveTile;

    private readonly ManagerHomeViewModel _viewModel;

    public ManagerHomePage(ManagerHomeViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        SafeAreaInset.Track(this, pageBody, new Thickness(20, 0, 20, 32));

        BindingContext = _viewModel;

        _pendingLeaveTile = pendingLeaveTile;
        _reviewedLeaveTile = reviewedLeaveTile;
        _entranceBlocks = new VisualElement[]
        {
            profileHeaderBar,
            managerScopeCard,
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
        _viewModel.FeatureRequested += OnFeatureRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        await _viewModel.LoadAsync();
        ApplyViewModel(_viewModel);

        await _entranceBlocks.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 80, duration: 420);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        profileHeaderBar.ProfileTapped -= OnProfileTapped;
        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>Đẩy dữ liệu của ViewModel vào các thẻ trên màn hình.</summary>
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
    /// Số đơn chờ duyệt nạp bất đồng bộ nên thẻ KPI phải tự bám theo ViewModel,
    /// không phụ thuộc vào thứ tự LoadAsync hoàn tất.
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
                // PropertyChanged có thể phát từ thread của timer CSDL — ép về UI thread.
                MainThread.BeginInvokeOnMainThread(() => ApplyViewModel(_viewModel));
                break;
        }
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//MainTabs/Profile");
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
}