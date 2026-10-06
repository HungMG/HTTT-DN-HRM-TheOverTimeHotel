using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Thanh tiêu đề người dùng dùng chung cho cả ba nhánh sau đăng nhập: nhận diện khách sạn,
/// lời chào và dòng phòng ban/chức vụ.
/// Trang chủ gọi <see cref="Bind"/> để nạp dữ liệu người dùng từ ViewModel nhánh tương ứng.
/// </summary>
public partial class ProfileHeaderBar : ContentView
{
    public ProfileHeaderBar()
    {
        InitializeComponent();
    }

    /// <summary>Nạp lời chào và dòng phòng ban/chức vụ từ ViewModel nhánh.</summary>
    public void Bind(HomeViewModelBase viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        lblGreeting.Text = viewModel.Greeting;
        lblRoleDepartment.Text = viewModel.RoleDepartmentLine;
    }
}