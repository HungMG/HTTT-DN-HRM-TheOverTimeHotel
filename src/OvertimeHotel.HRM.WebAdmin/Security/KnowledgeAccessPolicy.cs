using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Luật phân quyền kho tri thức của Trợ lý AI — tài liệu PDF (quy chế, sổ tay, hợp đồng mẫu).
///
/// <para>
/// <b>Hai câu hỏi khác nhau, hai hàm khác nhau:</b> <i>được GHI vào kho</i> và <i>được ĐỌC những
/// phạm vi nào</i>. Gộp chúng lại sẽ khiến một Quản lý bộ phận — người được đọc tài liệu của
/// phòng mình — vô tình có quyền đăng văn bản toàn khách sạn lên kho dùng chung.
/// </para>
///
/// <para>
/// <b>Nguyên tắc Fail-Closed:</b> vai trò lạ, claim thiếu, hoặc dữ liệu không đọc được đều rơi
/// về phạm vi hẹp nhất (<see cref="ScopeAll"/>) chứ không mở rộng. Trả về "không thấy tài liệu
/// nào" thà chấp nhận được hơn là để lọt một quy chế lương ra ngoài phòng ban.
/// </para>
///
/// <para>
/// <b>Vì sao mã phạm vi là chuỗi chứ không phải enum:</b> giá trị này đi qua JSON sang tiến trình
/// Node (<c>docScopes</c>), nơi enum C# không tồn tại. Một quy ước chuỗi chặt — chỉ <c>*</c>,
/// <c>all</c> và <c>dept:&lt;số&gt;</c> — vừa qua được ranh giới tiến trình, vừa kiểm được bằng
/// biểu thức chính quy ở phía nhận.
/// </para>
/// </summary>
public static class KnowledgeAccessPolicy
{
    /// <summary>Phạm vi "toàn bộ kho tri thức" — chỉ vai trò quản trị được nhận.</summary>
    /// <remarks>
    /// Ký tự <c>*</c> làm phía nhận hiểu là "không lọc gì cả", nên nó <b>chỉ được trả về từ
    /// đây</b>, không bao giờ được nhận từ trình duyệt. Xem <c>frontend/app/api/chat/route.ts</c>:
    /// route đó tự gọi Web Admin để lấy danh sách này chứ không đọc từ thân yêu cầu.
    /// </remarks>
    public const string ScopeWholeKnowledgeBase = "*";

    /// <summary>Tài liệu áp dụng cho mọi nhân viên (nội quy, quy chế chung).</summary>
    public const string ScopeAll = "all";

    /// <summary>Tiền tố phạm vi theo phòng ban, ghép với mã phòng ban: <c>dept:3</c>.</summary>
    public const string DepartmentScopePrefix = "dept:";

    /// <summary>Nhãn phạm vi khi người dùng xem được toàn bộ kho tri thức.</summary>
    public const string WholeKnowledgeBaseLabel = "Toàn khách sạn";

    /// <summary>Nhãn phạm vi khi người dùng chỉ xem được tài liệu chung.</summary>
    public const string SharedDocumentsLabel = "Tài liệu dùng chung";

    /// <summary>
    /// Vai trò được đọc toàn bộ kho tri thức (mọi tài liệu, kể cả của phòng ban khác).
    ///
    /// <para>
    /// Admin, HR và IT. Nhân sự cần đọc hết để soạn thảo và đối chiếu; IT cần đọc hết để vận hành;
    /// Admin vốn đã đứng trên toàn hệ. Quản lý bộ phận <b>không</b> thuộc nhóm này — họ bị giới
    /// hạn trong phòng mình, cùng ranh giới với <c>ReportPolicy.ResolveScope</c>.
    /// </para>
    /// </summary>
    public static bool CanReadWholeKnowledgeBase(string? role) =>
        LeaveApprovalPolicy.NormalizeRole(role) is RoleType.Admin or RoleType.HR or RoleType.IT;

    /// <summary>Tài liệu được quản lý trong mã nguồn; người dùng không tải tài liệu lên từ Web.</summary>
    public static bool CanIngestKnowledge() => false;

    /// <summary>
    /// Danh sách phạm vi tài liệu người dùng được đọc, dạng chuỗi đi được qua JSON.
    ///
    /// <para>
    /// Vai trò quản trị nhận đúng một phần tử <see cref="ScopeWholeKnowledgeBase"/>. Các vai trò
    /// còn lại luôn nhận <see cref="ScopeAll"/>, cộng thêm phạm vi phòng ban khi có claim hợp lệ.
    /// Nhân viên chưa gán phòng ban (<c>DepartmentId</c> mặc định là <c>"0"</c> và vé bỏ hẳn claim)
    /// chỉ đọc được tài liệu chung — không được nâng lên "toàn khách sạn".
    /// </para>
    /// </summary>
    /// <param name="role">Tên vai trò thô từ claim.</param>
    /// <param name="departmentId">Mã phòng ban từ claim; <c>null</c> hoặc <c>&lt;= 0</c> nghĩa là chưa gán.</param>
    public static IReadOnlyList<string> ResolveDocumentScopes(string? role, int? departmentId)
    {
        if (CanReadWholeKnowledgeBase(role))
        {
            return [ScopeWholeKnowledgeBase];
        }

        if (departmentId is > 0)
        {
            return [ScopeAll, $"{DepartmentScopePrefix}{departmentId.Value}"];
        }

        return [ScopeAll];
    }

    /// <summary>
    /// Nhãn tiếng Việt của phạm vi, dùng cho huy hiệu trên khung chat và cho câu trả lời.
    /// </summary>
    /// <param name="role">Tên vai trò thô.</param>
    /// <param name="departmentId">Mã phòng ban từ claim.</param>
    /// <param name="departmentName">Tên phòng ban tra từ CSDL; <c>null</c> khi chưa tra được.</param>
    public static string ResolveScopeLabel(string? role, int? departmentId, string? departmentName)
    {
        if (CanReadWholeKnowledgeBase(role))
        {
            return WholeKnowledgeBaseLabel;
        }

        if (departmentId is > 0 && !string.IsNullOrWhiteSpace(departmentName))
        {
            return $"Bộ phận: {departmentName.Trim()}";
        }

        return SharedDocumentsLabel;
    }

    /// <summary>
    /// Kiểm một phạm vi do phía gọi gửi lên có nằm trong danh sách được phép hay không.
    ///
    /// <para>
    /// Dùng khi nạp tài liệu: HR chọn "toàn khách sạn" hay một phòng ban cụ thể, và lựa chọn đó
    /// phải khớp với danh mục phòng ban thật. So khớp theo <b>chuỗi chính xác</b> chứ không phân
    /// tích cú pháp — mọi giá trị lạ đều bị loại.
    /// </para>
    /// </summary>
    /// <param name="requested">Phạm vi người dùng chọn.</param>
    /// <param name="allowed">Danh sách phạm vi hợp lệ do máy chủ dựng.</param>
    public static bool IsAllowedScope(string? requested, IReadOnlyList<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        var trimmed = requested.Trim();
        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, trimmed, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
