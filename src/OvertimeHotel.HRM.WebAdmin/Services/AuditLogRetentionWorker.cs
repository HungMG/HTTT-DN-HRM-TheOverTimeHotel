using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

/// <summary>Xóa toàn bộ nhật ký quá 30 ngày khi khởi động và mỗi 24 giờ khi web chạy.</summary>
public sealed class AuditLogRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuditLogRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), timeProvider);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cutoff = timeProvider.GetUtcNow().AddDays(-30);
                    var deleted = await context.NhatKyQuanTris
                        .Where(log => log.ThoiGian < cutoff)
                        .ExecuteDeleteAsync(stoppingToken);

                    logger.LogInformation(
                        "Đã xóa {Count} nhật ký quản trị quá 30 ngày (trước {Cutoff:O}).", deleted, cutoff);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Lỗi dọn nhật ký không được làm dừng WebAdmin; thử lại ở lượt kế tiếp.
                    logger.LogWarning(ex, "Không thể xóa nhật ký quá 30 ngày. Sẽ thử lại sau 24 giờ hoặc khi khởi động lại web.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Tắt web bình thường, không ghi cảnh báo lỗi database.
        }
    }
}
