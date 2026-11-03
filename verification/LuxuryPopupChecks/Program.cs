using OvertimeHotel.HRM.MobileApp.Views.Controls;

var popup = new LuxuryPopup();
int calls = 0;
popup.Show("Confirm", "Question", onConfirm: () => calls++);
popup.ConfirmForTest();
Check(calls == 1 && !popup.IsVisible, "Confirm must hide popup and invoke callback once");
popup.ConfirmForTest();
Check(calls == 1, "Repeated confirmation must not repeat callback");
popup.Show("Cancel", "Question", onConfirm: () => calls++);
popup.Hide();
popup.ConfirmForTest();
Check(calls == 1, "Dismissed popup must not invoke callback");
popup.Show("First", "Question", onConfirm: () => popup.Show("Next", "Success"));
popup.ConfirmForTest();
Check(popup.IsVisible, "Callback may show a follow-up popup");
Console.WriteLine("PASS: actual popup confirmation callback, exactly once, dismissal and follow-up dialog");
static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

// Only XAML-generated fields, MAUI visuals and animations are stubbed.
public class ContentView { public bool IsVisible { get; set; } public bool InputTransparent { get; set; } }
public class VisualElement { }
public class TestElement : VisualElement
{
    public bool IsVisible { get; set; }
    public double Opacity { get; set; }
    public string Text { get; set; } = "";
    public Color? TextColor { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? Stroke { get; set; }
    public Task FadeToAsync(double opacity, int duration, object easing) => Task.CompletedTask;
    public Task BounceInAsync(int duration) => Task.CompletedTask;
}
public class Color { public static Color FromArgb(string value) => new(); }
public static class Grid { public static void SetColumnSpan(TestElement target, int span) { } }
public static class Easing { public static object CubicOut { get; } = new(); }
namespace OvertimeHotel.HRM.MobileApp.Animations
{
    public static class Animations { public static Task SpringTapAsync(this VisualElement target) => Task.CompletedTask; }
}
namespace OvertimeHotel.HRM.MobileApp.Views.Controls
{
    public partial class LuxuryPopup
    {
        private readonly TestElement popupOverlay = new(), popupCard = new(), lblModalTitle = new(), lblModalMessage = new(), lblModalIcon = new(), modalIconBox = new(), btnModalConfirm = new(), btnModalCancel = new();
        private void InitializeComponent() { }
        public void ConfirmForTest() => OnModalConfirmClicked(null, EventArgs.Empty);
    }
}
