using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Services;

static class InvalidationChecks
{
    public static async Task Run(SearchListCache cache)
    {
        var loads = 0;
        Task<List<int>> Load() { loads++; return Task.FromResult(new List<int> { loads }); }
        var transactions = new SearchCacheTransactionInterceptor(cache);
        await cache.GetAsync("read-only-transaction", Load);
        using var transaction = new StubTransaction();
        transactions.TransactionCommitted(transaction, null!);
        await cache.GetAsync("read-only-transaction", Load);
        if (loads != 1) throw new Exception("Read-only payroll transactions must not flush cached search lists");
        Console.WriteLine("PASS: read-only transaction commits preserve cached lists");
        transactions.MarkDirty(transaction);
        transactions.TransactionCommitted(transaction, null!);
        await cache.GetAsync("read-only-transaction", Load);
        if (loads != 2) throw new Exception("A committed write transaction must invalidate");
        transactions.MarkDirty(transaction);
        await transactions.TransactionRolledBackAsync(transaction, null!);
        await cache.GetAsync("read-only-transaction", Load);
        if (loads != 3) throw new Exception("A rolled-back write transaction must invalidate");
        Console.WriteLine("PASS: write transaction commit and rollback invalidate lists");
        loads = 0;
        var fakeSave = new OfflineSave();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=never_connected;Username=test;Password=test")
            .AddInterceptors(new SearchCacheSaveInterceptor(cache, transactions), new SearchCacheCommandInterceptor(cache, transactions), fakeSave, new OfflineSql()).Options;
        await using var db = new AppDbContext(options);
        await cache.GetAsync("interceptor", Load);
        await db.SaveChangesAsync();
        await cache.GetAsync("interceptor", Load);
        if (loads != 1) throw new Exception("An empty SaveChanges must not invalidate");
        fakeSave.Result = 1;
        await db.SaveChangesAsync();
        await cache.GetAsync("interceptor", Load);
        if (loads != 2) throw new Exception("Successful SaveChangesAsync must invalidate");
        db.SaveChanges();
        await cache.GetAsync("interceptor", Load);
        if (loads != 3) throw new Exception("Successful synchronous SaveChanges must invalidate");
        await db.NhanViens.Where(n => n.MaNhanVien == -1).ExecuteUpdateAsync(s => s.SetProperty(n => n.Ten, "Test"));
        await cache.GetAsync("interceptor", Load);
        if (loads != 4) throw new Exception("ExecuteUpdateAsync must invalidate");
        await db.NhanViens.Where(n => n.MaNhanVien == -1).ExecuteDeleteAsync();
        await cache.GetAsync("interceptor", Load);
        if (loads != 5) throw new Exception("ExecuteDeleteAsync must invalidate");
        Console.WriteLine("PASS: real EF save/bulk-update/bulk-delete pipelines invalidate cached lists without contacting a database");
    }
    private sealed class StubTransaction : DbTransaction
    {
        public override System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
        protected override DbConnection? DbConnection => null;
        public override void Commit() { }
        public override void Rollback() { }
    }
    private sealed class OfflineSave : SaveChangesInterceptor
    {
        public int Result { get; set; }
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => InterceptionResult<int>.SuppressWithResult(Result);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(Result));
    }
    private sealed class OfflineSql : DbConnectionInterceptor, IDbCommandInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult.Suppress());
        public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
    }
}
