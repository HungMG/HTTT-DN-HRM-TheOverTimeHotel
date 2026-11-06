using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel trang chủ nhánh QUẢN LÝ &amp; QUẢN TRỊ: bố cục ưu tiên duyệt đơn từ,
/// theo dõi phạm vi quản lý và giám sát ca trực vận hành khách sạn.
/// </summary>
public class ManagerHomeViewModel : HomeViewModelBase
{
    private readonly IAttendanceService _attendanceService;

    private string _shiftTimeText = "--:-- – --:--";
    private string _shiftTaskText = "Đang tải ca trực...";
    private string _nightBadgeText = string.Empty;
    private bool _hasShiftToday;

    public ManagerHomeViewModel(
        IAuthService authService,
        ILeaveApprovalService leaveApprovalService,
        IAttendanceService attendanceService)
        : base(authService, leaveApprovalService)
    {
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        ApproveCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));
        RefreshManagerScope();
    }

    /// <summary>Mở màn hình duyệt đơn từ trực tuyến — thao tác quan trọng nhất của nhánh này.</summary>
    public ICommand ApproveCommand { get; }

    /// <summary>Nhãn vai trò hiển thị trên thẻ phạm vi quản lý.</summary>
    public string CommandRole { get; private set; } = "Quản trị viên";

    /// <summary>Phạm vi duyệt đơn: phòng ban của quản lý, hoặc toàn khách sạn với HR/Quản trị viên.</summary>
    public string CommandScope { get; private set; } = "Toàn khách sạn";

    /// <summary>Người dùng hiện tại có thẩm quyền phê duyệt đơn hay không.</summary>
    public bool CanApproveLeave => AuthService.CurrentUser?.CanApproveLeave == true;

    /// <summary>
    /// Quyền hạn hiển thị dưới dòng phạm vi trên thẻ navy.
    /// </summary>
    public string PermissionLabel => CanApproveLeave ? "CÓ QUYỀN DUYỆT ĐƠN" : "CHỈ XEM · KHÔNG DUYỆT ĐƠN";

    /// <summary>Khung giờ ca trực hoặc điều hành hôm nay.</summary>
    public string ShiftTimeText
    {
        get => _shiftTimeText;
        private set => SetProperty(ref _shiftTimeText, value);
    }

    /// <summary>Tên nhiệm vụ / ca điều hành hôm nay.</summary>
    public string ShiftTaskText
    {
        get => _shiftTaskText;
        private set => SetProperty(ref _shiftTaskText, value);
    }

    /// <summary>Huy hiệu ca trực hoặc quản lý trực ca.</summary>
    public string NightBadgeText
    {
        get => _nightBadgeText;
        private set => SetProperty(ref _nightBadgeText, value);
    }

    /// <summary>Hôm nay có ca trực trong phân ca hay không.</summary>
    public bool HasShiftToday
    {
        get => _hasShiftToday;
        private set => SetProperty(ref _hasShiftToday, value);
    }

    /// <summary>
    /// Số đơn đã xử lý trong phạm vi quyền hạn — chỉ số tiếp theo sau đơn chờ duyệt.
    /// </summary>
    private int _reviewedLeaveCount;

    /// <summary>Số đơn đã xử lý. Được nạp cùng lúc với đơn chờ duyệt.</summary>
    public int ReviewedLeaveCount
    {
        get => _reviewedLeaveCount;
        private set => SetProperty(ref _reviewedLeaveCount, value);
    }

    /// <summary>
    /// Nạp lại vai trò và phạm vi quản lý sau mỗi lần trang hiện ra.
    /// </summary>
    public override async Task LoadAsync()
    {
        RefreshManagerScope();
        await base.LoadAsync();
    }

    /// <summary>
    /// Nhánh Quản lý nạp thêm ca trực hôm nay và số đơn đã xử lý.
    /// </summary>
    protected override async Task LoadBranchKpiAsync(UserSession user)
    {
        await LoadTodayShiftAsync();

        if (!user.CanApproveLeave)
        {
            ReviewedLeaveCount = 0;
            return;
        }

        try
        {
            var reviewed = await LeaveApprovalService.GetReviewedRequestsAsync(user.TenVaiTro, user.MaPhongBan);
            ReviewedLeaveCount = reviewed.Count;
        }
        catch
        {
            // Giống đơn chờ duyệt: CSDL chập chờn thì hiện 0 thay vì chặn màn hình.
            ReviewedLeaveCount = 0;
        }
    }

    private async Task LoadTodayShiftAsync()
    {
        try
        {
            var shift = await _attendanceService.GetTodayShiftAsync();
            HasShiftToday = shift.MaPhanCa > 0;

            if (!HasShiftToday)
            {
                ShiftTimeText = "08:00 – 17:00";
                ShiftTaskText = "Ca điều hành tiêu chuẩn · Quản lý vận hành khách sạn";
                NightBadgeText = "Trưởng Bộ Phận";
                return;
            }

            ShiftTimeText = $"{FormatTime(shift.GioBatDau)} – {FormatTime(shift.GioKetThuc)}";
            ShiftTaskText = string.IsNullOrWhiteSpace(shift.TenSuKien)
                ? (string.IsNullOrWhiteSpace(shift.TenCa) ? "Ca điều hành & quản lý" : shift.TenCa)
                : $"{shift.TenCa} • {shift.TenSuKien}";
            NightBadgeText = IsNightShift(shift.GioBatDau, shift.GioKetThuc)
                ? "Ca Đêm · +30%"
                : "Quản Lý Trực Ca";
        }
        catch
        {
            HasShiftToday = false;
            ShiftTimeText = "08:00 – 17:00";
            ShiftTaskText = "Ca điều hành tiêu chuẩn · Quản lý vận hành khách sạn";
            NightBadgeText = "Trưởng Bộ Phận";
        }
    }

    private static bool IsNightShift(DateTimeOffset start, DateTimeOffset end) =>
        end.TimeOfDay <= start.TimeOfDay;

    private static string FormatTime(DateTimeOffset value) =>
        value == default ? "--:--" : value.ToLocalTime().ToString("HH:mm");

    private void RefreshManagerScope()
    {
        var user = AuthService.CurrentUser;
        if (user is null)
        {
            return;
        }

        CommandRole = user.IsAdmin
            ? "Quản trị viên"
            : user.IsHR
                ? "Nhân sự (HR)"
                : "Quản lý bộ phận";

        // Quản lý chỉ duyệt được đơn trong phòng ban của mình; HR và quản trị viên duyệt toàn khách sạn.
        // Tài khoản quản lý chưa được gán phòng ban trong CSDL thì rơi về phạm vi mặc định
        // để thẻ không bị trống — nói rõ là chưa gán phòng ban chứ không bịa tên.
        CommandScope = user.IsManager && !user.IsHR && !user.IsAdmin
            ? (string.IsNullOrWhiteSpace(user.TenPhongBan) ? "Chưa gán phòng ban" : user.TenPhongBan)
            : "Toàn khách sạn";

        OnPropertyChanged(nameof(CommandRole));
        OnPropertyChanged(nameof(CommandScope));
        OnPropertyChanged(nameof(PermissionLabel));
        OnPropertyChanged(nameof(CanApproveLeave));
    }
}