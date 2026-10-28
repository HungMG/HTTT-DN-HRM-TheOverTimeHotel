using Microsoft.Extensions.Caching.Memory;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.WebAdmin.Services;

var clock = new ManualClock();
using var memory = new MemoryCache(new MemoryCacheOptions());
using var cache = new SearchListCache(memory, clock);
var loads = 0;
Task<List<NhanVien>> Load() { loads++; return Task.FromResult(new List<NhanVien> { new() { MaNhanVien = 1, Ho = "Nguyễn", Ten = "An" }, new() { MaNhanVien = 2, Ten = "Bình" } }); }
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }

var first = await cache.GetAsync("employees", Load);
var second = await cache.GetAsync("employees", Load);
Check(loads == 1 && second.Count == 2, "different keyword searches reuse one list load");
first[0].Ten = "Changed";
Check(second[0].Ten == "An", "requests receive independent entity snapshots");
clock.Advance(TimeSpan.FromMinutes(14));
await cache.GetAsync("employees", Load);
Check(loads == 1, "cache survives until 15 minutes");
clock.Advance(TimeSpan.FromMinutes(1));
Check(loads == 1, "expiry does not reload in the background");
await cache.GetAsync("employees", Load);
Check(loads == 2, "first request at absolute expiry reloads");
cache.Invalidate();
await cache.GetAsync("employees", Load);
Check(loads == 3, "successful write invalidation reloads on next use");
await cache.GetAsync("department:2", Load);
Check(loads == 4, "different authorization scopes do not share entries");

var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var concurrentLoads = 0;
async Task<List<int>> SlowLoad() { concurrentLoads++; started.TrySetResult(); await gate.Task; return [1]; }
var pending = cache.GetAsync("concurrent", SlowLoad);
await started.Task;
var pending2 = cache.GetAsync("concurrent", SlowLoad);
gate.SetResult();
await Task.WhenAll(pending, pending2);
Check(concurrentLoads == 1, "concurrent misses load only once");

var retryLoads = 0;
try { await cache.GetAsync<List<int>>("failure", () => throw new InvalidOperationException("offline")); } catch (InvalidOperationException) { }
await cache.GetAsync("failure", () => { retryLoads++; return Task.FromResult(new List<int> { 2 }); });
Check(retryLoads == 1, "failed loads are not cached");

var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var staleLoad = cache.GetAsync("write-race", async () => { writeStarted.SetResult(); await release.Task; return new List<int> { 1 }; });
await writeStarted.Task;
cache.Invalidate();
release.SetResult();
await staleLoad;
var afterWrite = await cache.GetAsync("write-race", () => Task.FromResult(new List<int> { 2 }));
Check(afterWrite[0] == 2, "in-flight stale load cannot repopulate cache after a write");

var dept = new PhongBan { MaPhongBan = 1, TenPhongBan = "HR" };
var employee = new NhanVien { MaNhanVien = 3, Ten = "Lan", PhongBan = dept };
dept.NhanViens.Add(employee);
var graph = await cache.GetAsync("cycle", () => Task.FromResult(new List<NhanVien> { employee }));
Check(graph[0].PhongBan!.NhanViens.First() == graph[0], "entity navigation cycles survive snapshot copies");
await ControllerChecks.Run(cache, memory);
await InvalidationChecks.Run(cache);
Console.WriteLine("All search cache checks passed.");

sealed class ManualClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}
