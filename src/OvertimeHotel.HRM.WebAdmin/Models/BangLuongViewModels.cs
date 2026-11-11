using System.ComponentModel.DataAnnotations;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public class BangLuongIndexViewModel
{
    public int SelectedThang { get; set; }
    public int SelectedNam { get; set; }
    public KyLuong? CurrentPeriod { get; set; }
    public List<KyLuong> Periods { get; set; } = [];
    public List<PhieuLuongRowViewModel> Items { get; set; } = [];

    public string? SearchKeyword { get; set; }
    public string? DepartmentFilter { get; set; }
    public List<string> Departments { get; set; } = [];

    // Thống kê tài chính
    public int TotalEmployees => Items.Count;
    public decimal TotalGross => Items.Sum(i => i.TongGross);
    public decimal TotalDeductions => Items.Sum(i => i.TongKhauTru);
    public decimal TotalNet => Items.Sum(i => i.ThucNhan);
    public decimal TotalEventBonus => Items.Sum(i => i.TienSuKien);
    public int TotalEventStaffCount => Items.Count(i => i.TienSuKien > 0);

    // Quyền hạn thao tác
    public bool CanCalculate { get; set; }
    public bool CanPublishDraft { get; set; }
    public bool IsReviewingDraft { get; set; }
    public DateTimeOffset? HanPhanHoi { get; set; }
    public DateTimeOffset? NgayPhatHanhNhap { get; set; }
    public string? NguoiPhatHanhNhap { get; set; }
    public bool CanSubmitApproval { get; set; }
    public bool CanApprove { get; set; }
    public bool CanLock { get; set; }
    public bool CanPublishAndExport { get; set; }
    public bool ArePayslipsPublished => (CurrentPeriod?.PhieuLuongs.Any(p => p.TrangThai == OvertimeHotel.HRM.Core.Constants.SalaryConstants.PAYSLIP_PAID) == true)
                                        || (Items.Count > 0 && Items.All(i => i.TrangThai == OvertimeHotel.HRM.Core.Constants.SalaryConstants.PAYSLIP_PAID));

    // Phân định vai trò (Separation of Duties)
    public bool IsHR { get; set; }
    public bool IsAdminOrBoss { get; set; }
    public bool ShowPayrollData { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PhieuLuongRowViewModel
{
    public int MaPhieuLuong { get; set; }
    public int MaNhanVien { get; set; }
    public string MaNvFormatted => $"NV{MaNhanVien:D4}";
    public string HoTen { get; set; } = string.Empty;
    public string PhongBan { get; set; } = string.Empty;
    public string ChucVu { get; set; } = string.Empty;
    public decimal LuongCoBan { get; set; }
    public decimal NgayCongThucTe { get; set; }
    public decimal TienLuongCong { get; set; }
    public decimal TienTangCa { get; set; }
    public decimal TongPhuCap { get; set; }
    public decimal TongThuong { get; set; }
    public decimal TienSuKien { get; set; }
    public decimal TongGross => TienLuongCong + TienTangCa + TongPhuCap + TongThuong;
    public decimal TongKhauTru { get; set; }
    public decimal ThucNhan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

public class PhieuLuongDetailViewModel
{
    public int MaPhieuLuong { get; set; }
    public int MaNhanVien { get; set; }
    public string MaNvFormatted => $"NV{MaNhanVien:D4}";
    public string HoTen { get; set; } = string.Empty;
    public string PhongBan { get; set; } = string.Empty;
    public string ChucVu { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public int Thang { get; set; }
    public int Nam { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string PeriodStatus { get; set; } = string.Empty;
    public bool IsLocked => PeriodStatus == "KHOA_SO";

    // Thống kê chấm công, ngày công, tăng ca & kỷ luật (Attendance & Discipline Summary)
    public int CongChuan => OvertimeHotel.HRM.Core.Constants.SalaryConstants.STANDARD_WORKING_DAYS; // 26 ngày
    public decimal NgayDiLam { get; set; }
    public decimal NgayNghiPhep { get; set; }
    public decimal NgayNghiKhongLuong { get; set; }
    public int SoCaBo { get; set; }

    public decimal TongGioOt { get; set; }
    public decimal GioOtThuong { get; set; }
    public decimal GioOtCuoiTuan { get; set; }
    public decimal GioOtNgayLe { get; set; }

    public int SoLanDiMuon { get; set; }
    public int TongPhutDiMuon { get; set; }
    public int SoLanVeSom { get; set; }
    public int TongPhutVeSom { get; set; }
    public bool CanEdit { get; set; }
    public bool IsHR { get; set; }

    // Tầng 1: Thu nhập (Gross)
    public decimal LuongCoBan { get; set; }
    public decimal NgayCongThucTe { get; set; }
    public decimal TienLuongCong { get; set; }

    public List<ChiTietKhoanItemViewModel> OvertimeItems { get; set; } = [];
    public decimal TienTangCa { get; set; }

    public ChiTietKhoanItemViewModel? NightShiftItem { get; set; }
    public decimal TienCaDem => NightShiftItem?.SoTien ?? 0m;

    public ChiTietKhoanItemViewModel? MealAllowanceItem { get; set; }
    public decimal TienAnCa => MealAllowanceItem?.SoTien ?? 0m;

    public List<ChiTietKhoanItemViewModel> OtherAllowances { get; set; } = [];
    public List<ChiTietKhoanItemViewModel> EventAllowances { get; set; } = [];
    public decimal TongPhuCap { get; set; }

    public List<ChiTietKhoanItemViewModel> BonusItems { get; set; } = [];
    public decimal TongThuong { get; set; }

    public decimal TongGross => TienLuongCong + TienTangCa + TongPhuCap + TongThuong;

    // Tầng 2: Khấu trừ
    public InsuranceDetailViewModel? Insurance { get; set; }
    public decimal TongBaoHiem => Insurance?.TongBaoHiem ?? 0m;

    public List<FineViolationItemViewModel> Violations { get; set; } = [];
    public decimal TongTienPhat { get; set; }

    public TaxDetailViewModel? Tax { get; set; }
    public decimal TienThue => Tax?.TienThue ?? 0m;

    public List<ChiTietKhoanItemViewModel> AdvanceItems { get; set; } = [];
    public decimal TongKhauTru { get; set; }

    // Thực nhận (Net)
    public decimal ThucNhan { get; set; }
    public string ThucNhanBangChu { get; set; } = string.Empty;
    public bool IsBalanced => Math.Abs(ThucNhan - (TongGross - TongKhauTru)) < 0.01m;

    public AdjustSalaryItemInput AdjustInput { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChiTietKhoanItemViewModel
{
    public int MaChiTiet { get; set; }
    public string LoaiKhoan { get; set; } = string.Empty;
    public string TenKhoan { get; set; } = string.Empty;
    public decimal SoTien { get; set; }
    public string? GhiChu { get; set; }
    public bool CanDelete { get; set; }
}

public class InsuranceDetailViewModel
{
    public decimal LuongDongBaoHiem { get; set; }
    public decimal TyLeBhxh { get; set; } = 8.00m;
    public decimal TienBhxh { get; set; }
    public decimal TyLeBhyt { get; set; } = 1.50m;
    public decimal TienBhyt { get; set; }
    public decimal TyLeBhtn { get; set; } = 1.00m;
    public decimal TienBhtn { get; set; }
    public decimal TongBaoHiem { get; set; }
}

public class FineViolationItemViewModel
{
    public string LoaiTru { get; set; } = string.Empty;
    public int SoPhutViPham { get; set; }
    public decimal SoTienTru { get; set; }
    public string LyDoChiTiet { get; set; } = string.Empty;
}

public class TaxDetailViewModel
{
    public decimal ThuNhapChiuThue { get; set; }
    public decimal GiamTruBanThan { get; set; } = 15500000.00m;
    public int SoNguoiPhuThuoc { get; set; }
    public decimal GiamTruPhuThuoc { get; set; }
    public decimal ThuNhapTinhThue { get; set; }
    public decimal TienThue { get; set; }
}

public class AdjustSalaryItemInput
{
    public int MaPhieuLuong { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại khoản")]
    public string LoaiKhoan { get; set; } = "PHU_CAP";

    [Required(ErrorMessage = "Vui lòng nhập tên khoản điều chỉnh")]
    [StringLength(150, ErrorMessage = "Tên khoản tối đa 150 ký tự")]
    public string TenKhoan { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số tiền")]
    [Range(1000, 500000000, ErrorMessage = "Số tiền phải từ 1.000đ đến 500.000.000đ")]
    public decimal SoTien { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự")]
    public string? GhiChu { get; set; }
}

public class RejectPayrollInput
{
    public int MaKyLuong { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối bảng lương")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do từ chối từ 5 đến 500 ký tự")]
    public string LyDo { get; set; } = string.Empty;
}

public class PublishDraftPayrollInput
{
    public int MaKyLuong { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }

    [Range(1, 14, ErrorMessage = "Số ngày phản hồi từ 1 đến 14 ngày")]
    public int SoNgayPhanHoi { get; set; } = 3;

    public DateTime? HanPhanHoiTuyChinh { get; set; }
}

