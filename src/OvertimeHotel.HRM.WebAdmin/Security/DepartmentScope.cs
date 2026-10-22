using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Phạm vi bộ phận của phiên đăng nhập trên Web Admin.
///
/// <para>
/// Trước đây mỗi controller tự chép lại cùng một hàm riêng tư (Phân ca, Chấm công,
/// Giải trình chấm công, Yêu cầu đổi ca). Bốn bản sao cùng một quy tắc nghĩa là
/// sửa một chỗ thì ba chỗ còn lại lặng lẽ giữ hành vi cũ — đúng loại lỗi phân
/// quyền khó phát hiện nhất. Gom về đây để chỉ còn một định nghĩa.
/// </para>
/// </summary>
public static class DepartmentScope
{
    /// <summary>Tên claim mang mã bộ phận, do <c>AccountController</c> phát hành khi đăng nhập.</summary>
    public const string ClaimType = "DepartmentId";

    /// <summary>Vai trò duy nhất bị giới hạn theo bộ phận.</summary>
    public const string ManagerRole = "Manager";

    /// <summary>
    /// Bộ phận mà Quản lý bị giới hạn trong đó.
    ///
    /// <para>
    /// <c>null</c> = không giới hạn (Admin/HR/IT điều phối toàn khách sạn).
    /// <c>-1</c> = là Quản lý nhưng thiếu claim bộ phận ⇒ đóng về "không thấy bộ
    /// phận nào" thay vì mở toang. Mở rộng khi thiếu dữ liệu là kiểu hỏng âm thầm:
    /// giao diện trông vẫn bình thường nhưng phạm vi đã sai.
    /// </para>
    /// </summary>
    public static int? GetManagedDepartmentScope(this ClaimsPrincipal user)
    {
        if (user is null || !user.IsInRole(ManagerRole))
        {
            return null;
        }

        return int.TryParse(user.FindFirstValue(ClaimType), out var departmentId) && departmentId > 0
            ? departmentId
            : -1;
    }

    /// <summary>
    /// Mã bộ phận thực sự dùng để lọc: Quản lý luôn bị ép về bộ phận của mình,
    /// người khác dùng tham số lọc do họ chọn. Trả <c>null</c> nghĩa là xem tất cả.
    /// </summary>
    public static int? ResolveEffectiveDepartmentId(this ClaimsPrincipal user, int? requestedDepartmentId)
    {
        var scope = user.GetManagedDepartmentScope();
        if (scope.HasValue)
        {
            return scope.Value;
        }

        return requestedDepartmentId is > 0 ? requestedDepartmentId : null;
    }
}
