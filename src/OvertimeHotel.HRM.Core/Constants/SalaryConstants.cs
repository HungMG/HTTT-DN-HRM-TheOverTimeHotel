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
}
