using Microsoft.Extensions.DependencyInjection;

namespace OvertimeHotel.HRM.MobileApp;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));

		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// AppShell nhận IAuthService để dựng thanh tab theo vai trò, nên phải lấy
		// từ DI thay vì new trực tiếp (xem MauiProgram.AddSingleton<AppShell>).
		var shell = _services.GetRequiredService<AppShell>();

		var window = new Window(shell)
		{
			Title = "The OverTime Hotel · HRM Mobile App",
			Width = 420,
			Height = 840,
			MinimumWidth = 360,
			MinimumHeight = 650,
			MaximumWidth = 480
		};

		return window;
	}
}
