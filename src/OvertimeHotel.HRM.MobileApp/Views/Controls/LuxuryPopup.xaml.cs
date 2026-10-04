using OvertimeHotel.HRM.MobileApp.Animations;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Custom Luxury Modal Popup dùng chung cho toàn bộ Mobile App, thay thế DisplayAlert thô
/// theo quy chuẩn thiết kế §3: lớp phủ nền mờ, thẻ bo góc lớn, icon tròn nổi bật và
/// nút bấm viền vàng kim. Trang chủ đặt nó phủ toàn màn hình (Fill) nhưng mặc định ẩn.
/// </summary>
public partial class LuxuryPopup : ContentView
{
    private Action? _confirmAction;

    public LuxuryPopup()
    {
        InitializeComponent();

        // Mặc định ẩn hoàn toàn; trang chủ bật lên bằng Show().
        popupOverlay.IsVisible = false;
    }

    /// <summary>
    /// Hiện popup với nội dung truyền vào.
    /// </summary>
    /// <param name="title">Tiêu đề in đậm 18px.</param>
    /// <param name="message">Nội dung diễn giải ngắn gọn, tự xuống dòng.</param>
    /// <param name="iconText">Ký tự icon: ★ cho thành công, ! cho lỗi.</param>
    /// <param name="iconTextColor">Màu chữ icon.</param>
    /// <param name="iconBgColor">Màu nền ô icon.</param>
    /// <param name="borderColor">Màu viền thẻ và ô icon.</param>
    /// <param name="confirmText">Nhãn nút xác nhận.</param>
    /// <param name="showCancel">Hiện thêm nút Hủy hay không.</param>
    /// <param name="onConfirm">Hành động chạy khi người dùng bấm Xác nhận.</param>
    public void Show(
        string title,
        string message,
        string iconText = "★",
        string iconTextColor = "#B45309",
        string iconBgColor = "#FFFBEB",
        string borderColor = "#D4AF37",
        string confirmText = "ĐỒNG Ý",
        bool showCancel = false,
        Action? onConfirm = null)
    {
        lblModalTitle.Text = title;
        lblModalMessage.Text = message;
        lblModalIcon.Text = iconText;
        lblModalIcon.TextColor = Color.FromArgb(iconTextColor);
        modalIconBox.BackgroundColor = Color.FromArgb(iconBgColor);
        modalIconBox.Stroke = Color.FromArgb(borderColor);
        popupCard.Stroke = Color.FromArgb(borderColor);
        btnModalConfirm.Text = confirmText;

        btnModalCancel.IsVisible = showCancel;
        Grid.SetColumnSpan(btnModalConfirm, showCancel ? 1 : 2);

        _confirmAction = onConfirm;

        popupOverlay.IsVisible = true;
        popupOverlay.Opacity = 0;
        _ = popupOverlay.FadeToAsync(1, 180, Easing.CubicOut);
        _ = popupCard.BounceInAsync(360);
    }

    /// <summary>Ẩn popup ngay lập tức, dùng khi trang bị đóng trước khi người dùng bấm nút.</summary>
    public void Hide()
    {
        popupOverlay.IsVisible = false;
        _confirmAction = null;
    }

    private async void OnModalCancelClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        Hide();
    }

    private async void OnModalConfirmClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        Hide();

        _confirmAction?.Invoke();
    }
}