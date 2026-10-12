using System.ComponentModel;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Trang chủ nhánh NHÂN SỰ (HR Dashboard).
/// Thiết kế bám sát Mục 4 của tài liệu đặc tả design_ui_ux.md.
/// </summary>
public partial class HrHomePage : ContentPage
{
    private readonly HrHomeViewModel _viewModel;

    public HrHomePage(HrHomeViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.FeatureRequested += OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden += OnAttendanceSummaryForbidden;
        _viewModel.ShowNotificationDialog += OnShowNotificationDialog;

        await _viewModel.LoadAsync();
        profileHeaderBar.Bind(_viewModel);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.FeatureRequested -= OnFeatureRequested;
        _viewModel.AttendanceSummaryForbidden -= OnAttendanceSummaryForbidden;
        _viewModel.ShowNotificationDialog -= OnShowNotificationDialog;
    }

    private void OnShowNotificationDialog(object? sender, string message)
    {
        popup.Show(
            title: "Thông Báo Nhân Sự",
            message: message,
            iconText: "i",
            iconTextColor: "#1E3A8A",
            iconBgColor: "#EFF6FF",
            borderColor: "#D4AF37",
            confirmText: "ĐÃ HIỂU",
            showCancel: false,
            onConfirm: null);
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
