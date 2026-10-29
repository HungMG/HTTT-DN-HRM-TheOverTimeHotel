using OvertimeHotel.HRM.Core.Policies;

Console.WriteLine("==================================================================");
Console.WriteLine("OVERTIME HOTEL HRM - REPORT RULES ACCEPTANCE TESTS");
Console.WriteLine("PRD Reference: 02_Phan_Tich_Chuc_Nang_BFD.md - Phan He 10.0 (TSK-24)");
Console.WriteLine("==================================================================");

var tests = new (string Name, Action Run)[]
{
    // ══════════════════════════════════════════════════════════════════
    //  PART 1: PHÂN QUYỀN & PHẠM VI DỮ LIỆU (ma trận PRD 10.0)
    // ══════════════════════════════════════════════════════════════════

    ("RBAC: Admin / HR / IT xem toàn khách sạn", () =>
    {
        Equal(true, ReportPolicy.CanViewReports("Admin", null));
        Equal(true, ReportPolicy.CanViewReports("HR", null));
        Equal(true, ReportPolicy.CanViewReports("IT", null));

        Assert(ReportPolicy.ResolveScope("Admin", null).IsWholeHotel, "Admin phải xem toàn khách sạn.");
        Assert(ReportPolicy.ResolveScope("HR", null).IsWholeHotel, "HR phải xem toàn khách sạn.");
        Assert(ReportPolicy.ResolveScope("IT", 9).IsWholeHotel, "IT phải xem toàn khách sạn.");
    }),

    ("RBAC: Quản lý chỉ thấy bộ phận mình", () =>
    {
        var scope = ReportPolicy.ResolveScope("Manager", 4);

        Equal(true, scope.IsAllowed);
        Equal(false, scope.IsWholeHotel);
        Equal(4, scope.DepartmentId);

        // Tên vai trò trong CSDL do người quản trị gõ tay -> phải nhận cả cách viết tiếng Việt.
        Equal(true, ReportPolicy.CanViewReports("TRUONGPHONG", 7));
        Equal(7, ReportPolicy.ResolveScope("TRUONGPHONG", 7).DepartmentId);
    }),

    ("RBAC: Quản lý thiếu claim phòng ban -> TỪ CHỐI (fail-closed)", () =>
    {
        // Thà chặn nhầm còn hơn để lọt số liệu toàn khách sạn ra ngoài phạm vi được phép.
        Equal(false, ReportPolicy.CanViewReports("Manager", null));
        Equal(false, ReportPolicy.CanViewReports("Manager", 0));
        Equal(false, ReportPolicy.ResolveScope("Manager", null).IsAllowed);
        Equal(false, ReportPolicy.ResolveScope("Manager", 0).IsAllowed);
    }),

    ("RBAC: Employee và vai trò lạ bị TỪ CHỐI", () =>
    {
        Equal(false, ReportPolicy.CanViewReports("Employee", 4));
        Equal(false, ReportPolicy.CanViewReports("NHANVIEN", 4));
        Equal(false, ReportPolicy.CanViewReports(null, 4));
        Equal(false, ReportPolicy.CanViewReports("", 4));
        Equal(false, ReportPolicy.CanViewReports("   ", 4));
        Equal(false, ReportPolicy.CanViewReports("KeToan", 5)); // vai trò không tồn tại trong hệ thống
    }),

    ("RBAC: Báo cáo quỹ lương (10.4) chỉ dành cho Admin / HR", () =>
    {
        Equal(true, ReportPolicy.CanViewPayrollFundReport("Admin"));
        Equal(true, ReportPolicy.CanViewPayrollFundReport("HR"));

        // Quản lý bộ phận KHÔNG thấy quỹ lương - số liệu tài chính toàn khách sạn.
        Equal(false, ReportPolicy.CanViewPayrollFundReport("Manager"));
        Equal(false, ReportPolicy.CanViewPayrollFundReport("IT"));
        Equal(false, ReportPolicy.CanViewPayrollFundReport("Employee"));
        Equal(false, ReportPolicy.CanViewPayrollFundReport(null));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 2: KỲ BÁO CÁO (Tháng / Quý / Năm)
    // ══════════════════════════════════════════════════════════════════

    ("Kỳ báo cáo: theo Tháng", () =>
    {
        var (from, to, label) = ReportPolicy.ResolvePeriodRange(2026, null, 10);

        Equal(new DateOnly(2026, 10, 1), from);
        Equal(new DateOnly(2026, 10, 31), to);
        Equal("Tháng 10/2026", label);
    }),

    ("Kỳ báo cáo: theo Quý", () =>
    {
        var (from, to, label) = ReportPolicy.ResolvePeriodRange(2026, 4, null);

        Equal(new DateOnly(2026, 10, 1), from);
        Equal(new DateOnly(2026, 12, 31), to);
        Equal("Quý 4/2026", label);

        var quy1 = ReportPolicy.ResolvePeriodRange(2026, 1, null);
        Equal(new DateOnly(2026, 3, 31), quy1.To); // Quý 1 kết thúc 31/03
    }),

    ("Kỳ báo cáo: theo Năm", () =>
    {
        var (from, to, label) = ReportPolicy.ResolvePeriodRange(2026, null, null);

        Equal(new DateOnly(2026, 1, 1), from);
        Equal(new DateOnly(2026, 12, 31), to);
        Equal("Năm 2026", label);
    }),

    ("Kỳ báo cáo: tháng 2 năm nhuận cắt đúng 29 ngày", () =>
    {
        var (_, to, _) = ReportPolicy.ResolvePeriodRange(2028, null, 2);
        Equal(new DateOnly(2028, 2, 29), to);
    }),

    ("Kỳ báo cáo: Tháng ưu tiên hơn Quý khi client gửi kèm cả hai", () =>
    {
        // Lấy mức chi tiết nhất để không âm thầm nới rộng phạm vi số liệu.
        var (from, to, label) = ReportPolicy.ResolvePeriodRange(2026, 4, 10);

        Equal(new DateOnly(2026, 10, 1), from);
        Equal(new DateOnly(2026, 10, 31), to);
        Equal("Tháng 10/2026", label);
    }),

    ("Số tháng trong kỳ - dùng quy đổi định mức ngân sách", () =>
    {
        Equal(1, ReportPolicy.MonthsInPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
        Equal(3, ReportPolicy.MonthsInPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)));
        Equal(12, ReportPolicy.MonthsInPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Equal(0, ReportPolicy.MonthsInPeriod(new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1)));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 3: TRẠNG THÁI - nơi mã hóa các giá trị lệch giữa các tầng
    // ══════════════════════════════════════════════════════════════════

    ("Trạng thái: nhân viên thôi việc nhận CẢ HAI cách viết", () =>
    {
        // Ràng buộc CHECK của CSDL cho 'THOI_VIEC', nhưng form nhập Web ghi 'DA_THOI_VIEC'.
        // Bỏ sót một trong hai sẽ khiến báo cáo biến động đếm thiếu người đã nghỉ.
        Equal(true, ReportPolicy.IsResigned("THOI_VIEC"));
        Equal(true, ReportPolicy.IsResigned("DA_THOI_VIEC"));

        Equal(false, ReportPolicy.IsResigned("DANG_LAM"));
        Equal(false, ReportPolicy.IsResigned("NGHI_PHEP"));
        Equal(false, ReportPolicy.IsResigned(null));
        Equal(false, ReportPolicy.IsResigned(""));

        Equal(true, ReportPolicy.IsWorking("DANG_LAM"));
        Equal(false, ReportPolicy.IsWorking("DA_THOI_VIEC"));
    }),

    ("Trạng thái: vi phạm chuyên cần là trễ/sớm, KHÔNG gồm vắng mặt", () =>
    {
        Equal(true, ReportPolicy.IsViolation("DI_TRE"));
        Equal(true, ReportPolicy.IsViolation("VE_SOM"));
        Equal(true, ReportPolicy.IsViolation("TRE_VA_SOM"));

        Equal(false, ReportPolicy.IsViolation("DUNG_GIO"));
        Equal(false, ReportPolicy.IsViolation("VANG"));
        Equal(false, ReportPolicy.IsViolation(null));

        Equal(true, ReportPolicy.IsOnTime("DUNG_GIO"));
        Equal(false, ReportPolicy.IsOnTime("DI_TRE"));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 4: HỌC VẤN (PRD 10.3) - chuẩn hóa theo TỪ KHÓA
    // ══════════════════════════════════════════════════════════════════

    ("Học vấn: khớp đúng sáu nhãn chuẩn trong form nhập", () =>
    {
        Equal("Sau Đại học (Thạc sĩ / Tiến sĩ)", ReportPolicy.NormalizeEducation("Sau Đại học (Thạc sĩ / Tiến sĩ)"));
        Equal("Đại học", ReportPolicy.NormalizeEducation("Đại học"));
        Equal("Cao đẳng", ReportPolicy.NormalizeEducation("Cao đẳng"));
        Equal("Trung cấp", ReportPolicy.NormalizeEducation("Trung cấp"));
        Equal("Chứng chỉ nghề / Nghiệp vụ", ReportPolicy.NormalizeEducation("Chứng chỉ nghề / Nghiệp vụ"));
        Equal("Phổ thông (THPT)", ReportPolicy.NormalizeEducation("Phổ thông (THPT)"));
    }),

    ("Học vấn: phân loại ĐÚNG 20 hồ sơ thật trong seed_data_24_tables.sql", () =>
    {
        // Cột trinh_do là văn bản TỰ DO. So khớp bằng đẳng thức với sáu nhãn chuẩn sẽ
        // dồn gần hết hồ sơ vào "Khác" và làm báo cáo vô nghĩa. Test này khóa hành vi
        // khớp theo từ khóa lại.
        string[] hoSoThat =
        [
            "Thạc sĩ Quản trị Khách sạn",
            "Đại học Quản trị Nhân sự",
            "Đại học Du lịch",
            "Cao đẳng Lễ tân",
            "Đại học Kinh tế",
            "Trung cấp Nghề",
            "Trung cấp Nghề",
            "Đại học Du lịch",
            "Cao đẳng F&B",
            "Trung cấp Phục vụ",
            "Chứng chỉ Bếp Trưởng Quốc tế",
            "Cao đẳng Chế biến Món ăn",
            "Trung cấp Bếp",
            "Kỹ sư Cơ điện Bách Khoa",
            "Kỹ sư Điện lạnh",
            "Cao đẳng Kỹ thuật",
            "Sĩ quan An ninh giải ngũ",
            "Chứng chỉ Vệ sĩ Chuyên nghiệp",
            "Bộ đội xuất ngũ",
            "Đại học Ngoại ngữ"
        ];

        var thongKe = hoSoThat
            .Select(ReportPolicy.NormalizeEducation)
            .GroupBy(nhan => nhan)
            .ToDictionary(g => g.Key, g => g.Count());

        Equal(20, thongKe.Values.Sum());
        Equal(1, thongKe.GetValueOrDefault("Sau Đại học (Thạc sĩ / Tiến sĩ)"));
        Equal(7, thongKe.GetValueOrDefault("Đại học"));   // gồm cả 2 hồ sơ "Kỹ sư ..."
        Equal(4, thongKe.GetValueOrDefault("Cao đẳng"));
        Equal(4, thongKe.GetValueOrDefault("Trung cấp"));
        Equal(2, thongKe.GetValueOrDefault("Chứng chỉ nghề / Nghiệp vụ"));
        Equal(0, thongKe.GetValueOrDefault("Phổ thông (THPT)"));
        Equal(2, thongKe.GetValueOrDefault("Khác"));      // Sĩ quan, Bộ đội
    }),

    ("Học vấn: giá trị lạ, rỗng và 'Chưa cập nhật' gom vào Khác", () =>
    {
        Equal("Khác", ReportPolicy.NormalizeEducation("Chưa cập nhật"));
        Equal("Khác", ReportPolicy.NormalizeEducation(null));
        Equal("Khác", ReportPolicy.NormalizeEducation(""));
        Equal("Khác", ReportPolicy.NormalizeEducation("   "));

        // Chuỗi tự do có chứa từ khóa vẫn phải rơi đúng bậc.
        Equal("Cao đẳng", ReportPolicy.NormalizeEducation("Cao đẳng Công nghệ thông tin"));
    }),

    ("Học vấn: 'Sau Đại học' không bị nhánh 'Đại học' nuốt mất", () =>
    {
        // Thứ tự kiểm tra có ý nghĩa: xét "Đại học" trước sẽ nhận nhầm "Sau Đại học".
        Equal("Sau Đại học (Thạc sĩ / Tiến sĩ)", ReportPolicy.NormalizeEducation("Sau Đại học"));
        Equal("Sau Đại học (Thạc sĩ / Tiến sĩ)", ReportPolicy.NormalizeEducation("Thạc sĩ Quản trị Khách sạn"));
        Equal("Sau Đại học (Thạc sĩ / Tiến sĩ)", ReportPolicy.NormalizeEducation("Tiến sĩ Kinh tế"));
    }),

    ("Học vấn: tỷ lệ từ đại học trở lên = 40% (8/20 hồ sơ thật)", () =>
    {
        Equal(40.0m, ReportPolicy.HigherEducationRate(8, 20));
        Equal(0m, ReportPolicy.HigherEducationRate(0, 0));   // chặn chia 0
        Equal(0m, ReportPolicy.HigherEducationRate(5, 0));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 5: THÂM NIÊN (PRD 10.3)
    // ══════════════════════════════════════════════════════════════════

    ("Thâm niên: số tháng tròn giữa ngày vào làm và ngày mốc", () =>
    {
        Equal(2, ReportPolicy.TenureMonths(new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 17)));
        Equal(12, ReportPolicy.TenureMonths(new DateOnly(2025, 10, 17), new DateOnly(2026, 10, 17)));

        // Chưa tới ngày kỷ niệm trong tháng thì chưa tính là tròn tháng.
        Equal(11, ReportPolicy.TenureMonths(new DateOnly(2025, 10, 18), new DateOnly(2026, 10, 17)));

        // Ngày vào làm nằm ở tương lai -> 0, không trả số âm.
        Equal(0, ReportPolicy.TenureMonths(new DateOnly(2027, 1, 1), new DateOnly(2026, 10, 17)));
    }),

    ("Thâm niên: biên ba nhóm < 1 năm / 1-3 năm / > 3 năm", () =>
    {
        Equal("Dưới 1 năm", ReportPolicy.TenureBucketLabel(0));
        Equal("Dưới 1 năm", ReportPolicy.TenureBucketLabel(11));
        Equal("Từ 1 - 3 năm", ReportPolicy.TenureBucketLabel(12));   // biên dưới
        Equal("Từ 1 - 3 năm", ReportPolicy.TenureBucketLabel(35));
        Equal("Trên 3 năm", ReportPolicy.TenureBucketLabel(36));     // biên trên
        Equal("Trên 3 năm", ReportPolicy.TenureBucketLabel(120));
    }),

    ("Thâm niên: thứ tự hiển thị cố định của ba nhóm", () =>
    {
        Equal(3, ReportPolicy.TenureBucketOrder.Count);
        Equal("Dưới 1 năm", ReportPolicy.TenureBucketOrder[0]);
        Equal("Từ 1 - 3 năm", ReportPolicy.TenureBucketOrder[1]);
        Equal("Trên 3 năm", ReportPolicy.TenureBucketOrder[2]);
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 6: BIẾN ĐỘNG NHÂN SỰ (PRD 10.1)
    // ══════════════════════════════════════════════════════════════════

    ("Biến động: tỷ lệ thôi việc = thôi việc / quân số bình quân * 100", () =>
    {
        Equal(12.0m, ReportPolicy.TurnoverRate(100, 12));
        Equal(0m, ReportPolicy.TurnoverRate(100, 0));
        Equal(0m, ReportPolicy.TurnoverRate(0, 5));    // chặn chia 0
        Equal(0m, ReportPolicy.TurnoverRate(-1, 5));   // mẫu số vô lý
    }),

    ("Biến động: quân số bình quân làm tròn lên", () =>
    {
        Equal(10, ReportPolicy.AverageHeadcount(10, 10));
        Equal(11, ReportPolicy.AverageHeadcount(10, 11));   // 10.5 -> 11
        Equal(1, ReportPolicy.AverageHeadcount(0, 1));
        Equal(0, ReportPolicy.AverageHeadcount(0, 0));
        Equal(0, ReportPolicy.AverageHeadcount(-1, 5));     // chặn đầu vào vô lý
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 7: CHUYÊN CẦN (PRD 10.5)
    // ══════════════════════════════════════════════════════════════════

    ("Chuyên cần: tỷ lệ đúng giờ", () =>
    {
        Equal(95.0m, ReportPolicy.AttendanceRate(19, 20));
        Equal(100.0m, ReportPolicy.AttendanceRate(20, 20));
        Equal(0m, ReportPolicy.AttendanceRate(0, 0));   // chặn chia 0
        Equal(0m, ReportPolicy.AttendanceRate(5, 0));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 8: ĐỐI SOÁT QUỸ LƯƠNG (PRD 10.4)
    // ══════════════════════════════════════════════════════════════════

    ("Quỹ lương: chênh lệch, tỷ lệ giải ngân và cờ vượt định mức", () =>
    {
        var vuot = ReportPolicy.BudgetVariance(110m, 100m);
        Equal(10m, vuot.Variance);
        Equal(110.0m, vuot.Rate);
        Equal(true, vuot.Exceeded);

        var duoi = ReportPolicy.BudgetVariance(90m, 100m);
        Equal(-10m, duoi.Variance);
        Equal(90.0m, duoi.Rate);
        Equal(false, duoi.Exceeded);

        // Bằng đúng định mức thì KHÔNG phải vượt.
        var bang = ReportPolicy.BudgetVariance(100m, 100m);
        Equal(0m, bang.Variance);
        Equal(false, bang.Exceeded);
    }),

    ("Quỹ lương: định mức bằng 0 -> tỷ lệ 0, không chia cho 0", () =>
    {
        var ketQua = ReportPolicy.BudgetVariance(50m, 0m);

        Equal(50m, ketQua.Variance);
        Equal(0m, ketQua.Rate);          // không được là Infinity/NaN
        Equal(true, ketQua.Exceeded);
    }),

    ("Quỹ lương: ngưỡng cảnh báo đỏ là 10%", () =>
    {
        Equal(10m, ReportPolicy.BudgetAlertThresholdPercent);
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 9: SO SÁNH KỲ TRƯỚC & CÙNG KỲ NĂM TRƯỚC (Task 11)
    // ══════════════════════════════════════════════════════════════════

    ("So sánh: kỳ trước theo tháng (trong năm và lùi năm)", () =>
    {
        var t10 = ReportPolicy.ResolvePreviousPeriodRange(2026, null, 10);
        Equal(new DateOnly(2026, 9, 1), t10.From);
        Equal(new DateOnly(2026, 9, 30), t10.To);
        Equal("Tháng 09/2026", t10.Label);

        var t1 = ReportPolicy.ResolvePreviousPeriodRange(2026, null, 1);
        Equal(new DateOnly(2025, 12, 1), t1.From);
        Equal(new DateOnly(2025, 12, 31), t1.To);
        Equal("Tháng 12/2025", t1.Label);
    }),

    ("So sánh: kỳ trước theo quý và năm", () =>
    {
        var q4 = ReportPolicy.ResolvePreviousPeriodRange(2026, 4, null);
        Equal(new DateOnly(2026, 7, 1), q4.From);
        Equal(new DateOnly(2026, 9, 30), q4.To);
        Equal("Quý 3/2026", q4.Label);

        var q1 = ReportPolicy.ResolvePreviousPeriodRange(2026, 1, null);
        Equal(new DateOnly(2025, 10, 1), q1.From);
        Equal(new DateOnly(2025, 12, 31), q1.To);
        Equal("Quý 4/2025", q1.Label);

        var nam = ReportPolicy.ResolvePreviousPeriodRange(2026, null, null);
        Equal(new DateOnly(2025, 1, 1), nam.From);
        Equal(new DateOnly(2025, 12, 31), nam.To);
        Equal("Năm 2025", nam.Label);
    }),

    ("So sánh: cùng kỳ năm trước (YoY)", () =>
    {
        var t10YoY = ReportPolicy.ResolveSamePeriodLastYearRange(2026, null, 10);
        Equal(new DateOnly(2025, 10, 1), t10YoY.From);
        Equal(new DateOnly(2025, 10, 31), t10YoY.To);
        Equal("Tháng 10/2025", t10YoY.Label);

        var q2YoY = ReportPolicy.ResolveSamePeriodLastYearRange(2026, 2, null);
        Equal(new DateOnly(2025, 4, 1), q2YoY.From);
        Equal(new DateOnly(2025, 6, 30), q2YoY.To);
        Equal("Quý 2/2025", q2YoY.Label);
    }),

    ("So sánh: tính biến động CalculateChange", () =>
    {
        var tang = ReportPolicy.CalculateChange(120m, 100m);
        Equal(20m, tang.Absolute);
        Equal(20.0m, tang.Percent);
        Equal("tang", tang.Trend);

        var giam = ReportPolicy.CalculateChange(80m, 100m);
        Equal(-20m, giam.Absolute);
        Equal(-20.0m, giam.Percent);
        Equal("giam", giam.Trend);

        var bang = ReportPolicy.CalculateChange(100m, 100m);
        Equal(0m, bang.Absolute);
        Equal(0.0m, bang.Percent);
        Equal("khong_doi", bang.Trend);

        // Chặn chia cho 0 khi kỳ trước = 0
        var tuKhong = ReportPolicy.CalculateChange(50m, 0m);
        Equal(50m, tuKhong.Absolute);
        Equal(null, tuKhong.Percent);
        Equal("tang", tuKhong.Trend);

        var haiKhong = ReportPolicy.CalculateChange(0m, 0m);
        Equal(0m, haiKhong.Absolute);
        Equal(null, haiKhong.Percent);
        Equal("khong_doi", haiKhong.Trend);
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 10: THÂM NIÊN VỚI NHÂN VIÊN ĐÃ NGHỈ (Task 2)
    // ══════════════════════════════════════════════════════════════════

    ("Thâm niên: nhân viên thôi việc dừng tính tại ngày nghỉ", () =>
    {
        var vaoLam = new DateOnly(2024, 1, 15);
        var thoiViec = new DateOnly(2025, 7, 15); // làm đúng 18 tháng
        var asOf = new DateOnly(2026, 10, 1);     // mốc sau đó rất lâu

        // Nếu có ngày nghỉ trước mốc -> thâm niên chốt tại 18 tháng, không tăng tiếp thành 32 tháng
        Equal(18, ReportPolicy.TenureMonths(vaoLam, asOf, thoiViec));

        // Nếu chưa nghỉ (ngayThoiViec = null) -> tính bình thường tới asOf (32 tháng)
        Equal(32, ReportPolicy.TenureMonths(vaoLam, asOf, null));

        // Nếu ngày nghỉ nằm sau asOf -> tính tới asOf
        var thoiViecTuongLai = new DateOnly(2027, 1, 1);
        Equal(32, ReportPolicy.TenureMonths(vaoLam, asOf, thoiViecTuongLai));
    }),

    // ══════════════════════════════════════════════════════════════════
    //  PART 11: AN TOÀN XUẤT TỆP & CHỐNG CSV FORMULA INJECTION (Task 6)
    // ══════════════════════════════════════════════════════════════════

    ("An toàn CSV: vô hiệu hóa tiền tố công thức bảng tính", () =>
    {
        // Các ký tự nguy hiểm: =, +, -, @, \t, \r
        Equal("'=cmd|' /C calc'!A0", ReportPolicy.EscapeCsv("=cmd|' /C calc'!A0"));
        Equal("'+12345", ReportPolicy.EscapeCsv("+12345"));
        Equal("'-SUM(A1:A10)", ReportPolicy.EscapeCsv("-SUM(A1:A10)"));
        Equal("\"'@HYPERLINK(\"\"http://evil.com\"\")\"", ReportPolicy.EscapeCsv("@HYPERLINK(\"http://evil.com\")"));

        // Có dấu phẩy -> bọc ngoặc kép và thoát dấu nháy
        Equal("\"'=SUM(A1, B1)\"", ReportPolicy.EscapeCsv("=SUM(A1, B1)"));
        Equal("\"Phòng Lễ Tân, Buồng\"", ReportPolicy.EscapeCsv("Phòng Lễ Tân, Buồng"));
        Equal("\"Giám đốc \"\"VIP\"\"\"", ReportPolicy.EscapeCsv("Giám đốc \"VIP\""));

        // Chuỗi bình thường và chuỗi null/rỗng
        Equal("Phòng Nhân sự", ReportPolicy.EscapeCsv("Phòng Nhân sự"));
        Equal("", ReportPolicy.EscapeCsv(null));
        Equal("", ReportPolicy.EscapeCsv(""));
    })
};

int failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[PASS] ");
        Console.ResetColor();
        Console.WriteLine(test.Name);
    }
    catch (Exception ex)
    {
        failures++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[FAIL] ");
        Console.ResetColor();
        Console.WriteLine($"{test.Name}: {ex.Message}");
    }
}

Console.WriteLine("==================================================================");
Console.WriteLine($"Total: {tests.Length} tests. Passed: {tests.Length - failures}, Failed: {failures}.");
Console.WriteLine("==================================================================");
return failures == 0 ? 0 : 1;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"Expected {expected}; got {actual}.");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception($"Assertion failed: {message}");
    }
}
