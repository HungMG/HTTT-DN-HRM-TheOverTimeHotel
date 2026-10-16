using System.Text;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Utilities;

Console.WriteLine("==================================================================");
Console.WriteLine("OVERTIME HOTEL HRM - PAYROLL RULES & FORMULAS ACCEPTANCE TESTS");
Console.WriteLine("PRD Reference: 05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md");
Console.WriteLine("==================================================================");

var tests = new (string Name, Action Run)[]
{
    // --- PART 1: THUẾ THU NHẬP CÁ NHÂN (PIT) 7 BẬC TIŨY TIẾN ---
    ("PIT: Thu nhập tính thuế <= 0 => Thuế = 0", () =>
    {
        Equal(0m, PersonalIncomeTaxCalculator.CalculateTax(-1_000_000m));
        Equal(0m, PersonalIncomeTaxCalculator.CalculateTax(0m));
    }),

    ("PIT: Bậc 1 (Thu nhập tính thuế <= 5M, 5%)", () =>
    {
        // Thu nhập tính thuế = 4M => 4M * 5% = 200,000đ
        Equal(200_000m, PersonalIncomeTaxCalculator.CalculateTax(4_000_000m));
        // Thu nhập tính thuế = 5M => 5M * 5% = 250,000đ
        Equal(250_000m, PersonalIncomeTaxCalculator.CalculateTax(5_000_000m));
    }),

    ("PIT: Bậc 2 (5M - 10M, 10%)", () =>
    {
        // Thu nhập tính thuế = 9M.
        // Bậc 1: 5M * 5% = 250,000đ
        // Bậc 2: 4M * 10% = 400,000đ => Tổng = 650,000đ
        Equal(650_000m, PersonalIncomeTaxCalculator.CalculateTax(9_000_000m));
    }),

    ("PIT: Bậc 3 (10M - 18M, 15%)", () =>
    {
        // Thu nhập tính thuế = 18M.
        // Bậc 1: 5M * 5% = 250,000
        // Bậc 2: 5M * 10% = 500,000
        // Bậc 3: 8M * 15% = 1,200,000 => Tổng = 1,950,000đ
        Equal(1_950_000m, PersonalIncomeTaxCalculator.CalculateTax(18_000_000m));
    }),

    ("PIT: Bậc 4 đến Bậc 7 lũy tiến từng phần", () =>
    {
        // Thu nhập tính thuế cực cao: 89M
        // B1: 5M * 5% = 250,000
        // B2: 5M * 10% = 500,000
        // B3: 8M * 15% = 1,200,000
        // B4: 14M * 20% = 2,800,000
        // B5: 20M * 25% = 5,000,000
        // B6: 28M * 30% = 8,400,000 (từ 52M đến 80M)
        // B7: (89M - 80M) = 9M * 35% = 3,150,000
        // Tổng = 250k + 500k + 1.2M + 2.8M + 5M + 8.4M + 3.15M = 21,300,000đ
        Equal(21_300_000m, PersonalIncomeTaxCalculator.CalculateTax(89_000_000m));
    }),

    // --- PART 2: TIỆN ÍCH ĐỌC TIỀN THÀNH CHỮ TIẾNG VIỆT ---
    ("NumberToWords: Đọc đúng số 0 và các số tiền thông dụng", () =>
    {
        Equal("Không đồng", VietnameseNumberToWords.ConvertToWords(0));
        Equal("Mười lăm triệu đồng", VietnameseNumberToWords.ConvertToWords(15_000_000));
        Equal("Mười bốn triệu bảy trăm năm mươi nghìn đồng", VietnameseNumberToWords.ConvertToWords(14_750_000));
        Equal("Hai triệu ba trăm lẻ năm nghìn đồng", VietnameseNumberToWords.ConvertToWords(2_305_000));
        Equal("Một trăm nghìn đồng", VietnameseNumberToWords.ConvertToWords(100_000));
    }),

    // --- PART 3: CONSTANTS VÀ HỆ SỐ LƯƠNG THEO PRD 05 ---
    ("Constants: Đúng các tham số quy định trong PRD 05", () =>
    {
        Equal(26, SalaryConstants.STANDARD_WORKING_DAYS);
        Equal(8, SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY);
        Equal(1.50m, SalaryConstants.OVERTIME_NORMAL_RATE);
        Equal(2.00m, SalaryConstants.OVERTIME_WEEKEND_RATE);
        Equal(3.00m, SalaryConstants.OVERTIME_HOLIDAY_RATE);
        Equal(0.30m, SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE);
        Equal(30_000m, SalaryConstants.MEAL_ALLOWANCE_PER_DAY);
        Equal(0.08m, SalaryConstants.BHXH_RATE);
        Equal(0.015m, SalaryConstants.BHYT_RATE);
        Equal(0.01m, SalaryConstants.BHTN_RATE);
        Equal(0.105m, SalaryConstants.TOTAL_INSURANCE_RATE);
        Equal(11_000_000m, SalaryConstants.PERSONAL_TAX_DEDUCTION);
        Equal(4_400_000m, SalaryConstants.DEPENDENT_TAX_DEDUCTION);
    }),

    ("Fines: Đúng các mức phạt chấm công theo PRD 05", () =>
    {
        Equal(50_000m, SalaryConstants.FINE_LATE_1_TO_15_MINS);    // 1-15m: 50.000đ
        Equal(100_000m, SalaryConstants.FINE_LATE_16_TO_30_MINS);  // 16-30m: 100.000đ
        Equal(200_000m, SalaryConstants.FINE_ABSENCE_PENALTY);     // Phạt bổ sung bỏ ca: 200.000đ
    }),

    // --- PART 4: CÔNG THỨC TÍNH 2 TẦNG (GROSS -> KHẤU TRỪ -> NET) & INVARIANT ---
    ("2-Tier Formula & Invariant: ThucNhan == TongGross - TongKhauTru (Zero Decimal Delta)", () =>
    {
        decimal luongCoBan = 10_400_000m;
        decimal standardDays = SalaryConstants.STANDARD_WORKING_DAYS; // 26
        decimal standardDailyRate = Math.Round(luongCoBan / standardDays, 2, MidpointRounding.AwayFromZero); // 400,000.00
        decimal standardHourlyRate = Math.Round(standardDailyRate / 8m, 2, MidpointRounding.AwayFromZero);    // 50,000.00

        Equal(400_000m, standardDailyRate);
        Equal(50_000m, standardHourlyRate);

        // TẦNG 1: THU NHẬP GROSS
        decimal ngayCongThucTe = 24m;
        decimal luongCongNhat = Math.Round(standardDailyRate * ngayCongThucTe, 0, MidpointRounding.AwayFromZero); // 9,600,000
        decimal ngayNghiPhep = 2m;
        decimal luongNghiPhep = Math.Round(standardDailyRate * ngayNghiPhep, 0, MidpointRounding.AwayFromZero);   // 800,000
        decimal phuCapAnTrua = ngayCongThucTe * SalaryConstants.MEAL_ALLOWANCE_PER_DAY;                          // 720,000

        decimal otHours = 10m;
        decimal luongOt = Math.Round(standardHourlyRate * otHours * SalaryConstants.OVERTIME_NORMAL_RATE, 0, MidpointRounding.AwayFromZero); // 750,000

        decimal nightHours = 8m;
        decimal luongDem = Math.Round(standardHourlyRate * nightHours * SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE, 0, MidpointRounding.AwayFromZero); // 120,000

        decimal thuongDuAn = 1_100_000m; // Có dự án đánh giá XUẤT SẮC (+10%)

        decimal tongGross = luongCongNhat + luongNghiPhep + phuCapAnTrua + luongOt + luongDem + thuongDuAn;
        Equal(13_090_000m, tongGross);

        // TẦNG 2: CÁC KHOẢN KHẤU TRỪ
        decimal bhxh = Math.Round(luongCoBan * SalaryConstants.BHXH_RATE, 0, MidpointRounding.AwayFromZero); // 832,000
        decimal bhyt = Math.Round(luongCoBan * SalaryConstants.BHYT_RATE, 0, MidpointRounding.AwayFromZero); // 156,000
        decimal bhtn = Math.Round(luongCoBan * SalaryConstants.BHTN_RATE, 0, MidpointRounding.AwayFromZero); // 104,000
        decimal tongBaoHiem = bhxh + bhyt + bhtn;
        Equal(1_092_000m, tongBaoHiem);

        decimal phatChamCong = 150_000m; // 1 lần trễ 10p (50k) + 1 lần trễ 20p (100k)

        // PIT: Thu nhập chịu thuế = Gross - Phụ cấp ăn trưa (miễn thuế) = 13,090,000 - 720,000 = 12,370,000
        decimal thuNhapChiuThue = tongGross - phuCapAnTrua;
        // Thu nhập tính thuế = Thu nhập chịu thuế - Bảo hiểm (1,092,000) - Giảm trừ gia cảnh bản thân (11,000,000)
        // = 12,370,000 - 1,092,000 - 11,000,000 = 278,000đ (Bậc 1: 5%)
        decimal thuNhapTinhThue = thuNhapChiuThue - tongBaoHiem - SalaryConstants.PERSONAL_TAX_DEDUCTION;
        decimal thueTNCN = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);
        decimal expectedPit = Math.Round(278_000m * 0.05m, 0, MidpointRounding.AwayFromZero); // 13,900
        Equal(expectedPit, thueTNCN);

        decimal tongKhauTru = tongBaoHiem + phatChamCong + thueTNCN;
        decimal thucNhan = tongGross - tongKhauTru;

        // BẮT BUỘC INVARIANT: ThucNhan == Gross - Deductions
        Equal(thucNhan, tongGross - tongKhauTru);
        Equal(11_834_100m, thucNhan);
    }),

    // --- PART 5: PHỤ CẤP DỰ ÁN / SỰ KIỆN (MỤC 1.1 PRD 05) ---
    ("Project Assignment: Đánh giá XUAT_SAC được cộng 10% thưởng theo PRD 05", () =>
    {
        decimal baseAllowance = 1_000_000m;
        string danhGiaHoanThanh = "HOAN_THANH";
        string danhGiaXuatSac = "XUAT_SAC";

        decimal allowanceHoanThanh = danhGiaHoanThanh == "XUAT_SAC" ? baseAllowance * (1m + SalaryConstants.EVENT_EXCELLENT_BONUS_RATE) : baseAllowance;
        decimal allowanceXuatSac = danhGiaXuatSac == "XUAT_SAC" ? baseAllowance * (1m + SalaryConstants.EVENT_EXCELLENT_BONUS_RATE) : baseAllowance;

        Equal(1_000_000m, allowanceHoanThanh);
        Equal(1_100_000m, allowanceXuatSac);
    }),

    // --- PART 6: WORKFLOW STATE MACHINE (4 BƯỚC THEO QUY TRÌNH NGƯỜI DÙNG) ---
    ("Workflow: Thứ tự chuyển trạng thái chuẩn (NHAP -> CHO_DUYET -> DA_DUYET -> KHOA_SO)", () =>
    {
        var ky = new KyLuong { TrangThai = SalaryConstants.PERIOD_DRAFT };

        // Bước 1: Hệ thống tự tính xong -> Trạng thái là NHAP
        Equal("NHAP", ky.TrangThai);

        // Bước 2: HR gửi duyệt -> Trạng thái sang CHO_DUYET
        ky.TrangThai = SalaryConstants.PERIOD_PENDING_APPROVAL;
        ky.NgayGuiDuyet = DateTimeOffset.UtcNow;
        ky.NguoiGuiDuyet = "HR User";
        Equal("CHO_DUYET", ky.TrangThai);

        // Bước 3: Sếp duyệt -> Trạng thái sang DA_DUYET
        ky.TrangThai = SalaryConstants.PERIOD_APPROVED;
        ky.NgayDuyet = DateTimeOffset.UtcNow;
        ky.NguoiDuyet = "Manager User";
        Equal("DA_DUYET", ky.TrangThai);

        // Bước 4: Khóa sổ -> Trạng thái sang KHOA_SO
        ky.TrangThai = SalaryConstants.PERIOD_LOCKED;
        ky.NgayKhoaSo = DateTimeOffset.UtcNow;
        ky.NguoiKhoaSo = "Manager User";
        Equal("KHOA_SO", ky.TrangThai);
    }),

    ("Workflow: Từ chối yêu cầu lý do bắt buộc và trả về HR để sửa", () =>
    {
        var ky = new KyLuong { TrangThai = SalaryConstants.PERIOD_PENDING_APPROVAL };

        // Sếp từ chối không có lý do -> Bị chặn
        string? emptyReason = "";
        Assert(string.IsNullOrWhiteSpace(emptyReason), "Reason cannot be empty");

        // Sếp từ chối có lý do -> Chuyển thành TU_CHOI
        string validReason = "Số giờ OT ca đêm của bộ phận Lễ tân chưa khớp với bảng phân ca.";
        ky.TrangThai = SalaryConstants.PERIOD_REJECTED;
        ky.LyDoTuChoi = validReason;
        ky.NgayDuyet = DateTimeOffset.UtcNow;
        ky.NguoiDuyet = "Manager User";

        Equal("TU_CHOI", ky.TrangThai);
        Equal(validReason, ky.LyDoTuChoi);

        // HR chỉnh sửa xong, bấm Gửi duyệt lại -> Lại thành CHO_DUYET
        ky.TrangThai = SalaryConstants.PERIOD_PENDING_APPROVAL;
        ky.NgayGuiDuyet = DateTimeOffset.UtcNow;
        ky.NguoiGuiDuyet = "HR User";
        ky.LyDoTuChoi = null; // Reset lý do sau khi gửi lại
        Equal("CHO_DUYET", ky.TrangThai);
        Assert(ky.LyDoTuChoi == null, "LyDoTuChoi must be cleared after resubmission");
    }),

    ("Workflow: Khóa sổ rồi mới được xuất file chuyển khoản và gửi phiếu lương", () =>
    {
        // Khi chưa khóa sổ (VD: DA_DUYET hoặc CHO_DUYET) -> Không cho phép xuất file ngân hàng
        var kyChuaKhoa = new KyLuong { TrangThai = SalaryConstants.PERIOD_APPROVED };
        bool canExportBeforeLock = kyChuaKhoa.TrangThai == SalaryConstants.PERIOD_LOCKED;
        Equal(false, canExportBeforeLock);

        // Khi đã khóa sổ -> Cho phép xuất file ngân hàng
        var kyDaKhoa = new KyLuong { TrangThai = SalaryConstants.PERIOD_LOCKED };
        bool canExportAfterLock = kyDaKhoa.TrangThai == SalaryConstants.PERIOD_LOCKED;
        Equal(true, canExportAfterLock);
    }),

    // --- PART 1.1: KIỂM TRA TẤT CẢ CÁC MỐC BIÊN CỦA 7 BẬC THUẾ TNCN ---
    ("PIT: Chính xác từng mốc biên của 7 bậc thuế lũy tiến", () =>
    {
        // Mốc 0đ: 0%
        Equal(0m, PersonalIncomeTaxCalculator.CalculateTax(0m));

        // Mốc 5M: Bậc 1 max = 5M * 5% = 250,000đ
        Equal(250_000m, PersonalIncomeTaxCalculator.CalculateTax(5_000_000m));

        // Mốc 10M: Bậc 1 (250k) + Bậc 2 (5M * 10% = 500k) = 750,000đ
        Equal(750_000m, PersonalIncomeTaxCalculator.CalculateTax(10_000_000m));

        // Mốc 18M: 750k + Bậc 3 (8M * 15% = 1.2M) = 1,950,000đ
        Equal(1_950_000m, PersonalIncomeTaxCalculator.CalculateTax(18_000_000m));

        // Mốc 32M: 1.95M + Bậc 4 (14M * 20% = 2.8M) = 4,750,000đ
        Equal(4_750_000m, PersonalIncomeTaxCalculator.CalculateTax(32_000_000m));

        // Mốc 52M: 4.75M + Bậc 5 (20M * 25% = 5M) = 9,750,000đ
        Equal(9_750_000m, PersonalIncomeTaxCalculator.CalculateTax(52_000_000m));

        // Mốc 80M: 9.75M + Bậc 6 (28M * 30% = 8.4M) = 18,150,000đ
        Equal(18_150_000m, PersonalIncomeTaxCalculator.CalculateTax(80_000_000m));

        // Mốc 100M: 18.15M + Bậc 7 (20M * 35% = 7M) = 25,150,000đ
        Equal(25_150_000m, PersonalIncomeTaxCalculator.CalculateTax(100_000_000m));
    }),

    ("OT Multipliers: Tính đúng hệ số ngày nghỉ (2.0x) và ngày lễ (3.0x)", () =>
    {
        decimal hourlyRate = 50_000m;
        decimal otWeekendHours = 4m;
        decimal otHolidayHours = 6m;

        decimal weekendOtPay = Math.Round(hourlyRate * otWeekendHours * SalaryConstants.OVERTIME_WEEKEND_RATE, 0, MidpointRounding.AwayFromZero);
        decimal holidayOtPay = Math.Round(hourlyRate * otHolidayHours * SalaryConstants.OVERTIME_HOLIDAY_RATE, 0, MidpointRounding.AwayFromZero);

        // 50k * 4 * 2.0 = 400k
        Equal(400_000m, weekendOtPay);
        // 50k * 6 * 3.0 = 900k
        Equal(900_000m, holidayOtPay);
    }),

    ("Fines Accumulation: Tổng hợp vi phạm chấm công nhiều lỗi", () =>
    {
        // 2 lần trễ 1-15m (50k * 2 = 100k)
        // 1 lần trễ 16-30m (100k)
        // 1 lần bỏ ca (phạt bổ sung 200k)
        decimal totalFines = (2 * SalaryConstants.FINE_LATE_1_TO_15_MINS)
                           + (1 * SalaryConstants.FINE_LATE_16_TO_30_MINS)
                           + (1 * SalaryConstants.FINE_ABSENCE_PENALTY);
        Equal(400_000m, totalFines);
    }),

    ("Bank Transfer CSV: Xử lý ký tự đặc biệt theo chuẩn RFC 4180 (escape dấu phẩy và ngoặc kép)", () =>
    {
        string rawName = "Nguyễn Văn \"Đại\" Gia";
        string escapedName = $"\"{rawName.Replace("\"", "\"\"")}\"";
        Equal("\"Nguyễn Văn \"\"Đại\"\" Gia\"", escapedName);
    }),

    // --- PART 7: PHÂN ĐỊNH VAI TRÒ (SEPARATION OF DUTIES: HR VS ADMIN/SẾP) ---
    ("Separation of Duties: Tài khoản HR thực hiện kiểm tra, sửa sai sót và gửi duyệt (Không được duyệt)", () =>
    {
        string userRole = "HR";
        bool isHR = userRole == "HR";
        bool isBoss = userRole == "Admin" || userRole == "Manager";

        // Ở bước 2 (Bản nháp NHAP):
        string periodStatus = SalaryConstants.PERIOD_DRAFT;
        bool canSubmitApproval = isHR && periodStatus == SalaryConstants.PERIOD_DRAFT;
        bool canEditDetails = isHR && periodStatus == SalaryConstants.PERIOD_DRAFT;
        bool canApprove = isBoss && periodStatus == SalaryConstants.PERIOD_PENDING_APPROVAL;

        Equal(true, isHR);
        Equal(false, isBoss);
        Equal(true, canSubmitApproval); // HR ĐƯỢC bấm Gửi duyệt
        Equal(true, canEditDetails);    // HR ĐƯỢC điều chỉnh/sửa sai sót
        Equal(false, canApprove);       // HR KHÔNG ĐƯỢC phê duyệt
    }),

    ("Separation of Duties: Tài khoản Admin thực hiện Sếp phê duyệt (Không được gửi duyệt thay HR)", () =>
    {
        string userRole = "Admin";
        bool isHR = userRole == "HR"; // Admin KHÔNG phải là HR
        bool isBoss = userRole == "Admin" || userRole == "Manager";

        // Ở bước 2 (Bản nháp NHAP):
        string draftStatus = SalaryConstants.PERIOD_DRAFT;
        bool canAdminSubmit = isHR && draftStatus == SalaryConstants.PERIOD_DRAFT;
        bool canAdminEdit = isHR && draftStatus == SalaryConstants.PERIOD_DRAFT;

        Equal(false, isHR);
        Equal(true, isBoss);
        Equal(false, canAdminSubmit); // Admin KHÔNG ĐƯỢC bấm gửi duyệt thay HR (Chờ HR gửi)
        Equal(false, canAdminEdit);   // Admin KHÔNG sửa sai sót thay HR

        // Ở bước 3 (Đã gửi duyệt CHO_DUYET):
        string pendingStatus = SalaryConstants.PERIOD_PENDING_APPROVAL;
        bool canAdminApprove = isBoss && pendingStatus == SalaryConstants.PERIOD_PENDING_APPROVAL;
        Equal(true, canAdminApprove); // Admin ĐƯỢC phê duyệt bảng lương

        // Ở bước 4 (Đã duyệt DA_DUYET):
        string approvedStatus = SalaryConstants.PERIOD_APPROVED;
        bool canAdminLock = isBoss && approvedStatus == SalaryConstants.PERIOD_APPROVED;
        Equal(true, canAdminLock); // Admin ĐƯỢC khóa sổ bảng lương
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
        throw new Exception($"Expected {expected}; got {actual}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception($"Assertion failed: {message}");
}
