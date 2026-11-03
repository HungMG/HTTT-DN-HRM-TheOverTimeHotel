using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Số kỳ lương gần nhất trả về cho mục cá nhân. Đủ để thấy xu hướng, không phình ngữ cảnh.</summary>
    private const int PersonalPayslipCount = 3;

    /// <summary>
    /// Giới hạn số nhân viên trả về mỗi lượt tra cứu. Một khách sạn có thể có nhiều người
    /// trùng tên; trả cả danh sách sẽ phình ngữ cảnh của mô hình, còn cắt bớt kèm tổng số thì
    /// mô hình vẫn nói đúng "có N người, đây là 10 người đầu tiên".
    /// </summary>
    private const int DirectoryResultLimit = 10;

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

        var keyword = (request.Keyword ?? string.Empty).Trim();
        if (keyword.Length == 0)
        {
            return BadRequest(new { message = "Thiếu từ khóa tra cứu tên nhân viên." });
        }

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

            query = query.Where(n =>
                EF.Functions.ILike(n.Ho + " " + n.Ten, pattern)
                || EF.Functions.ILike(n.Ho, pattern)
                || EF.Functions.ILike(n.Ten, pattern)
                || (hasEmployeeCode && n.MaNhanVien == employeeCode));

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
/// <param name="Scope"><c>reports</c> (mặc định), <c>my-payslip</c> hoặc <c>directory</c>.</param>
/// <param name="Year">Năm tài chính; bỏ trống thì lấy năm hiện tại.</param>
/// <param name="Month">Tháng 1–12.</param>
/// <param name="Quarter">Quý 1–4; có giá trị thì được ưu tiên hơn tháng.</param>
/// <param name="Keyword">
/// Từ khóa tra cứu cho phạm vi <c>directory</c>, do router của chatbot trích từ câu hỏi.
/// Chỉ dùng để thu hẹp kết quả tìm kiếm; phạm vi quyền vẫn được máy chủ phân giải lại từ vé.
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
