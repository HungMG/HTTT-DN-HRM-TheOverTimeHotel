using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Cổng nộp đơn điện tử của nhân viên (TSK-18): chọn loại đơn, điền khoảng ngày và
/// lý do, gửi điện tử, rồi xem lịch sử và trạng thái duyệt của chính mình.
/// </summary>
public partial class LeaveSubmitPage : ContentPage
{
    private readonly LeaveSubmitViewModel _viewModel;

    public LeaveSubmitPage(LeaveSubmitViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;

        _viewModel.PopupRequested += OnPopupRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Nhãn nút lịch sử có số đếm đơn, đổi theo lúc danh sách được nạp lại.
        // Gán tay ở đây thay vì binding vì XAML SourceGen không cho Content
        // lồng nhau trong Button — nhãn còn có icon 📋 nằm ngoài Button.
        btnOpenHistory.Text = _viewModel.HistoryButtonText;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await RunEntranceAnimationAsync();

        // Nạp lại mỗi lần vào trang: người dùng có thể vừa gửi đơn rồi quay ra,
        // hoặc vừa được HR duyệt xong — số liệu cũ hiện ra sẽ sai ngay.
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PopupRequested -= OnPopupRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Các khối khai báo Opacity="0" sẵn trong XAML nên chỉ trượt vào, không chạy
    /// lại hiệu ứng mờ chớp 1 khung hình.
    /// </summary>
    private async Task RunEntranceAnimationAsync() =>
        await new VisualElement[]
        {
            headerBar, typeCard, formCard, historyCard, footerLabel
        }.StaggerCascadeAsync(SlideDirection.Up, delayBetweenMs: 70, duration: 420);

    /// <summary>
    /// Lớp phủ lịch sử do binding bật/tắt, nên hiệu ứng bung sheet phải canh theo
    /// PropertyChanged thay vì gắn thẳng vào lệnh: cả nút trên trang lẫn nút ✕/
    /// bấm ra ngoài/vùng tối đều đi qua cùng một cờ.
    /// </summary>
    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeaveSubmitViewModel.HistoryButtonText))
        {
            btnOpenHistory.Text = _viewModel.HistoryButtonText;
            return;
        }

        if (e.PropertyName != nameof(LeaveSubmitViewModel.IsHistoryOpen))
        {
            return;
        }

        if (_viewModel.IsHistoryOpen)
        {
            historySheet.TranslationY = 420;
            await historySheet.SlideFadeInAsync(SlideDirection.Up, distance: 420, duration: 320);
        }
        else
        {
            // Trượt xuống rồi mới tắt hẳn — tắt thẳng sẽ làm sheet biến mất
            // đột ngột, mất cảm giác "đóng một tấm ngăn".
            await historySheet.TranslateToAsync(0, 420, 200, Easing.CubicIn);
        }
    }

    /// <summary>
    /// Lỗi hệ thống mở popup; lỗi người dùng sửa được thì ViewModel đã hiện tại
    /// chỗ dưới ô nhập nên không popup lần nữa.
    /// </summary>
    private void OnPopupRequested(string icon, string message, bool isError)
    {
        popup.Show(
            title: isError ? "Không Gửi Được Đơn" : "Gửi Đơn Thành Công",
            message: message,
            iconText: icon,
            iconTextColor: isError ? "#B91C1C" : "#047857",
            iconBgColor: isError ? "#FEE2E2" : "#ECFDF5",
            borderColor: isError ? "#F87171" : "#34D399",
            confirmText: "ĐÃ BIẾT",
            showCancel: false,
            onConfirm: null);
    }
}