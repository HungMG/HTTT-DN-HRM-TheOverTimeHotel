using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

/// <summary>
/// Cổng API trả về hồ sơ đầy đủ cho phiên đã đăng nhập.
/// JWT chỉ mang mã (EmployeeId, DepartmentId) chứ không mang tên phòng ban / chức vụ,
/// nên khi app khôi phục phiên (auto-login) cần gọi endpoint này để lấy lại tên đầy đủ.
/// Tách riêng khỏi <c>MobileAuthController</c> vì controller đó gắn [AllowAnonymous] ở cấp lớp.
/// </summary>
[ApiController]
[Route("api/mobile/profile")]
[Authorize(AuthenticationSchemes = "MobileBearer")]
public class MobileProfileController : ControllerBase
{
    private readonly AppDbContext _context;

    public MobileProfileController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        // Token do Supabase GoTrue cấp: mã nhân viên nằm trong app_metadata và đã
        // được rải ra claim phẳng "ma_nhan_vien" ở OnTokenValidated.
        //
        // Trước đây đọc ClaimTypes.NameIdentifier — nhưng "sub" của Supabase là
        // UUID của auth.users, int.TryParse luôn fail dù token hợp lệ.
        var maNhanVien = SupabaseClaims.GetMaNhanVien(User);
        if (maNhanVien is not > 0)
        {
            return Unauthorized(new LoginResponse(false, "Phiên đăng nhập không hợp lệ."));
        }

        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(t => t.MaNhanVien == maNhanVien.Value);

        // Tài khoản bị khoá giữa chừng thì phải buộc app quay về màn Login.
        // Claim chỉ là gợi ý để tra, không phải giấy uỷ quyền (Zero Trust).
        if (account == null || account.TrangThai != true)
        {
            return Unauthorized(new LoginResponse(false, "Tài khoản đã bị khoá hoặc không còn hiệu lực."));
        }

        return Ok(new LoginResponse(
            Success: true,
            Message: "Lấy hồ sơ thành công.",
            MaTaiKhoan: account.MaTaiKhoan,
            MaNhanVien: account.MaNhanVien,
            HoTen: account.NhanVien != null ? $"{account.NhanVien.Ho} {account.NhanVien.Ten}".Trim() : account.TenDangNhap,
            TenDangNhap: account.TenDangNhap,
            TenVaiTro: account.VaiTro?.TenVaiTro ?? "Employee",
            TenPhongBan: account.NhanVien?.PhongBan?.TenPhongBan,
            TenChucVu: account.NhanVien?.ChucVu?.TenChucVu
        ));
    }
}