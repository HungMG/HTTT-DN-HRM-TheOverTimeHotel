using System.Text.Json;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Đọc lịch phân ca 24/7 từ Supabase qua phiên đăng nhập của chính người dùng.
/// </summary>
/// <remarks>
/// <para>
/// Tái dùng <see cref="AttendanceSummaryPostgrstMapper.ToShift"/> để đọc dòng
/// JSON thành <see cref="PhanCa"/> — mapper đó đã gắn sẵn
/// <c>CaLamViec.TenCa</c> và <c>ChamCong</c> từ các quan hệ nhúng, nên không
/// cần bộ đọc JSON thứ hai cho cùng một bảng.
/// </para>
/// <para>
/// <b>Cố ý không có tham số mã nhân viên.</b> Màn hình lịch dành cho nhân viên
/// xem lịch của chính mình; RLS cũng vậy. Khoá theo phiên đăng nhập ở đây khiến
/// việc đọc nhầm lịch đồng nghiệp trở thành không thể thực hiện ngay từ API.
/// </para>
/// </remarks>
public sealed class MobileScheduleService(SupabaseRestClient rest, IAuthService authService) : IScheduleService
{
    public bool IsDemoMode => false;

    public async Task<IReadOnlyList<PhanCa>> GetScheduleAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from) throw new InvalidOperationException("Khoảng ngày không hợp lệ.");

        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true || user.MaNhanVien <= 0)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var rows = await LoadRowsAsync(
            "phan_ca?select=ma_phan_ca,ma_nhan_vien,ma_ca,ngay_lam_viec,bat_dau_du_kien," +
            "ket_thuc_du_kien,trang_thai,ca_lam_viec(ten_ca,gio_bat_dau,gio_ket_thuc,qua_dem),cham_cong(ma_cham_cong,gio_check_in," +
            "gio_check_out,trang_thai)" +
            $"&ma_nhan_vien=eq.{user.MaNhanVien}" +
            $"&ngay_lam_viec=gte.{from:yyyy-MM-dd}&ngay_lam_viec=lte.{to:yyyy-MM-dd}" +
            "&order=ngay_lam_viec.asc,ma_ca.asc",
            cancellationToken);

        return rows.Select(AttendanceSummaryPostgrstMapper.ToShift).ToArray();
    }

    public async Task<IReadOnlyList<ScheduleEventItem>> GetEventsAsync(IReadOnlyList<PhanCa> shifts, CancellationToken cancellationToken = default)
    {
        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true || user.MaNhanVien <= 0)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        var ownShifts = shifts.Where(s => s.MaNhanVien == user.MaNhanVien).ToArray();
        if (ownShifts.Length == 0) return [];
        var from = ownShifts.Min(s => s.NgayLamViec);
        var to = ownShifts.Max(s => s.NgayLamViec);
        var rows = await LoadRowsAsync($"rpc/hrm_my_schedule_events?p_from={from:yyyy-MM-dd}&p_to={to:yyyy-MM-dd}", cancellationToken);
        return rows.Select(ScheduleEventMapper.Parse).ToArray();
    }

    private async Task<List<JsonElement>> LoadRowsAsync(string query, CancellationToken cancellationToken)
    {
        var rows = new List<JsonElement>();
        // Vẫn tiếp tục khi còn dữ liệu: server có thể chặn bản trang nhỏ hơn limit.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = AttendanceSummaryPostgrstMapper.ReadRows(
                await rest.GetAsync($"{query}&limit=500&offset={rows.Count}", cancellationToken));
            if (page.Length == 0) return rows;
            rows.AddRange(page);
        }
    }
}