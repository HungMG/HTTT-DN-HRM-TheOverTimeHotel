using System.Globalization;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Đọc an toàn các giá trị trong JSON trả về từ PostgREST.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao phải có lớp này.</b> PostgREST dùng <c>snake_case</c> cho tên cột
/// và kiểu <c>numeric</c> trả về JSON dạng CHUỖI ("3.50") chứ không phải số. Đọc
/// tay bằng <c>JsonElement</c> ở từng chỗ dễ quên một trong hai điều đó, nên tập
/// trung lại một chỗ và cho nó trả luôn giá trị mặc định khi thiếu.
/// </para>
///
/// <para>
/// <b>Không dùng <c>JsonSerializer</c> cho các bảng này.</b> DTO dùng tên thuộc
/// tính PascalCase trong khi PostgREST trả <c>ma_don</c>, <c>trang_thai</c>…; ép
/// qua <c>[JsonPropertyName]</c> cho mọi cột làm bảng DTO phình to và dễ lệch
/// với CSDL mỗi lần thêm cột. Ở đây đọc theo tên cột là rõ và bền hơn.
/// </para>
/// </remarks>
public static class PostgrstJson
{
    /// <summary>
    /// Tách mảng JSON thành danh sách element độc lập.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bắt buộc Clone.</b> <see cref="JsonElement"/> là struct chỉ giữ <i>con trỏ
    /// vào bộ đệm của JsonDocument</i>, chứ không tự mang nội dung. Toán tử
    /// <c>[.. doc.RootElement.EnumerateArray()]</c> chỉ copy con trỏ đó ra mảng
    /// mới, nên khi <c>using</c> Dispose tài liệu, toàn bộ element đã trả v�� hoá
    /// thành con trỏ treo và lần đọc kế tiếp ném <c>ObjectDisposedException</c>.
    /// </para>
    /// <para>
    /// Bỏ Clone ở đây từng làm đăng nhập chết ở bước đọc hồ sơ: token đã lấy được,
    /// PostgREST đã trả 200, nhưng hàm đọc hồ sơ không có try/catch nên lỗi nổi
    /// thành thông báo chung — hoàn toàn giống lỗi mạng, rất khó chẩn đoán.
    /// </para>
    /// </remarks>
    public static JsonElement[] ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<JsonElement>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                result.Add(item.Clone());
            }

            return [.. result];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Đọc cột chuỗi, trả chuỗi rỗng khi thiếu hoặc không phải chuỗi.</summary>
    public static string ReadString(JsonElement element, string column) =>
        element.TryGetProperty(column, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Đọc cột chuỗi, trả null khi thiếu — dùng cho cột nullable.</summary>
    public static string? ReadNullableString(JsonElement element, string column) =>
        element.TryGetProperty(column, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    /// <summary>Đọc cột số nguyên, trả null khi thiếu (PostgREST trả null cho LEFT JOIN).</summary>
    public static int? ReadNullableInt(JsonElement element, string column) =>
        element.TryGetProperty(column, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : null;

    /// <summary>Đọc cột ngày kiểu DATE (chuỗi "yyyy-MM-dd").</summary>
    public static DateOnly ReadDate(JsonElement element, string column)
    {
        var raw = ReadString(element, column);
        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : default;
    }

    /// <summary>Đọc cột ngày kiểu DATE có thể null (chuỗi "yyyy-MM-dd").</summary>
    public static DateOnly? ReadNullableDate(JsonElement element, string column)
    {
        var raw = ReadNullableString(element, column);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;
    }

    /// <summary>Đọc cột thời gian kiểu TIMESTAMPTZ (ISO 8601 kèm offset).</summary>
    public static DateTimeOffset? ReadNullableTimestamp(JsonElement element, string column)
    {
        var raw = ReadNullableString(element, column);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    /// <summary>Đọc cột TIMESTAMPTZ có giá trị, trả default khi thiếu.</summary>
    public static DateTimeOffset ReadTimestamp(JsonElement element, string column) =>
        ReadNullableTimestamp(element, column) ?? default;

    /// <summary>Đọc cột BOOLEAN, trả giá trị dự phòng khi thiếu.</summary>
    public static bool ReadBool(JsonElement element, string column, bool fallback = false) =>
        element.TryGetProperty(column, out var prop)
            ? prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                // Một số cột boolean PostgREST trả dạng chuỗi "t"/"f".
                JsonValueKind.String => prop.GetString() switch
                {
                    "t" or "true" or "TRUE" => true,
                    "f" or "false" or "FALSE" => false,
                    _ => fallback
                },
                _ => fallback
            }
            : fallback;

    /// <summary>
    /// Đọc cột kiểu NUMERIC của PostgreSQL. PostgREST trả số này dưới dạng
    /// CHUỖI ("3.50") vì JSON không có kiểu decimal — đọc bằng GetDecimal() sẽ ném.
    /// </summary>
    public static decimal ReadDecimal(JsonElement element, string column, decimal fallback = 0m)
    {
        if (!element.TryGetProperty(column, out var prop))
        {
            return fallback;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(
                prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback
        };
    }

    /// <summary>
    /// Đọc quan hệ nhúng PostgREST (vd <c>phong_ban(ten_phong_ban)</c>).
    /// </summary>
    /// <remarks>
    /// Quan hệ n-1 trả về OBJECT: <c>"phong_ban": {"ten_phong_ban": …}</c> — hoặc
    /// null khi nhân viên chưa được gán phòng. Vẫn xử lý nốt dạng mảng vì cùng một
    /// cấu trúc được dùng khi nhúng qua bảng nối 1-n; thiếu nhánh này thì phiếu
    /// lương sẽ hiện trống phòng ban dù API trả về đúng.
    /// </remarks>
    public static JsonElement? ReadEmbedded(JsonElement element, string relation)
    {
        if (!element.TryGetProperty(relation, out var prop))
        {
            // Dự phòng chuyển đổi qua lại giữa camelCase và snake_case (vd chamCong <-> cham_cong)
            var alternate = relation.Contains('_')
                ? string.Concat(relation.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select((w, i) => i == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]))
                : string.Concat(relation.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

            if (!element.TryGetProperty(alternate, out prop))
            {
                return null;
            }
        }

        if (prop.ValueKind == JsonValueKind.Object)
        {
            return prop.Clone();
        }

        if (prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    return item.Clone();
                }
            }
        }

        return null;
    }

    /// <summary>Đọc một cột bên trong quan hệ nhúng, trả chuỗi rỗng khi không có.</summary>
    public static string ReadEmbeddedString(JsonElement element, string relation, string column) =>
        ReadEmbedded(element, relation) is { } embedded
            ? ReadString(embedded, column)
            : string.Empty;

    /// <summary>
    /// Đọc một cột nằm trong quan hệ nhúng NẰM TRONG quan hệ nhúng khác.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cần hàm riêng vì <see cref="ReadEmbeddedString"/> chỉ đi đúng MỘT tầng.
    /// Trường hợp thật của màn duyệt đơn là hai tầng: PostgREST trả
    /// <c>nv.chuc_vu = {"ten_chuc_vu": …}</c> lồng trong <c>nv = {…}</c>, tức
    /// <c>nv.chuc_vu.ten_chuc_vu</c>. Gọi <c>ReadEmbeddedString(row, "nv", "chuc_vu")</c>
    /// sẽ đọc nhầm sang khóa con <c>chuc_vu</c> — vốn là OBJECT, không phải chuỗi
    /// — nên trả về rỗng, và thẻ hiện "Chức vụ:" trống trong khi dữ liệu CSDL
    /// hoàn toàn đúng.
    /// </para>
    /// <para>
    /// <b>Bẫy thứ hai, cùng một chỗ.</b> JSON do PostgREST sinh ra là object LỒNG
    /// THẬT, không có khóa phẳng mang dấu chấm. <c>TryGetProperty("nv.chuc_vu")</c>
    /// vì thế luôn trả false và hàm trả rỗng — im lặng, không báo lỗi, chỉ biểu
    /// hiện bằng nhãn trống trên thẻ. Vì vậy <paramref name="path"/> được tách theo
    /// dấu chấm rồi đi từng tầng một, chứ không dùng làm tên khóa trực tiếp.
    /// </para>
    /// <para>
    /// Nhận <paramref name="path"/> là chuỗi nhiều tầng phân tách bằng dấu chấm
    /// ("nv.chuc_vu") thay vì tham số varargs: biến thể <c>params string[]</c> sẽ
    /// dễ bị gọi nhầm với bản một tầng hơn là một lời gọi có tên rõ ràng.
    /// </para>
    /// </remarks>
    public static string ReadNestedString(JsonElement element, string path, string column)
    {
        // "nv.chuc_vu" → đi nv, rồi chuc_vu. Mỗi tầng phải là OBJECT; LEFT JOIN
        // không khớp thì PostgREST trả null và ta trả rỗng ngay tại đó.
        var current = element;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!current.TryGetProperty(segment, out var prop) || prop.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            current = prop;
        }

        return ReadString(current, column);
    }

    /// <summary>Đọc một cột số bên trong quan hệ nhúng, trả null khi không có.</summary>
    public static int? ReadEmbeddedInt(JsonElement element, string relation, string column) =>
        ReadEmbedded(element, relation) is { } embedded
            ? ReadNullableInt(embedded, column)
            : null;
}