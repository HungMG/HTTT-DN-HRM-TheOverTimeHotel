using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class AttendanceSummaryPage : ContentPage, IQueryAttributable
{
    private readonly AttendanceSummaryViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();

    /// <summary>
    /// Đã dựng theo kiểu trang gốc của tab hay chưa — dùng để chỉ cấu hình lần đầu.
    /// </summary>
    private bool? _isTabRoot;

    public AttendanceSummaryPage(AttendanceSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Cùng một lớp trang phục vụ hai kiến trúc Shell khác nhau nên phần đầu trang
    /// phải bám theo vai trò đang dùng:
    ///
    /// · Nhân viên vào thẳng từ tab "Giấy Tờ" — không có trang trước để quay lại
    ///   nên ẩn nút ←.
    ///
    /// · Quản lý / nhân sự push tới từ ô chức năng ở trang chủ — cần nút ←.
    ///
    /// Khoảng đệm trên đầu thì cả hai nhánh đều giống nhau: Shell đã tự chừa sẵn
    /// chỗ cho thanh trạng thái ở cả trang gốc lẫn trang push, nên không cộng thêm
    /// (xem <see cref="SafeAreaInset"/>).
    ///
    /// Một phiên bản của lớp không đổi vai trò giữa các lần hiện, nên chỉ cấu
    /// hình một lần rồi bỏ qua các lần hiện sau.
    /// </summary>
    private void SyncShellContext()
    {
        bool isTabRoot = Navigation.NavigationStack.Count <= 1;

        if (_isTabRoot == isTabRoot) return;

        _isTabRoot = isTabRoot;
        backButton.IsVisible = !isTabRoot;
    }

    /// <summary>
    /// Nhịp thở của khối skeleton. Không dùng vòng tròn xoay ActivityIndicator vì
    /// quy chuẩn §2 cấm — skeleton cho người dùng thấy trước bố cục trang.
    /// </summary>
    private void StartSkeletonLoop()
    {
        DisposeSkeleton();
        _skeletonCts = new CancellationTokenSource();
        _ = skeletonLayout.ShimmerLoopAsync(_skeletonCts.Token);
    }

    /// <summary>
    /// Dừng nhịp thở khi rời trang: animation nền vô ích vừa tốn pin vừa giữ CTS sống
    /// (Dispose rồi tạo mới) để lần vào trang sau dùng lại được.
    /// </summary>
    private void DisposeSkeleton()
    {
        if (_skeletonCts.IsCancellationRequested) return;

        _skeletonCts.Cancel();
        _skeletonCts.Dispose();
        _skeletonCts = new CancellationTokenSource();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.Configure(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SyncShellContext();
        StartSkeletonLoop();
        headerBar.Opacity = 0;
        headerBar.TranslationY = -12;

        // Dừng nhịp thở ngay khi nạp xong chứ không chờ rời trang: khối skeleton đã
        // ẩn đi (IsLoading=false) nên nhịp thở chạy tiếp chỉ là tốn pin vô ích.
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
    }

    protected override void OnDisappearing()
    {
        DisposeSkeleton();
        _viewModel.CancelLoad();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement button) await button.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Mở cổng nộp đơn. Sau khi gỡ khối chức năng khỏi trang chủ nhánh nhân viên
    /// (2026-10-10), đây là đường vào DUY NHẤT của LeaveSubmitPage.
    /// </summary>
    private async void OnLeaveSubmitTapped(object? sender, EventArgs e)
    {
        if (sender is VisualElement card) await card.SpringTapAsync();
        await Shell.Current.GoToAsync("LeaveSubmitPage");
    }
}
