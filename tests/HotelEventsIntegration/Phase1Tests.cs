using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Services;

static class Phase1Tests
{
    public static async Task Run(Fixture f, Func<string, Func<Task>, Task> check)
    {
        await check("Phase 1 legacy booking creates catalog needs without staff or shifts", async () =>
        {
            var item = await f.Create(f.Input());
            Require(item.TrangThai == "LEN_KE_HOACH" && item.PhanCongs.Count == 0, "Booking incorrectly requires staffing.");
            Require(item.NhuCaus.Count == 12 && item.NhuCaus.All(n => n.LaMau && n.SoNguoi == 1),
                "MICE with 40 guests must persist twelve required catalog needs, each with one person.");
        });

        await check("Phase 1 guest changes preserve edited needs and rescale only template needs", async () =>
        {
            var item = await f.Create(f.Input());
            var need = item.NhuCaus.Single(n => n.MaPhongBan == "KY_THUAT" && n.VaiTro == "Phụ trách âm thanh và micro");
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new EventNeedInput
            {
                Version = item.Version, NeedId = need.MaNhuCau, Name = "Customer technicians", Department = need.MaPhongBan,
                Role = need.VaiTro, Count = 7, Start = HotelEventRules.Local(need.BatDau), End = HotelEventRules.Local(need.KetThuc),
                Reason = "Customer requested seven technicians"
            }));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item);
            edit.ExpectedGuests = 80;
            await f.With(s => s.SaveAsync(f.Admin, edit));
            item = await f.Get(item.MaDuAn);
            var custom = item.NhuCaus.Single(n => n.MaNhuCau == need.MaNhuCau);
            Require(!custom.LaMau && custom.SoNguoi == 7 && custom.TenKhung == "Customer technicians"
                && custom.BatDau == need.BatDau && custom.KetThuc == need.KetThuc, "Guest changes overwrote a custom need.");
            var role = HotelEventRules.GetStaffingSummary(item).Departments.Single(d => d.Code == "KY_THUAT")
                .Roles.Single(r => r.Name == custom.VaiTro);
            Require(role.RequiredCount == 7 && role.MissingCount == 7,
                "Staffing checks ignored the persisted seven-person override.");
            Require(item.NhuCaus.Single(n => n.MaPhongBan == "AM_THUC" && n.VaiTro == "Phục vụ tiệc trà").SoNguoi == 2,
                "Template staffing did not scale from 40 to 80 guests.");
        });

        await check("Phase 1 legacy event cannot shrink past an edited need", async () =>
        {
            var item = await f.Create(f.Input());
            var need = item.NhuCaus.First();
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new EventNeedInput
            {
                Version = item.Version, NeedId = need.MaNhuCau, Department = need.MaPhongBan, Role = need.VaiTro,
                Count = 3, Start = HotelEventRules.Local(need.BatDau), End = HotelEventRules.Local(need.KetThuc), Reason = "Customer override"
            }));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item);
            edit.End = edit.End.AddMinutes(-30);
            await Reject(() => f.With(s => s.SaveAsync(f.Admin, edit)), "Input.Start");
            var stored = await f.Get(item.MaDuAn);
            Require(stored.NgayKetThuc == item.NgayKetThuc && stored.NhuCaus.Single(n => n.MaNhuCau == need.MaNhuCau).SoNguoi == 3,
                "Rejected schedule edit changed persisted event or need.");
        });

        await check("Phase 1 legacy event cannot change type past an edited department", async () =>
        {
            var item = await f.Create(f.Input());
            var need = item.NhuCaus.First(n => n.MaPhongBan == "IT");
            await f.With(s => s.SaveNeedAsync(f.HR, item.MaDuAn, new EventNeedInput
            {
                Version = item.Version, NeedId = need.MaNhuCau, Department = need.MaPhongBan, Role = need.VaiTro,
                Count = 2, Start = HotelEventRules.Local(need.BatDau), End = HotelEventRules.Local(need.KetThuc), Reason = "Dedicated IT support"
            }));
            item = await f.Get(item.MaDuAn);
            var edit = f.Edit(item);
            edit.Type = "TIEC_CUOI";
            await Reject(() => f.With(s => s.SaveAsync(f.Admin, edit)), "Input.Start");
            Require((await f.Get(item.MaDuAn)).LoaiDuAn == "HOI_NGHI_MICE", "Rejected type change was persisted.");
        });

        await check("Phase 1 overview reads preserve manager scope and do not auto-cancel", async () =>
        {
            var item = await f.Create(f.Input());
            await f.MoveToPast(item.MaDuAn);
            Require(await f.With(s => s.GetOverviewAsync(f.OtherManager, item.MaDuAn)) is null, "Overview bypassed manager scope.");
            var own = await f.With(s => s.GetOverviewAsync(f.Manager, item.MaDuAn));
            Require(own?.TrangThai == "LEN_KE_HOACH" && own.LyDoHuy is null, "Overview cancelled an event on read.");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task Reject(Func<Task> run, string field)
    {
        try { await run(); }
        catch (EventOperationException ex) when (ex.Field == field && !ex.Forbidden) { return; }
        throw new InvalidOperationException("Expected event validation rejection for " + field);
    }
}
