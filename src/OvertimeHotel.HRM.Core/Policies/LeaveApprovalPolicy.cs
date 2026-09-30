using OvertimeHotel.HRM.Core.Enums;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) định nghĩa toàn bộ quy tắc phân quyền (RBAC),
/// phạm vi phòng ban và điều kiện duyệt/từ chối đơn từ cho Task 19.
/// </summary>
public static class LeaveApprovalPolicy
{
    // Ma trận loại đơn:
    // Loại 1: Nghỉ phép năm -> Manager (cùng phòng ban), Admin
    // Loại 2: Nghỉ ốm / BHXH -> Manager (cùng phòng ban), Admin
    // Loại 3: Nghỉ thai sản -> HR (toàn khách sạn), Admin
    // Loại 4: Nghỉ việc riêng không lương -> Manager (cùng phòng ban), Admin
    // Loại 5: Xin thôi việc -> HR (toàn khách sạn), Admin
    private static readonly HashSet<int> ManagerAllowedTypes = new() { 1, 2, 4 };
    private static readonly HashSet<int> HrAllowedTypes = new() { 3, 5 };

    /// <summary>
    /// Chuẩn hóa tên vai trò từ chuỗi (hỗ trợ cả tiếng Anh và tiếng Việt trong DB).
    /// </summary>
    public static RoleType? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        var clean = role.Trim().ToUpperInvariant();
        return clean switch
        {
            "ADMIN" or "QUANTRI" or "QUANTRIVIEN" => RoleType.Admin,
            "HR" or "NHANSU" or "QUANLYNHANSU" => RoleType.HR,
            "MANAGER" or "QUANLY" or "TRUONGPHONG" or "TRUONG_PHONG" => RoleType.Manager,
            "EMPLOYEE" or "NHANVIEN" or "NHAN_VIEN" => RoleType.Employee,
            _ => null
        };
    }

    public static bool IsAdmin(string? role) => NormalizeRole(role) == RoleType.Admin;
    public static bool IsHR(string? role) => NormalizeRole(role) == RoleType.HR;
    public static bool IsManager(string? role) => NormalizeRole(role) == RoleType.Manager;

    /// <summary>
    /// Liệt kê các mã loại đơn mà vai trò được phép tiếp nhận và duyệt.
    /// Dùng để lọc danh sách đơn ngay ở tầng truy vấn (ví dụ khối "Cần bạn xử lý"
    /// trên bảng điều khiển) thay vì tải toàn bộ đơn rồi mới lọc trong bộ nhớ.
    /// </summary>
    public static IReadOnlyCollection<int> GetReviewableRequestTypes(string? role)
    {
        return NormalizeRole(role) switch
        {
            // Admin xem tất cả các loại đơn trên toàn khách sạn
            RoleType.Admin => ManagerAllowedTypes.Union(HrAllowedTypes).ToArray(),
            // HR duyệt loại 3, 5 toàn khách sạn
            RoleType.HR => HrAllowedTypes.ToArray(),
            // Manager duyệt loại 1, 2, 4 nhưng còn bị giới hạn cùng phòng ban
            RoleType.Manager => ManagerAllowedTypes.ToArray(),
            // Employee hoặc vai trò không xác định không được tiếp nhận đơn
            _ => []
        };
    }

    /// <summary>
    /// Kiểm tra người dùng có quyền xem đơn từ này hay không theo nguyên tắc Fail-Closed.
    /// </summary>
    public static bool CanView(string? role, int? reviewerDeptId, int? requestDeptId, int maLoaiDon)
    {
        var normalized = NormalizeRole(role);
        if (normalized == null)
            return false;

        return normalized switch
        {
            RoleType.Admin => true, // Admin xem tất cả các loại đơn trên toàn khách sạn
            RoleType.HR => HrAllowedTypes.Contains(maLoaiDon), // HR xem loại 3, 5 toàn khách sạn
            RoleType.Manager => reviewerDeptId.HasValue &&
                                requestDeptId.HasValue &&
                                reviewerDeptId.Value == requestDeptId.Value &&
                                ManagerAllowedTypes.Contains(maLoaiDon), // Manager xem loại 1, 2, 4 cùng phòng ban
            _ => false // Employee hoặc vai trò khác không có quyền
        };
    }

    /// <summary>
    /// Kiểm tra thẩm quyền phê duyệt / từ chối đơn theo nguyên tắc Fail-Closed.
    /// </summary>
    public static bool CanReview(string? role, int? reviewerDeptId, int? requestDeptId, int maLoaiDon)
    {
        return CanView(role, reviewerDeptId, requestDeptId, maLoaiDon);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ toàn diện khi thực hiện phê duyệt / từ chối đơn.
    /// </summary>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateReviewAction(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        string currentStatus,
        bool approved,
        string? phanHoi)
    {
        // 1. Kiểm tra trạng thái hiện tại của đơn (chỉ cho phép xử lý đơn CHO_DUYET)
        if (!string.Equals(currentStatus, "CHO_DUYET", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "ALREADY_REVIEWED", "Đơn từ không còn ở trạng thái chờ duyệt (đã được người khác xử lý).");
        }

        // 2. Kiểm tra thẩm quyền RBAC
        if (!CanReview(role, reviewerDeptId, requestDeptId, maLoaiDon))
        {
            return (false, "FORBIDDEN", "Bạn không có thẩm quyền duyệt loại đơn này.");
        }

        // 3. Quy tắc từ chối: Bắt buộc có lý do
        if (!approved && string.IsNullOrWhiteSpace(phanHoi))
        {
            return (false, "VALIDATION_ERROR", "Bắt buộc phải nhập lý do khi từ chối đơn.");
        }

        // 4. Giới hạn độ dài phản hồi tối đa 255 ký tự theo schema database
        if (!string.IsNullOrEmpty(phanHoi) && phanHoi.Trim().Length > 255)
        {
            return (false, "VALIDATION_ERROR", "Phản hồi duyệt không được vượt quá 255 ký tự.");
        }

        return (true, null, null);
    }
}
