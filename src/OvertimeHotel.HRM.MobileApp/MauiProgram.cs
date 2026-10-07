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
			.UseMauiMaps()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("SegoeUI-Regular.ttf", "SegoeUI");
				fonts.AddFont("SegoeUI-Bold.ttf", "SegoeUIBold");
			});

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
			builder.Services.AddSingleton<ILeaveApprovalService, DemoLeaveApprovalService>();
			builder.Services.AddSingleton<IAttendanceService, DemoAttendanceService>();
		}
		else
		{
			builder.Services.AddSingleton<IAuthService, AuthService>();
			builder.Services.AddSingleton<ILeaveApprovalService, LeaveApprovalService>();
			builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
		}

		// Client PostgREST dùng chung cho các service nghiệp vụ. Singleton để tái
		// dùng một HttpClient/connection pool — dựng HttpClient mới cho mỗi lần
		// quẹt thẻ sẽ rò bộ nhớ socket trên Android.
		builder.Services.AddSingleton<SupabaseRestClient>();

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
		builder.Services.AddTransient<AttendanceViewModel>();
		builder.Services.AddTransient<EmployeeDashboardViewModel>();
		builder.Services.AddTransient<EmployeeHomeViewModel>();
		builder.Services.AddTransient<ManagerHomeViewModel>();
		builder.Services.AddTransient<HrHomeViewModel>();

		// Đăng ký Views
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<IntroPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<EmployeeHomePage>();
		builder.Services.AddTransient<ManagerHomePage>();
		builder.Services.AddTransient<HrHomePage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<ChangePasswordPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<LeaveApprovalPage>();
		builder.Services.AddTransient<LeaveDetailPage>();
		builder.Services.AddTransient<LeaveSubmitPage>();
		builder.Services.AddTransient<AttendancePage>();

		// KHÔNG bọc trong #if DEBUG: bản Release mới là bản chạy thật trên
		// emulator, mà AddDebug() chỉ hoạt động ở Debug — nên mọi _logger.LogError
		// trong AuthService im lặng đúng lúc cần nhất. AndroidLogProvider ghi
		// thẳng vào Android.Util.Log nên vẫn sống trong Release. Cả hai đều chỉ ra
		// logcat, không ghi ra UI và không ghi mật khẩu/token.
		builder.Logging.AddDebug();
		builder.Logging.AddProvider(new AndroidLogProvider());
		builder.Logging.SetMinimumLevel(LogLevel.Debug);

		return builder.Build();
	}
}
