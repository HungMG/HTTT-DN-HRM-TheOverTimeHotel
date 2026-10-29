using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OvertimeHotel.HRM.Data.Context;
using Npgsql;

// Exercises EF-generated SQL/materialization without opening any socket or executing SQL.
sealed class OfflineConnection : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData data, InterceptionResult result, CancellationToken token = default) =>
        ValueTask.FromResult(InterceptionResult.Suppress());
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data,
        InterceptionResult result) => InterceptionResult.Suppress();
}

sealed class OfflineReader : DbCommandInterceptor
{
    public long? BudgetVersion { get; set; }
    public List<string> Commands { get; } = [];
    public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=offline;Username=offline;Password=offline;Timeout=1;Pooling=false")
        .AddInterceptors(new OfflineConnection(), this).Options);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken token = default)
    {
        Commands.Add(command.CommandText);
        var sql=command.CommandText.Replace("\"","").ToLowerInvariant();
        var start=new DateTimeOffset(2026,10,10,1,0,0,TimeSpan.Zero);
        DataTable Table(Type[] types, params object[][] rows)
        {
            var table=new DataTable();
            for (var i=0;i<types.Length;i++) table.Columns.Add("c"+i,types[i]);
            foreach(var row in rows) table.Rows.Add(row);
            return table;
        }
        DataTable table;
        if (sql.Contains("from ngan_sach_luong") && BudgetVersion.HasValue)
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(
                Table([typeof(int),typeof(string),typeof(long)],[7,"CHO_DUYET",BudgetVersion.Value]).CreateDataReader()));
        if (sql.Contains("from nhan_vien") || sql.Contains("from phong_ban"))
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Table([typeof(bool)],[true]).CreateDataReader()));
        if (sql.Contains("from ngan_sach_luong"))
            throw new PostgresException("relation ngan_sach_luong does not exist", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable);
        if (sql.Contains("from phan_ca")) throw new NotSupportedException("captured attendance query");
        if (sql.Contains("from quyet_toan_su_kien"))
            table=Table([typeof(int),typeof(int),typeof(DateOnly),typeof(int),typeof(int),typeof(decimal),typeof(string),typeof(string),typeof(string)],
                [17,1,new DateOnly(2026,10,1),5,DBNull.Value,76923m,"Worker","Department","Hourly event"]);
        else if (sql.Contains("from phan_cong_du_an") && sql.Contains("so_tien_phu_cap <>"))
            table=Table([typeof(int),typeof(int),typeof(DateTimeOffset),typeof(decimal),typeof(string),typeof(string),typeof(string),typeof(string)]);
        else if (sql.Contains("from phan_cong_du_an"))
            table=Table([typeof(int),typeof(int),typeof(int),typeof(string),typeof(decimal),typeof(string)],
                [17,7,1,"HOAN_THANH",999999m,"DA_PHAN"],[17,7,1,"HOAN_THANH",999999m,"DA_PHAN"]);
        else if (sql.Contains("from du_an"))
            table=Table([typeof(int),typeof(string),typeof(string),typeof(string),typeof(DateTimeOffset),typeof(DateTimeOffset),typeof(bool),typeof(decimal)],
                [17,"Hourly event","HOI_NGHI_MICE","HOAN_THANH",start,start.AddHours(2),true,100000m]);
        else throw new InvalidOperationException("Unexpected offline SQL: "+command.CommandText);
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken token = default)
    {
        Commands.Add(command.CommandText);
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
}
