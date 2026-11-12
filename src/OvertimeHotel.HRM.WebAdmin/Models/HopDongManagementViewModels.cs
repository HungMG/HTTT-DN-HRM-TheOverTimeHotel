using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class HopDongIndexViewModel
{
    public HopDongFormViewModel CreateContractForm { get; set; } = new();
    public string? Keyword { get; set; }
    public string? ContractFilter { get; set; }
    public int? DepartmentId { get; set; }
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
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
    public string DepartmentName { get; init; } = string.Empty;
    public string PositionName { get; init; } = string.Empty;
    public string? PersonalEmail { get; init; }
    public int? DaysRemaining { get; init; }
    public string State { get; init; } = string.Empty;
    public bool IsRenewed { get; init; }
    public bool EmployeeHasOtherContract { get; init; }
}

public class HopDongDetailsModalViewModel
{
    public HopDong Contract { get; init; } = new();
    public NhanVien Employee { get; init; } = new();
    public string DepartmentName { get; init; } = string.Empty;
    public string PositionName { get; init; } = string.Empty;
    public int? DaysRemaining { get; init; }
    public string State { get; init; } = string.Empty;
    public bool IsRenewed { get; init; }
    public HopDong? PreviousContract { get; init; }
    public HopDong? NextContract { get; init; }
}

public class HopDongTerminateViewModel
{
    public DateOnly? NgayChamDut { get; set; }
    public string? LoaiChamDut { get; set; }
    public string? LyDoChamDut { get; set; }
}

public class HopDongSalaryAmendmentsViewModel
{
    public int ContractId { get; init; }
    public int EmployeeId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string Education { get; init; } = string.Empty;
    public int? PositionId { get; init; }
    public int? DepartmentId { get; init; }
    public decimal? CurrentSalary { get; init; }
    public DateOnly DefaultEffectiveDate { get; init; } = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
    public bool NextMonthOutsideContract { get; init; }
    public bool CanAdd { get; init; } = true;
    public bool RequiresBaselineConfirmation { get; init; }
    public IReadOnlyList<HopDongSalaryAmendmentItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; init; } = [];
    public IReadOnlyList<SelectListItem> PositionOptions { get; init; } = [];
}

public class HopDongSalaryAmendmentItemViewModel
{
    public DateOnly EffectiveDate { get; init; }
    public decimal Salary { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string PositionName { get; init; } = string.Empty;
}
