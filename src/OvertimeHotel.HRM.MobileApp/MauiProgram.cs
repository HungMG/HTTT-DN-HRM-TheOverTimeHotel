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
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("SegoeUI-Regular.ttf", "SegoeUI");
				fonts.AddFont("SegoeUI-Bold.ttf", "SegoeUIBold");
			});

		// Đăng ký dịch vụ ứng dụng
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<ILeaveApprovalService, LeaveApprovalService>();

		// Đăng ký ViewModels
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();
		builder.Services.AddTransient<LeaveApprovalViewModel>();
		builder.Services.AddTransient<LeaveDetailViewModel>();

		// Đăng ký Views
		builder.Services.AddTransient<IntroPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<LeaveApprovalPage>();
		builder.Services.AddTransient<LeaveDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
