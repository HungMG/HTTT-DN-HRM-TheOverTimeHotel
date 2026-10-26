using System.Data.Common;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Services;

// No database is contacted: intercept the real Npgsql DELETE and its parameters.
var workerType = typeof(BaoCaoReportService).Assembly.GetType(
    "OvertimeHotel.HRM.WebAdmin.Services.AuditLogRetentionWorker");
Require(workerType != null, "Automatic audit log retention worker is missing.");

foreach (var failFirst in new[] { false, true })
{
    var clock = new ManualClock();
    var capture = new DeleteCapture { FailFirst = failFirst };
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<TimeProvider>(clock);
    services.AddDbContext<AppDbContext>(options => options
        .UseNpgsql("Host=localhost;Database=never_connected;Username=test;Password=test")
        .AddInterceptors(new OfflineConnection(), capture));
    await using var provider = services.BuildServiceProvider();
    using var worker = (BackgroundService)ActivatorUtilities.CreateInstance(provider, workerType!);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    await worker.StartAsync(timeout.Token);
    var first = await capture.Commands.Reader.ReadAsync(timeout.Token);
    CheckDelete(first, new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));

    // A failed cleanup must leave the worker alive, so the next day can retry.
    // Wait for the first tick waiter before advancing the deterministic clock.
    await Task.Delay(50, timeout.Token);
    Require(worker.ExecuteTask is { IsCompleted: false }, "Worker stopped after its first cleanup.");
    clock.AdvanceDay();
    var second = await capture.Commands.Reader.ReadAsync(timeout.Token);
    CheckDelete(second, new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));

    await worker.StopAsync(timeout.Token);
    Require(worker.ExecuteTask!.IsCompletedSuccessfully, "Shutdown should cancel cleanly.");
    Require(!capture.Commands.Reader.TryRead(out _), "Unexpected extra cleanup.");
    Console.WriteLine($"PASS: startup, daily UTC cutoff, strict older-than, audit-only DELETE, cancellation{(failFirst ? ", recovery after failure" : "")}");
}

static void CheckDelete(Deletion deletion, DateTimeOffset expectedCutoff)
{
    // A strict '<' preserves logs exactly 30 days old and all newer logs.
    var sql = string.Join(' ', deletion.Sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    Require(sql == "DELETE FROM nhat_ky_quan_tri AS n WHERE n.thoi_gian < @cutoff",
        $"Unexpected DELETE scope or boundary: {sql}");
    Require(deletion.Cutoff == expectedCutoff, $"Wrong cutoff: {deletion.Cutoff:O}");
    Require(deletion.Cutoff.Offset == TimeSpan.Zero, "Cutoff must be UTC.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

record Deletion(string Sql, DateTimeOffset Cutoff);

sealed class OfflineConnection : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
}

sealed class DeleteCapture : DbCommandInterceptor
{
    public bool FailFirst { get; init; }
    public Channel<Deletion> Commands { get; } = Channel.CreateUnbounded<Deletion>();
    private int _calls;

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var cutoff = (DateTimeOffset)command.Parameters[0].Value!;
        Commands.Writer.TryWrite(new Deletion(command.CommandText, cutoff));
        if (++_calls == 1 && FailFirst) throw new InvalidOperationException("Simulated database outage");
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(2));
    }
}

sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private ManualTimer? _timer;
    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != TimeSpan.FromDays(1) || period != TimeSpan.FromDays(1))
            throw new InvalidOperationException("Cleanup must run daily.");
        return _timer = new ManualTimer(callback, state);
    }

    public void AdvanceDay()
    {
        _now = _now.AddDays(1);
        _timer!.Tick();
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;
        public void Tick() { if (!_disposed) callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;
        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
