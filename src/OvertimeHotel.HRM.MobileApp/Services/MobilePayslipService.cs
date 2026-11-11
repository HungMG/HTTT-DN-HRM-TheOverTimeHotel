using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Services;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Đọc phiếu lương của người đang đăng nhập từ Supabase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao phải embed KY_LUONG.</b> Bảng <c>phieu_luong</c> không có cột
/// tháng/năm — kỳ lương nằm ở <c>ma_ky_luong</c>. PostgREST chỉ embed được quan hệ
/// mà chính người gọi cũng được phép đọc, nên không có policy mở <c>ky_luong</c>
/// cho nhân viên thì app không lấy được nhãn "Tháng 09/2026". Migration
/// 20261010_mo_ky_luong_cho_chu_phieu.sql mở policy đó; bảng ấy không chứa cột
/// tiền nào nên không rò rỉ lương.
/// </para>
/// <para>
/// <b>Đọc decimal.</b> Mọi cột <c>DECIMAL</c> của PostgreSQL được PostgREST trả
/// về dưới dạng CHUỖI ("3.50") vì JSON không có kiểu decimal — đọc bằng
/// <see cref="PostgrstJson.ReadDecimal"/> là bắt buộc, không tự parse tay.
/// </para>
/// </remarks>
public sealed class MobilePayslipService(SupabaseRestClient rest, IAuthService authService) : IPayslipService
{
    public bool IsDemoMode => false;

    public async Task<IReadOnlyList<PayslipSummaryDto>> GetPayslipsAsync(
        CancellationToken cancellationToken = default)
    {
        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true || user.MaNhanVien <= 0)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var rows = await LoadRowsAsync(
            "phieu_luong?select=ma_phieu_luong,ma_nhan_vien,luong_co_ban,ngay_cong_thuc_te," +
            "tien_luong_cong,tien_tang_ca,tong_phu_cap,tong_thuong,tong_khau_tru,thuc_nhan," +
            $"trang_thai,ky_luong(thang,nam,trang_thai,han_phan_hoi_nhap)&ma_nhan_vien=eq.{user.MaNhanVien}" +
            "&order=ma_phieu_luong.desc",
            cancellationToken);

        var name = await ReadEmployeeNameAsync(user.MaNhanVien, cancellationToken);
        var dept = user.TenPhongBan ?? string.Empty;
        var role = user.TenChucVu ?? string.Empty;

        return rows
            .Where(IsPayslipPublishedToEmployee)
            .Select(row => ToSummary(row, name, dept, role))
            .ToArray();
    }

    public async Task<IReadOnlyList<PayslipItemDto>> GetPayslipItemsAsync(int maPhieuLuong,
        CancellationToken cancellationToken = default)
    {
        if (maPhieuLuong <= 0) throw new InvalidOperationException("Mã phiếu lương không hợp lệ.");

        var user = authService.CurrentUser;
        if (user?.IsLoggedIn != true || user.MaNhanVien <= 0)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var rows = await LoadRowsAsync(
            "chi_tiet_phieu_luong?select=ma_chi_tiet,loai_khoan,loai_khoan_luu,ten_khoan_luu,so_tien,ghi_chu" +
            $"&ma_phieu_luong=eq.{maPhieuLuong}&order=loai_khoan.asc,ma_chi_tiet.asc",
            cancellationToken);

        return rows.Select(row =>
        {
            var loai = PostgrstJson.ReadString(row, "loai_khoan");
            if (string.IsNullOrWhiteSpace(loai))
                loai = PostgrstJson.ReadString(row, "loai_khoan_luu");

            return new PayslipItemDto(
                PostgrstJson.ReadNullableInt(row, "ma_chi_tiet") ?? 0,
                PostgrstJson.ReadString(row, "ten_khoan_luu"),
                loai,
                PostgrstJson.ReadDecimal(row, "so_tien"),
                PostgrstJson.ReadNullableString(row, "ghi_chu"));
        }).ToArray();
    }

    /// <summary>
    /// Kiểm tra phiếu lương đã được duyệt/khóa sổ hoặc phát hành bản nháp để hiển thị cho nhân viên.
    /// Nhân viên thấy phiếu lương khi:
    /// - Phiếu đã được phát hành chính thức (trang_thai == "DA_THANH_TOAN")
    /// - Hoặc kỳ lương đã được Ban Giám Đốc phê duyệt và khóa sổ (ky_luong.trang_thai == "KHOA_SO")
    /// - Hoặc kỳ lương đang ở giai đoạn phát hành bản nháp cho nhân viên kiểm tra (ky_luong.trang_thai == "CHO_PHAN_HOI")
    /// </summary>
    private static bool IsPayslipPublishedToEmployee(JsonElement row)
    {
        var slipStatus = PostgrstJson.ReadString(row, "trang_thai");
        if (string.Equals(slipStatus, "DA_THANH_TOAN", StringComparison.OrdinalIgnoreCase))
            return true;

        var period = PostgrstJson.ReadEmbedded(row, "ky_luong");
        if (period is { } p && p.TryGetProperty("trang_thai", out var pStatus))
        {
            var periodStatus = pStatus.GetString();
            if (string.Equals(periodStatus, "CHO_PHAN_HOI", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(periodStatus, "KHOA_SO", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(slipStatus, "DA_DUYET", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(slipStatus, "DA_THANH_TOAN", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static PayslipSummaryDto ToSummary(JsonElement row, string employeeName, string dept, string role)
    {
        var period = PostgrstJson.ReadEmbedded(row, "ky_luong");
        var periodStatus = period is { } p && p.TryGetProperty("trang_thai", out var pStatus) ? pStatus.GetString() : null;
        var isDraft = string.Equals(periodStatus, "CHO_PHAN_HOI", StringComparison.OrdinalIgnoreCase);

        DateTimeOffset? hanPhanHoi = null;
        if (period is { } pObj && pObj.TryGetProperty("han_phan_hoi_nhap", out var hVal) && hVal.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(hVal.GetString(), out var dt))
            {
                hanPhanHoi = dt;
            }
        }

        return new PayslipSummaryDto(
            PostgrstJson.ReadNullableInt(row, "ma_phieu_luong") ?? 0,
            PostgrstJson.ReadNullableInt(row, "ma_nhan_vien") ?? 0,
            employeeName,
            dept,
            role,
            period is { } value && value.TryGetProperty("thang", out var month) &&
                month.ValueKind == System.Text.Json.JsonValueKind.Number ? month.GetInt32() : 0,
            period is { } named && named.TryGetProperty("nam", out var year) &&
                year.ValueKind == System.Text.Json.JsonValueKind.Number ? year.GetInt32() : 0,
            PostgrstJson.ReadDecimal(row, "luong_co_ban"),
            PostgrstJson.ReadDecimal(row, "ngay_cong_thuc_te"),
            PostgrstJson.ReadDecimal(row, "tien_luong_cong"),
            PostgrstJson.ReadDecimal(row, "tien_tang_ca"),
            PostgrstJson.ReadDecimal(row, "tong_phu_cap"),
            PostgrstJson.ReadDecimal(row, "tong_thuong"),
            PostgrstJson.ReadDecimal(row, "tong_khau_tru"),
            PostgrstJson.ReadDecimal(row, "thuc_nhan"),
            PostgrstJson.ReadString(row, "trang_thai"),
            isDraft,
            hanPhanHoi);
    }

    private async Task<string> ReadEmployeeNameAsync(int employeeId, CancellationToken cancellationToken)
    {
        var rows = PostgrstJson.ParseArray(await rest.GetAsync(
            $"nhan_vien?select=ho,ten&ma_nhan_vien=eq.{employeeId}&limit=1", cancellationToken));
        if (rows.Length != 1) return string.Empty;
        return $"{PostgrstJson.ReadString(rows[0], "ho")} {PostgrstJson.ReadString(rows[0], "ten")}".Trim();
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
