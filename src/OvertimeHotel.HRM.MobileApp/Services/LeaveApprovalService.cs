using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ duyệt đơn gọi THẲNG Supabase PostgREST, đúng kiến trúc PRD 06.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao không gọi qua Web Admin.</b> Trước đây service này dựng HttpClient
/// trỏ <c>ApiConfig.BaseUrl</c> (cổng API của WebAdmin). Trên bản Release giá trị
/// đó là <c>https://api.overtimehotel.com</c> — domain không tồn tại, mọi lệnh gọi
/// rơi vào <c>HttpRequestException</c> rồi hiện chung thông báo "lỗi tải dữ liệu".
/// PRD 06 vốn đã vẽ Mobile App nối thẳng Supabase qua REST/Realtime.
/// </para>
///
/// <para>
/// <b>Phạm vi lọc ở tầng CSDL, không ở client.</b> Bộ lọc vai trò / phòng ban /
/// loại đơn được nhét thẳng vào truy vấn PostgREST (thành điều kiện WHERE) thay
/// vì tải hết về rồi lọc trong bộ nhớ. RLS trên Cloud là lớp chặn thật — app không
/// tự lọc quyền rồi tin là an toàn; phần lọc ở đây chỉ để tiết kiệm băng thông và
/// hiển thị đúng, và trùng khớp với policy để hai bên không lệch nhau.
/// </para>
///
/// <para>
/// <b>Luật nghiệp vụ vẫn gọi từ <c>Core</c>.</b> Ma trận loại đơn và điều kiện
/// không tự duyệt lấy từ <see cref="LeaveApprovalPolicy"/> — cùng nguồn luật với
/// WebAdmin, không chép lại. Gọi ở đây để báo lỗi tiếng Việt sớm và đúng nguyên nhân
/// trước khi ghi; RLS vẫn là lớp chặn cuối, không phải lớp thay thế.
/// </para>
/// </remarks>
public class LeaveApprovalService : ILeaveApprovalService
{
    private const string STATUS_CHO_DUYET = "CHO_DUYET";

    /// <summary>
    /// Cột trả về kèm quan hệ nhúng.
    ///
    /// <para>
    /// don_tu có HAI khoá ngoại trỏ cùng bảng nhan_vien (ma_nhan_vien và
    /// ma_nguoi_duyet). PostgREST bắt buộc phải alias cho quan hệ thứ hai — không
    /// alias thì hoặc nó đoán nhầm (PGRST201 "ambiguous"), hoặc báo 42712 "table
    /// name specified more than once". Vì vậy dùng dạng <c>alias:table!ten_fk()</c>
    /// cho cả hai, và tên quan hệ trả về đúng là phần trước dấu ":".
    ///
    /// <para>
    /// <b>Chức vụ và phòng ban là quan hệ NHÚNG HAI TẦNG.</b>
    /// <c>nhan_vien</c> lại trỏ tiếp <c>chuc_vu</c> / <c>phong_ban</c>, nên trong
    /// JSON chúng nằm ở <c>nv.chuc_vu.ten_chuc_vu</c>, không phải
    /// <c>nv.chuc_vu</c>. Đọc bằng <c>ReadEmbeddedString(row,"nv","chuc_vu")</c> sẽ
    /// gặp OBJECT thay vì chuỗi và trả về rỗng — thẻ hiện "Chức vụ:" trống dù
    /// CSDL có đủ dữ liệu. Vì vậy hai chỗ đó gọi
    /// <see cref="PostgrstJson.ReadNestedString"/>.
    /// </para>
    /// </para>
    /// </summary>
    private const string SelectDonTu =
        "ma_don,ma_nhan_vien,ma_loai_don,ma_nguoi_duyet,tu_ngay,den_ngay,ly_do," +
        "ngay_gui,trang_thai,phan_hoi_duyet,ngay_duyet," +
        "nv:nhan_vien!don_tu_ma_nhan_vien_fkey!inner(ho,ten,ma_phong_ban," +
        "chuc_vu:chuc_vu!nhan_vien_ma_chuc_vu_fkey(ten_chuc_vu),phong_ban(ten_phong_ban))," +
        "loai_don!don_tu_ma_loai_don_fkey(ten_loai_don,co_huong_luong)," +
        "nd:nhan_vien!don_tu_ma_nguoi_duyet_fkey(ho,ten)";

    /// <summary>
    /// Bản select rút gọn cho lệnh đếm. Phải khai báo nv vì bộ lọc
    /// <c>nv.ma_phong_ban</c> của Manager chỉ hợp lệ khi quan hệ đó nằm trong
    /// select — PostgREST từ chối với PGRST108 nếu không.
    /// </summary>
    private const string SelectDonTuCount =
        "ma_don,nv:nhan_vien!don_tu_ma_nhan_vien_fkey!inner(ma_phong_ban)";

    private readonly SupabaseRestClient _rest;
    private readonly WebAdminApiClient _api;
    private readonly IAuthService _authService;
    private readonly ILogger<LeaveApprovalService> _logger;

    public bool IsDemoMode => false;

    public LeaveApprovalService(
        SupabaseRestClient rest,
        WebAdminApiClient api,
        IAuthService authService,
        ILogger<LeaveApprovalService> logger)
    {
        _rest = rest ?? throw new ArgumentNullException(nameof(rest));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LeaveRequestListResponse> GetLeaveRequestsAsync(string view = "pending", string type = "all",
        int? departmentId = null, int offset = 0, int? pageSize = null)
    {
        await _authService.RefreshPermissionsAsync();
        var user = RequireSession();

        // Fail-closed: Employee và vai trò lạ không được chạm vào dữ liệu duyệt đơn.
        if (!user.CanViewLeave)
        {
            throw new InvalidOperationException("Bạn không có thẩm quyền truy cập danh sách duyệt đơn.");
        }

        if (departmentId is <= 0) throw new ArgumentOutOfRangeException(nameof(departmentId));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var isPending = !string.Equals(view, "reviewed", StringComparison.OrdinalIgnoreCase);

        var filters = BuildScopeFilters(user);
        if (departmentId.HasValue) filters.Add($"nv.ma_phong_ban=eq.{departmentId.Value}");
        filters.Add(isPending ? "trang_thai=eq." + STATUS_CHO_DUYET : "trang_thai=neq." + STATUS_CHO_DUYET);

        if (int.TryParse(type, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maLoai))
        {
            filters.Add($"ma_loai_don=eq.{maLoai}");
        }

        // Đơn đã xử lý sắp theo ngày duyệt; đơn chờ sắp theo ngày gửi — đơn còn
        // chờ có ngay_duyet NULL nên sort theo cột đó sẽ loạn cả hai bảng.
        filters.Add(isPending ? "order=ngay_gui.desc,ma_don.desc" : "order=ngay_duyet.desc,ma_don.desc");
        if (pageSize.HasValue)
        {
            // One look-ahead row tells the UI whether another page exists.
            filters.Add($"limit={pageSize.Value + 1}");
            filters.Add($"offset={offset}");
        }

        var json = await _rest.GetAsync($"don_tu?select={SelectDonTu}&{string.Join('&', filters)}");

        var items = PostgrstJson.ParseArray(json).Select(ToListItemDto).ToList();
        var hasMore = pageSize.HasValue && items.Count > pageSize.Value;
        if (pageSize.HasValue) items = items.Take(pageSize.Value).ToList();
        var pendingCount = offset == 0 ? await CountPendingAsync(user, departmentId, type) : 0;

        return new LeaveRequestListResponse(items, pendingCount, "production", hasMore);
    }

    public async Task<List<DepartmentOption>> GetDepartmentOptionsAsync()
    {
        if (!RequireSession().CanViewLeave)
            throw new UnauthorizedAccessException("Bạn không có thẩm quyền truy cập danh sách phòng ban.");

        var json = await _rest.GetAsync("phong_ban?select=ma_phong_ban,ten_phong_ban&order=ten_phong_ban.asc");
        return PostgrstJson.ParseArray(json)
            .Select(row => new DepartmentOption(PostgrstJson.ReadNullableInt(row, "ma_phong_ban"),
                PostgrstJson.ReadString(row, "ten_phong_ban")))
            .Where(option => option.MaPhongBan > 0).ToList();
    }

    public async Task<LeaveReviewResult> ReviewLeaveRequestAsync(int maDon, bool approved, string? phanHoi)
    {
        try
        {
            await _authService.RefreshPermissionsAsync();
            var user = RequireSession();
            var donTu = await LoadRequestSnapshotAsync(maDon);

            if (donTu is null)
            {
                return new LeaveReviewResult(false, "NOT_FOUND", "Không tìm thấy đơn từ yêu cầu.");
            }

            if (!user.CanReviewRequest(donTu.Value.MaLoaiDon))
                return new LeaveReviewResult(false, "FORBIDDEN", "Bạn không còn quyền xử lý loại đơn này.");

            if (user.IsManager && donTu.Value.RequestDeptId != user.MaPhongBan)
                return new LeaveReviewResult(false, "FORBIDDEN", "Đơn không thuộc bộ phận của bạn.");

            var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateReviewAction(
                role: user.IsManager ? "HR" : user.TenVaiTro,
                reviewerDeptId: user.MaPhongBan,
                requestDeptId: donTu.Value.RequestDeptId,
                maLoaiDon: donTu.Value.MaLoaiDon,
                currentStatus: donTu.Value.TrangThai,
                approved: approved,
                phanHoi: phanHoi,
                reviewerEmployeeId: user.MaNhanVien,
                requestEmployeeId: donTu.Value.MaNhanVien);

            if (!isValid)
            {
                return new LeaveReviewResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!);
            }

            // Nếu WebAdmin API đã được cấu hình, ưu tiên xử lý tập trung phía máy chủ
            if (_api.IsConfigured)
            {
                return await _api.PostAsync<LeaveReviewResult>(
                    $"api/leaverequests/{maDon}/review",
                    new LeaveReviewRequest(approved, string.IsNullOrWhiteSpace(phanHoi) ? null : phanHoi.Trim()));
            }

            // Fallback sang PostgREST PATCH cho môi trường chưa cấu hình WebAdmin API (bảo toàn client cũ)
            var body = JsonSerializer.Serialize(
                new ReviewPayload(
                    TrangThai: approved ? "DA_DUYET" : "TU_CHOI",
                    NgayDuyet: AttendanceCalculator.ToUtc(DateTimeOffset.UtcNow)
                        .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                    MaNguoiDuyet: user.MaNhanVien,
                    PhanHoiDuyet: string.IsNullOrWhiteSpace(phanHoi) ? null : phanHoi.Trim()),
                JsonPayloadOptions);

            await _rest.PatchAsync($"don_tu?ma_don=eq.{maDon}&trang_thai=eq.{STATUS_CHO_DUYET}", body);

            if (_rest.NoRowsMatched)
            {
                return new LeaveReviewResult(
                    false, "ALREADY_REVIEWED", "Đơn từ đã được xử lý bởi người khác trước đó.");
            }

            _logger.LogInformation(
                "Người duyệt #{ReviewerId} ({Role}) đã {Action} đơn #{MaDon}.",
                user.MaNhanVien, user.TenVaiTro, approved ? "duyệt" : "từ chối", maDon);

            return new LeaveReviewResult(
                true, "REVIEWED", approved ? "Phê duyệt đơn thành công." : "Đã từ chối đơn thành công.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new LeaveReviewResult(false, "UNAUTHENTICATED", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Thao tác duyệt đơn #{MaDon} bị máy chủ từ chối.", maDon);
            return new LeaveReviewResult(
                false, _rest.WasDeniedByPolicy ? "FORBIDDEN" : "SERVER_ERROR", ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gửi yêu cầu review đơn #{MaDon}", maDon);
            var code = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Conflict => "CONCURRENCY_CONFLICT",
                System.Net.HttpStatusCode.Forbidden => "FORBIDDEN",
                System.Net.HttpStatusCode.NotFound => "NOT_FOUND",
                _ => "NETWORK_ERROR"
            };
            return new LeaveReviewResult(false, code, string.IsNullOrWhiteSpace(ex.Message) ? "Không thể kết nối máy chủ để xử lý đơn. Vui lòng thử lại." : ex.Message);
        }
    }

    public async Task<List<LeaveRequestItem>> GetPendingRequestsAsync(string role, int? maPhongBan) =>
        MapToItems(await LoadScopeAsync(isPending: true));

    public async Task<List<LeaveRequestItem>> GetReviewedRequestsAsync(string role, int? maPhongBan) =>
        MapToItems(await LoadScopeAsync(isPending: false));

    public async Task<int> GetPendingCountAsync(string role, int? maPhongBan)
    {
        try
        {
            return await _rest.CountAsync($"don_tu?select={SelectDonTuCount}&{JoinScope(isPending: true)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Badge hỏng không được làm hỏng cả trang chủ (HomeViewModelBase cũng có
            // fallback riêng, chặn ở đây để không ném vô nghĩa lên UI thread).
            _logger.LogWarning(ex, "Không đếm được số đơn chờ duyệt — hiển thị 0.");
            return 0;
        }
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int maDon)
    {
        var result = await ReviewLeaveRequestAsync(maDon, true, null);
        return (result.Success, result.Message);
    }

    public async Task<(bool Success, string Message)> RejectAsync(int maDon, string lyDoTuChoi)
    {
        var result = await ReviewLeaveRequestAsync(maDon, false, lyDoTuChoi);
        return (result.Success, result.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PHÍA NHÂN VIÊN NỘP ĐƠN (TSK-18)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Mã loại đơn phép năm — loại duy nhất trừ vào quỹ phép năm.</summary>
    private const int MA_LOAI_PHEP_NAM = 1;

    private const string SelectLoaiDon = "ma_loai_don,ten_loai_don,co_huong_luong,mo_ta";

    public async Task<List<LeaveTypeOption>> GetSubmittableTypesAsync()
    {
        var allowed = LeaveApprovalPolicy.GetSubmittableRequestTypes();

        // Lọc ở tầng truy vấn thay vì tải 5 dòng về rồi lọc trong bộ nhớ: danh mục
        // loại đơn là dữ liệu tham chiếu, để CSDL quyết định loại nào còn tồn tại.
        var json = await _rest.GetAsync(
            $"loai_don?select={SelectLoaiDon}&ma_loai_don=in.({string.Join(',', allowed)})&order=ma_loai_don.asc");

        return [.. PostgrstJson.ParseArray(json).Select(row => new LeaveTypeOption(
            MaLoaiDon: PostgrstJson.ReadNullableInt(row, "ma_loai_don") ?? 0,
            TenLoaiDon: PostgrstJson.ReadString(row, "ten_loai_don"),
            CoHuongLuong: PostgrstJson.ReadBool(row, "co_huong_luong"),
            MoTa: PostgrstJson.ReadString(row, "mo_ta")))];
    }

    public async Task<List<LeaveRequestItem>> GetMyRequestsAsync(bool chiChoDuyet = false, CancellationToken cancellationToken = default)
    {
        var maNhanVien = RequireEmployeeId();

        // Khoá theo chính mình ngay trong truy vấn: app không tự lọc rồi tin là
        // an toàn, RLS mới là lớp chặn thật. Truy vấn này chỉ phục vụ hiển thị.
        var filters = new List<string> { $"ma_nhan_vien=eq.{maNhanVien}" };
        if (chiChoDuyet)
        {
            filters.Add("trang_thai=eq." + STATUS_CHO_DUYET);
        }

        filters.Add("order=ngay_gui.desc");

        var json = await _rest.GetAsync($"don_tu?select={SelectDonTu}&{string.Join('&', filters)}", cancellationToken);
        return MapToItems(json);
    }

    public async Task<int> GetUsedAnnualLeaveDaysAsync()
    {
        var maNhanVien = RequireEmployeeId();
        var firstDay = new DateOnly(DateTime.Today.Year, 1, 1);
        var nextYear = firstDay.AddYears(1);

        // Chỉ tính đơn phép năm ĐÃ ĐƯỢC DUYỆT trong năm hiện tại. Đơn đang chờ
        // hoặc bị từ chối không tiêu tốn quỹ — tính cả chúng thì quỹ phép của
        // người dùng hiện ra âm bởi chính những đơn chưa ai xử lý.
        var json = await _rest.GetAsync(
            $"don_tu?select=tu_ngay,den_ngay&ma_nhan_vien=eq.{maNhanVien}" +
            $"&ma_loai_don=eq.{MA_LOAI_PHEP_NAM}&trang_thai=eq.DA_DUYET" +
            $"&den_ngay=gte.{firstDay:yyyy-MM-dd}&den_ngay=lt.{nextYear:yyyy-MM-dd}");

        var used = 0;
        foreach (var row in PostgrstJson.ParseArray(json))
        {
            var tu = PostgrstJson.ReadDate(row, "tu_ngay");
            var den = PostgrstJson.ReadDate(row, "den_ngay");
            used += Math.Max(1, den.DayNumber - tu.DayNumber + 1);
        }

        return used;
    }

    public async Task<LeaveSubmitResult> SubmitRequestAsync(
        int maLoaiDon, DateOnly tuNgay, DateOnly denNgay, string lyDo)
    {
        try
        {
            await _authService.RefreshPermissionsAsync();
            var user = RequireSession();
            if (!user.HasPermission(OvertimeHotel.HRM.Core.Policies.MobilePermissionPolicy.SubmitRequest))
                return new LeaveSubmitResult(false, "FORBIDDEN", "Bạn không có quyền nộp đơn từ.");
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Zero Trust: mã nhân viên lấy TỪ PHIÊN, không nhận từ tham số —
            // không có đường để nộp đơn hộ người khác dù có sửa được app.
            var maNhanVien = user.MaNhanVien;
            if (maNhanVien <= 0)
            {
                return new LeaveSubmitResult(
                    false, "UNAUTHENTICATED", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
            }

            var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateCreate(
                maLoaiDon, tuNgay, denNgay, lyDo, today);

            if (!isValid)
            {
                return new LeaveSubmitResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!);
            }

            var soNgay = denNgay.DayNumber - tuNgay.DayNumber + 1;

            // Chặn trùng lịch: đã có đơn CHO_DUYET nào trùng khoảng ngày thì cảnh
            // báo, không chặn cứng — người dùng vẫn có thể cần nộp (ví dụ đơn
            // cũ treo lâu, giờ xin nộp lại). Chỉ đơn đang chờ mới tính.
            var pending = await GetMyRequestsAsync(chiChoDuyet: true);
            var coTrung = LeaveApprovalPolicy.OverlapsAny(
                tuNgay, denNgay,
                pending.Select(r => (r.TuNgay, r.DenNgay)));

            if (coTrung)
            {
                return new LeaveSubmitResult(
                    false, "OVERLAP", "Bạn đã có đơn đang chờ duyệt trùng với khoảng ngày này. " +
                    "Vui lòng kiểm tra lịch sử đơn trước khi gửi tiếp.");
            }

            if (maLoaiDon == MA_LOAI_PHEP_NAM)
            {
                var used = await GetUsedAnnualLeaveDaysAsync();
                var (quotaOk, quotaCode, quotaMsg) = LeaveApprovalPolicy.ValidateAnnualLeaveBalance(
                    soNgay, used);

                if (!quotaOk)
                {
                    return new LeaveSubmitResult(false, quotaCode ?? "EXCEEDS_QUOTA", quotaMsg!);
                }
            }

            // ngay_gui và trang_thai KHÔNG gửi lên — để CSDL lấy DEFAULT (thời điểm
            // gửi và CHO_DUYET). Nếu app tự điền trang_thai thì một máy sửa được
            // có thể tự tạo đơn đã "được duyệt" và đánh lừa người duyệt.
            var body = JsonSerializer.Serialize(
                new CreatePayload(
                    MaNhanVien: maNhanVien,
                    MaLoaiDon: maLoaiDon,
                    TuNgay: tuNgay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DenNgay: denNgay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    LyDo: lyDo.Trim()),
                JsonPayloadOptions);

            var response = await _rest.PostAsync("don_tu", body);

            if (_rest.NoRowsMatched)
            {
                return new LeaveSubmitResult(
                    false, "SERVER_ERROR", "Máy chủ không xác nhận đã lưu đơn. Vui lòng thử lại.");
            }

            var maDon = ReadCreatedDonId(response);

            _logger.LogInformation(
                "Nhân viên #{MaNhanVien} nộp đơn loại {MaLoaiDon} từ {TuNgay} đến {DenNgay} ({SoNgay} ngày).",
                maNhanVien, maLoaiDon, tuNgay, denNgay, soNgay);

            return new LeaveSubmitResult(
                true, "SUBMITTED",
                $"Đã gửi đơn thành công. Đơn #{maDon} đang chờ người quản lý xét duyệt.",
                maDon);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new LeaveSubmitResult(false, "UNAUTHENTICATED", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Thao tác nộp đơn bị máy chủ từ chối.");

            // WasDeniedByPolicy = true nghĩa là RLS chặn INSERT. Đây KHÔNG phải
            // lỗi người dùng — nhưng cũng không được nói "thử lại" vì sẽ không
            // bao giờ được. Báo rõ việc cần quản trị viên cấp quyền.
            return new LeaveSubmitResult(
                false,
                _rest.WasDeniedByPolicy ? "FORBIDDEN" : "SERVER_ERROR",
                _rest.WasDeniedByPolicy
                    ? "Máy chủ chưa cấp quyền ghi đơn cho tài khoản của bạn. Vui lòng liên hệ quản trị viên."
                    : ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi nộp đơn từ.");
            return new LeaveSubmitResult(
                false, "NETWORK_ERROR", "Không thể kết nối máy chủ để gửi đơn. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Lấy mã đơn vừa tạo từ phản hồi PostgREST.
    /// </summary>
    /// <remarks>
    /// <c>PostAsync</c> gửi kèm <c>Prefer: return=representation</c> nên phản hồi
    /// chứa chính dòng vừa ghi. Nếu RLS chặn, hoặc dịch vụ trả 204, body rỗng thì
    /// trả 0 — chỉ dùng để hiển thị, không làm hỏng luồng gửi đơn.
    /// </remarks>
    private static int ReadCreatedDonId(string response)
    {
        var rows = PostgrstJson.ParseArray(response);
        return rows.Length == 0 ? 0 : PostgrstJson.ReadNullableInt(rows[0], "ma_don") ?? 0;
    }

    /// <summary>Lấy mã nhân viên từ phiên, chặn khi phiên hỏng thay vì ghi đơn rác.</summary>
    private int RequireEmployeeId()
    {
        var maNhanVien = RequireSession().MaNhanVien;
        if (maNhanVien <= 0)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
        }

        return maNhanVien;
    }

    private Task<string> LoadScopeAsync(bool isPending) =>
        _rest.GetAsync($"don_tu?select={SelectDonTu}&{JoinScope(isPending)}");

    /// <summary>Nối các điều kiện phạm vi thành chuỗi truy vấn.</summary>
    private string JoinScope(bool isPending)
    {
        var filters = BuildScopeFilters(RequireSession());
        filters.Add(isPending ? "trang_thai=eq." + STATUS_CHO_DUYET : "trang_thai=neq." + STATUS_CHO_DUYET);
        filters.Add(isPending ? "order=ngay_gui.desc" : "order=ngay_duyet.desc");
        return string.Join('&', filters);
    }

    /// <summary>
    /// Dựng điều kiện giới hạn phạm vi, khớp 1-1 với <see cref="LeaveApprovalPolicy"/>
    /// và với phần truy vấn tương ứng ở
    /// <c>LeaveRequestsController.GetLeaveRequestsCoreAsync</c>.
    /// </summary>
    private static List<string> BuildScopeFilters(UserSession user)
    {
        var filters = new List<string>();

        // Không ai duyệt đơn của chính mình (trừ Admin) — cùng quy tắc với CanView.
        if (!user.IsAdmin)
        {
            filters.Add($"ma_nhan_vien=neq.{user.MaNhanVien}");
        }

        var allowedTypes = Enumerable.Range(1, 5)
            .Where(user.CanViewRequest).ToList();
        if (allowedTypes.Count == 0) throw new UnauthorizedAccessException("Bạn không có quyền xem loại đơn nào.");
        filters.Add($"ma_loai_don=in.({string.Join(',', allowedTypes.OrderBy(t => t))})");

        // Manager bị khoá trong phòng ban của mình. CanApproveLeave đã chặn sẵn
        // trường hợp không có phòng ban, nhưng nhánh if vẫn để nguyên vì
        // BuildScopeFilters là hợp đồng với LeaveApprovalPolicy chứ không phải
        // với UserSession — đổi một trong hai không được làm lệch cái còn lại.
        if (LeaveApprovalPolicy.IsManager(user.TenVaiTro)
            && user.MaPhongBan is int dept
            && user.MaPhongBan > 0)
        {
            filters.Add($"nv.ma_phong_ban=eq.{dept}");
        }

        return filters;
    }

    private async Task<int> CountPendingAsync(UserSession user, int? departmentId = null, string type = "all")
    {
        try
        {
            var filters = BuildScopeFilters(user);
            if (departmentId.HasValue) filters.Add($"nv.ma_phong_ban=eq.{departmentId.Value}");
            if (int.TryParse(type, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maLoai))
                filters.Add($"ma_loai_don=eq.{maLoai}");
            filters.Add("trang_thai=eq." + STATUS_CHO_DUYET);
            return await _rest.CountAsync($"don_tu?select={SelectDonTuCount}&{string.Join('&', filters)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Mất badge thì chấp nhận được; mất cả danh sách thì không — nuốt lỗi
            // riêng ở đây để phần đọc danh sách vẫn trả về bình thường.
            _logger.LogWarning(ex, "Không đếm được số đơn chờ duyệt — hiển thị 0.");
            return 0;
        }
    }

    /// <summary>
    /// Nạp một đơn chỉ để lấy hai thông tin luật thẩm quyền cần: mã nhân viên nộp
    /// đơn và phòng ban của họ.
    /// </summary>
    private async Task<RequestSnapshot?> LoadRequestSnapshotAsync(int maDon)
    {
        const string select = "ma_don,ma_nhan_vien,ma_loai_don,trang_thai," +
            "nv:nhan_vien!don_tu_ma_nhan_vien_fkey(ma_phong_ban)";

        var rows = PostgrstJson.ParseArray(
            await _rest.GetAsync($"don_tu?select={select}&ma_don=eq.{maDon}&limit=1"));

        if (rows.Length == 0)
        {
            return null;
        }

        var row = rows[0];
        return new RequestSnapshot(
            MaNhanVien: PostgrstJson.ReadNullableInt(row, "ma_nhan_vien") ?? 0,
            MaLoaiDon: PostgrstJson.ReadNullableInt(row, "ma_loai_don") ?? 0,
            TrangThai: PostgrstJson.ReadString(row, "trang_thai"),
            RequestDeptId: PostgrstJson.ReadEmbeddedInt(row, "nv", "ma_phong_ban"));
    }

    private static List<LeaveRequestItem> MapToItems(string json) =>
        [.. PostgrstJson.ParseArray(json).Select(ToItem)];

    /// <summary>
    /// Bản DTO dùng chung với WebAdmin cho endpoint <c>GetLeaveRequestsAsync</c>.
    /// Cùng nguồn dữ liệu nhưng là hai kiểu khác nhau: DTO để đóng gói giao thức,
    /// còn <see cref="LeaveRequestItem"/> là model có thuộc tính hiển thị sẵn cho
    /// XAML — nên ánh xạ riêng thay vì ép kiểu qua nhau.
    /// </summary>
    private static LeaveRequestListItemDto ToListItemDto(JsonElement row)
    {
        var maNhanVien = PostgrstJson.ReadNullableInt(row, "ma_nhan_vien") ?? 0;
        var loaiDon = PostgrstJson.ReadEmbedded(row, "loai_don");
        var maLoaiDon = PostgrstJson.ReadNullableInt(row, "ma_loai_don") ?? 0;

        return new LeaveRequestListItemDto(
            MaDon: PostgrstJson.ReadNullableInt(row, "ma_don") ?? 0,
            MaNhanVien: maNhanVien,
            HoTenNhanVien: ReadFullName(row, "nv") ?? $"NV#{maNhanVien}",
            ChucVu: NullIfEmpty(PostgrstJson.ReadNestedString(row, "nv.chuc_vu", "ten_chuc_vu")),
            MaPhongBan: PostgrstJson.ReadEmbeddedInt(row, "nv", "ma_phong_ban"),
            TenPhongBan: NullIfEmpty(PostgrstJson.ReadNestedString(row, "nv.phong_ban", "ten_phong_ban")),
            MaLoaiDon: maLoaiDon,
            TenLoaiDon: PostgrstJson.ReadEmbeddedString(row, "loai_don", "ten_loai_don") is { Length: > 0 } ten
                ? ten
                : $"Loại #{maLoaiDon}",
            CoHuongLuong: loaiDon is { } type && PostgrstJson.ReadBool(type, "co_huong_luong"),
            TuNgay: PostgrstJson.ReadDate(row, "tu_ngay"),
            DenNgay: PostgrstJson.ReadDate(row, "den_ngay"),
            LyDo: PostgrstJson.ReadString(row, "ly_do"),
            NgayGui: PostgrstJson.ReadTimestamp(row, "ngay_gui"),
            TrangThai: PostgrstJson.ReadString(row, "trang_thai"),
            PhanHoiDuyet: PostgrstJson.ReadNullableString(row, "phan_hoi_duyet"),
            NgayDuyet: PostgrstJson.ReadNullableTimestamp(row, "ngay_duyet"),
            HoTenNguoiDuyet: ReadFullName(row, "nd"));
    }

    /// <summary>Chuỗi rỗng trong JSON nên thành null để XAML không hiện dấu phân tách treo.</summary>
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Chuyển một dòng PostgREST sang model hiển thị.
    ///
    /// <para>
    /// Quan hệ nhúng có thể vắng mặt hoàn toàn (nhân viên chưa được gán phòng
    /// ban) hoặc để nguyên NULL khi LEFT JOIN không khớp — vì vậy đọc qua
    /// <see cref="PostgrstJson.ReadEmbeddedString"/> thay vì ép kiểu trực tiếp.
    /// </para>
    /// </summary>
    private static LeaveRequestItem ToItem(JsonElement row)
    {
        var maNhanVien = PostgrstJson.ReadNullableInt(row, "ma_nhan_vien") ?? 0;
        var loaiDon = PostgrstJson.ReadEmbedded(row, "loai_don");

        var status = PostgrstJson.ReadString(row, "trang_thai");
        var typeId = PostgrstJson.ReadNullableInt(row, "ma_loai_don") ?? 0;
        var start = PostgrstJson.ReadDate(row, "tu_ngay");
        var todayVietnam = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (status == "CHO_DUYET" && typeId is >= 1 and <= 4 && start <= todayVietnam)
            status = "HET_HAN";
        return new LeaveRequestItem
        {
            MaDon = PostgrstJson.ReadNullableInt(row, "ma_don") ?? 0,
            MaNhanVien = maNhanVien,
            TenNhanVien = ReadFullName(row, "nv") ?? $"NV#{maNhanVien}",
            TenChucVu = PostgrstJson.ReadNestedString(row, "nv.chuc_vu", "ten_chuc_vu"),
            MaPhongBan = PostgrstJson.ReadEmbeddedInt(row, "nv", "ma_phong_ban"),
            TenPhongBan = PostgrstJson.ReadNestedString(row, "nv.phong_ban", "ten_phong_ban"),
            MaLoaiDon = PostgrstJson.ReadNullableInt(row, "ma_loai_don") ?? 0,
            TenLoaiDon = PostgrstJson.ReadEmbeddedString(row, "loai_don", "ten_loai_don"),
            CoHuongLuong = loaiDon is { } type && PostgrstJson.ReadBool(type, "co_huong_luong"),
            TuNgay = PostgrstJson.ReadDate(row, "tu_ngay"),
            DenNgay = PostgrstJson.ReadDate(row, "den_ngay"),
            LyDo = PostgrstJson.ReadString(row, "ly_do"),
            NgayGui = PostgrstJson.ReadTimestamp(row, "ngay_gui"),
            TrangThai = status,
            MaNguoiDuyet = PostgrstJson.ReadNullableInt(row, "ma_nguoi_duyet"),
            NgayDuyet = PostgrstJson.ReadNullableTimestamp(row, "ngay_duyet"),
            PhanHoiDuyet = PostgrstJson.ReadNullableString(row, "phan_hoi_duyet"),
            NguoiDuyetTen = ReadFullName(row, "nd")
        };
    }

    /// <summary>
    /// Ghép "ho ten" từ quan hệ nhúng, giống hệt cách WebAdmin ghép ở
    /// <c>LeaveRequestsController</c> để danh sách trên app không lệch WebAdmin.
    /// Trả null khi quan hệ không có (đơn chưa ai duyệt).
    /// </summary>
    private static string? ReadFullName(JsonElement row, string relation)
    {
        if (PostgrstJson.ReadEmbedded(row, relation) is not { } person)
        {
            return null;
        }

        var full = string.Join(
            ' ',
            new[] { PostgrstJson.ReadString(person, "ho"), PostgrstJson.ReadString(person, "ten") }
                .Where(part => part.Length > 0)).Trim();

        return full.Length > 0 ? full : null;
    }

    private UserSession RequireSession()
    {
        var user = _authService.CurrentUser;
        if (user is null || !user.IsLoggedIn)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        return user;
    }

    private static readonly JsonSerializerOptions JsonPayloadOptions = new()
    {
        // PhanHoiDuyet = null PHẢI được gửi lên: đơn duyệt lại sau khi từ chối
        // cần xoá phản hồi cũ. Bỏ mặc định này thì null bị rụng khỏi JSON và
        // cột giữ nguyên giá trị cũ.
        //
        // KHÔNG thêm PropertyNamingPolicy = SnakeCaseLower ở đây: nó sẽ đổi tên
        // mọi property theo quy tắc hàm ý, và một property đã gắn
        // JsonPropertyName sẽ bị bỏ qua — dễ tưởng đã đổi hết trong khi thực ra
        // chỉ đổi một phần. Ghi tên cột thẳng tại từng record cho rõ.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Thông tin tối thiểu của một đơn để chạy luật thẩm quyền.</summary>
    private readonly record struct RequestSnapshot(int MaNhanVien, int MaLoaiDon, string TrangThai, int? RequestDeptId);

    /// <summary>Rút đơn qua API có kiểm tra danh tính và trạng thái ở máy chủ.</summary>
    public async Task<LeaveReviewResult> WithdrawRequestAsync(int maDon)
    {
        try
        {
            return await _api.PostAsync<LeaveReviewResult>($"api/leaverequests/{maDon}/withdraw");
        }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Không thể rút đơn #{MaDon}", maDon);
            return new LeaveReviewResult(false, "ERROR", ex is TaskCanceledException ? "Kết nối hết thời gian. Vui lòng thử lại." : ex.Message);
        }
    }

    /// <summary>Body PATCH duyệt đơn dùng tên cột PostgREST.</summary>
    private sealed record ReviewPayload(
        [property: JsonPropertyName("trang_thai")] string TrangThai,
        [property: JsonPropertyName("ngay_duyet")] string NgayDuyet,
        [property: JsonPropertyName("ma_nguoi_duyet")] int MaNguoiDuyet,
        [property: JsonPropertyName("phan_hoi_duyet")] string? PhanHoiDuyet);

    /// <summary>
    /// Body POST cho <c>don_tu</c> (TSK-18).
    /// </summary>
    /// <remarks>
    /// <b>Cố ý KHÔNG gửi <c>ngay_gui</c> và <c>trang_thai</c>.</b> Hai cột này có
    /// DEFAULT ở CSDL (<c>CURRENT_TIMESTAMP</c> và <c>'CHO_DUYET'</c>) và do server
    /// quyết định. Nếu app gửi kèm, một máy sửa được APK có thể tự tạo đơn ở
    /// trạng thái "đã duyệt" và tự phê duyệt hộ — đúng lỗ hổng mà RLS cứu không
    /// được, vì policy <c>FOR INSERT</c> không kiểm tra được cột do client tự chế.
    /// </remarks>
    private sealed record CreatePayload(
        [property: JsonPropertyName("ma_nhan_vien")] int MaNhanVien,
        [property: JsonPropertyName("ma_loai_don")] int MaLoaiDon,
        [property: JsonPropertyName("tu_ngay")] string TuNgay,
        [property: JsonPropertyName("den_ngay")] string DenNgay,
        [property: JsonPropertyName("ly_do")] string LyDo);
}
