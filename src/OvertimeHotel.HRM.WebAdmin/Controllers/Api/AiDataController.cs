using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers.Api;

/// <summary>
/// Cổng dữ liệu cho Trợ lý AI (chatbot). Chatbot là tiến trình Node riêng, không có phiên
/// đăng nhập và <b>không được cầm khoá Supabase</b>. Nó cầm "vé dữ liệu" do
/// <see cref="AiContextTokenService"/> ký, gọi vào đây, và Web Admin tự truy vấn rồi trả về
/// số liệu đã tổng hợp.
///
/// <para>
/// <b>Vì sao không dùng <c>[Authorize]</c>:</b> đây là điểm cuối dành cho tiến trình khác,
/// không phải cho trình duyệt của người dùng. Xác thực thực hiện thủ công qua
/// <see cref="AiContextTokenService.Validate"/> — vé mang đối tượng nhận riêng
/// (<see cref="AiContextTokenService.Audience"/>) nên token mobile hay cookie phiên đều
/// không dùng được ở đây, và ngược lại.
/// </para>
///
/// <para>
/// <b>Phạm vi dữ liệu vẫn do máy chủ quyết định:</b> endpoint dựng lại <see cref="ClaimsPrincipal"/>
/// từ vé rồi gọi đúng <see cref="ReportPolicy.ResolveScope"/> như trang Báo cáo &amp; Thống kê.
/// Vé không thể mở rộng tầm nhìn — nó chỉ chứng minh <i>ai</i> đang hỏi, không nói <i>được xem gì</i>.
/// </para>
/// </summary>
[ApiController]
[Route("api/ai-data")]
public class AiDataController : ControllerBase
{
    /// <summary>Phạm vi báo cáo tổng hợp sáu mục 10.1 → 10.6.</summary>
    private const string ScopeReports = "reports";

    /// <summary>Phạm vi phiếu lương của chính người đang hỏi.</summary>
    private const string ScopeMyPayslip = "my-payslip";

    /// <summary>Phạm vi tra cứu nhân sự theo từ khóa (tìm nhân viên theo tên).</summary>
    private const string ScopeDirectory = "directory";

    /// <summary>Phạm vi tra cứu hợp đồng lao động theo từ khóa (tên nhân viên, số hoặc loại hợp đồng).</summary>
    private const string ScopeContract = "contract";

    /// <summary>Phạm vi tra cứu đơn từ theo từ khóa (tên nhân viên hoặc loại đơn).</summary>
    private const string ScopeLeave = "leave";

    /// <summary>Phạm vi tra cứu phân ca theo từ khóa (tên nhân viên hoặc tên ca).</summary>
    private const string ScopeShift = "shift";

    /// <summary>Phạm vi tra cứu chấm công (quẹt thẻ) theo từ khóa (tên nhân viên, tên ca, trạng thái).</summary>
    private const string ScopeAttendance = "attendance";

    /// <summary>Phạm vi tra cứu sự kiện / dự án theo từ khóa (tên sự kiện, loại, trạng thái, tên nhân sự tham gia).</summary>
    private const string ScopeEvent = "event";

    /// <summary>Số kỳ lương gần nhất trả về cho mục cá nhân. Đủ để thấy xu hướng, không phình ngữ cảnh.</summary>
    private const int PersonalPayslipCount = 3;

    /// <summary>
    /// Giới hạn số nhân viên trả về mỗi lượt tra cứu. Một khách sạn có thể có nhiều người
    /// trùng tên; trả cả danh sách sẽ phình ngữ cảnh của mô hình, còn cắt bớt kèm tổng số thì
    /// mô hình vẫn nói đúng "có N người, đây là 10 người đầu tiên".
    /// </summary>
    private const int DirectoryResultLimit = 10;

    /// <summary>
    /// Giới hạn số hợp đồng trả về mỗi lượt tra cứu, theo cùng lý do với
    /// <see cref="DirectoryResultLimit"/>.
    /// </summary>
    private const int ContractResultLimit = 10;

    /// <summary>
    /// Giới hạn số đơn từ trả về mỗi lượt tra cứu, theo cùng lý do với
    /// <see cref="DirectoryResultLimit"/>.
    /// </summary>
    private const int LeaveResultLimit = 10;

    /// <summary>
    /// Giới hạn số ca trực trả về mỗi lượt tra cứu, theo cùng lý do với
    /// <see cref="DirectoryResultLimit"/>.
    /// </summary>
    private const int ShiftResultLimit = 15;

    /// <summary>
    /// Giới hạn số dòng chấm công trả về mỗi lượt tra cứu, theo cùng lý do với
    /// <see cref="DirectoryResultLimit"/>.
    /// </summary>
    private const int AttendanceResultLimit = 20;

    /// <summary>
    /// Giới hạn số sự kiện / dự án trả về mỗi lượt tra cứu, theo cùng lý do với
    /// <see cref="DirectoryResultLimit"/>.
    /// </summary>
    private const int EventResultLimit = 10;

    private readonly AiContextTokenService _tokenService;
    private readonly AiKnowledgeAccessService _knowledgeAccessService;
    private readonly BaoCaoReportService _reportService;
    private readonly IAttendanceSummaryService _attendanceService;
    private readonly AppDbContext _context;
    private readonly ILogger<AiDataController> _logger;

    public AiDataController(
        AiContextTokenService tokenService,
        AiKnowledgeAccessService knowledgeAccessService,
        BaoCaoReportService reportService,
        IAttendanceSummaryService attendanceService,
        AppDbContext context,
        ILogger<AiDataController> logger)
    {
        _tokenService = tokenService;
        _knowledgeAccessService = knowledgeAccessService;
        _reportService = reportService;
        _attendanceService = attendanceService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Trả bản rút gọn số liệu cho Trợ lý AI đọc.
    ///
    /// <para>
    /// Thân yêu cầu: <c>{ token, scope, year, month, quarter }</c>. Trường <c>token</c> nằm trong
    /// thân chứ không phải tham số địa chỉ — địa chỉ có thể lọt vào nhật ký truy cập của máy chủ.
    /// </para>
    /// </summary>
    /// <response code="200">Số liệu đã lọc theo phạm vi được phép.</response>
    /// <response code="400">Thiếu vé hoặc vé dùng cho phạm vi không hợp lệ.</response>
    /// <response code="401">Vé sai chữ ký, hết hạn, sai đối tượng nhận, hoặc đã dùng rồi.</response>
    /// <response code="403">Vai trò không được xem số liệu, hoặc Quản lý thiếu claim phòng ban.</response>
    /// <response code="503">Chưa lấy được dữ liệu từ CSDL — cố ý không trả số 0 kèm theo.</response>
    [HttpPost("digest")]
    public async Task<IActionResult> Digest([FromBody] AiDataRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Thiếu vé dữ liệu." });
        }

        var principal = _tokenService.Validate(request.Token);
        if (principal is null)
        {
            return Unauthorized(new { message = "Vé dữ liệu không hợp lệ hoặc đã hết hiệu lực." });
        }

        var scope = (request.Scope ?? ScopeReports).Trim().ToLowerInvariant();

        return scope switch
        {
            ScopeMyPayslip => await BuildPersonalAsync(principal, request),
            ScopeReports => await BuildReportsAsync(principal, request),
            ScopeDirectory => await BuildDirectoryAsync(principal, request),
            ScopeContract => await BuildContractAsync(principal, request),
            ScopeLeave => await BuildLeaveAsync(principal, request),
            ScopeShift => await BuildShiftAsync(principal, request),
            ScopeAttendance => await BuildAttendanceAsync(principal, request),
            ScopeEvent => await BuildEventAsync(principal, request),
            _ => BadRequest(new { message = "Phạm vi dữ liệu không hợp lệ." })
        };
    }

    /// <summary>
    /// Cho biết người cầm vé được ĐỌC tài liệu nào và có được NẠP tài liệu không.
    ///
    /// <para>
    /// <b>Vì sao phải có endpoint riêng:</b> cả việc lọc tài liệu khi tra cứu lẫn việc chấp nhận
    /// tệp khi nạp đều không thể do phía trình duyệt tự khai. Chính route <c>/api/chat</c> và
    /// <c>/api/ingest</c> của chatbot gọi endpoint này <b>mỗi lượt</b>, bằng vé lấy từ chính phiên
    /// đăng nhập, rồi dùng kết quả đó để dựng bộ lọc.
    /// </para>
    ///
    /// <para>
    /// <b>Phạm vi được dựng lại ở đây, không đọc từ yêu cầu.</b> Thân yêu cầu chỉ có vé; nếu
    /// nhận thêm trường phạm vi thì endpoint này biến thành công cụ tự nâng quyền.
    /// </para>
    /// </summary>
    /// <response code="200">Phạm vi đọc/ghi đã phân giải.</response>
    /// <response code="400">Thiếu vé.</response>
    /// <response code="401">Vé sai chữ ký, hết hạn, sai đối tượng nhận, hoặc đã hết hiệu lực.</response>
    [HttpPost("knowledge-access")]
    public async Task<IActionResult> KnowledgeAccess([FromBody] AiKnowledgeAccessRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Thiếu vé dữ liệu." });
        }

        var principal = _tokenService.Validate(request.Token);
        if (principal is null)
        {
            return Unauthorized(new { message = "Vé dữ liệu không hợp lệ hoặc đã hết hiệu lực." });
        }

        var access = await _knowledgeAccessService.ResolveAsync(principal);
        return Ok(access);
    }

    /// <summary>
    /// Dựng sáu báo cáo tổng hợp trong đúng phạm vi quyền. Quỹ lương (10.4) chỉ kèm khi vai trò
    /// được xem — <see cref="AiDataDigestDto.QuyLuong"/> để <c>null</c> nếu không, và
    /// <c>WhenWritingNull</c> sẽ khiến khoá đó biến mất hẳn khỏi JSON.
    /// </summary>
    private async Task<IActionResult> BuildReportsAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            // Không tìm thấy tài khoản tương ứng trong CSDL, hoặc đã bị khoá: vé còn hạn
            // nhưng chủ vé không còn quyền.
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var scope = ReportPolicy.ResolveScope(role, ReadInt(principal, AiContextTokenService.DepartmentClaim));

        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        // Cổng quyền chức năng, đúng như giao diện đang dùng. Thừa so với ResolveScope
        // (vốn đã loại Employee) nhưng rẻ, và giữ một nguồn luật cho cả web lẫn API.
        if (!principal.HasPermission(PermissionCodes.ReportView))
        {
            return Forbid();
        }

        var model = await _reportService.BuildAsync(
            scope,
            role,
            ResolvePeriodKind(request),
            request.Year,
            request.Month,
            request.Quarter,
            phongBan: null,
            tab: null);

        // Không trả số 0 khi mất kết nối CSDL: mô hình sẽ đọc thành "thực chi 0 đồng".
        // Thà trả 503 để node trả lời "chưa lấy được số liệu" còn hơn một con số sai nghe rất hợp lý.
        if (!model.IsDatabaseOnline)
        {
            _logger.LogWarning("Trợ lý AI xin số liệu báo cáo nhưng CSDL không phản hồi.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được số liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }

        var digest = new AiDataDigestDto
        {
            ScopeLabel = model.PhamViLabel,
            PeriodLabel = model.NhanKy,
            RoleLabel = model.RoleLabel,
            GeneratedAt = model.GeneratedAt,
            GhiChu = BuildReportNotes()
        };

        digest.BienDong = MapBienDong(model.BienDong);
        digest.CoCau = MapCoCau(model.CoCau);
        digest.HocVan = MapHocVan(model.HocVanThamNien);
        digest.ChuyenCan = MapChuyenCan(model.ChuyenCan);
        digest.SuKien = MapSuKien(model.SuKien);

        // Chỉ gán khi vai trò thật sự được xem quỹ lương. Gán kèm thì Manager sẽ nhận được
        // một khối toàn số 0 — đúng thứ mà giao diện đang cố tránh bằng CanViewPayrollFund.
        if (model.CanViewPayrollFund)
        {
            digest.QuyLuong = MapQuyLuong(model.QuyLuong);
        }

        return Ok(digest);
    }

    /// <summary>
    /// Dựng phiếu lương của chính người đang hỏi.
    ///
    /// <para>
    /// <b>Mã nhân viên LUÔN lấy từ vé, không bao giờ từ tham số yêu cầu.</b> Nhận mã nhân viên
    /// từ thân yêu cầu sẽ biến endpoint này thành công cụ tra lương người khác.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildPersonalAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        var employeeId = ReadInt(principal, AiContextTokenService.EmployeeClaim);
        if (employeeId is not > 0)
        {
            return Forbid();
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var year = request.Year is >= 2000 and <= 2200 ? request.Year.Value : today.Year;
        var month = Math.Clamp(request.Month ?? today.Month, 1, 12);

        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        try
        {
            var attendance = await _attendanceService.GetAsync(employeeId.Value, from, to, HttpContext.RequestAborted);

            var payslips = await _context.PhieuLuongs
                .AsNoTracking()
                .Where(p => p.MaNhanVien == employeeId.Value)
                .OrderByDescending(p => p.MaKyLuong)
                .Take(PersonalPayslipCount)
                .Select(p => new
                {
                    p.TrangThai,
                    KyThang = p.KyLuong == null ? 0 : p.KyLuong.Thang,
                    KyNam = p.KyLuong == null ? 0 : p.KyLuong.Nam,
                    p.TienLuongCong,
                    p.TienTangCa,
                    p.TongPhuCap,
                    p.TongThuong,
                    p.TongKhauTru,
                    p.ThucNhan
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var digest = new AiDataDigestDto
            {
                ScopeLabel = "Phiếu lương của chính bạn",
                PeriodLabel = $"Tháng {month:00}/{year}",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildPersonalNotes(),
                CaNhan = new AiDataCaNhanDto
                {
                    NgayCongThucTe = attendance?.WorkedDays ?? 0,
                    NgayNghiPhep = attendance?.PaidLeaveDays ?? 0,
                    PhieuLuong = [.. payslips.Select(p => new AiDataPhieuLuongDto
                    {
                        KyLuong = p.KyThang > 0 ? $"{p.KyThang:00}/{p.KyNam}" : "Chưa gắn kỳ",
                        TienLuongCong = p.TienLuongCong,
                        TienTangCa = p.TienTangCa,
                        TongPhuCap = p.TongPhuCap,
                        TongThuong = p.TongThuong,
                        TongKhauTru = p.TongKhauTru,
                        ThucNhan = p.ThucNhan,
                        TrangThai = ResolvePayslipStatus(p.TrangThai)
                    })]
                }
            };

            return Ok(digest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI xin phiếu lương cá nhân nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được số liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu nhân sự theo từ khóa (ví dụ "có nhân viên nào tên Sang không?").
    ///
    /// <para>
    /// <b>Phạm vi dùng lại đúng luật báo cáo:</b> <see cref="ReportPolicy.ResolveScope"/> cho
    /// Admin/HR/IT xem toàn khách sạn, Quản lý chỉ bộ phận mình, Employee bị từ chối. Không
    /// kiểm thêm mã quyền chức năng vì tra cứu chỉ đọc tên/phòng ban/chức danh — hẹp hơn trang
    /// Nhân viên mà các vai trò này vốn được xem.
    /// </para>
    ///
    /// <para>
    /// <b>Chỉ trả thông tin cơ bản</b> (mã, họ tên, phòng ban, chức danh, trạng thái): kết quả
    /// đi vào ngữ cảnh của mô hình ngôn ngữ, nên điện thoại, email, ngày sinh và lương cố ý
    /// không được đưa vào. Từ khóa do router của chatbot trích ra từ câu hỏi, chỉ để thu hẹp
    /// kết quả — không thể mở rộng phạm vi quyền.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildDirectoryAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var scope = ReportPolicy.ResolveScope(role, ReadInt(principal, AiContextTokenService.DepartmentClaim));

        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        // Từ khóa trống nghĩa là hỏi chung ("có bao nhiêu nhân viên?", "những ai đang làm
        // việc?"): trả danh sách mặc định thay vì từ chối, để chatbot luôn có dữ liệu hiển thị.
        // Keyword rỗn KHÔNG bao giờ mở rộng quyền — scope đã quyết định trước khi tới đây.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            var pattern = $"%{keyword}%";
            var hasEmployeeCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var employeeCode);

            var query = _context.NhanViens.AsNoTracking();

            // Quản lý bộ phận bị khóa trong phòng ban của mình ngay từ truy vấn — cùng cách
            // lọc mà trang báo cáo áp dụng, nên không có vai trò nào thấy nhiều hơn trên web.
            if (!scope.IsWholeHotel)
            {
                query = query.Where(n => n.MaPhongBan == scope.DepartmentId);
            }

            // Hỏi chung (không từ khóa) → bỏ điều kiện tìm kiếm, trả danh sách mặc định.
            if (hasKeyword)
            {
                query = query.Where(n =>
                    EF.Functions.ILike(n.Ho + " " + n.Ten, pattern)
                    || EF.Functions.ILike(n.Ho, pattern)
                    || EF.Functions.ILike(n.Ten, pattern)
                    || (hasEmployeeCode && n.MaNhanVien == employeeCode));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                // Vẫn trả một khối Directory rỗng thay vì 404: "không tìm thấy ai khớp từ khóa"
                // là một câu trả lời nghiệp vụ hợp lệ, không phải lỗi.
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = scope.IsWholeHotel
                        ? "Tra cứu nhân sự — toàn khách sạn"
                        : "Tra cứu nhân sự — bộ phận của bạn",
                    PeriodLabel = "Dữ liệu hiện hành",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildDirectoryNotes(),
                    Directory = new AiDataDirectoryDto
                    {
                        TuKhoa = keyword,
                        TongSo = 0,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderBy(n => n.Ten).ThenBy(n => n.Ho)
                .Take(DirectoryResultLimit)
                .Select(n => new AiDataNhanVienDto
                {
                    MaNhanVien = n.MaNhanVien,
                    HoTen = n.Ho + " " + n.Ten,
                    PhongBan = n.PhongBan == null ? null : n.PhongBan.TenPhongBan,
                    ChucDanh = n.ChucVu == null ? null : n.ChucVu.TenChucVu,
                    TrangThai = n.TrangThai
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var tenPhongBanGioiHan = scope.IsWholeHotel
                ? null
                : await _context.PhongBans
                    .AsNoTracking()
                    .Where(p => p.MaPhongBan == scope.DepartmentId)
                    .Select(p => p.TenPhongBan)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return Ok(new AiDataDigestDto
            {
                ScopeLabel = scope.IsWholeHotel
                    ? "Tra cứu nhân sự — toàn khách sạn"
                    : $"Tra cứu nhân sự — bộ phận {tenPhongBanGioiHan ?? "của bạn"}",
                PeriodLabel = "Dữ liệu hiện hành",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildDirectoryNotes(),
                Directory = new AiDataDirectoryDto
                {
                    TuKhoa = keyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    DanhSach = [.. rows.Select(MapNhanVien)]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu nhân sự nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu hợp đồng lao động theo từ khóa (tên nhân viên, số hợp đồng, loại hợp đồng).
    ///
    /// <para>
    /// <b>Phạm vi dùng lại đúng luật báo cáo</b> như <see cref="BuildDirectoryAsync"/>:
    /// Admin/HR/IT xem toàn khách sạn, Quản lý chỉ bộ phận mình, Employee bị từ chối. Từ khóa
    /// chỉ thu hẹp kết quả — không thể mở rộng phạm vi quyền.
    /// </para>
    ///
    /// <para>
    /// <b>Chỉ trả thông tin lao động</b> (số hợp đồng, loại, ngày hiệu lực, lương cơ bản, trạng
    /// thái hiệu lực, tên/phòng ban/chức vụ của nhân viên sở hữu). Kết quả đi vào ngữ cảnh của
    /// mô hình ngôn ngữ, nên CMND, số điện thoại, email, địa chỉ và số tài khoản ngân hàng cố ý
    /// không được đưa vào.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildContractAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var scope = ReportPolicy.ResolveScope(role, ReadInt(principal, AiContextTokenService.DepartmentClaim));

        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        // Từ khóa trống nghĩa là hỏi chung ("hợp đồng nào còn hiệu lực?", "hợp đồng sắp hết
        // hạn nào?"): trả danh sách mặc định thay vì từ chối. Keyword rỗn không mở rộng quyền.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            var pattern = $"%{keyword}%";
            var hasContractCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var contractCode);
            var today = DateOnly.FromDateTime(DateTime.Today);

            // EF Core 8+: .Include() returns IIncludableQueryable which loses its Include type
            // after .Where(), so keep the base query as plain IQueryable and load the NhanVien
            // navigation by projecting it directly in the final Select below.
            IQueryable<OvertimeHotel.HRM.Core.Models.HopDong> query =
                _context.HopDongs.AsNoTracking().AsQueryable();

            // Quản lý bộ phận bị khóa trong phòng ban của mình ngay từ truy vấn, cùng cách lọc
            // mà trang báo cáo và tra cứu nhân sự áp dụng.
            if (!scope.IsWholeHotel)
            {
                query = query.Where(h => h.NhanVien != null
                    && h.NhanVien.MaPhongBan == scope.DepartmentId);
            }

            // Cột loai_hop_dong lưu mã chuẩn hoá ("XAC_DINH_THOI_HAN_3_NAM") nên tìm theo nhãn
            // tiếng Việt ("3 năm") không khớp bằng ILIKE. ContractTypes.MatchingCodes dịch từ
            // khóa thành danh sách mã khớp, đẩy vào EF dưới dạng list.Contains() để khớp cả mã
            // lẫn nhãn — đúng cách mà trang Quản lý Hợp đồng đang dùng.
            var matchingTypeCodes = ContractTypes.MatchingCodes(keyword);

            // Trạng thái hiệu lực nhắc tới bằng tiếng Việt ("còn hiệu lực", "đã hết hạn") — phải
            // dịch sang điều kiện ngày tháng trên cột, vì trạng thái được tính lúc runtime.
            var normalizedKeyword = keyword.Trim().ToLowerInvariant();
            var wantsExpiringSoon = (normalizedKeyword.Contains("sắp hết hạn", StringComparison.Ordinal)
                || normalizedKeyword.Contains("sap het han", StringComparison.Ordinal)
                || normalizedKeyword.Contains("sap_het_han", StringComparison.Ordinal)
                || normalizedKeyword.Contains("sắp hết", StringComparison.Ordinal)
                || normalizedKeyword.Contains("sap het", StringComparison.Ordinal)
                || normalizedKeyword.Contains("sap_het", StringComparison.Ordinal))
                && hasKeyword;
            var wantsExpired = (normalizedKeyword.Contains("đã hết hạn", StringComparison.Ordinal)
                || normalizedKeyword.Contains("da het han", StringComparison.Ordinal)
                || normalizedKeyword.Contains("da_het_han", StringComparison.Ordinal))
                && hasKeyword;
            var wantsActive = (normalizedKeyword.Contains("còn hiệu lực", StringComparison.Ordinal)
                || normalizedKeyword.Contains("con hieu luc", StringComparison.Ordinal)
                || normalizedKeyword.Contains("con_hieu_luc", StringComparison.Ordinal)
                || normalizedKeyword.Contains("đang hiệu lực", StringComparison.Ordinal)
                || normalizedKeyword.Contains("dang hieu luc", StringComparison.Ordinal)
                || normalizedKeyword.Contains("dang_hieu_luc", StringComparison.Ordinal))
                && hasKeyword;
            var wantsIndefinite = (normalizedKeyword.Contains("không xác định", StringComparison.Ordinal)
                || normalizedKeyword.Contains("khong xac dinh", StringComparison.Ordinal)
                || normalizedKeyword.Contains("khong_xac_dinh", StringComparison.Ordinal))
                && hasKeyword;

            if (hasKeyword)
            {
                query = query.Where(h =>
                    (h.NhanVien != null && (EF.Functions.ILike(h.NhanVien.Ho + " " + h.NhanVien.Ten, pattern)
                        || EF.Functions.ILike(h.NhanVien.Ho, pattern)
                        || EF.Functions.ILike(h.NhanVien.Ten, pattern)))
                    || EF.Functions.ILike(h.SoHopDong, pattern)
                    || EF.Functions.ILike(h.LoaiHopDong, pattern)
                    || (matchingTypeCodes.Count > 0 && matchingTypeCodes.Contains(h.LoaiHopDong))
                    || (hasContractCode && h.MaHopDong == contractCode)
                    // Sắp hết hạn: đã bắt đầu, chưa qua ngày kết thúc, còn trong 30 ngày.
                    || (wantsExpiringSoon && h.NgayBatDau <= today && h.NgayKetThuc != null
                        && h.NgayKetThuc >= today
                        && h.NgayKetThuc.Value.DayNumber - today.DayNumber <= 30)
                    || (wantsExpired && h.NgayKetThuc != null && h.NgayKetThuc < today)
                    || (wantsActive && h.NgayBatDau <= today && (h.NgayKetThuc == null || h.NgayKetThuc >= today))
                    || (wantsIndefinite && h.NgayKetThuc == null));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                // Trả một khối Contract rỗng thay vì 404: "không tìm thấy hợp đồng nào khớp từ
                // khóa" là một câu trả lời nghiệp vụ hợp lệ, không phải lỗi.
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = scope.IsWholeHotel
                        ? "Tra cứu hợp đồng — toàn khách sạn"
                        : "Tra cứu hợp đồng — bộ phận của bạn",
                    PeriodLabel = "Dữ liệu hiện hành",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildContractNotes(),
                    Contract = new AiDataContractDto
                    {
                        TuKhoa = keyword,
                        TongSo = 0,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderByDescending(h => h.NgayBatDau)
                .ThenBy(h => h.MaHopDong)
                .Take(ContractResultLimit)
                .Select(h => new
                {
                    h.MaHopDong,
                    h.SoHopDong,
                    h.LoaiHopDong,
                    h.NgayBatDau,
                    h.NgayKetThuc,
                    h.LuongCoBan,
                    h.TrangThai,
                    h.HinhThucHopDong,
                    h.MaChucVuKyKet,
                    h.MaPhongBanKyKet,
                    PhuLucLuongs = h.PhuLucLuongs.ToList(),
                    HoTen = h.NhanVien == null ? null : h.NhanVien.Ho + " " + h.NhanVien.Ten,
                    PhongBan = h.NhanVien == null || h.NhanVien.PhongBan == null
                        ? null
                        : h.NhanVien.PhongBan.TenPhongBan,
                    ChucDanh = h.NhanVien == null || h.NhanVien.ChucVu == null
                        ? null
                        : h.NhanVien.ChucVu.TenChucVu
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var tenPhongBanGioiHan = scope.IsWholeHotel
                ? null
                : await _context.PhongBans
                    .AsNoTracking()
                    .Where(p => p.MaPhongBan == scope.DepartmentId)
                    .Select(p => p.TenPhongBan)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

            var todayForState = DateOnly.FromDateTime(DateTime.Today);
            return Ok(new AiDataDigestDto
            {
                ScopeLabel = scope.IsWholeHotel
                    ? "Tra cứu hợp đồng — toàn khách sạn"
                    : $"Tra cứu hợp đồng — bộ phận {tenPhongBanGioiHan ?? "của bạn"}",
                PeriodLabel = "Dữ liệu hiện hành",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildContractNotes(),
                Contract = new AiDataContractDto
                {
                    TuKhoa = keyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    DanhSach = [.. rows.Select(r => new AiDataHopDongItemDto
                    {
                        MaHopDong = r.MaHopDong,
                        SoHopDong = r.SoHopDong,
                        HoTen = r.HoTen ?? string.Empty,
                        PhongBan = r.PhongBan,
                        ChucDanh = r.ChucDanh,
                        LoaiHopDong = ContractTypes.ToDisplayName(r.LoaiHopDong)
                            ?? string.Empty,
                        NgayBatDau = r.NgayBatDau,
                        NgayKetThuc = r.NgayKetThuc,
                        LuongCoBan = r.LuongCoBan,
                        LuongHieuLuc = EffectiveSalaryResolver.Resolve([new OvertimeHotel.HRM.Core.Models.HopDong
                        {
                            MaHopDong = r.MaHopDong, NgayBatDau = r.NgayBatDau, NgayKetThuc = r.NgayKetThuc,
                            TrangThai = r.TrangThai, LoaiHopDong = r.LoaiHopDong, HinhThucHopDong = r.HinhThucHopDong,
                            LuongCoBan = r.LuongCoBan, MaChucVuKyKet = r.MaChucVuKyKet,
                            MaPhongBanKyKet = r.MaPhongBanKyKet, PhuLucLuongs = r.PhuLucLuongs
                        }], todayForState)?.MonthlySalary,
                        NgayThamChieuLuong = todayForState,
                        TrangThai = ResolveContractState(
                            new OvertimeHotel.HRM.Core.Models.HopDong
                            {
                                TrangThai = r.TrangThai,
                                NgayBatDau = r.NgayBatDau,
                                NgayKetThuc = r.NgayKetThuc
                            },
                            todayForState)
                    })]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu hợp đồng nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu đơn từ theo từ khóa (tên nhân viên nộp đơn hoặc tên loại đơn).
    ///
    /// <para>
    /// <b>Phạm vi dùng lại đúng luật báo cáo</b> như <see cref="BuildDirectoryAsync"/>:
    /// Admin/HR/IT xem toàn khách sạn, Quản lý chỉ bộ phận mình, Employee bị từ chối. Từ khóa
    /// chỉ thu hẹp kết quả — không thể mở rộng phạm vi quyền.
    /// </para>
    ///
    /// <para>
    /// Chỉ trả thông tin trình duyệt (mã đơn, loại đơn, tên người nộp, khoảng ngày nghỉ, trạng
    /// thái, ngày gửi, lý do, phản hồi duyệt). Không trả số điện thoại, email, địa chỉ.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildLeaveAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var scope = ReportPolicy.ResolveScope(role, ReadInt(principal, AiContextTokenService.DepartmentClaim));

        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        // Từ khóa trống nghĩa là hỏi chung ("đơn nào đang chờ duyệt?", "đơn nghỉ phép nào
        // trong tuần này?"): trả danh sách mặc định thay vì từ chối. Keyword rỗn không mở rộng quyền.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            var pattern = $"%{keyword}%";
            var hasDonCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var donCode);

            // Trạng thái trình duyệt do người dùng nhắc tới bằng tiếng Việt ("chờ duyệt", "đã
            // duyệt", "từ chối") — cột trang_thai lưu mã nên phải dịch từ khóa sang mã trước khi
            // so, nếu không câu "đơn nào đang chờ duyệt" sẽ ra 0 kết quả trong khi có đơn chờ thật.
            var normalizedKeyword = keyword.Trim().ToLowerInvariant();
            var wantsPending = normalizedKeyword.Contains("chờ duyệt", StringComparison.Ordinal)
                || normalizedKeyword.Contains("cho duyet", StringComparison.Ordinal)
                || normalizedKeyword.Contains("cho_duyet", StringComparison.Ordinal)
                || normalizedKeyword.Contains("chưa duyệt", StringComparison.Ordinal);
            var wantsApproved = normalizedKeyword.Contains("đã duyệt", StringComparison.Ordinal)
                || normalizedKeyword.Contains("da duyet", StringComparison.Ordinal)
                || normalizedKeyword.Contains("da_duyet", StringComparison.Ordinal);
            var wantsRejected = normalizedKeyword.Contains("từ chối", StringComparison.Ordinal)
                || normalizedKeyword.Contains("tu choi", StringComparison.Ordinal)
                || normalizedKeyword.Contains("tu_choi", StringComparison.Ordinal);

            IQueryable<OvertimeHotel.HRM.Core.Models.DonTu> query =
                _context.DonTus.AsNoTracking().AsQueryable();

            // Quản lý bộ phận bị khóa trong phòng ban của mình ngay từ truy vấn, cùng cách lọc
            // mà trang Đơn từ và tra cứu nhân sự áp dụng.
            if (!scope.IsWholeHotel)
            {
                query = query.Where(d => d.NhanVien != null
                    && d.NhanVien.MaPhongBan == scope.DepartmentId);
            }

            if (hasKeyword)
            {
                query = query.Where(d =>
                    (d.NhanVien != null && (EF.Functions.ILike(d.NhanVien.Ho + " " + d.NhanVien.Ten, pattern)
                        || EF.Functions.ILike(d.NhanVien.Ho, pattern)
                        || EF.Functions.ILike(d.NhanVien.Ten, pattern)))
                    || (d.LoaiDon != null && EF.Functions.ILike(d.LoaiDon.TenLoaiDon, pattern))
                    || (hasDonCode && d.MaDon == donCode)
                    || (wantsPending && d.TrangThai == "CHO_DUYET")
                    || (wantsApproved && d.TrangThai == "DA_DUYET")
                    || (wantsRejected && d.TrangThai == "TU_CHOI"));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = scope.IsWholeHotel
                        ? "Tra cứu đơn từ — toàn khách sạn"
                        : "Tra cứu đơn từ — bộ phận của bạn",
                    PeriodLabel = "Dữ liệu hiện hành",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildLeaveNotes(),
                    Leave = new AiDataLeaveDto
                    {
                        TuKhoa = keyword,
                        TongSo = 0,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderByDescending(d => d.TuNgay)
                .ThenByDescending(d => d.MaDon)
                .Take(LeaveResultLimit)
                .Select(d => new
                {
                    d.MaDon,
                    LoaiDon = d.LoaiDon == null ? string.Empty : d.LoaiDon.TenLoaiDon,
                    HoTen = d.NhanVien == null ? string.Empty : d.NhanVien.Ho + " " + d.NhanVien.Ten,
                    PhongBan = d.NhanVien == null || d.NhanVien.PhongBan == null
                        ? null
                        : d.NhanVien.PhongBan.TenPhongBan,
                    d.TuNgay,
                    d.DenNgay,
                    d.TrangThai,
                    NgayGui = d.NgayGui.Date,
                    d.LyDo,
                    d.PhanHoiDuyet
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var tenPhongBanGioiHan = scope.IsWholeHotel
                ? null
                : await _context.PhongBans
                    .AsNoTracking()
                    .Where(p => p.MaPhongBan == scope.DepartmentId)
                    .Select(p => p.TenPhongBan)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return Ok(new AiDataDigestDto
            {
                ScopeLabel = scope.IsWholeHotel
                    ? "Tra cứu đơn từ — toàn khách sạn"
                    : $"Tra cứu đơn từ — bộ phận {tenPhongBanGioiHan ?? "của bạn"}",
                PeriodLabel = "Dữ liệu hiện hành",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildLeaveNotes(),
                Leave = new AiDataLeaveDto
                {
                    TuKhoa = keyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    DanhSach = [.. rows.Select(r => new AiDataDonTuItemDto
                    {
                        MaDon = r.MaDon,
                        LoaiDon = r.LoaiDon,
                        HoTen = r.HoTen,
                        PhongBan = r.PhongBan,
                        TuNgay = r.TuNgay,
                        DenNgay = r.DenNgay,
                        SoNgayNghi = r.DenNgay.DayNumber - r.TuNgay.DayNumber + 1,
                        TrangThai = ResolveLeaveStatus(r.TrangThai),
                        NgayGui = DateOnly.FromDateTime(r.NgayGui),
                        LyDo = string.IsNullOrWhiteSpace(r.LyDo) ? null : r.LyDo.Trim(),
                        PhanHoiDuyet = string.IsNullOrWhiteSpace(r.PhanHoiDuyet) ? null : r.PhanHoiDuyet.Trim()
                    })]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu đơn từ nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu phân ca (lịch làm việc) theo từ khóa (tên nhân viên hoặc tên ca).
    ///
    /// <para>
    /// <b>Khoảng ngày tra cứu:</b> tuần hiện tại (thứ Hai → Chủ Nhật) theo lịch máy chủ. Không
    /// nhận khoảng ngày từ yêu cầu vì người gọi không được tự định nghĩa phạm vi dữ liệu; kỳ hiển
    /// thị là do máy chủ phát ra.
    /// </para>
    ///
    /// <para>
    /// <b>Phạm vi dùng lại đúng luật báo cáo</b> như <see cref="BuildDirectoryAsync"/>:
    /// Admin/HR/IT xem toàn khách sạn, Quản lý chỉ bộ phận mình, Employee bị từ chối.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildShiftAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var scope = ReportPolicy.ResolveScope(role, ReadInt(principal, AiContextTokenService.DepartmentClaim));

        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        // Từ khóa trống nghĩa là hỏi chung ("hôm nay ai làm việc?", "tuần này có ca nào?"):
        // trả toàn bộ lịch tuần hiện tại thay vì từ chối. Keyword rỗn không mở rộng quyền.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            // Tuần hiện tại: thứ Hai của tuần này → Chủ Nhật. Dùng lịch máy chủ để mọi
            // kết quả nhất quán với màn hình lịch đang mở của người dùng.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);

            var pattern = $"%{keyword}%";
            var hasPhanCaCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var phanCaCode);

            // Ca qua đêm: "ca đêm", "đêm", "night", "quadem", "qua dem"
            var normalizedKeyword = keyword.Trim().ToLowerInvariant();
            var wantsNightShift = normalizedKeyword.Contains("đêm", StringComparison.Ordinal)
                || normalizedKeyword.Contains("dem", StringComparison.Ordinal)
                || normalizedKeyword.Contains("night", StringComparison.Ordinal)
                || normalizedKeyword.Contains("qua dem", StringComparison.Ordinal)
                || normalizedKeyword.Contains("qua_dem", StringComparison.Ordinal);

            IQueryable<OvertimeHotel.HRM.Core.Models.PhanCa> query =
                _context.PhanCas.AsNoTracking().AsQueryable();

            // Quản lý bộ phận bị khóa trong phòng ban của mình ngay từ truy vấn.
            if (!scope.IsWholeHotel)
            {
                query = query.Where(p => p.NhanVien != null
                    && p.NhanVien.MaPhongBan == scope.DepartmentId);
            }

            query = query.Where(p => p.NgayLamViec >= monday && p.NgayLamViec <= sunday);

            // Hỏi chung (không từ khóa) → trả toàn bộ lịch tuần hiện tại; hỏi rõ → thu hẹp.
            if (hasKeyword)
            {
                query = query.Where(p =>
                    (p.NhanVien != null && (EF.Functions.ILike(p.NhanVien.Ho + " " + p.NhanVien.Ten, pattern)
                        || EF.Functions.ILike(p.NhanVien.Ho, pattern)
                        || EF.Functions.ILike(p.NhanVien.Ten, pattern)))
                    || (p.CaLamViec != null && EF.Functions.ILike(p.CaLamViec.TenCa, pattern))
                    || (hasPhanCaCode && p.MaPhanCa == phanCaCode)
                    || (wantsNightShift && p.CaLamViec != null && p.CaLamViec.QuaDem));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = scope.IsWholeHotel
                        ? "Tra cứu phân ca — toàn khách sạn"
                        : "Tra cứu phân ca — bộ phận của bạn",
                    PeriodLabel = $"Tuần {monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy}",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildShiftNotes(),
                    Shift = new AiDataShiftDto
                    {
                        TuKhoa = keyword,
                        TongSo = 0,
                        TuNgay = monday,
                        DenNgay = sunday,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.MaPhanCa)
                .Take(ShiftResultLimit)
                .Select(p => new
                {
                    p.MaPhanCa,
                    p.NgayLamViec,
                    HoTen = p.NhanVien == null ? string.Empty : p.NhanVien.Ho + " " + p.NhanVien.Ten,
                    PhongBan = p.NhanVien == null || p.NhanVien.PhongBan == null
                        ? null
                        : p.NhanVien.PhongBan.TenPhongBan,
                    TenCa = p.CaLamViec == null ? string.Empty : p.CaLamViec.TenCa,
                    GioBatDau = p.CaLamViec == null ? string.Empty : p.CaLamViec.GioBatDau.ToString("HH:mm"),
                    GioKetThuc = p.CaLamViec == null ? string.Empty : p.CaLamViec.GioKetThuc.ToString("HH:mm"),
                    QuaDem = p.CaLamViec != null && p.CaLamViec.QuaDem,
                    p.TrangThai,
                    p.GioTangCaDuKien
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var tenPhongBanGioiHan = scope.IsWholeHotel
                ? null
                : await _context.PhongBans
                    .AsNoTracking()
                    .Where(pb => pb.MaPhongBan == scope.DepartmentId)
                    .Select(pb => pb.TenPhongBan)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return Ok(new AiDataDigestDto
            {
                ScopeLabel = scope.IsWholeHotel
                    ? "Tra cứu phân ca — toàn khách sạn"
                    : $"Tra cứu phân ca — bộ phận {tenPhongBanGioiHan ?? "của bạn"}",
                PeriodLabel = $"Tuần {monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy}",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildShiftNotes(),
                Shift = new AiDataShiftDto
                {
                    TuKhoa = keyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    TuNgay = monday,
                    DenNgay = sunday,
                    DanhSach = [.. rows.Select(r => new AiDataPhanCaItemDto
                    {
                        MaPhanCa = r.MaPhanCa,
                        NgayLamViec = r.NgayLamViec,
                        HoTen = r.HoTen,
                        PhongBan = r.PhongBan,
                        TenCa = r.TenCa,
                        GioBatDau = r.GioBatDau,
                        GioKetThuc = r.GioKetThuc,
                        QuaDem = r.QuaDem,
                        TrangThai = ResolveShiftStatus(r.TrangThai),
                        GioTangCaDuKien = r.GioTangCaDuKien
                    })]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu phân ca nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu chấm công (quẹt thẻ) theo từ khóa (tên nhân viên, tên ca, hoặc trạng thái).
    ///
    /// <para>
    /// <b>Khoảng ngày tra cứu:</b> tuần hiện tại (thứ Hai → Chủ Nhật) theo lịch máy chủ,
    /// giống <see cref="BuildShiftAsync"/>. Không nhận khoảng ngày từ yêu cầu.
    /// </para>
    ///
    /// <para>
    /// <b>Phạm vi gần với bảng công cá nhân nhưng mở rộng theo vai trò:</b>
    /// Admin/HR/IT xem toàn khách sạn; Manager chỉ bộ phận mình; Employee — chỉ của
    /// chính mình (dùng claim mã nhân viên từ vé). Đây là điểm khác với
    /// directory/contract/leave/shift nơi Employee bị từ chối hẳn — ở phạm vi chấm công,
    /// Employee vẫn được xem bảng công của mình để hỗ trợ câu "tôi hôm nay có đi trễ
    /// không?", nhưng KHÔNG bao giờ thấy của người khác.
    /// </para>
    ///
    /// <para>
    /// <b>Chỉ trả thông tin cơ bản:</b> giờ quẹt vào/ra, phút đi trễ/về sớm, giờ OT,
    /// trạng thái (Đúng giờ/Đi trễ/Về sớm/Trễ và sớm/Vắng mặt/Chưa quẹt thẻ).
    /// Cố ý không trả tọa độ GPS, thiết bị quẹt, phương thức check-in.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildAttendanceAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var viewerEmployeeId = ReadInt(principal, AiContextTokenService.EmployeeClaim);
        var viewerDeptId = ReadInt(principal, AiContextTokenService.DepartmentClaim);

        // Nhân viên (Employee) được xem chấm công của chính mình. Admin/HR/IT toàn khách
        // sạn, Manager bộ phận mình. Không dùng ReportPolicy.ResolveScope ở đây vì nó từ
        // chối Employee — chính sách chấm công là "xem được của mình", không phải "bị cấm".
        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        var isWholeHotel = normalizedRole is RoleType.Admin or RoleType.HR or RoleType.IT;
        var isManager = normalizedRole is RoleType.Manager;
        var isEmployee = normalizedRole is RoleType.Employee;

        if (isManager && viewerDeptId is not > 0) return Forbid();
        if (isEmployee && viewerEmployeeId is not > 0) return Forbid();
        if (!isWholeHotel && !isManager && !isEmployee) return Forbid();

        // Từ khóa trống nghĩa là hỏi chung ("hôm nay ai đi trễ?", "ai đã quẹt thẻ?"):
        // trả danh sách mặc định trong tuần hiện tại thay vì từ chối.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);

            var pattern = $"%{keyword}%";
            var hasPhanCaCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var phanCaCode);

            // Trạng thái chấm công nhắc bằng tiếng Việt — dịch sang cột TrangThai.
            var normalizedKeyword = keyword.Trim().ToLowerInvariant();
            var wantsLate = (normalizedKeyword.Contains("đi trễ", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("di tre", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("di_tre", StringComparison.Ordinal))
                && hasKeyword;
            var wantsEarly = (normalizedKeyword.Contains("về sớm", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("ve som", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("ve_som", StringComparison.Ordinal))
                && hasKeyword;
            var wantsOnTime = (normalizedKeyword.Contains("đúng giờ", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("dung gio", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("dung_gio", StringComparison.Ordinal))
                && hasKeyword;
            var wantsAbsent = (normalizedKeyword.Contains("vắng", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("vang", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("vắng mặt", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("vang mat", StringComparison.Ordinal))
                && hasKeyword;
            var wantsNotCheckedIn = (normalizedKeyword.Contains("chưa quẹt", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("chua quet", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("chưa check", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("chua_check", StringComparison.Ordinal))
                && hasKeyword;
            var wantsOT = (normalizedKeyword.Contains("tăng ca", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("tang ca", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("tang_ca", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("ot", StringComparison.Ordinal))
                && hasKeyword;

            IQueryable<OvertimeHotel.HRM.Core.Models.PhanCa> query =
                _context.PhanCas.AsNoTracking().AsQueryable();

            // Khóa theo phạm vi ngay từ truy vấn.
            if (isWholeHotel)
            {
                // toàn khách sạn — không lọc thêm
            }
            else if (isManager)
            {
                query = query.Where(p => p.NhanVien != null
                    && p.NhanVien.MaPhongBan == viewerDeptId);
            }
            else // isEmployee
            {
                query = query.Where(p => p.MaNhanVien == viewerEmployeeId!.Value);
            }

            query = query.Where(p => p.NgayLamViec >= monday && p.NgayLamViec <= sunday);

            // Hỏi chung (không từ khóa) → trả toàn bộ chấm công trong tuần; hỏi rõ → thu hẹp.
            if (hasKeyword)
            {
                query = query.Where(p =>
                    (p.NhanVien != null && (EF.Functions.ILike(p.NhanVien.Ho + " " + p.NhanVien.Ten, pattern)
                        || EF.Functions.ILike(p.NhanVien.Ho, pattern)
                        || EF.Functions.ILike(p.NhanVien.Ten, pattern)))
                    || (p.CaLamViec != null && EF.Functions.ILike(p.CaLamViec.TenCa, pattern))
                    || (hasPhanCaCode && p.MaPhanCa == phanCaCode)
                    // Trạng thái: cột ChamCong.TrangThai lưu mã; ca chưa quẹt thẻ là NULL.
                    || (wantsLate && p.ChamCong != null && (p.ChamCong.TrangThai == "DI_TRE" || p.ChamCong.TrangThai == "TRE_VA_SOM"))
                    || (wantsEarly && p.ChamCong != null && (p.ChamCong.TrangThai == "VE_SOM" || p.ChamCong.TrangThai == "TRE_VA_SOM"))
                    || (wantsOnTime && p.ChamCong != null && p.ChamCong.TrangThai == "DUNG_GIO")
                    || (wantsAbsent && p.ChamCong != null && p.ChamCong.TrangThai == "VANG_MAT")
                    || (wantsNotCheckedIn && (p.ChamCong == null || p.ChamCong.GioCheckIn == null))
                    || (wantsOT && p.ChamCong != null && p.ChamCong.GioTangCaOt > 0));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = isWholeHotel
                        ? "Tra cứu chấm công — toàn khách sạn"
                        : isManager ? "Tra cứu chấm công — bộ phận của bạn"
                        : "Tra cứu chấm công — của chính bạn",
                    PeriodLabel = $"Tuần {monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy}",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildAttendanceNotes(),
                    Attendance = new AiDataAttendanceDto
                    {
                        TuKhoa = keyword,
                        TongSo = 0,
                        TuNgay = monday,
                        DenNgay = sunday,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.MaPhanCa)
                .Take(AttendanceResultLimit)
                .Select(p => new
                {
                    p.MaPhanCa,
                    p.NgayLamViec,
                    HoTen = p.NhanVien == null ? string.Empty : p.NhanVien.Ho + " " + p.NhanVien.Ten,
                    PhongBan = p.NhanVien == null || p.NhanVien.PhongBan == null
                        ? null
                        : p.NhanVien.PhongBan.TenPhongBan,
                    TenCa = p.CaLamViec == null ? string.Empty : p.CaLamViec.TenCa,
                    GioCheckIn = p.ChamCong != null ? p.ChamCong.GioCheckIn : null,
                    GioCheckOut = p.ChamCong != null ? p.ChamCong.GioCheckOut : null,
                    PhutDiTre = p.ChamCong != null ? p.ChamCong.PhutDiTre : 0,
                    PhutVeSom = p.ChamCong != null ? p.ChamCong.PhutVeSom : 0,
                    GioTangCaOt = p.ChamCong != null ? p.ChamCong.GioTangCaOt : 0m,
                    TrangThai = p.ChamCong != null ? p.ChamCong.TrangThai : null
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            var tenPhongBanGioiHan = isManager
                ? await _context.PhongBans
                    .AsNoTracking()
                    .Where(pb => pb.MaPhongBan == viewerDeptId)
                    .Select(pb => pb.TenPhongBan)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted)
                : null;

            return Ok(new AiDataDigestDto
            {
                ScopeLabel = isWholeHotel
                    ? "Tra cứu chấm công — toàn khách sạn"
                    : isManager ? $"Tra cứu chấm công — bộ phận {tenPhongBanGioiHan ?? "của bạn"}"
                    : "Tra cứu chấm công — của chính bạn",
                PeriodLabel = $"Tuần {monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy}",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildAttendanceNotes(),
                Attendance = new AiDataAttendanceDto
                {
                    TuKhoa = keyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    TuNgay = monday,
                    DenNgay = sunday,
                    DanhSach = [.. rows.Select(r => new AiDataChamCongItemDto
                    {
                        MaPhanCa = r.MaPhanCa,
                        NgayLamViec = r.NgayLamViec,
                        HoTen = r.HoTen,
                        PhongBan = r.PhongBan,
                        TenCa = r.TenCa,
                        GioCheckIn = r.GioCheckIn?.ToString("HH:mm"),
                        GioCheckOut = r.GioCheckOut?.ToString("HH:mm"),
                        PhutDiTre = r.PhutDiTre,
                        PhutVeSom = r.PhutVeSom,
                        GioTangCaOt = r.GioTangCaOt,
                        TrangThai = ResolveAttendanceStatus(r.TrangThai)
                    })]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu chấm công nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// Tra cứu sự kiện / dự án (DuAn + PhanCongDuAn) theo từ khóa (tên sự kiện, loại sự kiện,
    /// trạng thái, hoặc tên nhân sự tham gia).
    ///
    /// <para>
    /// <b>Khoảng ngày tra cứu:</b> mặc định lấy sự kiện liên quan tới hôm nay — 30 ngày vừa qua
    /// đến 90 ngày tới, để "hỏi chung" có ý nghĩa ("sắp có sự kiện gì?" lẫn "hôm qua có sự kiện
    /// gì?"). Không nhận khoảng ngày từ yêu cầu.
    /// </para>
    /// <para>
    /// <b>Phạm vi gần với bảng công cá nhân nhưng mở rộng theo vai trò:</b>
    /// Admin/HR/IT xem toàn khách sạn; Manager chỉ sự kiện có ít nhất một người của bộ phận mình
    /// tham gia; Employee chỉ sự kiện mình tham gia. Lý do: sự kiện liên quan nhiều phòng ban,
    /// nên "thuộc bộ phận" không sạch như chấm công.
    /// </para>
    /// <para>
    /// <b>Chỉ trả thông tin cơ bản:</b> tên, loại, địa điểm, thời gian, trạng thái, số nhân sự
    /// tham gia. Cố ý không trả ngân sách phụ cấp, số tiền phụ cấp từng người, lý do hủy,
    /// hay ghi chú nghiệm thu.
    /// </para>
    /// </summary>
    private async Task<IActionResult> BuildEventAsync(ClaimsPrincipal principal, AiDataRequest request)
    {
        if (!await IsAccountActiveAsync(principal))
        {
            return Unauthorized(new { message = "Tài khoản không còn hiệu lực." });
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        var viewerEmployeeId = ReadInt(principal, AiContextTokenService.EmployeeClaim);
        var viewerDeptId = ReadInt(principal, AiContextTokenService.DepartmentClaim);

        // Nhân viên (Employee) được xem sự kiện mình tham gia. Admin/HR/IT toàn khách sạn,
        // Manager sự kiện có nhân sự bộ phận mình. Không dùng ReportPolicy.ResolveScope
        // vì nó từ chối Employee — chính sách sự kiện là "xem được sự kiện mình tham gia".
        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        var isWholeHotel = normalizedRole is RoleType.Admin or RoleType.HR or RoleType.IT;
        var isManager = normalizedRole is RoleType.Manager;
        var isEmployee = normalizedRole is RoleType.Employee;

        if (isManager && viewerDeptId is not > 0) return Forbid();
        if (isEmployee && viewerEmployeeId is not > 0) return Forbid();
        if (!isWholeHotel && !isManager && !isEmployee) return Forbid();

        // Từ khóa trống nghĩa là hỏi chung ("sắp có sự kiện gì?"): trả danh sách mặc định
        // các sự kiện sắp diễn ra thay vì từ chối.
        var keyword = (request.Keyword ?? string.Empty).Trim();
        var hasKeyword = keyword.Length > 0;

        try
        {
            var todayVat = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
            var windowStartUtc = HotelEventRules.ToUtc(DateOnly.FromDateTime(todayVat.DateTime).ToDateTime(TimeOnly.MinValue));
            var windowEndUtc = HotelEventRules.ToUtc(DateOnly.FromDateTime(todayVat.DateTime).AddDays(90).ToDateTime(TimeOnly.MinValue));

            var pattern = $"%{keyword}%";
            var hasDuAnCode = int.TryParse(keyword, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duAnCode);

            // Trạng thái sự kiện nhắc bằng tiếng Việt — dịch sang cột TrangThai.
            var normalizedKeyword = keyword.Trim().ToLowerInvariant();
            string? wantStatus = null;
            if (hasKeyword)
            {
                if (normalizedKeyword.Contains("đang diễn ra", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("dang dien ra", StringComparison.Ordinal))
                    wantStatus = "DANG_DIEN_RA";
                else if (normalizedKeyword.Contains("kế hoạch", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("ke hoach", StringComparison.Ordinal))
                    wantStatus = "LEN_KE_HOACH";
                else if (normalizedKeyword.Contains("chờ nghiệm thu", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("cho nghiem thu", StringComparison.Ordinal))
                    wantStatus = "CHO_NGHIEM_THU";
                else if (normalizedKeyword.Contains("hoàn thành", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("hoan thanh", StringComparison.Ordinal))
                    wantStatus = "HOAN_THANH";
                else if (normalizedKeyword.Contains("đã hủy", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("da huy", StringComparison.Ordinal))
                    wantStatus = "HUY";
            }

            // Loại sự kiện nhắc bằng tiếng Việt — dịch sang cột LoaiDuAn.
            string? wantType = null;
            if (hasKeyword)
            {
                if (normalizedKeyword.Contains("đám cưới", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("dam cuoi", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("tiệc cưới", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("tiec cuoi", StringComparison.Ordinal))
                    wantType = "TIEC_CUOI";
                else if (normalizedKeyword.Contains("hội nghị", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("hoi nghi", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("mice", StringComparison.Ordinal))
                    wantType = "HOI_NGHI_MICE";
                else if (normalizedKeyword.Contains("sinh nhật", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("sinh nhat", StringComparison.Ordinal))
                    wantType = "TIEC_SINH_NHAT";
                else if (normalizedKeyword.Contains("tất niên", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("tat nien", StringComparison.Ordinal))
                    wantType = "TIEC_TAT_NIEN";
                else if (normalizedKeyword.Contains("họp báo", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("hop bao", StringComparison.Ordinal))
                    wantType = "HOP_BAO";
                else if (normalizedKeyword.Contains("gala", StringComparison.Ordinal))
                    wantType = "GALA_DINNER";
                else if (normalizedKeyword.Contains("festival", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("ẩm thực", StringComparison.Ordinal)
                    || normalizedKeyword.Contains("am thuc", StringComparison.Ordinal))
                    wantType = "FESTIVAL_AM_THUC";
                else if (normalizedKeyword.Contains("vip", StringComparison.Ordinal))
                    wantType = "DOAN_VIP";
            }

            IQueryable<OvertimeHotel.HRM.Core.Models.DuAn> query =
                _context.DuAns.AsNoTracking().AsQueryable();

            // Cửa sổ mặc định: sự kiện liên quan tới "hôm nay" — hôm nay và 90 ngày tới, HOẶC
            // 30 ngày vừa qua. Sự kiện vừa diễn ra vẫn là thông tin người dùng muốn ("hôm qua có
            // sự kiện gì?"), và trạng thái như CHO_NGHIEM_THU chỉ xuất hiện sau khi kết thúc.
            var recentWindowStartUtc = HotelEventRules.ToUtc(DateOnly.FromDateTime(todayVat.DateTime).AddDays(-30).ToDateTime(TimeOnly.MinValue));
            query = query.Where(d => d.NgayKetThuc >= recentWindowStartUtc && d.NgayBatDau <= windowEndUtc);

            // Khóa theo phạm vi ngay từ truy vấn.
            if (isManager)
            {
                query = query.Where(d =>
                    d.PhanCongs.Any(p =>
                        p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.TrangThaiThamGia != "DA_HUY"
                        && p.NhanVien != null
                        && p.NhanVien.MaPhongBan == viewerDeptId));
            }
            else if (isEmployee)
            {
                query = query.Where(d =>
                    d.PhanCongs.Any(p =>
                        p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.TrangThaiThamGia != "DA_HUY"
                        && p.MaNhanVien == viewerEmployeeId!.Value));
            }

            // Từ khóa chỉ chứa từ chung ("sự kiện", "dự án", "sắp có sự kiện gì?") là hỏi tổng quan,
            // không phải tên sự kiện cụ thể — coi như không có từ khóa để trả danh sách mặc định.
            // Router hay trích cả cụm "sắp có sự kiện gì", nên phải kiểm "chứa" chứ không kiểm
            // "bằng" — từ khóa thật ("tiệc cưới", "gala") không chứa các từ chung này nên an toàn.
            var isGenericKeyword = hasKeyword && (
                normalizedKeyword.Contains("sự kiện", StringComparison.Ordinal)
                || normalizedKeyword.Contains("su kien", StringComparison.Ordinal)
                || normalizedKeyword.Contains("dự án", StringComparison.Ordinal)
                || normalizedKeyword.Contains("du an", StringComparison.Ordinal)
                || normalizedKeyword.Contains("hoạt động", StringComparison.Ordinal)
                || normalizedKeyword.Contains("hoat dong", StringComparison.Ordinal)
                || normalizedKeyword == "sap toi"
                || normalizedKeyword == "sắp tới");
            var effectiveKeyword = isGenericKeyword ? "" : keyword;
            var effectiveHasKeyword = isGenericKeyword ? false : hasKeyword;
            var effectivePattern = $"%{effectiveKeyword}%";

            // Hỏi chung (không từ khóa) → trả toàn bộ sự kiện trong cửa sổ; hỏi rõ → thu hẹp.
            if (effectiveHasKeyword)
            {
                query = query.Where(d =>
                    EF.Functions.ILike(d.TenDuAn, effectivePattern)
                    || EF.Functions.ILike(d.DiaDiem, effectivePattern)
                    || (hasDuAnCode && d.MaDuAn == duAnCode)
                    || (wantStatus != null && d.TrangThai == wantStatus)
                    || (wantType != null && d.LoaiDuAn == wantType)
                    || d.PhanCongs.Any(p =>
                        p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.TrangThaiThamGia != "DA_HUY"
                        && p.NhanVien != null
                        && (EF.Functions.ILike(p.NhanVien.Ho + " " + p.NhanVien.Ten, effectivePattern)
                            || EF.Functions.ILike(p.NhanVien.Ho, effectivePattern)
                            || EF.Functions.ILike(p.NhanVien.Ten, effectivePattern))));
            }

            var total = await query.CountAsync(HttpContext.RequestAborted);
            if (total == 0)
            {
                return Ok(new AiDataDigestDto
                {
                    ScopeLabel = isWholeHotel
                        ? "Tra cứu sự kiện / dự án — toàn khách sạn"
                        : isManager ? "Tra cứu sự kiện / dự án — sự kiện có bộ phận của bạn"
                        : "Tra cứu sự kiện / dự án — sự kiện bạn tham gia",
                    PeriodLabel = "Sự kiện gần đây và sắp tới",
                    RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                    GeneratedAt = DateTimeOffset.UtcNow,
                    GhiChu = BuildEventNotes(),
                    Event = new AiDataEventDto
                    {
                        TuKhoa = effectiveKeyword,
                        TongSo = 0,
                        DanhSach = []
                    }
                });
            }

            var rows = await query
                .OrderBy(d => d.NgayBatDau)
                .Take(EventResultLimit)
                .Select(d => new
                {
                    d.MaDuAn,
                    d.TenDuAn,
                    d.LoaiDuAn,
                    d.DiaDiem,
                    d.NgayBatDau,
                    d.NgayKetThuc,
                    d.TrangThai,
                    NhanSu = d.PhanCongs
                        .Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.TrangThaiThamGia != "DA_HUY"
                            && p.NhanVien != null)
                        .Select(p => p.NhanVien!.Ho + " " + p.NhanVien!.Ten)
                        .ToArray()
                })
                .ToArrayAsync(HttpContext.RequestAborted);

            return Ok(new AiDataDigestDto
            {
                ScopeLabel = isWholeHotel
                    ? "Tra cứu sự kiện / dự án — toàn khách sạn"
                    : isManager ? "Tra cứu sự kiện / dự án — sự kiện có bộ phận của bạn"
                    : "Tra cứu sự kiện / dự án — sự kiện bạn tham gia",
                PeriodLabel = "Sự kiện gần đây và sắp tới",
                RoleLabel = BaoCaoReportService.ResolveRoleLabel(role),
                GeneratedAt = DateTimeOffset.UtcNow,
                GhiChu = BuildEventNotes(),
                Event = new AiDataEventDto
                {
                    TuKhoa = effectiveKeyword,
                    TongSo = total,
                    BiGioiHan = total > rows.Length,
                    DanhSach = [.. rows.Select(r => new AiDataSuKienItemTraCuuDto
                    {
                        MaDuAn = r.MaDuAn,
                        TenDuAn = r.TenDuAn,
                        LoaiSuKien = HotelEventRules.TypeLabels.GetValueOrDefault(r.LoaiDuAn, r.LoaiDuAn),
                        DiaDiem = r.DiaDiem,
                        NgayBatDau = r.NgayBatDau,
                        NgayKetThuc = r.NgayKetThuc,
                        TrangThai = HotelEventRules.StatusLabels.GetValueOrDefault(r.TrangThai, r.TrangThai),
                        SoNhanSuThamGia = r.NhanSu.Length,
                        NhanSuThamGia = string.Join(", ", r.NhanSu.Take(5))
                    })]
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trợ lý AI tra cứu sự kiện nhưng không truy vấn được.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa lấy được dữ liệu từ hệ thống. Vui lòng thử lại sau."
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ÁNH XẠ SANG BẢN RÚT GỌN
    // ═══════════════════════════════════════════════════════════════════════

    private static AiDataBienDongDto MapBienDong(BienDongNhanSuReport source) => new()
    {
        DauKy = source.DauKy,
        CuoiKy = source.CuoiKy,
        TuyenMoi = source.TuyenMoi,
        ThoiViec = source.NghiViec,
        BienDongRong = source.BienDongRong,
        TyLeThoiViec = source.TyLeThoiViec,
        TheoPhongBan = [.. source.TheoPhongBan.Select(p => new AiDataPhongBanDto
        {
            PhongBan = p.PhongBan,
            QuanSo = p.QuanSo,
            TuyenMoi = p.TuyenMoi,
            ThoiViec = p.NghiViec
        })]
    };

    private static AiDataCoCauDto MapCoCau(CoCauNhanLucReport source) => new()
    {
        TongNhanSu = source.TongNhanSu,
        DangLam = source.DangLam,
        NghiPhep = source.NghiPhep,
        DaThoiViec = source.DaThoiViec,
        TheoPhongBan = MapPhanBo(source.TheoPhongBan),
        TheoGioiTinh = MapPhanBo(source.TheoGioiTinh),
        TheoChucVu = MapPhanBo(source.TheoChucVu)
    };

    private static AiDataHocVanDto MapHocVan(HocVanThamNienReport source) => new()
    {
        TyLeDaiHocTroLen = source.TyLeDaiHocTroLen,
        ThamNienTrungBinh = source.ThamNienTrungBinh,
        TheoTrinhDo = MapPhanBo(source.TheoTrinhDo),
        TheoThamNien = MapPhanBo(source.TheoThamNien)
    };

    private static AiDataQuyLuongDto MapQuyLuong(QuyLuongNganSachReport source) => new()
    {
        ThucChiThucNhan = source.ThucChi,
        ThucChiGop = source.ThucChiGross,
        DinhMucThamChieu = source.NganSachThamChieu,
        ChenhLech = source.ChenhLech,
        TyLeGiaiNgan = source.PhanTramGiaiNgan,
        VuotNguong = source.VuotNguong,
        SoThangTrongKy = source.SoThangTrongKy,
        TheoPhongBan = [.. source.TheoPhongBan.Select(p => new AiDataQuyLuongPhongBanDto
        {
            PhongBan = p.PhongBan,
            ThucChi = p.ThucChi,
            DinhMuc = p.NganSach
        })]
    };

    private static AiDataChuyenCanDto MapChuyenCan(ChuyenCanViPhamReport source) => new()
    {
        TongLuot = source.TongBanGhi,
        DungGio = source.DungGio,
        // "Trễ và sớm" gộp vào đi trễ: với người đọc, một lượt vừa trễ vừa sớm vẫn là đi trễ,
        // tách riêng sẽ khiến tổng các nhóm không khớp tổng lượt và mô hình sẽ tưởng thiếu dữ liệu.
        DiTre = source.DiTre + source.TreVaSom,
        VeSom = source.VeSom,
        Vang = source.Vang,
        TyLeChuyenCan = source.TyLeChuyenCan,
        TongPhutTre = source.TongPhutTre,
        TongPhutVeSom = source.TongPhutVeSom,
        TongGioOt = source.TongGioOt,
        TheoPhongBan = MapPhanBo(source.TheoPhongBan)
    };

    private static AiDataSuKienDto MapSuKien(HieuQuaSuKienReport source) => new()
    {
        TongSuKien = source.TongSuKien,
        DangDienRa = source.DangDienRa,
        DaHoanThanh = source.DaHoanThanh,
        TongLuotPhanCong = source.TongLuotPhanCong,
        TongNganSach = source.TongNganSach,
        TongPhuCapDaPhanBo = source.TongPhuCapDaPhanBo,
        TyLeXuatSac = source.TyLeXuatSac,
        DanhSach = [.. source.DanhSach.Select(s => new AiDataSuKienItemDto
        {
            Ten = s.TenDuAn,
            Loai = s.LoaiLabel,
            TrangThai = s.TrangThaiLabel,
            SoNhanSu = s.SoNhanSu,
            NganSach = s.NganSach,
            TyLeSuDungNganSach = s.TyLeSuDungNganSach
        })]
    };

    private static IReadOnlyList<AiDataNhanDto> MapPhanBo(IReadOnlyList<PhanBoItem> source) =>
        [.. source.Select(p => new AiDataNhanDto { Nhan = p.Nhan, GiaTri = p.GiaTri, TyLe = p.TyLe })];

    private static AiDataNhanVienDto MapNhanVien(AiDataNhanVienDto source) => new()
    {
        MaNhanVien = source.MaNhanVien,
        HoTen = source.HoTen,
        PhongBan = source.PhongBan,
        ChucDanh = source.ChucDanh,
        // Mã trạng thái dịch thành nhãn tiếng Việt ở đây, không để mô hình tự đoán mã CSDL.
        TrangThai = ResolveEmployeeStatus(source.TrangThai)
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  GHI CHÚ PHƯƠNG PHÁP
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ghi chú kèm theo số liệu báo cáo. Mô hình phải nhắc lại khi diễn giải để người dùng
    /// không đọc "định mức tham chiếu" thành "ngân sách đã được duyệt".
    /// </summary>
    private static List<string> BuildReportNotes() =>
    [
        "Số liệu tổng hợp từ phân hệ Báo cáo & Thống kê, không phải số liệu kế toán đã khóa sổ.",
        "Định mức tham chiếu của quỹ lương được suy ra từ hợp đồng lao động đang hiệu lực, "
            + "không phải hạn mức ngân sách đã được phê duyệt.",
        "Tỷ lệ phần trăm đã được làm tròn một chữ số thập phân; tổng các nhóm có thể lệch 0,1% so với tổng thể."
    ];

    /// <summary>Ghi chú kèm theo phiếu lương cá nhân.</summary>
    private static List<string> BuildPersonalNotes() =>
    [
        "Đây là số liệu phiếu lương của chính người đang hỏi, không bao gồm phiếu lương của người khác.",
        "Phiếu lương ở trạng thái chưa duyệt vẫn có thể thay đổi sau khi khóa sổ."
    ];

    /// <summary>Ghi chú kèm theo kết quả tra cứu nhân sự.</summary>
    private static List<string> BuildDirectoryNotes() =>
    [
        "Đây là kết quả tra cứu theo từ khóa, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ danh bạ nhân viên.",
        "Kết quả chỉ chứa thông tin cơ bản; tuyệt đối không suy diễn lương, số điện thoại hay địa chỉ của các nhân viên được nêu."
    ];

    /// <summary>Ghi chú kèm theo kết quả tra cứu hợp đồng lao động.</summary>
    private static List<string> BuildContractNotes() =>
    [
        "Đây là kết quả tra cứu hợp đồng theo từ khóa, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ kho hợp đồng.",
        "LuongCoBan là lương gốc ghi trên hợp đồng. LuongHieuLuc là lương hợp đồng/phụ lục tại NgayThamChieuLuong; null khi hợp đồng không hiệu lực tại ngày đó. Hai mức này không phải tổng thu nhập thực nhận; tổng thực nhận còn cộng phụ cấp, tăng ca, thưởng và trừ khấu trừ."
    ];

    /// <summary>Ghi chú kèm theo kết quả tra cứu đơn từ.</summary>
    private static List<string> BuildLeaveNotes() =>
    [
        "Đây là kết quả tra cứu đơn từ theo từ khóa, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ kho đơn từ.",
        "Chỉ trình duyệt đơn từ (loại đơn, khoảng ngày nghỉ, trạng thái) — không suy diễn lý do cá nhân ngoài những gì nhân viên đã ghi, không bịa ra phản hồi của người duyệt."
    ];

    /// <summary>Nhãn tiếng Việt của trạng thái trình duyệt đơn từ.</summary>
    private static string ResolveLeaveStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        _ => "Chưa xác định"
    };

    /// <summary>Ghi chú kèm theo kết quả tra cứu phân ca.</summary>
    private static List<string> BuildShiftNotes() =>
    [
        "Đây là kết quả tra cứu phân ca theo từ khóa trong tuần hiện tại, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ lịch phân ca.",
        "Chỉ trình duyệt lịch làm việc (ngày, tên ca, giờ bắt đầu/kết thúc) — không suy diễn tình trạng chấm công thực tế của nhân viên."
    ];

    /// <summary>Nhãn tiếng Việt của trạng thái phân ca.</summary>
    private static string ResolveShiftStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "DA_PHAN" => "Đã phân",
        "HOAN_THANH" => "Hoàn thành",
        "DA_CONG_BO" => "Đã công bố",
        "DA_HUY" => "Đã hủy",
        _ => "Chưa xác định"
    };

    /// <summary>Ghi chú kèm theo kết quả tra cứu sự kiện / dự án.</summary>
    private static List<string> BuildEventNotes() =>
    [
        "Đây là kết quả tra cứu sự kiện theo từ khóa, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ sự kiện của khách sạn.",
        "Chỉ trình duyệt tên, loại, địa điểm, thời gian, trạng thái và số nhân sự tham gia. Không suy diễn ngân sách phụ cấp, số tiền phụ cấp từng người, hay lý do hủy/đóng sự kiện ngoài những gì đã cho."
    ];

    private static List<string> BuildAttendanceNotes() =>
    [
        "Đây là kết quả tra cứu chấm công theo từ khóa trong tuần hiện tại, đã lọc theo phạm vi quyền của người hỏi — không phải toàn bộ bảng công.",
        "Chỉ trình duyệt giờ quẹt vào/ra, phút đi trễ/về sớm, giờ OT và trạng thái. Không suy diễn lý do đi trễ/về sớm ngoài những gì có trong dữ liệu, không bịa ra trạng thái khi chưa quẹt thẻ."
    ];

    /// <summary>
    /// Nhãn tiếng Việt của trạng thái chấm công. Ca chưa quẹt thẻ (null) là "Chưa quẹt thẻ",
    /// không phải "Đúng giờ" — mặc định DUNG_GIO trong model là giá trị khởi tạo, không phải kết quả.
    /// </summary>
    private static string ResolveAttendanceStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "DUNG_GIO" => "Đúng giờ",
        "DI_TRE" => "Đi trễ",
        "VE_SOM" => "Về sớm",
        "TRE_VA_SOM" => "Trễ và sớm",
        "VANG_MAT" => "Vắng mặt",
        null => "Chưa quẹt thẻ",
        _ => "Chưa quẹt thẻ"
    };

    /// <summary>Nhãn tiếng Việt của trạng thái phiếu lương.</summary>
    private static string ResolvePayslipStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "NHAP" => "Nháp",
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Bị từ chối",
        "KHOA_SO" => "Đã khóa sổ",
        _ => "Chưa xác định"
    };

    /// <summary>
    /// Nhãn tiếng Việt của trạng thái nhân viên. Gọi qua <see cref="ReportPolicy"/> để không
    /// so khớp mã CSDL thủ công — mã thôi việc có hai cách viết đang lệch giữa các tầng.
    /// </summary>
    private static string ResolveEmployeeStatus(string? status) =>
        ReportPolicy.IsWorking(status) ? "Đang làm việc"
        : ReportPolicy.IsResigned(status) ? "Đã thôi việc"
        : "Chưa xác định";

    /// <summary>
    /// Nhãn tiếng Việt của trạng thái hiệu lực hợp đồng. Phân loại bằng
    /// <see cref="ContractState"/> của tầng Core nên khớp đúng cách trang Quản lý Hợp đồng
    /// đang hiển thị, không so khớp mã trạng thái thủ công.
    /// </summary>
    private static string ResolveContractState(OvertimeHotel.HRM.Core.Models.HopDong contract, DateOnly today) =>
        ContractState.Classify(contract, today) switch
        {
            "HIEU_LUC" => "Đang hiệu lực",
            "SAP_HET_HAN" => "Sắp hết hạn (trong 30 ngày)",
            "DA_HET_HAN" => "Đã hết hạn",
            "CHUA_HIEU_LUC" => "Chưa đến ngày hiệu lực",
            "DA_CHAM_DUT" => "Đã chấm dứt",
            _ => "Chưa xác định"
        };

    // ═══════════════════════════════════════════════════════════════════════
    //  TIỆN ÍCH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tài khoản gắn với vé còn tồn tại và chưa bị khoá. Vé sống tối đa 10 phút, nhưng
    /// tài khoản có thể bị khoá trong khoảng đó — quyền phải được kiểm tại thời điểm truy vấn,
    /// không phải tại thời điểm phát vé.
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
            .AnyAsync(t => t.MaTaiKhoan == accountId && t.TrangThai, HttpContext.RequestAborted);
    }

    /// <summary>Đọc claim số nguyên; thiếu hoặc sai định dạng trả <c>null</c>.</summary>
    private static int? ReadInt(ClaimsPrincipal principal, string claimType) =>
        int.TryParse(
            principal.FindFirst(claimType)?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    /// <summary>
    /// Loại kỳ báo cáo theo yêu cầu. Tháng là mặc định vì đó là kỳ người dùng hay hỏi nhất;
    /// không kèm năm thì <see cref="BaoCaoReportService"/> tự lấy năm hiện tại.
    /// </summary>
    private static string ResolvePeriodKind(AiDataRequest request)
    {
        if (request.Quarter is >= 1 and <= 4)
        {
            return "QUY";
        }

        return request.Month is >= 1 and <= 12 ? "THANG" : "NAM";
    }
}

/// <summary>Thân yêu cầu xin số liệu của Trợ lý AI.</summary>
/// <param name="Token">Vé dữ liệu do Web Admin ký, sống ngắn và dùng được nhiều lần trong hạn đó.</param>
/// <param name="Scope"><c>reports</c> (mặc định), <c>my-payslip</c>, <c>directory</c>, <c>contract</c>, <c>leave</c>, <c>shift</c>, <c>attendance</c> hoặc <c>event</c>.</param>
/// <param name="Year">Năm tài chính; bỏ trống thì lấy năm hiện tại.</param>
/// <param name="Month">Tháng 1–12.</param>
/// <param name="Quarter">Quý 1–4; có giá trị thì được ưu tiên hơn tháng.</param>
/// <param name="Keyword">
/// Từ khóa tra cứu cho phạm vi <c>directory</c>, <c>contract</c>, <c>leave</c>, <c>shift</c>, <c>attendance</c> và <c>event</c>, do router của
/// chatbot trích từ câu hỏi. Chỉ dùng để thu hẹp kết quả tìm kiếm; phạm vi quyền vẫn được máy chủ
/// phân giải lại từ vé. <b>Trống/không gửi</b> nghĩa là hỏi chung ("hôm nay ai làm việc?") → máy chủ
/// trả danh sách mặc định thay vì từ chối.
/// </param>
public sealed record AiDataRequest(string? Token, string? Scope, int? Year, int? Month, int? Quarter, string? Keyword);

/// <summary>
/// Thân yêu cầu xin phạm vi kho tri thức.
/// </summary>
/// <remarks>
/// Cố ý chỉ có vé. Mọi trường phạm vi (đọc được gì, nạp được gì) đều do máy chủ dựng từ vé và
/// CSDL; nhận thêm một trường như <c>role</c> hay <c>departmentId</c> sẽ biến endpoint thành
/// chỗ tự khai quyền.
/// </remarks>
/// <param name="Token">Vé dữ liệu do Web Admin ký cho phiên đăng nhập hiện tại.</param>
public sealed record AiKnowledgeAccessRequest(string? Token);
