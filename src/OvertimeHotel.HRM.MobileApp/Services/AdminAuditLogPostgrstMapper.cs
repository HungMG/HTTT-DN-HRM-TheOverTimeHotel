using System.Text.Json;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Truy vấn và ánh xạ dữ liệu lịch sử thao tác, kiểm toán hệ thống từ bảng <c>nhat_ky_quan_tri</c>.
/// </summary>
public static class AdminAuditLogPostgrstMapper
{
    private static readonly Dictionary<string, string> ActionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KHOA_SO_VA_PHAT_HANH_PHIEU_LUONG"] = "Khóa sổ lương",
        ["KHOA_SO_BANG_LUONG"] = "Khóa sổ lương",
        ["MO_KHOA_KY_LUONG"] = "Mở khóa lương",
        ["DUYET_BANG_LUONG"] = "Duyệt lương",
        ["GUI_DUYET_BANG_LUONG"] = "Gửi duyệt lương",
        ["TU_CHOI_BANG_LUONG"] = "Từ chối lương",
        ["TINH_LAI_PHIEU_LUONG"] = "Tính lại lương",
        ["TINH_LUONG_TU_DONG"] = "Tính lương",
        ["PHAT_HANH_PHIEU_LUONG"] = "Phát hành phiếu",
        ["DIEU_CHINH_LUONG"] = "Điều chỉnh lương",
        ["XOA_KHOAN_LUONG"] = "Xóa khoản lương",
        ["UPDATE_ROLE_PERMISSION"] = "Phân quyền",
        ["CREATE_ACCOUNT"] = "Cấp tài khoản",
        ["CREATE_ACCOUNT_AUTH"] = "Cấp tài khoản",
        ["UPDATE_ACCOUNT"] = "Sửa tài khoản",
        ["LOCK_ACCOUNT"] = "Khóa tài khoản",
        ["CREATE_ACCOUNT_REQUEST"] = "Yêu cầu TK",
        ["CLAIM_ACCOUNT_REQUEST"] = "Nhận xử lý TK",
        ["REJECT_ACCOUNT_REQUEST"] = "Từ chối TK",
        ["CREATE_PROMOTION_REQUEST"] = "Đề xuất thăng chức",
        ["PROMOTE_ACCOUNT"] = "Thăng chức",
        ["UPGRADE_ACCOUNT_ROLE"] = "Nâng vai trò",
        ["UPDATE_CONTRACT_PROMOTION"] = "Cập nhật HĐ",
        ["DUYET_DON_TU"] = "Duyệt đơn",
        ["DUYET_DOI_CA"] = "Đổi ca",
        ["SUA_CONG_GIAI_TRINH"] = "Giải trình công",
        ["TAO_SU_KIEN"] = "Tạo sự kiện",
        ["PHAN_CONG_SU_KIEN"] = "Phân công SK",
        ["NGHIEM_THU_SU_KIEN"] = "Nghiệm thu SK",
        ["DANH_GIA_SU_KIEN"] = "Đánh giá SK",
        ["HUY_SU_KIEN"] = "Hủy sự kiện",
        ["TU_DONG_HUY_SU_KIEN"] = "Hủy sự kiện",
        ["TU_DONG_BAT_DAU_SU_KIEN"] = "Bắt đầu SK",
        ["XAC_NHAN_CONG_SU_KIEN"] = "Chốt công SK",
        ["TRANG_THAI_SU_KIEN"] = "Trạng thái SK",
        ["TAO_DIA_DIEM"] = "Tạo địa điểm",
    };

    /// <summary>
    /// Lấy danh sách nhật ký thao tác và kiểm toán hệ thống trong <paramref name="withinDays"/> ngày gần đây.
    /// </summary>
    public static async Task<IReadOnlyList<AdminAuditLogItem>> FetchRecentAsync(
        SupabaseRestClient restClient,
        int withinDays,
        int limit)
    {
        // Neo mốc 00:00 giờ Việt Nam (+07:00) của withinDays ngày trước rồi quy đổi sang UTC để lọc chính xác
        var cutoffLocal = new DateTimeOffset(DateTime.Today.AddDays(-withinDays), TimeSpan.FromHours(7));
        var cutoffUtc = cutoffLocal.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var json = await restClient.GetAsync(
            "nhat_ky_quan_tri?select=ma_nhat_ky,hanh_dong,noi_dung,ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,thoi_gian" +
            $"&thoi_gian=gte.{cutoffUtc}&order=thoi_gian.desc&limit={limit}");

        var rows = PostgrstJson.ParseArray(json);
        var items = new List<AdminAuditLogItem>(rows.Length);
        foreach (var row in rows)
        {
            items.Add(Parse(row));
        }

        return items;
    }

    public static AdminAuditLogItem Parse(JsonElement row)
    {
        var id = row.TryGetProperty("ma_nhat_ky", out var idProp) && idProp.TryGetInt64(out var parsedId)
            ? parsedId
            : 0L;
        var actionCode = PostgrstJson.ReadString(row, "hanh_dong");
        var description = PostgrstJson.ReadString(row, "noi_dung");
        var actor = PostgrstJson.ReadString(row, "ten_nguoi_thuc_hien");
        var target = PostgrstJson.ReadString(row, "ten_tai_khoan_bi_tac_dong");
        var occurredAt = PostgrstJson.ReadNullableTimestamp(row, "thoi_gian") ?? DateTimeOffset.Now;

        return new AdminAuditLogItem
        {
            AuditLogId = id,
            ActionCode = actionCode,
            ActionName = FormatActionName(actionCode),
            Description = string.IsNullOrWhiteSpace(description) ? actionCode : description,
            ActorName = string.IsNullOrWhiteSpace(actor) ? "Hệ thống" : actor,
            TargetUsername = string.IsNullOrWhiteSpace(target) ? "Hệ thống" : target,
            OccurredAt = occurredAt
        };
    }

    private static string FormatActionName(string? rawAction)
    {
        if (string.IsNullOrWhiteSpace(rawAction))
        {
            return "Hệ thống";
        }

        if (ActionLabels.TryGetValue(rawAction.Trim(), out var label))
        {
            return label;
        }

        return rawAction.Replace('_', ' ');
    }
}
