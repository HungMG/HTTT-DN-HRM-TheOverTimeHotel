using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using OvertimeHotel.HRM.WebAdmin.Controllers;
using OvertimeHotel.HRM.WebAdmin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;

static partial class HourlyOperationsTests
{
    public static async Task RunPhase3(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Phase 3 payroll absence uses a supported deduction code without blocking settlement transfer", async () =>
        {
            var (item, work)=await WorkFixture(f);
            var month=HourlyEventRules.WorkMonth(item.NgayBatDau);
            var period=await Period(f,month);
            await using var db=f.Open();
            var shiftId=item.PhanCongs.Single().MaPhanCa;
            await db.ChamCongs.Where(c=>c.MaPhanCa==shiftId).ExecuteUpdateAsync(c=>c.SetProperty(p=>p.TrangThai,"VANG_MAT"));
            await new PayrollCalculationService(db).CalculatePayrollAsync(month.Month,month.Year,f.Prefix,f.HR.AccountId);
            var fine=await db.ChiTietTruViPhamTamUngs.Where(v=>v.ChiTietPhieuLuong!.PhieuLuong!.MaNhanVien==f.WorkerA
                && v.ChiTietPhieuLuong.PhieuLuong.MaKyLuong==period).SingleAsync();
            Require(fine.LoaiTru=="KY_LUAT" && fine.SoTienTru>0 && fine.LyDoChiTiet.Contains("Bỏ ca"),
                "Absence payment failed to retain its disciplinary category, amount or explanation.");
        });
        await check("Phase 3 popup retains the payment period selected by create or choose", async () =>
        {
            var item=await f.Create(await Input(f));
            var month=HourlyEventRules.WorkMonth(item.NgayBatDau);
            var period=await Period(f,month);
            await using var db=f.Open();
            var http=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier,f.HR.AccountId.ToString())],"Test")) };
            var controller=new SuKienController(db,new HotelEventService(db),NullLogger<SuKienController>.Instance)
            { ControllerContext=new() { HttpContext=http }, ViewData=new(new EmptyModelMetadataProvider(),new ModelStateDictionary()) };
            var result=await controller.EnsurePaymentPeriod(item.MaDuAn,month.Month,month.Year,new() { IsPopup=true });
            Require(result is PartialViewResult { Model:EventDetailsViewModel updated } && updated.PaymentPeriodId==period,
                "Popup response discarded the period just selected by HR.");
        });
        await check("Phase 3 inconsistent absent work requires reconciliation before acceptance", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await f.With(s => s.RecordWorkAsync(f.HR, item.MaDuAn, work));
            await using (var db = f.Open())
                await db.PhanCongDuAns.Where(a => a.MaDuAn == item.MaDuAn)
                    .ExecuteUpdateAsync(a => a.SetProperty(p => p.DanhGia, "KHONG_THAM_GIA"));
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count > 0, "Absent staff with recorded work silently receives money.");
            var period = await Period(f, HourlyEventRules.WorkMonth(item.NgayBatDau));
            await Reject(() => f.With(s => s.AcceptHourlyAsync(f.HR, item.MaDuAn, preview.Version, period, preview.Token)));
        });
        await check("Phase 3 all absent staff may finish at zero without creating a payment", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await f.With(s => s.ReviewAsync(f.HR, item.MaDuAn, new() { Version=item.Version,
                AssignmentId=work.AssignmentId, Rating="KHONG_THAM_GIA" }));
            var preview = await f.With(s => s.PreviewAsync(f.HR, item.MaDuAn));
            Require(preview.Warnings.Count == 0 && preview.Total == 0 && preview.Rows.Count == 0, "Absent staff gets a fabricated award.");
            var period = await Period(f, HourlyEventRules.WorkMonth(item.NgayBatDau));
            await f.With(s => s.AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,period,preview.Token));
            var closed = await f.Get(item.MaDuAn);
            Require(closed.TrangThai == "HOAN_THANH" && closed.NganSachPhuCap == 0 && closed.QuyetToans.Count == 0,
                "Zero settlement creates money or fails to close.");
            work.Version=closed.Version;
            await Reject(() => f.With(s => s.RecordWorkAsync(f.HR,item.MaDuAn,work)));
        });
        await check("Phase 3 cross-month work rejects early payment and freezes each month separately", async () =>
        {
            var (item, work) = await WorkFixture(f);
            await f.SeedStatus(item.MaDuAn, "LEN_KE_HOACH");
            var start = HotelEventRules.ToUtc(new DateTime(2025,5,31,23,0,0));
            var end = start.AddHours(4);
            await using (var db = f.Open())
            {
                var shiftId=item.PhanCongs.Single().MaPhanCa;
                // This fixture has no work yet; detach its past assignment while constructing an overnight source.
                await db.PhanCongDuAns.Where(a=>a.MaDuAn==item.MaDuAn).ExecuteUpdateAsync(a=>a.SetProperty(p=>p.TrangThaiThamGia,"HUY_PHAN_CONG"));
                await db.PhanCas.Where(s=>s.MaPhanCa==shiftId).ExecuteUpdateAsync(s=>s
                    .SetProperty(p=>p.NgayLamViec,new DateOnly(2025,5,31)).SetProperty(p=>p.BatDauDuKien,start).SetProperty(p=>p.KetThucDuKien,end));
                await db.DuAns.Where(e=>e.MaDuAn==item.MaDuAn).ExecuteUpdateAsync(e=>e
                    .SetProperty(p=>p.NgayBatDau,start).SetProperty(p=>p.NgayKetThuc,end));
                await db.PhanCongDuAns.Where(a=>a.MaDuAn==item.MaDuAn).ExecuteUpdateAsync(a=>a
                    .SetProperty(p=>p.BatDau,start).SetProperty(p=>p.KetThuc,end).SetProperty(p=>p.TrangThaiThamGia,"DA_PHAN"));
                await db.PhanCas.Where(s=>s.MaPhanCa==shiftId).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.DaCongBo,true));
            }
            await f.SeedStatus(item.MaDuAn, "CHO_NGHIEM_THU");
            item=await f.Get(item.MaDuAn); work.Version=item.Version;
            work.Start=HotelEventRules.Local(start); work.End=work.Start.AddHours(3);
            await f.With(s=>s.RecordWorkAsync(f.HR,item.MaDuAn,work));
            var preview=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            Require(preview.Warnings.Count==0 && preview.Rows.Count==2 && preview.Rows[0].Minutes==60 && preview.Rows[1].Minutes==120,
                "Cross-month participation is not split using Vietnam time.");
            var early=await Period(f,new(2025,5,1));
            await Reject(()=>f.With(s=>s.AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,early,preview.Token)));
            var valid=await Period(f,new(2025,6,1));
            await f.With(s=>s.AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,valid,preview.Token));
            var frozen=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            Require(frozen.Rows.All(r=>r.Transfer?.PeriodId==valid) && frozen.Rows.Sum(r=>r.Minutes)==180,
                "Frozen months lost the shared valid payment period.");
        });
        await check("Phase 3 acceptance rolls back persisted settlement when saving fails", async () =>
        {
            var (item, work)=await WorkFixture(f);
            await f.With(s=>s.RecordWorkAsync(f.HR,item.MaDuAn,work));
            var preview=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            var period=await Period(f,HourlyEventRules.WorkMonth(item.NgayBatDau));
            await using var connection=f.Open();
            var options=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.Database.GetConnectionString())
                .AddInterceptors(new FailAcceptedSave(item.MaDuAn)).Options;
            await using (var broken=new AppDbContext(options))
            {
                try { await new HotelEventService(broken).AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,period,preview.Token);
                    throw new Exception("Injected failure was not reached."); }
                catch (InvalidOperationException e) when (e.Message=="Injected acceptance failure") { }
            }
            var current=await f.Get(item.MaDuAn);
            Require(current.TrangThai=="CHO_NGHIEM_THU" && current.Version==preview.Version && current.QuyetToans.Count==0,
                "A failed save leaves a partially accepted event or settlement.");
            var history=await f.With(s=>s.HistoryAsync(item.MaDuAn));
            Require(history.All(h=>h.HanhDong!="NGHIEM_THU_SU_KIEN"), "Rolled-back approval left an acceptance audit.");
            await f.With(s=>s.AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,period,preview.Token));
        });
        await check("Phase 3 payroll transfer status follows frozen money and blocks approval until linked", async () =>
        {
            var (item, work)=await WorkFixture(f);
            await f.With(s=>s.RecordWorkAsync(f.HR,item.MaDuAn,work));
            var month=HourlyEventRules.WorkMonth(item.NgayBatDau);
            var period=await Period(f,month);
            async Task Calculate() { await using var db=f.Open(); await new PayrollCalculationService(db)
                .CalculatePayrollAsync(month.Month,month.Year,f.Prefix,f.HR.AccountId); }
            await Calculate();
            var preview=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            await f.With(s=>s.AcceptHourlyAsync(f.HR,item.MaDuAn,preview.Version,period,preview.Token));
            var frozen=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            Require(frozen.Rows.Single().Transfer is { DetailId:null, PayslipId:null, PeriodStatus:"MO" }, "Unlinked money claims a payslip.");
            await using (var db=f.Open())
            {
                try { await new PayrollCalculationService(db).SubmitForApprovalAsync(period,f.Prefix,f.HR.AccountId);
                    throw new Exception("Approval allowed an outstanding settlement."); }
                catch (InvalidOperationException e) when (e.Message.Contains("quyết toán")) { }
            }
            await Calculate(); await Calculate();
            var linked=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            var transfer=linked.Rows.Single().Transfer!;
            Require(transfer.PayslipId.HasValue && transfer.DetailId.HasValue && linked.Total==frozen.Total,
                "Recalculation duplicated or repriced a frozen settlement.");
            await using (var db=f.Open())
            {
                Require(await db.ChiTietPhieuLuongs.CountAsync(d=>d.MaPhieuLuong==transfer.PayslipId && d.TenKhoanLuu=="Tiền sự kiện SK-"+item.MaDuAn)==1,
                    "Repeated payroll calculation duplicates an event payment.");
                await db.KyLuongs.Where(k=>k.MaKyLuong==period).ExecuteUpdateAsync(k=>k.SetProperty(p=>p.TrangThai,"KHOA_SO"));
            }
            var locked=await f.With(s=>s.PreviewAsync(f.HR,item.MaDuAn));
            Require(locked.Rows.Single().Transfer?.PeriodStatus=="KHOA_SO" && locked.Total==frozen.Total,
                "Locked period changed the saved money or disappeared from transfer status.");
            await Period(f,month);
        });
    }
}

sealed class FailAcceptedSave(int eventId) : SaveChangesInterceptor
{
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,int result,CancellationToken cancellationToken=default)
    {
        if (eventData.Context!.ChangeTracker.Entries<DuAn>().Any(e=>e.Entity.MaDuAn==eventId && e.Entity.TrangThai=="HOAN_THANH"))
            throw new InvalidOperationException("Injected acceptance failure");
        return ValueTask.FromResult(result);
    }
}
