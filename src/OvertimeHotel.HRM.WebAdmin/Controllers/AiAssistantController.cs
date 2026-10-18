using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Phân hệ Trợ lý AI Khách sạn The OverTime Hotel (Hỏi đáp quy chế nhân sự, tính lương, tra cứu và đọc hiểu tài liệu PDF nội bộ).
/// </summary>
[Authorize]
public class AiAssistantController : Controller
{
    private readonly AiChatbotUrlBuilder _urlBuilder;

    public AiAssistantController(AiChatbotUrlBuilder urlBuilder)
    {
        _urlBuilder = urlBuilder;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // Địa chỉ cơ bản để hiển thị và mở ở tab riêng (không kèm vé).
        ViewBag.ChatbotUrl = _urlBuilder.GetBaseUrl();

        // Địa chỉ nhúng: kèm ngữ cảnh trang và vé dữ liệu ngắn hạn. Dùng chung một
        // bộ dựng với drawer nổi ở _Layout.cshtml để hai nơi không lệch nhau.
        ViewBag.ChatbotEmbedUrl = _urlBuilder.BuildEmbedUrl(
            User,
            RouteData.Values["controller"]?.ToString(),
            RouteData.Values["action"]?.ToString(),
            Request.Query);

        return View();
    }
}
