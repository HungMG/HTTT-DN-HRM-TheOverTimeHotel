using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.Data.Services;

public sealed partial class HotelEventService
{
    public async Task<int> AutoStartReadyEventsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await db.DuAns.AsNoTracking().AnyAsync(e => e.TrangThai == "LEN_KE_HOACH"
            && e.NgayBatDau <= now && e.NgayKetThuc > now, cancellationToken)) return 0;

        return await Write(async () =>
        {
            // Re-read after taking the shared event/payroll lock: another worker may have started it.
            var scanTime = DateTimeOffset.UtcNow;
            var items = await Events.Where(e => e.TrangThai == "LEN_KE_HOACH"
                && e.NgayBatDau <= scanTime && e.NgayKetThuc > scanTime).ToListAsync(cancellationToken);
            var started = 0;
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { await EnsureReadyToStart(item); }
                catch (EventOperationException) { continue; }
                // Validation may take time; never start an event whose scheduled end has passed.
                if (item.NgayKetThuc <= DateTimeOffset.UtcNow) continue;
                item.TrangThai = "DANG_DIEN_RA";
                item.Version++;
                db.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    TenNguoiThucHien = "Hệ thống", TenTaiKhoanBiTacDong = $"SK-{item.MaDuAn}",
                    HanhDong = "TU_DONG_BAT_DAU_SU_KIEN",
                    NoiDung = "Tự động bắt đầu sự kiện khi đã đến giờ và đủ thông tin, nhân sự hợp lệ.",
                    GiaTriMoi = $"version={item.Version}; trang_thai=DANG_DIEN_RA"
                });
                started++;
            }
            return started;
        });
    }

    private async Task EnsureReadyToStart(DuAn item)
    {
        Require(item.SoKhachDuKien > 0, "", "Nhập số khách dự kiến lớn hơn 0 trước khi bắt đầu sự kiện.");
        Require(HotelEventRules.CountEffectiveStaff(item.PhanCongs) > 0, "", "Chưa có phân công thực hiện sự kiện.");
        var summary = HotelEventRules.GetStaffingSummary(item);
        Require(summary.IsFullyStaffed, "",
            $"Chưa thể bắt đầu sự kiện vì còn thiếu vai trò bắt buộc: {string.Join("; ", summary.MissingRoleDescriptions)}.");
        if (item.TinhTheoGio)
            foreach (var assignment in item.PhanCongs.Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY")))
                await ValidateSource(item, assignment);
    }
}
