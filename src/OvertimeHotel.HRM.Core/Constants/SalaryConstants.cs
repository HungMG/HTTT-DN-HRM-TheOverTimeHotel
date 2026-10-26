namespace OvertimeHotel.HRM.Core.Constants;

public static class SalaryConstants
{
    public const int STANDARD_WORKING_DAYS = 26;
    public const int STANDARD_WORKING_HOURS_PER_DAY = 8;

    public const decimal NIGHT_SHIFT_ALLOWANCE_RATE = 0.30m; // Phụ cấp ca đêm 30%
    public const decimal OVERTIME_NORMAL_RATE = 1.50m;       // Hệ số làm thêm ngày thường 150%
    public const decimal OVERTIME_WEEKEND_RATE = 2.00m;      // Hệ số làm thêm ngày nghỉ 200%
    public const decimal OVERTIME_HOLIDAY_RATE = 3.00m;      // Hệ số làm thêm ngày lễ 300%

    // Tỷ lệ trích đóng bảo hiểm bắt buộc của người lao động (Tổng cộng 10.5%)
    public const decimal BHXH_RATE = 0.08m;                  // BHXH 8%
    public const decimal BHYT_RATE = 0.015m;                 // BHYT 1.5%
    public const decimal BHTN_RATE = 0.01m;                  // BHTN 1.0%
    public const decimal TOTAL_INSURANCE_RATE = 0.105m;      // Tổng cộng 10.5%

    // Mức trần tiền lương đóng bảo hiểm theo luật
    public const decimal MAX_INSURANCE_SALARY_BASE = 46800000m;              // 20 lần lương cơ sở (2.340.000đ x 20) cho BHXH, BHYT
    public const decimal MAX_UNEMPLOYMENT_INSURANCE_SALARY_BASE = 99200000m; // 20 lần lương tối thiểu vùng 1 (4.960.000đ x 20) cho BHTN

    // Định mức phụ cấp ăn ca miễn thuế TNCN
    public const decimal MEAL_ALLOWANCE_PER_DAY = 30000m;    // 30.000 VNĐ / ngày công thực tế

    // Giảm trừ gia cảnh thuế TNCN
    public const decimal PERSONAL_TAX_DEDUCTION = 11000000m; // 11.000.000 VNĐ/tháng
    public const decimal DEPENDENT_TAX_DEDUCTION = 4400000m; // 4.400.000 VNĐ/tháng/người

    // Phạt vi phạm kỷ luật chấm công
    public const decimal FINE_PER_MINUTE_LATE = 2000m;       // Phạt 2.000 VNĐ / phút đi trễ (theo cau_hinh_khau_tru ma 5)
    public const decimal FINE_LATE_1_TO_15_MINS = 50000m;    // [Legacy] Đi trễ 1-15 phút: 50.000 VNĐ/lần
    public const decimal FINE_LATE_16_TO_30_MINS = 100000m;  // [Legacy] Đi trễ 16-30 phút: 100.000 VNĐ/lần
    public const decimal FINE_ABSENCE_PENALTY = 200000m;     // Phạt bổ sung bỏ ca: 200.000 VNĐ/lần

    // Hệ số thù lao sự kiện khách sạn
    public const decimal EVENT_EXCELLENT_BONUS_RATE = 0.10m; // Thưởng đánh giá xuất sắc +10%

    // Trạng thái kỳ lương (Chỉ gồm 2 trạng thái duy nhất: MO và KHOA_SO)
    public const string PERIOD_OPEN = "MO";
    public const string PERIOD_LOCKED = "KHOA_SO";

    // Tương thích ngược: toàn bộ giai đoạn chưa khóa sổ đều thuộc trạng thái MO
    public const string PERIOD_DRAFT = "MO";
    public const string PERIOD_PENDING_APPROVAL = "MO";
    public const string PERIOD_APPROVED = "MO";
    public const string PERIOD_REJECTED = "MO";

    // Trạng thái phiếu lương
    public const string PAYSLIP_DRAFT = "NHAP";
    public const string PAYSLIP_PENDING = "CHO_DUYET";
    public const string PAYSLIP_APPROVED = "DA_DUYET";
    public const string PAYSLIP_PAID = "DA_THANH_TOAN";
}
