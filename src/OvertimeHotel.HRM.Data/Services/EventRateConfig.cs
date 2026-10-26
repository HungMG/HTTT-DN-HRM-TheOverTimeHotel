using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

/// <summary>
/// Đọc hệ số thưởng sự kiện theo giờ từ cấu hình khoản lương
/// (<c>cau_hinh_khoan_luong.ma_khoan = SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID</c>).
/// Hệ số này do HR/Admin sửa tại màn hình Cấu hình lương &amp; Phụ cấp → Thưởng.
/// Khi dòng cấu hình bị xóa hoặc giá trị không hợp lệ, dùng hằng số mặc định
/// <see cref="SalaryConstants.EVENT_HOURLY_RATE"/> để bảng lương không bị chặn.
/// </summary>
public static class EventRateConfig
{
    public const string ConfigName = "Hệ số thưởng sự kiện theo giờ";

    public static async Task<decimal> ReadAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var rate = await db.CauHinhKhoanLuongs.AsNoTracking()
            .Where(x => x.MaKhoan == SalaryConstants.EVENT_HOURLY_RATE_CONFIG_ID
                && x.LoaiKhoan == "THUONG"
                && x.GiaTriMacDinh > 0)
            .Select(x => x.GiaTriMacDinh)
            .FirstOrDefaultAsync(cancellationToken);

        return rate > 0 ? rate : SalaryConstants.EVENT_HOURLY_RATE;
    }
}
