using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Thẻ hero ca làm việc: khối navy nổi bật nhất màn hình nhân viên, chứa khung giờ ca,
/// nhiệm vụ và nút quẹt thẻ check-in màu vàng kim — thao tác quan trọng nhất trong ca.
/// </summary>
public partial class ShiftHeroCard : ContentView
{
    /// <summary>Sự kiện phát ra khi người dùng bấm nút quẹt thẻ check-in.</summary>
    public event EventHandler? CheckInRequested;

    public ShiftHeroCard()
    {
        InitializeComponent();
    }

    /// <summary>Nạp khung giờ ca, nhiệm vụ và huy hiệu phụ cấp từ ViewModel nhánh.</summary>
    public void Bind(EmployeeHomeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        lblShiftTime.Text = viewModel.ShiftTimeText;
        lblShiftTask.Text = viewModel.ShiftTaskText;
        lblNightBadge.Text = viewModel.NightBadgeText;

        // Huy hiệu rỗng khi ca là ban ngày — ẩn hẳn khối chứ không để lọt chuỗi
        // rỗng ra viền vàng, trông như lỗi hiển thị.
        nightBadgeBorder.IsVisible = !string.IsNullOrEmpty(viewModel.NightBadgeText);

        // Không có ca hôm nay thì nút không dẫn đâu được: tắt thay vì bấm rồi
        // đưa nhân viên sang trang chấm công chỉ để đọc "không có ca".
        btnCheckIn.Text = viewModel.CheckInButtonText;
        btnCheckIn.IsEnabled = viewModel.HasShiftToday;
    }

    private async void OnCheckInClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            await element.SpringTapAsync();
        }

        CheckInRequested?.Invoke(this, EventArgs.Empty);
    }
}