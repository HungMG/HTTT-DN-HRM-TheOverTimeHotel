using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>Read-only HR aggregates, authorized by Supabase using the user's JWT.</summary>
public sealed class HrSupabaseService
{
    private readonly Func<string, Task<string>> _read;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public HrSupabaseService(SupabaseRestClient rest) : this(path => rest.GetAsync(path)) { }
    public HrSupabaseService(Func<string, Task<string>> read) => _read = read;
    public Task<HrDashboardResponse> GetDashboardAsync() => ReadAsync<HrDashboardResponse>("dashboard");
    public Task<StatisticsResponse> GetStatisticsAsync() => ReadAsync<StatisticsResponse>("statistics");
    private async Task<T> ReadAsync<T>(string view) =>
        JsonSerializer.Deserialize<T>(await _read($"rpc/hrm_hr_overview?p_view={view}"), JsonOptions)
        ?? throw new InvalidOperationException("Supabase trả về dữ liệu thống kê không hợp lệ.");
}
