using Microsoft.Extensions.DependencyInjection;

namespace OvertimeHotel.HRM.MobileApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell())
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
