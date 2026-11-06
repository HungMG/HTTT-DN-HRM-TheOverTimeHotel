namespace OvertimeHotel.HRM.Core.Constants;

public static class ContractTerminationTypes
{
    public const string HET_HAN_KHONG_TAI_KY = "HET_HAN_KHONG_TAI_KY";
    public const string NHAN_VIEN_XIN_NGHI = "NHAN_VIEN_XIN_NGHI";
    public const string SA_THAI_VI_PHAM = "SA_THAI_VI_PHAM";
    public const string KHAC = "KHAC";

    public static readonly IReadOnlyList<(string Code, string Label)> All =
    [
        (HET_HAN_KHONG_TAI_KY, "Hết hạn, không tái ký"),
        (NHAN_VIEN_XIN_NGHI, "Nhân viên xin nghỉ"),
        (SA_THAI_VI_PHAM, "Sa thải / vi phạm"),
        (KHAC, "Khác")
    ];

    public static bool IsValid(string? code) => All.Any(item => item.Code == code);

    public static string Label(string? code) => All.FirstOrDefault(item => item.Code == code).Label ?? code ?? string.Empty;
}
