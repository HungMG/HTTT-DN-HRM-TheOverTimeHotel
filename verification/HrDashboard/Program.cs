using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.Services;
using OvertimeHotel.HRM.MobileApp.ViewModels;
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var data = new HrDashboardResponse { UpdatedAt = DateTimeOffset.UtcNow, Staff = [new() { StaffCode = "123", FullName = "Kiểm Thử", Department = "Buồng phòng" }] };
var stats = new StatisticsResponse { Period = "Tháng 11/2026", TotalEmployees = 1, Departments = [new() { Name = "Buồng phòng", Count = 1, Rate = 100 }] };
bool fail = false;
var paths = new List<string>();
var service = new HrSupabaseService(path => {
 paths.Add(path);
 if (fail) throw new HttpRequestException("offline");
 return Task.FromResult(path.EndsWith("dashboard") ? JsonSerializer.Serialize(data, options) : JsonSerializer.Serialize(stats, options));
});
var vm = new HrHomeViewModel(new Auth(), new Leaves(), service);
var statistics = new HrStatisticsViewModel(service);
await vm.LoadAsync();
Check(vm.LoadError == "" && vm.MorningShiftText == "0/0" && vm.ReviewedLeaveCount == 0 && vm.ExpiringContractCount == 0, "Zero values stay zero");
Check(vm.FilterResultText.Contains("1 nhân sự"), "Directory comes from Supabase payload");
vm.SearchKeyword = "Thử";
Check(vm.SearchResults.Single().StaffCode == "123", "Search uses real employee ID");
data.Morning.CheckedIn = 2; data.Morning.Scheduled = 3; data.ExpiringContractCount = 4;
await vm.LoadAsync();
Check(vm.MorningShiftText == "2/3" && vm.ExpiringContractCount == 4, "Refresh reflects changed Supabase data");
await statistics.LoadAsync();
Check(statistics.Summary.StartsWith("1 nhân viên") && statistics.Departments.Single().Rate == 100 && !statistics.HasError, "Statistics reads Supabase aggregates");
fail = true;
await vm.LoadAsync(); await statistics.LoadAsync();
Check(vm.LoadError.Length > 0 && vm.MorningShiftText == "—" && vm.SearchResults.Count == 0 && vm.DailyEvents.Count == 0 && vm.LastUpdatedText == "Chưa cập nhật", "Dashboard error clears stale data");
Check(statistics.HasError && statistics.Summary == "—" && statistics.Departments.Count == 0, "Statistics error clears stale data");
fail = false; await vm.LoadAsync(); await statistics.LoadAsync();
Check(vm.LoadError == "" && !statistics.HasError && vm.MorningShiftText == "2/3", "Recovery reloads data");
Check(paths.All(x => x.StartsWith("rpc/hrm_hr_overview?p_view=")) && paths.Any(x => x.EndsWith("statistics")), "Both pages use Supabase RPC without WebAdmin");
Console.WriteLine("All 9 HR Supabase checks passed.");
static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
sealed class Auth : IAuthService
{
 public UserSession CurrentUser { get; } = new() { HoTen = "HR", TenVaiTro = "HR", IsLoggedIn = true };
 public Task<string?> GetTokenAsync() => Task.FromResult<string?>("test-token");
}
sealed class Leaves : ILeaveApprovalService { public Task<int> GetPendingCountAsync(string role, int? department) => Task.FromResult(0); }
