using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Phân hệ Trợ lý AI Khách sạn The OverTime Hotel (Hỏi đáp quy chế nhân sự, tính lương, tra cứu và đọc hiểu tài liệu PDF nội bộ).
/// </summary>
[Authorize]
public class AiAssistantController : Controller
{
    private readonly IConfiguration _configuration;

    public AiAssistantController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var chatbotUrl = _configuration["Chatbot:Url"] ?? "http://localhost:3000";
        ViewBag.ChatbotUrl = chatbotUrl;
        return View();
    }
}
