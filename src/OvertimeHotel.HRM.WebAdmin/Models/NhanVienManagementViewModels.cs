using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class NhanVienIndexViewModel
{
    public NhanVienFormViewModel CreateEmployeeForm { get; set; } = new();
    public string? Keyword { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public string? Status { get; set; }
    public string? ContractFilter { get; set; }
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PositionOptions { get; set; } = [];
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
    public string AccountStatus => Employee.TaiKhoan != null ? "DA_CAP" :
        Employee.YeuCauTaiKhoanCuaNhanVien.OrderByDescending(request => request.NgayGui)
            .ThenByDescending(request => request.MaYeuCau).FirstOrDefault()?.TrangThai switch
        {
            "CHO_IT" or "DANG_XU_LY" => "CHO_CAP",
            "TU_CHOI" => "TU_CHOI",
            _ => "CHUA_CAP"
        };
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
    public int TenureYears => TenureMonths / 12;
    public int TenureMonths => OvertimeHotel.HRM.Core.Policies.ReportPolicy.TenureMonths(Employee.NgayVaoLam, DateOnly.FromDateTime(DateTime.Today), Employee.NgayThoiViec);
}

public class NhanVienFormViewModel
{
    public const string PersonNamePattern = @"\A[\p{L}\p{M}]+(?:[ '’-][\p{L}\p{M}]+)*\z";
    public const string MobilePhonePattern = @"(?:0|\+84)[35789][0-9]{8}";
    public string EntryMode { get; set; } = "FROM_CONTRACT";
    public int? ContractId { get; set; }
    public IReadOnlyList<HopDong> EligibleContracts { get; set; } = [];
    public NhanVien Employee { get; set; } = new();
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PositionOptions { get; set; } = [];
}

public class HopDongFormViewModel
{
    public HopDong Contract { get; set; } = new();
    public string EmployeeName { get; set; } = string.Empty;

    [Display(Name = "Nhân viên")]
    public int? EmployeeId { get; set; }

    [Display(Name = "Phòng ban")]
    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    [Display(Name = "Chức vụ")]
    public int? PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;

    [Display(Name = "Trình độ chuyên môn")]
    public string TrinhDo { get; set; } = string.Empty;

    [Display(Name = "Email cá nhân")]
    public string? PersonalEmail { get; set; }

    [Display(Name = "Gửi thông báo qua email cá nhân")]
    public string EmailNotificationOption { get; set; } = "KHONG_GUI";

    [Display(Name = "Khép hợp đồng cũ để chuyển tiếp sang hợp đồng mới")]
    public bool ClosePreviousContract { get; set; }

    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PositionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> EmployeeOptions { get; set; } = [];
    public IReadOnlyList<HopDongEmployeeOptionItem> EmployeeList { get; set; } = [];
}

public class HopDongEmployeeOptionItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int? PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public string PersonalEmail { get; set; } = string.Empty;
    public string TrinhDo { get; set; } = string.Empty;
}
