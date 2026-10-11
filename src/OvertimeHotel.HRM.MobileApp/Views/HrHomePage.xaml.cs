using System.ComponentModel;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Views.Controls;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh NHÂN SỰ.
/// Bố cục ưu tiên duyệt đơn (cả 5 loại, toàn khách sạn) và các nhóm quyền riêng
/// của nhân sự: hợp đồng lao động, tính lương. Không có bản đồ GPS vì thẻ chấm công
/// thuộc về nhân viên, không phải người duyệt.
/// </summary>
public partial class HrHomePage : ContentPage
{
    /// <summary>Các khối sẽ lướt vào theo nhịp so le khi trang vừa hiện.</summary>
    private readonly VisualElement[] _entranceBlocks;

    /// <summary>Các thẻ KPI nạp bất đồng bộ nên phải tự cập nhật theo ViewModel.</summary>
    private readonly StatTile _pendingLeaveTile;

    /// <summary>Thẻ đếm đơn đã xử lý trong phạm vi quyền hạn.</summary>
    private readonly StatTile _reviewedLeaveTile;

    private readonly HrHomeViewModel _viewModel;

    public HrHomePage(HrHomeViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        BindingContext = _viewModel;

        _pendingLeaveTile = pendingLeaveTile;
        _reviewedLeaveTile = reviewedLeaveTile;
        _entranceBlocks = new VisualElement[]
        {
            profileHeaderBar,
            hrScopeCard,
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

        await _entranceBlocks.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 80, duration: 420);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden -= OnAttendanceSummaryForbidden;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>Đẩy dữ liệu của ViewModel vào các thẻ trên màn hình.</summary>
    private void ApplyViewModel(HrHomeViewModel viewModel)
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
    /// Số đơn nạp bất đồng bộ nên thẻ KPI phải tự bám theo ViewModel, không phụ
    /// thuộc vào thứ tự LoadAsync hoàn tất.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HrHomeViewModel.CommandRole):
            case nameof(HrHomeViewModel.CommandScope):
            case nameof(HrHomeViewModel.PermissionLabel):
            case nameof(HrHomeViewModel.PendingLeaveCount):
            case nameof(HrHomeViewModel.PendingLeaveSub):
            case nameof(HrHomeViewModel.ReviewedLeaveCount):
                // PropertyChanged có thể phát từ thread của timer CSDL — ép về UI thread.
                MainThread.BeginInvokeOnMainThread(() => ApplyViewModel(_viewModel));
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

    /// <summary>
    /// Bấm ô "Công &amp; nghỉ phép" mà vai trò không có hồ sơ lao động.
    /// Cùng bộ thông báo với trang bảng công và cùng kiểu lỗi, nên lấy thẳng từ
    /// exception thay vì chép lại câu chữ.
    /// </summary>
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
