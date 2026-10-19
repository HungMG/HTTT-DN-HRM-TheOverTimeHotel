using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OvertimeHotel.HRM.WebAdmin.Models;

/// <summary>
/// Màn hình "HR duyệt hợp đồng → IT cấp tài khoản" (bảng <c>yeu_cau_tai_khoan</c>).
///
/// <para>
/// Người xem danh sách và người gửi yêu cầu là HAI vai trò khác nhau: xem thì
/// chỉ cần <c>QL_TAI_KHOAN</c> (HR theo dõi được yêu cầu của chính mình),
/// gửi thì là việc quản lý hồ sơ nhân sự (Admin/HR). <see cref="CanSubmitRequest"/>
/// mang ranh giới đó xuống tầng giao diện để không hiện nút thừa.
/// </para>
/// </summary>
public class YeuCauTaiKhoanIndexViewModel
{
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái yêu cầu; rỗng nghĩa là lấy tất cả.</summary>
    public string? TrangThai { get; set; }

    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];
    public IReadOnlyList<YeuCauTaiKhoanListItemViewModel> Requests { get; set; } = [];

    // Năm thẻ KPI đếm theo trạng thái. Người gửi chỉ thấy số của chính mình —
    // đúng bằng phân tát quyền áp ngay trong truy vấn, không phải ẩn ở UI.
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public int ProcessingRequests { get; set; }
    public int GrantedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int TotalFilteredRequests { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 7;
    public int TotalPages { get; set; } = 1;

    /// <summary>Mở khối gửi yêu cầu — xem LeaveApprovalPolicy.CanSubmitAccountRequest.</summary>
    public bool CanSubmitRequest { get; set; }
}

public class YeuCauTaiKhoanListItemViewModel
{
    public int RequestId { get; set; }
    public int EmployeeId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public string? Department { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>Đánh dấu đây là yêu cầu thăng chức lên Quản lý (thay vì cấp mới).</summary>
    public bool IsPromotion { get; set; }

    /// <summary>Số hợp đồng làm căn cứ; null nghĩa là không gắn hợp đồng.</summary>
    public string? ContractNo { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string? HandlerUsername { get; set; }
    public string? RejectionNote { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? HandledAt { get; set; }

    /// <summary>Chỉ có giá trị khi trạng thái là DA_CAP — trỏ sang dòng tai_khoan vừa tạo.</summary>
    public string? GrantedUsername { get; set; }
}

/// <summary>
/// Form gửi yêu cầu cấp tài khoản. Bắt buộc gắn hợp đồng lao động hợp lệ.
/// </summary>
public class CreateYeuCauTaiKhoanViewModel
{
    [Display(Name = "Nhân viên")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên cần cấp tài khoản.")]
    public int EmployeeId { get; set; }

    [Display(Name = "Hợp đồng")]
    [Required(ErrorMessage = "Vui lòng chọn hợp đồng lao động làm căn cứ cấp tài khoản.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hợp đồng lao động hợp lệ.")]
    public int? ContractId { get; set; }

    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải có từ 3 đến 50 ký tự.")]
    [RegularExpression("^[a-z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ thường không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Vai trò")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò sẽ gán cho tài khoản.")]
    public int RoleId { get; set; }

    public IReadOnlyList<SelectListItem> EmployeeOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> RoleOptions { get; set; } = [];

    /// <summary>
    /// Toàn bộ hợp đồng HIỆU LỰC (đã bắt đầu và chưa hết hạn) của khách sạn, kèm mã nhân viên
    /// để trình duyệt lọc theo người đã chọn.
    /// </summary>
    public IReadOnlyList<ActiveContractOptionViewModel> ActiveContracts { get; set; } = [];
}

public class ActiveContractOptionViewModel
{
    public int ContractId { get; set; }
    public int EmployeeId { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Form đề xuất thăng chức cho nhân viên lên Quản lý (Manager).
/// </summary>
public class CreatePromotionRequestViewModel
{
    [Display(Name = "Nhân viên")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên cần đề xuất thăng chức.")]
    public int EmployeeId { get; set; }

    [Display(Name = "Hợp đồng")]
    [Required(ErrorMessage = "Vui lòng chọn hợp đồng lao động làm căn cứ thăng chức.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hợp đồng lao động hợp lệ.")]
    public int? ContractId { get; set; }

    [Display(Name = "Vai trò mục tiêu")]
    public int TargetRoleId { get; set; }

    public string TargetRoleName { get; set; } = "Quản lý";

    [Display(Name = "Lý do thăng chức")]
    [Required(ErrorMessage = "Vui lòng nhập lý do hoặc căn cứ đề xuất thăng chức.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do thăng chức phải có từ 5 đến 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> EligibleEmployees { get; set; } = [];
    public IReadOnlyList<ActiveContractOptionViewModel> ActiveContracts { get; set; } = [];
}