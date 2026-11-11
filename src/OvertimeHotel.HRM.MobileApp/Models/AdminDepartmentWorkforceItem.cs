namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Đại diện cho một khối phòng ban trong báo cáo quân số và tỷ lệ hiện diện dành cho Ban Giám Đốc.
/// </summary>
public class AdminDepartmentWorkforceItem
{
    public string ClusterName { get; set; } = string.Empty;
    public string DepartmentScope { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int TotalCount { get; set; }
    public double AttendanceRate { get; set; }
    public string RateText => $"{AttendanceRate * 100:0.#}%";
    public string RatioText => $"{PresentCount}/{TotalCount}";
    public string Icon { get; set; } = "🏢";
    public string StatusColor { get; set; } = "#15803D";
    public string ProgressColor { get; set; } = "#1E3A8A";
}
