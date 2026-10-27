using OvertimeHotel.HRM.Data.Services;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed class HotelEventAutoStartWorker(IServiceScopeFactory scopes, ILogger<HotelEventAutoStartWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IHotelEventService>().AutoStartReadyEventsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Không thể kiểm tra tự bắt đầu sự kiện; hệ thống sẽ thử lại."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
