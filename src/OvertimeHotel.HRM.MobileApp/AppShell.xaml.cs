using OvertimeHotel.HRM.MobileApp.Views;

namespace OvertimeHotel.HRM.MobileApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("LeaveApprovalPage", typeof(LeaveApprovalPage));
		Routing.RegisterRoute("LeaveDetailPage", typeof(LeaveDetailPage));
	}
}
