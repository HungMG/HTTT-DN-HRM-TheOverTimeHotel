using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Đọc lịch phân ca 24/7 của chính người đang đăng nhập.
/// </summary>
/// <remarks>
/// Ràng buộc bảo mật: RLS <c>doc_phan_ca</c> chỉ cho phép đọc ca của CHÍNH
/// mình. Màn hình này dành cho nhân viên nên không nhận tham số mã nhân viên —
/// luôn kẹp theo phiên đăng nhập. Nhờ vậy không có đường nào để xem lịch của
/// đồng nghiệp, kể cả khi ai đó gọi hàm bằng tay.
/// </remarks>
public interface IScheduleService
{
    bool IsDemoMode { get; }

    Task<IReadOnlyList<ScheduleEventItem>> GetEventsAsync(IReadOnlyList<PhanCa> shifts, CancellationToken cancellationToken = default);

    /// <summary>Lấy các ca trong khoảng <paramref name="from"/>..<paramref name="to"/> (bao trọn hai đầu).</summary>
    Task<IReadOnlyList<PhanCa>> GetScheduleAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}