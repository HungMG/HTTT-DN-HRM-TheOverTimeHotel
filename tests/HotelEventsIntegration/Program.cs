using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;

// This runner writes only run-prefixed fixtures. It never creates or migrates schema.
var passed = 0;
var failed = 0;
var filterIndex = Array.IndexOf(args, "--filter");
var filter = filterIndex >= 0 && filterIndex + 1 < args.Length ? args[filterIndex + 1] : null;
Fixture? fixture = null;
var keepFixture = false;
var previewPath = Path.Combine(RepositoryRoot(), ".probe", "event-preview.json");
if (args.Contains("--cleanup-preview"))
{
    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(previewPath));
    var preview = new Fixture(ReadConnection(), json.RootElement.GetProperty("Prefix").GetString());
    await preview.Cleanup();
    File.Delete(previewPath);
    Console.WriteLine("PASS preview fixture cleanup");
    return 0;
}
if (args.Contains("--apply-migration"))
{
    var sqlPath = Path.Combine(RepositoryRoot(), "src", "OvertimeHotel.HRM.Data", "migrations", args.Length > 1 && args[1].EndsWith(".sql") ? args[1] : "20261023_event_hourly_shift_planning.sql");
    var sql = await File.ReadAllTextAsync(sqlPath);
    await using var conn = new NpgsqlConnection(ReadConnection());
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("PASS migration 20261016_remove_maintenance_events.sql applied successfully");
    return 0;
}
static bool IsTransientNetwork(Exception? ex)
{
    while (ex != null)
    {
        if (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException || ex is Npgsql.NpgsqlException)
            return true;
        ex = ex.InnerException;
    }
    return false;
}

async Task Check(string name, Func<Task> run)
{
    if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
    for (var attempt = 1; attempt <= 2; attempt++)
    {
        try
        {
            await run();
            passed++;
            Console.WriteLine($"PASS {name}");
            return;
        }
        catch (Exception ex) when (attempt < 2 && IsTransientNetwork(ex))
        {
            Console.WriteLine($"RETRY {name} (attempt {attempt}) due to cloud network glitch...");
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            failed++;
            Console.Error.WriteLine($"FAIL {name}:");
            var cur = ex;
            while (cur != null)
            {
                Console.Error.WriteLine($"  [{cur.GetType().Name}] {cur.Message}");
                if (cur is PostgresException pg)
                    Console.Error.WriteLine($"  [PostgresException] code={pg.SqlState} table={pg.TableName} constraint={pg.ConstraintName} detail={pg.Detail} message={pg.MessageText}");
                cur = cur.InnerException;
            }
            Console.Error.WriteLine(ex.StackTrace);
            return;
        }
    }
}

try
{
    var previewPassword = args.Contains("--preview-fixture") ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) : null;
    fixture = new Fixture(ReadConnection(), password: previewPassword);
    var f = fixture;
    await f.Initialize();
    if (args.Contains("--verify-schema-cleanup"))
    {
        await Check("Schema cleanup removes exactly the eight obsolete columns", () => SchemaCleanupTests.VerifySchema(f));
        return failed == 0 ? 0 : 1;
    }
    if (args.Contains("--schema-cleanup-migration"))
    {
        await Check("Schema cleanup archives removed values and preserves paid legacy links", () => SchemaCleanupTests.RunMigration(f, RepositoryRoot()));
        return failed == 0 ? 0 : 1;
    }
    if (previewPassword is not null)
    {
        var pastEvent = await f.Create(f.Input());
        await f.MoveToPast(pastEvent.MaDuAn);
        pastEvent = await f.Get(pastEvent.MaDuAn);
        var hourlyPreview = await HourlyOperationsTests.PreparePreview(f);
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
        var preview = new
        {
            f.Prefix, Password = previewPassword,
            PastEventId = pastEvent.MaDuAn, PastEventDate = HotelEventRules.Local(pastEvent.NgayBatDau).ToString("yyyy-MM-dd"),
            HourlyEventId = hourlyPreview.EventId, HourlyShiftId = hourlyPreview.ShiftId,
            Admin = new { Username = f.Prefix + "-admin", f.Admin.EmployeeId, f.Admin.AccountId },
            HR = new { Username = f.Prefix + "-hr", f.HR.EmployeeId, f.HR.AccountId },
            Manager = new { Username = f.Prefix + "-manager", f.Manager.EmployeeId, f.Manager.AccountId },
            OtherManager = new { Username = f.Prefix + "-manager2", f.OtherManager.EmployeeId, f.OtherManager.AccountId },
            IT = new { Username = f.Prefix + "-it", f.IT.EmployeeId, f.IT.AccountId },
            f.WorkerA, f.WorkerB, f.RoleWorkers
        };
        await File.WriteAllTextAsync(previewPath, JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true }));
        keepFixture = true;
        Console.WriteLine("PASS preview fixture created; credentials and exact run prefix are in .probe/event-preview.json");
        return 0;
    }

    await Check("HR-only event manager picker excludes other roles and unavailable HR", async () =>
    {
        var people = await f.With(s => s.ManagersAsync());
        Require(people.Any(n => n.MaNhanVien == f.HR.EmployeeId), "Active HR is missing.");
        Require(!people.Any(n => new[] { f.Manager.EmployeeId, f.OtherManager.EmployeeId, f.Admin.EmployeeId, f.IT.EmployeeId, f.WorkerA }.Contains(n.MaNhanVien)),
            "Non-HR employees are available as event managers.");
        await using var db = f.Open();
        var ids = people.Select(n => n.MaNhanVien).ToArray();
        Require(!await db.NhanViens.AnyAsync(n => ids.Contains(n.MaNhanVien)
            && (n.TrangThai != "DANG_LAM" || n.TaiKhoan == null || !n.TaiKhoan.TrangThai
                || n.TaiKhoan.VaiTro == null || n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() != "HR")), "Unavailable HR was offered.");
    });
    await Check("HR-only event manager rejects forged non-HR selection on create", async () =>
    {
        foreach (var person in new[] { f.Manager.EmployeeId, f.Admin.EmployeeId, f.IT.EmployeeId, f.WorkerA })
        {
            var input = f.Input();
            input.ManagerId = person;
            await Reject(() => f.With(s => s.SaveAsync(f.Admin, input)), field: "Input.ManagerId");
            await using var db = f.Open();
            Require(!await db.DuAns.AnyAsync(e => e.TenDuAn == input.Name), "Invalid manager was persisted.");
        }
    });
    await Check("HR-only event manager saves HR and rejects non-HR on edit", async () =>
    {
        var input = f.Input();
        input.ManagerId = f.HR.EmployeeId;
        var item = await f.Create(input);
        Require(item.MaNguoiQuanLy == f.HR.EmployeeId, "HR manager was not saved.");
        var edit = f.Edit(item);
        edit.ManagerId = f.Manager.EmployeeId;
        await Reject(() => f.With(s => s.SaveAsync(f.HR, edit)), field: "Input.ManagerId");
        Require((await f.Get(item.MaDuAn)).MaNguoiQuanLy == f.HR.EmployeeId, "Rejected edit changed the manager.");
    });

    await EventAutoStartTests.Run(f, Check);
    await HourlyOperationsTests.RunPhase3(f, Check);
    await HourlyOperationsTests.RunMergeCompatibility(f, Check);
    await EventReportTests.Run(f, Check);
    await HourlyOperationsTests.Run(f, Check);
    await Phase1Tests.Run(f, Check);
    await EventTypeIntegrationTests.Run(f, Check);

    await Check("Creation rejects past dates but accepts today and future", async () =>
    {
        var today = HotelEventRules.Local(DateTimeOffset.UtcNow).Date;
        foreach (var end in new[] { today.AddHours(-1), today.AddDays(1) })
        {
            var past = f.Input();
            past.Start = today.AddDays(-1).AddHours(9);
            past.End = end;
            await Reject(() => f.With(s => s.SaveAsync(f.Admin, past)), field: "Input.Start");
            await using var db = f.Open();
            Require(!await db.DuAns.AnyAsync(e => e.TenDuAn == past.Name), "A rejected past event was persisted.");
        }
        var current = f.Input();
        current.Start = today;
        current.End = today.AddHours(1);
        await f.Create(current);
        await f.Create(f.Input());
    });

    await Check("Create persists UTC+7 wall clock, manager and audit", async () =>
    {
        var input = f.Input();
        var item = await f.Create(input);
        Require(item.NgayBatDau == new DateTimeOffset(input.Start, TimeSpan.FromHours(7)), "Start lost Vietnam timezone.");
        Require(item.MaNguoiQuanLy == f.HR.EmployeeId && item.Version > 0, "Manager or version missing.");
        var history = await f.With(s => s.HistoryAsync(item.MaDuAn));
        Require(history.Any(h => h.TenTaiKhoanBiTacDong == $"SK-{item.MaDuAn}"), "Create audit was not committed.");
    });

    await Check("Historical events remain readable after creation dates are restricted", async () =>
    {
        var item = await f.Create(f.Input());
        await f.SeedLegacyManager(item.MaDuAn, f.Manager.EmployeeId);
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        Require(item.NgayBatDau < DateTimeOffset.UtcNow.AddDays(-1), "Fixture must be historical.");
        var events = await f.With(s => s.ListAsync(f.Admin, item.NgayBatDau.AddDays(-1), item.NgayKetThuc.AddDays(1)));
        Require(events.Any(e => e.MaDuAn == item.MaDuAn), "Historical event disappeared from the calendar.");
        Require((await f.With(s => s.GetAsync(f.Manager, item.MaDuAn)))?.MaDuAn == item.MaDuAn, "Manager lost historical details.");
    });

    await Check("Event dates outside supported calendar years are rejected", async () =>
    {
        foreach (var year in new[] { 1899, 2101 })
        {
            var input = f.Input();
            input.Start = new DateTime(year, 1, 1, 8, 0, 0);
            input.End = input.Start.AddHours(1);
            await Reject(() => f.With(s => s.SaveAsync(f.Admin, input)), field: "Input.Start");
        }
    });

    await Check("Hotel is a general location and simultaneous events do not reserve an exclusive hall", async () =>
    {
        var original = f.Input();
        await f.Create(original);
        var overlap = f.Input();
        overlap.Venue = $"  {original.Venue.ToLowerInvariant()}  ";
        overlap.Start = original.Start.AddMinutes(30);
        overlap.End = original.End;
        var simultaneous = await f.Create(overlap);
        Require(simultaneous.DiaDiem == "Khách sạn The Overtime",
            "Simultaneous hotel events unexpectedly require a hall.");
        overlap.Name += "-adjacent";
        overlap.Start = original.End;
        overlap.End = original.End.AddHours(1);
        await f.Create(overlap);
    });

    await Check("Multiday calendar query includes middle days and excludes midnight end", async () =>
    {
        var input = f.Input();
        input.Start = input.Start.Date.AddHours(22);
        input.End = input.Start.Date.AddDays(3);
        var item = await f.Create(input);
        var middle = new DateTimeOffset(input.Start.Date.AddDays(1), TimeSpan.FromHours(7));
        Require((await f.With(s => s.ListAsync(f.Admin, middle, middle.AddDays(1)))).Any(e => e.MaDuAn == item.MaDuAn), "Middle day lost multiday event.");
        var end = new DateTimeOffset(input.End, TimeSpan.FromHours(7));
        Require((await f.With(s => s.ListAsync(f.Admin, end, end.AddDays(1)))).All(e => e.MaDuAn != item.MaDuAn), "Midnight end leaked into next day.");
    });

    await Check("Manager cannot read or mutate another manager's event", async () =>
    {
        var input = f.Input();
        var item = await f.Create(input);
        await f.SeedLegacyManager(item.MaDuAn, f.OtherManager.EmployeeId);
        Require(await f.With(s => s.GetAsync(f.Manager, item.MaDuAn)) is null, "Foreign event details leaked.");
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "HUY", "Test")), true);
        var from = item.NgayBatDau.AddDays(-1);
        Require((await f.With(s => s.ListAsync(f.Manager, from, from.AddDays(3)))).All(e => e.MaDuAn != item.MaDuAn), "Foreign event leaked into calendar.");
    });

    await Check("IT cannot create or list events even with a forged Admin role", async () =>
    {
        var forged = f.IT with { Role = "Admin" };
        await Reject(() => f.With(s => s.SaveAsync(forged, f.Input())), true);
        await Reject(() => f.With(s => s.ListAsync(forged, DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow)), true);
    });

    await Check("Manager cannot create or accept events", async () =>
    {
        await Reject(() => f.With(s => s.SaveAsync(f.Manager, f.Input())), true);
        var item = await f.Create(f.Input());
        await f.SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.AcceptAsync(f.Manager, item.MaDuAn, item.Version)), true);
    });

    await Check("Cross-department assignment persists without an implicit payroll entry", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA, 125.50m)));
        var row = (await f.Get(item.MaDuAn)).PhanCongs.Single();
        Require(row.MaNhanVien == f.WorkerA && row.PhuCapDuKien == 125.50m, "Assignment was not stored.");
        Require(row.SoTienPhuCap == 0 && row.MaChiTiet is null && row.TrangThaiChiTra == "CHUA_CHI_TRA", "Assignment created payment prematurely.");
    });

    await Check("Multiple assignments save together and reject invalid groups atomically", async () =>
    {
        var item = await f.Create(f.Input());
        await Reject(() => f.With(s => s.AssignManyAsync(f.HR, item.MaDuAn, item.Version,
            [f.Assignment(item, f.WorkerA), f.Assignment(item, f.InactiveWorker)])));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 0, "Invalid batch partially assigned staff.");
        await Reject(() => f.With(s => s.AssignManyAsync(f.HR, item.MaDuAn, item.Version,
            [f.Assignment(item, f.WorkerA), f.Assignment(item, f.WorkerA)])));
        await Reject(() => f.With(s => s.AssignManyAsync(f.HR, item.MaDuAn, item.Version, [])));
        var second = f.Assignment(item, f.WorkerB);
        second.Role = HotelEventRoleRules.GetRoles(item.LoaiDuAn, "KY_THUAT", item.SoKhachDuKien)[1].Name;
        await f.With(s => s.AssignManyAsync(f.HR, item.MaDuAn, item.Version, [f.Assignment(item, f.WorkerA), second]));
        var after = await f.Get(item.MaDuAn);
        Require(after.PhanCongs.Count == 2 && after.Version == item.Version + 1
            && after.PhanCongs.Single(p => p.MaNhanVien == f.WorkerB).VaiTroDuAn == second.Role,
            "Batch did not save individual roles together.");
        await Reject(() => f.With(s => s.AssignManyAsync(f.HR, item.MaDuAn, item.Version,
            [f.Assignment(item, f.WorkerA)])));
        await Reject(() => f.With(s => s.AssignManyAsync(f.OtherManager, item.MaDuAn, after.Version,
            [f.Assignment(after, f.WorkerB)])), forbidden: true);
    });

    await Check("Duplicate employee assignment is rejected", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 1, "Duplicate row persisted.");
    });

    await Check("Inactive and uncovered employees cannot be assigned", async () =>
    {
        var item = await f.Create(f.Input());
        foreach (var worker in new[] { f.InactiveWorker, f.ExpiredWorker })
            await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, worker))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 0, "Invalid staffing persisted.");
    });

    await Check("Contract must cover the last participation day", async () =>
    {
        var input = f.Input();
        input.End = input.Start.AddDays(1);
        var item = await f.Create(input);
        await f.SetContractEnd(f.BoundaryWorker, DateOnly.FromDateTime(input.Start));
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.BoundaryWorker))));
    });

    await Check("Midnight participation accepts a contract ending on the previous day", async () =>
    {
        var input = f.Input();
        input.End = input.Start.Date.AddDays(1);
        var item = await f.Create(input);
        await f.SetContractEnd(f.BoundaryWorker, DateOnly.FromDateTime(input.Start));
        var assignment = f.Assignment(item, f.BoundaryWorker);
        try { await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, assignment)); }
        catch (EventOperationException)
        {
            Console.Error.WriteLine(await f.CoverageDiagnostic(f.BoundaryWorker, item, assignment));
            throw;
        }
    });

    await Check("Employee cannot overlap across simultaneous hotel events", async () =>
    {
        var input = f.Input();
        var a = await f.Create(input);
        var other = f.Input();
        other.Start = input.Start;
        other.End = input.End;
        var b = await f.Create(other);
        await f.With(s => s.AssignAsync(f.HR, a.MaDuAn, f.Assignment(a, f.WorkerA)));
        await Reject(() => f.With(s => s.AssignAsync(f.HR, b.MaDuAn, f.Assignment(b, f.WorkerA))));
    });

    await Check("Planned allowance cannot exceed event budget", async () =>
    {
        var input = f.Input();
        input.Budget = 100;
        var item = await f.Create(input);
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA, 60)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerB, 40.01m))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 1, "Over-budget assignment persisted.");
    });

    await Check("Review requires waiting state and event end", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, f.Review(item))));
        var future = f.Input();
        future.Start = DateTime.Today.AddDays(5).AddHours(8);
        future.End = future.Start.AddHours(2);
        var upcoming = await f.Create(future);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.HR, upcoming.MaDuAn, upcoming.Version, "DANG_DIEN_RA")));
        await f.With(s => s.AssignAsync(f.HR, upcoming.MaDuAn, f.Assignment(upcoming, f.WorkerB)));
        await f.SeedStatus(upcoming.MaDuAn, "CHO_NGHIEM_THU");
        upcoming = await f.Get(upcoming.MaDuAn);
        await Reject(() => f.With(s => s.ReviewAsync(f.HR, upcoming.MaDuAn, f.Review(upcoming))));
    });

    await Check("Lifecycle reaches waiting state only after the event ends", async () =>
    {
        var item = await f.Create(f.Input());
        await f.StaffFully(item);
        item = await f.Get(item.MaDuAn);
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "CHO_NGHIEM_THU"));
        Require((await f.Get(item.MaDuAn)).TrangThai == "CHO_NGHIEM_THU", "Lifecycle did not persist.");
    });

    await Check("Actual allowance and absent payment are validated", async () =>
    {
        var item = await f.WaitingWithAssignment(100);
        var review = f.Review(item, 100.01m);
        await Reject(() => f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, review)));
        review.ActualAllowance = -1;
        await Reject(() => f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, review)));
        review.ActualAllowance = 1;
        review.Rating = "KHONG_THAM_GIA";
        await Reject(() => f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, review)));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Single().DanhGia == "CHUA_DANH_GIA", "Invalid review persisted.");
    });

    await Check("Acceptance rejects missing reviews", async () =>
    {
        var item = await f.WaitingWithAssignment();
        await Reject(() => f.With(s => s.AcceptAsync(f.Admin, item.MaDuAn, item.Version)));
        Require((await f.Get(item.MaDuAn)).TrangThai == "CHO_NGHIEM_THU", "Unreviewed event accepted.");
    });

    await Check("Automatic event budget is frozen from completed staff at acceptance", async () =>
    {
        var input = f.Input();
        input.CalculateBudgetAtAcceptance = true;
        input.Budget = 9000;
        input.Name = f.Prefix + new string('X', 200 - f.Prefix.Length);
        var item = await f.Create(input);
        Require(item.TinhPhuCapKhiNghiemThu && item.NganSachPhuCap == 0, "New event kept a creation budget.");
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA, 123)));
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerB, 123)));
        item = await f.Get(item.MaDuAn);
        Require(item.PhanCongs.All(p => p.PhuCapDuKien == 0 && p.MaPhongBanSuKien == "KY_THUAT"),
            "Automatic event did not snapshot the workers' department without planned allowances.");
        await f.MoveToPast(item.MaDuAn);
        await f.SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
        item = await f.Get(item.MaDuAn);
        var review = new EventReviewInput { Version = item.Version,
            AssignmentId = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerA).MaPhanCong,
            Rating = "HOAN_THANH", ActualAllowance = 987 };
        await f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, review));
        item = await f.Get(item.MaDuAn);
        review = new EventReviewInput { Version = item.Version,
            AssignmentId = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerB).MaPhanCong,
            Rating = "KHONG_THAM_GIA", ActualAllowance = 987 };
        await f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, review));
        item = await f.Get(item.MaDuAn);
        Require(item.PhanCongs.All(p => p.SoTienPhuCap == 0), "Review paid staff before acceptance.");
        int? originalDepartment;
        await using (var departmentDb = f.Open())
        {
            originalDepartment = await departmentDb.NhanViens.Where(n => n.MaNhanVien == f.WorkerA)
                .Select(n => n.MaPhongBan).SingleAsync();
            var unrelatedDepartment = await departmentDb.NhanViens.Where(n => n.MaNhanVien == f.WorkerUnrelated)
                .Select(n => n.MaPhongBan).SingleAsync();
            await departmentDb.NhanViens.Where(n => n.MaNhanVien == f.WorkerA)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.MaPhongBan, unrelatedDepartment));
        }
        try { await f.With(s => s.AcceptAsync(f.HR, item.MaDuAn, item.Version)); }
        finally
        {
            await using var departmentDb = f.Open();
            await departmentDb.NhanViens.Where(n => n.MaNhanVien == f.WorkerA)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.MaPhongBan, originalDepartment));
        }
        item = await f.Get(item.MaDuAn);
        Require(item.TrangThai == "HOAN_THANH" && item.NganSachPhuCap > 0
            && item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerA).SoTienPhuCap == item.NganSachPhuCap
            && item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerB).SoTienPhuCap == 0,
            "Acceptance did not freeze a matching employee payment and total budget.");
        var workMonth = HourlyEventRules.WorkMonth(item.NgayKetThuc);
        bool existingPeriod;
        await using (var periodDb = f.Open())
            existingPeriod = await periodDb.KyLuongs.AnyAsync(k => k.Thang == workMonth.Month && k.Nam == workMonth.Year);
        var periodId = await f.With(s => s.EnsurePaymentPeriodAsync(f.HR, workMonth.Month, workMonth.Year));
        if (!existingPeriod) HourlyOperationsTests.TrackPeriod(periodId);
        await f.With(s => s.ReconcileLegacyAsync(f.HR, item.MaDuAn, item.Version, periodId,
            "HR verified the historical frozen employee allowance and work month."));
        await using var db = f.Open();
        var payslip = await db.PhieuLuongs.SingleOrDefaultAsync(p => p.MaNhanVien == f.WorkerA && p.MaKyLuong == periodId);
        if (payslip is null)
        {
            payslip = new PhieuLuong { MaNhanVien = f.WorkerA, MaKyLuong = periodId };
            db.PhieuLuongs.Add(payslip);
            await db.SaveChangesAsync();
        }
        Require(await new PayrollCalculationService(db).RecalculateEmployeeAsync(payslip.MaPhieuLuong, "Event test", null),
            "Payroll recalculation failed for a reconciled historical event with a long name.");
        var currentPayslip = await db.PhieuLuongs.SingleAsync(p => p.MaNhanVien == f.WorkerA && p.MaKyLuong == periodId);
        var payrollItem = await db.ChiTietPhieuLuongs.SingleAsync(d => d.MaPhieuLuong == currentPayslip.MaPhieuLuong
            && d.LoaiKhoan == "THUONG" && d.GhiChu != null && d.GhiChu.Contains($"SK-{item.MaDuAn}"));
        Require(payrollItem.GhiChu!.Length <= 255 && payrollItem.SoTien == item.NganSachPhuCap,
            "Long event name prevented the reconciled frozen payout from reaching its selected payroll period.");
    });

    await Check("Acceptance freezes edits, staffing, reviews and status", async () =>
    {
        var item = await f.ReadyForAcceptance();
        await f.With(s => s.AcceptAsync(f.HR, item.MaDuAn, item.Version));
        item = await f.Get(item.MaDuAn);
        Require(item.TrangThai == "HOAN_THANH", "Acceptance did not commit.");
        var edit = f.Edit(item);
        edit.Name += " changed";
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, edit)));
        await Reject(() => f.With(s => s.AssignAsync(f.Admin, item.MaDuAn, f.Assignment(item, f.WorkerB))));
        await Reject(() => f.With(s => s.RemoveAssignmentAsync(f.Admin, item.MaDuAn, item.PhanCongs.Single().MaPhanCong, item.Version)));
        await Reject(() => f.With(s => s.ReviewAsync(f.Admin, item.MaDuAn, f.Review(item))));
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Admin, item.MaDuAn, item.Version, "HUY", "Test")));
        var after = await f.Get(item.MaDuAn);
        Require(after.Version == item.Version && after.TenDuAn == item.TenDuAn && after.PhanCongs.Count == 1, "Frozen event changed.");
    });

    await Check("Stale update cannot overwrite a newer edit", async () =>
    {
        var item = await f.Create(f.Input());
        var stale = f.Edit(item);
        var fresh = f.Edit(item);
        fresh.Description = "winning edit";
        await f.With(s => s.SaveAsync(f.Admin, fresh));
        stale.Description = "stale edit";
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, stale)));
        Require((await f.Get(item.MaDuAn)).MoTa == "winning edit", "Stale edit overwrote winner.");
    });

    await Check("Stale update cannot overwrite acceptance", async () =>
    {
        var item = await f.ReadyForAcceptance();
        var stale = f.Edit(item);
        stale.Description = "overwrite acceptance";
        await f.With(s => s.AcceptAsync(f.Admin, item.MaDuAn, item.Version));
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, stale)));
        var after = await f.Get(item.MaDuAn);
        Require(after.TrangThai == "HOAN_THANH" && after.MoTa != stale.Description, "Stale edit overwrote acceptance.");
    });

    await Check("Cancellation requires a reason and keeps later hotel event creation available", async () =>
    {
        var input = f.Input();
        var item = await f.Create(input);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Admin, item.MaDuAn, item.Version, "HUY", " ")));
        await f.With(s => s.ChangeStatusAsync(f.Admin, item.MaDuAn, item.Version, "HUY", "Integration fixture cancelled"));
        var replacement = f.Input();
        replacement.Venue = input.Venue;
        replacement.Start = input.Start;
        replacement.End = input.End;
        await f.Create(replacement);
    });

    await Check("Concurrent hotel events with distinct names can both be created", async () =>
    {
        var first = f.Input();
        var second = f.Input();
        second.Venue = first.Venue;
        second.Start = first.Start;
        second.End = first.End;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Create(EventInput input)
        {
            await gate.Task;
            return await Succeeds(() => f.With(s => s.SaveAsync(f.Admin, input)));
        }
        var a = Create(first);
        var b = Create(second);
        gate.SetResult();
        var result = await Task.WhenAll(a, b);
        Require(result.Count(x => x) == 2, "A general hotel location blocked simultaneous events.");
        await using var db = f.Open();
        Require(await db.DuAns.CountAsync(e => e.TenDuAn == first.Name || e.TenDuAn == second.Name) == 2, "Concurrent hotel event creation lost a record.");
    });

    await Check("Concurrent cross-event staffing has exactly one winner", async () =>
    {
        var first = f.Input();
        var second = f.Input();
        second.Start = first.Start;
        second.End = first.End;
        var a = await f.Create(first);
        var b = await f.Create(second);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Assign(DuAn item)
        {
            await gate.Task;
            return await Succeeds(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA))));
        }
        var left = Assign(a);
        var right = Assign(b);
        gate.SetResult();
        Require((await Task.WhenAll(left, right)).Count(x => x) == 1, "Staffing race did not produce exactly one winner.");
        Require((await f.Get(a.MaDuAn)).PhanCongs.Count + (await f.Get(b.MaDuAn)).PhanCongs.Count == 1, "Concurrent employee conflict persisted.");
    });

    await Check("Concurrent acceptance commits once with one audit", async () =>
    {
        var item = await f.ReadyForAcceptance();
        var before = (await f.With(s => s.HistoryAsync(item.MaDuAn))).Count;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Accept(EventActor actor)
        {
            await gate.Task;
            return await Succeeds(() => f.With(s => s.AcceptAsync(actor, item.MaDuAn, item.Version)));
        }
        var a = Accept(f.Admin);
        var b = Accept(f.HR);
        gate.SetResult();
        Require((await Task.WhenAll(a, b)).Count(x => x) == 1, "Acceptance race did not produce exactly one winner.");
        var after = await f.Get(item.MaDuAn);
        Require(after.TrangThai == "HOAN_THANH" && after.Version == item.Version + 1, "Acceptance was not atomic.");
        Require((await f.With(s => s.HistoryAsync(item.MaDuAn))).Count == before + 1, "Acceptance audit duplicated or lost.");
    });

    await Check("Assignment rejects forged roles and requires a name for custom roles", async () =>
    {
        var item = await f.Create(f.Input());
        var input = f.Assignment(item, f.WorkerA);
        input.Role = "Tiếp nhận đăng ký";
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, input)), field: "Assignment.Role");
        input.Role = "Role forged by client";
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, input)), field: "Assignment.Role");
    });

    await Check("Custom roles retain their prefix and new assignments have no task", async () =>
    {
        var item = await f.Create(f.Input());
        var input = f.Assignment(item, f.WorkerA);
        input.Role = "__OTHER__";
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, input)), field: "Assignment.Role");
        input.OtherRole = "Custom support";
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, input));
        var stored = (await f.Get(item.MaDuAn)).PhanCongs.Single();
        Require(stored.VaiTroDuAn == "Khác: Custom support" && stored.NhiemVuCuThe is null, "Custom role or empty task was not preserved.");
    });

    await Check("A primary-only team cannot start despite enough employees per department", async () =>
    {
        var item = await f.Create(f.Input());
        foreach (var dept in HotelEventRules.GetRequiredDepartments(item.LoaiDuAn))
            foreach (var worker in f.RoleWorkers[dept].Take(2))
            {
                await f.With(s => s.AssignAsync(f.HR,item.MaDuAn,f.Assignment(item,worker)));
                item = await f.Get(item.MaDuAn);
            }
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.HR,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("A zero-guest event cannot start with an otherwise complete team", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var edit = f.Edit(item); edit.ExpectedGuests = 0;
        await f.With(s => s.SaveAsync(f.Admin,edit));
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.HR,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("Changing guests keeps existing assignments but increases role demand", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var oldCount=item.PhanCongs.Count;
        var edit=f.Edit(item); edit.ExpectedGuests=120;
        await f.With(s=>s.SaveAsync(f.Admin,edit));
        await f.MoveToOngoing(item.MaDuAn);
        item=await f.Get(item.MaDuAn);
        Require(item.PhanCongs.Count==oldCount,"Changing guests silently removed assignments.");
        await Reject(()=>f.With(s=>s.ChangeStatusAsync(f.HR,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("Staffing rejects employees belonging to disallowed departments", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE
        await Reject(() => f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerUnrelated))), field: "Assignment.EmployeeId");
    });

    await Check("Starting an event is blocked when any mandatory department is missing", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE requires: TIEN_SANH, AM_THUC, BEP, KY_THUAT, IT, AN_NINH
        // Assign 5 departments, leaving out AN_NINH
        foreach (var worker in new[] { f.WorkerFO, f.WorkerFB, f.WorkerKitchen, f.WorkerA, f.WorkerIT })
        {
            await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, worker)));
            item = await f.Get(item.MaDuAn);
        }

        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        try
        {
            await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
            throw new InvalidOperationException("Event started while missing mandatory department.");
        }
        catch (EventOperationException ex)
        {
            Require(ex.Message.Contains("An ninh"), "Rejection did not name the missing department: " + ex.Message);
        }
    });

    await Check("Starting an event succeeds when all mandatory departments are satisfied", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);
        Require(item.TrangThai == "DANG_DIEN_RA", "Event failed to transition to DANG_DIEN_RA after full staffing.");
    });

    await Check("Mở trang không làm hủy sự kiện của mình hoặc người khác", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerA, 100)));
        item = await f.Get(item.MaDuAn);
        Require(item.TrangThai == "LEN_KE_HOACH", "Fixture should start in LEN_KE_HOACH.");

        await f.MoveToPast(item.MaDuAn);

        var refreshed = await f.Get(item.MaDuAn);
        Require(refreshed.TrangThai == "LEN_KE_HOACH", $"Event whose day has passed must remain LEN_KE_HOACH, but was {refreshed.TrangThai}.");
        Require(string.IsNullOrEmpty(refreshed.LyDoHuy), $"Cancellation reason should be empty, but was '{refreshed.LyDoHuy}'.");
        Require(refreshed.PhanCongs.Any(p => p.MaNhanVien == f.WorkerA && p.TrangThaiChiTra != "HUY_BO"), "Assignments must remain intact.");

        var history = await f.With(s => s.HistoryAsync(item.MaDuAn));
        var autoCancelAudit = history.FirstOrDefault(h => h.HanhDong == "TU_DONG_HUY_SU_KIEN");
        Require(autoCancelAudit == null, "TU_DONG_HUY_SU_KIEN should NOT be logged when viewing an event.");
    });

    await Check("Ending a mandatory role is blocked even when another role has extra people", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var spare = f.RoleWorkers["KY_THUAT"][2];
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, spare)));
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.HR, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);

        var assignB = item.PhanCongs.Single(p => p.MaNhanVien == spare);
        var handover = HotelEventRules.Local(DateTimeOffset.UtcNow);
        await f.With(s => s.EndParticipationAsync(f.HR, item.MaDuAn, assignB.MaPhanCong, item.Version, handover,
            "Spare technician handed over after completing their work."));
        item = await f.Get(item.MaDuAn);
        Require(item.PhanCongs.Single(p => p.MaPhanCong == assignB.MaPhanCong).TrangThaiThamGia == "DA_KET_THUC",
            "Ending spare participation must keep its historical record.");

        // WorkerB still covers support, but cannot substitute for WorkerA's primary role.
        var assignA = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerA);
        await Reject(() => f.With(s => s.EndParticipationAsync(f.HR, item.MaDuAn, assignA.MaPhanCong, item.Version,
            HotelEventRules.Local(DateTimeOffset.UtcNow), "Attempted handover without a replacement")));
    });

    await Check("Changing event type is rejected if assigned staff are incompatible with new type", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE allows IT
        await f.With(s => s.AssignAsync(f.HR, item.MaDuAn, f.Assignment(item, f.WorkerIT)));
        item = await f.Get(item.MaDuAn);

        var edit = f.Edit(item);
        edit.Type = "TIEC_CUOI"; // TIEC_CUOI does NOT allow IT
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, edit)), field: "Input.Type");

        // After removing WorkerIT, changing to TIEC_CUOI succeeds
        var assignIT = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerIT);
        await f.With(s => s.RemoveAssignmentAsync(f.HR, item.MaDuAn, assignIT.MaPhanCong, item.Version));
        item = await f.Get(item.MaDuAn);

        edit.Version = item.Version;
        await f.With(s => s.SaveAsync(f.Admin, edit));
        item = await f.Get(item.MaDuAn);
        Require(item.LoaiDuAn == "TIEC_CUOI", "Event type was not updated to TIEC_CUOI.");
    });

    await Check("Creating BAO_TRI_NANG_CAP event is rejected", async () =>
    {
        var input = f.Input();
        input.Type = "BAO_TRI_NANG_CAP";
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, input)), field: "Input.Type");
    });
}
catch (Exception ex)
{
    failed++;
    // Avoid dumping configuration, connection strings or provider diagnostics on setup errors.
    Console.Error.WriteLine($"FAIL fixture setup: {ex.GetType().Name}: {ex.Message}\n{(ex.InnerException != null ? "INNER: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message + "\n" : "")}{ex.StackTrace}");
    var provider = ex as PostgresException ?? ex.InnerException as PostgresException;
    if (provider is not null)
        Console.Error.WriteLine($"PostgreSQL: code={provider.SqlState}, table={provider.TableName}, constraint={provider.ConstraintName}, message={provider.MessageText}");
}
finally
{
    if (fixture is not null && !keepFixture)
    {
        try { await HourlyOperationsTests.Cleanup(fixture); await fixture.Cleanup(); Console.WriteLine("PASS owned fixture cleanup"); }
        catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL owned fixture cleanup: {ex}; run prefix {fixture.Prefix}"); }
    }
}
Console.WriteLine($"Hotel event PostgreSQL integration: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static async Task Reject(Func<Task> run, bool forbidden = false, string? field = null)
{
    try { await run(); }
    catch (EventOperationException ex)
    {
        Require(!forbidden || ex.Forbidden, "Authorization failure was not forbidden.");
        Require(field is null || ex.Field == field, "Validation was returned for the wrong field.");
        return;
    }
    throw new InvalidOperationException("Expected service rejection.");
}
static async Task<bool> Succeeds(Func<Task> run)
{
    try { await run(); return true; }
    catch (EventOperationException) { return false; }
}
static string ReadConnection()
{
    var supplied = Environment.GetEnvironmentVariable("EVENT_TEST_CONNECTION");
    if (!string.IsNullOrWhiteSpace(supplied)) return supplied;
    string? connection = null;
    foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
    {
        var path = Path.Combine(RepositoryRoot(), "src", "OvertimeHotel.HRM.WebAdmin", name);
        if (!File.Exists(path)) continue;
        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (json.RootElement.TryGetProperty("ConnectionStrings", out var strings) && strings.TryGetProperty("DefaultConnection", out var value))
            connection = value.GetString();
    }
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("No PostgreSQL connection configured.");
    var builder = new NpgsqlConnectionStringBuilder(connection)
    {
        KeepAlive = 15,
        ConnectionIdleLifetime = 15,
        Timeout = 30,
        CommandTimeout = 30
    };
    return builder.ConnectionString;
}
static string RepositoryRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src", "OvertimeHotel.HRM.WebAdmin"))) current = current.Parent;
    return current?.FullName ?? throw new InvalidOperationException("Run from the repository.");
}

sealed class Fixture(string connection, string? prefix = null, string? password = null)
{
    public string Prefix { get; } = prefix is null ? $"EVT-{Guid.NewGuid().ToString("N")[..12]}"
        : Regex.IsMatch(prefix, "^EVT(-TEST)?-[0-9a-f]{12,32}$") ? prefix : throw new ArgumentException("Invalid fixture prefix.");
    private int sequence;
    public EventActor Admin { get; private set; } = null!;
    public EventActor HR { get; private set; } = null!;
    public EventActor Manager { get; private set; } = null!;
    public EventActor OtherManager { get; private set; } = null!;
    public EventActor IT { get; private set; } = null!;
    public int WorkerA { get; private set; }
    public int WorkerB { get; private set; }
    public int InactiveWorker { get; private set; }
    public int ExpiredWorker { get; private set; }
    public int BoundaryWorker { get; private set; }
    public int WorkerFO { get; private set; }
    public int WorkerFB { get; private set; }
    public int WorkerKitchen { get; private set; }
    public int WorkerHK { get; private set; }
    public int WorkerSecurity { get; private set; }
    public int WorkerIT { get; private set; }
    public int WorkerUnrelated { get; private set; }
    public Dictionary<string, List<int>> RoleWorkers { get; private set; } = [];
    public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    public async Task<T> With<T>(Func<IHotelEventService, Task<T>> run)
    {
        await using var db = Open();
        return await run(new HotelEventService(db));
    }
    public async Task With(Func<IHotelEventService, Task> run)
    {
        await using var db = Open();
        await run(new HotelEventService(db));
    }
    public async Task Initialize()
    {
        await using (var orphanDb = Open())
        {
            var orphanEvents = await orphanDb.DuAns.Where(e => e.TenDuAn.StartsWith("EVT-") && !e.TenDuAn.StartsWith(Prefix)).Select(e => e.MaDuAn).ToArrayAsync();
            var empIds = await orphanDb.NhanViens.Where(e => e.EmailCongVu.StartsWith("EVT-") && !e.EmailCongVu.StartsWith(Prefix)).Select(e => e.MaNhanVien).ToArrayAsync();
            if (orphanEvents.Length > 0 || empIds.Length > 0)
            {
                var subjects = orphanEvents.Select(id => $"SK-{id}").ToArray();
                var accIds = await orphanDb.TaiKhoans.Where(a => empIds.Contains(a.MaNhanVien)).Select(a => a.MaTaiKhoan).ToArrayAsync();
                var pcIds = await orphanDb.PhanCongDuAns.Where(p => orphanEvents.Contains(p.MaDuAn) || empIds.Contains(p.MaNhanVien)).Select(p => p.MaPhanCong).ToArrayAsync();
                var plIds = await orphanDb.PhieuLuongs.Where(p => empIds.Contains(p.MaNhanVien)).Select(p => p.MaPhieuLuong).ToArrayAsync();

                await orphanDb.NhatKyQuanTris.Where(h => subjects.Contains(h.TenTaiKhoanBiTacDong) || (h.MaTaiKhoanThucHien.HasValue && accIds.Contains(h.MaTaiKhoanThucHien.Value))).ExecuteDeleteAsync();
                await orphanDb.CongSuKiens.Where(c => pcIds.Contains(c.MaPhanCong) || empIds.Contains(c.MaNguoiXacNhan)).ExecuteDeleteAsync();
                await orphanDb.QuyetToanSuKiens.Where(q => orphanEvents.Contains(q.MaDuAn) || empIds.Contains(q.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.ChiTietTruBaoHiems.Where(b => orphanDb.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == b.MaChiTiet)).ExecuteDeleteAsync();
                await orphanDb.ChiTietTruThueTncns.Where(t => orphanDb.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == t.MaChiTiet)).ExecuteDeleteAsync();
                await orphanDb.ChiTietTruViPhamTamUngs.Where(v => orphanDb.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == v.MaChiTiet)).ExecuteDeleteAsync();
                await orphanDb.ChiTietPhieuLuongs.Where(c => plIds.Contains(c.MaPhieuLuong)).ExecuteDeleteAsync();
                await orphanDb.PhieuLuongs.Where(p => empIds.Contains(p.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.PhanCongDuAns.Where(a => orphanEvents.Contains(a.MaDuAn) || empIds.Contains(a.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.NhuCauSuKiens.Where(n => orphanEvents.Contains(n.MaDuAn)).ExecuteDeleteAsync();
                await orphanDb.DuAns.Where(e => orphanEvents.Contains(e.MaDuAn)).ExecuteDeleteAsync();
                await orphanDb.DiaDiemSuKiens.Where(v => v.TenDiaDiem.StartsWith("EVT-") && !v.TenDiaDiem.StartsWith(Prefix)).ExecuteDeleteAsync();
                await orphanDb.ChamCongs.Where(c => orphanDb.PhanCas.Any(s => s.MaPhanCa == c.MaPhanCa && empIds.Contains(s.MaNhanVien))).ExecuteDeleteAsync();
                await orphanDb.PhanCas.Where(s => empIds.Contains(s.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.CongBoLichTuans.Where(w => empIds.Contains(w.MaNguoiCongBo)).ExecuteDeleteAsync();
                await orphanDb.DinhBienCas.Where(d => orphanDb.CaLamViecs.Any(c => c.MaCa == d.MaCa && c.TenCa.StartsWith("EVT-") && !c.TenCa.StartsWith(Prefix))).ExecuteDeleteAsync();
                await orphanDb.CaLamViecs.Where(c => c.TenCa.StartsWith("EVT-") && !c.TenCa.StartsWith(Prefix)).ExecuteDeleteAsync();
                await orphanDb.DonTus.Where(d => empIds.Contains(d.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.LoaiDons.Where(d => d.TenLoaiDon.StartsWith("EVT-") && !d.TenLoaiDon.StartsWith(Prefix)).ExecuteDeleteAsync();
                await orphanDb.HopDongs.Where(h => empIds.Contains(h.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.TaiKhoans.Where(a => accIds.Contains(a.MaTaiKhoan)).ExecuteDeleteAsync();
                await orphanDb.NhanViens.Where(e => empIds.Contains(e.MaNhanVien)).ExecuteDeleteAsync();
                await orphanDb.PhongBans.Where(p => p.TenPhongBan.StartsWith("EVT-") && !p.TenPhongBan.StartsWith(Prefix)).ExecuteDeleteAsync();
            }
        }
        await using var db = Open();
        var roles = await db.VaiTros.AsNoTracking().ToListAsync();
        var job = await db.ChucVus.Select(c => c.MaChucVu).FirstAsync();
        var office = new PhongBan { TenPhongBan = Prefix + "-it" };
        var operations = new PhongBan { TenPhongBan = Prefix + "-kythuat" };
        var deptFO = new PhongBan { TenPhongBan = Prefix + "-tiensanh" };
        var deptFB = new PhongBan { TenPhongBan = Prefix + "-amthuc" };
        var deptKitchen = new PhongBan { TenPhongBan = Prefix + "-bep" };
        var deptHK = new PhongBan { TenPhongBan = Prefix + "-buongphong" };
        var deptSec = new PhongBan { TenPhongBan = Prefix + "-anninh" };
        var deptUnrelated = new PhongBan { TenPhongBan = Prefix + "-nhansu" };
        db.PhongBans.AddRange(office, operations, deptFO, deptFB, deptKitchen, deptHK, deptSec, deptUnrelated);
        await db.SaveChangesAsync();
        async Task<NhanVien> Employee(string label, int department, string? role = null, bool active = true, bool expired = false)
        {
            var employee = new NhanVien
            {
                MaPhongBan = department, MaChucVu = job, Ho = "Integration", Ten = label,
                NgaySinh = new DateOnly(1990, 1, 1), GioiTinh = "NAM", DienThoai = "0900000000",
                EmailCongVu = $"{Prefix}-{label}@example.invalid", TrinhDo = "Test",
                NgayVaoLam = new DateOnly(2010, 1, 1), TrangThai = active ? "DANG_LAM" : "NGHI_PHEP"
            };
            employee.HopDongs.Add(new HopDong
            {
                SoHopDong = Prefix + "-" + label, LoaiHopDong = "KHONG_XAC_DINH_THOI_HAN",
                NgayBatDau = new DateOnly(2010, 1, 1), NgayKetThuc = expired ? new DateOnly(2020, 1, 1) : null,
                LuongCoBan = 1000000, TrangThai = "HIEU_LUC", HinhThucHopDong = "CHINH_THUC"
            });
            if (role is not null)
                employee.TaiKhoan = new TaiKhoan
                {
                    MaVaiTro = roles.Single(r => r.TenVaiTro.Equals(role, StringComparison.OrdinalIgnoreCase)).MaVaiTro,
                    TenDangNhap = Prefix + "-" + label,
                    MatKhauBam = password is null ? "integration-test-account-disabled-for-login"
                        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant(),
                    TrangThai = true
                };
            db.NhanViens.Add(employee);
            await db.SaveChangesAsync();
            return employee;
        }
        static EventActor Actor(NhanVien employee, string role) => new(employee.MaNhanVien, employee.TaiKhoan!.MaTaiKhoan, role, employee.HoTen);
        Admin = Actor(await Employee("admin", office.MaPhongBan, "Admin"), "Admin");
        HR = Actor(await Employee("hr", office.MaPhongBan, "HR"), "HR");
        Manager = Actor(await Employee("manager", office.MaPhongBan, "Manager"), "Manager");
        OtherManager = Actor(await Employee("manager2", office.MaPhongBan, "Manager"), "Manager");
        IT = Actor(await Employee("it", office.MaPhongBan, "IT"), "IT");
        WorkerA = (await Employee("worker-a", operations.MaPhongBan)).MaNhanVien;
        WorkerB = (await Employee("worker-b", operations.MaPhongBan)).MaNhanVien;
        InactiveWorker = (await Employee("inactive", operations.MaPhongBan, active: false)).MaNhanVien;
        ExpiredWorker = (await Employee("expired", operations.MaPhongBan, expired: true)).MaNhanVien;
        BoundaryWorker = (await Employee("boundary", operations.MaPhongBan)).MaNhanVien;
        WorkerFO = (await Employee("worker-fo", deptFO.MaPhongBan)).MaNhanVien;
        WorkerFB = (await Employee("worker-fb", deptFB.MaPhongBan)).MaNhanVien;
        WorkerKitchen = (await Employee("worker-kitchen", deptKitchen.MaPhongBan)).MaNhanVien;
        WorkerHK = (await Employee("worker-hk", deptHK.MaPhongBan)).MaNhanVien;
        WorkerSecurity = (await Employee("worker-sec", deptSec.MaPhongBan)).MaNhanVien;
        WorkerIT = (await Employee("worker-it", office.MaPhongBan)).MaNhanVien;
        WorkerUnrelated = (await Employee("worker-unrelated", deptUnrelated.MaPhongBan)).MaNhanVien;
        RoleWorkers = new()
        {
            ["TIEN_SANH"] = [WorkerFO], ["AM_THUC"] = [WorkerFB], ["BEP"] = [WorkerKitchen],
            ["BUONG_PHONG"] = [WorkerHK], ["KY_THUAT"] = [WorkerA, WorkerB],
            ["AN_NINH"] = [WorkerSecurity], ["IT"] = [WorkerIT]
        };
        foreach (var (code, workers) in RoleWorkers)
        {
            var department = db.NhanViens.Local.Single(n => n.MaNhanVien == workers[0]).MaPhongBan!.Value;
            while (workers.Count < 3)
                workers.Add((await Employee($"role-{code}-{workers.Count+1}", department)).MaNhanVien);
        }
    }
    public async Task<DuAn> StaffFully(DuAn item)
    {
        var depts = HotelEventRules.GetRequiredDepartments(item.LoaiDuAn);
        foreach (var d in depts)
        {
            foreach (var role in HotelEventRoleRules.GetRoles(item.LoaiDuAn,d,item.SoKhachDuKien).Where(r=>r.IsRequired))
                while (item.PhanCongs.Count(p=>p.VaiTroDuAn==role.Name)<role.RequiredCount)
                {
                    var worker=RoleWorkers[d].First(id=>item.PhanCongs.All(p=>p.MaNhanVien!=id));
                    var input=Assignment(item,worker); input.Role=role.Name;
                    await With(s => s.AssignAsync(HR,item.MaDuAn,input));
                    item=await Get(item.MaDuAn);
                }
        }
        return item;
    }
    public EventInput Input()
    {
        var index = ++sequence;
        var start = HotelEventRules.Local(DateTimeOffset.UtcNow).Date.AddDays(index).AddHours(8);
        return new EventInput
        {
            Name = $"{Prefix}-event-{index}", Venue = $"{Prefix}-venue-{index}",
            Start = start, End = start.AddHours(2), ManagerId = HR.EmployeeId, ExpectedGuests = 40, Budget = 1000
        };
    }
    public async Task<DuAn> Create(EventInput input) => await Get(await With(s => s.SaveAsync(Admin, input)));
    public async Task<DuAn> Get(int id) => await With(s => s.GetAsync(Admin, id)) ?? throw new InvalidOperationException("Owned event missing.");
    public EventAssignmentInput Assignment(DuAn item, int worker, decimal allowance = 50) => new()
    {
        Version = item.Version, EmployeeId = worker,
        Role = HotelEventRoleRules.GetRoles(item.LoaiDuAn,RoleWorkers.FirstOrDefault(d=>d.Value.Contains(worker)).Key ?? "KY_THUAT",item.SoKhachDuKien).FirstOrDefault()?.Name ?? "Support",
        Start = item.NgayBatDau.ToOffset(TimeSpan.FromHours(7)).DateTime,
        End = item.NgayKetThuc.ToOffset(TimeSpan.FromHours(7)).DateTime, Allowance = allowance
    };
    public EventReviewInput Review(DuAn item, decimal actual = 50) => new()
    {
        Version = item.Version, AssignmentId = item.PhanCongs.Single().MaPhanCong, Rating = "HOAN_THANH", ActualAllowance = actual
    };
    public EventInput Edit(DuAn item) => new()
    {
        Id = item.MaDuAn, Version = item.Version, Name = item.TenDuAn, Type = item.LoaiDuAn,
        Venue = item.DiaDiem, Start = item.NgayBatDau.ToOffset(TimeSpan.FromHours(7)).DateTime,
        End = item.NgayKetThuc.ToOffset(TimeSpan.FromHours(7)).DateTime, ManagerId = item.MaNguoiQuanLy,
        ExpectedGuests = item.SoKhachDuKien, Budget = item.NganSachPhuCap, Description = item.MoTa
    };
    // Historical fixtures retain Manager ownership to cover access to events created under the old policy.
    public async Task SeedLegacyManager(int id, int employeeId)
    {
        await using var db = Open();
        var changed = await db.DuAns.Where(e => e.MaDuAn == id && e.TenDuAn.StartsWith(Prefix))
            .ExecuteUpdateAsync(p => p.SetProperty(e => e.MaNguoiQuanLy, employeeId));
        if (changed != 1) throw new InvalidOperationException("Expected one owned historical event.");
    }
    public async Task SeedStatus(int id, string status)
    {
        await using var db = Open();
        await db.DuAns.Where(e => e.MaDuAn == id && e.TenDuAn.StartsWith(Prefix)).ExecuteUpdateAsync(p => p.SetProperty(e => e.TrangThai, status));
    }
    public async Task MoveToOngoing(int id)
    {
        await using var db=Open();
        var item=await db.DuAns.Include(e=>e.PhanCongs).Include(e=>e.NhuCaus).SingleAsync(e=>e.MaDuAn==id && e.TenDuAn.StartsWith(Prefix));
        var delta=DateTimeOffset.UtcNow.AddHours(-1)-item.NgayBatDau;
        item.NgayBatDau+=delta; item.NgayKetThuc+=delta;
        foreach(var assignment in item.PhanCongs) { assignment.BatDau+=delta; assignment.KetThuc+=delta; }
        await db.SaveChangesAsync();
    }
    public async Task MoveToPast(int id)
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var owned = db.DuAns.Where(e => e.MaDuAn == id && e.TenDuAn.StartsWith(Prefix));
        var changed = await owned.ExecuteUpdateAsync(p => p
            .SetProperty(e => e.NgayBatDau, e => e.NgayBatDau.AddDays(-366))
            .SetProperty(e => e.NgayKetThuc, e => e.NgayKetThuc.AddDays(-366)));
        if (changed != 1) throw new InvalidOperationException("Expected one owned event to move into history.");
        await db.PhanCongDuAns.Where(p => p.MaDuAn == id && owned.Any()).ExecuteUpdateAsync(p => p
            .SetProperty(e => e.BatDau, e => e.BatDau.AddDays(-366))
            .SetProperty(e => e.KetThuc, e => e.KetThuc.AddDays(-366)));
        await transaction.CommitAsync();

    }
    public async Task SetContractEnd(int employee, DateOnly end)
    {
        await using var db = Open();
        var changed = await db.HopDongs.Where(h => h.MaNhanVien == employee && h.SoHopDong.StartsWith(Prefix)).ExecuteUpdateAsync(p => p.SetProperty(h => h.NgayKetThuc, end));
        if (changed != 1) throw new InvalidOperationException($"Expected one owned contract update, got {changed}.");
        var actual = await db.HopDongs.Where(h => h.MaNhanVien == employee && h.SoHopDong.StartsWith(Prefix)).Select(h => h.NgayKetThuc).SingleAsync();
        if (actual != end) throw new InvalidOperationException($"Owned contract end expected {end:yyyy-MM-dd}, stored {actual:yyyy-MM-dd}.");
    }
    public async Task<string> CoverageDiagnostic(int employee, DuAn item, EventAssignmentInput input)
    {
        await using var db = Open();
        var person = await db.NhanViens.AsNoTracking().Include(e => e.HopDongs)
            .SingleAsync(e => e.MaNhanVien == employee && e.EmailCongVu.StartsWith(Prefix + "-"));
        var contracts = string.Join(";", person.HopDongs.Select(h => $"{h.TrangThai}:{h.NgayBatDau:yyyy-MM-dd}..{h.NgayKetThuc:yyyy-MM-dd}"));
        return $"Coverage diagnostic: eventUTC={item.NgayBatDau:O}..{item.NgayKetThuc:O}; inputLocal={input.Start:O}..{input.End:O}; employee={person.TrangThai}:{person.NgayVaoLam:yyyy-MM-dd}..{person.NgayThoiViec:yyyy-MM-dd}; contracts={contracts}";
    }
    public async Task<DuAn> WaitingWithAssignment(decimal budget = 1000)
    {
        var input = Input();
        input.Budget = budget;
        var item = await Create(input);
        await With(s => s.AssignAsync(HR, item.MaDuAn, Assignment(item, WorkerA, Math.Min(50, budget))));
        await MoveToPast(item.MaDuAn);
        await SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
        return await Get(item.MaDuAn);
    }
    public async Task<DuAn> ReadyForAcceptance()
    {
        var item = await WaitingWithAssignment();
        await With(s => s.ReviewAsync(HR, item.MaDuAn, Review(item)));
        return await Get(item.MaDuAn);
    }
    public async Task Cleanup()
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var eventIds = await db.DuAns.Where(e => e.TenDuAn.StartsWith(Prefix)).Select(e => e.MaDuAn).ToArrayAsync();
        var subjects = eventIds.Select(id => $"SK-{id}").ToArray();
        var employeeIds = await db.NhanViens.Where(e => e.EmailCongVu.StartsWith(Prefix + "-")).Select(e => e.MaNhanVien).ToArrayAsync();
        var accountIds = await db.TaiKhoans.Where(a => employeeIds.Contains(a.MaNhanVien)).Select(a => a.MaTaiKhoan).ToArrayAsync();
        var pcIds = await db.PhanCongDuAns.Where(p => eventIds.Contains(p.MaDuAn) || employeeIds.Contains(p.MaNhanVien)).Select(p => p.MaPhanCong).ToArrayAsync();
        var plIds = await db.PhieuLuongs.Where(p => employeeIds.Contains(p.MaNhanVien)).Select(p => p.MaPhieuLuong).ToArrayAsync();

        await db.NhatKyQuanTris.Where(h => subjects.Contains(h.TenTaiKhoanBiTacDong)
            || (h.MaTaiKhoanThucHien.HasValue && accountIds.Contains(h.MaTaiKhoanThucHien.Value))).ExecuteDeleteAsync();
        await db.CongSuKiens.Where(c => pcIds.Contains(c.MaPhanCong) || employeeIds.Contains(c.MaNguoiXacNhan)).ExecuteDeleteAsync();
        await db.QuyetToanSuKiens.Where(q => eventIds.Contains(q.MaDuAn) || employeeIds.Contains(q.MaNhanVien)).ExecuteDeleteAsync();
        await db.ChiTietTruBaoHiems.Where(b => db.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == b.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietTruThueTncns.Where(t => db.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == t.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietTruViPhamTamUngs.Where(v => db.ChiTietPhieuLuongs.Any(c => plIds.Contains(c.MaPhieuLuong) && c.MaChiTiet == v.MaChiTiet)).ExecuteDeleteAsync();
        await db.ChiTietPhieuLuongs.Where(c => plIds.Contains(c.MaPhieuLuong)).ExecuteDeleteAsync();
        await db.PhieuLuongs.Where(p => employeeIds.Contains(p.MaNhanVien)).ExecuteDeleteAsync();
        await db.PhanCongDuAns.Where(a => eventIds.Contains(a.MaDuAn) || employeeIds.Contains(a.MaNhanVien)).ExecuteDeleteAsync();
        await db.NhuCauSuKiens.Where(n => eventIds.Contains(n.MaDuAn)).ExecuteDeleteAsync();
        await db.DuAns.Where(e => eventIds.Contains(e.MaDuAn)).ExecuteDeleteAsync();
        await db.DiaDiemSuKiens.Where(v => v.TenDiaDiem.StartsWith(Prefix)).ExecuteDeleteAsync();
        await db.ChamCongs.Where(c => db.PhanCas.Any(s => s.MaPhanCa == c.MaPhanCa && employeeIds.Contains(s.MaNhanVien))).ExecuteDeleteAsync();
        await db.PhanCas.Where(s => employeeIds.Contains(s.MaNhanVien)).ExecuteDeleteAsync();
        await db.CongBoLichTuans.Where(w => employeeIds.Contains(w.MaNguoiCongBo)).ExecuteDeleteAsync();
        await db.DinhBienCas.Where(d => db.CaLamViecs.Any(c => c.MaCa == d.MaCa && c.TenCa.StartsWith(Prefix))).ExecuteDeleteAsync();
        await db.CaLamViecs.Where(c => c.TenCa.StartsWith(Prefix)).ExecuteDeleteAsync();
        await db.DonTus.Where(d => employeeIds.Contains(d.MaNhanVien)).ExecuteDeleteAsync();
        await db.LoaiDons.Where(d => d.TenLoaiDon.StartsWith(Prefix)).ExecuteDeleteAsync();
        await db.HopDongs.Where(h => employeeIds.Contains(h.MaNhanVien)).ExecuteDeleteAsync();
        await db.TaiKhoans.Where(a => accountIds.Contains(a.MaTaiKhoan)).ExecuteDeleteAsync();
        await db.NhanViens.Where(e => employeeIds.Contains(e.MaNhanVien)).ExecuteDeleteAsync();
        await db.PhongBans.Where(p => p.TenPhongBan.StartsWith(Prefix)).ExecuteDeleteAsync();
        await db.CaLamViecs.Where(c => c.TenCa.StartsWith(Prefix)).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}




