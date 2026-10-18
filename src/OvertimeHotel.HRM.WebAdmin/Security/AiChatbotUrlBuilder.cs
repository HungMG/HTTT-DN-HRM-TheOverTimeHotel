using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Dựng địa chỉ nhúng khung chat Trợ lý AI, kèm ngữ cảnh trang và vé dữ liệu.
///
/// <para>
/// <b>Vì sao phải gom về một chỗ:</b> khung chat được nhúng ở hai nơi — drawer nổi trong
/// <c>Views/Shared/_Layout.cshtml</c> (xuất hiện ở mọi trang) và trang riêng
/// <c>Views/AiAssistant/Index.cshtml</c>. Nếu mỗi nơi tự ghép chuỗi truy vấn thì chỉ cần
/// sửa một bên là hai bên lệch nhau ngay, và vé dữ liệu sẽ có lúc được truyền, lúc không.
/// </para>
///
/// <para>
/// <b>Vé nằm ở phần fragment (<c>#bt=</c>), không phải query string.</b> Fragment không được
/// trình duyệt gửi lên máy chủ Next.js, nên vé không lọt vào access log của máy chủ đó và
/// không bị mang theo qua header <c>Referer</c> khi người dùng bấm liên kết ra ngoài.
/// </para>
/// </summary>
public sealed class AiChatbotUrlBuilder
{
    private readonly IConfiguration _configuration;
    private readonly AiContextTokenService _tokenService;

    public AiChatbotUrlBuilder(IConfiguration configuration, AiContextTokenService tokenService)
    {
        _configuration = configuration;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Địa chỉ trang chatbot khi mở ở tab riêng hoặc khi chưa có phiên đăng nhập.
    /// </summary>
    public string GetBaseUrl() => _configuration["Chatbot:Url"] ?? "http://localhost:3000";

    /// <summary>
    /// Dựng địa chỉ nhúng khung chat cho phiên đăng nhập hiện tại.
    /// </summary>
    /// <param name="user">Principal của phiên đăng nhập Web Admin.</param>
    /// <param name="controller">Tên controller của trang đang mở, làm ngữ cảnh cho bot.</param>
    /// <param name="action">Tên action của trang đang mở.</param>
    /// <param name="query">Chuỗi truy vấn hiện tại của trang, để bot biết đang xem kỳ báo cáo nào.</param>
    /// <returns>Địa chỉ dạng <c>{chatbotUrl}?embed=true&amp;page=...&amp;tab=...#bt={v&#233;}</c>.</returns>
    public string BuildEmbedUrl(ClaimsPrincipal user, string? controller, string? action, IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = new List<string> { "embed=true" };

        if (!string.IsNullOrWhiteSpace(controller))
        {
            parameters.Add($"page={Uri.EscapeDataString(controller)}");
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            parameters.Add($"action={Uri.EscapeDataString(action)}");
        }

        // Tab đang xem là ngữ cảnh quan trọng nhất trên trang Báo cáo & Thống kê:
        // đó là thứ quyết định "con số này" trong câu hỏi của người dùng đang trỏ tới đâu.
        if (query.TryGetValue("tab", out var tab) && !string.IsNullOrWhiteSpace(tab))
        {
            parameters.Add($"tab={Uri.EscapeDataString(tab.ToString())}");
        }

        // Nhãn chữ cho mô hình đọc, và các tham số SỐ để chatbot gửi ngược về /api/ai-data/digest.
        // Phải có cả hai: trang /BaoCao nhận kỳ dưới dạng số (nam/thang/quy), còn phần dữ liệu
        // chỉ nhận nhãn thì không dựng lại được kỳ đang xem và luôn rơi về cả năm.
        var period = BuildPeriodLabel(query);
        if (!string.IsNullOrWhiteSpace(period))
        {
            parameters.Add($"period={Uri.EscapeDataString(period)}");
        }

        AddNumericPeriod(query, parameters, "nam");
        AddNumericPeriod(query, parameters, "thang");
        AddNumericPeriod(query, parameters, "quy");

        var url = $"{GetBaseUrl()}?{string.Join("&", parameters)}";

        // Ký vé riêng cho lần nhúng này. Vé chỉ để ĐỌC số liệu tổng hợp, sống 10 phút,
        // và dùng được nhiều lần trong khoảng đó — xem AiContextTokenService để biết lý do.
        try
        {
            var token = _tokenService.Issue(user);
            url += $"#bt={token}";
        }
        catch (InvalidOperationException)
        {
            // Chưa cấu hình khoá ký: vẫn nhúng khung chat để hỏi đáp tài liệu PDF,
            // chỉ mất phần hỏi số liệu. Cố ý không chặn cả trang.
        }

        return url;
    }

    /// <summary>
    /// Ghép nhãn kỳ báo cáo từ các tham số lọc đang có trên địa chỉ trang.
    /// Trả chuỗi rỗng khi trang không có bộ lọc kỳ nào (phần lớn các trang).
    /// </summary>
    private static string BuildPeriodLabel(IQueryCollection query)
    {
        var parts = new List<string>();

        void Add(string key, string prefix)
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{prefix} {value.ToString().Trim()}");
            }
        }

        Add("thang", "Tháng");
        Add("quy", "Quý");
        Add("nam", "Năm");

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    /// <summary>
    /// Chuyển một tham số kỳ dạng số từ địa chỉ trang sang địa chỉ khung chat.
    ///
    /// <para>
    /// Chỉ nhận giá trị số hợp lệ. Giá trị lạ (chữ, số âm, số quá lớn) bị bỏ qua thay vì
    /// chuyển tiếp nguyên văn: phía chatbot sẽ gửi thẳng con số này vào truy vấn báo cáo, nên
    /// chặn ở đây rẻ hơn nhiều so với việc để nó đi tới tận tầng dữ liệu.
    /// </para>
    /// </summary>
    private static void AddNumericPeriod(IQueryCollection query, List<string> parameters, string key)
    {
        if (!query.TryGetValue(key, out var raw) || !int.TryParse(raw.ToString(), out var value))
        {
            return;
        }

        (int Min, int Max)? range = key switch
        {
            "nam" => (2000, 2100),
            "thang" => (1, 12),
            "quy" => (1, 4),
            _ => null
        };

        if (range is not { } bounds || value < bounds.Min || value > bounds.Max)
        {
            return;
        }

        parameters.Add($"{key}={value}");
    }
}
