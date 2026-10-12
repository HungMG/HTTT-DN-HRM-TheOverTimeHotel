namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Model đại diện cho thông tin kết quả tra cứu nhân sự nhanh của HR.
/// Hỗ trợ tra cứu theo Mã nhân viên (StaffCode) hoặc Họ tên, hiển thị trạng thái ca trực thực tế.
/// </summary>
public class HrStaffLookupItem
{
	public string StaffCode { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string Department { get; set; } = string.Empty;
	public string Position { get; set; } = string.Empty;
	public string WorkStatus { get; set; } = string.Empty;
	public string WorkStatusDetail { get; set; } = string.Empty;
	public string StatusColor { get; set; } = "#15803D";
	public string StatusBgColor { get; set; } = "#DCFCE7";
	public string InternalPhone { get; set; } = string.Empty;

	public string AvatarInitial => !string.IsNullOrWhiteSpace(FullName)
		? FullName.Trim().Split(' ').LastOrDefault()?[0].ToString().ToUpper() ?? "NV"
		: "NV";
}
