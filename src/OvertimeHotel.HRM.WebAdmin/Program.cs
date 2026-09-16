using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.Data.Supabase;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cấu hình kết nối Entity Framework Core với Supabase PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
