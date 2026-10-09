namespace OvertimeHotel.HRM.Data.Services;

/// <summary>
/// Kết quả cấp phát một tài khoản trên Supabase Auth.
/// </summary>
/// <param name="Success">Thành công thì <paramref name="SupabaseUserId"/> có giá trị.</param>
/// <param name="SupabaseUserId">UUID của user trong <c>auth.users</c>.</param>
/// <param name="ErrorCode">Mã lỗi để Web hiển thị thông báo tiếng Việt tương ứng.</param>
/// <param name="ErrorMessage">Thông báo lỗi đã dịch sẵn, an toàn để đưa ra UI.</param>
public record SupabaseUserProvisionResult(
    bool Success,
    string? SupabaseUserId,
    SupabaseUserProvisionError ErrorCode,
    string? ErrorMessage)
{
    public static SupabaseUserProvisionResult Succeeded(string supabaseUserId) =>
        new(true, supabaseUserId, SupabaseUserProvisionError.None, null);

    public static SupabaseUserProvisionResult Failed(SupabaseUserProvisionError code, string message) =>
        new(false, null, code, message);
}

public enum SupabaseUserProvisionError
{
    /// <summary>Không có lỗi.</summary>
    None,

    /// <summary>Chưa cấu hình ServiceRoleKey trong appsettings.</summary>
    NotConfigured,

    /// <summary>Email đã có trong <c>auth.users</c> — không tạo trùng.</summary>
    EmailAlreadyExists,

    /// <summary>Mật khẩu không đạt yêu cầu của GoTrue (tối thiểu 6 ký tự).</summary>
    WeakPassword,

    /// <summary>Không gọi được Supabase — timeout hoặc mạng lỗi.</summary>
    NetworkFailure,

    /// <summary>Supabase trả lỗi không xác định.</summary>
    Unexpected
}

/// <summary>
/// Cấp phát user trên Supabase Auth (<c>auth.users</c>) để Mobile App đăng nhập được.
///
/// <para>
/// <b>Vì sao cần lớp này.</b> Hệ thống có hai lớp tài khoản tách biệt: bảng
/// <c>tai_khoan</c> (PostgreSQL, Web Admin đọc) và <c>auth.users</c> (Supabase
/// Auth, Mobile App đọc). Có dòng trong <c>tai_khoan</c> mà không có dòng trong
/// <c>auth.users</c> thì nhân viên đăng nhập được Web nhưng KHÔNG đăng nhập được
/// app — vì GoTrue không cấp token, và mọi policy RLS của app đều chặn.
/// </para>
///
/// <para>
/// <b>Điều kiện bắt buộc.</b> Email truyền vào phải khớp <c>nhan_vien.email</c>
/// (email công vụ) — <c>custom_access_token_hook</c> đối chiếu chính xác cột đó
/// để cấp claim vai trò. Lệch một ký tự là app đăng nhập được nhưng mất toàn bộ
/// quyền, chỉ thấy thông báo chung chung.
/// </para>
///
/// <para>
/// <b>Về khoá.</b> Chỉ endpoint <c>POST /auth/v1/admin/users</c> mới tạo được
/// user hàng loạt, và endpoint đó chỉ nhận khoá <c>service_role</c> — tương
/// đương toàn quyền database. Vì vậy khoá này chỉ được đặt ở phía SERVER
/// (appsettings của Web Admin), TUYỆT ĐỐI không nhúng vào Mobile App.
/// </para>
/// </summary>
public interface ISupabaseUserProvisioningService
{
    /// <summary>
    /// Tạo user trên Supabase Auth với email và mật khẩu đã băm sẵn ở phía Web.
    /// Không ném exception ra ngoài — mọi lỗi đều được gói vào kết quả trả về.
    /// </summary>
    Task<SupabaseUserProvisionResult> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default);
}
