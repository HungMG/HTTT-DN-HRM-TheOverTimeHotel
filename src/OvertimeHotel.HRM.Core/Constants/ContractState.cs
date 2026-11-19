using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Constants;

public static class ContractState
{
    public static bool IsCurrentOrUpcoming(HopDong contract, DateOnly today) =>
        Classify(contract, today) is "HIEU_LUC" or "SAP_HET_HAN" or "CHUA_HIEU_LUC";

    public static string Classify(HopDong? contract, DateOnly today)
    {
        if (contract == null) return "CHUA_CO";
        if (contract.TrangThai is "DA_CHAM_DUT" or "CHAM_DUT") return "DA_CHAM_DUT";
        if (contract.NgayKetThuc < today) return "DA_HET_HAN";
        if (contract.NgayBatDau > today) return "CHUA_HIEU_LUC";
        if (contract.NgayKetThuc is DateOnly end && end.DayNumber - today.DayNumber <= 30) return "SAP_HET_HAN";
        return "HIEU_LUC";
    }
}
