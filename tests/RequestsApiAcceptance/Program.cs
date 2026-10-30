using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OvertimeHotel.HRM.WebAdmin.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using Npgsql;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.WebAdmin.Controllers.Api;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Security;

var socket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/hrm-isolated-check");
var dbName = "hrm_requests_api_" + Guid.NewGuid().ToString("N")[..12];
var admin = new NpgsqlConnectionStringBuilder { Host = socket, Port = 55448, Database = "postgres", Username = Environment.UserName }.ConnectionString;
var target = new NpgsqlConnectionStringBuilder(admin) { Database = dbName }.ConnectionString;
await using var connection = new NpgsqlConnection(admin);
await connection.OpenAsync();
await new NpgsqlCommand($"CREATE DATABASE {dbName}", connection).ExecuteNonQueryAsync();
try
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(target).Options;
    await using var db = new AppDbContext(options);
    await db.Database.EnsureCreatedAsync();
    db.PhongBans.Add(new() { MaPhongBan = 701, TenPhongBan = "Acceptance" });
    db.VaiTros.Add(new() { MaVaiTro = 701, TenVaiTro = "Employee" });
    db.NhanViens.Add(new() { MaNhanVien = 7101, MaPhongBan = 701, Ho = "Local", Ten = "Test", EmailCongVu = "local@example.invalid", DienThoai = "7101", NgayVaoLam = new(2020,1,1) });
    db.TaiKhoans.Add(new() { MaTaiKhoan = 8101, MaNhanVien = 7101, MaVaiTro = 701, TenDangNhap = "local_test", MatKhauBam = "local", TrangThai = true });
    await db.SaveChangesAsync();
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SupabaseClaims.MaNhanVienClaim, "7101"), new Claim("sub", Guid.NewGuid().ToString())], "test"));
    var controller = new ShiftSwapsApiController(db, NullLogger<ShiftSwapsApiController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
    var result = await controller.GetEligibleMyShifts();
    if (result is not OkObjectResult) throw new Exception($"Authenticated Supabase employee received {result.GetType().Name}, expected OK.");
    Console.WriteLine("PASS Supabase employee claim resolves the active account");
    foreach (var id in new[] {7102, 7103})
    {
        db.NhanViens.Add(new() { MaNhanVien=id, MaPhongBan=701, Ho="Local", Ten=$"Test{id}", EmailCongVu=$"local{id}@example.invalid", DienThoai=id.ToString(), NgayVaoLam=new(2020,1,1) });
        db.TaiKhoans.Add(new() { MaTaiKhoan=id+1000, MaNhanVien=id, MaVaiTro=701, TenDangNhap=$"local_{id}", MatKhauBam="local", TrangThai=true });
    }
    db.CaLamViecs.Add(new() {MaCa=701, TenCa="Local", GioBatDau=new(8,0), GioKetThuc=new(16,0)});
    var start=DateTimeOffset.UtcNow.AddDays(3);
    db.PhanCas.Add(new() {MaPhanCa=701, MaNhanVien=7101, MaCa=701, NgayLamViec=DateOnly.FromDateTime(start.UtcDateTime), BatDauDuKien=start, KetThucDuKien=start.AddHours(8)});
    db.PhanCas.Add(new() {MaPhanCa=702, MaNhanVien=7102, MaCa=701, NgayLamViec=DateOnly.FromDateTime(start.AddDays(2).UtcDateTime), BatDauDuKien=start.AddDays(2), KetThucDuKien=start.AddDays(2).AddHours(8)});
    await db.SaveChangesAsync();
    ShiftSwapsApiController Api(AppDbContext context, int employee) => new(context, NullLogger<ShiftSwapsApiController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SupabaseClaims.MaNhanVienClaim, employee.ToString())], "test")) } } };
    int Status(IActionResult action) => action is ObjectResult o ? o.StatusCode ?? 200 : action is StatusCodeResult r ? r.StatusCode : action is OkResult ? 200 : 0;
    void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    async Task<(int status,int id)> Create()
    {
        await using var ctx=new AppDbContext(options);
        var action=await Api(ctx,7101).CreateShiftSwap(new CreateShiftSwapRequest { MaPhanCaGui=701, MaPhanCaNhan=702, LyDo="Thử đổi" });
        return (Status(action), (action as OkObjectResult)?.Value is ShiftSwapOperationResult value ? value.MaYeuCau ?? 0 : 0);
    }
    async Task<int> Respond(int id,int employee=7102,bool accept=true,string? reason=null)
    {
        await using var ctx=new AppDbContext(options);
        return Status(await Api(ctx,employee).RespondShiftSwap(id,new RespondShiftSwapRequest { DongY=accept, LyDo=reason }));
    }
    async Task<int> Withdraw(int id,int employee=7101)
    {
        await using var ctx=new AppDbContext(options);
        return Status(await Api(ctx,employee).WithdrawShiftSwap(id));
    }
    var first=await Create();
    Require(first.status==200 && first.id>0,"Could not create the first swap request");
    using var services=new ServiceCollection().AddMemoryCache().AddSingleton<SearchListCache>().BuildServiceProvider();
    async Task<YeuCauDoiCaItemViewModel> ManagerView(int id)
    {
        await using var ctx=new AppDbContext(options);
        var http=new DefaultHttpContext { RequestServices=services, User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"Admin")],"test")) };
        var mvc=new YeuCauDoiCaController(ctx,NullLogger<YeuCauDoiCaController>.Instance) {ControllerContext=new ControllerContext {HttpContext=http}, TempData = new TempDataDictionary(http,new TestTempDataProvider())};
        var view=await mvc.Index(null,null) as ViewResult;
        return ((YeuCauDoiCaIndexViewModel)view!.Model!).Items.Single(x=>x.MaYeuCau==id);
    }
    Require((await ManagerView(first.id)).TrangThai=="CHO_NGUOI_NHAN", "TC-10 initial WebAdmin status is wrong");
    Require(await Respond(first.id)==200, "TC-01 receiver accept failed");
    Require((await ManagerView(first.id)).TrangThai=="CHO_DUYET", "TC-10 WebAdmin still shows stale status after another instance responds");
    await using(var ctx=new AppDbContext(options))
    {
        var resultList=await Api(ctx,7102).GetMyShiftSwaps() as OkObjectResult;
        Require(resultList?.Value is List<ShiftSwapItemDto> list && list.Any(x=>x.MaYeuCau==first.id && x.TrangThai=="CHO_DUYET"), "TC-10 mobile list does not show updated state");
    }
    Console.WriteLine("PASS TC-10 WebAdmin sees response from another instance");
    Require(await Withdraw(first.id)==200,"TC-01 withdrawal after accept failed");
    await using(var check=new AppDbContext(options)) Require((await check.YeuCauDoiCas.SingleAsync(x=>x.MaYeuCau==first.id)).TrangThai=="DA_HUY","TC-01 wrong final status");
    Console.WriteLine("PASS TC-01 accept followed by withdrawal");
    var second=await Create();
    Require(second.status==200 && await Withdraw(second.id)==200 && await Respond(second.id)==409,"TC-02 expected conflict after withdrawal");
    Console.WriteLine("PASS TC-02 withdraw followed by accept returns 409");
    var third=await Create();
    Require(third.status==200, "Could not create request for authorization test");
    Require(await Respond(third.id,7103)==403 && await Withdraw(third.id,7103)==403,"TC-06 unauthorized third party not forbidden");
    await using(var ctx=new AppDbContext(options))
    {
        Require(Status(await Api(ctx,7103).GetShiftSwapDetail(third.id))==403, "TC-06 unauthorized third-party detail not forbidden");
        Require(Status(await Api(ctx,7103).GetShiftSwapDetail(2147483647))==404, "TC-06 nonexistent request not found");
    }
    Require(await Respond(third.id,7102,false,"   ")==400,"TC-07 blank reason was not rejected");
    Console.WriteLine("PASS TC-06/07 third-party actions and blank refusal rejected");
    var duplicate=await Create();
    Require(duplicate.status!=200,"TC-05 duplicate shift accepted");
    Console.WriteLine("PASS TC-05 duplicate request rejected sequentially");
    Require(await Withdraw(third.id)==200,"Fixture withdrawal failed");
    var concurrent=await Task.WhenAll(Create(),Create());
    Require(concurrent.Count(x=>x.status==200)==1,"TC-05 concurrent creation did not admit exactly one request");
    Console.WriteLine("PASS TC-05 concurrent requests for same shift");
    var pendingId=concurrent.Single(x=>x.status==200).id;
    Require(await Respond(pendingId)==200,"Could not prepare manager approval");
    async Task<int> Approve(int id)
    {
        await using var ctx=new AppDbContext(options);
        var http=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"8103"),new Claim("EmployeeId","7103"),new Claim(ClaimTypes.Role,"Admin")],"test")) };
        http.Request.Headers.XRequestedWith="XMLHttpRequest";
        var adminController=new YeuCauDoiCaController(ctx,NullLogger<YeuCauDoiCaController>.Instance) {ControllerContext=new ControllerContext { HttpContext=http }};
        await adminController.Approve(id,null);
        return http.Response.StatusCode;
    }
    await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_second_swap() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.ma_phan_ca=702 THEN RAISE EXCEPTION 'forced acceptance failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER fail_second_swap BEFORE UPDATE ON phan_ca FOR EACH ROW EXECUTE FUNCTION fail_second_swap();");
    Require(await Approve(pendingId)==400,"TC-04 injected failure did not reject approval");
    await using(var check=new AppDbContext(options))
    {
        Require((await check.YeuCauDoiCas.SingleAsync(x=>x.MaYeuCau==pendingId)).TrangThai=="CHO_DUYET", "TC-04 request status was not rolled back");
        Require((await check.PhanCas.SingleAsync(x=>x.MaPhanCa==701)).MaNhanVien==7101 && (await check.PhanCas.SingleAsync(x=>x.MaPhanCa==702)).MaNhanVien==7102, "TC-04 shift ownership was not rolled back");
    }
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_second_swap ON phan_ca; DROP FUNCTION fail_second_swap();");
    Console.WriteLine("PASS TC-04 forced second shift failure rolls back the whole transaction");
    var race=await Task.WhenAll(Approve(pendingId),Withdraw(pendingId));
    await using(var check=new AppDbContext(options))
    {
        var status=(await check.YeuCauDoiCas.SingleAsync(x=>x.MaYeuCau==pendingId)).TrangThai;
        var sender=(await check.PhanCas.SingleAsync(x=>x.MaPhanCa==701)).MaNhanVien;
        var receiver=(await check.PhanCas.SingleAsync(x=>x.MaPhanCa==702)).MaNhanVien;
        Require((status=="DA_DUYET" && race[0]==200 && race[1]==409 && sender==7102 && receiver==7101) || (status=="DA_HUY" && race[0]==400 && race[1]==200 && sender==7101 && receiver==7102), "TC-03 inconsistent outcome between approval and withdrawal");
    }
    Console.WriteLine("PASS TC-03 simultaneous manager approval and withdrawal");
    await db.Database.ExecuteSqlRawAsync("UPDATE phan_ca SET ma_nhan_vien=CASE ma_phan_ca WHEN 701 THEN 7101 ELSE 7102 END WHERE ma_phan_ca IN (701,702)");
    async Task<bool> CreateManager()
    {
        await using var ctx=new AppDbContext(options);
        var http=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"Admin")],"test")) };
        var mvc=new YeuCauDoiCaController(ctx,NullLogger<YeuCauDoiCaController>.Instance) { ControllerContext=new ControllerContext {HttpContext=http}, TempData=new TempDataDictionary(http,new TestTempDataProvider()) };
        return await mvc.Create(new YeuCauDoiCaCreateViewModel {MaPhanCaGui=701,MaPhanCaNhan=702,LyDo="Manager test"}) is RedirectToActionResult;
    }
    var managerAttempt=CreateManager();
    var mobileAttempt=Create();
    await Task.WhenAll(managerAttempt,mobileAttempt);
    Require((managerAttempt.Result ? 1 : 0) + (mobileAttempt.Result.status==200 ? 1 : 0)==1, "TC-05 manager and mobile created duplicate open requests");
    Console.WriteLine("PASS TC-05 concurrent WebAdmin and mobile creation");
    await db.Database.ExecuteSqlRawAsync("UPDATE yeu_cau_doi_ca SET trang_thai='DA_HUY' WHERE trang_thai='CHO_NGUOI_NHAN'");
    db.ChangeTracker.Clear();
    var previous=new OvertimeHotel.HRM.Core.Models.YeuCauDoiCa {MaPhanCaGui=701,MaPhanCaNhan=702,MaNhanVienGui=7101,MaNhanVienNhan=7102,LyDo="Đơn cũ",TrangThai="CHO_DUYET"};
    db.YeuCauDoiCas.Add(previous);
    await db.SaveChangesAsync();
    Require(await Approve(previous.MaYeuCau)==200,"TC-09 historical CHO_DUYET request could not be approved");
    Console.WriteLine("PASS TC-09 legacy waiting request remains approvable");
    var explanation=new OvertimeHotel.HRM.Core.Models.GiaiTrinhChamCong {MaPhanCa=701,MaNhanVien=7101,LyDo="Giải trình",GioDeXuatVao=DateTimeOffset.UtcNow.AddHours(-3),TrangThai="CHO_HR"};
    db.GiaiTrinhChamCongs.Add(explanation);
    await db.SaveChangesAsync();
    await using(var ctx=new AppDbContext(options))
    {
        var http=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(SupabaseClaims.MaNhanVienClaim,"7101")],"test"))};
        var expController=new AttendanceExplanationsApiController(ctx,NullLogger<AttendanceExplanationsApiController>.Instance) {ControllerContext=new ControllerContext {HttpContext=http}};
        Require(Status(await expController.WithdrawExplanation(explanation.MaGiaiTrinh))==409,"TC-08 HR-pending explanation could be withdrawn");
    }
    Console.WriteLine("PASS TC-08 HR-pending explanation cannot be withdrawn");
    var previousShift=DateTimeOffset.UtcNow.AddDays(-2);
    db.PhanCas.Add(new() {MaPhanCa=703,MaNhanVien=7101,MaCa=701,NgayLamViec=DateOnly.FromDateTime(previousShift.UtcDateTime),BatDauDuKien=previousShift.AddHours(-8),KetThucDuKien=previousShift});
    await db.SaveChangesAsync();
    await using(var ctx=new AppDbContext(options))
    {
        var http=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(SupabaseClaims.MaNhanVienClaim,"7101")],"test"))};
        var api=new AttendanceExplanationsApiController(ctx,NullLogger<AttendanceExplanationsApiController>.Instance) {ControllerContext=new ControllerContext {HttpContext=http}};
        var proposed=previousShift.AddHours(-8).ToOffset(TimeSpan.FromHours(7));
        var action=await api.CreateExplanation(new CreateAttendanceExplanationRequest(703,proposed,null,"Giờ từ điện thoại",null));
        Require(Status(action)==200,"UTC offset explanation was not accepted");
        var saved=await ctx.GiaiTrinhChamCongs.AsNoTracking().SingleAsync(x=>x.MaPhanCa==703);
        Require(saved.GioDeXuatVao.HasValue && saved.GioDeXuatVao.Value.Offset == TimeSpan.Zero, "Saved time must be UTC (offset 0)");
        Require(saved.GioDeXuatVao.HasValue && Math.Abs((saved.GioDeXuatVao.Value - proposed.ToUniversalTime()).TotalMilliseconds) < 1, "Proposed time changed while converting to UTC");
    }
    Console.WriteLine("PASS explanation with +07 offset is stored as the same UTC instant");

    // Test Luồng Duyệt và Rút Đơn Nghỉ Phép (LeaveRequestsController)
    db.LoaiDons.Add(new() { MaLoaiDon = 1, TenLoaiDon = "Nghỉ phép năm", CoHuongLuong = true });
    db.VaiTros.Add(new() { MaVaiTro = 702, TenVaiTro = "Manager" });
    var tk7103 = await db.TaiKhoans.SingleAsync(t => t.MaNhanVien == 7103);
    tk7103.MaVaiTro = 702;
    var leave1 = new OvertimeHotel.HRM.Core.Models.DonTu
    {
        MaDon = 901,
        MaNhanVien = 7101,
        MaLoaiDon = 1,
        TuNgay = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
        DenNgay = DateOnly.FromDateTime(DateTime.Today.AddDays(6)),
        LyDo = "Nghỉ phép việc riêng",
        TrangThai = "CHO_DUYET",
        NgayGui = DateTimeOffset.UtcNow
    };
    db.DonTus.Add(leave1);
    await db.SaveChangesAsync();

    LeaveRequestsController LeaveApi(AppDbContext context, int employee) =>
        new(context, NullLogger<LeaveRequestsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SupabaseClaims.MaNhanVienClaim, employee.ToString())], "test"))
                }
            }
        };

    await using (var ctx = new AppDbContext(options))
    {
        // 1. Employee cố tình duyệt đơn của chính mình -> 403 Forbidden
        var empReview = await LeaveApi(ctx, 7101).ReviewLeaveRequest(901, new LeaveReviewRequest(true, "Tự duyệt"));
        Require(Status(empReview) == 403, "Employee was able to call review leave request endpoint");

        // 2. Manager duyệt đơn -> 200 OK
        var mgrReview = await LeaveApi(ctx, 7103).ReviewLeaveRequest(901, new LeaveReviewRequest(true, "Đồng ý cho nghỉ"));
        Require(Status(mgrReview) == 200, "Manager could not approve leave request");

        var approvedLeave = await ctx.DonTus.AsNoTracking().SingleAsync(d => d.MaDon == 901);
        Require(approvedLeave.TrangThai == "DA_DUYET" && approvedLeave.MaNguoiDuyet == 7103, "Leave request not approved in DB");

        // 3. Duyệt lại đơn đã xử lý -> 409 Conflict
        var secondReview = await LeaveApi(ctx, 7103).ReviewLeaveRequest(901, new LeaveReviewRequest(true, "Duyệt lại"));
        Require(Status(secondReview) == 409, "Second review was not rejected with 409 Conflict");

        // 4. Chủ đơn cố rút đơn đã duyệt -> 409 Conflict
        var withdrawApproved = await LeaveApi(ctx, 7101).WithdrawLeaveRequest(901);
        Require(Status(withdrawApproved) == 409, "Approved leave was withdrawable");
    }

    var leave2 = new OvertimeHotel.HRM.Core.Models.DonTu
    {
        MaDon = 902,
        MaNhanVien = 7101,
        MaLoaiDon = 1,
        TuNgay = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
        DenNgay = DateOnly.FromDateTime(DateTime.Today.AddDays(12)),
        LyDo = "Nghỉ phép rút thử",
        TrangThai = "CHO_DUYET",
        NgayGui = DateTimeOffset.UtcNow
    };
    db.DonTus.Add(leave2);
    await db.SaveChangesAsync();

    await using (var ctx = new AppDbContext(options))
    {
        // 5. Người khác cố rút đơn -> 403 Forbidden
        var thirdPartyWithdraw = await LeaveApi(ctx, 7102).WithdrawLeaveRequest(902);
        Require(Status(thirdPartyWithdraw) == 403, "Third party was able to withdraw leave request");

        // 6. Chủ đơn rút đơn khi CHO_DUYET -> 200 OK
        var ownerWithdraw = await LeaveApi(ctx, 7101).WithdrawLeaveRequest(902);
        Require(Status(ownerWithdraw) == 200, "Owner could not withdraw CHO_DUYET leave request");

        var withdrawnLeave = await ctx.DonTus.AsNoTracking().SingleAsync(d => d.MaDon == 902);
        Require(withdrawnLeave.TrangThai == "DA_HUY", "Leave status not updated to DA_HUY");

        // 7. Rút lại đơn đã rút -> 409 Conflict
        var withdrawAgain = await LeaveApi(ctx, 7101).WithdrawLeaveRequest(902);
        Require(Status(withdrawAgain) == 409, "Withdrawing already withdrawn leave did not return 409 Conflict");
    }
    Console.WriteLine("PASS leave request server-side review and withdrawal permissions verified");




}
finally
{
    NpgsqlConnection.ClearAllPools();
    await new NpgsqlCommand($"DROP DATABASE {dbName} WITH (FORCE)", connection).ExecuteNonQueryAsync();
}

sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string,object> LoadTempData(HttpContext context) => new Dictionary<string,object>();
    public void SaveTempData(HttpContext context, IDictionary<string,object> values) { }
}
