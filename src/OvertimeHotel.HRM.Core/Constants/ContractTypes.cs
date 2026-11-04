namespace OvertimeHotel.HRM.Core.Constants;

/// <summary>
/// Bốn mã loại hợp đồng lao động của hệ thống, cùng cách đọc/chuẩn hoá chúng.
///
/// <para>
/// Trước file này, ba nơi mỗi nơi tự đoán một kiểu: form ghi ra mã, seed ghi ra
/// câu tiếng Việt ("HĐLĐ Xác định thời hạn (3 năm)"), JS so bằng
/// <c>includes('3 năm')</c>, còn DonTu lại so tuyệt đối với một chuỗi thứ tư
/// không tồn tại ("KHONG_XAC_DINH"). Hệ quả là hợp đồng không xác định thời hạn
/// của Tổng Giám Đốc bị đọc thành hợp đồng xác định thời hạn, kéo theo số ngày
/// báo trước thôi việc còn 30 thay vì 45 theo Điều 35 BLLĐ.
/// </para>
/// <para>
/// Chuẩn duy nhất từ nay là các mã dưới đây. <see cref="Normalize"/> nhận cả mã
/// lẫn câu tiếng Việt cũ để nâng dần dữ liệu cũ về chuẩn.
/// </para>
/// </summary>
public static class ContractTypes
{
    /// <summary>Hợp đồng thử việc — tách riêng bằng hinh_thuc_hop_dong = THU_VIEC.</summary>
    public const string THU_VIEC = "THU_VIEC";

    /// <summary>Xác định thời hạn 1 năm.</summary>
    public const string XAC_DINH_THOI_HAN_1_NAM = "XAC_DINH_THOI_HAN_1_NAM";

    /// <summary>Xác định thời hạn 3 năm.</summary>
    public const string XAC_DINH_THOI_HAN_3_NAM = "XAC_DINH_THOI_HAN_3_NAM";

    /// <summary>Không xác định thời hạn — báo trước thôi việc 45 ngày.</summary>
    public const string KHONG_XAC_DINH_THOI_HAN = "KHONG_XAC_DINH_THOI_HAN";

    private static readonly Dictionary<string, string> NhanTiengViet = new()
    {
        [THU_VIEC] = "Hợp đồng thử việc",
        [XAC_DINH_THOI_HAN_1_NAM] = "HĐ lao động 01 năm",
        [XAC_DINH_THOI_HAN_3_NAM] = "HĐ lao động 03 năm",
        [KHONG_XAC_DINH_THOI_HAN] = "HĐ lao động không xác định thời hạn"
    };

    /// <summary>
    /// Đưa một chuỗi <c>loai_hop_dong</c> bất kỳ (mã chuẩn, mã cũ, hoặc câu
    /// tiếng Việt do seed nhập tay) về một trong bốn mã chuẩn.
    /// Trả về <c>null</c> khi không nhận ra — người gọi phải quyết định, không
    /// được lặng lẽ mặc định thành "xác định thời hạn".
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();

        // So khớp mã trước, để "XAC_DINH_THOI_HAN_1_NAM" không bị nhánh
        // KHONG_XAC_DINH của nhánh sau bắt nhầm.
        if (v.Equals(THU_VIEC, StringComparison.OrdinalIgnoreCase)) return THU_VIEC;
        if (v.Equals(XAC_DINH_THOI_HAN_1_NAM, StringComparison.OrdinalIgnoreCase)) return XAC_DINH_THOI_HAN_1_NAM;
        if (v.Equals(XAC_DINH_THOI_HAN_3_NAM, StringComparison.OrdinalIgnoreCase)) return XAC_DINH_THOI_HAN_3_NAM;
        if (v.Equals(KHONG_XAC_DINH_THOI_HAN, StringComparison.OrdinalIgnoreCase)) return KHONG_XAC_DINH_THOI_HAN;

        // Rồi tới câu tiếng Việt do seed cũ nhập tay. Phải kiểm "không xác định"
        // trước "3 năm"/"1 năm" vì câu "HĐLĐ Không xác định thời hạn" không chứa
        // số năm nào — nhưng thứ tự này còn bảo vệ trường hợp câu lai sau này.
        if (v.Contains("không xác định", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("KHONG_XAC_DINH", StringComparison.OrdinalIgnoreCase))
            return KHONG_XAC_DINH_THOI_HAN;

        if (v.Contains("03 năm", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("3 năm", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("3_NAM", StringComparison.OrdinalIgnoreCase))
            return XAC_DINH_THOI_HAN_3_NAM;

        // Nhãn hiển thị dùng dạng hai chữ số ("01 năm"), còn người dùng thường gõ
        // "1 năm" — phải kiểm cả hai dạng, nếu không tìm kiếm theo loại sẽ ra 0 kết quả
        // trong khi trang Quản lý Hợp đồng hiển thị nhãn "01 năm" cho cùng loại này.
        if (v.Contains("01 năm", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("1 năm", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("1_NAM", StringComparison.OrdinalIgnoreCase))
            return XAC_DINH_THOI_HAN_1_NAM;

        if (v.Contains("thử việc", StringComparison.OrdinalIgnoreCase))
            return THU_VIEC;

        // "XAC_DINH_THOI_HAN" trần (không kèm số năm) từng xuất hiện trong dữ liệu.
        // Nó không nói được là 1 năm hay 3 năm, nên trả null thay vì đoán bừa.
        return null;
    }

    /// <summary>Nhãn tiếng Việt để hiển thị trên UI. Chuỗi lạ thì trả về nguyên bản.</summary>
    public static string ToDisplayName(string? value)
    {
        var code = Normalize(value);
        if (code != null && NhanTiengViet.TryGetValue(code, out var ten)) return ten;
        return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }

    /// <summary>
    /// Các mã khớp với một từ khoá tìm kiếm, tra theo cả mã lẫn nhãn tiếng Việt.
    ///
    /// <para>
    /// Cần hàm này vì sau khi chuẩn hoá, cột <c>loai_hop_dong</c> chỉ còn mã
    /// ("XAC_DINH_THOI_HAN_1_NAM"). Người dùng gõ "1 năm" vào ô tìm kiếm hợp
    /// đồng thì <c>LIKE '%1 năm%'</c> trên cột thô không khớp gì nữa.
    /// </para>
    /// <para>
    /// Dùng ở phía C# để lấy danh sách mã rồi đẩy vào EF dưới dạng
    /// <c>list.Contains(cột)</c> — dịch được thành <c>= ANY(...)</c> của
    /// PostgreSQL, khác với gọi thẳng <see cref="ToDisplayName"/> trong LINQ.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> MatchingCodes(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<string>();
        var k = keyword.Trim();
        var normalized = Normalize(k);
        var matched = NhanTiengViet
            .Where(pair =>
                pair.Value.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                (normalized != null &&
                    (pair.Key == normalized ||
                        pair.Value.Contains(NormalizeLabel(normalized) ?? string.Empty, StringComparison.OrdinalIgnoreCase))))
            .Select(pair => pair.Key)
            .ToList();
        return matched;
    }

    /// <summary>Định dạng số năm hai chữ số như nhãn hiển thị: "1 năm" → "01 năm", "3 năm" → "03 năm".</summary>
    private static string? NormalizeLabel(string code) => code switch
    {
        XAC_DINH_THOI_HAN_1_NAM => "01 năm",
        XAC_DINH_THOI_HAN_3_NAM => "03 năm",
        _ => null
    };

    /// <summary>
    /// Hợp đồng không xác định thời hạn hay không — dùng cho mức báo trước
    /// thôi việc 45 ngày (Điều 35 BLLĐ 2019). Đọc qua <see cref="Normalize"/>
    /// nên nhận được cả mã mới lẫn câu tiếng Việt cũ.
    /// </summary>
    public static bool IsIndefinite(string? value) =>
        Normalize(value) == KHONG_XAC_DINH_THOI_HAN;

    /// <summary>Thời hạn điển hình theo tháng, dùng khi gia hạn. Vô thời hạn trả null.</summary>
    public static int? DurationMonths(string? value) => Normalize(value) switch
    {
        XAC_DINH_THOI_HAN_1_NAM => 12,
        XAC_DINH_THOI_HAN_3_NAM => 36,
        THU_VIEC => 2,
        _ => null
    };
}
