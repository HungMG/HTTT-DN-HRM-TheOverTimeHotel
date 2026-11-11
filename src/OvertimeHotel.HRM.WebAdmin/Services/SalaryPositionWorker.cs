using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

/// <summary>Applies agreed position changes only after their effective date.</summary>
public sealed class SalaryPositionWorker(IServiceScopeFactory scopes, SearchListCache cache, ILogger<SalaryPositionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)", stoppingToken);
                    var employees = await db.NhanViens.Include(x => x.HopDongs).ThenInclude(x => x.PhuLucLuongs)
                        .Where(x => x.TrangThai != "DA_THOI_VIEC" && x.HopDongs.Any(h => h.PhuLucLuongs.Any(p => p.MaChucVuMoi != null && p.NgayHieuLuc <= today)))
                        .ToListAsync(stoppingToken);
                    foreach (var employee in employees)
                    {
                        var current = EffectiveSalaryResolver.Resolve(employee.HopDongs, today);
                        if (current == null) continue;
                        employee.MaChucVu = current.PositionId ?? employee.MaChucVu;
                        employee.MaPhongBan = current.DepartmentId ?? employee.MaPhongBan;
                    }
                    var changed = await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);
                    if (changed > 0) cache.Invalidate();
                });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Không thể đồng bộ chức vụ theo phụ lục; sẽ thử lại."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
