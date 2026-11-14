namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class AccountDeletionPolicy
{
    public static string? GetDenial(string? actorRole, int? actorId, int targetId,
        string? targetRole, string? department, string? position)
    {
        if (actorRole != "IT") return "Chỉ IT được xóa tài khoản.";
        if (!actorId.HasValue || actorId == targetId) return "Bạn không thể xóa tài khoản đang đăng nhập.";
        if (targetRole == "Admin") return "Không thể xóa tài khoản quản trị viên.";
        var isItDepartment = !string.IsNullOrWhiteSpace(department) &&
            (department.Contains("công nghệ thông tin", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("CNTT", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("information technology", StringComparison.OrdinalIgnoreCase));
        var isHead = !string.IsNullOrWhiteSpace(position) &&
            (position.Contains("trưởng bộ phận", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("trưởng phòng", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("IT manager", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("head of it", StringComparison.OrdinalIgnoreCase));
        if ((targetRole == "IT" || isItDepartment) && isHead)
            return "Không thể xóa tài khoản Trưởng bộ phận IT.";
        return null;
    }
}
