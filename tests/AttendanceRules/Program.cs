using System.Text.Json;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.MobileApp.Services;

// Offline business acceptance checks. Never connects to or modifies Supabase.
var tests = new (string Name, Action Run)[]
{
    ("Supabase object relations preserve real punches, paid leave and double-count warnings", () =>
    {
        var shifts = AttendanceSummaryPostgrstMapper.ReadRows("""
            [{"ma_phan_ca":4,"ma_nhan_vien":5,"ma_ca":1,"ngay_lam_viec":"2026-10-04",
              "bat_dau_du_kien":"2026-10-04T06:00:00+07:00","ket_thuc_du_kien":"2026-10-04T14:00:00+07:00",
              "trang_thai":"HOAN_THANH","ca_lam_viec":{"ten_ca":"Ca sáng"},
              "cham_cong":{"ma_cham_cong":1,"gio_check_in":"2026-10-04T06:00:00+07:00",
              "gio_check_out":"2026-10-04T14:00:00+07:00","trang_thai":"DUNG_GIO"}}]
            """).Select(AttendanceSummaryPostgrstMapper.ToShift);
        var requests = AttendanceSummaryPostgrstMapper.ReadRows("""
            [{"ma_don":101,"ma_nhan_vien":5,"ma_loai_don":97,"tu_ngay":"2026-10-04",
              "den_ngay":"2026-10-04","ngay_duyet":"2026-10-03T10:00:00Z","trang_thai":"DA_DUYET",
              "loai_don":{"ten_loai_don":"Phép năm","co_huong_luong":true}}]
            """).Select(AttendanceSummaryPostgrstMapper.ToLeave);
        var result = Summary(shifts, requests);
        Equal(1, result.WorkedDays);
        Equal(0, result.PaidLeaveDays);
        Equal(1, result.TotalCreditedDays);
        Equal(1, result.ReviewDays);
        Equal("Ca sáng", result.Days.Single().Shifts.Single().Name);
    }),
    ("Supabase array relations and null punches retain scheduled paid leave", () =>
    {
        var shift = AttendanceSummaryPostgrstMapper.ToShift(AttendanceSummaryPostgrstMapper.ReadRows("""
            [{"ma_phan_ca":4,"ma_nhan_vien":5,"ma_ca":1,"ngay_lam_viec":"2026-10-04",
              "bat_dau_du_kien":"2026-10-04T06:00:00+07:00","ket_thuc_du_kien":"2026-10-04T14:00:00+07:00",
              "trang_thai":"DA_PHAN","ca_lam_viec":[{"ten_ca":"Ca sáng"}],"cham_cong":null}]
            """).Single());
        var leave = AttendanceSummaryPostgrstMapper.ToLeave(AttendanceSummaryPostgrstMapper.ReadRows("""
            [{"ma_don":101,"ma_nhan_vien":5,"ma_loai_don":97,"tu_ngay":"2026-10-04",
              "den_ngay":"2026-10-04","ngay_duyet":null,"trang_thai":"DA_DUYET",
              "loai_don":[{"ten_loai_don":"Phép năm","co_huong_luong":"t"}]}]
            """).Single());
        var result = Summary([shift], [leave]);
        Equal(1, result.PaidLeaveDays);
        Equal(0, result.WorkedDays);
    }),
    ("Malformed Supabase payloads are errors instead of empty zero totals", () =>
    {
        Throws<JsonException>(() => AttendanceSummaryPostgrstMapper.ReadRows("not json"));
        Throws<JsonException>(() => AttendanceSummaryPostgrstMapper.ReadRows("{}"));
        Throws<JsonException>(() => AttendanceSummaryPostgrstMapper.ReadRows("[1]"));
        Equal(0, AttendanceSummaryPostgrstMapper.ReadRows("[]").Length);
    }),
    ("Missing leave catalog and invalid schedule timestamps fail instead of silently losing credit", () =>
    {
        var leave = AttendanceSummaryPostgrstMapper.ReadRows("[{\"loai_don\":null}]").Single();
        Throws<JsonException>(() => AttendanceSummaryPostgrstMapper.ToLeave(leave));
        var shift = AttendanceSummaryPostgrstMapper.ReadRows("""
            [{"ma_phan_ca":4,"ma_nhan_vien":5,"ma_ca":1,"ngay_lam_viec":"2026-10-04",
              "bat_dau_du_kien":"invalid","ket_thuc_du_kien":"2026-10-04T14:00:00+07:00","cham_cong":null}]
            """).Single();
        Throws<JsonException>(() => AttendanceSummaryPostgrstMapper.ToShift(shift));
    }),
    ("Approved paid leave counts only scheduled dates, inclusive", () =>
    {
        var result = Summary([Shift(4), Shift(5)], [Leave(1, 4, 6)]);
        Equal(2, result.PaidLeaveDays);
        Equal(2, result.TotalCreditedDays);
        Equal(3, result.Days.Count);
        Equal(0, result.Days.Single(d => d.Date.Day == 6).CreditedDays);
    }),
    ("Pending, rejected, unpaid and missing leave types do not contribute", () =>
    {
        var requests = new[] { Leave(1, 4, 4), Leave(2, 4, 4), Leave(3, 4, 4), Leave(4, 4, 4) };
        requests[0].TrangThai = "CHO_DUYET";
        requests[1].TrangThai = "TU_CHOI";
        requests[2].LoaiDon!.CoHuongLuong = false;
        requests[3].LoaiDon = null;
        Equal(0, Summary([Shift(4)], requests).TotalCreditedDays);
    }),
    ("Paid eligibility comes from the catalog flag, not hardcoded type IDs", () =>
    {
        var request = Leave(1, 4, 4);
        request.MaLoaiDon = 97;
        Equal(1, Summary([Shift(4)], [request]).PaidLeaveDays);
        request.LoaiDon!.CoHuongLuong = false;
        Equal(0, Summary([Shift(4)], [request]).PaidLeaveDays);
    }),
    ("Overlapping applications count once and preserve every source ID", () =>
    {
        var request = Leave(1, 4, 5);
        var result = Summary([Shift(4), Shift(5)], [request, Leave(2, 5, 6), request]);
        Equal(2, result.TotalCreditedDays);
        Equal("1,2", string.Join(',', result.Days.Single(d => d.Date.Day == 5).LeaveRequests.Select(r => r.RequestId)));
    }),
    ("Worked day plus paid leave produces one credit and a review flag", () =>
    {
        var result = Summary([Shift(4, worked: true), Shift(5)], [Leave(1, 4, 5)]);
        Equal(1, result.WorkedDays);
        Equal(1, result.PaidLeaveDays);
        Equal(2, result.TotalCreditedDays);
        Equal(1, result.ReviewDays);
    }),
    ("Multiple shifts on a date do not multiply daily leave credits", () =>
    {
        var first = Shift(4);
        var second = Shift(4, worked: true);
        second.MaPhanCa = 100;
        Equal(1, Summary([first, second], [Leave(1, 4, 4)]).TotalCreditedDays);
        Equal(1, Summary([first, second], []).ScheduledDays);
    }),
    ("An open punch is not completed work and is flagged if paid leave exists", () =>
    {
        var shift = Shift(4, worked: true);
        shift.ChamCong!.GioCheckOut = null;
        Equal(0, Summary([shift], []).WorkedDays);
        var result = Summary([shift], [Leave(1, 4, 4)]);
        Equal(1, result.PaidLeaveDays);
        Equal(1, result.ReviewDays);
    }),
    ("Default attendance status without punches does not imply a worked day", () =>
    {
        var shift = Shift(4);
        shift.ChamCong = new ChamCong();
        Equal(0, Summary([shift], []).WorkedDays);
    }),
    ("Invalid checkout and absent status cannot create worked credit", () =>
    {
        var shift = Shift(4, worked: true);
        shift.ChamCong!.GioCheckOut = shift.ChamCong.GioCheckIn!.Value.AddMinutes(-1);
        Equal(0, Summary([shift], []).WorkedDays);
        shift.ChamCong.GioCheckOut = shift.ChamCong.GioCheckIn.Value.AddHours(8);
        shift.ChamCong.TrangThai = "VANG";
        Equal(0, Summary([shift], []).WorkedDays);
    }),
    ("Late/early completed attendance remains a worked day", () =>
    {
        foreach (var status in new[] { "DUNG_GIO", "DI_TRE", "VE_SOM", "TRE_VA_SOM" })
        {
            var shift = Shift(4, worked: true);
            shift.ChamCong!.TrangThai = status;
            Equal(1, Summary([shift], []).WorkedDays);
        }
    }),
    ("Night shift belongs to its scheduled date across month boundary", () =>
    {
        var shift = Shift(31, worked: true);
        shift.BatDauDuKien = new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.FromHours(7));
        shift.KetThucDuKien = shift.BatDauDuKien.AddHours(8);
        shift.ChamCong!.GioCheckIn = shift.BatDauDuKien;
        shift.ChamCong.GioCheckOut = shift.KetThucDuKien;
        Equal(1, Summary([shift], [Leave(1, 31, 31)]).TotalCreditedDays);
        Equal(0, AttendanceSummaryCalculator.Calculate(5, "Employee", new(2026, 11, 1), new(2026, 11, 30), [shift], []).TotalCreditedDays);
    }),
    ("Cross-month leave is clipped to the selected period", () =>
    {
        var request = Leave(1, 1, 31);
        request.TuNgay = new DateOnly(2026, 9, 29);
        request.DenNgay = new DateOnly(2026, 11, 2);
        var result = Summary([Shift(1), Shift(31)], [request]);
        Equal(31, result.Days.Count);
        Equal(2, result.PaidLeaveDays);
    }),
    ("Recompute after approval, rejection and rescheduling reflects current data", () =>
    {
        var request = Leave(1, 4, 5);
        request.TrangThai = "CHO_DUYET";
        Equal(0, Summary([Shift(4)], [request]).PaidLeaveDays);
        request.TrangThai = "DA_DUYET";
        Equal(1, Summary([Shift(4)], [request]).PaidLeaveDays);
        Equal(2, Summary([Shift(4), Shift(5)], [request]).PaidLeaveDays);
        Equal(0, Summary([], [request]).PaidLeaveDays);
        request.TrangThai = "TU_CHOI";
        Equal(0, Summary([Shift(4)], [request]).PaidLeaveDays);
    }),
    ("Other employees and cancelled shifts are excluded", () =>
    {
        var otherShift = Shift(4, worked: true);
        otherShift.MaNhanVien = 8;
        var otherLeave = Leave(2, 5, 5);
        otherLeave.MaNhanVien = 8;
        var cancelled = Shift(6, worked: true);
        cancelled.TrangThai = "DA_HUY";
        var result = Summary([otherShift, Shift(5), cancelled], [otherLeave, Leave(1, 6, 6)]);
        Equal(0, result.TotalCreditedDays);
        Equal(1, result.ScheduledDays);
    }),
    ("Leap day, maximum date and invalid intervals are handled", () =>
    {
        var shift = Shift(4);
        var request = Leave(1, 4, 4);
        foreach (var day in new[] { new DateOnly(2028, 2, 29), DateOnly.MaxValue })
        {
            shift.NgayLamViec = request.TuNgay = request.DenNgay = day;
            Equal(1, AttendanceSummaryCalculator.Calculate(5, "Employee", day, day, [shift], [request]).PaidLeaveDays);
        }
        Throws<ArgumentException>(() => AttendanceSummaryCalculator.ValidatePeriod(new(2026, 10, 4), new(2026, 10, 3)));
        Throws<ArgumentException>(() => AttendanceSummaryCalculator.ValidatePeriod(new(2026, 1, 1), new(2027, 1, 2)));
    }),
    ("Repeated evaluation neither accumulates credit nor alters attendance/OT", () =>
    {
        var shift = Shift(4, worked: true);
        shift.ChamCong!.GioTangCaOt = 2.5m;
        var request = Leave(1, 4, 4);
        var before = JsonSerializer.Serialize(shift);
        var initial = JsonSerializer.Serialize(Summary([shift], [request]));
        for (var i = 0; i < 20; i++) Equal(initial, JsonSerializer.Serialize(Summary([shift], [request])));
        Equal(before, JsonSerializer.Serialize(shift));
    }),
    ("Summary survives API camel-case JSON roundtrip", () =>
    {
        var result = Summary([Shift(4, worked: true), Shift(5)], [Leave(1, 4, 5)]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var restored = JsonSerializer.Deserialize<AttendanceSummaryDto>(JsonSerializer.Serialize(result, options), options)!;
        Equal(2, restored.TotalCreditedDays);
        Equal(1, restored.PaidLeaveDays);
        Equal(1, restored.ReviewDays);
        Equal(1, restored.Days[0].LeaveRequests[0].RequestId);
    }),
    ("Daily credit never exceeds one under overlapping random applications", () =>
    {
        var random = new Random(20);
        var shifts = Enumerable.Range(1, 31).Select(day => Shift(day, worked: day % 3 == 0)).ToArray();
        var requests = Enumerable.Range(1, 100).Select(id =>
        {
            var from = random.Next(1, 32);
            return Leave(id, from, random.Next(from, 32));
        }).ToArray();
        var result = Summary(shifts, requests);
        Equal(result.WorkedDays + result.PaidLeaveDays, result.TotalCreditedDays);
        Equal(true, result.TotalCreditedDays <= 31);
        Equal(true, result.Days.All(d => d.CreditedDays is 0 or 1));
    }),
    ("Employee access is self-only; unknown roles fail closed even for self", () =>
    {
        Equal(true, AttendanceAccessPolicy.CanView("Employee", 5, 4, 5, 4));
        Equal(false, AttendanceAccessPolicy.CanView("Employee", 5, 4, 6, 4));
        Equal(false, AttendanceAccessPolicy.CanView("unexpected", 5, 4, 5, 4));
        Equal(false, AttendanceAccessPolicy.CanView(null, 5, 4, 5, 4));
    }),
    ("Manager access is department-scoped; HR/Admin may view other departments", () =>
    {
        Equal(true, AttendanceAccessPolicy.CanView("Manager", 4, 4, 5, 4));
        Equal(false, AttendanceAccessPolicy.CanView("Manager", 4, 4, 5, 8));
        Equal(false, AttendanceAccessPolicy.CanView("Manager", 4, null, 5, null));
        Equal(true, AttendanceAccessPolicy.CanView("HR", 2, 8, 5, 4));
        Equal(true, AttendanceAccessPolicy.CanView("Admin", 1, null, 5, 4));
        Equal(false, AttendanceAccessPolicy.CanView("Admin", 0, null, 5, 4));
    })
};

var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed.");
return failures == 0 ? 0 : 1;

static AttendanceSummaryDto Summary(IEnumerable<PhanCa> shifts, IEnumerable<DonTu> requests) =>
    AttendanceSummaryCalculator.Calculate(5, "Employee", new(2026, 10, 1), new(2026, 10, 31), shifts, requests);

static PhanCa Shift(int day, bool worked = false)
{
    var start = new DateTimeOffset(2026, 10, day, 6, 0, 0, TimeSpan.FromHours(7));
    return new PhanCa
    {
        MaPhanCa = day, MaNhanVien = 5, NgayLamViec = new(2026, 10, day),
        BatDauDuKien = start, KetThucDuKien = start.AddHours(8),
        ChamCong = worked ? new ChamCong { GioCheckIn = start, GioCheckOut = start.AddHours(8) } : null
    };
}

static DonTu Leave(int id, int from, int to) => new()
{
    MaDon = id, MaNhanVien = 5, MaLoaiDon = 1, TuNgay = new(2026, 10, from), DenNgay = new(2026, 10, to),
    TrangThai = "DA_DUYET", LoaiDon = new LoaiDon { TenLoaiDon = "Phép năm", CoHuongLuong = true },
    NgayDuyet = new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.FromHours(7))
};

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; got {actual}.");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
