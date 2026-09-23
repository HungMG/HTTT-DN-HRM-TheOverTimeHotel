using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class HopDongIndexViewModel
{
    public HopDongFormViewModel CreateContractForm { get; set; } = new();
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? ContractFilter { get; set; }
    public IReadOnlyList<SelectListItem> EmployeeOptions { get; set; } = [];
    public IReadOnlyList<HopDongListItemViewModel> Contracts { get; set; } = [];
    public int TotalContracts { get; set; }
    public int TotalFilteredContracts { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;
    public int ActiveContracts { get; set; }
    public int ExpiringContracts { get; set; }
    public int ExpiredContracts { get; set; }
}

public class HopDongListItemViewModel
{
    public HopDong Contract { get; init; } = new();
    public string EmployeeName { get; init; } = string.Empty;
    public int? DaysRemaining { get; init; }
    public string State { get; init; } = string.Empty;
}
