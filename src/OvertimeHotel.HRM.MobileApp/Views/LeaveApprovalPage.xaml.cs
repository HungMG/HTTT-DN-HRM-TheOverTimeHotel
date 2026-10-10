using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class LeaveApprovalPage : ContentPage
{
    private readonly LeaveApprovalViewModel _viewModel;
    private CancellationTokenSource _skeletonCts = new();
    private CancellationTokenSource _loadMoreCts = new();

    /// <summary>
    /// GSAP "power2.out" — Giảm tốc mượt mà tự nhiên, không giật, tối ưu cho trượt nhẹ và fade in.
    /// </summary>
    private static readonly Easing GsapPower2Out = new(t => 1.0 - Math.Pow(1.0 - t, 2));

    /// <summary>
    /// GSAP "power2.in" — Gia tốc rút lui nhanh và dứt khoát cho hiệu ứng thu gọn (exit/collapse).
    /// </summary>
    private static readonly Easing GsapPower2In = new(t => t * t);

    private static readonly Color HighlightNavyStroke = Color.FromArgb("#1E3A8A");
    private static readonly Color DefaultBorderStroke = Color.FromArgb("#CBD5E1");

    public LeaveApprovalPage(LeaveApprovalViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeaveApprovalViewModel.IsDepartmentDropdownOpen))
        {
            await AnimateDropdownAsync(_viewModel.IsDepartmentDropdownOpen);
            return;
        }

        // Danh sách đơn được nạp lại mỗi lần vào trang và mỗi lần bấm làm mới, nên
        // nhịp thở skeleton phải bám theo cờ IsLoading chứ không chỉ vào OnAppearing.
        if (e.PropertyName == nameof(LeaveApprovalViewModel.IsLoading))
        {
            if (_viewModel.IsLoading) StartSkeletonLoop(); else DisposeSkeleton();
            return;
        }

        // Nhãn "Đang tải thêm đơn…" thay cho vòng tròn xoay ở chân danh sách.
        if (e.PropertyName == nameof(LeaveApprovalViewModel.IsLoadingMore))
        {
            if (_viewModel.IsLoadingMore) StartLoadMoreLoop(); else DisposeLoadMoreLoop();
        }
    }

    /// <summary>
    /// Nhịp thở của khối skeleton danh sách đơn. Quy chuẩn §2 cấm ActivityIndicator —
    /// skeleton cho quản lý thấy trước bố cục trang thay vì nhìn vòng tròn quay.
    /// </summary>
    private void StartSkeletonLoop()
    {
        DisposeSkeleton();
        _skeletonCts = new CancellationTokenSource();
        _ = skeletonLayout.ShimmerLoopAsync(_skeletonCts.Token);
    }

    /// <summary>
    /// Dừng nhịp thở: animation nền vô ích vừa tốn pin vừa giữ CTS sống. Dispose
    /// rồi tạo mới để lần nạp sau dùng lại được.
    /// </summary>
    private void DisposeSkeleton()
    {
        if (_skeletonCts.IsCancellationRequested) return;

        _skeletonCts.Cancel();
        _skeletonCts.Dispose();
        _skeletonCts = new CancellationTokenSource();
    }

    /// <summary>Nhịp thở cho nhãn "Đang tải thêm đơn…" ở chân danh sách.</summary>
    private void StartLoadMoreLoop()
    {
        DisposeLoadMoreLoop();
        _loadMoreCts = new CancellationTokenSource();
        _ = loadMoreLabel.ShimmerLoopAsync(_loadMoreCts.Token, minOpacity: 0.35, maxOpacity: 1.0, cycleDuration: 600);
    }

    /// <summary>Dừng nhịp thở nhãn tải thêm, cùng lý do và cùng cách xử lý như trên.</summary>
    private void DisposeLoadMoreLoop()
    {
        if (_loadMoreCts.IsCancellationRequested) return;

        _loadMoreCts.Cancel();
        _loadMoreCts.Dispose();
        _loadMoreCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Hiệu ứng chuyển động chuẩn GSAP Power2 Smooth Glide:
    /// - Pre-warmed Layout: Card luôn nằm trong bộ nhớ cây giao diện (không bật/tắt IsVisible), loại bỏ hoàn toàn độ trễ Cold Layout (0 dropped frames).
    /// - Compositor-only transforms: Chỉ animate TranslationY (-6dp -> 0) và Opacity (0 -> 1) trực tiếp trên RenderNode GPU.
    /// - Highlight viền Navy (#1E3A8A) trên Trigger Box khi mở.
    /// </summary>
    private async Task AnimateDropdownAsync(bool isOpen)
    {
        // Hủy hoạt ảnh đang dở nếu người dùng chạm liên tục để phản hồi tức thì
        dropdownCard.CancelAnimations();
        chevronBorder.CancelAnimations();

        try
        {
            if (isOpen)
            {
                // 1. Bật viền Navy cho Trigger Box khi mở
                triggerBorder.Stroke = HighlightNavyStroke;

                // 2. Kích hoạt nhận tương tác trên Card và mở Backdrop
                dropdownCard.InputTransparent = false;
                dropdownBackdrop.Opacity = 0.01;
                dropdownBackdrop.IsVisible = true;

                // 3. GSAP Power2 Smooth Glide: 200ms (Không đo đạc lại layout, trượt êm mượt 120 FPS)
                await Task.WhenAll(
                    dropdownCard.FadeToAsync(1.0, 200, GsapPower2Out),
                    dropdownCard.TranslateToAsync(0, 0, 200, GsapPower2Out),
                    chevronBorder.RotateToAsync(180, 200, GsapPower2Out)
                );
            }
            else
            {
                // 1. Trả viền Trigger Box về mặc định
                triggerBorder.Stroke = DefaultBorderStroke;

                // 2. Tắt nhận tương tác ngay khi bắt đầu thu gọn
                dropdownCard.InputTransparent = true;
                dropdownBackdrop.IsVisible = false;

                // 3. Thu gọn nhanh và mượt theo GSAP Power2.in (150ms)
                await Task.WhenAll(
                    dropdownCard.FadeToAsync(0, 150, GsapPower2In),
                    dropdownCard.TranslateToAsync(0, -6, 150, GsapPower2In),
                    chevronBorder.RotateToAsync(0, 150, GsapPower2In)
                );
            }
        }
        catch
        {
            // Dự phòng an toàn nếu hoạt ảnh bị gián đoạn
            dropdownCard.Opacity = isOpen ? 1 : 0;
            dropdownCard.TranslationY = isOpen ? 0 : -6;
            dropdownCard.InputTransparent = !isOpen;
            chevronBorder.Rotation = isOpen ? 180 : 0;
            dropdownBackdrop.IsVisible = isOpen;
            triggerBorder.Stroke = isOpen ? HighlightNavyStroke : DefaultBorderStroke;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Tự động nạp lại danh sách dữ liệu để cập nhật trạng thái khi quay lại từ LeaveDetailPage
        _viewModel.LoadDataCommand.Execute(null);

        // 2. Hoạt ảnh lướt nhẹ nhàng header
        headerBar.Opacity = 0;
        headerBar.TranslationY = -20;

        await Task.Delay(40);
        _ = Task.WhenAll(
            headerBar.FadeToAsync(1.0, 350, Easing.CubicOut),
            headerBar.TranslateToAsync(0, 0, 400, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Dừng cả hai nhịp thở khi rời trang: chúng là animation nền, chạy tiếp vừa tốn
    /// pin vừa giữ CTS sống trong khi trang đã không hiển thị.
    /// </summary>
    protected override void OnDisappearing()
    {
        DisposeSkeleton();
        DisposeLoadMoreLoop();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
