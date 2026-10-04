using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Data.Context;

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
        // Lấy mã tài khoản từ chính token đã xác thực, không nhận từ query string.
        var rawId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(rawId, out var maTaiKhoan))
        {
            return Unauthorized(new LoginResponse(false, "Phiên đăng nhập không hợp lệ."));
        }

        var account = await _context.TaiKhoans
            .Include(t => t.VaiTro)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(t => t.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .FirstOrDefaultAsync(t => t.MaTaiKhoan == maTaiKhoan);

        // Tài khoản bị khoá giữa chừng thì phải buộc app quay về màn Login.
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