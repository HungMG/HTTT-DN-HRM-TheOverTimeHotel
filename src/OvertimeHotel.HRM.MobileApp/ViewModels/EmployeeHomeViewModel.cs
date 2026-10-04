using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh NHÂN VIÊN: bố cục ưu tiên thao tác nhanh trong ca trực —
/// quẹt thẻ check-in nổi bật ngay đầu trang, kèm đồng hồ thật và bản đồ GPS.
/// </summary>
public class EmployeeHomeViewModel : HomeViewModelBase
{
    public EmployeeHomeViewModel(IAuthService authService, ILeaveApprovalService leaveApprovalService)
        : base(authService, leaveApprovalService)
    {
    }

    /// <summary>Khung giờ của ca đang trực, hiển thị trên thẻ hero.</summary>
    public string ShiftTimeText => "22:00 – 06:00";

    /// <summary>Nhiệm vụ trong ca, hiển thị dưới khung giờ.</summary>
    public string ShiftTaskText => "Trực sảnh & An ninh · Đúng giờ ca quy định";

    /// <summary>Huy hiệu phụ cấp ca đêm, luôn cố định theo công thức lương (+30%).</summary>
    public string NightBadgeText => "Ca Đêm (+30%)";
}