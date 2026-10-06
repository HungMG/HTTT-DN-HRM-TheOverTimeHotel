using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh NHÂN SỰ. Gần nhánh quản lý nhưng phạm vi rộng hơn:
/// theo <c>LeaveApprovalPolicy</c>, HR duyệt toàn bộ 5 loại đơn trên toàn khách
/// sạn — kể cả đơn của quản lý, vì quản lý nộp đơn thì không còn ai ở cấp dưới.
/// </summary>
public class HrHomeViewModel : HomeViewModelBase
{
	private string _commandRole = "Nhân Sự";
	private string _commandScope = "Toàn khách sạn";
	private string _permissionLabel = "CHỈ XEM · KHÔNG DUYỆT ĐƠN";
	private int _reviewedLeaveCount;

	/// <summary>Lệnh mở trang duyệt đơn — nhánh HR cần vào thẳng từ trang chủ.</summary>
	public Command ApproveCommand { get; }

	public HrHomeViewModel(IAuthService authService, ILeaveApprovalService leaveApprovalService)
		: base(authService, leaveApprovalService)
	{
		ApproveCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));
	}

	/// <summary>Vai trò hiển thị trên thẻ phạm vi — lấy từ session, không ghi cứng.</summary>
	public string CommandRole
	{
		get => _commandRole;
		private set => SetProperty(ref _commandRole, value);
	}

	/// <summary>Phạm vi dữ liệu mà vai trò này được phép duyệt.</summary>
	public string CommandScope
	{
		get => _commandScope;
		private set => SetProperty(ref _commandScope, value);
	}

	/// <summary>Huy hiệu quyền trên thẻ phạm vi: có duyệt được đơn hay không.</summary>
	public string PermissionLabel
	{
		get => _permissionLabel;
		private set => SetProperty(ref _permissionLabel, value);
	}

	/// <summary>Số đơn đã xử lý — nhánh HR không có ô "Ca trực đêm" ghi cứng như quản lý.</summary>
	public int ReviewedLeaveCount
	{
		get => _reviewedLeaveCount;
		private set => SetProperty(ref _reviewedLeaveCount, value);
	}

	/// <summary>
	/// Ghi đè nhãn phụ của lớp cơ sở — nó mặc định ghi "Bộ phận quản lý" cho mọi
	/// vai trò có quyền duyệt, gâi sai nhãn với HR.
	/// </summary>
	public override async Task LoadAsync()
	{
		RefreshHrScope();
		await base.LoadAsync();
	}

	protected override async Task LoadBranchKpiAsync(UserSession user)
	{
		// Lớp cơ sở gán PendingLeaveSub = "Bộ phận quản lý" cho mọi vai trò có quyền
		// duyệt. HR duyệt toàn bộ 5 loại đơn (kể cả đơn của quản lý) nên phải ghi đè
		// lại ở đây — hàm này chạy sau LoadPendingLeaveCountAsync nên mới ghi đè được.
		PendingLeaveSub = user.CanApproveLeave ? "Toàn bộ 5 loại đơn" : "Đơn của bạn";

		ReviewedLeaveCount = 0;

		if (!user.CanApproveLeave)
		{
			return;
		}

		try
		{
			var reviewed = await LeaveApprovalService.GetReviewedRequestsAsync(user.TenVaiTro, user.MaPhongBan);
			ReviewedLeaveCount = reviewed.Count;
		}
		catch
		{
			// CSDL chập chờn thì giữ 0 đơn đã xử lý. Không chặn trang chủ: người dùng
			// vẫn vào được trang duyệt đơn để thử lại.
			ReviewedLeaveCount = 0;
		}
	}

	/// <summary>
	/// HR xem toàn khách sạn. Tài khoản kiêm quản lý thì phạm vi hẹp lại theo
	/// phòng ban — cùng logic phạm vi với <see cref="UserSession.CanApproveLeave"/>.
	/// </summary>
	private void RefreshHrScope()
	{
		var user = AuthService.CurrentUser;
		if (user is null)
		{
			return;
		}

		CommandRole = user.IsHR ? "Nhân Sự" : LocalizeRole(user.TenVaiTro);
		CommandScope = user.IsManager && !user.IsHR && !user.IsAdmin
			? (string.IsNullOrWhiteSpace(user.TenPhongBan) ? "Chưa gán phòng ban" : user.TenPhongBan)
			: "Toàn khách sạn";
		PermissionLabel = user.CanApproveLeave ? "CÓ QUYỀN DUYỆT ĐƠN" : "CHỈ XEM · KHÔNG DUYỆT ĐƠN";
	}
}
