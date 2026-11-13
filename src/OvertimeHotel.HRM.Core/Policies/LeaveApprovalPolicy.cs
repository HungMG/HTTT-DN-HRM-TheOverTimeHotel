using OvertimeHotel.HRM.Core.Enums;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Nguồn luật duy nhất (Single Source of Truth) định nghĩa toàn bộ quy tắc phân quyền (RBAC),
/// phạm vi phòng ban và điều kiện duyệt/từ chối đơn từ cho Task 19.
/// </summary>
public static class LeaveApprovalPolicy
{
    // Ma trận loại đơn:
    // Loại 1: Nghỉ phép năm -> HR (toàn khách sạn), Admin
    // Loại 2: Nghỉ ốm / BHXH -> HR (toàn khách sạn), Admin
    // Loại 3: Nghỉ thai sản -> HR (toàn khách sạn), Admin
    // Loại 4: Nghỉ việc riêng không lương -> HR (toàn khách sạn), Admin
    // Loại 5: Xin thôi việc -> HR (toàn khách sạn), Admin
    //
    // HR xử lý TOÀN BỘ 5 loại đơn trên toàn khách sạn, kể cả đơn của quản lý:
    // quản lý nộp đơn thì không còn ai ở cấp dưới họ để duyệt. Quản lý chỉ duyệt
    // loại 1, 2, 4 CÙNG phòng ban; loại 3 và 5 vốn là việc của nhân sự nên quản lý
    // không đụng tới — kể cả thôi việc, vì thôi việc cần người đủ thẩm quyền chốt
    // hồ sơ chứ không chỉ xác nhận người nộp.
    private static readonly HashSet<int> ManagerAllowedTypes = new() { 1, 2, 4 };
    private static readonly HashSet<int> HrAllowedTypes = new() { 1, 2, 3, 4, 5 };

    /// <summary>
    /// Toàn bộ loại đơn người dùng được phép nộp, theo đúng 5 dòng seed trong bảng
    /// <c>LOAI_DON</c>.
    /// </summary>
    /// <remarks>
    /// <b>Đừng đọc số 4 trong PRD là "thôi việc".</b> PRD 02 mục 4.4 ghi
    /// <c>ma_loai_don = 4</c> cho đơn thôi việc, nhưng seed
    /// <c>seed_data_24_tables.sql</c> và chính ma trận trên đây đều quy định
    /// 4 là "Nghỉ việc riêng không lương" và 5 mới là "Đơn xin thôi việc". CSDL là
    /// nguồn luật, PRD là bản thiết kế — lệch ở đây thì mỗi bên hiểu một kiểu.
    /// </remarks>
    private static readonly HashSet<int> SubmittableTypes = new() { 1, 2, 3, 4, 5 };

    /// <summary>Định mức phép năm theo chế độ (ngày/năm), dùng cho cảnh báo quỹ phép.</summary>
    public const int AnnualLeaveQuotaDays = 12;

    /// <summary>Số ngày phải báo trước khi thôi việc theo Điều 35 BLLĐ 2019.</summary>
    public static readonly Dictionary<bool, int> ResignationNoticeDays = new()
    {
        [true] = 45,  // HĐLĐ không xác định thời hạn
        [false] = 30  // HĐLĐ xác định thời hạn
    };

    /// <summary>
    /// Chuẩn hóa tên vai trò từ chuỗi (hỗ trợ cả tiếng Anh và tiếng Việt trong DB).
    /// </summary>
    public static RoleType? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        var clean = role.Trim().ToUpperInvariant();
        return clean switch
        {
            "ADMIN" or "QUANTRI" or "QUANTRIVIEN" => RoleType.Admin,
            "HR" or "NHANSU" or "QUANLYNHANSU" => RoleType.HR,
            "MANAGER" or "QUANLY" or "TRUONGPHONG" or "TRUONG_PHONG" => RoleType.Manager,
            "EMPLOYEE" or "NHANVIEN" or "NHAN_VIEN" => RoleType.Employee,
            // IT không phải từ khoá cột: "IT" đúng nghĩa là Information
            // Technology. Vẫn cần chấp nhận mã số và cách viết tiếng Việt
            // có dấu, vì tên vai trò trong CSDL do người quản trị gõ tay qua
            // màn hình danh mục chứ không phải hằng số trong code.
            "IT" or "5" or "CNTT" or "IT_SUPPORT" or "IT_MANAGER" or "ITSUPPORT" or "ITMANAGER"
                or "CONGNGHETHONGTIN" => RoleType.IT,
            _ => null
        };
    }

    public static bool IsAdmin(string? role) => NormalizeRole(role) == RoleType.Admin;
    public static bool IsHR(string? role) => NormalizeRole(role) == RoleType.HR;
    public static bool IsManager(string? role) => NormalizeRole(role) == RoleType.Manager;

    /// <summary>
    /// Bộ phận CNTT — vai trò được giao cấp phát tài khoản đăng nhập.
    /// </summary>
    public static bool IsIT(string? role) => NormalizeRole(role) == RoleType.IT;

    /// <summary>
    /// Vai trò được phép vào cổng Web Admin để thao tác.
    ///
    /// <para>
    /// Employee tự phục vụ trên Mobile App nên bị chặn ở cổng Web. IT thì vào
    /// được: cấp phát tài khoản là công việc chính của họ, và làm trên Web vì
    /// cần đọc CSDL để đối chiếu hồ sơ nhân viên.
    /// </para>
    /// </summary>
    public static bool CanAccessWebPortal(string? role) =>
        NormalizeRole(role) is RoleType.Admin or RoleType.HR or RoleType.Manager or RoleType.IT;

    // ═══════════════════════════════════════════════════════════════════════
    //  QUY TRÌNH "HR DUYỆT HĐ → IT CẤP TÀI KHOẢN" — TÁCH HAI HÀM, KHÔNG GỘP
    // ═══════════════════════════════════════════════════════════════════════
    //
    //  VÌ SAO PHẢI TÁCH (đây là ranh giới bảo mật, không phải lựa chọn UI):
    //
    //  Quy trình cấp tài khoản có HAI bước khác hẳn về bản chất rủi ro:
    //
    //    1. GỬI YÊU CẦU — chỉ là ghi thêm một dòng vào bảng nội bộ
    //       yeu_cau_tai_khoan. Không có tác dụng phụ, không mở cửa nào,
    //       không sinh ra user nào trong auth.users.
    //
    //    2. XỬ LÝ — gọi POST /auth/v1/admin/users bằng service_role để tạo
    //       user thật trong Supabase Auth. Đây CHÍNH LÀ CỬA VÀO của mọi
    //       RLS policy trên toàn bộ dữ liệu khách sạn: token mà GoTrue cấp
    //       cho user đó là thứ duy nhất Mobile App dùng để đọc/ghi dữ liệu.
    //
    //  Nếu gộp cả hai thành một hàm "được cấp tài khoản" và tra quyền đó cho
    //  HR, thì HR tự gửi yêu cầu rồi tự bấm "Cấp tài khoản" — tức là tự cấp
    //  cho mình (và cho bất kỳ ai) một tài khoản App. Đó là leo quyền, và
    //  các trigger CSDL chỉ chặn được phần dễ thấy, không chặn được chuyện
    //  này.
    //
    //  Vì vậy CanProcessAccountRequest bám đúng danh sách vai trò được gán mã
    //  quyền CAP_TAI_KHOAN_MOBILE trong CSDL (ma_vai_tro 1 Admin và 5 IT),
    //  để policy ở tầng C# và policy trong trigger chan_yeu_cau_tai_khoan_dung_quyen
    //  luôn nói chung một điều. Lệch một bên là một bên vô hiệu.

    /// <summary>
    /// Quyền tạo/gửi yêu cầu cấp tài khoản — người quản lý hồ sơ nhân sự và hợp đồng.
    /// </summary>
    public static bool CanSubmitAccountRequest(string? role)
        => NormalizeRole(role) is RoleType.Admin or RoleType.HR;

    /// <summary>
    /// Quyền xử lý yêu cầu — chỉ Admin và IT, đúng với mã quyền CAP_TAI_KHOAN_MOBILE.
    /// </summary>
    public static bool CanProcessAccountRequest(string? role)
        => NormalizeRole(role) is RoleType.Admin or RoleType.IT;

    /// <summary>
    /// Kiểm tra người gửi có quyền đề xuất vai trò mục tiêu:
    /// Chỉ Quản trị viên (Admin) mới được đề xuất cấp vai trò Admin.
    /// </summary>
    public static bool CanProposeRole(string? senderRole, string? targetRole)
    {
        var normalizedSender = NormalizeRole(senderRole);
        var normalizedTarget = NormalizeRole(targetRole);

        if (normalizedTarget == RoleType.Admin)
        {
            return normalizedSender == RoleType.Admin;
        }

        return normalizedSender is RoleType.Admin or RoleType.HR;
    }

    /// <summary>
    /// Quyền tạo/gửi yêu cầu thăng chức — chỉ Admin và HR.
    /// </summary>
    public static bool CanSubmitPromotionRequest(string? senderRole)
        => NormalizeRole(senderRole) is RoleType.Admin or RoleType.HR;

    /// <summary>
    /// Quy tắc nghiệp vụ thăng chức:
    /// Nhân viên (Employee) CHỈ có thể được thăng chức lên Quản lý (Manager).
    /// </summary>
    public static bool CanPromoteRole(string? currentRole, string? targetRole)
    {
        var normalizedCurrent = NormalizeRole(currentRole);
        var normalizedTarget = NormalizeRole(targetRole);

        return normalizedCurrent == RoleType.Employee && normalizedTarget == RoleType.Manager;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của hợp đồng lao động làm căn cứ cấp tài khoản:
    /// - Trạng thái: "HIEU_LUC"
    /// - Đã bắt đầu: ngay_bat_dau &lt;= today
    /// - Chưa hết hạn: ngay_ket_thuc == null (không xác định thời hạn) HOẶC ngay_ket_thuc &gt;= today (chấp nhận hết hạn trong hôm nay).
    /// </summary>
    public static bool IsValidContractForAccountRequest(
        string? trangThai,
        DateOnly ngayBatDau,
        DateOnly? ngayKetThuc,
        DateOnly today)
    {
        if (!string.Equals(trangThai, "HIEU_LUC", StringComparison.OrdinalIgnoreCase))
            return false;

        if (ngayBatDau > today)
            return false;

        if (ngayKetThuc.HasValue && ngayKetThuc.Value < today)
            return false;

        return true;
    }

    /// <summary>
    /// Liệt kê các mã loại đơn mà vai trò được phép tiếp nhận và duyệt.
    /// Dùng để lọc danh sách đơn ngay ở tầng truy vấn (ví dụ khối "Cần bạn xử lý"
    /// trên bảng điều khiển) thay vì tải toàn bộ đơn rồi mới lọc trong bộ nhớ.
    /// </summary>
    public static IReadOnlyCollection<int> GetReviewableRequestTypes(string? role)
    {
        return NormalizeRole(role) switch
        {
            // Admin xem tất cả các loại đơn trên toàn khách sạn
            RoleType.Admin => ManagerAllowedTypes.Union(HrAllowedTypes).ToArray(),
            // HR duyệt toàn bộ 5 loại đơn, toàn khách sạn
            RoleType.HR => HrAllowedTypes.ToArray(),
            // Manager duyệt loại 1, 2, 4 nhưng còn bị giới hạn cùng phòng ban
            RoleType.Manager => ManagerAllowedTypes.ToArray(),
            // Employee hoặc vai trò không xác định không được tiếp nhận đơn
            _ => []
        };
    }

    /// <summary>
    /// Kiểm tra người dùng có quyền xem đơn từ này hay không theo nguyên tắc Fail-Closed.
    /// </summary>
    /// <remarks>
    /// <paramref name="reviewerEmployeeId"/> và <paramref name="requestEmployeeId"/>
    /// là mã nhân viên của người duyệt và của người nộp đơn. Khi cả hai trùng nhau
    /// thì đó là đơn của chính người đang xem — mọi vai trò trừ Admin đều không được
    /// thấy, để không lộ trạng thái đơn qua chính bộ lọc quyền.
    /// </remarks>
    public static bool CanView(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        var normalized = NormalizeRole(role);
        if (normalized == null)
            return false;

        // Không ai tự duyệt đơn của chính mình, trừ quản trị viên là vai trò đứng
        // trên cùng và là thoát vòng lặp khi chính Admin cần nghỉ.
        if (normalized != RoleType.Admin &&
            reviewerEmployeeId.HasValue &&
            requestEmployeeId.HasValue &&
            reviewerEmployeeId.Value == requestEmployeeId.Value)
        {
            return false;
        }

        return normalized switch
        {
            RoleType.Admin => true, // Admin xem tất cả các loại đơn trên toàn khách sạn
            // HR xem toàn bộ 5 loại đơn toàn khách sạn, kể cả đơn của quản lý
            RoleType.HR => HrAllowedTypes.Contains(maLoaiDon),
            RoleType.Manager => reviewerDeptId.HasValue &&
                                requestDeptId.HasValue &&
                                reviewerDeptId.Value == requestDeptId.Value &&
                                ManagerAllowedTypes.Contains(maLoaiDon), // Manager xem loại 1, 2, 4 cùng phòng ban
            _ => false // Employee hoặc vai trò khác không có quyền
        };
    }

    /// <summary>
    /// Kiểm tra thẩm quyền phê duyệt / từ chối đơn theo nguyên tắc Fail-Closed.
    /// </summary>
    public static bool CanReview(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        return CanView(role, reviewerDeptId, requestDeptId, maLoaiDon, reviewerEmployeeId, requestEmployeeId);
    }

    /// <summary>
    /// Liệt kê các mã loại đơn mà người dùng được phép nộp.
    /// </summary>
    public static IReadOnlyCollection<int> GetSubmittableRequestTypes() =>
        SubmittableTypes.OrderBy(t => t).ToArray();

    /// <summary>
    /// Kiểm tra tính hợp lệ toàn diện khi NHÂN VIÊN NỘP đơn (phía app, TSK-18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gọi trước khi ghi để báo lỗi tiếng Việt sớm và đúng nguyên nhân. Đây KHÔNG
    /// phải lớp bảo mật: RLS trên Supabase mới là lớp chặn thật, và hàm này cố
    /// tình không nhận <c>maNhanVien</c> từ client — mã nhân viên luôn lấy từ phiên
    /// đăng nhập, không tin giá trị người dùng gửi lên.
    /// </para>
    /// </remarks>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateCreate(
        int maLoaiDon,
        DateOnly tuNgay,
        DateOnly denNgay,
        string? lyDo,
        DateOnly today)
    {
        if (!SubmittableTypes.Contains(maLoaiDon))
        {
            return (false, "INVALID_TYPE", "Loại đơn không hợp lệ. Vui lòng chọn lại loại đơn.");
        }

        if (tuNgay < today)
        {
            return (false, "PAST_DATE", "Ngày bắt đầu không được nằm trong quá khứ.");
        }

        // Đơn thôi việc có quy định báo trước riêng; không áp dụng giới hạn đơn nghỉ.
        if (maLoaiDon is >= 1 and <= 4 && tuNgay == today)
            return (false, "SAME_DAY", "Đơn nghỉ phải được gửi trước ngày bắt đầu nghỉ.");
        if (maLoaiDon is >= 1 and <= 4 && tuNgay.DayNumber - today.DayNumber > 30)
            return (false, "TOO_EARLY", "Chỉ được gửi đơn nghỉ trước tối đa 30 ngày.");

        if (denNgay < tuNgay)
        {
            return (false, "INVALID_PERIOD", "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        }

        // Nghỉ thai sản theo chế độ là 06 tháng; chặn sớm để không tạo đơn 6 tháng
        // nhưng người dùng định gõ nhầm 6 năm. Còn lại thì không đặt trần cứng —
        // trần nghiệp vụ thuộc quy chế từng khách sạn, chặn ở đây chỉ bắt nhập nhầm.
        if (maLoaiDon == 3 && denNgay.DayNumber - tuNgay.DayNumber + 1 > 183)
        {
            return (false, "TOO_LONG", "Đơn nghỉ thai sản không được dài quá 06 tháng.");
        }

        // ly_do là TEXT nên CSDL không giới hạn, nhưng dài vô tận thì người duyệt
        // không đọc nổi. 500 ký tự là trần thực tế, đủ cho một đoạn lý do.
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            return (false, "REASON_REQUIRED", "Vui lòng nhập lý do nghỉ.");
        }

        if (lyDo.Trim().Length > 500)
        {
            return (false, "REASON_TOO_LONG", "Lý do nghỉ không được vượt quá 500 ký tự.");
        }

        return (true, null, null);
    }

    /// <summary>
    /// Kiểm tra khoảng ngày xin nghỉ có chồng lấn đơn đang chờ duyệt nào khác không.
    /// </summary>
    /// <remarks>
    /// Hai khoảng [a1,a2] và [b1,b2] chồng nhau khi a1 ≤ b2 và b1 ≤ a2. Chỉ kiểm
    /// các đơn <c>CHO_DUYET</c>: đơn đã bị từ chối thì ngày đó đã được hoàn trả,
    /// nộp lại vẫn hợp lệ.
    /// </remarks>
    public static bool OverlapsAny(
        DateOnly tuNgay,
        DateOnly denNgay,
        IEnumerable<(DateOnly TuNgay, DateOnly DenNgay)> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        foreach (var (existingTu, existingDen) in existing)
        {
            if (tuNgay <= existingDen && existingTu <= denNgay)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra đơn thôi việc có đủ thời hạn báo trước theo Điều 35 BLLĐ 2019 không.
    /// </summary>
    /// <param name="laHopDongKhongXacDinhThoiHan">
    /// true nếu nhân viên đang trong hợp đồng không xác định thời hạn (báo trước 45 ngày).
    /// </param>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateResignationNotice(
        DateOnly ngayThoiViec,
        DateOnly today,
        bool laHopDongKhongXacDinhThoiHan)
    {
        var required = ResignationNoticeDays[laHopDongKhongXacDinhThoiHan];
        var actual = ngayThoiViec.DayNumber - today.DayNumber;

        if (actual < required)
        {
            return (false, "NOTICE_TOO_SHORT",
                $"Theo Điều 35 BLLĐ, đơn thôi việc phải báo trước tối thiểu {required} ngày " +
                $"(hợp đồng {(laHopDongKhongXacDinhThoiHan ? "không xác định thời hạn" : "xác định thời hạn")}). " +
                $"Ngày bạn chọn chỉ còn {actual} ngày.");
        }

        return (true, null, null);
    }

    /// <summary>
    /// Tính số ngày phép năm còn lại trong năm, dựa trên các đơn phép năm đã được duyệt.
    /// </summary>
    /// <param name="daDung">Tổng số ngày nghỉ trong các đơn loại 1 đã có trạng thái <c>DA_DUYET</c>.</param>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateAnnualLeaveBalance(
        int soNgayXinNghi,
        int daDung,
        int quota = AnnualLeaveQuotaDays)
    {
        if (soNgayXinNghi + daDung > quota)
        {
            return (false, "EXCEEDS_QUOTA",
                $"Đơn này xin {soNgayXinNghi} ngày nhưng quỹ phép năm của bạn chỉ còn " +
                $"{Math.Max(0, quota - daDung)} ngày (đã dùng {daDung}/{quota}).");
        }

        return (true, null, null);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ toàn diện khi thực hiện phê duyệt / từ chối đơn.
    /// </summary>
    public static (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateReviewAction(
        string? role,
        int? reviewerDeptId,
        int? requestDeptId,
        int maLoaiDon,
        string currentStatus,
        bool approved,
        string? phanHoi,
        int? reviewerEmployeeId = null,
        int? requestEmployeeId = null)
    {
        // 1. Kiểm tra trạng thái hiện tại của đơn (chỉ cho phép xử lý đơn CHO_DUYET)
        if (!string.Equals(currentStatus, "CHO_DUYET", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "ALREADY_REVIEWED", "Đơn từ không còn ở trạng thái chờ duyệt (đã được người khác xử lý).");
        }

        // 2. Chặn tự duyệt đơn của chính mình — tách riêng khỏi CanReview để trả
        // về thông báo đúng nguyên nhân thay vì chung "không có thẩm quyền".
        // Quản trị viên được miễn vì đó là thoát vòng lặp khi chính họ cần nghỉ.
        var normalized = NormalizeRole(role);
        if (normalized != RoleType.Admin &&
            reviewerEmployeeId.HasValue &&
            requestEmployeeId.HasValue &&
            reviewerEmployeeId.Value == requestEmployeeId.Value)
        {
            return (false, "SELF_REVIEW_FORBIDDEN", "Bạn không thể tự duyệt đơn của chính mình. Đơn sẽ được chuyển lên cấp trên.");
        }

        // 3. Kiểm tra thẩm quyền RBAC
        if (!CanReview(role, reviewerDeptId, requestDeptId, maLoaiDon, reviewerEmployeeId, requestEmployeeId))
        {
            return (false, "FORBIDDEN", "Bạn không có thẩm quyền duyệt loại đơn này.");
        }

        // 4. Quy tắc từ chối: Bắt buộc có lý do
        if (!approved && string.IsNullOrWhiteSpace(phanHoi))
        {
            return (false, "VALIDATION_ERROR", "Bắt buộc phải nhập lý do khi từ chối đơn.");
        }

        // 5. Giới hạn độ dài phản hồi tối đa 255 ký tự theo schema database
        if (!string.IsNullOrEmpty(phanHoi) && phanHoi.Trim().Length > 255)
        {
            return (false, "VALIDATION_ERROR", "Phản hồi duyệt không được vượt quá 255 ký tự.");
        }

        return (true, null, null);
    }
}
