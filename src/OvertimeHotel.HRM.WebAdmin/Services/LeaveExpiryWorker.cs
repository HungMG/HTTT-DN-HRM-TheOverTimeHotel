using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed class LeaveExpiryWorker(IServiceScopeFactory scopes, ILogger<LeaveExpiryWorker> logger, SearchListCache cache) : BackgroundService
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
                var changed = await db.Database.ExecuteSqlRawAsync("""
                    UPDATE public.don_tu SET trang_thai = 'HET_HAN'
                    WHERE trang_thai = 'CHO_DUYET' AND ma_loai_don BETWEEN 1 AND 4
                      AND tu_ngay <= (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                    """, stoppingToken);
                if (changed > 0) cache.Invalidate();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không thể cập nhật đơn nghỉ hết hạn; sẽ thử lại."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
