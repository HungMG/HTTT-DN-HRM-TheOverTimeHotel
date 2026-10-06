namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Cấu hình Endpoint của Mobile App.
///
/// <para>
/// <b>App chỉ nói chuyện với Supabase Cloud.</b> PRD 06 vẽ Mobile App → Supabase
/// REST/Realtime thẳng, không qua Web Admin: auth thì GoTrue, dữ liệu nghiệp vụ
/// thì PostgREST. Vì vậy ở đây không còn URL của Web Admin nào cả.
/// </para>
///
/// <para>
/// <b>Đã xoá gì và vì sao.</b> Trước đây có <c>BaseUrl</c> trỏ
/// <c>https://api.overtimehotel.com</c> cùng cơ chế <c>custom_api_base_url</c> cho
/// phép trỏ sang máy khác. Trên bản Release domain đó không tồn tại (đã kiểm chứng
/// bằng DNS) và <c>ValidateTransportSecurity</c> lại chặn mọi URL không phải HTTPS
/// nên cũng không cứu được. Hai service dùng nó đều rơi vào chỗ bắt
/// <c>HttpRequestException</c> rồi hiện chung thông báo "lỗi tải dữ liệu" — đúng
/// triệu chứng người dùng gặp trên bản cài thật. Xoá hẳn để compiler bắt được chỗ
/// gọi sót ngay lần build sau, thay vì để lại "bẫy" rồi âm thầm hỏng trên máy thật.
/// </para>
///
/// <para>
/// <b>Không còn escape hatch bằng HTTP.</b> Debug trên emulator giờ dùng thẳng
/// Supabase Cloud qua chính các URL này — hoạt động giống hệt bản Release nên
/// emulator và máy thật không lệch nhau.
/// </para>
/// </summary>
public static class ApiConfig
{
    // ------------------------------------------------------------------
    //  Supabase Cloud — nguồn dữ liệu thật của Mobile App
    // ------------------------------------------------------------------
    //  PRD 06 vẽ Mobile App -> Supabase REST thẳng, không đi qua Web Admin.
    //  Vì vậy app tự gọi GoTrue (đăng nhập) + PostgREST (dữ liệu).
    //
    //  AnonKey là khoá CÔNG KHAI theo thiết kế của Supabase: mọi client đều
    //  phải gửi nó nên không thể giấu — app là binary tải về, ai cũng đọc được.
    //  Ranh giới bảo mật thật nằm ở RLS: migration
    //  20261005_hon_ho_so_jwt_quyen_theo_vai_tro.sql đã thu hồi toàn bộ quyền
    //  của role anon, nên dù có khoá này thì cũng không đọc được dòng nào nào.
    //  ServiceRoleKey (khoá bí mật) KHÔNG BAO GIỜ đặt ở đây.
    public const string SupabaseUrl = "https://soethtbttktnwtxyxjrj.supabase.co";

    /// <summary>Khoá anon (JWT "eyJ...") — cả GoTrue lẫn PostgREST đều chấp nhận.</summary>
    public const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNvZXRodGJ0dGt0bnd0eHl4anJqIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1MjkxNjgsImV4cCI6MjEwNTEwNTE2OH0.KoIyzRJQAb9QAP1-cMdkVHzv6ZNlXOV3j2oNFoBhEYE";

    /// <summary>
    /// Gốc GoTrue — nơi cấp và làm mới JWT.
    ///
    /// Dấu "/" CUỐI bắt buộc phải có, và đường dẫn tương đối gọi kèm phải KHÔNG
    /// có dấu "/" ở đầu. HttpClient nối hai bên bằng thuật toán RFC 3986: chỉ cần
    /// một trong hai sai là mất "/auth/v1" và gõi thẳng vào gốc domain.
    ///
    ///   BaseAddress ".../auth/v1/" + "token?..."  ✔  .../auth/v1/token?...
    ///   BaseAddress ".../auth/v1/" + "/token?..." ✘  .../token?...  → 404
    ///   BaseAddress ".../auth/v1"  + "token?..."  ✘  .../token?...  → 404
    ///
    /// 404 kiểu này rất dễ bị nhầm là "sai mật khẩu" vì code chỉ kiểm tra
    /// IsSuccessStatusCode rồi báo thông báo chung. Đã gặp đúng triệu chứng này
    /// trên emulator: nhãn trạng thái đỏ và đăng nhập báo sai thông tin.
    /// </summary>
    public static string AuthBaseUrl => $"{SupabaseUrl}/auth/v1/";

    /// <summary>Gốc PostgREST — nơi đọc/ghi bảng nghiệp vụ. Cùng lý do dấu "/" cuối.</summary>
    public static string RestBaseUrl => $"{SupabaseUrl}/rest/v1/";
}