using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

internal static class HourlyEventTests
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, Action test)
        {
            try { test(); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e.Message); }
        }
        var day = HotelEventRules.ToUtc(new DateTime(2027, 1, 7));
        Check("Hourly events add ONE hourly wage and round only the final employee amount", () =>
        {
            Equal(76923m, HourlyEventRules.Amount([new(day.AddHours(15), day.AddHours(17), 8_000_000m, 1)]));
            Equal(57692m, HourlyEventRules.Amount([new(day.AddHours(15.5), day.AddHours(17), 8_000_000m, 1)]));
            Equal(76923m, HourlyEventRules.Amount([new(day.AddHours(15), day.AddHours(16), 8_000_000m, 1), new(day.AddHours(16), day.AddHours(17), 8_000_000m, 1)]));
        });
        Check("Event participation stops at the end of the source shift", () =>
        {
            var window = HourlyEventRules.Intersect(day.AddHours(15), day.AddHours(20), day.AddHours(8), day.AddHours(17));
            Equal(day.AddHours(15), window!.Value.Start); Equal(day.AddHours(17), window.Value.End);
            Equal(true, HourlyEventRules.Intersect(day.AddHours(15), day.AddHours(20), day.AddHours(20), day.AddHours(28)) is null);
        });
        Check("Hourly work cannot count the same minute twice", () =>
        {
            Reject(() => HourlyEventRules.Amount([new(day.AddHours(15), day.AddHours(17), 8_000_000m, 1), new(day.AddHours(16), day.AddHours(18), 8_000_000m, 1)]));
        });
        Check("Hourly rates follow the effective working date and split at salary changes", () =>
        {
            var contracts = new[] {
                new HopDong { MaHopDong=1, NgayBatDau=new(2027,1,1), NgayKetThuc=new(2027,1,7), LuongCoBan=8_000_000m, TrangThai="HET_HAN" },
                new HopDong { MaHopDong=2, NgayBatDau=new(2027,1,8), LuongCoBan=10_000_000m, TrangThai="HIEU_LUC" }
            };
            var slices=HourlyEventRules.Price(day.AddHours(23),day.AddHours(25),contracts);
            Equal(2,slices.Count); Equal(86538m,HourlyEventRules.Amount(slices));
        });
        Check("Hourly earnings split by Vietnam work month rather than UTC month", () =>
        {
            var start=HotelEventRules.ToUtc(new DateTime(2027,1,31,23,0,0));
            var slices=HourlyEventRules.Price(start,start.AddHours(2), [new HopDong { MaHopDong=1, NgayBatDau=new(2027,1,1), LuongCoBan=8_000_000m, TrangThai="HIEU_LUC" }]);
            Equal(2,slices.Count); Equal(1,HourlyEventRules.WorkMonth(slices[0].Start).Month); Equal(2,HourlyEventRules.WorkMonth(slices[1].Start).Month);
        });
        Check("Missing or ambiguous salary evidence blocks approval", () =>
        {
            Reject(() => HourlyEventRules.Price(day,day.AddHours(1),[]));
            Reject(() => HourlyEventRules.Price(day,day.AddHours(1),[
                new HopDong { NgayBatDau=new(2027,1,1), LuongCoBan=8_000_000m, TrangThai="HIEU_LUC" },
                new HopDong { NgayBatDau=new(2027,1,1), LuongCoBan=10_000_000m, TrangThai="HIEU_LUC" }]));
        });
        Check("A published schedule week is Monday based even across years", () =>
        {
            Equal(new DateOnly(2026,12,28),HourlyEventRules.WeekStart(new DateOnly(2027,1,3)));
        });
        Check("Department names identify the actual department instead of installation-specific IDs", () =>
        {
            Equal("KY_THUAT",HotelEventRules.ResolveDepartmentCode(2,"Kỹ thuật"));
            Equal<string?>(null,HotelEventRules.ResolveDepartmentCode(2,"Hành chính nhân sự"));
        });
        Check("Role coverage follows declared windows and the department saved at assignment", () =>
        {
            var e=new DuAn { TinhTheoGio=true, LoaiDuAn="HOI_NGHI_MICE", NgayBatDau=day.AddHours(8), NgayKetThuc=day.AddDays(2).AddHours(17), SoKhachDuKien=40 };
            e.NhuCaus.Add(new NhuCauSuKien { MaPhongBan="AM_THUC", VaiTro="Phục vụ tiệc trà",SoNguoi=1,BatDau=day.AddHours(8),KetThuc=day.AddHours(17) });
            e.NhuCaus.Add(new NhuCauSuKien { MaPhongBan="AM_THUC", VaiTro="Phục vụ tiệc trà",SoNguoi=1,BatDau=day.AddDays(2).AddHours(8),KetThuc=day.AddDays(2).AddHours(17) });
            e.PhanCongs.Add(new PhanCongDuAn { MaNhanVien=1,MaPhongBanSuKien="AM_THUC",VaiTroDuAn="Phục vụ tiệc trà",BatDau=day.AddHours(8),KetThuc=day.AddHours(17),NhanVien=new NhanVien { MaPhongBan=5 } });
            Equal(false,HotelEventRules.GetStaffingSummary(e).IsFullyStaffed);
            e.PhanCongs.Add(new PhanCongDuAn { MaNhanVien=2,MaPhongBanSuKien="AM_THUC",VaiTroDuAn="Phục vụ tiệc trà",BatDau=day.AddDays(2).AddHours(8),KetThuc=day.AddDays(2).AddHours(17) });
            Equal(true,HotelEventRules.GetStaffingSummary(e).IsFullyStaffed);
            e.PhanCongs.Last().TrangThaiThamGia="HUY_PHAN_CONG";
            Equal(false,HotelEventRules.GetStaffingSummary(e).IsFullyStaffed);
        });
        return failures;
    }
    private static void Equal<T>(T expected,T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Reject(Action action)
    { try { action(); } catch(ArgumentException) { return; } throw new Exception("Expected invalid work evidence to be rejected."); }
}
