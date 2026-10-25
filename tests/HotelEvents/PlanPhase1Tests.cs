using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

internal static class PlanPhase1Tests
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, Action test)
        {
            try { test(); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        var start = new DateTimeOffset(2028, 5, 10, 2, 0, 0, TimeSpan.Zero);
        Check("Legacy staffing honors saved needs when starting and removing staff", () =>
        {
            var end = start.AddHours(4);
            var item = new DuAn
            {
                LoaiDuAn = "HOI_NGHI_MICE", SoKhachDuKien = 40, NgayBatDau = start, NgayKetThuc = end,
                NhuCaus = [new() { MaPhongBan = "KY_THUAT", VaiTro = "Phụ trách âm thanh và micro",
                    SoNguoi = 7, BatDau = start, KetThuc = end, LaMau = false }]
            };
            void AddMember(int id) => item.PhanCongs.Add(new()
            {
                MaPhanCong = id, MaNhanVien = id, MaPhongBanSuKien = "KY_THUAT",
                VaiTroDuAn = "Phụ trách âm thanh và micro", BatDau = start, KetThuc = end
            });
            for (var id = 1; id <= 6; id++) AddMember(id);
            var summary = HotelEventRules.GetStaffingSummary(item);
            var role = summary.Departments.Single(d => d.Code == "KY_THUAT").Roles
                .Single(r => r.Name == "Phụ trách âm thanh và micro");
            Equal(7, role.RequiredCount);
            Equal(1, role.MissingCount);
            Equal(false, summary.IsFullyStaffed);
            AddMember(7);
            Equal(true, HotelEventRules.GetStaffingSummary(item).IsFullyStaffed);
            Equal(false, HotelEventRoleRules.CanRemove(item, 1, start.AddHours(1)));
            AddMember(8);
            Equal(true, HotelEventRoleRules.CanRemove(item, 1, start.AddHours(1)));
        });
        Check("Effective staff count deduplicates segments and excludes both cancellation codes", () =>
        {
            PhanCongDuAn[] assignments = [
                new() { MaNhanVien = 10, TrangThaiThamGia = "DA_PHAN" },
                new() { MaNhanVien = 10, TrangThaiThamGia = "DA_KET_THUC" },
                new() { MaNhanVien = 11, TrangThaiThamGia = "HUY_PHAN_CONG" },
                new() { MaNhanVien = 12, TrangThaiThamGia = "DA_HUY" },
                new() { MaNhanVien = 13, TrangThaiThamGia = "DA_KET_THUC" }
            ];
            Equal(2, HotelEventRules.CountEffectiveStaff(assignments));
            Equal(0, HotelEventRules.CountEffectiveStaff([]));
        });
        Check("Late confirmation warns at the start only while an event is still planned", () =>
        {
            var item = new DuAn { TrangThai = "LEN_KE_HOACH", NgayBatDau = start };
            Equal(false, HotelEventRules.NeedsLateConfirmationWarning(item, start.AddTicks(-1)));
            Equal(true, HotelEventRules.NeedsLateConfirmationWarning(item, start));
            Equal(true, HotelEventRules.NeedsLateConfirmationWarning(item, start.AddDays(3)));
            foreach (var status in new[] { "DANG_DIEN_RA", "CHO_NGHIEM_THU", "HOAN_THANH", "HUY" })
            {
                item.TrangThai = status;
                Equal(false, HotelEventRules.NeedsLateConfirmationWarning(item, start.AddDays(3)));
            }
        });
        Check("Multi-day events may start late through their end but cannot start afterwards", () =>
        {
            var end = start.AddDays(2);
            Equal(false, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, start.AddTicks(-1)));
            Equal(true, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, start.AddDays(1)));
            Equal(true, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, end));
            Equal(false, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, end.AddTicks(1)));
        });
        return failures;
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
}
