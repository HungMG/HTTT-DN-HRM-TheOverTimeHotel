using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed class SearchCacheSaveInterceptor(SearchListCache cache, SearchCacheTransactionInterceptor transactions) : SaveChangesInterceptor
{
    private void Invalidate(SaveChangesCompletedEventData eventData, int result)
    {
        if (result <= 0) return;
        transactions.MarkDirty(eventData.Context?.Database.CurrentTransaction?.GetDbTransaction());
        cache.Invalidate();
    }
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Invalidate(eventData, result);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Invalidate(eventData, result);
        return ValueTask.FromResult(result);
    }
}

// ExecuteUpdate/Delete and raw SQL bypass SaveChanges; invalidate their list snapshots too.
public sealed class SearchCacheCommandInterceptor(SearchListCache cache, SearchCacheTransactionInterceptor transactions) : DbCommandInterceptor
{
    private static readonly Regex WriteSql = new(@"\b(INSERT|UPDATE|DELETE|TRUNCATE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private void Invalidate(DbCommand command)
    {
        if (WriteSql.IsMatch(command.CommandText))
        {
            transactions.MarkDirty(command.Transaction);
            cache.Invalidate();
        }
    }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Invalidate(command); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Invalidate(command); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Invalidate(command); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
}

// A concurrent read may have seen the old state while an explicit transaction was open.
public sealed class SearchCacheTransactionInterceptor(SearchListCache cache) : DbTransactionInterceptor
{
    private readonly ConditionalWeakTable<DbTransaction, object> _dirty = new();
    public void MarkDirty(DbTransaction? transaction)
    {
        if (transaction != null) _dirty.GetValue(transaction, _ => new object());
    }
    private void Complete(DbTransaction transaction)
    {
        if (_dirty.Remove(transaction)) cache.Invalidate();
    }
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Complete(transaction); return Task.CompletedTask; }
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Complete(transaction); return Task.CompletedTask; }
}
