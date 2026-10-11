using OvertimeHotel.HRM.Core.Enums;

namespace OvertimeHotel.HRM.Core.Policies;

public static class AttendanceAccessPolicy
{
    /// <summary>
    /// Vai trò không có hồ sơ lao động nên không có bảng công cho chính mình.
    ///
    /// <para>
    /// <b>Không tự ý mở rộng danh sách này.</b> Mỗi vai trò thêm vào đây đều đồng
    /// nghĩa với việc gỡ ô "Công &amp; nghỉ phép" khỏi trang chủ và ẩn nút "Thử lại"
    /// trên trang bảng công — tức là một quyết định sản phẩm, không chỉ một điều
    /// kiện kỹ thuật. Sửa ở đây thì UI tự bám theo, còn sửa rải rác ở từng
    /// ViewModel/View thì các bản sao sẽ trôi lệch nhau.
    /// </para>
    /// </summary>
    public static bool CanViewOwnAttendanceSummary(string? role) =>
        LeaveApprovalPolicy.NormalizeRole(role) is not (RoleType.Admin or RoleType.IT);

    public static bool CanView(string? role, int viewerEmployeeId, int? viewerDepartmentId,
        int targetEmployeeId, int? targetDepartmentId)
    {
        var normalized = LeaveApprovalPolicy.NormalizeRole(role);
        if (normalized == null || viewerEmployeeId <= 0 || targetEmployeeId <= 0)
            return false;

        return viewerEmployeeId == targetEmployeeId || normalized is RoleType.Admin or RoleType.HR ||
            (normalized == RoleType.Manager && viewerDepartmentId.HasValue &&
             viewerDepartmentId == targetDepartmentId);
    }
}
