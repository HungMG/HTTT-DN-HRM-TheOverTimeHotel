using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public interface IAccountRequestLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(int requestId, CancellationToken cancellationToken);
}

public sealed class AccountRequestLock(AppDbContext context) : IAccountRequestLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(int requestId, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT pg_try_advisory_xact_lock(20261105, @request_id)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "request_id";
            parameter.Value = requestId;
            command.Parameters.Add(parameter);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
                return new Handle(connection, transaction);
            await transaction.DisposeAsync();
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
        await connection.DisposeAsync();
        return null;
    }

    private sealed class Handle(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
