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
    var sqlPath = Path.Combine(RepositoryRoot(), "src", "OvertimeHotel.HRM.Data", "migrations", "20261016_remove_maintenance_events.sql");
    var sql = await File.ReadAllTextAsync(sqlPath);
    await using var conn = new NpgsqlConnection(ReadConnection());
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("PASS migration 20261016_remove_maintenance_events.sql applied successfully");
    return 0;
}
async Task Check(string name, Func<Task> run)
{
    if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
    try { await run(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
}

try
{
    var previewPassword = args.Contains("--preview-fixture") ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) : null;
    fixture = new Fixture(ReadConnection(), password: previewPassword);
    var f = fixture;
    await f.Initialize();
    if (previewPassword is not null)
    {
        var pastEvent = await f.Create(f.Input());
        await f.MoveToPast(pastEvent.MaDuAn);
        pastEvent = await f.Get(pastEvent.MaDuAn);
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
        var preview = new
        {
            f.Prefix, Password = previewPassword,
            PastEventId = pastEvent.MaDuAn, PastEventDate = HotelEventRules.Local(pastEvent.NgayBatDau).ToString("yyyy-MM-dd"),
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
        Require(item.MaNguoiQuanLy == f.Manager.EmployeeId && item.Version > 0, "Manager or version missing.");
        var history = await f.With(s => s.HistoryAsync(item.MaDuAn));
        Require(history.Any(h => h.TenTaiKhoanBiTacDong == $"SK-{item.MaDuAn}"), "Create audit was not committed.");
    });

    await Check("Historical events remain readable after creation dates are restricted", async () =>
    {
        var item = await f.Create(f.Input());
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

    await Check("Normalized venue overlaps are rejected and adjacency is allowed", async () =>
    {
        var original = f.Input();
        await f.Create(original);
        var overlap = f.Input();
        overlap.Venue = $"  {original.Venue.ToLowerInvariant()}  ";
        overlap.Start = original.Start.AddMinutes(30);
        overlap.End = original.End;
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, overlap)));
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
        input.ManagerId = f.OtherManager.EmployeeId;
        var item = await f.Create(input);
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
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA, 125.50m)));
        var row = (await f.Get(item.MaDuAn)).PhanCongs.Single();
        Require(row.MaNhanVien == f.WorkerA && row.PhuCapDuKien == 125.50m, "Assignment was not stored.");
        Require(row.SoTienPhuCap == 0 && row.MaChiTiet is null && row.TrangThaiChiTra == "CHUA_CHI_TRA", "Assignment created payment prematurely.");
    });

    await Check("Duplicate employee assignment is rejected", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 1, "Duplicate row persisted.");
    });

    await Check("Inactive and uncovered employees cannot be assigned", async () =>
    {
        var item = await f.Create(f.Input());
        foreach (var worker in new[] { f.InactiveWorker, f.ExpiredWorker })
            await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, worker))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 0, "Invalid staffing persisted.");
    });

    await Check("Contract must cover the last participation day", async () =>
    {
        var input = f.Input();
        input.End = input.Start.AddDays(1);
        var item = await f.Create(input);
        await f.SetContractEnd(f.BoundaryWorker, DateOnly.FromDateTime(input.Start));
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.BoundaryWorker))));
    });

    await Check("Midnight participation accepts a contract ending on the previous day", async () =>
    {
        var input = f.Input();
        input.End = input.Start.Date.AddDays(1);
        var item = await f.Create(input);
        await f.SetContractEnd(f.BoundaryWorker, DateOnly.FromDateTime(input.Start));
        var assignment = f.Assignment(item, f.BoundaryWorker);
        try { await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, assignment)); }
        catch (EventOperationException)
        {
            Console.Error.WriteLine(await f.CoverageDiagnostic(f.BoundaryWorker, item, assignment));
            throw;
        }
    });

    await Check("Employee cannot overlap across different event venues", async () =>
    {
        var input = f.Input();
        var a = await f.Create(input);
        var other = f.Input();
        other.Start = input.Start;
        other.End = input.End;
        var b = await f.Create(other);
        await f.With(s => s.AssignAsync(f.Manager, a.MaDuAn, f.Assignment(a, f.WorkerA)));
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, b.MaDuAn, f.Assignment(b, f.WorkerA))));
    });

    await Check("Planned allowance cannot exceed event budget", async () =>
    {
        var input = f.Input();
        input.Budget = 100;
        var item = await f.Create(input);
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA, 60)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerB, 40.01m))));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Count == 1, "Over-budget assignment persisted.");
    });

    await Check("Review requires waiting state and event end", async () =>
    {
        var item = await f.Create(f.Input());
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA)));
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ReviewAsync(f.Manager, item.MaDuAn, f.Review(item))));
        var future = f.Input();
        future.Start = DateTime.Today.AddDays(5).AddHours(8);
        future.End = future.Start.AddHours(2);
        var upcoming = await f.Create(future);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Manager, upcoming.MaDuAn, upcoming.Version, "DANG_DIEN_RA")));
        await f.With(s => s.AssignAsync(f.Manager, upcoming.MaDuAn, f.Assignment(upcoming, f.WorkerB)));
        await f.SeedStatus(upcoming.MaDuAn, "CHO_NGHIEM_THU");
        upcoming = await f.Get(upcoming.MaDuAn);
        await Reject(() => f.With(s => s.ReviewAsync(f.Manager, upcoming.MaDuAn, f.Review(upcoming))));
    });

    await Check("Lifecycle reaches waiting state only after the event ends", async () =>
    {
        var item = await f.Create(f.Input());
        await f.StaffFully(item);
        item = await f.Get(item.MaDuAn);
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "CHO_NGHIEM_THU"));
        Require((await f.Get(item.MaDuAn)).TrangThai == "CHO_NGHIEM_THU", "Lifecycle did not persist.");
    });

    await Check("Actual allowance and absent payment are validated", async () =>
    {
        var item = await f.WaitingWithAssignment(100);
        var review = f.Review(item, 100.01m);
        await Reject(() => f.With(s => s.ReviewAsync(f.Manager, item.MaDuAn, review)));
        review.ActualAllowance = -1;
        await Reject(() => f.With(s => s.ReviewAsync(f.Manager, item.MaDuAn, review)));
        review.ActualAllowance = 1;
        review.Rating = "KHONG_THAM_GIA";
        await Reject(() => f.With(s => s.ReviewAsync(f.Manager, item.MaDuAn, review)));
        Require((await f.Get(item.MaDuAn)).PhanCongs.Single().DanhGia == "CHUA_DANH_GIA", "Invalid review persisted.");
    });

    await Check("Acceptance rejects missing reviews", async () =>
    {
        var item = await f.WaitingWithAssignment();
        await Reject(() => f.With(s => s.AcceptAsync(f.Admin, item.MaDuAn, item.Version)));
        Require((await f.Get(item.MaDuAn)).TrangThai == "CHO_NGHIEM_THU", "Unreviewed event accepted.");
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

    await Check("Cancellation requires a reason and releases venue bookings", async () =>
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

    await Check("Concurrent venue creates have exactly one winner", async () =>
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
        Require(result.Count(x => x) == 1, "Venue race did not produce exactly one winner.");
        await using var db = f.Open();
        Require(await db.DuAns.CountAsync(e => e.TenDuAn == first.Name || e.TenDuAn == second.Name) == 1, "Venue race persisted conflicting records.");
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
            return await Succeeds(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerA))));
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
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, input)), field: "Assignment.Role");
        input.Role = "Role forged by client";
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, input)), field: "Assignment.Role");
    });

    await Check("Custom roles retain their prefix and new assignments have no task", async () =>
    {
        var item = await f.Create(f.Input());
        var input = f.Assignment(item, f.WorkerA);
        input.Role = "__OTHER__";
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, input)), field: "Assignment.Role");
        input.OtherRole = "Custom support";
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, input));
        var stored = (await f.Get(item.MaDuAn)).PhanCongs.Single();
        Require(stored.VaiTroDuAn == "Khác: Custom support" && stored.NhiemVuCuThe is null, "Custom role or empty task was not preserved.");
    });

    await Check("A primary-only team cannot start despite enough employees per department", async () =>
    {
        var item = await f.Create(f.Input());
        foreach (var dept in HotelEventRules.GetRequiredDepartments(item.LoaiDuAn))
            foreach (var worker in f.RoleWorkers[dept].Take(2))
            {
                await f.With(s => s.AssignAsync(f.Manager,item.MaDuAn,f.Assignment(item,worker)));
                item = await f.Get(item.MaDuAn);
            }
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Manager,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("A zero-guest event cannot start with an otherwise complete team", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var edit = f.Edit(item); edit.ExpectedGuests = 0;
        await f.With(s => s.SaveAsync(f.Admin,edit));
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await Reject(() => f.With(s => s.ChangeStatusAsync(f.Manager,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("Changing guests keeps existing assignments but increases role demand", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var oldCount=item.PhanCongs.Count;
        var edit=f.Edit(item); edit.ExpectedGuests=120;
        await f.With(s=>s.SaveAsync(f.Admin,edit));
        await f.MoveToPast(item.MaDuAn);
        item=await f.Get(item.MaDuAn);
        Require(item.PhanCongs.Count==oldCount,"Changing guests silently removed assignments.");
        await Reject(()=>f.With(s=>s.ChangeStatusAsync(f.Manager,item.MaDuAn,item.Version,"DANG_DIEN_RA")));
    });

    await Check("Staffing rejects employees belonging to disallowed departments", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE
        await Reject(() => f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerUnrelated))), field: "Assignment.EmployeeId");
    });

    await Check("Starting an event is blocked when any mandatory department is missing", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE requires: TIEN_SANH, AM_THUC, BEP, KY_THUAT, IT, AN_NINH
        // Assign 5 departments, leaving out AN_NINH
        foreach (var worker in new[] { f.WorkerFO, f.WorkerFB, f.WorkerKitchen, f.WorkerA, f.WorkerIT })
        {
            await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, worker)));
            item = await f.Get(item.MaDuAn);
        }

        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        try
        {
            await f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
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
        await f.MoveToPast(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);
        Require(item.TrangThai == "DANG_DIEN_RA", "Event failed to transition to DANG_DIEN_RA after full staffing.");
    });

    await Check("Removing a mandatory role is blocked even when another role has extra people", async () =>
    {
        var item = await f.Create(f.Input());
        item = await f.StaffFully(item);
        var spare = f.RoleWorkers["KY_THUAT"][2];
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, spare)));
        await f.MoveToOngoing(item.MaDuAn);
        item = await f.Get(item.MaDuAn);
        await f.With(s => s.ChangeStatusAsync(f.Manager, item.MaDuAn, item.Version, "DANG_DIEN_RA"));
        item = await f.Get(item.MaDuAn);

        var assignB = item.PhanCongs.Single(p => p.MaNhanVien == spare);
        await f.With(s => s.RemoveAssignmentAsync(f.Manager, item.MaDuAn, assignB.MaPhanCong, item.Version));
        item = await f.Get(item.MaDuAn);

        // WorkerB still covers support, but cannot substitute for WorkerA's primary role.
        var assignA = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerA);
        await Reject(() => f.With(s => s.RemoveAssignmentAsync(f.Manager, item.MaDuAn, assignA.MaPhanCong, item.Version)));
    });

    await Check("Changing event type is rejected if assigned staff are incompatible with new type", async () =>
    {
        var item = await f.Create(f.Input()); // HOI_NGHI_MICE allows IT
        await f.With(s => s.AssignAsync(f.Manager, item.MaDuAn, f.Assignment(item, f.WorkerIT)));
        item = await f.Get(item.MaDuAn);

        var edit = f.Edit(item);
        edit.Type = "TIEC_CUOI"; // TIEC_CUOI does NOT allow IT
        await Reject(() => f.With(s => s.SaveAsync(f.Admin, edit)), field: "Input.Type");

        // After removing WorkerIT, changing to TIEC_CUOI succeeds
        var assignIT = item.PhanCongs.Single(p => p.MaNhanVien == f.WorkerIT);
        await f.With(s => s.RemoveAssignmentAsync(f.Manager, item.MaDuAn, assignIT.MaPhanCong, item.Version));
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
    Console.Error.WriteLine($"FAIL fixture setup: {ex.GetType().Name}. Check PostgreSQL access, roles and the applied event migration.");
    var provider = ex as PostgresException ?? ex.InnerException as PostgresException;
    if (provider is not null)
        Console.Error.WriteLine($"PostgreSQL: code={provider.SqlState}, table={provider.TableName}, constraint={provider.ConstraintName}, message={provider.MessageText}");
}
finally
{
    if (fixture is not null && !keepFixture)
    {
        try { await fixture.Cleanup(); Console.WriteLine("PASS owned fixture cleanup"); }
        catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL owned fixture cleanup: {ex.GetType().Name}; run prefix {fixture.Prefix}"); }
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
    return !string.IsNullOrWhiteSpace(connection) ? connection : throw new InvalidOperationException("No PostgreSQL connection configured.");
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
                    await With(s => s.AssignAsync(Manager,item.MaDuAn,input));
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
            Start = start, End = start.AddHours(2), ManagerId = Manager.EmployeeId, ExpectedGuests = 40, Budget = 1000
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
    public async Task SeedStatus(int id, string status)
    {
        await using var db = Open();
        await db.DuAns.Where(e => e.MaDuAn == id && e.TenDuAn.StartsWith(Prefix)).ExecuteUpdateAsync(p => p.SetProperty(e => e.TrangThai, status));
    }
    public async Task MoveToOngoing(int id)
    {
        await using var db=Open();
        var item=await db.DuAns.Include(e=>e.PhanCongs).SingleAsync(e=>e.MaDuAn==id && e.TenDuAn.StartsWith(Prefix));
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
        await With(s => s.AssignAsync(Manager, item.MaDuAn, Assignment(item, WorkerA, Math.Min(50, budget))));
        await MoveToPast(item.MaDuAn);
        await SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
        return await Get(item.MaDuAn);
    }
    public async Task<DuAn> ReadyForAcceptance()
    {
        var item = await WaitingWithAssignment();
        await With(s => s.ReviewAsync(Manager, item.MaDuAn, Review(item)));
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
        await db.NhatKyQuanTris.Where(h => subjects.Contains(h.TenTaiKhoanBiTacDong)
            || (h.MaTaiKhoanThucHien.HasValue && accountIds.Contains(h.MaTaiKhoanThucHien.Value))).ExecuteDeleteAsync();
        await db.PhanCongDuAns.Where(a => eventIds.Contains(a.MaDuAn)).ExecuteDeleteAsync();
        await db.DuAns.Where(e => eventIds.Contains(e.MaDuAn)).ExecuteDeleteAsync();
        await db.HopDongs.Where(h => employeeIds.Contains(h.MaNhanVien)).ExecuteDeleteAsync();
        await db.TaiKhoans.Where(a => accountIds.Contains(a.MaTaiKhoan)).ExecuteDeleteAsync();
        await db.NhanViens.Where(e => employeeIds.Contains(e.MaNhanVien)).ExecuteDeleteAsync();
        await db.PhongBans.Where(p => p.TenPhongBan.StartsWith(Prefix)).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}
