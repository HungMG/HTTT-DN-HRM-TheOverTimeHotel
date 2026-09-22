using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.Data.Supabase;
using OvertimeHotel.HRM.WebAdmin.Security;

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
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Cấu hình Cookie Authentication bảo mật cho Web Admin
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "OverTimeHotel_HRM_Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PermissionCodes.ManageAccounts, policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim(PermissionCodes.ClaimType, PermissionCodes.ManageAccounts)));
    options.AddPolicy(PermissionCodes.ManagePermissions, policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim(PermissionCodes.ClaimType, PermissionCodes.ManagePermissions)));
    options.AddPolicy(PermissionCodes.BackupDatabase, policy => policy.RequireAssertion(context =>
        context.User.IsInRole("Admin") || context.User.HasClaim(PermissionCodes.ClaimType, PermissionCodes.BackupDatabase)));
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

// Đăng ký dịch vụ sao lưu & phục hồi CSDL
builder.Services.AddScoped<IBackupService, BackupService>();

// Cấu hình Supabase Client cho các tác vụ Realtime / Storage
var supabaseSection = builder.Configuration.GetSection("Supabase");
var supabaseUrl = supabaseSection["Url"] ?? "";
var supabaseKey = supabaseSection["PublishableKey"] ?? supabaseSection["AnonKey"] ?? "";
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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
