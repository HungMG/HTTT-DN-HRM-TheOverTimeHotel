namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class AccountDeletionPolicy
{
    public static string? GetDenial(string? actorRole, int? actorId, int targetId,
        string? targetRole, string? department, string? position, int itHeadAccountCount)
    {
        if (actorRole != "IT") return "Chỉ IT được xóa tài khoản.";
        if (!actorId.HasValue || actorId == targetId) return "Bạn không thể xóa tài khoản đang đăng nhập.";
        if (IsItDepartmentHead(department, position) && itHeadAccountCount <= 1)
            return "Không thể xóa tài khoản trưởng bộ phận IT cuối cùng.";
        return null;
    }

    public static bool IsItDepartmentHead(string? department, string? position)
    {
        var isItDepartment = !string.IsNullOrWhiteSpace(department) &&
            (department.Contains("công nghệ thông tin", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("CNTT", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("information technology", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(department.Trim(), "IT", StringComparison.OrdinalIgnoreCase));
        var isHead = !string.IsNullOrWhiteSpace(position) &&
            (position.Contains("trưởng bộ phận", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("trưởng phòng", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("IT manager", StringComparison.OrdinalIgnoreCase) ||
             position.Contains("head of it", StringComparison.OrdinalIgnoreCase));
        return isItDepartment && isHead;
    }
}
