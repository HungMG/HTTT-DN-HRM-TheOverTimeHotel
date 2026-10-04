using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Thanh tiêu đề người dùng dùng chung cho cả hai nhánh sau đăng nhập: nhận diện khách sạn,
/// lời chào, dòng phòng ban/chức vụ và nút mở trang Hồ Sơ.
/// Trang chủ gọi <see cref="Bind"/> để nạp dữ liệu người dùng từ ViewModel nhánh tương ứng.
/// </summary>
public partial class ProfileHeaderBar : ContentView
{
    /// <summary>Sự kiện phát ra khi người dùng bấm nút Hồ Sơ ở góc phải.</summary>
    public event EventHandler? ProfileTapped;

    public ProfileHeaderBar()
    {
        InitializeComponent();
    }

    /// <summary>Nạp lời chào, dòng phòng ban và chữ cái avatar từ ViewModel nhánh.</summary>
    public void Bind(HomeViewModelBase viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        lblGreeting.Text = viewModel.Greeting;
        lblRoleDepartment.Text = viewModel.RoleDepartmentLine;
        lblHeaderAvatar.Text = viewModel.AvatarInitial;
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement element)
        {
            await element.SpringTapAsync();
        }

        ProfileTapped?.Invoke(this, EventArgs.Empty);
    }
}