using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// Lớp cơ sở cho hai ViewModel trang chủ sau đăng nhập (nhánh Nhân viên và nhánh Quản lý).
/// Gom những phần hai nhánh dùng chung: thông tin người dùng, dòng phòng ban/chức vụ,
/// và số đơn chờ duyệt. Mỗi nhánh chỉ viết phần riêng của mình.
/// </summary>
public abstract class HomeViewModelBase : INotifyPropertyChanged
{
    protected readonly IAuthService AuthService;
    protected readonly ILeaveApprovalService LeaveApprovalService;

    private string _greeting = "Xin chào!";
    private string _roleDepartmentLine = "The OverTime Hotel";
    private string _pendingLeaveSub = "Đang nạp...";
    private int _pendingLeaveCount;

    protected HomeViewModelBase(IAuthService authService, ILeaveApprovalService leaveApprovalService)
    {
        AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
        LeaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));

        // Thẻ chức năng "Hồ Sơ" trên trang chủ nay mở thẳng trang Thông tin cá nhân,
        // không qua tab Cài Đặt nữa — bỏ một bước bấm thừa.
        ProfileCommand = new Command(() => _ = Shell.Current.GoToAsync("ProfilePage"));
        FeatureCommand = new Command<string>(featureName => RequestFeature(featureName));

        // Thẻ chức năng "Đơn Điện Tử" (TSK-18). Mở cho MỌI vai trò, không chỉ
        // nhân viên: quản lý và HR cũng là người lao động của chính khách sạn này,
        // họ cần nộp được đơn của mình chứ không chỉ duyệt đơn của người khác.
        LeaveSubmitCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveSubmitPage"));

        // Admin và IT không có hồ sơ lao động nên không có bảng công cho chính mình.
        // Ô chức năng đã bị ẩn theo CanViewAttendanceSummary; lệnh này là lưới an toàn
        // cho trường hợp ô vẫn còn hiện (đổi vai trò khi trang đang mở). Không báo
        // bằng DisplayAlert thô — quy chuẩn §3 cấm, View sẽ bắt sự kiện và hiện
        // Custom Luxury Popup.
        AttendanceSummaryCommand = new Command(() =>
        {
            if (AuthService.CurrentUser?.CanViewAttendanceSummary != true)
            {
                AttendanceSummaryForbidden?.Invoke(this, EventArgs.Empty);
                return;
            }
            _ = Shell.Current.GoToAsync("AttendanceSummaryPage");
        });
    }

    /// <summary>
    /// Phát ra khi bấm ô "Công &amp; nghỉ phép" mà vai trò hiện tại không được xem.
    /// Trang chủ nhận sự kiện để hiện Custom Luxury Modal Popup.
    /// </summary>
    public event EventHandler? AttendanceSummaryForbidden;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Cho biết vai trò hiện tại có áp dụng xem công &amp; nghỉ phép hay không (Admin và IT trả false).</summary>
    public bool CanViewAttendanceSummary => AuthService.CurrentUser?.CanViewAttendanceSummary ?? false;

    /// <summary>Mở trang Thông tin cá nhân (trang push từ Cài Đặt hoặc từ trang chủ).</summary>
    public ICommand ProfileCommand { get; }

    /// <summary>Mở cổng nộp đơn điện tử của nhân viên (TSK-18).</summary>
    public ICommand LeaveSubmitCommand { get; }

    public ICommand AttendanceSummaryCommand { get; }

    /// <summary>Mở một tính năng nghiệp vụ; nhận tên tính năng qua CommandParameter.</summary>
    public ICommand FeatureCommand { get; }

    /// <summary>
    /// Phát sự kiện khi người dùng bấm một tính năng chưa được nối vào API.
    /// Trang chủ nhận sự kiện để hiện Custom Luxury Modal Popup.
    /// </summary>
    public event EventHandler<string>? FeatureRequested;

    /// <summary>Lệnh mở trang Hồ Sơ, dùng cho thẻ chức năng "Hồ Sơ".</summary>
    protected void RequestFeature(string featureName) =>
        FeatureRequested?.Invoke(this, featureName);

    /// <summary>Lời chào kèm họ tên người dùng vừa đăng nhập.</summary>
    public string Greeting
    {
        get => _greeting;
        protected set => SetProperty(ref _greeting, value);
    }

    /// <summary>Dòng "Bộ phận · Chức vụ (Vai trò)" hiển thị dưới lời chào.</summary>
    public string RoleDepartmentLine
    {
        get => _roleDepartmentLine;
        protected set => SetProperty(ref _roleDepartmentLine, value);
    }

    /// <summary>Số đơn từ đang chờ duyệt (nhánh Quản lý) hoặc của bạn (nhánh Nhân viên).</summary>
    public int PendingLeaveCount
    {
        get => _pendingLeaveCount;
        protected set
        {
            if (SetProperty(ref _pendingLeaveCount, value))
            {
                OnPropertyChanged(nameof(HasPendingLeaves));
                OnPropertyChanged(nameof(HasNoPendingLeaves));
                OnPropertyChanged(nameof(UrgentApproveButtonText));
            }
        }
    }

    /// <summary>Có đơn chờ duyệt hay không (dùng để ẩn/hiện thẻ khẩn cấp theo Phương án 1).</summary>
    public bool HasPendingLeaves => PendingLeaveCount > 0;

    /// <summary>Ngược lại của HasPendingLeaves (dùng để hiện thẻ hoàn thành nhiệm vụ).</summary>
    public bool HasNoPendingLeaves => PendingLeaveCount == 0;

    /// <summary>Nút hành động khẩn cấp có kèm số lượng đơn.</summary>
    public string UrgentApproveButtonText => PendingLeaveCount > 0
        ? $"Xử lý ngay ({PendingLeaveCount} đơn)"
        : "Mở danh sách duyệt đơn";

    /// <summary>Dòng chú thích nhỏ dưới số đếm đơn.</summary>
    public string PendingLeaveSub
    {
        get => _pendingLeaveSub;
        protected set => SetProperty(ref _pendingLeaveSub, value);
    }

    /// <summary>Nạp lại thông tin người dùng và số đơn chờ duyệt. Gọi mỗi lần trang hiện ra.</summary>
    public virtual async Task LoadAsync()
    {
        var user = AuthService.CurrentUser;

        if (user is null || !user.IsLoggedIn)
        {
            Greeting = "Xin chào, Nhân viên!";
            RoleDepartmentLine = "Bộ phận Buồng phòng · The OverTime Hotel";
            PendingLeaveCount = 0;
            PendingLeaveSub = "Chưa đăng nhập";
            return;
        }

        Greeting = $"Xin chào, {user.HoTen}!";
        RoleDepartmentLine = BuildRoleDepartmentLine(user);
        OnPropertyChanged(nameof(CanViewAttendanceSummary));

        await LoadPendingLeaveCountAsync(user);
        await LoadBranchKpiAsync(user);
    }

    /// <summary>
    /// Móc nối riêng cho từng nhánh nạp thêm chỉ số đặc thù. Nhánh Nhân viên
    /// không có chỉ số riêng nên mặc định không làm gì; nhánh Quản lý ghi đè
    /// để nạp số đơn đã xử lý.
    /// </summary>
    protected virtual Task LoadBranchKpiAsync(UserSession user) => Task.CompletedTask;

    /// <summary>Nạp số đơn chờ duyệt theo phạm vi quyền hạn của người dùng.</summary>
    private async Task LoadPendingLeaveCountAsync(UserSession user)
    {
        if (!user.CanApproveLeave)
        {
            PendingLeaveCount = 0;
            PendingLeaveSub = "Đơn của bạn";
            return;
        }

        PendingLeaveSub = user.IsManager ? "Bộ phận quản lý" : "Toàn khách sạn";

        try
        {
            PendingLeaveCount = await LeaveApprovalService.GetPendingCountAsync(user.TenVaiTro, user.MaPhongBan);
        }
        catch
        {
            // Không chặn màn hình khi CSDL chập chờn: hiển thị 0 đơn thay vì báo lỗi dở dang.
            PendingLeaveCount = 0;
            PendingLeaveSub = "Không có đơn";
        }
    }

    /// <summary>
    /// Dòng phụ dưới lời chào: "Bộ phận X · Chức vụ Y (Vai trò)".
    /// Bỏ qua mọi phần rỗng để không sinh dấu phân cách treo lơ lửng kiểu "· (Manager)".
    /// internal (không protected) vì tab Cài Đặt cũng dựng lại dòng này mà
    /// SettingsViewModel không kế thừa lớp này.
    /// </summary>
    internal static string BuildRoleDepartmentLine(UserSession user)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(user.TenPhongBan)) parts.Add(user.TenPhongBan);
        if (!string.IsNullOrWhiteSpace(user.TenChucVu)) parts.Add(user.TenChucVu);

        var role = LocalizeRole(user.TenVaiTro);
        if (!string.IsNullOrWhiteSpace(role)) parts.Add($"({role})");

        return parts.Count > 0
            ? string.Join(" · ", parts)
            : "The OverTime Hotel";
    }

    /// <summary>
    /// Dịch mã vai trò trong CSDL (tiếng Anh) sang nhãn tiếng Việt.
    /// CSDL lưu vai trò dạng "Employee"/"Manager"/"HR"/"Admin" nên hiển thị thẳng
    /// sẽ lọt tiếng Anh vào giữa giao diện tiếng Việt. Trả về chuỗi gốc nếu
    /// không khớp mã nào để không mất thông tin với vai trò mới thêm về sau.
    /// </summary>
    internal static string LocalizeRole(string? role) =>
        role?.Trim().ToLowerInvariant() switch
        {
            "employee" => "Nhân viên",
            "manager" => "Quản lý",
            "hr" => "Nhân sự",
            "admin" => "Quản trị viên",
            null or "" => string.Empty,
            _ => role
        };

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
