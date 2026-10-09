using OvertimeHotel.HRM.MobileApp.Services;
using OvertimeHotel.HRM.MobileApp.Views;

namespace OvertimeHotel.HRM.MobileApp;

/// <summary>
/// Vỏ ứng dụng. Ba màn hình ngoài phiên (Intro, Login) đứng riêng; phần sau
/// đăng nhập là một thanh tab dựng động theo vai trò của phiên đang đăng nhập.
/// </summary>
public partial class AppShell : Shell
{
	/// <summary>Route tới trang chủ của nhánh đang đăng nhập.</summary>
	public const string HomeRoute = "//MainTabs/Home";

	// Icon tab là PNG sinh từ scripts/generate_mobile_brand_icons.py — Shell nhận
	// Icon kiểu ImageSource nên không dùng được ký tự emoji. Trạng thái tab được
	// chọn thì Shell tự tô, không cần bản icon thứ hai.
	private const string TAB_ICON_EMPLOYEE = "tab_home.png";
	private const string TAB_ICON_MANAGER = "tab_manager.png";
	private const string TAB_ICON_HR = "tab_hr.png";
	private const string TAB_ICON_SETTINGS = "tab_settings.png";
	private const string TAB_ICON_ATTENDANCE = "tab_attendance.png";

	// Bảng màu thanh tab theo nhận diện khách sạn 5★ (rules/mobile-design-rules.md):
	// nền trắng, tab đang chọn vàng kim, tab chưa chọn xám xanh, tab khoá xám nhạt.
	private const string TAB_BAR_BACKGROUND = "#FFFFFF";
	private const string TAB_BAR_SELECTED = "#D4AF37";
	private const string TAB_BAR_UNSELECTED = "#94A3B8";
	private const string TAB_BAR_DISABLED = "#CBD5E1";

	// Nhãn tab dùng navy đậm thay vì vàng kim: chữ nhỏ 12px trên nền trắng mà vàng kim
	// sẽ mờ, còn navy giữ đủ tương phản mà vẫn đúng nhận diện (mobile-design-rules.md §1).
	private const string TAB_BAR_TITLE = "#1E3A8A";

	private readonly IAuthService _authService;

	/// <summary>Số tab đã dựng — dùng để biết cây tab còn sạch hay không.</summary>
	private bool _tabsBuilt;

	public AppShell(IAuthService authService)
	{
		_authService = authService ?? throw new ArgumentNullException(nameof(authService));

		InitializeComponent();

		Routing.RegisterRoute("LeaveApprovalPage", typeof(LeaveApprovalPage));
		Routing.RegisterRoute("LeaveDetailPage", typeof(LeaveDetailPage));

		// Cổng nộp đơn phía nhân viên (TSK-18). Push từ ô "Đơn Điện Tử" trên
		// trang chủ nhánh Nhân Viên, nên cũng phải đăng ký route toàn cục.
		Routing.RegisterRoute("LeaveSubmitPage", typeof(LeaveSubmitPage));
		Routing.RegisterRoute("AttendanceSummaryPage", typeof(AttendanceSummaryPage));

		// Hồ Sơ và Đổi Mật Khẩu không còn là tab riêng mà là hai trang push mở từ
		// tab Cài Đặt, nên phải đăng ký route toàn cục thì GoToAsync mới tới được.
		Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
		Routing.RegisterRoute("ChangePasswordPage", typeof(ChangePasswordPage));
	}

	/// <summary>
	/// Dựng thanh tab theo đúng vai trò rồi nhảy tới trang chủ.
	/// Nhân viên KHÔNG thấy tab Quản lý/Nhân sự: tab bị bỏ hẳn khỏi cây Shell
	/// nên cũng không gõ tay route để mở được — trang đó chứa nút duyệt đơn thật.
	/// </summary>
	public async Task OpenHomeAsync()
	{
		var user = _authService.CurrentUser;

		BuildRoleTabs(user?.IsEmployee == true, user?.IsHR == true);
		await GoToAsync(HomeRoute);
	}

	/// <summary>Gỡ toàn bộ tab của nhánh đã đăng nhập (dùng khi thoát tài khoản).</summary>
	public void ClearRoleTabs()
	{
		foreach (TabBar bar in Items.OfType<TabBar>().ToList())
		{
			Items.Remove(bar);
		}

		_tabsBuilt = false;
	}

	private void BuildRoleTabs(bool isEmployee, bool isHr)
	{
		if (_tabsBuilt)
		{
			// Đã đăng nhập rồi mới bấm nút quay lại trang chủ: cây tab giữ nguyên
			// để không mất vị trí chọn và không nhảy layout giữa chừng.
			return;
		}

		ClearRoleTabs();

		// Ba nhánh trang chủ, mỗi nhánh một bộ chức năng riêng: nhân viên thao tác
		// trong ca, quản lý duyệt đơn trong bộ phận, nhân sự xử lý hồ sơ và lương
		// toàn khách sạn. Gộp HR vào nhánh quản lý sẽ khiến thẻ bấm phải tự ẩn
		// theo quyền — dễ để sót nút mà người dùng bấm phải mới thấy lỗi quyền.
		string homeTitle = isEmployee ? "Nhân Viên" : isHr ? "Nhân Sự" : "Quản Lý";
		string homeIcon = isEmployee ? TAB_ICON_EMPLOYEE : isHr ? TAB_ICON_HR : TAB_ICON_MANAGER;
		Type homePageType = isEmployee
			? typeof(EmployeeHomePage)
			: isHr
				? typeof(HrHomePage)
				: typeof(ManagerHomePage);

		ShellContent homeTab = new()
		{
			Title = homeTitle,
			Icon = homeIcon,
			Route = "Home",
			ContentTemplate = new DataTemplate(homePageType)
		};

		ShellContent settingsTab = new()
		{
			Title = "Cài Đặt",
			Icon = TAB_ICON_SETTINGS,
			Route = "Settings",
			ContentTemplate = new DataTemplate(typeof(SettingsPage))
		};

		ShellContent attendanceTab = new()
		{
			Title = "Chấm Công",
			Icon = TAB_ICON_ATTENDANCE,
			Route = "Attendance",
			ContentTemplate = new DataTemplate(typeof(AttendancePage))
		};

		// Thứ tự tab cố định: trang chủ ở đầu, chức năng làm việc ở giữa,
		// Cài Đặt sát mép phải. Cài Đặt nằm cuối vì nó là menu phụ — nhân viên
		// vào bằng ngón tay phải, không cạnh tranh chỗ với ca trực ở giữa.
		//
		// Màu phải đặt ở đây chứ không khai trong <Shell.Resources>: TabBarForegroundColor
		// là attached property của Shell và chỉ Shell mới tra tới trên cây Shell
		// do chính nó sinh ra. TabBar ở đây do code dựng nên không đi qua bước tra
		// resource đó, và để trống thì Shell rơi về bảng màu mặc định (hồng magenta)
		// — lệch hẳn nhận diện navy + vàng kim. Tab đang chọn tô vàng kim, tab chưa
		// chọn xám xanh để nổi bật tab hiện hành mà vẫn giữ tương phản trên nền trắng.
		var mainTabs = new TabBar { Route = "MainTabs" };
		Shell.SetTabBarBackgroundColor(mainTabs, Color.FromArgb(TAB_BAR_BACKGROUND));
		Shell.SetTabBarForegroundColor(mainTabs, Color.FromArgb(TAB_BAR_SELECTED));
		Shell.SetTabBarUnselectedColor(mainTabs, Color.FromArgb(TAB_BAR_UNSELECTED));
		Shell.SetTabBarDisabledColor(mainTabs, Color.FromArgb(TAB_BAR_DISABLED));

		// Nhãn tab có màu riêng, không dùng chung với icon: để trống thì Shell rơi về
		// TitleColor mặc định (hồng magenta) và nhãn tab hiện hành lại lệch màu thương hiệu.
		Shell.SetTabBarTitleColor(mainTabs, Color.FromArgb(TAB_BAR_TITLE));

		mainTabs.Items.Add(homeTab);
		mainTabs.Items.Add(attendanceTab);
		mainTabs.Items.Add(settingsTab);

		// Màu thanh tab chỉ ăn khi đặt trên TabBar; nhưng từng tab con cũng cần màu
		// riêng cho nhãn, vì bộ dựng hình Shell tô màu chữ theo chính ShellContent.
		// Thiếu bước này thì nhãn tab rơi về màu tiêu đề mặc định của hệ thống.
		ApplyTabAppearance(homeTab);
		ApplyTabAppearance(settingsTab);
		ApplyTabAppearance(attendanceTab);

		Items.Add(mainTabs);

		_tabsBuilt = true;
	}

	/// <summary>
	/// Gắn màu nhãn thương hiệu lên một tab con. Không đặt màu icon ở đây vì Shell
	/// không có API tô icon theo từng tab — màu icon do TabBarForegroundColor lo.
	/// </summary>
	private static void ApplyTabAppearance(ShellContent tab) =>
		Shell.SetTabBarTitleColor(tab, Color.FromArgb(TAB_BAR_TITLE));
}
