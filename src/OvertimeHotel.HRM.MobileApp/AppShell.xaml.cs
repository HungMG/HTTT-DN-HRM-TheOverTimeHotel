using OvertimeHotel.HRM.MobileApp.Services;
using OvertimeHotel.HRM.MobileApp.Views;

namespace OvertimeHotel.HRM.MobileApp;

/// <summary>
/// Vỏ ứng dụng. Ba màn hình ngoài phiên (Intro, Login) đứng riêng; phần sau
/// đăng nhập là một thanh tab dựng động theo vai trò của phiên đang đăng nhập.
/// Bám sát quy chuẩn điều hướng 4 vai trò trong design_ui_ux.md Mục 2.
/// </summary>
public partial class AppShell : Shell
{
	/// <summary>Route tới trang chủ của nhánh đang đăng nhập.</summary>
	public const string HomeRoute = "//MainTabs/Home";

	private const string TAB_ICON_EMPLOYEE = "tab_home.png";
	private const string TAB_ICON_MANAGER = "tab_manager.png";
	private const string TAB_ICON_HR = "tab_hr.png";
	private const string TAB_ICON_SETTINGS = "tab_settings.png";
	private const string TAB_ICON_ATTENDANCE = "tab_attendance.png";
	private const string TAB_ICON_DOCS = "tab_docs.png";

	private const string TAB_BAR_BACKGROUND = "#FFFFFF";
	private const string TAB_BAR_SELECTED = "#D4AF37";
	private const string TAB_BAR_UNSELECTED = "#94A3B8";
	private const string TAB_BAR_DISABLED = "#CBD5E1";
	private const string TAB_BAR_TITLE = "#1E3A8A";

	private readonly IAuthService _authService;
	private readonly IDispatcherTimer _sessionTimer;
	private bool _checkingSession;
	private bool _tabsBuilt;

	public AppShell(IAuthService authService)
	{
		_authService = authService ?? throw new ArgumentNullException(nameof(authService));

		InitializeComponent();
		_sessionTimer = Dispatcher.CreateTimer();
		_sessionTimer.Interval = TimeSpan.FromSeconds(10);
		_sessionTimer.Tick += async (_, _) => await CheckSessionAsync();

		// Đăng ký các route toàn cục
		Routing.RegisterRoute("LeaveApprovalPage", typeof(LeaveApprovalPage));
		Routing.RegisterRoute("LeaveDetailPage", typeof(LeaveDetailPage));
		Routing.RegisterRoute("LeaveSubmitPage", typeof(LeaveSubmitPage));
		Routing.RegisterRoute("RequestsPage", typeof(RequestsPage));
		Routing.RegisterRoute("AttendanceExplanationPage", typeof(AttendanceExplanationPage));
		Routing.RegisterRoute("ShiftSwapPage", typeof(ShiftSwapPage));
		Routing.RegisterRoute("AttendanceSummaryPage", typeof(AttendanceSummaryPage));
		Routing.RegisterRoute("PersonalAttendancePage", typeof(PersonalAttendancePage));
		Routing.RegisterRoute("SchedulePage", typeof(SchedulePage));
		Routing.RegisterRoute("PayslipPage", typeof(PayslipPage));
		Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
		Routing.RegisterRoute("ChangePasswordPage", typeof(ChangePasswordPage));

		// Tuyến đường mới cho Admin và HR
		Routing.RegisterRoute("AdminHomePage", typeof(AdminHomePage));
		Routing.RegisterRoute("AdminAuditLogPage", typeof(AdminAuditLogPage));
		Routing.RegisterRoute("HrStatisticsPage", typeof(HrStatisticsPage));
	}

	/// <summary>
	/// Dựng thanh tab theo đúng vai trò rồi nhảy tới trang chủ.
	/// Hỗ trợ cả 4 vai trò: Nhân viên, Quản lý, Nhân sự (HR), Quản trị viên (Admin).
	/// </summary>
	public async Task OpenHomeAsync()
	{
		var user = _authService.CurrentUser;

		BuildRoleTabs(
			isEmployee: user?.IsEmployee == true,
			isHr: user?.IsHR == true,
			isAdmin: user?.IsAdmin == true,
			isManager: user?.IsManager == true);

		await GoToAsync(HomeRoute);
		_sessionTimer.Start();
	}

	private async Task CheckSessionAsync()
	{
		var user = _authService.CurrentUser;
		if (_checkingSession || user == null || _authService.IsDemoMode) return;
		_checkingSession = true;
		try
		{
			var status = await _authService.CheckSessionAsync();
			if (status != SessionStatus.Invalid || !ReferenceEquals(user, _authService.CurrentUser)) return;
			await _authService.LogoutAsync();
			ClearRoleTabs();
			await GoToAsync("//LoginPage");
		}
		finally { _checkingSession = false; }
	}

	/// <summary>Gỡ toàn bộ tab của nhánh đã đăng nhập (dùng khi thoát tài khoản).</summary>
	public void ClearRoleTabs()
	{
		_sessionTimer.Stop();
		foreach (TabBar bar in Items.OfType<TabBar>().ToList())
		{
			Items.Remove(bar);
		}

		_tabsBuilt = false;
	}

	private void BuildRoleTabs(bool isEmployee, bool isHr, bool isAdmin, bool isManager)
	{
		if (_tabsBuilt)
		{
			return;
		}

		ClearRoleTabs();

		var mainTabs = new TabBar { Route = "MainTabs" };
		Shell.SetTabBarBackgroundColor(mainTabs, Color.FromArgb(TAB_BAR_BACKGROUND));
		Shell.SetTabBarForegroundColor(mainTabs, Color.FromArgb(TAB_BAR_SELECTED));
		Shell.SetTabBarUnselectedColor(mainTabs, Color.FromArgb(TAB_BAR_UNSELECTED));
		Shell.SetTabBarDisabledColor(mainTabs, Color.FromArgb(TAB_BAR_DISABLED));
		Shell.SetTabBarTitleColor(mainTabs, Color.FromArgb(TAB_BAR_TITLE));

		// Tab Cài Đặt (dùng chung cho mọi vai trò, luôn nằm ở cuối)
		ShellContent settingsTab = new()
		{
			Title = "Cài Đặt",
			Icon = TAB_ICON_SETTINGS,
			Route = "Settings",
			ContentTemplate = new DataTemplate(typeof(SettingsPage))
		};
		ApplyTabAppearance(settingsTab);

		if (isAdmin)
		{
			// ═══ NHÁNH QUẢN TRỊ VIÊN (ADMIN) ═══
			// 1: Trang chủ | 2: Duyệt đơn | 3: Nhật ký | 4: Cài đặt
			var adminHomeTab = new ShellContent
			{
				Title = "Quản Trị",
				Icon = TAB_ICON_MANAGER,
				Route = "Home",
				ContentTemplate = new DataTemplate(typeof(AdminHomePage))
			};
			var leaveApprovalTab = new ShellContent
			{
				Title = "Duyệt Đơn",
				Icon = TAB_ICON_DOCS,
				Route = "LeaveApproval",
				ContentTemplate = new DataTemplate(typeof(LeaveApprovalPage))
			};
			var auditLogTab = new ShellContent
			{
				Title = "Nhật Ký",
				Icon = TAB_ICON_DOCS,
				Route = "AuditLog",
				ContentTemplate = new DataTemplate(typeof(AdminAuditLogPage))
			};

			ApplyTabAppearance(adminHomeTab);
			ApplyTabAppearance(leaveApprovalTab);
			ApplyTabAppearance(auditLogTab);

			mainTabs.Items.Add(adminHomeTab);
			mainTabs.Items.Add(leaveApprovalTab);
			mainTabs.Items.Add(auditLogTab);
			mainTabs.Items.Add(settingsTab);
		}
		else if (isHr)
		{
			// ═══ NHÁNH NHÂN SỰ (HR) ═══
			// 1: Trang chủ | 2: Duyệt đơn | 3: Thống kê & báo cáo | 4: Cài đặt
			var hrHomeTab = new ShellContent
			{
				Title = "Nhân Sự",
				Icon = TAB_ICON_HR,
				Route = "Home",
				ContentTemplate = new DataTemplate(typeof(HrHomePage))
			};
			var leaveApprovalTab = new ShellContent
			{
				Title = "Duyệt Đơn",
				Icon = TAB_ICON_DOCS,
				Route = "LeaveApproval",
				ContentTemplate = new DataTemplate(typeof(LeaveApprovalPage))
			};
			var statsTab = new ShellContent
			{
				Title = "Thống Kê",
				Icon = TAB_ICON_ATTENDANCE,
				Route = "Statistics",
				ContentTemplate = new DataTemplate(typeof(HrStatisticsPage))
			};

			ApplyTabAppearance(hrHomeTab);
			ApplyTabAppearance(leaveApprovalTab);
			ApplyTabAppearance(statsTab);

			mainTabs.Items.Add(hrHomeTab);
			mainTabs.Items.Add(leaveApprovalTab);
			mainTabs.Items.Add(statsTab);
			mainTabs.Items.Add(settingsTab);
		}
		else if (isManager)
		{
			// ═══ NHÁNH QUẢN LÝ BỘ PHẬN (MANAGER) ═══
			// Trang chủ, chấm công, đơn của bản thân, duyệt đơn và cài đặt.
			var managerHomeTab = new ShellContent
			{
				Title = "Quản Lý",
				Icon = TAB_ICON_MANAGER,
				Route = "Home",
				ContentTemplate = new DataTemplate(typeof(ManagerHomePage))
			};
			var attendanceTab = new ShellContent
			{
				Title = "Chấm Công",
				Icon = TAB_ICON_ATTENDANCE,
				Route = "Attendance",
				ContentTemplate = new DataTemplate(typeof(AttendancePage))
			};
			var leaveApprovalTab = new ShellContent
			{
				Title = "Duyệt Đơn",
				Icon = TAB_ICON_DOCS,
				Route = "LeaveApproval",
				ContentTemplate = new DataTemplate(typeof(LeaveApprovalPage))
			};
			var requestsTab = new ShellContent
			{
				Title = "Đơn Từ",
				Icon = TAB_ICON_DOCS,
				Route = "Requests",
				ContentTemplate = new DataTemplate(typeof(RequestsPage))
			};

			ApplyTabAppearance(managerHomeTab);
			ApplyTabAppearance(attendanceTab);
			ApplyTabAppearance(requestsTab);
			ApplyTabAppearance(leaveApprovalTab);

			mainTabs.Items.Add(managerHomeTab);
			mainTabs.Items.Add(attendanceTab);
			mainTabs.Items.Add(requestsTab);
			mainTabs.Items.Add(leaveApprovalTab);
			mainTabs.Items.Add(settingsTab);
		}
		else
		{
			// ═══ NHÁNH NHÂN VIÊN (EMPLOYEE) ═══
			// 1: Trang chủ | 2: Chấm công | 3: Giấy tờ | 4: Cài đặt
			var employeeHomeTab = new ShellContent
			{
				Title = "Nhân Viên",
				Icon = TAB_ICON_EMPLOYEE,
				Route = "Home",
				ContentTemplate = new DataTemplate(typeof(EmployeeHomePage))
			};
			var attendanceTab = new ShellContent
			{
				Title = "Chấm Công",
				Icon = TAB_ICON_ATTENDANCE,
				Route = "Attendance",
				ContentTemplate = new DataTemplate(typeof(AttendancePage))
			};
			var requestsTab = new ShellContent
			{
				Title = "Đơn Từ",
				Icon = TAB_ICON_DOCS,
				Route = "Requests",
				ContentTemplate = new DataTemplate(typeof(RequestsPage))
			};

			ApplyTabAppearance(employeeHomeTab);
			ApplyTabAppearance(attendanceTab);
			ApplyTabAppearance(requestsTab);

			mainTabs.Items.Add(employeeHomeTab);
			mainTabs.Items.Add(attendanceTab);
			mainTabs.Items.Add(requestsTab);
			mainTabs.Items.Add(settingsTab);
		}

		Items.Add(mainTabs);
		_tabsBuilt = true;
	}

	private static void ApplyTabAppearance(ShellContent tab) =>
		Shell.SetTabBarTitleColor(tab, Color.FromArgb(TAB_BAR_TITLE));
}
