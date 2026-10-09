using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class LeaveApprovalPage : ContentPage
{
    private readonly LeaveApprovalViewModel _viewModel;

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
        }
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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
