using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public static class ItHeadDeletionGuard
{
    // Both account deletion (including Auth removal) and employee purge hold this lock.
    public const int LockId = int.MinValue;
    public const string BusyMessage = "Đang có thao tác xóa tài khoản hoặc nhân viên. Vui lòng thử lại.";

    public static async Task<string?> GetDenialAsync(AppDbContext db, int employeeId)
    {
        var employee = await db.NhanViens.AsNoTracking().Include(n => n.PhongBan)
            .Include(n => n.ChucVu).SingleOrDefaultAsync(n => n.MaNhanVien == employeeId);
        if (employee == null) return null;
        var isHead = AccountDeletionPolicy.IsItDepartmentHead(employee.PhongBan?.TenPhongBan, employee.ChucVu?.TenChucVu);
        if (db.Database.IsNpgsql())
        {
            // Retired heads can still have a grant referencing their appointment appendix.
            isHead |= await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM public.it_head_grant WHERE ma_nhan_vien={employeeId}) AS \"Value\"").SingleAsync();
            if (!isHead) return null;
            var replacement = await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM public.nhan_vien n WHERE n.ma_nhan_vien <> {employeeId} AND public.hrm_it_head_access(n.ma_nhan_vien)) AS \"Value\"").SingleAsync();
            if (replacement) return null;
        }
        else if (!isHead) return null;
        return "Cần có ít nhất một trưởng bộ phận IT khác đang hoạt động và đã được cấp quyền hợp lệ trước khi xóa.";
    }
}
