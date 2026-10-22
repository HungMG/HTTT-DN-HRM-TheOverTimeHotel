namespace OvertimeHotel.HRM.Core.Constants;

/// <summary>
/// Hai chế độ làm việc của hệ thống, dùng cho cả chức danh và ca làm việc.
///
/// <para>
/// Khách sạn 5 sao vận hành 24/7 nên phần lớn nhân sự trực ca xoay. Nhưng khối
/// văn phòng — Tổng Giám Đốc, HR, CNTT, Kế toán — làm giờ hành chính, và việc
/// xếp họ vào ca đêm hay ca sáng không phải "hơi bất thường" mà là sai nghiệp
/// vụ. Hai mã này là chiều đối chiếu để chặn chuyện đó.
/// </para>
///
/// <para>
/// Vì sao không dùng <c>bool LaHanhChinh</c>: khi có chế độ thứ ba (bán thời
/// gian, thực tập) thì mỗi nơi so sánh bool phải sửa. Mã chuỗi thêm giá trị
/// không phá chỗ đọc cũ.
/// </para>
/// </summary>
public static class ShiftWorkModes
{
    /// <summary>Ca trực 24/7 — xếp được cho mọi chức danh.</summary>
    public const string XOAY_CA = "XOAY_CA";

    /// <summary>Giờ hành chính — chỉ xếp cho nhân sự cũng có chế độ này.</summary>
    public const string HANH_CHINH = "HANH_CHINH";

    public static bool IsValid(string? value) =>
        value is XOAY_CA or HANH_CHINH;

    /// <summary>Nhãn tiếng Việt để hiển thị trên giao diện. Chuỗi lạ trả về nguyên bản.</summary>
    public static string ToDisplayName(string? value) => value switch
    {
        HANH_CHINH => "Giờ hành chính",
        XOAY_CA => "Ca xoay 24/7",
        _ => string.IsNullOrWhiteSpace(value) ? XOAY_CA : value.Trim()
    };
}
