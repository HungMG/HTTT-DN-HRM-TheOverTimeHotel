using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class NhanVienIndexViewModel
{
    public NhanVienFormViewModel CreateEmployeeForm { get; set; } = new();
    public string? Keyword { get; set; }
    public int? DepartmentId { get; set; }
    public string? Status { get; set; }
    public string? ContractFilter { get; set; }
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<EmployeeListItemViewModel> Employees { get; set; } = [];
    public int TotalEmployees { get; set; }
    public int TotalFilteredEmployees { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;
    public int WorkingEmployees { get; set; }
    public int ExpiringContracts { get; set; }
    public int ExpiredContracts { get; set; }
}

public class EmployeeListItemViewModel
{
    public NhanVien Employee { get; init; } = new();
    public HopDong? LatestContract { get; init; }
    public int? ContractDaysRemaining { get; init; }
    public string ContractState { get; init; } = "CHUA_CO";
}

public class NhanVienProfileModalViewModel
{
    public NhanVien Employee { get; init; } = new();
    public HopDong? LatestContract { get; init; }
    public IReadOnlyList<HopDong> ContractHistory { get; init; } = [];
    public TaiKhoan? Account { get; init; }
    public int? ContractDaysRemaining { get; init; }
    public string ContractState { get; init; } = "CHUA_CO";
    public PhongBan? Department => Employee.PhongBan;
    public ChucVu? Position => Employee.ChucVu;
    public int TenureYears => Math.Max(0, DateTime.Today.Year - Employee.NgayVaoLam.Year);
    public int TenureMonths => Math.Max(0, ((DateTime.Today.Year - Employee.NgayVaoLam.Year) * 12) + DateTime.Today.Month - Employee.NgayVaoLam.Month);
}

public class NhanVienFormViewModel
{
    public NhanVien Employee { get; set; } = new();
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PositionOptions { get; set; } = [];
}

public class HopDongFormViewModel
{
    public HopDong Contract { get; set; } = new();
    public string EmployeeName { get; set; } = string.Empty;

    [Display(Name = "Nhân viên")]
    public int EmployeeId { get; set; }

    [Display(Name = "Phòng ban")]
    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    [Display(Name = "Email cá nhân")]
    public string? PersonalEmail { get; set; }

    [Display(Name = "Gửi thông báo qua email cá nhân")]
    public string EmailNotificationOption { get; set; } = "KHONG_GUI";

    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EmployeeOptions { get; set; } = [];
}
