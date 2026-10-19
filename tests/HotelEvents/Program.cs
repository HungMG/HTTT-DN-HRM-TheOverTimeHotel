using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

var start = new DateTimeOffset(2028, 2, 29, 17, 0, 0, TimeSpan.Zero); // Vietnam March 1 midnight
var end = start.AddHours(2);
var tests = new (string Name, Action Run)[]
{
    ("Creation uses the Vietnam calendar day, including midnight and earlier hours today", () =>
    {
        var beforeMidnight = new DateTimeOffset(2026, 10, 15, 16, 59, 59, TimeSpan.Zero);
        Equal(false, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 14), beforeMidnight));
        Equal(true, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 15), beforeMidnight));
        var midnight = new DateTimeOffset(2026, 10, 15, 17, 0, 0, TimeSpan.Zero);
        Equal(false, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 15), midnight));
        Equal(true, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 16), midnight));
        Equal(true, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 17), midnight));
        Equal(true, HotelEventRules.CanCreateOn(new DateOnly(2026, 10, 16), midnight.AddHours(20)));
    }),
    ("Vietnam wall clock ignores machine timezone and DateTime.Kind", () =>
    {
        foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Local, DateTimeKind.Utc })
            Equal(start, HotelEventRules.ToUtc(DateTime.SpecifyKind(new DateTime(2028, 3, 1), kind)));
        Equal(new DateTime(2028, 3, 1), HotelEventRules.Local(start));
        Equal(DateTimeKind.Unspecified, HotelEventRules.Local(start).Kind);
    }),
    ("Half open intervals allow adjacency and detect overnight conflicts", () =>
    {
        Equal(false, HotelEventRules.Overlaps(start, end, end, end.AddHours(1)));
        Equal(true, HotelEventRules.Overlaps(start, end, start.AddMinutes(-30), start.AddMinutes(1)));
        Equal(true, HotelEventRules.Overlaps(start, end, start, end));
        Equal(false, HotelEventRules.Overlaps(start, start, start, end));
    }),
    ("Employment and an effective contract cover every local participation day", () =>
    {
        var employee = Employee();
        Equal(true, HotelEventRules.ContractCovers(employee, start, end));
        employee.HopDongs.Single().NgayKetThuc = new DateOnly(2028, 2, 29);
        Equal(false, HotelEventRules.ContractCovers(employee, start, end));
        Equal(true, HotelEventRules.ContractCovers(employee, start.AddHours(-2), start));
        employee.HopDongs.Single().NgayKetThuc = null;
        employee.NgayVaoLam = new DateOnly(2028, 3, 2);
        Equal(false, HotelEventRules.ContractCovers(employee, start, end));
        employee.NgayVaoLam = new DateOnly(2028, 2, 1);
        employee.NgayThoiViec = new DateOnly(2028, 3, 1);
        Equal(false, HotelEventRules.ContractCovers(employee, start, end));
        employee.NgayThoiViec = null;
        employee.TrangThai = "DA_NGHI";
        Equal(false, HotelEventRules.ContractCovers(employee, start, end));
        employee.TrangThai = "DANG_LAM";
        employee.HopDongs.Single().TrangThai = "HET_HAN";
        Equal(false, HotelEventRules.ContractCovers(employee, start, end));
    }),
    ("Amounts preserve cents and numeric(15,2) bounds", () =>
    {
        Equal(true, HotelEventRules.IsMoney(0));
        Equal(true, HotelEventRules.IsMoney(9999999999999.99m));
        foreach (var value in new[] { -0.01m, 0.001m, 10000000000000m }) Equal(false, HotelEventRules.IsMoney(value));
    }),
    ("Office roles operate all events and Managers only their own", () =>
    {
        Equal(true, HotelEventRules.CanOperate("HR", 3, 8));
        Equal(true, HotelEventRules.CanOperate("Admin", 3, 8));
        Equal(true, HotelEventRules.CanOperate("Manager", 8, 8));
        Equal(false, HotelEventRules.CanOperate("Manager", 3, 8));
        Equal(false, HotelEventRules.CanOperate("IT", 8, 8));
        Equal(false, HotelEventRules.CanOperate("Employee", 8, 8));
    }),
    ("Manual lifecycle cannot run early or skip to acceptance", () =>
    {
        Equal(false, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, start.AddTicks(-1)));
        Equal(true, HotelEventRules.CanTransition("LEN_KE_HOACH", "DANG_DIEN_RA", start, end, start));
        Equal(false, HotelEventRules.CanTransition("DANG_DIEN_RA", "CHO_NGHIEM_THU", start, end, end.AddTicks(-1)));
        Equal(true, HotelEventRules.CanTransition("DANG_DIEN_RA", "CHO_NGHIEM_THU", start, end, end));
        Equal(false, HotelEventRules.CanTransition("LEN_KE_HOACH", "HOAN_THANH", start, end, end));
        Equal(false, HotelEventRules.CanTransition("HOAN_THANH", "HUY", start, end, end));
        Equal(false, HotelEventRules.CanTransition("HUY", "LEN_KE_HOACH", start, end, end));
        Equal(false, HotelEventRules.CanTransition("INVALID", "HUY", start, end, end));
        Equal(true, HotelEventRules.CanTransition("CHO_NGHIEM_THU", "HUY", start, end, end));
    }),
    ("Acceptance requires complete reviews within budget and zero absent pay", () =>
    {
        var a = new PhanCongDuAn { DanhGia = "HOAN_THANH", SoTienPhuCap = 50m, NgayDanhGia = end };
        Equal(true, HotelEventRules.ReviewsReady([a], 50));
        Equal(false, HotelEventRules.ReviewsReady([a], 49.99m));
        a.DanhGia = "CHUA_DANH_GIA";
        Equal(false, HotelEventRules.ReviewsReady([a], 50));
        a.DanhGia = "KHONG_THAM_GIA";
        Equal(false, HotelEventRules.ReviewsReady([a], 50));
        a.SoTienPhuCap = 0;
        Equal(true, HotelEventRules.ReviewsReady([a], 0));
        a.NgayDanhGia = null;
        Equal(false, HotelEventRules.ReviewsReady([a], 0));
    }),
    ("Only 5 valid event types exist and BAO_TRI_NANG_CAP is removed", () =>
    {
        Equal(5, HotelEventRules.TypeLabels.Count);
        Equal(false, HotelEventRules.TypeLabels.ContainsKey("BAO_TRI_NANG_CAP"));
        Equal(true, HotelEventRules.TypeLabels.ContainsKey("TIEC_CUOI"));
        Equal(true, HotelEventRules.TypeLabels.ContainsKey("HOI_NGHI_MICE"));
        Equal(true, HotelEventRules.TypeLabels.ContainsKey("GALA_DINNER"));
        Equal(true, HotelEventRules.TypeLabels.ContainsKey("FESTIVAL_AM_THUC"));
        Equal(true, HotelEventRules.TypeLabels.ContainsKey("DOAN_VIP"));
    }),
    ("Department codes resolve by ID and by normalized name", () =>
    {
        Equal("TIEN_SANH", HotelEventRules.ResolveDepartmentCode(2, null));
        Equal("AM_THUC", HotelEventRules.ResolveDepartmentCode(4, null));
        Equal("BEP", HotelEventRules.ResolveDepartmentCode(5, null));
        Equal("BUONG_PHONG", HotelEventRules.ResolveDepartmentCode(3, null));
        Equal("KY_THUAT", HotelEventRules.ResolveDepartmentCode(6, null));
        Equal("AN_NINH", HotelEventRules.ResolveDepartmentCode(7, null));
        Equal("IT", HotelEventRules.ResolveDepartmentCode(9, null));

        Equal("TIEN_SANH", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Tiền sảnh (FO - Front Office)"));
        Equal("AM_THUC", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Ẩm thực (F&B - Food & Beverage)"));
        Equal("BEP", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Bếp (Kitchen / Culinary)"));
        Equal("BUONG_PHONG", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Buồng phòng (HK - Housekeeping)"));
        Equal("KY_THUAT", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Kỹ thuật (ENG - Engineering)"));
        Equal("AN_NINH", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận An ninh (Security)"));
        Equal("IT", HotelEventRules.ResolveDepartmentCode(null, "Bộ phận Công nghệ Thông tin (IT - Information Technology)"));
        Equal(null, HotelEventRules.ResolveDepartmentCode(8, "Bộ phận Nhân sự & Tài chính (HR & ACC)"));
    }),
    ("Mandatory department rules match specifications for each of the 5 event types", () =>
    {
        Equal(6, HotelEventRules.GetRequiredDepartments("TIEC_CUOI").Count);
        Equal(6, HotelEventRules.GetRequiredDepartments("HOI_NGHI_MICE").Count);
        Equal(5, HotelEventRules.GetRequiredDepartments("GALA_DINNER").Count);
        Equal(5, HotelEventRules.GetRequiredDepartments("FESTIVAL_AM_THUC").Count);
        Equal(5, HotelEventRules.GetRequiredDepartments("DOAN_VIP").Count);

        // MICE requires IT, Wedding does not
        Equal(true, HotelEventRules.GetRequiredDepartments("HOI_NGHI_MICE").Contains("IT"));
        Equal(false, HotelEventRules.GetRequiredDepartments("TIEC_CUOI").Contains("IT"));
        Equal(true, HotelEventRules.GetRequiredDepartments("TIEC_CUOI").Contains("BUONG_PHONG"));
        Equal(false, HotelEventRules.GetRequiredDepartments("HOI_NGHI_MICE").Contains("BUONG_PHONG"));
    }),
    ("Department completeness and summary message format correctly", () =>
    {
        // Wedding: 6 departments: TIEN_SANH, AM_THUC, BEP, BUONG_PHONG, KY_THUAT, AN_NINH
        var staffWedding = new List<PhanCongDuAn>
        {
            new() { NhanVien = new NhanVien { MaPhongBan = 2, Ten = "FO" } },
            new() { NhanVien = new NhanVien { MaPhongBan = 4, Ten = "FB" } },
            new() { NhanVien = new NhanVien { MaPhongBan = 5, Ten = "Kitchen" } },
            new() { NhanVien = new NhanVien { MaPhongBan = 3, Ten = "HK" } },
            new() { NhanVien = new NhanVien { MaPhongBan = 6, Ten = "ENG" } }
            // Missing AN_NINH
        };
        var s1 = HotelEventRules.GetStaffingSummary("TIEC_CUOI", staffWedding);
        Equal(6, s1.TotalRequired);
        Equal(5, s1.SatisfiedCount);
        Equal(false, s1.IsFullyStaffed);
        Equal(1, s1.MissingDepartmentNames.Count);
        Equal("An ninh", s1.MissingDepartmentNames[0]);
        Equal("Đã đủ nhân sự 5/6 phòng ban — còn thiếu An ninh.", s1.SummaryText);

        // Add An ninh
        staffWedding.Add(new() { NhanVien = new NhanVien { MaPhongBan = 7, Ten = "Security" } });
        var s2 = HotelEventRules.GetStaffingSummary("TIEC_CUOI", staffWedding);
        Equal(6, s2.SatisfiedCount);
        Equal(true, s2.IsFullyStaffed);
        Equal(0, s2.MissingDepartmentNames.Count);
        Equal("Đã đủ nhân sự 6/6 phòng ban.", s2.SummaryText);

        // Empty staff
        var s3 = HotelEventRules.GetStaffingSummary("GALA_DINNER", []);
        Equal(5, s3.TotalRequired);
        Equal(0, s3.SatisfiedCount);
        Equal(false, s3.IsFullyStaffed);
        Equal(5, s3.MissingDepartmentNames.Count);
        Equal("Đã đủ nhân sự 0/5 phòng ban — còn thiếu Tiền sảnh, Ẩm thực (F&B), Bếp, Kỹ thuật, An ninh.", s3.SummaryText);
    })
};
var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
failed += RoleStaffingTests.Run();
if (failed != 0) Environment.Exit(1);

static NhanVien Employee() => new()
{
    NgayVaoLam = new DateOnly(2028, 2, 1),
    HopDongs = [new HopDong { NgayBatDau = new DateOnly(2028, 2, 1), NgayKetThuc = new DateOnly(2028, 3, 1) }]
};
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
