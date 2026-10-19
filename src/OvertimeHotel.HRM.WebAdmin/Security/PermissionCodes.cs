namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Danh mục mã quyền (RBAC) dùng thống nhất cho toàn bộ Web Admin.
/// Mỗi hằng ở đây ánh xạ 1-1 với <c>QUYEN.ma_quyen_code</c> trong CSDL Supabase.
/// </summary>
public static class PermissionCodes
{
    /// <summary>Kiểu claim mang mã quyền trong token đăng nhập.</summary>
    public const string ClaimType = "Permission";

    // ── Nhóm: Quản trị hệ thống ────────────────────────────────────────────
    public const string SystemAdmin = "SYS_ADMIN";
    public const string ManageAccounts = "QL_TAI_KHOAN";

    /// <summary>
    /// Cấp user trong Supabase Auth để nhân viên đăng nhập Mobile App.
    /// </summary>
    /// <remarks>
    /// Tách khỏi <see cref="ManageAccounts"/> có chủ đích: quản lý hồ sơ tài
    /// khoản và cấp quyền đăng nhập là hai việc khác nhau. HR cần quyền cập
    /// nhật tài khoản (đổi tên đăng nhập, khoá) nhưng KHÔNG được cấp quyền vào
    /// app — cửa vào của mọi RLS policy trên Supabase chỉ nên mở cho Admin
    /// và bộ phận CNTT.
    /// </remarks>
    public const string ProvisionMobileAccount = "CAP_TAI_KHOAN_MOBILE";
    public const string ManagePermissions = "QL_PHAN_QUYEN";
    public const string ManageCatalog = "QL_DANH_MUC";
    public const string BackupDatabase = "SAO_LUU_CSDL";

    // ── Nhóm: Quản trị nhân sự ─────────────────────────────────────────────
    public const string ManageEmployees = "QL_NHAN_SU";
    public const string ManageContracts = "QL_HOP_DONG";

    // ── Nhóm: Vận hành ca & chấm công ──────────────────────────────────────
    public const string AssignShifts = "XEP_CA_LAM";
    public const string ViewDepartmentAttendance = "XEM_CHAM_CONG_BO_PHAN";
    public const string MobileCheckIn = "CHAM_CONG_DI_DONG";

    // ── Nhóm: Lương ────────────────────────────────────────────────────────
    public const string CalculatePayroll = "TINH_LUONG";
    public const string ExportPayslipPdf = "XUAT_PHIEU_LUONG_PDF";

    // ── Nhóm: Trợ lý AI ────────────────────────────────────────────────────
    /// <summary>
    /// Nạp tài liệu PDF vào kho tri thức của Trợ lý AI.
    /// </summary>
    /// <remarks>
    /// Cấp cho HR và IT. Tách khỏi <see cref="ReportView"/> có chủ đích: xem báo cáo và
    /// nạp văn bản là hai quyền khác nhau, và Quản lý bộ phận (có <c>REPORT_VIEW</c>)
    /// không được đưa tài liệu toàn khách sạn vào kho tri thức dùng chung.
    /// Admin vốn qua mọi policy nhờ <c>IsInRole("Admin")</c>.
    /// </remarks>
    public const string ManageAiKnowledge = "QL_TRI_THUC_AI";

    // ── Nhóm: Báo cáo & Thống kê ───────────────────────────────────────────
    /// <summary>
    /// Mở phân hệ Báo cáo &amp; Thống kê quản trị (Phân hệ 10.0 — TSK-24).
    /// </summary>
    /// <remarks>
    /// Theo ma trận PRD 02 mục 10.0: Admin / HR / Manager / IT được xem,
    /// Employee không. Quản lý bị thu hẹp còn bộ phận của mình ở tầng truy vấn
    /// (<c>ReportPolicy.ResolveScope</c>) chứ không phải ở mã quyền này.
    /// </remarks>
    public const string ReportView = "REPORT_VIEW";

    // ── Nhóm: Phê duyệt ────────────────────────────────────────────────────
    public const string ApproveResignation = "DUYET_THOI_VIEC";
    public const string ApproveLeave = "DUYET_DON_NGHI";
    public const string ApproveOvertime = "DUYET_TANG_CA_OT";
    public const string SubmitRequest = "NOP_DON_TU";
}
