using Microsoft.Maui.Animations;

namespace OvertimeHotel.HRM.MobileApp.Animations;

/// <summary>
/// Hướng di chuyển cho các hiệu ứng trượt xuất hiện (Slide Entrance).
/// </summary>
public enum SlideDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Bộ công cụ chuyển động chuyên nghiệp (Motion Engine) cho .NET MAUI.
/// Chuyển thể từ các thư viện hoạt ảnh đỉnh cao của Flutter &amp; GSAP (Spring Physics, Shimmer, Shake, Stagger Cascade).
/// Hỗ trợ gọi 1 dòng ngắn gọn qua Extension Methods trên mọi VisualElement.
/// </summary>
public static class MobileMotion
{
    /// <summary>
    /// Hiệu ứng đàn hồi vật lý khi chạm/click nút bấm (Flutter Spring Tap).
    /// Co nhẹ phần tử lại và nảy tưng trở lại tạo cảm giác phản hồi xúc giác chân thực.
    /// </summary>
    public static async Task SpringTapAsync(this VisualElement element, double scale = 0.94, uint pressDuration = 60, uint releaseDuration = 140)
    {
        if (element == null) return;

        // Bấm lún xuống (Squish)
        await element.ScaleToAsync(scale, pressDuration, Easing.CubicOut);
        // Nảy lò xo bung ra (Spring back)
        await element.ScaleToAsync(1.0, releaseDuration, Easing.SpringOut);
    }

    /// <summary>
    /// Hiệu ứng rung lắc báo lỗi (iOS Error Shake).
    /// Lắc qua lại nhanh khi người dùng nhập sai mật khẩu hoặc thao tác không hợp lệ.
    /// </summary>
    public static async Task ShakeAsync(this VisualElement element, double offset = 10, int cycles = 3, uint duration = 50)
    {
        if (element == null) return;

        for (int i = 0; i < cycles; i++)
        {
            await element.TranslateToAsync(-offset, 0, duration, Easing.SinInOut);
            await element.TranslateToAsync(offset, 0, duration, Easing.SinInOut);
        }
        await element.TranslateToAsync(0, 0, duration, Easing.SinInOut);
    }

    /// <summary>
    /// Hiệu ứng bung lò xo xuất hiện từ tâm (Modal Spring Pop).
    /// Phóng to từ kích thước nhỏ (0.75) lên chuẩn (1.0) kèm độ nảy lò xo mượt mà.
    /// </summary>
    public static async Task BounceInAsync(this VisualElement element, uint duration = 360)
    {
        if (element == null) return;

        element.Scale = 0.75;
        element.Opacity = 0;

        _ = element.FadeToAsync(1.0, duration / 2, Easing.CubicOut);
        await element.ScaleToAsync(1.0, duration, Easing.SpringOut);
    }

    /// <summary>
    /// Hiệu ứng trượt vào êm ái kết hợp làm mờ (Smooth Slide &amp; Fade In).
    /// </summary>
    public static async Task SlideFadeInAsync(this VisualElement element, SlideDirection direction = SlideDirection.Up, double distance = 30, uint duration = 420)
    {
        if (element == null) return;

        element.Opacity = 0;

        double startX = 0;
        double startY = 0;

        switch (direction)
        {
            case SlideDirection.Up: startY = distance; break;
            case SlideDirection.Down: startY = -distance; break;
            case SlideDirection.Left: startX = distance; break;
            case SlideDirection.Right: startX = -distance; break;
        }

        element.TranslationX = startX;
        element.TranslationY = startY;

        _ = element.FadeToAsync(1.0, duration - 50, Easing.CubicOut);
        await element.TranslateToAsync(0, 0, duration, Easing.CubicOut);
    }

    /// <summary>
    /// Hiệu ứng nạp so le lần lượt cho một danh sách các phần tử (Staggered Cascade Entrance).
    /// </summary>
    public static async Task StaggerCascadeAsync(this IEnumerable<VisualElement> elements, SlideDirection direction = SlideDirection.Up, int delayBetweenMs = 70, uint duration = 400)
    {
        if (elements == null) return;

        foreach (var el in elements)
        {
            _ = el.SlideFadeInAsync(direction, 25, duration);
            await Task.Delay(delayBetweenMs);
        }
    }

    /// <summary>
    /// Hiệu ứng lướt sáng nhịp thở cho Skeleton Loading (Breathing Shimmer).
    /// Chạy chu kỳ lặp lại cho đến khi có tín hiệu hủy (CancellationToken).
    /// </summary>
    public static async Task ShimmerLoopAsync(this VisualElement element, CancellationToken token, double minOpacity = 0.45, double maxOpacity = 0.95, uint cycleDuration = 450)
    {
        if (element == null) return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await element.FadeToAsync(minOpacity, cycleDuration, Easing.SinInOut);
                if (token.IsCancellationRequested) break;
                await element.FadeToAsync(maxOpacity, cycleDuration, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException)
        {
            // Kết thúc hiệu ứng mượt mà khi dữ liệu nạp xong
        }
        finally
        {
            element.Opacity = 1.0;
        }
    }

    /// <summary>
    /// Hiệu ứng nhịp tim đập nhẹ cho các huy hiệu trạng thái, ca đêm, live indicator (Heartbeat Pulse).
    /// </summary>
    public static async Task PulseBadgeAsync(this VisualElement element, double maxScale = 1.08, uint duration = 600)
    {
        if (element == null) return;

        await element.ScaleToAsync(maxScale, duration / 2, Easing.SinInOut);
        await element.ScaleToAsync(1.0, duration / 2, Easing.SinInOut);
    }

    /// <summary>
    /// Hiệu ứng nâng nổi 3D nhẹ khi thẻ Card được chọn (Card 3D Elevation).
    /// </summary>
    public static async Task ElevateCardAsync(this VisualElement element, bool isElevated)
    {
        if (element == null) return;

        if (isElevated)
        {
            _ = element.ScaleToAsync(1.02, 180, Easing.CubicOut);
            await element.TranslateToAsync(0, -4, 180, Easing.CubicOut);
        }
        else
        {
            _ = element.ScaleToAsync(1.0, 180, Easing.CubicOut);
            await element.TranslateToAsync(0, 0, 180, Easing.CubicOut);
        }
    }

    /// <summary>
    /// Hiệu ứng chuyển biến văn bản mượt mà (Text Slide-Fade Morphing).
    /// Chữ cũ trượt nhẹ lên và mờ dần, sau đó chữ mới trượt từ dưới lên vào vị trí chuẩn.
    /// </summary>
    public static async Task MorphTextAsync(this Label label, string newText, uint fadeOutMs = 90, uint fadeInMs = 150)
    {
        if (label == null) return;
        if (label.Text == newText) return;

        // 1. Chữ cũ mờ dần và trượt nhẹ lên trên
        await Task.WhenAll(
            label.FadeToAsync(0, fadeOutMs, Easing.CubicIn),
            label.TranslateToAsync(0, -6, fadeOutMs, Easing.CubicIn)
        );

        // 2. Gán nội dung mới và đặt vị trí từ dưới lên
        label.Text = newText;
        label.TranslationY = 6;

        // 3. Chữ mới mờ hiện và trượt êm ái vào vị trí chuẩn
        await Task.WhenAll(
            label.FadeToAsync(1.0, fadeInMs, Easing.CubicOut),
            label.TranslateToAsync(0, 0, fadeInMs, Easing.CubicOut)
        );
    }
}
