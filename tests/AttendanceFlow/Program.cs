using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;

// Offline writes affect demo memory only. --live performs auth and SELECTs only.
var passed = 0;
var failed = 0;
async Task Check(string name, Func<Task> run)
{
    try { await run(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
}

var services = new ServiceCollection().AddLogging();
services.AddSingleton<IAuthService, DemoAuthService>();
services.AddSingleton<DemoLeaveApprovalService>();
services.AddSingleton<ILeaveApprovalService>(p => p.GetRequiredService<DemoLeaveApprovalService>());
services.AddSingleton<IAttendanceService, DemoAttendanceService>();
services.AddSingleton<IMobileAttendanceSummaryService, DemoAttendanceSummaryService>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
var auth = provider.GetRequiredService<IAuthService>();
var leaves = provider.GetRequiredService<ILeaveApprovalService>();
var summary = provider.GetRequiredService<IMobileAttendanceSummaryService>();
await auth.LoginAsync("hr", "hr123");
var request = (await leaves.GetLeaveRequestsAsync()).Items.Single(r => r.MaDon == 101);
var year = request.TuNgay.Year;
var month = request.TuNgay.Month;

await Check("Demo resolves the shared approval singleton", () =>
{
    Require(ReferenceEquals(leaves, provider.GetRequiredService<DemoLeaveApprovalService>()), "Two separate approval datasets.");
    return Task.CompletedTask;
});
await Check("HR approval changes the next summary read without accumulating credit", async () =>
{
    var before = await summary.GetSummaryAsync(year, month, request.MaNhanVien);
    Require(before.Days.All(d => d.LeaveRequests.All(r => r.RequestId != request.MaDon)), "Pending leave was credited.");
    var result = await leaves.ApproveAsync(request.MaDon);
    Require(result.Success, result.Message);
    var after = await summary.GetSummaryAsync(year, month, request.MaNhanVien);
    var relevant = after.Days.Where(d => d.LeaveRequests.Any(r => r.RequestId == request.MaDon)).ToArray();
    Require(relevant.Length > 0, "Approved leave was not propagated.");
    Require(relevant.All(d => d.CreditedDays == (d.HasSchedule ? 1 : 0)), "Credit ignores schedules.");
    var repeated = await summary.GetSummaryAsync(year, month, request.MaNhanVien);
    Require(after.TotalCreditedDays == repeated.TotalCreditedDays, "Repeated reads accumulated credit.");
});
await Check("Rejecting leave contributes no paid leave", async () =>
{
    var pending = (await leaves.GetLeaveRequestsAsync()).Items.Single(r => r.MaDon == 102);
    var result = await leaves.RejectAsync(pending.MaDon, "Offline acceptance check");
    Require(result.Success, result.Message);
    var value = await summary.GetSummaryAsync(pending.TuNgay.Year, pending.TuNgay.Month, pending.MaNhanVien);
    Require(value.Days.All(d => d.LeaveRequests.All(r => r.RequestId != pending.MaDon)), "Rejected leave was credited.");
});
await Check("Employee can view own summary and cannot view a colleague", async () =>
{
    await auth.LoginAsync("employee", "emp123");
    Require((await summary.GetSummaryAsync(year, month)).EmployeeId == auth.CurrentUser!.MaNhanVien, "Wrong employee.");
    await Expect<InvalidOperationException>(() => summary.GetSummaryAsync(year, month, 6));
});
await Check("Manager cannot view another department", async () =>
{
    await auth.LoginAsync("manager", "manager123");
    await Expect<InvalidOperationException>(() => summary.GetSummaryAsync(year, month, 12));
});
await Check("Summary refuses invalid months, cancelled calls and logged out sessions", async () =>
{
    await Expect<InvalidOperationException>(() => summary.GetSummaryAsync(year, 13));
    await Expect<OperationCanceledException>(() => summary.GetSummaryAsync(year, month, cancellationToken: new CancellationToken(true)));
    await auth.LogoutAsync();
    await Expect<UnauthorizedAccessException>(() => summary.GetSummaryAsync(year, month));
});

await Check("Mobile request writes use authenticated WebAdmin API and propagate conflicts", async () =>
{
    var user = new SessionAuth(new UserSession { IsLoggedIn = true, MaNhanVien = 1, Token = "sample-token" });
    var handler = new WorkflowHandler();
    var api = new WebAdminApiClient(user, new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });
    var swaps = new ShiftSwapService(api);
    var created = await swaps.CreateShiftSwapAsync(new CreateShiftSwapRequest { MaPhanCaGui = 1, MaPhanCaNhan = 2, LyDo = "Test" });
    Require(created.Success && handler.LastPath == "/api/mobile/shift-swaps", "Swap creation did not use the workflow API.");
    Require(handler.LastAuthorization == "Bearer sample-token", "Bearer token was not sent.");
    var conflict = await swaps.RespondShiftSwapAsync(12, new RespondShiftSwapRequest { DongY = true });
    Require(!conflict.Success && conflict.Message == "Đơn đã được rút", "Conflict message was lost.");
    Require(handler.LastPath == "/api/mobile/shift-swaps/12/respond", "Wrong response endpoint.");
    Require(handler.LastBody?.Contains("\"accept\":true") == true, "Response body does not match the API contract.");
    var leave = new LeaveApprovalService(new SupabaseRestClient(user, NullLogger<SupabaseRestClient>.Instance), api, user, NullLogger<LeaveApprovalService>.Instance);
    var leaveResult = await leave.WithdrawRequestAsync(9);
    Require(leaveResult.Success && handler.LastPath == "/api/leaverequests/9/withdraw", "Leave withdrawal did not use the workflow API.");
    var explanations = new AttendanceExplanationService(api);
    var withdrawal = await explanations.WithdrawExplanationAsync(7);
    Require(withdrawal.Success && handler.LastPath == "/api/mobile/attendance-explanations/7/withdraw", "Explanation withdrawal did not use the workflow API.");
    handler.Unauthorized = true;
    var expired = await swaps.WithdrawShiftSwapAsync(12);
    Require(!expired.Success && expired.Message.Contains("đăng nhập lại"), "Expired token was not surfaced.");
});

if (args.Contains("--live"))
{
    var password = Environment.GetEnvironmentVariable("TSK20_TEST_PASSWORD")
        ?? throw new InvalidOperationException("Set TSK20_TEST_PASSWORD for the repo's cloud sample accounts.");
    SessionAuth? employee = null;
    SessionAuth? hr = null;
    await Check("Cloud sample employee authenticates", async () => employee = await SignIn("bao.chau@overtimehotel.com", password));
    await Check("Cloud sample HR authenticates with HR role", async () =>
    {
        hr = await SignIn("sang.vo@overtimehotel.com", password);
        Require(hr.CurrentUser!.IsHR, "Sample HR account has a different role.");
    });
    if (employee != null && hr != null)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var employeeRest = new SupabaseRestClient(employee, NullLogger<SupabaseRestClient>.Instance);
        var hrRest = new SupabaseRestClient(hr, NullLogger<SupabaseRestClient>.Instance);
        var employeeSummary = new MobileAttendanceSummaryService(employeeRest, employee);
        var hrSummary = new MobileAttendanceSummaryService(hrRest, hr);
        AttendanceSummaryDto? own = null;
        await Check("Employee reads current-month real schedules and approved leave", async () =>
        {
            own = await employeeSummary.GetSummaryAsync(now.Year, now.Month);
            Require(own.WorkedDays + own.PaidLeaveDays == own.TotalCreditedDays, "Totals disagree.");
            Console.WriteLine($"  Employee summary: scheduled={own.ScheduledDays}, worked={own.WorkedDays}, paidLeave={own.PaidLeaveDays}, review={own.ReviewDays}");
        });
        await Check("HR reads the same employee and gets identical totals", async () =>
        {
            var value = await hrSummary.GetSummaryAsync(now.Year, now.Month, employee.CurrentUser!.MaNhanVien);
            Require(own != null && value.TotalCreditedDays == own.TotalCreditedDays && value.Days.Count == own.Days.Count,
                "HR and employee views disagree or HR cannot read required relations.");
        });
        await Check("Month change reads another period instead of retaining the old data", async () =>
        {
            var previous = new DateOnly(now.Year, now.Month, 1).AddMonths(-1);
            var value = await employeeSummary.GetSummaryAsync(previous.Year, previous.Month);
            Require(value.Days.All(d => d.Date.Year == previous.Year && d.Date.Month == previous.Month), "Old month rows leaked.");
        });
        await Check("Employee service denies another employee before querying", () =>
            Expect<InvalidOperationException>(() => employeeSummary.GetSummaryAsync(now.Year, now.Month, hr.CurrentUser!.MaNhanVien)));
        await Check("HR approval list provides names and department IDs for employee orders", async () =>
        {
            var approval = new LeaveApprovalService(hrRest, new WebAdminApiClient(hr), hr, NullLogger<LeaveApprovalService>.Instance);
            var items = (await approval.GetLeaveRequestsAsync()).Items;
            Require(items.All(r => r.MaPhongBan.HasValue && !r.HoTenNhanVien.StartsWith("NV#")), "Employee relation was hidden/missing in the approval list.");
            Console.WriteLine($"  Authorized pending orders: {items.Count}");
        });
    }
}

Console.WriteLine($"Attendance flow: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

static void Require(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException(message);
}
static async Task Expect<T>(Func<Task> run) where T : Exception
{
    try { await run(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static async Task<SessionAuth> SignIn(string email, string password)
{
    using var client = new HttpClient { BaseAddress = new Uri(ApiConfig.AuthBaseUrl), Timeout = TimeSpan.FromSeconds(20) };
    client.DefaultRequestHeaders.Add("apikey", ApiConfig.SupabaseAnonKey);
    using var response = await client.PostAsJsonAsync("token?grant_type=password", new { email, password });
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Sample login returned HTTP {(int)response.StatusCode}.");
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var token = json.RootElement.GetProperty("access_token").GetString()!;
    var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
    using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='))));
    var claims = payload.RootElement.TryGetProperty("app_metadata", out var meta) ? meta : payload.RootElement;
    var id = claims.GetProperty("ma_nhan_vien").GetInt32();
    var roleId = claims.GetProperty("ma_vai_tro").GetInt32();
    var auth = new SessionAuth(new UserSession { IsLoggedIn = true, MaNhanVien = id, Token = token });
    var rest = new SupabaseRestClient(auth, NullLogger<SupabaseRestClient>.Instance);
    var roles = AttendanceSummaryPostgrstMapper.ReadRows(await rest.GetAsync($"vai_tro?select=ten_vai_tro&ma_vai_tro=eq.{roleId}"));
    auth.CurrentUser!.TenVaiTro = PostgrstJson.ReadString(roles.Single(), "ten_vai_tro");
    var profiles = AttendanceSummaryPostgrstMapper.ReadRows(await rest.GetAsync($"nhan_vien?select=ma_phong_ban&ma_nhan_vien=eq.{id}"));
    auth.CurrentUser.MaPhongBan = PostgrstJson.ReadNullableInt(profiles.Single(), "ma_phong_ban");
    return auth;
}

sealed class SessionAuth(UserSession session) : IAuthService
{
    public bool IsDemoMode => false;
    public UserSession? CurrentUser { get; private set; } = session;
    public bool IsAuthenticated => CurrentUser?.IsLoggedIn == true;
    public Task<string?> GetTokenAsync() => Task.FromResult(CurrentUser?.Token);
    public Task<string?> RefreshTokenAsync() => GetTokenAsync();
    public Task LogoutAsync() { CurrentUser = null; return Task.CompletedTask; }
    public Task<bool> TryAutoLoginAsync() => Task.FromResult(false);
    public Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe = true) => throw new NotSupportedException();
    public Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword) => throw new NotSupportedException();
}

sealed class WorkflowHandler : HttpMessageHandler
{
    public string? LastPath { get; private set; }
    public string? LastAuthorization { get; private set; }
    public string? LastBody { get; private set; }
    public bool Unauthorized { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        LastPath = request.RequestUri!.AbsolutePath;
        LastAuthorization = request.Headers.Authorization?.ToString();
        if (Unauthorized) return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        if (LastPath.EndsWith("/respond")) return new HttpResponseMessage(System.Net.HttpStatusCode.Conflict) { Content = new StringContent("{\"message\":\"Đơn đã được rút\"}") };
        var body = (LastPath.Contains("attendance-explanations") || LastPath.Contains("leaverequests")) ? "{\"success\":true,\"code\":\"WITHDRAWN\",\"message\":\"OK\"}" : "{\"success\":true,\"message\":\"OK\",\"maYeuCau\":12}";
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
