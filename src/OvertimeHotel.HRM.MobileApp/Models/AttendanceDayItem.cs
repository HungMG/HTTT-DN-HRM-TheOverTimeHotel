using System.Globalization;
using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Models;

public class AttendanceDayItem(AttendanceDayDto day)
{
    public string DateText => day.Date.ToString("dd/MM", CultureInfo.InvariantCulture);
    public string Weekday => day.Date.ToString("dddd", CultureInfo.GetCultureInfo("vi-VN"));
    public string ShiftText => day.HasSchedule
        ? string.Join(" · ", day.Shifts.Select(s => s.Name)) : "Không có lịch phân ca";
    public string StatusText => day.HasWorked ? "Công thực tế" : day.CountsAsPaidLeave ? "Phép hưởng lương" :
        day.HasPaidLeave ? "Chưa tính công phép" : "Chưa đủ chấm công";
    public string CreditText => $"{day.CreditedDays} ngày công";
    public string StatusColor => day.HasWorked ? "#1E3A8A" : day.CountsAsPaidLeave ? "#047857" : "#64748B";
    public string StatusBackground => day.HasWorked ? "#EFF6FF" : day.CountsAsPaidLeave ? "#ECFDF5" : "#F1F5F9";
    public bool HasLeave => day.HasPaidLeave;
    public bool NeedsReview => day.NeedsReview;
    public string Explanation => !day.HasSchedule ? "Đơn đã duyệt; ngày này chưa có ca nên chưa cộng công." :
        day.HasWorked && day.HasPaidLeave ? "Đã tính công thực tế; không cộng thêm công phép cho cùng ngày." :
        day.CountsAsPaidLeave ? "Tự động ghi nhận từ đơn đã duyệt và lịch phân ca." :
        day.HasWorked ? "Đã có giờ vào và giờ ra hợp lệ." : "Chưa có đủ giờ vào và giờ ra hợp lệ.";
    public string LeaveText => string.Join("\n", day.LeaveRequests.Select(r =>
        $"Đơn #{r.RequestId} · {r.LeaveType}\n{r.FromDate:dd/MM/yyyy} – {r.ToDate:dd/MM/yyyy}" +
        (r.ApprovedAt.HasValue ? $" · Duyệt {r.ApprovedAt.Value.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy}" : " · Đã duyệt")));
}
