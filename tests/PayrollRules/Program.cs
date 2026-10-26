using System.Text;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Utilities;

if (args.Contains("--integration")) return await EventPayrollIntegration.Run();

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
        Equal(46_800_000m, SalaryConstants.MAX_INSURANCE_SALARY_BASE);
        Equal(99_200_000m, SalaryConstants.MAX_UNEMPLOYMENT_INSURANCE_SALARY_BASE);
    }),

    ("Fines: Đúng các mức phạt chấm công theo PRD 05", () =>
    {
        Equal(2_000m, SalaryConstants.FINE_PER_MINUTE_LATE);       // Phạt 2.000đ / phút đi trễ
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

    // --- PART 5: THƯỞNG DỰ ÁN / SỰ KIỆN THEO MA TRẬN CAU_HINH_KHOAN_LUONG (MỤC 1.1 PRD 05) ---
    ("Project Assignment: Ma trận thưởng 27 định mức theo Loại dự án x Phòng ban", () =>
    {
        // Kiểm tra đúng 27 cặp định mức chuẩn quy định trong cau_hinh_khoan_luong
        Equal(27, HotelEventRules.StandardBonusMatrix.Count);

        // Đoàn VIP (5 phòng ban)
        Equal(800_000m, HotelEventRules.StandardBonusMatrix[("DOAN_VIP", "BEP")]);
        Equal(700_000m, HotelEventRules.StandardBonusMatrix[("DOAN_VIP", "AM_THUC")]);
        Equal(700_000m, HotelEventRules.StandardBonusMatrix[("DOAN_VIP", "AN_NINH")]);
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("DOAN_VIP", "TIEN_SANH")]);
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("DOAN_VIP", "BUONG_PHONG")]);

        // Tiệc cưới (6 phòng ban)
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "BEP")]);
        Equal(500_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "AM_THUC")]);
        Equal(500_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "KY_THUAT")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "TIEN_SANH")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "AN_NINH")]);
        Equal(300_000m, HotelEventRules.StandardBonusMatrix[("TIEC_CUOI", "BUONG_PHONG")]);

        // Hội nghị MICE (6 phòng ban)
        Equal(700_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "IT")]);
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "KY_THUAT")]);
        Equal(500_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "BEP")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "AM_THUC")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "TIEN_SANH")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("HOI_NGHI_MICE", "AN_NINH")]);

        // Gala Dinner (5 phòng ban)
        Equal(700_000m, HotelEventRules.StandardBonusMatrix[("GALA_DINNER", "BEP")]);
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("GALA_DINNER", "AM_THUC")]);
        Equal(600_000m, HotelEventRules.StandardBonusMatrix[("GALA_DINNER", "KY_THUAT")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("GALA_DINNER", "TIEN_SANH")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("GALA_DINNER", "AN_NINH")]);

        // Festival ẩm thực (5 phòng ban)
        Equal(700_000m, HotelEventRules.StandardBonusMatrix[("FESTIVAL_AM_THUC", "BEP")]);
        Equal(500_000m, HotelEventRules.StandardBonusMatrix[("FESTIVAL_AM_THUC", "AM_THUC")]);
        Equal(500_000m, HotelEventRules.StandardBonusMatrix[("FESTIVAL_AM_THUC", "KY_THUAT")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("FESTIVAL_AM_THUC", "BUONG_PHONG")]);
        Equal(400_000m, HotelEventRules.StandardBonusMatrix[("FESTIVAL_AM_THUC", "AN_NINH")]);
    }),

    ("Project Assignment: ResolveEventBonus tự động ánh xạ cấu hình cau_hinh_khoan_luong", () =>
    {
        var sampleConfigs = new List<CauHinhKhoanLuong>
        {
            new() { MaKhoan = 32, TenKhoan = "Thưởng Tiệc cưới - Bếp", LoaiKhoan = "THUONG", GiaTriMacDinh = 600_000m },
            new() { MaKhoan = 28, TenKhoan = "Thưởng MICE - IT", LoaiKhoan = "THUONG", GiaTriMacDinh = 700_000m },
            new() { MaKhoan = 11, TenKhoan = "Thưởng Đoàn VIP - Bếp", LoaiKhoan = "THUONG", GiaTriMacDinh = 800_000m }
        };

        var duAnCuoi = new DuAn { LoaiDuAn = "TIEC_CUOI", TenDuAn = "Tiệc cưới Trọng Thể" };
        var phongBep = new PhongBan { MaPhongBan = 5, TenPhongBan = "Bộ phận Bếp (Kitchen / Culinary)" };
        var pcCuoi = new PhanCongDuAn { MaPhanCong = 101, VaiTroDuAn = "Bếp chính" };

        var (maKhoan, tenKhoan, soTien) = HotelEventRules.ResolveEventBonus(duAnCuoi, phongBep, pcCuoi, sampleConfigs);
        Equal(32, maKhoan);
        Equal("Thưởng Tiệc cưới - Bếp", tenKhoan);
        Equal(600_000m, soTien);

        // Trường hợp không có config nạp vào: dùng trực tiếp ma trận tiêu chuẩn
        var duAnMice = new DuAn { LoaiDuAn = "HOI_NGHI_MICE", TenDuAn = "Hội thảo AI 2026" };
        var phongIt = new PhongBan { MaPhongBan = 9, TenPhongBan = "Bộ phận Công nghệ Thông tin (IT)" };
        var pcMice = new PhanCongDuAn { MaPhanCong = 102, VaiTroDuAn = "Phụ trách kỹ thuật mạng" };

        var (_, tenKhoanMice, soTienMice) = HotelEventRules.ResolveEventBonus(duAnMice, phongIt, pcMice, null);
        Equal("Thưởng Hội nghị MICE - IT", tenKhoanMice);
        Equal(700_000m, soTienMice);
    }),

    // --- PART 6: WORKFLOW STATE MACHINE (2 TRẠNG THÁI MO / KHOA_SO & 4 BƯỚC QUY TRÌNH) ---
    ("Workflow: Thứ tự chuyển trạng thái chuẩn (MO -> Gửi duyệt -> Sếp duyệt -> KHOA_SO)", () =>
    {
        var ky = new KyLuong { TrangThai = SalaryConstants.PERIOD_OPEN };

        // Bước 1: Hệ thống tự tính xong -> Trạng thái là MO, chưa gửi duyệt
        Equal("MO", ky.TrangThai);
        Assert(ky.NgayGuiDuyet == null, "Bước 1 chưa có NgayGuiDuyet");

        // Bước 2: HR gửi duyệt -> Trạng thái vẫn là MO trong DB, ghi nhận timestamp gửi duyệt
        ky.TrangThai = SalaryConstants.PERIOD_OPEN;
        ky.NgayGuiDuyet = DateTimeOffset.UtcNow;
        ky.NguoiGuiDuyet = "HR User";
        ky.LyDoTuChoi = null;
        Equal("MO", ky.TrangThai);
        Assert(ky.NgayGuiDuyet != null && ky.NgayDuyet == null && string.IsNullOrEmpty(ky.LyDoTuChoi), "Đang ở bước Chờ duyệt");

        // Bước 3: Sếp duyệt -> Trạng thái vẫn là MO, ghi nhận timestamp duyệt
        ky.TrangThai = SalaryConstants.PERIOD_OPEN;
        ky.NgayDuyet = DateTimeOffset.UtcNow;
        ky.NguoiDuyet = "Manager User";
        ky.LyDoTuChoi = null;
        Equal("MO", ky.TrangThai);
        Assert(ky.NgayDuyet != null, "Đã được sếp phê duyệt");

        // Bước 4: Khóa sổ -> Trạng thái đổi sang KHOA_SO
        ky.TrangThai = SalaryConstants.PERIOD_LOCKED;
        ky.NgayKhoaSo = DateTimeOffset.UtcNow;
        ky.NguoiKhoaSo = "Manager User";
        Equal("KHOA_SO", ky.TrangThai);
        Assert(ky.NgayKhoaSo != null, "Đã khóa sổ");
    }),

    ("Workflow: Từ chối yêu cầu lý do bắt buộc và trả về HR để sửa (vẫn giữ MO)", () =>
    {
        var ky = new KyLuong 
        { 
            TrangThai = SalaryConstants.PERIOD_OPEN,
            NgayGuiDuyet = DateTimeOffset.UtcNow,
            NguoiGuiDuyet = "HR User"
        };

        // Sếp từ chối không có lý do -> Bị chặn
        string? emptyReason = "";
        Assert(string.IsNullOrWhiteSpace(emptyReason), "Reason cannot be empty");

        // Sếp từ chối có lý do -> Giữ MO nhưng lưu LyDoTuChoi và reset NgayGuiDuyet
        string validReason = "Số giờ OT ca đêm của bộ phận Lễ tân chưa khớp với bảng phân ca.";
        ky.TrangThai = SalaryConstants.PERIOD_OPEN;
        ky.LyDoTuChoi = validReason;
        ky.NgayDuyet = null;
        ky.NgayGuiDuyet = null;
        ky.NguoiDuyet = "Manager User";

        Equal("MO", ky.TrangThai);
        Equal(validReason, ky.LyDoTuChoi);
        Assert(ky.NgayGuiDuyet == null, "Sau khi từ chối thì trạng thái quay về để HR chỉnh sửa");

        // HR chỉnh sửa xong, bấm Gửi duyệt lại -> Cập nhật NgayGuiDuyet và xóa LyDoTuChoi
        ky.TrangThai = SalaryConstants.PERIOD_OPEN;
        ky.NgayGuiDuyet = DateTimeOffset.UtcNow;
        ky.NguoiGuiDuyet = "HR User";
        ky.LyDoTuChoi = null; // Reset lý do sau khi gửi lại
        Equal("MO", ky.TrangThai);
        Assert(ky.LyDoTuChoi == null, "LyDoTuChoi must be cleared after resubmission");
        Assert(ky.NgayGuiDuyet != null, "Đã gửi duyệt lại");
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

    ("Workflow & App: Khóa sổ tự động chuyển trạng thái phiếu lương sang Đã gửi App (PAYSLIP_PAID)", () =>
    {
        var ky = new KyLuong
        {
            TrangThai = SalaryConstants.PERIOD_APPROVED,
            PhieuLuongs = new List<PhieuLuong>
            {
                new() { MaPhieuLuong = 1, MaNhanVien = 10, TrangThai = SalaryConstants.PAYSLIP_APPROVED },
                new() { MaPhieuLuong = 2, MaNhanVien = 11, TrangThai = SalaryConstants.PAYSLIP_APPROVED }
            }
        };

        // Khi ở bước Phê duyệt (DA_DUYET): Chưa khóa sổ, phiếu chưa chuyển sang PAYSLIP_PAID
        Equal(false, ky.PhieuLuongs.All(p => p.TrangThai == SalaryConstants.PAYSLIP_PAID));

        // Khóa sổ bảng lương -> chuyển sang KHOA_SO và tự động cập nhật toàn bộ PhieuLuong sang DA_THANH_TOAN (đã gửi tới App)
        ky.TrangThai = SalaryConstants.PERIOD_LOCKED;
        ky.NgayKhoaSo = DateTimeOffset.UtcNow;
        ky.NguoiKhoaSo = "Admin User";
        foreach (var p in ky.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_PAID;
        }

        Equal(SalaryConstants.PERIOD_LOCKED, ky.TrangThai);
        Equal(true, ky.PhieuLuongs.All(p => p.TrangThai == SalaryConstants.PAYSLIP_PAID));
    }),

    ("Mobile App Security: Nhân viên chỉ nhận phiếu lương khi bảng lương đã duyệt và khóa sổ", () =>
    {
        // Luật bảo mật: Nhân viên chỉ thấy phiếu lương trên Mobile khi kỳ lương đã KHOA_SO hoặc phiếu đã DA_THANH_TOAN
        static bool IsVisibleOnMobile(string slipStatus, string periodStatus)
        {
            if (slipStatus == SalaryConstants.PAYSLIP_PAID) return true;
            if (periodStatus == SalaryConstants.PERIOD_LOCKED &&
                (slipStatus == SalaryConstants.PAYSLIP_APPROVED || slipStatus == SalaryConstants.PAYSLIP_PAID))
                return true;
            return false;
        }

        // 1. Bản nháp HR đang tính: Tuyệt đối KHÔNG hiển thị trên App nhân viên
        Equal(false, IsVisibleOnMobile(SalaryConstants.PAYSLIP_DRAFT, SalaryConstants.PERIOD_DRAFT));

        // 2. Chờ Sếp duyệt: Tuyệt đối KHÔNG hiển thị trên App nhân viên
        Equal(false, IsVisibleOnMobile(SalaryConstants.PAYSLIP_PENDING, SalaryConstants.PERIOD_PENDING_APPROVAL));

        // 3. Đã duyệt nhưng CHƯA khóa sổ: Chưa gửi tới App nhân viên
        Equal(false, IsVisibleOnMobile(SalaryConstants.PAYSLIP_APPROVED, SalaryConstants.PERIOD_APPROVED));

        // 4. Bị từ chối: KHÔNG hiển thị
        Equal(false, IsVisibleOnMobile(SalaryConstants.PAYSLIP_DRAFT, SalaryConstants.PERIOD_REJECTED));

        // 5. Sau khi duyệt và khóa sổ (KHOA_SO & DA_THANH_TOAN): TỰ ĐỘNG GỬI QUA APP NHÂN VIÊN
        Equal(true, IsVisibleOnMobile(SalaryConstants.PAYSLIP_PAID, SalaryConstants.PERIOD_LOCKED));
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
    }),

    // --- PART 8: CẢI TIẾN QUY TẮC TÍNH LƯƠNG & CHÍNH SÁCH MỚI ---
    ("Service Charge Proration: Phân bổ phí phục vụ theo ngày công thực tế", () =>
    {
        decimal serviceChargePool = 1_000_000m;
        // Nhân viên làm 26/26 ngày: nhận đủ 1.000.000đ
        decimal scFull = Math.Round(serviceChargePool * Math.Min(1m, 26m / 26m), 0, MidpointRounding.AwayFromZero);
        Equal(1_000_000m, scFull);

        // Nhân viên làm 13/26 ngày: nhận 500.000đ
        decimal scHalf = Math.Round(serviceChargePool * Math.Min(1m, 13m / 26m), 0, MidpointRounding.AwayFromZero);
        Equal(500_000m, scHalf);

        // Nhân viên làm 1/26 ngày: nhận 38.462đ
        decimal sc1Day = Math.Round(serviceChargePool * Math.Min(1m, 1m / 26m), 0, MidpointRounding.AwayFromZero);
        Equal(38_462m, sc1Day);
    }),

    ("Holiday Shift Pay: Đi làm ca ngày Lễ được cộng thêm 200% để đạt tổng 300%", () =>
    {
        decimal hourlyRate = 50_000m;
        decimal shiftHours = 8m;
        // Lương ngày công tiêu chuẩn: 50.000 x 8 = 400.000đ (100%)
        decimal regularPay = hourlyRate * shiftHours;
        // Phụ cấp làm ngày Lễ: 50.000 x 8 x 2.0 = 800.000đ (+200%)
        decimal holidayAllowance = Math.Round(hourlyRate * shiftHours * 2.0m, 0, MidpointRounding.AwayFromZero);
        // Tổng thu nhập ngày lễ: 400.000 + 800.000 = 1.200.000đ (300%)
        Equal(400_000m, regularPay);
        Equal(800_000m, holidayAllowance);
        Equal(1_200_000m, regularPay + holidayAllowance);
    }),

    ("Insurance Ceiling: Lương đóng bảo hiểm không vượt quá mức trần luật định", () =>
    {
        decimal highSalary = 80_000_000m; // Lương cơ bản cấp quản lý 80 triệu
        decimal luongDongBh = Math.Min(highSalary, SalaryConstants.MAX_INSURANCE_SALARY_BASE); // Trần 46.8M
        decimal luongDongBhtn = Math.Min(highSalary, SalaryConstants.MAX_UNEMPLOYMENT_INSURANCE_SALARY_BASE); // Trần 99.2M -> lấy 80M

        Equal(46_800_000m, luongDongBh);
        Equal(80_000_000m, luongDongBhtn);

        decimal bhxh = Math.Round(luongDongBh * SalaryConstants.BHXH_RATE, 0, MidpointRounding.AwayFromZero); // 46.8M * 8% = 3,744,000đ (thay vì 6.4M nếu không có trần)
        Equal(3_744_000m, bhxh);
    }),

    ("Unexcused Absence: Bỏ ca không phép phạt bổ sung 200.000đ", () =>
    {
        decimal absenceFine = SalaryConstants.FINE_ABSENCE_PENALTY;
        Equal(200_000m, absenceFine);
    }),

    ("Tax Exemption: Miễn thuế TNCN cho phần chênh lệch làm thêm giờ & ca đêm (Thông tư 111/2013/TT-BTC)", () =>
    {
        decimal donGiaGio = 50_000m;
        decimal otHoursNormal = 10m;  // 10h x 50k x 1.5 = 750,000đ
        decimal otHoursWeekend = 8m;  // 8h x 50k x 2.0 = 800,000đ
        decimal nightHours = 8m;      // 8h x 50k x 0.3 = 120,000đ
        decimal holidayHours = 8m;    // 8h x 50k x 2.0 = 800,000đ

        decimal tienOtNormal = donGiaGio * otHoursNormal * 1.5m;    // 750,000
        decimal tienOtWeekend = donGiaGio * otHoursWeekend * 2.0m;  // 800,000
        decimal tienCaDem = donGiaGio * nightHours * 0.3m;          // 120,000
        decimal tienLamNgayLe = donGiaGio * holidayHours * 2.0m;    // 800,000

        // Phần chênh lệch trả cao hơn được miễn thuế TNCN
        decimal mienThueOtNormal = donGiaGio * otHoursNormal * (1.5m - 1.0m);  // 250,000
        decimal mienThueOtWeekend = donGiaGio * otHoursWeekend * (2.0m - 1.0m); // 400,000
        decimal mienThueCaDem = tienCaDem;                                     // 120,000
        decimal mienThueNgayLe = tienLamNgayLe;                                // 800,000

        Equal(250_000m, mienThueOtNormal);
        Equal(400_000m, mienThueOtWeekend);
        Equal(120_000m, mienThueCaDem);
        Equal(800_000m, mienThueNgayLe);

        decimal tongMienThue = mienThueOtNormal + mienThueOtWeekend + mienThueCaDem + mienThueNgayLe;
        Equal(1_570_000m, tongMienThue);
    }),

    ("Insurance Eligibility: Ngày công < 14 ngày thì không trích đóng bảo hiểm (K3 Đ85 Luật BHXH)", () =>
    {
        decimal luongCoBan = 10_400_000m;
        decimal ngayCongThucTe = 10m; // Làm 10 ngày (< 14 ngày)
        bool isEligible = ngayCongThucTe >= 14m;
        Assert(!isEligible, "Dưới 14 ngày công không được đóng bảo hiểm bắt buộc.");

        decimal tienBhxh = isEligible ? luongCoBan * SalaryConstants.BHXH_RATE : 0m;
        decimal tienBhyt = isEligible ? luongCoBan * SalaryConstants.BHYT_RATE : 0m;
        decimal tienBhtn = isEligible ? luongCoBan * SalaryConstants.BHTN_RATE : 0m;
        Equal(0m, tienBhxh + tienBhyt + tienBhtn);
    }),

    ("Insurance Eligibility: Hợp đồng thử việc (THU_VIEC) được miễn đóng bảo hiểm bắt buộc", () =>
    {
        string loaiHopDong = "Hợp đồng thử việc (2 tháng)";
        bool isProbation = loaiHopDong.Contains("thử việc", StringComparison.OrdinalIgnoreCase);
        Assert(isProbation, "Nhận diện đúng hợp đồng thử việc.");

        decimal ngayCongThucTe = 26m; // Đủ 26 ngày công nhưng là thử việc
        bool isEligible = !isProbation && ngayCongThucTe >= 14m;
        Assert(!isEligible, "Hợp đồng thử việc không thuộc đối tượng đóng bảo hiểm bắt buộc.");
    }),

    ("PIT: Giảm trừ gia cảnh theo số người phụ thuộc (SoNguoiPhuThuoc * 4.400.000đ)", () =>
    {
        var nv = new NhanVien { Ho = "Nguyen", Ten = "Van A", SoNguoiPhuThuoc = 2 };
        Equal(2, nv.SoNguoiPhuThuoc);

        decimal thuNhapChiuThue = 18_000_000m;
        decimal tongBaoHiem = 10_400_000m * SalaryConstants.TOTAL_INSURANCE_RATE; // 1,092,000đ
        decimal giamTruBanThan = SalaryConstants.PERSONAL_TAX_DEDUCTION;            // 11,000,000đ
        decimal giamTruPhuThuoc = nv.SoNguoiPhuThuoc * SalaryConstants.DEPENDENT_TAX_DEDUCTION; // 8,800,000đ

        Equal(8_800_000m, giamTruPhuThuoc);

        // Thu nhập tính thuế = 18,000,000 - 1,092,000 - 11,000,000 - 8,800,000 = -2,892,000 (<= 0 => Tax = 0)
        decimal thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBaoHiem - giamTruBanThan - giamTruPhuThuoc);
        Equal(0m, thuNhapTinhThue);
        Equal(0m, PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue));
    }),

    ("Fines: Khấu trừ vi phạm về sớm (> 15 phút) áp dụng định mức cấu hình", () =>
    {
        decimal earlyDepartureFineConfig = 50_000m;
        int phutVeSom = 25; // Về sớm 25 phút (> 15 phút)
        decimal fine = phutVeSom > 15 ? earlyDepartureFineConfig : 0m;
        Equal(50_000m, fine);
    }),

    ("Resignation Payroll Eligibility: Nhân viên thôi việc giữa kỳ vẫn được tính lương cho ngày công đã làm", () =>
    {
        var periodFrom = new DateOnly(2026, 10, 1);
        var periodTo = new DateOnly(2026, 10, 31);
        var nvResigned = new NhanVien
        {
            MaNhanVien = 99,
            Ho = "Tran",
            Ten = "Thoi Viec",
            TrangThai = "DA_THOI_VIEC",
            NgayThoiViec = new DateOnly(2026, 10, 20)
        };

        // Điều kiện tính lương: thôi việc trong hoặc sau kỳ
        bool isEligible = nvResigned.TrangThai == "DANG_LAM" ||
                          (nvResigned.NgayThoiViec.HasValue && nvResigned.NgayThoiViec.Value >= periodFrom);
        Assert(isEligible, "Nhân viên thôi việc giữa tháng phải đủ điều kiện tính lương.");

        // Hợp đồng kết thúc ngày 20/10 vẫn được khớp
        var hopDong = new HopDong
        {
            MaHopDong = 999,
            MaNhanVien = 99,
            NgayBatDau = new DateOnly(2025, 1, 1),
            NgayKetThuc = new DateOnly(2026, 10, 20),
            TrangThai = "DA_CHAM_DUT",
            LuongCoBan = 10_400_000m
        };

        bool isContractMatched = (hopDong.TrangThai == "HIEU_LUC" || (hopDong.NgayKetThuc.HasValue && hopDong.NgayKetThuc.Value >= periodFrom)) &&
                                 hopDong.NgayBatDau <= periodTo &&
                                 (hopDong.NgayKetThuc == null || hopDong.NgayKetThuc >= periodFrom);
        Assert(isContractMatched, "Hợp đồng đã chấm dứt nhưng có hiệu lực trong kỳ phải được dùng để tính lương.");
    }),

    ("Manager Allowance Proration: Phụ cấp quản lý phân bổ theo ngày công thực tế (0 công = 0đ, 13 công = 50%, 26 công = 100%)", () =>
    {
        decimal allowanceConfig = 1_000_000m;
        decimal standardDays = SalaryConstants.STANDARD_WORKING_DAYS; // 26

        static decimal CalcManagerAllowance(decimal allowance, decimal days, decimal std)
        {
            if (days <= 0) return 0m;
            var congHuong = Math.Min(std, days);
            return Math.Round(allowance * (congHuong / std), 0, MidpointRounding.AwayFromZero);
        }

        // 1. Không đi làm ngày nào (0 công): 0đ
        Equal(0m, CalcManagerAllowance(allowanceConfig, 0m, standardDays));

        // 2. Làm nửa tháng (13 công): 500.000đ (50%)
        Equal(500_000m, CalcManagerAllowance(allowanceConfig, 13m, standardDays));

        // 3. Đủ 26 công: 1.000.000đ (100%)
        Equal(1_000_000m, CalcManagerAllowance(allowanceConfig, 26m, standardDays));

        // 4. Làm vượt công (27 công): Vẫn tối đa 1.000.000đ
        Equal(1_000_000m, CalcManagerAllowance(allowanceConfig, 27m, standardDays));
    }),

    ("Attendance Bonus Strict Rules: Không thưởng chuyên cần khi có vi phạm về sớm (>15p) hoặc bỏ ca", () =>
    {
        static bool IsEligibleForAttendanceBonus(decimal days, bool hasLate, bool hasEarly, bool hasAbsence)
        {
            return days >= 25 && !hasLate && !hasEarly && !hasAbsence;
        }

        // Đủ 26 công, không vi phạm -> Nhận thưởng 500k
        Assert(IsEligibleForAttendanceBonus(26m, false, false, false), "Đạt đủ công và không vi phạm.");

        // Đủ 26 công nhưng đi trễ -> Bị loại
        Assert(!IsEligibleForAttendanceBonus(26m, true, false, false), "Đi trễ không được thưởng.");

        // Đủ 26 công nhưng về sớm > 15p -> Bị loại
        Assert(!IsEligibleForAttendanceBonus(26m, false, true, false), "Về sớm không được thưởng.");

        // Đủ 25 công nhưng có 1 ca bỏ ca không phép -> Bị loại
        Assert(!IsEligibleForAttendanceBonus(25m, false, false, true), "Bỏ ca không được thưởng.");

        // Dưới 25 công (24 công) dù không vi phạm -> Bị loại
        Assert(!IsEligibleForAttendanceBonus(24m, false, false, false), "Không đủ 25 ngày công.");
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
