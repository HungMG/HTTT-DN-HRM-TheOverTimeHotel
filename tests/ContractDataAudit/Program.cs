using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

// ============================================================================
// ContractDataAudit — Ràng lại dữ liệu seed Phân Ca theo HĐLĐ
//
// Pha 1 (mặc định)      : CHỈ ĐỌC — liệt kê phân ca vi phạm, phân loại nguyên nhân.
// Pha 2 (--renew)       : GHI — tạo hợp đồng gia hạn cho nhân sự HĐ đã hết hạn.
// Pha 3 (--apply)       : GHI — xóa phân ca vẫn còn vi phạm sau khi gia hạn.
//
// Ví dụ:
//   dotnet run --project tests/ContractDataAudit
//   dotnet run --project tests/ContractDataAudit -- --renew
//   dotnet run --project tests/ContractDataAudit -- --apply
// ============================================================================

var renew = args.Contains("--renew");
var apply = args.Contains("--apply");
var fixTypoDates = args.Contains("--fix-typo-dates");
var verifyMigrationIndex = Array.IndexOf(args, "--verify-migration");
var applyMigrationIndex = Array.IndexOf(args, "--apply-migration");
var probeLiveIndex = Array.IndexOf(args, "--probe-live");

var fromArgIndex = Array.IndexOf(args, "--from");
DateOnly fromDate = fromArgIndex >= 0 && fromArgIndex + 1 < args.Length && DateOnly.TryParse(args[fromArgIndex + 1], out var parsed)
    ? parsed
    : new DateOnly(2026, 10, 1);

string? connection = null;
foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
{
    var path = Path.Combine(AppContext.BaseDirectory, file);
    if (!File.Exists(path)) continue;
    var config = new ConfigurationBuilder().AddJsonFile(path, optional: true).Build();
    var value = config["ConnectionStrings:DefaultConnection"];
    if (!string.IsNullOrWhiteSpace(value)) connection = value;
}

if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("Không tìm thấy ConnectionStrings:DefaultConnection trong appsettings.");
    return 2;
}

var normalized = new NpgsqlConnectionStringBuilder(connection) { IncludeErrorDetail = false, Timeout = 15 }.ConnectionString;

// ==========================================================================
// CHẾ ĐỘ ĐẶC BIỆT — CHẠY THỬ MIGRATION TRIGGER RỒI ROLLBACK
// ==========================================================================
// Chạy file SQL trong MỘT transaction, thử chặn/cho qua, rồi ROLLBACK. Không để
// lại gì trên CSDL cloud: không trigger, không hàm, không dòng dữ liệu thử.
if (verifyMigrationIndex >= 0)
{
    var sqlPath = verifyMigrationIndex + 1 < args.Length
        ? args[verifyMigrationIndex + 1]
        : "supabase/migrations/20261016_rang_buoc_hop_dong_phan_ca_cham_cong.sql";

    if (!File.Exists(sqlPath))
    {
        Console.Error.WriteLine($"Không tìm thấy file migration: {sqlPath}");
        return 2;
    }

    await using var conn = new NpgsqlConnection(normalized);
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    var failures = 0;

    // 1. Chạy migration. Lỗi cú pháp / hàm thiếu sẽ vỡ ngay tại đây.
    await using (var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(sqlPath), conn, tx))
    {
        try
        {
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[OK] Migration chạy trọn vẹn trong transaction.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] Migration vỡ: {ex.Message}");
            await tx.RollbackAsync();
            return 1;
        }
    }

    // 2. Mọi dòng phan_ca hiện có phải qua được cửa mới.
    await using (var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM public.phan_ca pc " +
        "WHERE NOT public.nhan_vien_co_hop_dong_hieu_luc(pc.ma_nhan_vien, pc.ngay_lam_viec);", conn, tx))
    {
        var bad = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Console.WriteLine(bad == 0
            ? "[OK] 0 phân ca hiện có vi phạm luật mới."
            : $"[FAIL] {bad} phân ca hiện có vi phạm luật mới — trigger sẽ chặn UPDATE lên chúng.");
        if (bad != 0) failures++;
    }

    // Một câu lệnh RAISE làm hỏng cả transaction (SQLSTATE 25P02 cho mọi lệnh sau).
    // Bọc mỗi phép thử trong SAVEPOINT để cô lập: thử xong thì quay về mốc, dù đạt hay vỡ.
    async Task<(bool Blocked, string? Message)> ThuGhi(string sql, params (string Name, object Value)[] prms)
    {
        await using (var sp = new NpgsqlCommand("SAVEPOINT sp_probe;", conn, tx))
            await sp.ExecuteNonQueryAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            foreach (var (name, value) in prms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
            await using (var rel = new NpgsqlCommand("RELEASE SAVEPOINT sp_probe;", conn, tx))
                await rel.ExecuteNonQueryAsync();
            return (false, null);
        }
        catch (PostgresException ex)
        {
            await using (var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT sp_probe; RELEASE SAVEPOINT sp_probe;", conn, tx))
                await rb.ExecuteNonQueryAsync();
            return (ex.SqlState == "23000", ex.MessageText);
        }
    }

    // 3. Nhân viên KHÔNG có hợp đồng hiệu lực phải bị chặn. Thử ở 01/01/2035 — sau khi
    //    mọi hợp đồng đã hết hạn, đúng tình huống cần chặn.
    var probeDate = new DateOnly(2035, 1, 1);
    int? noContractEmp = null;
    await using (var cmd = new NpgsqlCommand(
        "SELECT nv.ma_nhan_vien FROM public.nhan_vien nv " +
        "WHERE nv.trang_thai = 'DANG_LAM' AND NOT public.nhan_vien_co_hop_dong_hieu_luc(nv.ma_nhan_vien, @d) " +
        "ORDER BY nv.ma_nhan_vien LIMIT 1;", conn, tx))
    {
        cmd.Parameters.AddWithValue("d", probeDate);
        var v = await cmd.ExecuteScalarAsync();
        if (v is not null and not DBNull) noContractEmp = Convert.ToInt32(v);
    }

    if (noContractEmp is int badEmp)
    {
        var (blocked, message) = await ThuGhi(
            "INSERT INTO public.phan_ca (ma_nhan_vien, ma_ca, ngay_lam_viec, bat_dau_du_kien, ket_thuc_du_kien) " +
            "VALUES (@nv, 1, @d, now(), now() + interval '8 hours');",
            ("nv", badEmp), ("d", probeDate));
        if (blocked)
            Console.WriteLine($"[OK] Trigger chặn đúng nhân viên #{badEmp} (ngày {probeDate:dd/MM/yyyy}): {message}");
        else
        {
            Console.WriteLine($"[FAIL] Đã lọt phân ca ngày {probeDate:dd/MM/yyyy} cho nhân viên #{badEmp} — trigger không chặn.");
            failures++;
        }
    }
    else
    {
        Console.WriteLine("[SKIP] Không tìm được nhân viên nào hết HĐ để thử chặn.");
    }

    // 4. Một ca HỢP LỆ thật phải đi qua được (không chặn oan).
    {
        var (blocked, message) = await ThuGhi(
            "INSERT INTO public.phan_ca (ma_nhan_vien, ma_ca, ngay_lam_viec, bat_dau_du_kien, ket_thuc_du_kien) " +
            "SELECT pc.ma_nhan_vien, pc.ma_ca, pc.ngay_lam_viec, pc.bat_dau_du_kien, pc.ket_thuc_du_kien " +
            "FROM public.phan_ca pc " +
            "WHERE public.nhan_vien_co_hop_dong_hieu_luc(pc.ma_nhan_vien, pc.ngay_lam_viec) LIMIT 1;");
        Console.WriteLine(!blocked
            ? "[OK] Ca hợp lệ đi qua bình thường — không chặn oan."
            : $"[FAIL] Ca hợp lệ bị chặn oan: {message}");
        if (blocked) failures++;
    }

    // 5. Trigger cham_cong cũng phải bắt. Dựng ca mồi ở 2035 bằng cách tạm tắt trigger
    //    phan_ca TRONG savepoint này, ghi chấm công vào ca mồi — phải bị chặn. Tắt
    //    trigger trong savepoint rồi ROLLBACK TO sẽ trả nguyên trạng, không rò ra ngoài.
    if (noContractEmp is int badEmp2)
    {
        await using (var sp = new NpgsqlCommand("SAVEPOINT sp_probe2;", conn, tx))
            await sp.ExecuteNonQueryAsync();

        await using (var off = new NpgsqlCommand(
            "ALTER TABLE public.phan_ca DISABLE TRIGGER trg_chan_phan_ca_khong_hop_dong;", conn, tx))
            await off.ExecuteNonQueryAsync();

        int seedShiftId = 0;
        await using (var mk = new NpgsqlCommand(
            "INSERT INTO public.phan_ca (ma_nhan_vien, ma_ca, ngay_lam_viec, bat_dau_du_kien, ket_thuc_du_kien) " +
            "VALUES (@nv, 1, @d, now(), now() + interval '8 hours') RETURNING ma_phan_ca;", conn, tx))
        {
            mk.Parameters.AddWithValue("nv", badEmp2);
            mk.Parameters.AddWithValue("d", probeDate);
            seedShiftId = Convert.ToInt32(await mk.ExecuteScalarAsync());
        }

        await using (var on = new NpgsqlCommand(
            "ALTER TABLE public.phan_ca ENABLE TRIGGER trg_chan_phan_ca_khong_hop_dong;", conn, tx))
            await on.ExecuteNonQueryAsync();

        var (blocked, message) = await ThuGhi(
            "INSERT INTO public.cham_cong (ma_phan_ca, gio_check_in, phut_di_tre, phut_ve_som, gio_tang_ca_ot, trang_thai) " +
            "VALUES (@pc, now(), 0, 0, 0, 'DUNG_GIO');", ("pc", seedShiftId));
        if (blocked)
            Console.WriteLine($"[OK] Trigger cham_cong chặn đúng: {message}");
        else
        {
            Console.WriteLine("[FAIL] Chấm công lọt vào ca không hợp lệ — trigger cham_cong không chặn.");
            failures++;
        }

        await using (var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT sp_probe2; RELEASE SAVEPOINT sp_probe2;", conn, tx))
            await rb.ExecuteNonQueryAsync();
    }

    await tx.RollbackAsync();
    Console.WriteLine();
    Console.WriteLine(failures == 0
        ? "[KẾT LUẬN] Migration đạt — đã ROLLBACK, CSDL không đổi."
        : $"[KẾT LUẬN] {failures} phép thử hỏng.");
    return failures == 0 ? 0 : 1;
}

// ==========================================================================
// ÁP MIGRATION LÊN CSDL THẬT — chạy trong MỘT transaction
// ==========================================================================
// Chỉ dùng khi người dùng đã yêu cầu rõ. Vì cả file SQL nằm trong một transaction,
// Postgres sẽ tự hủy toàn bộ nếu bất kỳ câu RAISE/CREATE nào lỗi — không để lại
// trạng thái nửa vời. Đối soát sau khi áp: 0 phân ca vi phạm.
if (applyMigrationIndex >= 0)
{
    var sqlFile = applyMigrationIndex + 1 < args.Length
        ? args[applyMigrationIndex + 1]
        : "supabase/migrations/20261016_rang_buoc_hop_dong_phan_ca_cham_cong.sql";

    if (!File.Exists(sqlFile))
    {
        Console.Error.WriteLine($"Không tìm thấy file migration: {sqlFile}");
        return 2;
    }

    Console.WriteLine("======================================================================");
    Console.WriteLine("*** GHI: ÁP TRIGGER RÀNG BUỘC HĐLĐ LÊN CSDL SUPABASE ***");
    Console.WriteLine($"File: {sqlFile}");
    Console.WriteLine("======================================================================");

    await using var conn = new NpgsqlConnection(normalized);
    await conn.OpenAsync();

    // Một transaction bao trọn: DDL + đối soát. Lỗi ở đâu cũng hủy hết.
    await using (var tx = await conn.BeginTransactionAsync())
    {
        Console.WriteLine("[1/4] Đang chạy migration…");
        try
        {
            await using var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(sqlFile), conn, tx);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FAIL] Migration vỡ, đã hủy toàn bộ: {ex.Message}");
            await tx.RollbackAsync();
            return 1;
        }

        Console.WriteLine("[2/4] Đối soát dữ liệu hiện có…");
        long soViPham;
        await using (var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM public.phan_ca pc " +
            "WHERE NOT public.nhan_vien_co_hop_dong_hieu_luc(pc.ma_nhan_vien, pc.ngay_lam_viec);", conn, tx))
        {
            soViPham = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        if (soViPham != 0)
        {
            Console.Error.WriteLine($"[FAIL] {soViPham} phân ca hiện có vi phạm luật mới —");
            Console.Error.WriteLine("       trigger sẽ chặn UPDATE lên chúng. Đã hủy, KHÔNG áp gì.");
            await tx.RollbackAsync();
            return 1;
        }
        Console.WriteLine("[OK]  0 phân ca hiện có vi phạm.");

        Console.WriteLine("[3/4] Kiểm tra trigger đã tạo…");
        await using (var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM pg_trigger WHERE tgname IN " +
            "('trg_chan_phan_ca_khong_hop_dong','trg_chan_cham_cong_khong_hop_dong') AND NOT tgisinternal;", conn, tx))
        {
            var n = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            if (n != 2)
            {
                Console.Error.WriteLine($"[FAIL] Chỉ thấy {n}/2 trigger. Đã hủy.");
                await tx.RollbackAsync();
                return 1;
            }
        }
        Console.WriteLine("[OK]  2/2 trigger hiện diện.");

        Console.WriteLine("[4/4] Chốt (COMMIT)…");
        await tx.CommitAsync();
    }

    // Xác nhận độc lập sau commit bằng kết nối sạch.
    await using (var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM pg_trigger WHERE tgname IN " +
        "('trg_chan_phan_ca_khong_hop_dong','trg_chan_cham_cong_khong_hop_dong') AND NOT tgisinternal;", conn))
    {
        var n = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Console.WriteLine();
        Console.WriteLine(n == 2
            ? "[XONG] Trigger đã áp vĩnh viễn lên Supabase. Luật HĐLĐ nay được chốt ở tầng CSDL."
            : $"[CẢNH BÁO] Sau commit chỉ thấy {n}/2 trigger — kiểm tra lại thủ công.");
        return n == 2 ? 0 : 1;
    }
}

// ==========================================================================
// KIỂM CHỨNG TRỰC TIẾP TRÊN CSDL ĐÃ ÁP — rollback nên vô hại
// ==========================================================================
// Khác --verify-migration (chạy migration RỒI mới thử): chế độ này KHÔNG chạy
// migration, chỉ thử đúng những trigger đang thật sự nằm trên CSDL. Dùng để xác
// nhận sau khi đã áp.
if (probeLiveIndex >= 0)
{
    await using var conn = new NpgsqlConnection(normalized);
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    Console.WriteLine("======================================================================");
    Console.WriteLine("KIỂM CHỨNG TRỰC TIẾP TRÊN CSDL SUPABASE (rollback — không để lại gì)");
    Console.WriteLine("======================================================================");

    long triggerCount;
    await using (var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM pg_trigger WHERE tgname IN " +
        "('trg_chan_phan_ca_khong_hop_dong','trg_chan_cham_cong_khong_hop_dong') AND NOT tgisinternal;", conn, tx))
        triggerCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
    Console.WriteLine($"Trigger hiện có: {triggerCount}/2");

    long before;
    await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM public.phan_ca;", conn, tx))
        before = Convert.ToInt64(await cmd.ExecuteScalarAsync());

    var probeDate = new DateOnly(2035, 1, 1);
    int? badEmp = null;
    await using (var cmd = new NpgsqlCommand(
        "SELECT nv.ma_nhan_vien FROM public.nhan_vien nv " +
        "WHERE nv.trang_thai = 'DANG_LAM' AND NOT public.nhan_vien_co_hop_dong_hieu_luc(nv.ma_nhan_vien, @d) " +
        "ORDER BY nv.ma_nhan_vien LIMIT 1;", conn, tx))
    {
        cmd.Parameters.AddWithValue("d", probeDate);
        var v = await cmd.ExecuteScalarAsync();
        if (v is not null and not DBNull) badEmp = Convert.ToInt32(v);
    }

    var pass = triggerCount == 2;
    if (badEmp is int emp)
    {
        await using (var sp = new NpgsqlCommand("SAVEPOINT s;", conn, tx)) await sp.ExecuteNonQueryAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO public.phan_ca (ma_nhan_vien, ma_ca, ngay_lam_viec, bat_dau_du_kien, ket_thuc_du_kien) " +
                "VALUES (@nv, 1, @d, now(), now() + interval '8 hours');", conn, tx);
            cmd.Parameters.AddWithValue("nv", emp);
            cmd.Parameters.AddWithValue("d", probeDate);
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[FAIL] Nhân viên #{emp} (hết HĐ) VẪN phân ca được — trigger không hoạt động!");
            pass = false;
        }
        catch (PostgresException ex) when (ex.SqlState == "23000")
        {
            Console.WriteLine($"[OK] Chặn đúng nhân viên #{emp}: {ex.MessageText}");
        }
        await using (var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT s; RELEASE SAVEPOINT s;", conn, tx))
            await rb.ExecuteNonQueryAsync();
    }

    await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM public.phan_ca;", conn, tx))
    {
        var after = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Console.WriteLine(after == before ? $"[OK] Số phân ca không đổi ({before})." : $"[FAIL] Số phân ca đổi {before} → {after}!");
        if (after != before) pass = false;
    }

    await tx.RollbackAsync();
    Console.WriteLine();
    Console.WriteLine(pass ? "[KẾT LUẬN] Trigger đang hoạt động trên Supabase." : "[KẾT LUẬN] Có vấn đề — cần xem lại.");
    return pass ? 0 : 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(normalized).Options;
await using var context = new AppDbContext(options);

var mode = fixTypoDates ? "*** GHI: SỬA NGÀY HĐ GÕ NHẦM ***"
    : renew ? "*** GHI: GIA HẠN HỢP ĐỒNG ***"
    : apply ? "*** GHI: XÓA PHÂN CA VI PHẠM ***"
    : "CHỈ ĐỌC (dry-run)";
Console.WriteLine("======================================================================");
Console.WriteLine($"RÀNG BUỘC HĐLĐ ↔ PHÂN CA  (từ {fromDate:dd/MM/yyyy})   {mode}");
Console.WriteLine("======================================================================");

// ==========================================================================
// PHA 0 — SỬA NGÀY HĐ GÕ NHẦM (ví dụ 2027 → 2026)
// ==========================================================================
if (fixTypoDates)
{
    var oneYearAhead = fromDate.AddYears(1);
    var typoContracts = await context.HopDongs
        .Where(h => h.NgayBatDau > oneYearAhead)
        .OrderBy(h => h.MaHopDong)
        .ToListAsync();

    if (typoContracts.Count == 0)
    {
        Console.WriteLine("Không tìm thấy hợp đồng nào có ngày bắt đầu sai xa (> 1 năm kể từ mốc đối soát).");
        return 0;
    }

    Console.WriteLine($"---- HỢP ĐỒNG CÓ NGÀY BẮT ĐẦU BẤT THƯỜNG (> {oneYearAhead:dd/MM/yyyy}) ----");
    foreach (var h in typoContracts)
    {
        var newStart = h.NgayBatDau.AddYears(-1);
        var newEnd = h.NgayKetThuc?.AddYears(-1);
        Console.WriteLine($"  HĐ#{h.MaHopDong,-4} NV#{h.MaNhanVien,-4} {h.SoHopDong,-20}");
        Console.WriteLine($"        ngay_bat_dau: {h.NgayBatDau:dd/MM/yyyy}  →  {newStart:dd/MM/yyyy}");
        if (newEnd is DateOnly ne) Console.WriteLine($"        ngay_ket_thuc: {h.NgayKetThuc:dd/MM/yyyy}  →  {ne:dd/MM/yyyy}");
    }

    Console.WriteLine();
    Console.Write($"Xác nhận sửa {typoContracts.Count} hợp đồng (lùi 1 năm)? Gõ 'YES' để tiếp tục: ");
    if (Console.ReadLine()?.Trim() != "YES")
    {
        Console.WriteLine("Đã hủy. Không sửa gì.");
        return 0;
    }

    foreach (var h in typoContracts)
    {
        h.NgayBatDau = h.NgayBatDau.AddYears(-1);
        h.NgayKetThuc = h.NgayKetThuc?.AddYears(-1);
        if (h.NgayKetThuc.HasValue && h.NgayKetThuc.Value < fromDate && h.TrangThai == "HIEU_LUC")
        {
            h.TrangThai = "HET_HAN";
        }
    }

    Console.WriteLine($"[OK] Đã sửa {await context.SaveChangesAsync()} hợp đồng.");
    return 0;
}

// ---- Nạp dữ liệu ----------------------------------------------------------
var employees = await context.NhanViens.Include(n => n.HopDongs).AsNoTracking().ToListAsync();
var employeeMap = employees.ToDictionary(e => e.MaNhanVien);

var shifts = await context.PhanCas
    .Include(p => p.CaLamViec)
    .Where(p => p.NgayLamViec >= fromDate)
    .AsNoTracking()
    .ToListAsync();

Console.WriteLine($"Nhân viên: {employees.Count}   Phân ca (từ {fromDate:dd/MM/yyyy}): {shifts.Count}");
Console.WriteLine();

// ---- Phân loại vi phạm ----------------------------------------------------
var violations = new List<(PhanCa Shift, NhanVien? Emp, string LyDo)>();

foreach (var s in shifts)
{
    if (!employeeMap.TryGetValue(s.MaNhanVien, out var emp))
    {
        violations.Add((s, null, "Không tìm thấy nhân viên"));
        continue;
    }
    if (ContractAttendancePolicy.HasValidContractOn(emp, s.NgayLamViec)) continue;

    var latest = emp.HopDongs.OrderByDescending(h => h.NgayBatDau).FirstOrDefault();
    var reason = emp.TrangThai != "DANG_LAM"
        ? $"Nhân viên ở trạng thái {emp.TrangThai}"
        : latest is null
            ? "Chưa ký hợp đồng lao động"
            : latest.NgayBatDau > s.NgayLamViec
                ? $"HĐ chỉ bắt đầu từ {latest.NgayBatDau:dd/MM/yyyy} (chưa tới hiệu lực)"
                : $"HĐ đã hết hạn ngày {latest.NgayKetThuc:dd/MM/yyyy}";

    violations.Add((s, emp, reason));
}

Console.WriteLine($"PHÂN CA KHÔNG HỢP LỆ: {violations.Count}");
Console.WriteLine();

if (violations.Count == 0)
{
    Console.WriteLine("[OK] Không có phân ca nào vi phạm. Dữ liệu đã sạch.");
    return 0;
}

// ---- Hồ sơ hợp đồng của từng nhân sự vi phạm ------------------------------
Console.WriteLine("---- HỒ SƠ HỢP ĐỒNG CỦA NHÂN SỰ VI PHẠM ----");
foreach (var g in violations.Where(v => v.Emp is not null).GroupBy(v => v.Emp!.MaNhanVien).OrderBy(g => g.Key))
{
    var emp = g.First().Emp!;
    var latest = emp.HopDongs.OrderByDescending(h => h.NgayBatDau).FirstOrDefault();
    var loai = latest is null
        ? "KHÔNG CÓ HĐ"
        : latest.NgayBatDau > fromDate ? "HĐ CHƯA TỚI HIỆU LỰC" : "HĐ ĐÃ HẾT HẠN";

    Console.WriteLine($"  NV#{emp.MaNhanVien,-4} {emp.HoTen,-24} [{loai}]  {g.Count(),3} ca vi phạm");
    foreach (var h in emp.HopDongs.OrderBy(h => h.NgayBatDau))
    {
        var ketThuc = h.NgayKetThuc is null ? "không xác định" : h.NgayKetThuc.Value.ToString("dd/MM/yyyy");
        Console.WriteLine($"        HĐ#{h.MaHopDong,-4} {h.SoHopDong,-22} {h.NgayBatDau:dd/MM/yyyy} → {ketThuc,-14} {h.TrangThai}");
    }
}
Console.WriteLine();

// Nhóm để gia hạn: chỉ những người HĐ đã hết hạn hoặc chưa có HĐ
var renewalCandidates = violations
    .Where(v => v.Emp is not null)
    .Select(v => v.Emp!)
    .DistinctBy(e => e.MaNhanVien)
    .Where(e =>
    {
        var latest = e.HopDongs.OrderByDescending(h => h.NgayBatDau).FirstOrDefault();
        return latest is null || (latest.NgayKetThuc.HasValue && latest.NgayKetThuc.Value < fromDate);
    })
    .OrderBy(e => e.MaNhanVien)
    .ToList();

var futureContractHolders = violations
    .Where(v => v.Emp is not null)
    .Select(v => v.Emp!)
    .DistinctBy(e => e.MaNhanVien)
    .Except(renewalCandidates)
    .OrderBy(e => e.MaNhanVien)
    .ToList();

Console.WriteLine($"Cần GIA HẠN HĐ:            {renewalCandidates.Count} nhân sự");
Console.WriteLine($"HĐ chưa tới hiệu lực:      {futureContractHolders.Count} nhân sự (phải xóa ca trước ngày ký)");
Console.WriteLine();

// ==========================================================================
// PHA 2 — GIA HẠN HỢP ĐỒNG
// ==========================================================================
if (renew)
{
    if (renewalCandidates.Count == 0)
    {
        Console.WriteLine("Không có nhân sự nào cần gia hạn.");
        return 0;
    }

    var renewalEnd = new DateOnly(2027, 12, 31);
    var year = DateTime.Now.Year;
    var existingNumbers = (await context.HopDongs.AsNoTracking().Select(h => h.SoHopDong).ToListAsync())
        .Select(n => n.ToLowerInvariant()).ToHashSet();

    Console.WriteLine("---- TẠO HỢP ĐỒNG GIA HẠN ----");

    var created = 0;
    foreach (var emp in renewalCandidates)
    {
        var latest = emp.HopDongs.OrderByDescending(h => h.NgayBatDau).FirstOrDefault();

        // Bắt đầu ngay sau ngày kết thúc HĐ cũ để không chồng lấn (bảo toàn IsRenewed),
        // hoặc từ đầu tháng đối soát nếu nhân sự chưa từng có hợp đồng.
        var start = latest?.NgayKetThuc is DateOnly oldEnd
            ? oldEnd.AddDays(1)
            : fromDate;

        if (start > renewalEnd)
        {
            Console.WriteLine($"  BỎ QUA NV#{emp.MaNhanVien}: HĐ cũ kết thúc sau {renewalEnd:dd/MM/yyyy}.");
            continue;
        }

        // Sinh số hợp đồng duy nhất theo đúng định dạng app (HDLD-YYYY/NN/OTH)
        string soHopDong;
        var seq = 1;
        do
        {
            soHopDong = $"HDLD-{year}/{seq:D2}/OTH";
            seq++;
        } while (existingNumbers.Contains(soHopDong.ToLowerInvariant()));
        existingNumbers.Add(soHopDong.ToLowerInvariant());

        var luong = latest?.LuongCoBan ?? 8000000m;

        context.HopDongs.Add(new HopDong
        {
            MaNhanVien = emp.MaNhanVien,
            SoHopDong = soHopDong,
            LoaiHopDong = "HĐLĐ Xác định thời hạn (3 năm)",
            NgayBatDau = start,
            NgayKetThuc = renewalEnd,
            LuongCoBan = luong,
            TrangThai = "HIEU_LUC",
            HinhThucHopDong = "CHINH_THUC"
        });

        Console.WriteLine($"  NV#{emp.MaNhanVien,-4} {emp.HoTen,-24} {soHopDong,-22} {start:dd/MM/yyyy} → {renewalEnd:dd/MM/yyyy}   {luong:N0} đ");
        created++;
    }

    await context.SaveChangesAsync();
    Console.WriteLine($"\n[OK] Đã tạo {created} hợp đồng gia hạn.");
    Console.WriteLine(">>> Chạy lại KHÔNG tham số để xem còn bao nhiêu phân ca vi phạm.");
    return 0;
}

// ==========================================================================
// PHA 3 — XÓA PHÂN CA VI PHẠM CÒN LẠI
// ==========================================================================
if (apply)
{
    var violationIds = violations.Select(v => v.Shift.MaPhanCa).ToHashSet();
    var attendanceOnViolation = await context.ChamCongs
        .Where(c => violationIds.Contains(c.MaPhanCa))
        .ToListAsync();

    Console.WriteLine("---- XÓA ----");
    if (attendanceOnViolation.Count > 0)
    {
        context.ChamCongs.RemoveRange(attendanceOnViolation);
        Console.WriteLine($"  Đã xóa {await context.SaveChangesAsync()} bản ghi chấm công gắn vào ca vi phạm.");
    }

    var toRemove = await context.PhanCas.Where(p => violationIds.Contains(p.MaPhanCa)).ToListAsync();
    context.PhanCas.RemoveRange(toRemove);
    Console.WriteLine($"  Đã xóa {await context.SaveChangesAsync()} phân ca vi phạm.");

    // Kiểm chứng
    var remaining = await context.PhanCas.Where(p => p.NgayLamViec >= fromDate).AsNoTracking().ToListAsync();
    var empAfter = (await context.NhanViens.Include(n => n.HopDongs).AsNoTracking().ToListAsync()).ToDictionary(e => e.MaNhanVien);
    var stillBad = remaining.Count(p => !empAfter.TryGetValue(p.MaNhanVien, out var e) || !ContractAttendancePolicy.HasValidContractOn(e, p.NgayLamViec));

    Console.WriteLine();
    Console.WriteLine("---- KIỂM CHỨNG ----");
    Console.WriteLine($"  Phân ca còn lại (từ {fromDate:dd/MM/yyyy}): {remaining.Count}");
    Console.WriteLine($"  Còn vi phạm:                      {stillBad}");
    Console.WriteLine(stillBad == 0 ? "  [OK] Sạch — mọi phân ca đều có HĐLĐ hiệu lực." : "  [FAIL] Vẫn còn vi phạm!");
    return 0;
}

Console.WriteLine(">>> CHẾ ĐỘ CHỈ ĐỌC. Chưa sửa gì.");
Console.WriteLine($">>> Gia hạn {renewalCandidates.Count} hợp đồng:  --renew");
Console.WriteLine($">>> Xóa {violations.Count} phân ca vi phạm:  --apply");
return 0;
