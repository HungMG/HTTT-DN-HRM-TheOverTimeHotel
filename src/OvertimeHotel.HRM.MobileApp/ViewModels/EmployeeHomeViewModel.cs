using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh NHÂN VIÊN: bố cục ưu tiên thao tác nhanh trong ca trực —
/// quẹt thẻ check-in nổi bật ngay đầu trang, kèm đồng hồ thật và bản đồ GPS.
/// </summary>
public class EmployeeHomeViewModel : HomeViewModelBase
{
    private readonly IAttendanceService _attendanceService;

    private string _shiftTimeText = "--:-- – --:--";
    private string _shiftTaskText = "Đang tải ca làm việc...";
    private string _nightBadgeText = string.Empty;
    private bool _hasShiftToday;
    private string _checkInButtonText = "ĐANG TẢI CA...";

    public EmployeeHomeViewModel(
        IAuthService authService,
        ILeaveApprovalService leaveApprovalService,
        IAttendanceService attendanceService)
        : base(authService, leaveApprovalService)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
    }

    /// <summary>Khung giờ của ca đang trực, nạp từ API chấm công thay vì ghi cứng.</summary>
    public string ShiftTimeText
    {
        get => _shiftTimeText;
        private set => SetProperty(ref _shiftTimeText, value);
    }

    /// <summary>Nhiệm vụ trong ca, hiển thị dưới khung giờ.</summary>
    public string ShiftTaskText
    {
        get => _shiftTaskText;
        private set => SetProperty(ref _shiftTaskText, value);
    }

    /// <summary>
    /// Huy hiệu phụ cấp. Chỉ hiện "Ca Đêm (+30%)" khi ca thật kéo qua nửa đêm —
    /// trước đây chuỗi này ghi cứng nên mọi nhân viên đều thấy ca đêm dù thực tế
    /// đang được phân ca sáng hay chiều.
    /// </summary>
    public string NightBadgeText
    {
        get => _nightBadgeText;
        private set => SetProperty(ref _nightBadgeText, value);
    }

    /// <summary>Hôm nay có ca được phân hay không — quyết định bật/tắt nút quẹt thẻ.</summary>
    public bool HasShiftToday
    {
        get => _hasShiftToday;
        private set => SetProperty(ref _hasShiftToday, value);
    }

    /// <summary>Chữ trên nút hành động, đổi theo trạng thái quẹt thẻ của ca.</summary>
    public string CheckInButtonText
    {
        get => _checkInButtonText;
        private set => SetProperty(ref _checkInButtonText, value);
    }

    /// <summary>
    /// Nạp ca hôm nay từ máy chủ rồi ghi đè bộ thuộc tính hiển thị của thẻ hero.
    /// Không chặn màn hình khi CSDL lỗi: giữ nhãn trung tính để nhân viên vẫn
    /// thấy trang chủ và tự thử lại.
    /// </summary>
    protected override async Task LoadBranchKpiAsync(UserSession user)
    {
        await LoadTodayShiftAsync();
        await base.LoadBranchKpiAsync(user);
    }

    private async Task LoadTodayShiftAsync()
    {
        try
        {
            var shift = await _attendanceService.GetTodayShiftAsync();
            HasShiftToday = shift.MaPhanCa > 0;

            if (!HasShiftToday)
            {
                ShiftTimeText = "Chưa được phân ca";
                ShiftTaskText = "Hôm nay bạn không được phân ca. Vui lòng liên hệ quản lý.";
                NightBadgeText = string.Empty;
                CheckInButtonText = "HÔM NAY KHÔNG CÓ CA";
                return;
            }

            ShiftTimeText = $"{FormatTime(shift.GioBatDau)} – {FormatTime(shift.GioKetThuc)}";
            ShiftTaskText = string.IsNullOrWhiteSpace(shift.TenSuKien)
                ? (string.IsNullOrWhiteSpace(shift.TenCa) ? "Ca làm việc" : shift.TenCa)
                : $"{shift.TenCa} • {shift.TenSuKien}";
            NightBadgeText = IsNightShift(shift.GioBatDau, shift.GioKetThuc)
                ? "Ca Đêm (+30%)"
                : "Ca Làm Việc";
            CheckInButtonText = shift.DaCheckOut.HasValue
                ? "ĐÃ QUẸT THẺ RA CA"
                : shift.DaCheckIn.HasValue
                    ? "QUẸT THẺ CHECK-OUT CA"
                    : "QUẸT THẺ CHECK-IN CA";
        }
        catch
        {
            HasShiftToday = false;
            ShiftTimeText = "--:-- – --:--";
            ShiftTaskText = "Không tải được ca làm việc. Tải lại trang để thử lại.";
            NightBadgeText = string.Empty;
            CheckInButtonText = "CHƯA TẢI ĐƯỢC CA";
        }
    }

    /// <summary>
    /// Ca kéo qua nửa đêm khi giờ kết thúc không lớn hơn giờ bắt đầu — cùng quy
    /// tắc với hàm dựng ca mẫu, giữ cho tiêu chí phụ cấp "+30%" thống nhất.
    /// </summary>
    private static bool IsNightShift(DateTimeOffset start, DateTimeOffset end) =>
        end.TimeOfDay <= start.TimeOfDay;

    private static string FormatTime(DateTimeOffset value) =>
        value == default ? "--:--" : value.ToLocalTime().ToString("HH:mm");
}