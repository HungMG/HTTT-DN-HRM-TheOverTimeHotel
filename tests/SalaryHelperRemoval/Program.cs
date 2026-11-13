using Npgsql;
using System.Data;

const string adminConnection = "Host=127.0.0.1;Port=55439;Database=postgres;Username=bryan";
var dbName = "salary_remove_" + Environment.ProcessId;
await using var admin = new NpgsqlConnection(adminConnection);
await admin.OpenAsync();
var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(dbName);
await using (var create = new NpgsqlCommand("CREATE DATABASE " + quoted, admin)) await create.ExecuteNonQueryAsync();
try
{
    var cs = new NpgsqlConnectionStringBuilder(adminConnection) { Database = dbName }.ConnectionString;
    await using var conn = new NpgsqlConnection(cs); await conn.OpenAsync();
    async Task Exec(string sql) { await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 }; await cmd.ExecuteNonQueryAsync(); }
    async Task<object?> Scalar(string sql) { await using var cmd = new NpgsqlCommand(sql, conn); return await cmd.ExecuteScalarAsync(); }
    var path = "src/OvertimeHotel.HRM.Data/migrations/";
    var assertions = File.ReadAllText("src/OvertimeHotel.HRM.Data/verification/SalaryBands/assertions.sql");
    await Exec(File.ReadAllText("src/OvertimeHotel.HRM.Data/verification/SalaryBands/fixture.sql"));
    await Exec(File.ReadAllText(path + "20261111_salary_bands.sql"));
    await Exec(assertions);
    await Exec("CREATE TEMP TABLE expected_baseline AS SELECT * FROM public.salary_position_baseline");
    await Exec(File.ReadAllText(path + "20261113_remove_salary_helper_tables.sql"));
    if (!(bool)(await Scalar("SELECT to_regclass('public.salary_write_guard') IS NULL AND to_regclass('public.salary_position_baseline') IS NULL"))!) throw new Exception("Both helper tables must be gone");
    if ((long)(await Scalar("SELECT count(*) FROM ((SELECT ma_nhan_vien,observed_date,ma_chuc_vu FROM expected_baseline EXCEPT SELECT ma_nhan_vien,ngay_ghi_nhan_chuc_vu,ma_chuc_vu_ghi_nhan FROM public.nhan_vien WHERE ngay_ghi_nhan_chuc_vu IS NOT NULL) UNION ALL (SELECT ma_nhan_vien,ngay_ghi_nhan_chuc_vu,ma_chuc_vu_ghi_nhan FROM public.nhan_vien WHERE ngay_ghi_nhan_chuc_vu IS NOT NULL EXCEPT SELECT ma_nhan_vien,observed_date,ma_chuc_vu FROM expected_baseline)) q"))! != 0) throw new Exception("Baseline data not preserved");
    try { await Exec("UPDATE public.nhan_vien SET ma_chuc_vu_ghi_nhan=2 WHERE ma_nhan_vien=2"); throw new Exception("Baseline mutation allowed"); }
    catch (PostgresException e) when (e.MessageText == "Observed salary position baseline is immutable") { }
    try { await Exec("INSERT INTO public.nhan_vien(ma_nhan_vien,ngay_vao_lam,trang_thai,ngay_ghi_nhan_chuc_vu) VALUES (3,'2026-01-01','DANG_LAM','2026-01-01')"); throw new Exception("Baseline injection allowed"); }
    catch (PostgresException e) when (e.MessageText == "Observed salary position baseline is immutable") { }
    var movedAssertions = assertions.Replace("SELECT ma_chuc_vu FROM public.salary_position_baseline WHERE ma_nhan_vien=2", "SELECT ma_chuc_vu_ghi_nhan FROM public.nhan_vien WHERE ma_nhan_vien=2");
    await Exec(movedAssertions);
    Console.WriteLine("PASS existing business assertions and immutable preserved baselines after removal");
    foreach(var isolation in new[]{IsolationLevel.ReadCommitted,IsolationLevel.RepeatableRead})
    {
        await using var a=new NpgsqlConnection(cs); await using var b=new NpgsqlConnection(cs); await a.OpenAsync(); await b.OpenAsync();
        await using var ta=await a.BeginTransactionAsync(isolation); await using var tb=await b.BeginTransactionAsync(isolation);
        await using(var establish=new NpgsqlCommand("SELECT count(*) FROM public.hop_dong",b,tb))await establish.ExecuteScalarAsync();
        await using(var first=new NpgsqlCommand("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=1,ma_phong_ban_ky_ket=1 WHERE ma_hop_dong=1",a,ta))await first.ExecuteNonQueryAsync();
        async Task<bool> Compete(){try{await using var second=new NpgsqlCommand("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=1,ma_phong_ban_ky_ket=1 WHERE ma_hop_dong=2",b,tb);await second.ExecuteNonQueryAsync();await tb.CommitAsync();return false;}catch(PostgresException e) when(e.SqlState is "P0001" or "40001"){try{await tb.RollbackAsync();}catch(InvalidOperationException){}return true;}}
        var competing=Compete();await Task.Delay(100);await ta.CommitAsync();
        if(!await competing)throw new Exception("Concurrent duplicate GM allowed: "+isolation);
        await Exec("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=NULL,ma_phong_ban_ky_ket=NULL");
        Console.WriteLine("PASS concurrent appointments: "+isolation);
    }
    await Exec(File.ReadAllText(path+"20261113_remove_salary_helper_tables.rollback.sql"));
    if ((long)(await Scalar("SELECT count(*) FROM ((SELECT * FROM expected_baseline EXCEPT SELECT * FROM public.salary_position_baseline) UNION ALL (SELECT * FROM public.salary_position_baseline EXCEPT SELECT * FROM expected_baseline)) q"))! != 0)throw new Exception("Rollback baseline mismatch");
    await Exec(assertions);
    Console.WriteLine("PASS rollback restores tables, data, permissions and original business assertions");
}
finally
{
    NpgsqlConnection.ClearAllPools();
    await using var drop=new NpgsqlCommand("DROP DATABASE " + quoted,admin);await drop.ExecuteNonQueryAsync();
}
