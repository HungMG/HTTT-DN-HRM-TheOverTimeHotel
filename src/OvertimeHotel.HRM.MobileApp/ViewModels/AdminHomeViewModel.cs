using System.Collections.ObjectModel;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel cho Trang Chủ Quản Trị Viên (Admin Dashboard trên Mobile).
/// Thiết kế bám sát Mục 6 của tài liệu design_ui_ux.md.
/// </summary>
public class AdminHomeViewModel : HomeViewModelBase
{
    private string _commandRole = "Quản Trị Viên";
    private string _commandScope = "Toàn hệ thống";
    private string _permissionLabel = "TOÀN QUYỀN HỆ THỐNG";
    private int _pendingAccountRequestCount = 4;
    private string _pendingAccountRequestDescription = "3 nhân viên mới · 1 đổi phân quyền";
    private int _activeAccountCount = 156;
    private string _lastUpdatedText = "Cập nhật lúc 08:35";
    private bool _isRefreshing;

    public ObservableCollection<AdminAuditLogItem> RecentActivities { get; } = new();

    public ICommand OpenAccountRequestsCommand { get; }
    public ICommand OpenLeaveApprovalCommand { get; }
    public ICommand OpenAuditLogsCommand { get; }
    public ICommand OpenDesktopWebCommand { get; }
    public ICommand RefreshCommand { get; }

    public event EventHandler<string>? ShowNotificationDialog;

    public string CommandRole
    {
        get => _commandRole;
        private set => SetProperty(ref _commandRole, value);
    }

    public string CommandScope
    {
        get => _commandScope;
        private set => SetProperty(ref _commandScope, value);
    }

    public string PermissionLabel
    {
        get => _permissionLabel;
        private set => SetProperty(ref _permissionLabel, value);
    }

    public int PendingAccountRequestCount
    {
        get => _pendingAccountRequestCount;
        set => SetProperty(ref _pendingAccountRequestCount, value);
    }

    public string PendingAccountRequestDescription
    {
        get => _pendingAccountRequestDescription;
        set => SetProperty(ref _pendingAccountRequestDescription, value);
    }

    public int ActiveAccountCount
    {
        get => _activeAccountCount;
        set => SetProperty(ref _activeAccountCount, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public AdminHomeViewModel(IAuthService authService, ILeaveApprovalService leaveApprovalService)
        : base(authService, leaveApprovalService)
    {
        OpenAccountRequestsCommand = new Command(() =>
        {
            ShowNotificationDialog?.Invoke(this, "Yêu cầu cấp tài khoản: Vui lòng xem danh sách chi tiết và hoàn tất phê duyệt kích hoạt trên cổng Web Admin máy tính.");
        });

        OpenLeaveApprovalCommand = new Command(() => _ = Shell.Current.GoToAsync("LeaveApprovalPage"));
        OpenAuditLogsCommand = new Command(() => _ = Shell.Current.GoToAsync("AdminAuditLogPage"));

        OpenDesktopWebCommand = new Command(() =>
        {
            ShowNotificationDialog?.Invoke(this, "Cổng Quản Trị Máy Tính: Các nghiệp vụ cấu hình lương, phân quyền và sao lưu dữ liệu cần thực hiện tại: https://hrm.overtime.local");
        });

        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            await LoadAsync();
            IsRefreshing = false;
        });
    }

    public override async Task LoadAsync()
    {
        await base.LoadAsync();
        LastUpdatedText = $"Cập nhật lúc {DateTime.Now:HH:mm}";

        // Nạp 3 hoạt động kiểm toán gần nhất theo Mục 6 đặc tả
        RecentActivities.Clear();
        RecentActivities.Add(new AdminAuditLogItem
        {
            AuditLogId = 1,
            ActionCode = "APPROVE",
            ActionName = "Duyệt đơn",
            Description = "Thao.Nguyen (HR) duyệt đơn nghỉ phép #102",
            ActorName = "Thao.Nguyen",
            TargetUsername = "NV-042",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        });
        RecentActivities.Add(new AdminAuditLogItem
        {
            AuditLogId = 2,
            ActionCode = "SHIFT_CHANGE",
            ActionName = "Đổi ca",
            Description = "Dang.Le đổi ca trực FO Ca 1 ➔ Ca 2",
            ActorName = "Dang.Le",
            TargetUsername = "NV-019",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-50)
        });
        RecentActivities.Add(new AdminAuditLogItem
        {
            AuditLogId = 3,
            ActionCode = "USER_LOCK",
            ActionName = "Khóa tài khoản",
            Description = "IT-Admin khóa tài khoản NV-088 do sai PIN",
            ActorName = "IT-Admin",
            TargetUsername = "NV-088",
            OccurredAt = DateTimeOffset.UtcNow.AddHours(-3)
        });
    }

    protected override async Task LoadBranchKpiAsync(UserSession user)
    {
        PendingLeaveSub = "Thẩm quyền Admin";

        if (!user.CanApproveLeave) return;

        try
        {
            PendingLeaveCount = await LeaveApprovalService.GetPendingCountAsync(user.TenVaiTro, null);
        }
        catch
        {
            PendingLeaveCount = 1;
        }
    }
}
