using System.Globalization;
using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Services;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>Reads current schedules and approved paid leave through the user's Supabase session.</summary>
public sealed class MobileAttendanceSummaryService(SupabaseRestClient rest, IAuthService authService)
    : IMobileAttendanceSummaryService
{
    public bool IsDemoMode => false;

    public async Task<AttendanceSummaryDto> GetSummaryAsync(int year, int month, int? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12 || employeeId is <= 0)
            throw new InvalidOperationException("Tháng, năm hoặc mã nhân viên không hợp lệ.");

        await authService.RefreshPermissionsAsync();
        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true || user.MaNhanVien <= 0)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var targetId = employeeId ?? user.MaNhanVien;
        if (targetId == user.MaNhanVien && !user.CanViewAttendanceSummary)
            throw new AttendanceSummaryUnavailableException();

        // Employee sessions must never send queries for another employee.
        if (targetId != user.MaNhanVien && (!(user.IsManager || user.IsHR || user.IsAdmin)
            || !user.HasPermission(MobilePermissionPolicy.DepartmentAttendance)))
            throw new InvalidOperationException("Bạn không có quyền xem bảng công này.");

        var employees = AttendanceSummaryPostgrstMapper.ReadRows(await rest.GetAsync(
            $"nhan_vien?select=ma_nhan_vien,ho,ten,ma_phong_ban&ma_nhan_vien=eq.{targetId}&limit=1",
            cancellationToken));
        if (employees.Length != 1 || !AttendanceAccessPolicy.CanView(user.TenVaiTro,
                user.MaNhanVien, user.MaPhongBan, targetId,
                PostgrstJson.ReadNullableInt(employees[0], "ma_phong_ban")))
            throw new InvalidOperationException("Bạn không có quyền xem bảng công này.");

        // Client checks improve feedback; Supabase RLS enforces the actual data boundary.
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var fromText = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toText = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var shifts = await LoadRowsAsync(
            "phan_ca?select=ma_phan_ca,ma_nhan_vien,ma_ca,ngay_lam_viec,bat_dau_du_kien," +
            "ket_thuc_du_kien,trang_thai,ca_lam_viec(ten_ca,gio_bat_dau,gio_ket_thuc,qua_dem)," +
            "cham_cong(ma_cham_cong,gio_check_in,gio_check_out,trang_thai)" +
            $"&ma_nhan_vien=eq.{targetId}&ngay_lam_viec=gte.{fromText}&ngay_lam_viec=lte.{toText}" +
            "&order=ma_phan_ca.asc", cancellationToken);
        var requests = await LoadRowsAsync(
            "don_tu?select=ma_don,ma_nhan_vien,ma_loai_don,tu_ngay,den_ngay,trang_thai,ngay_duyet," +
            "loai_don!inner(ten_loai_don,co_huong_luong)" +
            $"&ma_nhan_vien=eq.{targetId}&trang_thai=eq.DA_DUYET&loai_don.co_huong_luong=eq.true" +
            $"&tu_ngay=lte.{toText}&den_ngay=gte.{fromText}&order=ma_don.asc", cancellationToken);

        var employee = employees[0];
        var name = $"{PostgrstJson.ReadString(employee, "ho")} {PostgrstJson.ReadString(employee, "ten")}".Trim();
        return AttendanceSummaryCalculator.Calculate(targetId, name, from, to,
            shifts.Select(AttendanceSummaryPostgrstMapper.ToShift),
            requests.Select(AttendanceSummaryPostgrstMapper.ToLeave));
    }

    private async Task<List<JsonElement>> LoadRowsAsync(string query, CancellationToken cancellationToken)
    {
        var rows = new List<JsonElement>();
        // Continue until empty, even when the server caps a page below the requested limit.
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
