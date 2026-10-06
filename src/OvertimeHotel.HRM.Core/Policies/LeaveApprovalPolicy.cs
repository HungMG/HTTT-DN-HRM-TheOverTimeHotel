using OvertimeHotel.HRM.Core.Enums;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) định nghĩa toàn bộ quy tắc phân quyền (RBAC),
/// phạm vi phòng ban và điều kiện duyệt/từ chối đơn từ cho Task 19.
/// </summary>
public static class LeaveApprovalPolicy
{
    // Ma trận loại đơn:
    // Loại 1: Nghỉ phép năm -> HR (toàn khách sạn), Admin
    // Loại 2: Nghỉ ốm / BHXH -> HR (toàn khách sạn), Admin
    // Loại 3: Nghỉ thai sản -> HR (toàn khách sạn), Admin
    // Loại 4: Nghỉ việc riêng không lương -> HR (toàn khách sạn), Admin
    // Loại 5: Xin thôi việc -> HR (toàn khách sạn), Admin
    //
    // HR xử lý TOÀN BỘ 5 loại đơn trên toàn khách sạn, kể cả đơn của quản lý:
    // quản lý nộp đơn thì không còn ai ở cấp dưới họ để duyệt. Quản lý chỉ duyệt
    // loại 1, 2, 4 CÙNG phòng ban; loại 3 và 5 vốn là việc của nhân sự nên quản lý
    // không đụng tới — kể cả thôi việc, vì thôi việc cần người đủ thẩm quyền chốt
    // hồ sơ chứ không chỉ xác nhận người nộp.
    private static readonly HashSet<int> ManagerAllowedTypes = new() { 1, 2, 4 };
    private static readonly HashSet<int> HrAllowedTypes = new() { 1, 2, 3, 4, 5 };

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
            // HR duyệt toàn bộ 5 loại đơn, toàn khách sạn
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
    /// <remarks>
    /// <paramref name="reviewerEmployeeId"/> và <paramref name="requestEmployeeId"/>
    /// là mã nhân viên của người duyệt và của người nộp đơn. Khi cả hai trùng nhau
    /// thì đó là đơn của chính người đang xem — mọi vai trò trừ Admin đều không được
    /// thấy, để không lộ trạng thái đơn qua chính bộ lọc quyền.
    /// </remarks>
    public static bool CanView(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        var normalized = NormalizeRole(role);
        if (normalized == null)
            return false;

        // Không ai tự duyệt đơn của chính mình, trừ quản trị viên là vai trò đứng
        // trên cùng và là thoát vòng lặp khi chính Admin cần nghỉ.
        if (normalized != RoleType.Admin &&
            reviewerEmployeeId.HasValue &&
            requestEmployeeId.HasValue &&
            reviewerEmployeeId.Value == requestEmployeeId.Value)
        {
            return false;
        }

        return normalized switch
        {
            RoleType.Admin => true, // Admin xem tất cả các loại đơn trên toàn khách sạn
            // HR xem toàn bộ 5 loại đơn toàn khách sạn, kể cả đơn của quản lý
            RoleType.HR => HrAllowedTypes.Contains(maLoaiDon),
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
    public static bool CanReview(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        return CanView(role, reviewerDeptId, requestDeptId, maLoaiDon, reviewerEmployeeId, requestEmployeeId);
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
        string? phanHoi,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        // 1. Kiểm tra trạng thái hiện tại của đơn (chỉ cho phép xử lý đơn CHO_DUYET)
        if (!string.Equals(currentStatus, "CHO_DUYET", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "ALREADY_REVIEWED", "Đơn từ không còn ở trạng thái chờ duyệt (đã được người khác xử lý).");
        }

        // 2. Chặn tự duyệt đơn của chính mình — tách riêng khỏi CanReview để trả
        // về thông báo đúng nguyên nhân thay vì chung "không có thẩm quyền".
        // Quản trị viên được miễn vì đó là thoát vòng lặp khi chính họ cần nghỉ.
        var normalized = NormalizeRole(role);
        if (normalized != RoleType.Admin &&
            reviewerEmployeeId.HasValue &&
            requestEmployeeId.HasValue &&
            reviewerEmployeeId.Value == requestEmployeeId.Value)
        {
            return (false, "SELF_REVIEW_FORBIDDEN", "Bạn không thể tự duyệt đơn của chính mình. Đơn sẽ được chuyển lên cấp trên.");
        }

        // 3. Kiểm tra thẩm quyền RBAC
        if (!CanReview(role, reviewerDeptId, requestDeptId, maLoaiDon, reviewerEmployeeId, requestEmployeeId))
        {
            return (false, "FORBIDDEN", "Bạn không có thẩm quyền duyệt loại đơn này.");
        }

        // 4. Quy tắc từ chối: Bắt buộc có lý do
        if (!approved && string.IsNullOrWhiteSpace(phanHoi))
        {
            return (false, "VALIDATION_ERROR", "Bắt buộc phải nhập lý do khi từ chối đơn.");
        }

        // 5. Giới hạn độ dài phản hồi tối đa 255 ký tự theo schema database
        if (!string.IsNullOrEmpty(phanHoi) && phanHoi.Trim().Length > 255)
        {
            return (false, "VALIDATION_ERROR", "Phản hồi duyệt không được vượt quá 255 ký tự.");
        }

        return (true, null, null);
    }
}
