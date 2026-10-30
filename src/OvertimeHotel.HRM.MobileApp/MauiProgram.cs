using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.MobileApp.Services;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Views;

namespace OvertimeHotel.HRM.MobileApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if !WINDOWS
			.UseMauiMaps()
#else
			.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler<Microsoft.Maui.Controls.Maps.Map, StubMapHandler>();
			})
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("SegoeUI-Regular.ttf", "SegoeUI");
				fonts.AddFont("SegoeUI-Bold.ttf", "SegoeUIBold");
			});

		// Đăng ký ILogger<T> vào DI.
		//
		// Ràng buộc này sập âm thầm: AddDebug()/AddProvider() chỉ đăng ký
		// ILoggerFactory, KHÔNG đăng ký ILogger<T>. Chỉ AddLogging() mới gắn
		// open-generic ILogger<> -> Logger<>. Thiếu nó thì mọi service có
		// ILogger<T> trong constructor đều chết lúc resolve với
		// "CannotResolveService, ILogger, <TênService>".
		//
		// Triệu chứng trước khi sửa: đăng nhập fail (AuthService đòi ILogger),
		// và app crash ngay khi AppShell dựng tab đầu tiên vì SupabaseRestClient
		// cũng đòi ILogger. AddLogging dùng TryAdd nên gọi lại nhiều lần vô hại.
		builder.Services.AddLogging();

		// Đăng ký dịch vụ ứng dụng: Hỗ trợ cấu hình chạy Production (JWT API) hoặc Demo (In-Memory)
		// Ưu tiên cờ biên dịch -p:DemoMode=true; cờ này có định nghĩa tại thời điểm build nên
		// Preferences vẫn được đọc để bật/tắt demo ngay trên máy đã cài mà không cần build lại.
#if DEMO
		bool useDemoMode = true;
#else
		bool useDemoMode = Preferences.Get("use_demo_mode", false);
#endif
		if (useDemoMode)
		{
			builder.Services.AddSingleton<IAuthService, DemoAuthService>();
			builder.Services.AddSingleton<DemoLeaveApprovalService>();
			builder.Services.AddSingleton<ILeaveApprovalService>(services => services.GetRequiredService<DemoLeaveApprovalService>());
			builder.Services.AddSingleton<IAttendanceService, DemoAttendanceService>();
			builder.Services.AddSingleton<IMobileAttendanceSummaryService, DemoAttendanceSummaryService>();
			builder.Services.AddSingleton<IAttendanceExplanationService, DemoAttendanceExplanationService>();
			builder.Services.AddSingleton<IShiftSwapService, DemoShiftSwapService>();
		}
		else
		{
			builder.Services.AddSingleton<IAuthService, AuthService>();
			builder.Services.AddSingleton<ILeaveApprovalService, LeaveApprovalService>();
			builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
			builder.Services.AddSingleton<IMobileAttendanceSummaryService, MobileAttendanceSummaryService>();
			builder.Services.AddSingleton<IAttendanceExplanationService, AttendanceExplanationService>();
			builder.Services.AddSingleton<IShiftSwapService, ShiftSwapService>();

			// Lịch 24/7 và phiếu lương: LUÔN đọc CSDL thật, không có nhánh demo.
			// Cả hai bảng đều bị RLS giới hạn theo chủ phiếu, nên dữ liệu mẫu
			// trong bộ nhớ sẽ không khớp quyền thật của người đang đăng nhập.
			builder.Services.AddSingleton<IScheduleService, MobileScheduleService>();
			builder.Services.AddSingleton<IPayslipService, MobilePayslipService>();
		}

		// Client PostgREST dùng chung cho các service nghiệp vụ. Singleton để tái
		// dùng một HttpClient/connection pool — dựng HttpClient mới cho mỗi lần
		// quẹt thẻ sẽ rò bộ nhớ socket trên Android.
		builder.Services.AddSingleton<SupabaseRestClient>();
        builder.Services.AddSingleton<WebAdminApiClient>();

		// Ghi nhận vị trí quẹt thẻ để đối chiếu chống gian lận chấm công.
		builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();

		// Đăng ký ViewModels
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();
		builder.Services.AddTransient<ChangePasswordViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<LeaveApprovalViewModel>();
		builder.Services.AddTransient<LeaveDetailViewModel>();
		builder.Services.AddTransient<LeaveSubmitViewModel>();
		builder.Services.AddTransient<RequestsViewModel>();
		builder.Services.AddTransient<AttendanceExplanationViewModel>();
		builder.Services.AddTransient<ShiftSwapViewModel>();
		builder.Services.AddTransient<AttendanceViewModel>();
		builder.Services.AddTransient<AttendanceSummaryViewModel>();
		builder.Services.AddTransient<ScheduleViewModel>();
		builder.Services.AddTransient<PayslipViewModel>();
		builder.Services.AddTransient<EmployeeDashboardViewModel>();
		builder.Services.AddTransient<EmployeeHomeViewModel>();
		builder.Services.AddTransient<ManagerHomeViewModel>();
		builder.Services.AddTransient<HrHomeViewModel>();
		builder.Services.AddTransient<AdminHomeViewModel>();
		builder.Services.AddTransient<AdminAuditLogViewModel>();
		builder.Services.AddTransient<HrStatisticsViewModel>();

		// Đăng ký Views
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<IntroPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<EmployeeHomePage>();
		builder.Services.AddTransient<ManagerHomePage>();
		builder.Services.AddTransient<HrHomePage>();
		builder.Services.AddTransient<AdminHomePage>();
		builder.Services.AddTransient<AdminAuditLogPage>();
		builder.Services.AddTransient<HrStatisticsPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<ChangePasswordPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<LeaveApprovalPage>();
		builder.Services.AddTransient<LeaveDetailPage>();
		builder.Services.AddTransient<LeaveSubmitPage>();
		builder.Services.AddTransient<RequestsPage>();
		builder.Services.AddTransient<AttendanceExplanationPage>();
		builder.Services.AddTransient<ShiftSwapPage>();
		builder.Services.AddTransient<AttendancePage>();
		builder.Services.AddTransient<AttendanceSummaryPage>();
		builder.Services.AddTransient<SchedulePage>();
		builder.Services.AddTransient<PayslipPage>();

		// KHÔNG bọc trong #if DEBUG: bản Release mới là bản chạy thật trên
		// emulator, mà AddDebug() chỉ hoạt động ở Debug — nên mọi _logger.LogError
		// trong AuthService im lặng đúng lúc cần nhất. AndroidLogProvider ghi
		// thẳng vào Android.Util.Log nên vẫn sống trong Release. Cả hai đều chỉ ra
		// logcat, không ghi ra UI và không ghi mật khẩu/token.
		//
		// KHÔNG bọc trong #if ANDROID: AndroidLogProvider tự rẽ nhánh non-Android
		// bên trong hàm Log (rơi về Debug.WriteLine), nên nó hợp lệ ở mọi target
		// mà MauiProgram.cs được biên dịch. Bọc #if ở đây chỉ khiến các bản
		// khác Android mất log hoàn toàn.
		builder.Logging.AddDebug();
		builder.Logging.AddProvider(new AndroidLogProvider());
		builder.Logging.SetMinimumLevel(LogLevel.Debug);

		return builder.Build();
	}
}

#if WINDOWS
public class StubMapHandler : Microsoft.Maui.Handlers.ViewHandler<Microsoft.Maui.Controls.Maps.Map, Microsoft.UI.Xaml.Controls.Border>
{
	public StubMapHandler() : base(PropertyMapper) { }
	public static PropertyMapper<Microsoft.Maui.Controls.Maps.Map, StubMapHandler> PropertyMapper = new(ViewMapper);
	protected override Microsoft.UI.Xaml.Controls.Border CreatePlatformView() => new();
}
#endif
