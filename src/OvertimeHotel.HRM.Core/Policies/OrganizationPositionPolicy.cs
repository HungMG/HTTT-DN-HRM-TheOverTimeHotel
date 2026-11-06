using System.Linq.Expressions;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public static class OrganizationPositionPolicy
{
    // Department names are catalog data; do not assume Ban Giám Đốc always has ID 1.
    public static Expression<Func<ChucVu, bool>> ForDepartment(PhongBan? department)
    {
        if (department == null) return position => false;
        var id = department.MaPhongBan;
        if (department.TenPhongBan.Trim().StartsWith("Ban Giám Đốc", StringComparison.OrdinalIgnoreCase))
            return position => position.MaPhongBan == id &&
                (position.TenChucVu.Trim().ToUpper() == "TỔNG GIÁM ĐỐC" ||
                 position.TenChucVu.Trim().ToUpper().StartsWith("TỔNG GIÁM ĐỐC ("));

        return position => position.MaPhongBan == id || position.MaPhongBan == null;
    }
}
