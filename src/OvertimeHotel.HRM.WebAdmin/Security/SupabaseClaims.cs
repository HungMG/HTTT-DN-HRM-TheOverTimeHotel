using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace OvertimeHotel.HRM.WebAdmin.Security;

/// <summary>
/// Đọc claim do Supabase GoTrue cấp trong phiên Mobile.
/// </summary>
/// <remarks>
/// <para>
/// JWT của Supabase đặt mã nhân viên, mã vai trò và danh sách quyền vào
/// <c>app_metadata</c> (khối JSON lồng), KHÔNG đặt ở top-level. Hàm
/// <c>JwtSecurityTokenHandler</c> không tự rải claim lồng ra nhiều claim phẳng —
/// nó chỉ nạp đúng các key top-level. Vì vậy phải tự đọc lồng rồi rải ra.
/// </para>
///
/// <para>
/// <b>Claim <c>app_metadata</c> đã là nội dung của khối lồng.</b> Không có
/// lớp bọc "app_metadata" thứ hai bên trong nó. Đây là điểm dễ sai nhất khi
/// đọc token Supabase: JSON gốc trong token là
/// <c>{"app_metadata":{"ma_nhan_vien":…}}</c>, nhưng giá trị claim mang tên
/// <c>app_metadata</c> thì đã là <c>{"ma_nhan_vien":…}</c>.
/// </para>
///
/// <para>
/// Rải xong thì phát ra claim phẳng tên <c>ma_nhan_vien</c> / <c>ma_vai_tro</c>,
/// đồng thời nhân bản từng mã quyền thành claim <see cref="PermissionCodes.ClaimType"/>
/// để các policy RBAC so khớp được.
/// </para>
///
/// <para>
/// <b>Không tin mù quáng.</b> Giá trị ở đây chỉ là gợi ý để tra CSDL. Controller
/// vẫn phải nạp lại <c>TaiKhoan</c> và kiểm tra <c>TrangThai</c>, vì token có
/// thể còn hợp lệ sau khi quản trị viên khoá tài khoản.
/// </para>
/// </remarks>
public static class SupabaseClaims
{
    /// <summary>Claim phẳng mang mã nhân viên, phát ra từ <c>app_metadata.ma_nhan_vien</c>.</summary>
    public const string MaNhanVienClaim = "ma_nhan_vien";

    /// <summary>Claim phẳng mang mã vai trò, phát ra từ <c>app_metadata.ma_vai_tro</c>.</summary>
    public const string MaVaiTroClaim = "ma_vai_tro";

    private const string AppMetadataKey = "app_metadata";

    /// <summary>
    /// Trích mã nhân viên từ claim đã rải phẳng. Trả <c>null</c> khi token không
    /// mang claim này — tức hook của Supabase chưa chạy, hoặc tài khoản không
    /// khớp dòng <c>tai_khoan</c> nào.
    /// </summary>
    public static int? GetMaNhanVien(ClaimsPrincipal? user) =>
        ReadIntClaim(user, MaNhanVienClaim);

    /// <summary>Trích mã vai trò, cùng ngữ nghĩa fail-closed như <see cref="GetMaNhanVien"/>.</summary>
    public static int? GetMaVaiTro(ClaimsPrincipal? user) =>
        ReadIntClaim(user, MaVaiTroClaim);

    /// <summary>
    /// Trả về tất cả mã quyền (chuỗi) mà token mang, ví dụ
    /// <c>["CHAM_CONG_DI_DONG", "NOP_DON_TU"]</c>.
    /// </summary>
    public static IReadOnlyList<string> GetQuyen(ClaimsPrincipal? user) =>
        user is null
            ? []
            : user.FindAll(PermissionCodes.ClaimType).Select(c => c.Value).ToArray();

    /// <summary>
    /// Rải <c>app_metadata</c> ra các claim phẳng. Gắn vào
    /// <c>JwtBearerEvents.OnTokenValidated</c>.
    /// </summary>
    /// <remarks>
    /// <b>Không để JsonElement sống vượt quá tài liệu.</b>
    /// <c>JsonElement</c> là struct chỉ giữ con trỏ vào bộ đệm của
    /// <c>JsonDocument</c> cha. Hàm này <c>Clone()</c> khối <c>app_metadata</c>
    /// để nó tự quản lý vòng đời, rồi chỉ giữ lại <c>string</c> / <c>int</c> —
    /// vốn là giá trị, không phải con trỏ. Bỏ Clone ở đây sẽ tái hiện đúng lỗi
    /// <c>ObjectDisposedException</c> đã gặp ở
    /// <c>OvertimeHotel.HRM.MobileApp/Services/AuthService.cs</c>.
    /// </remarks>
    public static Task FlattenAppMetadataAsync(TokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;
        if (identity is null)
        {
            return Task.CompletedTask;
        }

        // Đã rải rồi thì bỏ qua. JwtBearer có thể gọi lại handler khi token được
        // làm mới giữa phiên, và phát trùng claim làm log xác thực dài ngoằng.
        if (identity.HasClaim(c => c.Type == MaNhanVienClaim))
        {
            return Task.CompletedTask;
        }

        var appMetadata = ReadAppMetadata(context.Principal);
        if (appMetadata is null)
        {
            return Task.CompletedTask;
        }

        var meta = appMetadata.Value;
        var maNhanVien = ReadInt(meta, "ma_nhan_vien");
        if (maNhanVien is > 0)
        {
            identity.AddClaim(new Claim(MaNhanVienClaim, maNhanVien.Value.ToString(CultureInfo.InvariantCulture)));
        }

        var maVaiTro = ReadInt(meta, "ma_vai_tro");
        if (maVaiTro is > 0)
        {
            identity.AddClaim(new Claim(MaVaiTroClaim, maVaiTro.Value.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var quyen in ReadStringArray(meta, "quyen"))
        {
            // Bỏ rỗng để tránh sinh claim rỗng: nó làm mọi policy "không khớp"
            // mà log lại trông như đã cấp quyền.
            identity.AddClaim(new Claim(PermissionCodes.ClaimType, quyen));
        }

        return Task.CompletedTask;
    }

    private static JsonElement? ReadAppMetadata(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(AppMetadataKey)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);

            // Claim "app_metadata" đã là BẢN THÂN nội dung của khối lồng, KHÔNG
            // phải bọc thêm một lớp "app_metadata" nữa. Giá trị của nó đúng là
            // {"ma_nhan_vien":…,"ma_vai_tro":…,"quyen":[…]}.
            //
            // Trước đây hàm này lại TryGetProperty("app_metadata") thêm lần nữa
            // nên LUÔN trả null — mọi request Mobile đều 401 dù token hoàn toàn
            // hợp lệ. Chấp nhận cả hai dạng để không phụ thuộc vào hành vi map
            // claim của thư viện: giá trị có bọc thì bóc tiếp, còn không thì coi
            // chính nó là app_metadata.
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(AppMetadataKey, out var nested) &&
                nested.ValueKind == JsonValueKind.Object)
            {
                return nested.Clone();
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Clone() tạo bản sao tự quản lý vòng đời, sống được sau khi doc Dispose.
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Claim hỏng dạng thì coi như không có quyền. Fail-closed: thiếu claim
            // thì controller trả 401, còn hơn ném exception 500.
            return null;
        }
    }

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // Mọi phần tử được đọc thành string NGAY khi document còn sống — không giữ
        // JsonElement nào ra khỏi đây.
        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                result.Add(item.GetString()!);
            }
        }

        return result;
    }

    private static int? ReadIntClaim(ClaimsPrincipal? user, string claimType)
    {
        var raw = user?.FindFirst(claimType)?.Value;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}