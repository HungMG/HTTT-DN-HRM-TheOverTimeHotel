using System.Security.Claims;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

internal static class EmployeeResignationAccountLock
{
    public static bool IsResigned(string? status) => status is "DA_THOI_VIEC" or "THOI_VIEC" or "NGHI_VIEC";

    // The caller loads TaiKhoan and saves the lock with the employee and contract changes.
    public static bool Apply(AppDbContext db, NhanVien employee, HttpContext context)
    {
        if (!IsResigned(employee.TrangThai) || employee.TaiKhoan is not { TrangThai: true } account)
            return false;

        var now = DateTimeOffset.UtcNow;
        account.TrangThai = false;
        account.PhienHopLeSau = now;
        db.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) ? actorId : null,
            MaTaiKhoanBiTacDong = account.MaTaiKhoan,
            TenNguoiThucHien = context.User.FindFirstValue("FullName") ?? context.User.Identity?.Name ?? "Không xác định",
            TenTaiKhoanBiTacDong = account.TenDangNhap,
            HanhDong = "LOCK_ACCOUNT",
            NoiDung = "Tự động khóa tài khoản khi nhân viên nghỉ việc.",
            GiaTriCu = "Trạng thái: Hoạt động",
            GiaTriMoi = "Trạng thái: Đã khóa",
            LyDo = "Nhân viên đã nghỉ việc.",
            DiaChiIp = context.Connection.RemoteIpAddress?.ToString(),
            ThoiGian = now
        });
        return true;
    }
}
