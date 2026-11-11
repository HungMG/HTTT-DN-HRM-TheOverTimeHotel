namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Đại diện cho thông tin quỹ lương của một tháng trong danh sách 5 tháng gần nhất hiển thị cho Ban Giám Đốc.
/// </summary>
public class AdminRecentMonthPayrollItem
{
    public string MonthName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string StatusBgColor { get; set; } = "#F1F5F9";
    public string StatusTextColor { get; set; } = "#64748B";
    public string TotalPayoutText { get; set; } = "0 đ";
    public string SubtitleText { get; set; } = string.Empty;
    public double BarProgress { get; set; }
    public string BarColor { get; set; } = "#1E3A8A";
    public bool IsCurrentPeriod { get; set; }
}
