using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.Data.Supabase;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

var builder = WebApplication.CreateBuilder(args);

// Lưu khóa Data Protection trong workspace khi chạy Development.
// Môi trường sandbox không cho phép ghi vào thư mục AppData mặc định của người dùng.
if (builder.Environment.IsDevelopment())
{
    var dataProtectionKeysPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "DataProtection-Keys");

    Directory.CreateDirectory(dataProtectionKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("OvertimeHotel.HRM.WebAdmin");
}

// Xóa các logger hệ thống yêu cầu quyền Administrator (như Windows EventLog)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
// RazorRuntimeCompilation chỉ bật ở Development để sửa .cshtml có hiệu lực ngay
// mà không tăng thời gian biên dịch và bộ nhớ ở môi trường Production.
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddMemoryCache();

// Cấu hình Coexistence Authentication: Cookie Auth cho Web Admin & JWT Bearer cho Mobile API
//
// Đọc Supabase ở đây (trước AddAuthentication) vì MobileBearer cần địa chỉ JWKS
// để xác thực token do GoTrue ký. Khối cấu hình Supabase chi tiết vẫn giữ
// nguyên ở dưới để dùng cho SupabaseClient.
var supabaseUrl = builder.Configuration.GetSection("Supabase")["Url"] ?? "";
var supabaseAuthIssuer = $"{supabaseUrl.TrimEnd('/')}/auth/v1";

// Jwt:Key HS256 KHÔNG còn dùng để xác thực token Mobile nữa (token do GoTrue ký
// ES256, xác thực qua JWKS). Biến này vẫn phải được fail-closed vì
// MobileAuthController — endpoint đăng nhập kiểu cũ — dùng nó để ký token.
// Jwt:Issuer/Jwt:Audience thuộc về lớp xác thực HS256 nên không còn ở đây;
// MobileAuthController đọc trực tiếp từ IConfiguration.
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_KEY")))
{
    throw new InvalidOperationException("Bảo mật hệ thống (Fail-Closed): Khóa bí mật Jwt:Key chưa được cấu hình trong appsettings hoặc biến môi trường JWT_KEY.");
}

// Khoá ký vé dữ liệu Trợ lý AI (xem Security/AiContextTokenService.cs). Cố ý KHÔNG
// chặn khởi động như Jwt:Key ở trên: thiếu khoá này chỉ làm tính năng hỏi số liệu
// của chatbot tắt đi, không được phép hạ gục cả hệ thống quản trị. Nhưng cũng
// không được âm thầm ký bằng khoá rỗng — AiContextTokenService ném lỗi rõ ràng khi dùng.
if (string.IsNullOrWhiteSpace(builder.Configuration[AiContextTokenService.KeyConfigPath]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AiContextTokenService.KeyEnvVar)))
{
    Console.WriteLine(
        "[Cảnh báo] Chưa cấu hình '" + AiContextTokenService.KeyConfigPath +
        "': Trợ lý AI sẽ không truy cập được số liệu báo cáo. " +
        "Đặt khoá trong appsettings.Development.json hoặc biến môi trường " +
        AiContextTokenService.KeyEnvVar + ".");
}

// JWKS của Supabase. Tự tải và tự làm mới: Supabase xoay vòng khoá ký, nên
// hardcode khoá sẽ hỏng ngay sau lần xoay đầu tiên.
//
// Đi qua .well-known/openid-configuration chứ không tải thẳng jwks.json, vì
// thư viện IdentityModel không kèm sẵn IConfigurationRetriever<JsonWebKeySet>.
// Endpoint discovery của Supabase trả về jwks_uri (khoá công khai, an toàn) và
// sẵn cả "issuer" khớp đúng ValidIssuer ở trên.
//
// Trả về delegate IssuerSigningKeyResolver vì TokenValidationParameters yêu cầu
// IEnumerable<SecurityKey>, không nhận IConfigurationManager. Resolver được gọi
// mỗi request và tự chọn đúng khoá theo "kid" trên header token.
static IssuerSigningKeyResolver CreateSupabaseJwks(string authIssuer)
{
    var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{authIssuer.TrimEnd('/')}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever())
    {
        // 12 giờ. Khoá Supabase hiếm khi xoay, và mỗi lần tải lại là một
        // HTTP request ra ngoài. Khi Supabase xoay vòng sang kid mới thì token
        // mới không khớp khoá nào trong cache, và handler tự tải lại ngay.
        AutomaticRefreshInterval = TimeSpan.FromHours(12)
    };

    return (token, securityToken, kid, validationParameters) =>
    {
        var keys = configurationManager.GetConfigurationAsync(CancellationToken.None)
            .GetAwaiter().GetResult()
            .SigningKeys;

        // Ưu tiên khoá khớp "kid" trên header token. Không lọc thì handler thử
        // lần lượt từng khoá — chậm, và dễ báo "signature invalid" một cách gây
        // hiểu nhầm thay vì "không tìm thấy khoá".
        var matched = string.IsNullOrEmpty(kid)
            ? keys
            : keys.Where(k => string.Equals(k.KeyId, kid, StringComparison.Ordinal));

        var resolved = matched.ToArray();

        // Trả cache cũ khi Supabase tạm thời không trả lời: token đã ký hợp lệ
        // vẫn được xác thực bằng khoá cũ, thay vì để mọi phiên sập cùng lúc.
        // Cache cũng rỗng thì trả rỗng — handler sẽ từ chối token (401).
        return resolved.Length > 0 ? resolved : keys;
    };
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "OverTimeHotel_HRM_Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out var userId))
                {
                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var account = await dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(t => t.MaTaiKhoan == userId);
                    if (account == null || !account.TrangThai)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    // Thu hồi phiên làm việc tức thì nếu hợp đồng lao động đã hết hạn
                    var latestContract = await dbContext.HopDongs
                        .AsNoTracking()
                        .Where(h => h.MaNhanVien == account.MaNhanVien)
                        .OrderByDescending(h => h.NgayBatDau)
                        .FirstOrDefaultAsync();

                    var today = DateOnly.FromDateTime(DateTime.Today);
                    if (latestContract != null && latestContract.NgayKetThuc.HasValue && latestContract.NgayKetThuc.Value < today)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }
                }
            }
        };
    })
    .AddJwtBearer("MobileBearer", options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;

        // Mobile App đăng nhập thẳng Supabase nên token do GoTrue ký bằng
        // ES256 và mang định dạng claim riêng (mã nhân viên / mã vai trò nằm
        // trong app_metadata). Tắt ánh xạ claim để giữ nguyên tên key gốc,
        // rồi tự rải app_metadata ra ở OnTokenValidated.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseAuthIssuer,
            ValidateAudience = true,
            ValidAudience = "authenticated",

            // Supabase công bố khoá công khai tại /auth/v1/.well-known/jwks.json.
            // KHÔNG hardcode khoá: Supabase xoay vòng khoá ký (kid), hardcode sẽ
            // khiến mọi token mới bị từ chối sau lần xoay đầu tiên.
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = CreateSupabaseJwks(supabaseAuthIssuer),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                await SupabaseClaims.FlattenAppMetadataAsync(context);

                var maNhanVien = SupabaseClaims.GetMaNhanVien(context.Principal);
                if (maNhanVien is > 0)
                {
                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var account = await dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(t => t.MaNhanVien == maNhanVien.Value);
                    if (account != null && !account.TrangThai)
                    {
                        context.Fail("Tài khoản của bạn đã bị khóa bởi Quản trị viên.");
                        return;
                    }

                    var latestContract = await dbContext.HopDongs
                        .AsNoTracking()
                        .Where(h => h.MaNhanVien == maNhanVien.Value)
                        .OrderByDescending(h => h.NgayBatDau)
                        .FirstOrDefaultAsync();

                    var today = DateOnly.FromDateTime(DateTime.Today);
                    if (latestContract != null && latestContract.NgayKetThuc.HasValue && latestContract.NgayKetThuc.Value < today)
                    {
                        context.Fail($"Hợp đồng lao động của bạn đã hết hạn vào ngày {latestContract.NgayKetThuc.Value:dd/MM/yyyy}. Tài khoản đã tạm khóa truy cập.");
                        return;
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Đăng ký policy RBAC cho từng mã quyền trong bảng QUYEN.
    // Admin luôn đạt mọi policy vì chỉ nắm quyền gốc SYS_ADMIN, các quyền chi tiết
    // được cấp cho HR và Manager mới đi qua claim "Permission".
    void AddPermissionPolicy(string permissionCode)
    {
        options.AddPolicy(permissionCode, policy => policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.HasClaim(PermissionCodes.ClaimType, permissionCode)));
    }

    AddPermissionPolicy(PermissionCodes.ManageAccounts);
    // Cấp user Supabase Auth — cửa vào của mọi RLS policy. Tách khỏi
    // QL_TAI_KHOAN vì người quản lý hồ sơ tài khoản (HR) không nên tự mở cửa này.
    AddPermissionPolicy(PermissionCodes.ProvisionMobileAccount);
    AddPermissionPolicy(PermissionCodes.ManagePermissions);
    AddPermissionPolicy(PermissionCodes.BackupDatabase);
    AddPermissionPolicy(PermissionCodes.ManageCatalog);
    AddPermissionPolicy(PermissionCodes.ManageEmployees);
    AddPermissionPolicy(PermissionCodes.ManageContracts);
    AddPermissionPolicy(PermissionCodes.AssignShifts);
    AddPermissionPolicy(PermissionCodes.ViewDepartmentAttendance);
    // Quẹt thẻ check-in/out trên Mobile là thao tác GHI dữ liệu chấm công, nên
    // phải qua RBAC chứ không chỉ cần token hợp lệ. Mã quyền tương ứng là
    // quyen.ma_quyen_code = 'CHAM_CONG_DI_DONG' (ma_quyen = 15 trong CSDL).
    AddPermissionPolicy(PermissionCodes.MobileCheckIn);
    AddPermissionPolicy(PermissionCodes.CalculatePayroll);
    AddPermissionPolicy(PermissionCodes.ExportPayslipPdf);
    AddPermissionPolicy(PermissionCodes.ApproveResignation);
    AddPermissionPolicy(PermissionCodes.ApproveLeave);
    AddPermissionPolicy(PermissionCodes.ApproveOvertime);
    // Phân hệ Báo cáo & Thống kê (TSK-24). Admin/HR/IT xem toàn khách sạn; Manager
    // vẫn qua được policy này nhưng bị thu hẹp còn bộ phận của mình ở tầng truy vấn
    // (ReportPolicy.ResolveScope) — mã quyền không mang thông tin phạm vi.
    AddPermissionPolicy(PermissionCodes.ReportView);
});

// Cấu hình kết nối Entity Framework Core với Supabase PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            // Tự động thử lại khi gặp sự cố mạng tạm thời (Transient Failure)
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 1,
                maxRetryDelay: TimeSpan.FromSeconds(1),
                errorCodesToAdd: null);
        });
    }
});

// Vé dữ liệu cho Trợ lý AI: WebAdmin ký một JWT ngắn hạn mang danh tính người đang
// đăng nhập, chatbot cầm vé gọi ngược về /api/ai-data/* để xin số liệu tổng hợp.
// Nhờ vậy khoá Supabase không bao giờ phải rời máy chủ WebAdmin.
// Dùng IMemoryCache (đăng ký sẵn ở trên) làm sổ chống dùng lại vé.
builder.Services.AddScoped<AiContextTokenService>();

// Dựng địa chỉ nhúng khung chat Trợ lý AI (ngữ cảnh trang + vé dữ liệu).
// Dùng chung cho drawer nổi ở _Layout.cshtml và trang AiAssistant/Index.cshtml.
builder.Services.AddScoped<AiChatbotUrlBuilder>();

// Đăng ký dịch vụ sao lưu & phục hồi CSDL
builder.Services.AddScoped<IBackupService, BackupService>();

// Dựng số liệu sáu báo cáo thống kê (PRD 10.1 → 10.6). Dùng chung cho trang
// Báo cáo & Thống kê và cho Trợ lý AI, nên hai nơi không thể lệch số liệu.
builder.Services.AddScoped<BaoCaoReportService>();
builder.Services.AddScoped<IAttendanceSummaryService, AttendanceSummaryService>();
builder.Services.AddScoped<IHotelEventService, HotelEventService>();
builder.Services.AddScoped<IPayrollCalculationService, PayrollCalculationService>();

// Cấp phát user Supabase Auth khi tạo tài khoản trên Web.
// Đăng ký Singleton vì cấu hình là bất biến trong suốt vòng đời ứng dụng — đọc
// IConfiguration ở thời điểm đăng ký thay vì mỗi request, và không giữ chuỗi
// khoá bí mật trong object theo vòng đời ngắn. Service tự báo "chưa cấu hình"
// khi thiếu khoá, nên thiếu cấu hình sẽ ra thông báo rõ ràng chứ không im lặng.
builder.Services.AddSingleton<ISupabaseUserProvisioningService>(provider =>
    new SupabaseUserProvisioningService(
        provider.GetRequiredService<IConfiguration>().GetSection("Supabase")["Url"] ?? string.Empty,
        provider.GetRequiredService<IConfiguration>()["Supabase:ServiceRoleKey"]));

// Cấu hình Supabase Client cho các tác vụ Realtime / Storage
// (supabaseUrl đã đọc ở khối authentication phía trên).
var supabaseSection = builder.Configuration.GetSection("Supabase");
// Ưu tiên AnonKey (JWT "eyJ...") vì Supabase Client dùng nó cho cả REST lẫn Auth.
// PublishableKey dạng "sb_publishable_..." là khoá công khai dùng cho Storage/Edge
// Functions: PostgREST từ chối nó với thông điệp "Secret API key required", nên đặt
// sau AnonKey để cấu hình chỉ có mỗi PublishableKey vẫn chạy được, còn có cả hai thì
// lấy đúng khoá nào PostgREST chấp nhận.
var supabaseKey = supabaseSection["AnonKey"] ?? supabaseSection["PublishableKey"] ?? "";
if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
{
    builder.Services.AddSingleton(provider =>
        SupabaseClientFactory.CreateClient(supabaseUrl, supabaseKey));
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var schemaScope = app.Services.CreateAsyncScope();
    var schemaContext = schemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var schemaLogger = schemaScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSchema");

    try
    {
        await DatabaseSchemaInitializer.EnsureAdminAuditLogTableAsync(schemaContext);
    }
    catch (Exception ex)
    {
        schemaLogger.LogWarning(ex, "Không thể tự tạo bảng nhật ký quản trị. Hãy chạy migration 20260918_add_admin_audit_log.sql trên Supabase.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Phục vụ trực tiếp các file trong wwwroot. Cần đặt trước endpoint routing để
// tránh phản hồi rỗng khi trình duyệt yêu cầu CSS/JS với Accept-Encoding: gzip.
app.UseStaticFiles();

app.UseRouting();

// Thứ tự Middleware quan trọng: UseAuthentication phải trước UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
