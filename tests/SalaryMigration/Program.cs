using System.Text.Json;
using Npgsql;

// Explicit operator modes only. Connection values and personnel records are never printed.
var mode = args.SingleOrDefault() ?? "--local";
var root = Directory.GetCurrentDirectory();
var migration = Path.Combine(root, "src/OvertimeHotel.HRM.Data/migrations/20261111_salary_bands.sql");
var rollback = migration.Replace(".sql", ".rollback.sql");
var connectionString = mode == "--local"
    ? "Host=127.0.0.1;Port=55439;Database=postgres;Username=bryan"
    : Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (mode != "--local" && string.IsNullOrWhiteSpace(connectionString))
{
    foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
    {
        var path = Path.Combine(root, "src/OvertimeHotel.HRM.WebAdmin", name);
        if (!File.Exists(path)) continue;
        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (json.RootElement.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("DefaultConnection", out var c))
            connectionString = c.GetString();
    }
}
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("No database connection configured.");
try
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    async Task Execute(string sql) { await using var c = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 }; await c.ExecuteNonQueryAsync(); }
    async Task<object?> Scalar(string sql) { await using var c = new NpgsqlCommand(sql, connection); return await c.ExecuteScalarAsync(); }
    string Body(string path) => string.Join('\n', File.ReadAllLines(path).Where(l => l.Trim() is not "BEGIN;" and not "COMMIT;"));
    switch (mode)
    {
        case "--inspect":
            Console.WriteLine($"PostgreSQL version: {await Scalar("SHOW server_version")}");
            Console.WriteLine($"Known position count: {await Scalar("SELECT count(*) FROM public.chuc_vu WHERE ma_chuc_vu IN (1,2,4,5,6,7,8,9,10,11,12,13)")}");
            Console.WriteLine($"Salary bands table exists: {await Scalar("SELECT to_regclass('public.khung_luong_cap_bac') IS NOT NULL")}");
            Console.WriteLine($"Protected payroll periods: {await Scalar("SELECT count(*) FROM public.ky_luong WHERE trang_thai IN ('CHO_DUYET','DA_DUYET','KHOA_SO')")}");
            break;
        case "--dry-run":
            await using (var tx = await connection.BeginTransactionAsync())
            {
                await Execute("SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='60s';");
                await Execute(Body(migration));
                Console.WriteLine($"Transactional migration bands: {await Scalar("SELECT count(*) FROM public.khung_luong_cap_bac")}");
                await Execute(Body(rollback));
                await tx.RollbackAsync();
            }
            Console.WriteLine("Migration and guarded rollback dry-run rolled back; no persistent schema/data changes.");
            break;
        case "--apply":
            await using (var tx = await connection.BeginTransactionAsync())
            {
                await Execute("SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='60s';");
                await Execute("LOCK TABLE public.hop_dong, public.nhan_vien IN SHARE MODE;");
                var previousContracts = await Scalar("SELECT count(*)::text || ':' || coalesce(sum(luong_co_ban),0)::text FROM public.hop_dong");
                var previousEmployees = await Scalar("SELECT count(*) FROM public.nhan_vien");
                await Execute(Body(migration));
                if (!Equals(previousContracts, await Scalar("SELECT count(*)::text || ':' || coalesce(sum(luong_co_ban),0)::text FROM public.hop_dong")) || !Equals(previousEmployees, await Scalar("SELECT count(*) FROM public.nhan_vien"))) throw new InvalidOperationException("Business record verification mismatch.");
                if (Convert.ToInt32(await Scalar("SELECT count(*) FROM public.khung_luong_cap_bac")) != 13) throw new InvalidOperationException("Seed count mismatch.");
                await tx.CommitAsync();
            }
            Console.WriteLine("Salary schema applied atomically; 13 bands. No historical salaries modified.");
            break;
        case "--verify":
            Console.WriteLine($"Salary bands: {await Scalar("SELECT count(*) FROM public.khung_luong_cap_bac")}");
            Console.WriteLine($"Appendices: {await Scalar("SELECT count(*) FROM public.phu_luc_luong")}");
            Console.WriteLine($"Classified positions: {await Scalar("SELECT count(*) FROM public.chuc_vu WHERE nhom_luong IS NOT NULL")}");
            Console.WriteLine($"RLS tables: {await Scalar("SELECT count(*) FROM pg_class WHERE relnamespace='public'::regnamespace AND relname IN ('khung_luong_cap_bac','phu_luc_luong','salary_write_guard','salary_position_baseline') AND relrowsecurity")}");
            break;
        case "--local":
            await Execute(File.ReadAllText(Path.Combine(root, "src/OvertimeHotel.HRM.Data/verification/SalaryBands/fixture.sql")));
            await Execute(File.ReadAllText(migration));
            await Execute(File.ReadAllText(Path.Combine(root, "src/OvertimeHotel.HRM.Data/verification/SalaryBands/assertions.sql")));
            // Concurrent appointments: ReadCommitted must reject a second seat; RepeatableRead
            // must serialize-fail rather than commit a stale snapshot.
            foreach (var isolation in new[] { System.Data.IsolationLevel.ReadCommitted, System.Data.IsolationLevel.RepeatableRead })
            {
                await using var a = new NpgsqlConnection(connectionString);
                await using var b = new NpgsqlConnection(connectionString);
                await a.OpenAsync(); await b.OpenAsync();
                await using var ta = await a.BeginTransactionAsync(isolation);
                await using var tb = await b.BeginTransactionAsync(isolation);
                await using (var establish = new NpgsqlCommand("SELECT count(*) FROM public.hop_dong", b)) await establish.ExecuteScalarAsync();
                await using (var first = new NpgsqlCommand("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=1,ma_phong_ban_ky_ket=1 WHERE ma_hop_dong=1", a)) await first.ExecuteNonQueryAsync();
                async Task<bool> Compete()
                {
                    try
                    {
                        await using var second = new NpgsqlCommand("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=1,ma_phong_ban_ky_ket=1 WHERE ma_hop_dong=2", b);
                        await second.ExecuteNonQueryAsync();
                        await tb.CommitAsync();
                        return false;
                    }
                    catch (PostgresException e) when (e.SqlState is "P0001" or "40001") { try { await tb.RollbackAsync(); } catch (InvalidOperationException) { /* COMMIT already rolled back. */ } return true; }
                }
                var competing = Compete();
                await Task.Delay(100);
                await ta.CommitAsync();
                if (!await competing) throw new InvalidOperationException($"Concurrent GM check failed at {isolation}");
                await Execute("UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=NULL,ma_phong_ban_ky_ket=NULL");
                Console.WriteLine($"Concurrent GM check passed: {isolation}");
            }
            // Rollback must refuse schema drift and business use without partial deletion.
            foreach (var mutation in new[] {
                "COMMENT ON TABLE public.phu_luc_luong IS 'foreign-object'",
                "UPDATE public.khung_luong_cap_bac SET luong_toi_da=luong_toi_da+1 WHERE ma_khung_luong=1",
                "UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=4,ma_phong_ban_ky_ket=1 WHERE ma_hop_dong=1"
            })
            {
                await using var checkTx = await connection.BeginTransactionAsync();
                await Execute(mutation);
                var refused = false;
                try { await Execute(Body(rollback)); }
                catch (PostgresException e) when (e.SqlState == "P0001") { refused = true; }
                await checkTx.RollbackAsync();
                if (!refused) throw new InvalidOperationException("Guarded rollback did not refuse feature use/drift");
            }
            await Execute(File.ReadAllText(rollback));
            Console.WriteLine("Local synthetic migration, assertions, concurrency, rollback guards, rollback passed.");
            break;
        default: throw new ArgumentException("Allowed modes: --local, --inspect, --dry-run, --apply, --verify. Apply requires explicit operator authorization.");
    }
}
catch (PostgresException ex)
{
    Console.Error.WriteLine($"Database check failed [{ex.SqlState}]: {ex.MessageText}");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    // Avoid exception ToString/connection configuration, which may contain secrets.
    Console.Error.WriteLine(mode == "--local" ? ex.ToString() : $"Database check failed: {ex.GetType().Name}");
    Environment.ExitCode = 1;
}
