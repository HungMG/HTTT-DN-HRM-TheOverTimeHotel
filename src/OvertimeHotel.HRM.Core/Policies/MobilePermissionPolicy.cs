namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>Permission codes shared by mobile screens and request authorization.</summary>
public static class MobilePermissionPolicy
{
    public const string CheckIn = "CHAM_CONG_DI_DONG";
    public const string DepartmentAttendance = "XEM_CHAM_CONG_BO_PHAN";
    public const string AssignShifts = "XEP_CA_LAM";
    public const string Reports = "REPORT_VIEW";
    public const string SubmitRequest = "NOP_DON_TU";
    public const string ApproveLeave = "DUYET_DON_NGHI";
    public const string ApproveOvertime = "DUYET_TANG_CA_OT";
    public const string ApproveResignation = "DUYET_THOI_VIEC";

    // Database: 1–4 are leave; 5 is resignation. Overtime has its own workflow.
    public static string ReviewPermission(int requestType) => requestType == 5 ? ApproveResignation : ApproveLeave;
}
