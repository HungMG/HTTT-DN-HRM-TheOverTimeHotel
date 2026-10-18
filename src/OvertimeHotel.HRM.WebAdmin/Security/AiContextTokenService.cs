using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Cấp và xác minh "vé dữ liệu" mà Trợ lý AI dùng để hỏi số liệu tổng hợp từ Web Admin.
///
/// <para>
/// <b>Vì sao cần lớp này:</b> Chatbot là một tiến trình Node riêng, không có phiên đăng nhập
/// của người dùng. Thay vì đưa khoá Supabase cho nó, Web Admin ký một vé ngắn hạn chứa đúng
/// danh tính người đang đăng nhập; chatbot cầm vé gọi ngược về <c>/api/ai-data/*</c> và
/// Web Admin tự truy vấn. Khoá CSDL không bao giờ rời máy chủ.
/// </para>
///
/// <para>
/// <b>Khoá ký riêng, không mượn <c>Jwt:Key</c>:</b> <c>Jwt:Key</c> thuộc đường đăng nhập mobile
/// kiểu cũ đã bị đánh dấu lỗi thời và đang chờ gỡ. Dùng chung khoá sẽ khiến tính năng mới chết
/// theo khi khoá đó bị xoá.
/// </para>
///
/// <para>
/// <b>Vé chỉ để ĐỌC.</b> Vé không cấp quyền ghi, và phạm vi dữ liệu vẫn do
/// <c>ReportPolicy.ResolveScope</c> quyết định lại ở phía máy chủ — vé không thể mở rộng
/// tầm nhìn.
/// </para>
///
/// <para>
/// <b>Vé dùng được nhiều lần trong 10 phút.</b> Khung chat chỉ được cấp vé một lần lúc mở,
/// nhưng người dùng hỏi nhiều câu liên tiếp. Chặn dùng lại sẽ khiến mọi câu thứ hai trở đi
/// thất bại trong khi vé vẫn còn hạn. Cửa sổ rủi ro được giữ hẹp bằng TTL ngắn và bằng việc
/// xoá vé khỏi thanh địa chỉ ngay khi đọc.
/// </para>
/// </summary>
public sealed class AiContextTokenService
{
    /// <summary>Tên cấu hình chứa khoá ký vé, nằm trong mục <c>Chatbot</c>.</summary>
    public const string KeyConfigPath = "Chatbot:AiTokenKey";

    /// <summary>Biến môi trường thay thế khi không muốn để khoá trong tệp cấu hình.</summary>
    public const string KeyEnvVar = "AI_CONTEXT_TOKEN_KEY";

    /// <summary>Đối tượng nhận của vé — tách hẳn token mobile để không thể dùng lẫn.</summary>
    public const string Audience = "OverTimeHotel.AiData";

    /// <summary>Tuổi thọ của vé. Ngắn có chủ ý: vé nằm trong địa chỉ iframe.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Claim mã nhân viên, phục vụ tra cứu phiếu lương của chính mình.</summary>
    public const string EmployeeClaim = "emp";

    /// <summary>Claim mã phòng ban, phục vụ ghép phạm vi cho Quản lý bộ phận.</summary>
    public const string DepartmentClaim = "dept";

    /// <summary>Tên cấu hình chứa định danh bên phát hành, dùng chung với luồng mobile.</summary>
    public const string IssuerConfigPath = "Jwt:Issuer";

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiContextTokenService> _logger;

    public AiContextTokenService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<AiContextTokenService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Ký một vé mới từ phiên đăng nhập hiện tại. Vé sống <see cref="Lifetime"/> phút và
    /// dùng được nhiều lần trong khoảng thời gian đó.
    /// </summary>
    /// <param name="user">Principal của phiên đăng nhập Web Admin.</param>
    /// <returns>Chuỗi JWT để nhúng vào địa chỉ iframe chatbot.</returns>
    /// <exception cref="InvalidOperationException">Khoá ký chưa được cấu hình.</exception>
    public string Issue(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = ResolveSigningKey();
        var issuer = _configuration[IssuerConfigPath] ?? "OvertimeHotel.HRM";
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(JwtRegisteredClaimNames.Sub, user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty),
            new(ClaimTypes.Role, user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty)
        };

        // Mã nhân viên và mã phòng ban chỉ thêm khi có giá trị thật: claim rỗng
        // sẽ khiến tầng xác minh hiểu nhầm là "đã có phòng ban 0".
        var employeeId = user.FindFirst("EmployeeId")?.Value;
        if (int.TryParse(employeeId, out var emp) && emp > 0)
        {
            claims.Add(new Claim(EmployeeClaim, emp.ToString()));
        }

        var departmentId = user.FindFirst("DepartmentId")?.Value;
        if (int.TryParse(departmentId, out var dept) && dept > 0)
        {
            claims.Add(new Claim(DepartmentClaim, dept.ToString()));
        }

        // Quyền chức năng sao nguyên từ phiên, để tầng dữ liệu kiểm lại bằng đúng
        // WebAdminPermissions.HasPermission như giao diện đang dùng.
        claims.AddRange(user.FindAll(PermissionCodes.ClaimType)
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => new Claim(PermissionCodes.ClaimType, c.Value)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        // Ghi nhớ mã vé trong suốt thời gian sống. Mục đệm này KHÔNG chặn dùng lại: người
        // dùng hỏi nhiều câu trong cùng một phiên chat mà chỉ được cấp vé một lần lúc mở
        // khung trợ lý. Nó tồn tại để vé do chính máy chủ này phát mới được chấp nhận —
        // vé cùng khoá nhưng do nơi khác ký vẫn bị từ chối.
        _cache.Set(CacheKey(tokenId), true, Lifetime);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Xác minh vé. Trả <c>null</c> khi vé sai chữ ký, hết hạn, sai đối tượng nhận, hoặc
    /// không do máy chủ này phát — người gọi tự quyết định mã lỗi HTTP.
    /// </summary>
    /// <param name="token">Chuỗi vé nhận từ chatbot.</param>
    public ClaimsPrincipal? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var key = ResolveSigningKey();
        var issuer = _configuration[IssuerConfigPath] ?? "OvertimeHotel.HRM";

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateLifetime = true,

                    // Không nới thời gian lệch như luồng mobile (5 phút): vé chỉ sống vài phút
                    // nên nới thêm sẽ khiến cửa sổ dùng lại rộng gấp đôi.
                    ClockSkew = TimeSpan.Zero
                },
                out _);

            // Vé phải có mã định danh và mã đó phải còn trong bộ nhớ phát hành. Mục đệm
            // KHÔNG bị xoá khi xác minh: người dùng hỏi nhiều câu trong một phiên chat mà
            // chỉ được cấp vé một lần lúc mở khung trợ lý, nên chặn dùng lại sẽ làm mọi câu
            // hỏi thứ hai trở đi thất bại. Hết hạn là hết dùng được — TTL đệm trùng TTL vé.
            var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrWhiteSpace(tokenId))
            {
                _logger.LogWarning("Vé Trợ lý AI bị từ chối: thiếu mã định danh vé.");
                return null;
            }

            if (!_cache.TryGetValue(CacheKey(tokenId), out _))
            {
                _logger.LogWarning("Vé Trợ lý AI bị từ chối: vé hết hạn hoặc không do máy chủ này phát.");
                return null;
            }

            return principal;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            // Bắt cả ArgumentException: chuỗi không phải vé (sai định dạng Base64Url) làm bộ đọc
            // vé ném lỗi ngay ở bước giải mã, trước cả khi kịp kiểm chữ ký. Bỏ sót nhánh này thì
            // một chuỗi rác bất kỳ biến endpoint thành lỗi 500 kèm vết ngăn xếp thay vì 401.
            // Không ghi nội dung vé vào nhật ký: vé mang danh tính người đăng nhập.
            _logger.LogWarning(ex, "Vé Trợ lý AI không hợp lệ.");
            return null;
        }
    }

    /// <summary>
    /// Đọc khoá ký theo thứ tự cấu hình rồi biến môi trường. Thiếu khoá thì ném lỗi ngay
    /// thay vì chạy với khoá rỗng — cùng nguyên tắc fail-closed mà <c>Program.cs</c> đang áp dụng.
    /// </summary>
    private string ResolveSigningKey()
    {
        var key = _configuration[KeyConfigPath];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable(KeyEnvVar);
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Bảo mật hệ thống (Fail-Closed): Khóa ký vé dữ liệu Trợ lý AI chưa được cấu hình. " +
                $"Hãy đặt '{KeyConfigPath}' trong cấu hình hoặc biến môi trường {KeyEnvVar}.");
        }

        return key;
    }

    private static string CacheKey(string tokenId) => $"AiContextToken|{tokenId}";
}
