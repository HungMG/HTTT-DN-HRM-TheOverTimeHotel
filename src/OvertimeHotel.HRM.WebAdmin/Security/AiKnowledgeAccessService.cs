using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Một phòng ban có thể gắn cho tài liệu, dạng đã sẵn sàng đi qua JSON.
/// </summary>
/// <param name="Code">Mã phạm vi, ví dụ <c>dept:3</c> — đúng chuỗi sẽ ghi vào metadata tài liệu.</param>
/// <param name="Name">Tên hiển thị cho người dùng chọn.</param>
public sealed record KnowledgeDepartmentDto(string Code, string Name);

/// <summary>
/// Kết quả phân giải quyền đọc/ghi kho tri thức cho một người dùng đã xác thực bằng vé.
/// </summary>
/// <param name="CanIngest">Được nạp tài liệu PDF vào kho hay không.</param>
/// <param name="DocumentScopes">
/// Các phạm vi tài liệu được ĐỌC. Phần tử <c>*</c> nghĩa là toàn kho; <c>all</c> là tài liệu
/// dùng chung; <c>dept:&lt;n&gt;</c> là tài liệu của một phòng ban.
/// </param>
/// <param name="IngestScopes">Các phạm vi người dùng được phép GHI. Rỗng khi không có quyền nạp.</param>
/// <param name="ScopeLabel">Nhãn tiếng Việt của phạm vi đọc, dùng cho huy hiệu trên khung chat.</param>
/// <param name="Departments">Danh mục phòng ban để chọn khi nạp; rỗng khi không có quyền nạp.</param>
/// <param name="AccountId">
/// Mã tài khoản của người cầm vé, để tệp nạp vào đóng dấu được ai đã tải lên. <c>null</c> khi
/// vé không mang mã tài khoản hợp lệ.
/// </param>
public sealed record KnowledgeAccessDto(
    bool CanIngest,
    IReadOnlyList<string> DocumentScopes,
    IReadOnlyList<string> IngestScopes,
    string ScopeLabel,
    IReadOnlyList<KnowledgeDepartmentDto> Departments,
    int? AccountId);

/// <summary>
/// Trả lời hai câu hỏi mà Trợ lý AI cần trước khi đọc hoặc ghi kho tri thức:
/// <i>người này được đọc tài liệu nào</i> và <i>có được nạp tài liệu không</i>.
///
/// <para>
/// <b>Vì sao phải hỏi máy chủ thay vì tin trình duyệt:</b> trước đây <c>/api/ingest</c> nhận
/// tệp từ bất kỳ ai gọi được địa chỉ, còn <c>/api/chat</c> không lọc tài liệu theo người hỏi —
/// mọi nhân viên đều tra được mọi văn bản đã nạp. Cả hai lỗ đó cùng gốc: <i>phạm vi do phía
/// gọi tự khai</i>. Lớp này thay chỗ đó bằng một câu trả lời do máy chủ dựng từ vé và CSDL.
/// </para>
///
/// <para>
/// <b>Vé chỉ nói "ai", không nói "được xem gì":</b> đúng như <see cref="AiContextTokenService"/>
/// mô tả. Phạm vi luôn được dựng lại ở đây bằng <see cref="KnowledgeAccessPolicy"/>, và tài
/// khoản phải còn hoạt động tại thời điểm hỏi — vé sống tới 10 phút nên một tài khoản bị khoá
/// giữa chừng vẫn phải mất quyền ngay.
/// </para>
/// </summary>
public sealed class AiKnowledgeAccessService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AiKnowledgeAccessService> _logger;

    public AiKnowledgeAccessService(AppDbContext context, ILogger<AiKnowledgeAccessService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Mã tài khoản trong vé, hoặc <c>null</c> khi claim thiếu/sai định dạng.
    /// </summary>
    /// <remarks>
    /// Chỉ là mã tham chiếu để đóng dấu tài liệu đã nạp — không cấp quyền gì. Quyền vẫn do
    /// <see cref="ResolveAsync"/> và <see cref="KnowledgeAccessPolicy"/> quyết định.
    /// </remarks>
    public static int? ReadAccountId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ReadInt(principal, ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Dựng quyền truy cập kho tri thức từ principal đã xác thực bằng vé.
    ///
    /// <para>
    /// <b>Không ném lỗi khi CSDL trục trặc.</b> Mất kết nối CSDL thì trả về đúng phạm vi hẹp nhất
    /// (<c>all</c>) và tắt quyền nạp. Ném lỗi sẽ làm cả khung chat hỏng trong khi phần hỏi đáp
    /// tài liệu vẫn có thể chạy; còn nới rộng phạm vi để "cứu" tình huống thì lại là lỗ hổng.
    /// </para>
    /// </summary>
    /// <param name="principal">Principal dựng từ vé dữ liệu đã xác minh.</param>
    public async Task<KnowledgeAccessDto> ResolveAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var departmentId = ReadInt(principal, AiContextTokenService.DepartmentClaim);
        var accountId = ReadAccountId(principal);

        try
        {
            // Tài khoản có thể đã bị khoá trong 10 phút sống của vé. Kiểm lại tại thời điểm
            // hỏi thay vì tin trạng thái lúc phát vé.
            if (!await IsAccountActiveAsync(principal))
            {
                return BuildReadOnly(role, departmentId, departmentName: null, canIngest: false, accountId);
            }

            var canIngest = KnowledgeAccessPolicy.CanIngestKnowledge();

            var departmentName = departmentId is > 0
                ? await _context.PhongBans
                    .AsNoTracking()
                    .Where(p => p.MaPhongBan == departmentId.Value)
                    .Select(p => p.TenPhongBan)
                    .FirstOrDefaultAsync()
                : null;

            if (!canIngest)
            {
                return BuildReadOnly(role, departmentId, departmentName, canIngest: false, accountId);
            }

            // Chỉ vai trò có quyền nạp mới cần danh mục phòng ban. Tra ở đây thay vì ở mọi
            // lượt chat để mỗi câu hỏi không phải kéo thêm một bảng danh mục.
            var departments = await _context.PhongBans
                .AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .Select(p => new KnowledgeDepartmentDto(
                    KnowledgeAccessPolicy.DepartmentScopePrefix + p.MaPhongBan,
                    p.TenPhongBan))
                .ToListAsync();

            // Được nạp cả tài liệu dùng chung lẫn tài liệu của từng phòng ban.
            var ingestScopes = new List<string>(departments.Count + 1)
            {
                KnowledgeAccessPolicy.ScopeAll
            };
            ingestScopes.AddRange(departments.Select(d => d.Code));

            var read = BuildReadOnly(role, departmentId, departmentName, canIngest: true, accountId);
            return read with { IngestScopes = ingestScopes, Departments = departments };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ghi nhật ký rồi thu hẹp quyền. Không ném tiếp: khung chat vẫn phải mở được.
            _logger.LogWarning(ex, "Không dựng được phạm vi kho tri thức; thu hẹp về tài liệu dùng chung.");
            return BuildReadOnly(role, departmentId, departmentName: null, canIngest: false, accountId);
        }
    }

    /// <summary>
    /// Phạm vi hẹp nhất: chỉ tài liệu dùng chung, không nạp được gì.
    /// </summary>
    private static KnowledgeAccessDto BuildReadOnly(
        string? role,
        int? departmentId,
        string? departmentName,
        bool canIngest,
        int? accountId)
    {
        var scopes = KnowledgeAccessPolicy.ResolveDocumentScopes(role, departmentId);

        return new KnowledgeAccessDto(
            CanIngest: canIngest,
            DocumentScopes: scopes,
            IngestScopes: [],
            ScopeLabel: KnowledgeAccessPolicy.ResolveScopeLabel(role, departmentId, departmentName),
            Departments: [],
            AccountId: accountId);
    }

    /// <summary>
    /// Kiểm tài khoản gắn với vé còn tồn tại và chưa bị khoá — cùng luật với
    /// <c>AiDataController.BuildReportsAsync</c>.
    /// </summary>
    private async Task<bool> IsAccountActiveAsync(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(raw, out var accountId) || accountId <= 0)
        {
            return false;
        }

        return await _context.TaiKhoans
            .AsNoTracking()
            .AnyAsync(t => t.MaTaiKhoan == accountId && t.TrangThai);
    }

    /// <summary>Đọc claim số nguyên; thiếu hoặc sai định dạng trả <c>null</c>.</summary>
    private static int? ReadInt(ClaimsPrincipal principal, string claimType) =>
        int.TryParse(principal.FindFirst(claimType)?.Value, out var value) ? value : null;
}
