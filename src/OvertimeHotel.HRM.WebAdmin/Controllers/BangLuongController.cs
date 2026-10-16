using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Utilities;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize]
public class BangLuongController(
    IPayrollCalculationService payrollService,
    ILogger<BangLuongController> logger) : Controller
{
    private readonly IPayrollCalculationService _payrollService = payrollService;
    private readonly ILogger<BangLuongController> _logger = logger;

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    private string CurrentUserName =>
        User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Người dùng";

    // Quản trị viên hệ thống kiêm Ban Giám Đốc (Sếp phê duyệt, khóa sổ)
    private bool IsAdmin => User.IsInRole("Admin");

    // Ban Giám Đốc / Sếp có thẩm quyền phê duyệt bảng lương
    private bool IsBoss => User.IsInRole("Admin") || User.IsInRole("Manager");

    // Chuyên viên Nhân sự (HR): phụ trách kiểm tra, rà soát, đối chiếu, sửa sai sót và gửi duyệt
    // TÁCH BIỆT VAI TRÒ: Tài khoản chỉ có vai trò Admin KHÔNG phải là HR (không gửi duyệt thay HR)
    private bool IsHR => User.IsInRole("HR") || (!User.IsInRole("Admin") && User.HasClaim(PermissionCodes.ClaimType, PermissionCodes.CalculatePayroll));

    [HttpGet]
    public async Task<IActionResult> Index(int? thang, int? nam, string? keyword, string? department)
    {
        var targetThang = thang ?? DateTime.Today.Month;
        var targetNam = nam ?? DateTime.Today.Year;

        var periods = await _payrollService.GetAllPeriodsAsync();
        var currentPeriod = await _payrollService.GetPeriodWithDetailsAsync(targetThang, targetNam);

        if (currentPeriod == null)
        {
            currentPeriod = await _payrollService.GetOrCreatePeriodAsync(targetThang, targetNam);
            currentPeriod = await _payrollService.GetPeriodWithDetailsAsync(targetThang, targetNam);
        }

        var departments = currentPeriod?.PhieuLuongs
            .Select(p => p.NhanVien?.PhongBan?.TenPhongBan)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .OrderBy(d => d)
            .Cast<string>()
            .ToList() ?? [];

        var query = currentPeriod?.PhieuLuongs.AsEnumerable() ?? [];

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLowerInvariant();
            query = query.Where(p =>
                (p.NhanVien != null && p.NhanVien.HoTen.ToLowerInvariant().Contains(term)) ||
                $"NV{p.MaNhanVien:D4}".ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(p => p.NhanVien?.PhongBan?.TenPhongBan == department);
        }

        var periodStatus = currentPeriod?.TrangThai ?? SalaryConstants.PERIOD_OPEN;

        // Phân định hiển thị bảng lương theo yêu cầu:
        // - Tài khoản HR: luôn thấy bảng lương nháp để rà soát, đối chiếu và sửa sai sót.
        // - Tài khoản Admin: ban đầu KHÔNG hiện bảng lương, CHỈ hiển thị sau khi HR đã kiểm tra và bấm "Gửi duyệt"
        //   (Tức là khi trạng thái kỳ lương đã chuyển sang CHO_DUYET, DA_DUYET hoặc KHOA_SO).
        var showPayrollData = IsHR || (periodStatus is SalaryConstants.PERIOD_PENDING_APPROVAL
                                                    or SalaryConstants.PERIOD_APPROVED
                                                    or SalaryConstants.PERIOD_LOCKED);

        var rows = showPayrollData ? query.Select(p => new PhieuLuongRowViewModel
        {
            MaPhieuLuong = p.MaPhieuLuong,
            MaNhanVien = p.MaNhanVien,
            HoTen = p.NhanVien?.HoTen ?? "N/A",
            PhongBan = p.NhanVien?.PhongBan?.TenPhongBan ?? "N/A",
            ChucVu = p.NhanVien?.ChucVu?.TenChucVu ?? "N/A",
            LuongCoBan = p.LuongCoBan,
            NgayCongThucTe = p.NgayCongThucTe,
            TienLuongCong = p.TienLuongCong,
            TienTangCa = p.TienTangCa,
            TongPhuCap = p.TongPhuCap,
            TongThuong = p.TongThuong,
            TongKhauTru = p.TongKhauTru,
            ThucNhan = p.ThucNhan,
            TrangThai = p.TrangThai
        }).OrderBy(r => r.PhongBan).ThenBy(r => r.HoTen).ToList() : [];

        var model = new BangLuongIndexViewModel
        {
            SelectedThang = targetThang,
            SelectedNam = targetNam,
            CurrentPeriod = currentPeriod,
            Periods = periods,
            Items = rows,
            SearchKeyword = keyword,
            DepartmentFilter = department,
            Departments = departments,
            CanCalculate = IsHR && periodStatus != SalaryConstants.PERIOD_LOCKED,
            CanSubmitApproval = IsHR && (periodStatus is SalaryConstants.PERIOD_DRAFT or SalaryConstants.PERIOD_REJECTED or SalaryConstants.PERIOD_OPEN) && query.Any(),
            CanApprove = IsBoss && periodStatus == SalaryConstants.PERIOD_PENDING_APPROVAL,
            CanLock = IsBoss && periodStatus == SalaryConstants.PERIOD_APPROVED,
            CanPublishAndExport = (IsBoss || IsHR) && periodStatus == SalaryConstants.PERIOD_LOCKED,
            IsHR = IsHR,
            IsAdminOrBoss = IsBoss,
            ShowPayrollData = showPayrollData,
            SuccessMessage = TempData["Success"]?.ToString(),
            ErrorMessage = TempData["Error"]?.ToString()
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(int thang, int nam)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Nhân sự (HR) mới có quyền chạy tính toán bảng lương nháp.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        try
        {
            await _payrollService.CalculatePayrollAsync(thang, nam, CurrentUserName, CurrentUserId);
            TempData["Success"] = $"Đã chạy tính toán tự động bảng lương tháng {thang:D2}/{nam} thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tính lương kỳ {Thang}/{Nam}", thang, nam);
            TempData["Error"] = $"Không thể tính lương: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var payslip = await _payrollService.GetPayslipDetailAsync(id);
        if (payslip == null) return NotFound();

        var ky = payslip.KyLuong;
        var periodStatus = ky?.TrangThai ?? SalaryConstants.PERIOD_OPEN;

        // Nếu là Admin/Sếp và bảng lương chưa được HR gửi duyệt (vẫn là NHAP, MO, hoặc TU_CHOI):
        if (!IsHR && periodStatus is SalaryConstants.PERIOD_OPEN or SalaryConstants.PERIOD_DRAFT or SalaryConstants.PERIOD_REJECTED)
        {
            TempData["Error"] = $"Bảng lương tháng {ky?.Thang:D2}/{ky?.Nam} đang ở giai đoạn rà soát nội bộ của bộ phận HR. Ban Giám Đốc chỉ xem chi tiết sau khi HR bấm 'Gửi duyệt'.";
            return RedirectToAction(nameof(Index), new { thang = ky?.Thang, nam = ky?.Nam });
        }

        var nv = payslip.NhanVien;
        var details = payslip.ChiTietPhieuLuongs.ToList();

        var overtimeItems = details
            .Where(d => d.LoaiKhoan == "TANG_CA")
            .Select(d => new ChiTietKhoanItemViewModel
            {
                MaChiTiet = d.MaChiTiet,
                LoaiKhoan = d.LoaiKhoan ?? d.LoaiKhoanLuu,
                TenKhoan = d.TenKhoanLuu,
                SoTien = d.SoTien,
                GhiChu = d.GhiChu,
                CanDelete = false
            }).ToList();

        var nightItem = details.FirstOrDefault(d => d.MaLoaiOt == 4);
        var mealItem = details.FirstOrDefault(d => d.MaPhuCap == 1);

        var eventItems = details
            .Where(d => d.LoaiKhoan == "PHU_CAP" && d.TenKhoanLuu.StartsWith("Phụ cấp sự kiện"))
            .Select(d => new ChiTietKhoanItemViewModel
            {
                MaChiTiet = d.MaChiTiet,
                LoaiKhoan = d.LoaiKhoan ?? d.LoaiKhoanLuu,
                TenKhoan = d.TenKhoanLuu,
                SoTien = d.SoTien,
                GhiChu = d.GhiChu,
                CanDelete = false
            }).ToList();

        var otherAllowances = details
            .Where(d => d.LoaiKhoan == "PHU_CAP" && d != nightItem && d != mealItem && !eventItems.Any(e => e.MaChiTiet == d.MaChiTiet))
            .Select(d => new ChiTietKhoanItemViewModel
            {
                MaChiTiet = d.MaChiTiet,
                LoaiKhoan = d.LoaiKhoan ?? d.LoaiKhoanLuu,
                TenKhoan = d.TenKhoanLuu,
                SoTien = d.SoTien,
                GhiChu = d.GhiChu,
                CanDelete = d.MaPhuCap == null // Cho phép xóa nếu là dòng điều chỉnh thủ công
            }).ToList();

        var bonusItems = details
            .Where(d => d.LoaiKhoan == "THUONG")
            .Select(d => new ChiTietKhoanItemViewModel
            {
                MaChiTiet = d.MaChiTiet,
                LoaiKhoan = d.LoaiKhoan ?? d.LoaiKhoanLuu,
                TenKhoan = d.TenKhoanLuu,
                SoTien = d.SoTien,
                GhiChu = d.GhiChu,
                CanDelete = d.MaKhoan == null
            }).ToList();

        var insuranceDetail = details.FirstOrDefault(d => d.MaKhoanTru == 1)?.ChiTietTruBaoHiems.FirstOrDefault();
        InsuranceDetailViewModel? insuranceVm = null;
        if (insuranceDetail != null)
        {
            insuranceVm = new InsuranceDetailViewModel
            {
                LuongDongBaoHiem = insuranceDetail.LuongDongBaoHiem,
                TyLeBhxh = insuranceDetail.TyLeBhxh,
                TienBhxh = insuranceDetail.TienBhxh,
                TyLeBhyt = insuranceDetail.TyLeBhyt,
                TienBhyt = insuranceDetail.TienBhyt,
                TyLeBhtn = insuranceDetail.TyLeBhtn,
                TienBhtn = insuranceDetail.TienBhtn,
                TongBaoHiem = insuranceDetail.TongBaoHiem
            };
        }

        var fineDetail = details.FirstOrDefault(d => d.MaKhoanTru == 5);
        var violationItems = fineDetail?.ChiTietTruViPhamTamUngs.Select(v => new FineViolationItemViewModel
        {
            LoaiTru = v.LoaiTru,
            SoPhutViPham = v.SoPhutViPham,
            SoTienTru = v.SoTienTru,
            LyDoChiTiet = v.LyDoChiTiet
        }).ToList() ?? [];

        var taxDetail = details.FirstOrDefault(d => d.MaKhoanTru == 4)?.ChiTietTruThueTncns.FirstOrDefault();
        TaxDetailViewModel? taxVm = null;
        if (taxDetail != null)
        {
            taxVm = new TaxDetailViewModel
            {
                ThuNhapChiuThue = taxDetail.ThuNhapChiuThue,
                GiamTruBanThan = taxDetail.GiamTruBanThan,
                SoNguoiPhuThuoc = taxDetail.SoNguoiPhuThuoc,
                GiamTruPhuThuoc = taxDetail.GiamTruPhuThuoc,
                ThuNhapTinhThue = taxDetail.ThuNhapTinhThue,
                TienThue = taxDetail.TienThue
            };
        }

        var advanceItems = details
            .Where(d => d.LoaiKhoan == "KHAU_TRU" && d.MaKhoanTru != 1 && d.MaKhoanTru != 4 && d.MaKhoanTru != 5)
            .Select(d => new ChiTietKhoanItemViewModel
            {
                MaChiTiet = d.MaChiTiet,
                LoaiKhoan = d.LoaiKhoan ?? d.LoaiKhoanLuu,
                TenKhoan = d.TenKhoanLuu,
                SoTien = d.SoTien,
                GhiChu = d.GhiChu,
                CanDelete = true
            }).ToList();

        var model = new PhieuLuongDetailViewModel
        {
            MaPhieuLuong = payslip.MaPhieuLuong,
            MaNhanVien = payslip.MaNhanVien,
            HoTen = nv?.HoTen ?? "N/A",
            PhongBan = nv?.PhongBan?.TenPhongBan ?? "N/A",
            ChucVu = nv?.ChucVu?.TenChucVu ?? "N/A",
            Email = nv?.EmailCongVu ?? "N/A",
            DienThoai = nv?.DienThoai ?? "N/A",
            Thang = ky?.Thang ?? 0,
            Nam = ky?.Nam ?? 0,
            TuNgay = ky?.TuNgay ?? DateOnly.MinValue,
            DenNgay = ky?.DenNgay ?? DateOnly.MinValue,
            TrangThai = payslip.TrangThai,
            PeriodStatus = ky?.TrangThai ?? SalaryConstants.PERIOD_OPEN,
            LuongCoBan = payslip.LuongCoBan,
            NgayCongThucTe = payslip.NgayCongThucTe,
            TienLuongCong = payslip.TienLuongCong,
            OvertimeItems = overtimeItems,
            TienTangCa = payslip.TienTangCa,
            NightShiftItem = nightItem != null ? new ChiTietKhoanItemViewModel
            {
                MaChiTiet = nightItem.MaChiTiet,
                LoaiKhoan = nightItem.LoaiKhoan ?? nightItem.LoaiKhoanLuu,
                TenKhoan = nightItem.TenKhoanLuu,
                SoTien = nightItem.SoTien,
                GhiChu = nightItem.GhiChu
            } : null,
            MealAllowanceItem = mealItem != null ? new ChiTietKhoanItemViewModel
            {
                MaChiTiet = mealItem.MaChiTiet,
                LoaiKhoan = mealItem.LoaiKhoan ?? mealItem.LoaiKhoanLuu,
                TenKhoan = mealItem.TenKhoanLuu,
                SoTien = mealItem.SoTien,
                GhiChu = mealItem.GhiChu
            } : null,
            OtherAllowances = otherAllowances,
            EventAllowances = eventItems,
            TongPhuCap = payslip.TongPhuCap,
            BonusItems = bonusItems,
            TongThuong = payslip.TongThuong,
            Insurance = insuranceVm,
            Violations = violationItems,
            TongTienPhat = fineDetail?.SoTien ?? 0m,
            Tax = taxVm,
            AdvanceItems = advanceItems,
            TongKhauTru = payslip.TongKhauTru,
            ThucNhan = payslip.ThucNhan,
            ThucNhanBangChu = VietnameseNumberToWords.ConvertToWords(payslip.ThucNhan),
            AdjustInput = new AdjustSalaryItemInput { MaPhieuLuong = payslip.MaPhieuLuong },
            CanEdit = IsHR && (ky?.TrangThai is SalaryConstants.PERIOD_DRAFT or SalaryConstants.PERIOD_REJECTED or SalaryConstants.PERIOD_OPEN),
            IsHR = IsHR,
            SuccessMessage = TempData["Success"]?.ToString(),
            ErrorMessage = TempData["Error"]?.ToString()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var payslip = await _payrollService.GetPayslipDetailAsync(id);
        if (payslip == null) return NotFound();

        var detailVm = (await Detail(id) as ViewResult)?.Model as PhieuLuongDetailViewModel;
        if (detailVm == null) return NotFound();

        // Kiểm tra bất biến toàn vẹn PRD 05: Net in bắt buộc bằng Gross in - Khấu trừ in
        var diff = Math.Abs(detailVm.ThucNhan - (detailVm.TongGross - detailVm.TongKhauTru));
        if (diff >= 0.01m)
        {
            throw new InvalidOperationException($"Vi phạm bất biến cân bằng phiếu lương #{id}: Thực nhận ({detailVm.ThucNhan}) != Gross ({detailVm.TongGross}) - Khấu trừ ({detailVm.TongKhauTru}). Sai lệch: {diff:N2}đ.");
        }

        return View(detailVm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(AdjustSalaryItemInput input)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền kiểm tra, đối soát và điều chỉnh số liệu bảng lương nháp.";
            return RedirectToAction(nameof(Detail), new { id = input.MaPhieuLuong });
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu điều chỉnh không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Detail), new { id = input.MaPhieuLuong });
        }

        try
        {
            await _payrollService.AdjustSalaryItemAsync(
                input.MaPhieuLuong,
                input.LoaiKhoan,
                input.TenKhoan,
                input.SoTien,
                input.GhiChu,
                CurrentUserName,
                CurrentUserId);

            TempData["Success"] = $"Đã điều chỉnh khoản '{input.TenKhoan}' ({input.SoTien:N0}đ) thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi điều chỉnh khoản lương #{Id}", input.MaPhieuLuong);
            TempData["Error"] = $"Không thể điều chỉnh: {ex.Message}";
        }

        return RedirectToAction(nameof(Detail), new { id = input.MaPhieuLuong });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdjust(int chiTietId, int phieuLuongId)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền xóa khoản điều chỉnh.";
            return RedirectToAction(nameof(Detail), new { id = phieuLuongId });
        }

        try
        {
            await _payrollService.DeleteSalaryItemAsync(chiTietId, CurrentUserName, CurrentUserId);
            TempData["Success"] = "Đã xóa khoản điều chỉnh và cập nhật lại phiếu lương thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa khoản điều chỉnh #{Id}", chiTietId);
            TempData["Error"] = $"Không thể xóa: {ex.Message}";
        }

        return RedirectToAction(nameof(Detail), new { id = phieuLuongId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateEmployee(int id)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền tính lại số liệu nhân viên trong giai đoạn rà soát đối chiếu.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        try
        {
            await _payrollService.RecalculateEmployeeAsync(id, CurrentUserName, CurrentUserId);
            TempData["Success"] = "Đã tính lại toàn bộ phiếu lương cho nhân viên thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tính lại phiếu lương #{Id}", id);
            TempData["Error"] = $"Không thể tính lại: {ex.Message}";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitApproval(int maKyLuong, int thang, int nam)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền kiểm tra và Gửi duyệt bảng lương lên Ban Giám Đốc. Tài khoản Quản trị viên/Ban Giám Đốc không gửi duyệt thay HR.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        try
        {
            await _payrollService.SubmitForApprovalAsync(maKyLuong, CurrentUserName, CurrentUserId);
            TempData["Success"] = $"Bộ phận HR ({CurrentUserName}) đã gửi duyệt bảng lương tháng {thang:D2}/{nam} tới Ban Giám Đốc thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi duyệt kỳ #{Id}", maKyLuong);
            TempData["Error"] = $"Không thể gửi duyệt: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int maKyLuong, int thang, int nam)
    {
        if (!IsBoss)
        {
            TempData["Error"] = "Chỉ Ban Giám Đốc / Quản trị viên (Admin/Manager) mới có thẩm quyền Phê duyệt bảng lương.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        try
        {
            await _payrollService.ApprovePayrollAsync(maKyLuong, CurrentUserName, CurrentUserId);
            TempData["Success"] = $"Ban Giám Đốc / Sếp ({CurrentUserName}) đã phê duyệt bảng lương tháng {thang:D2}/{nam} thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi duyệt kỳ #{Id}", maKyLuong);
            TempData["Error"] = $"Không thể duyệt: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectPayrollInput input, int thang, int nam)
    {
        if (!IsBoss)
        {
            TempData["Error"] = "Chỉ Ban Giám Đốc / Quản trị viên (Admin/Manager) mới có quyền Từ chối bảng lương.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        if (string.IsNullOrWhiteSpace(input.LyDo))
        {
            TempData["Error"] = "Vui lòng nhập lý do từ chối bảng lương.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        try
        {
            await _payrollService.RejectPayrollAsync(input.MaKyLuong, CurrentUserName, input.LyDo, CurrentUserId);
            TempData["Success"] = $"Sếp ({CurrentUserName}) đã từ chối bảng lương tháng {thang:D2}/{nam} và chuyển lại cho bộ phận HR rà soát kèm lý do.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi từ chối kỳ #{Id}", input.MaKyLuong);
            TempData["Error"] = $"Không thể từ chối: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(int maKyLuong, int thang, int nam)
    {
        if (!IsBoss)
        {
            TempData["Error"] = "Chỉ Ban Giám Đốc / Quản trị viên (Admin/Manager) mới có quyền Khóa sổ bảng lương.";
            return RedirectToAction(nameof(Index), new { thang, nam });
        }

        try
        {
            await _payrollService.LockPayrollAsync(maKyLuong, CurrentUserName, CurrentUserId);
            TempData["Success"] = $"Đã khóa sổ bảng lương tháng {thang:D2}/{nam}. Bảng lương đã đóng băng an toàn!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi khóa sổ kỳ #{Id}", maKyLuong);
            TempData["Error"] = $"Không thể khóa sổ: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int maKyLuong, int thang, int nam)
    {
        if (!IsHR && !IsBoss) return Forbid();

        try
        {
            await _payrollService.PublishPayslipsAsync(maKyLuong, CurrentUserName, CurrentUserId);
            TempData["Success"] = $"Đã gửi phiếu lương tháng {thang:D2}/{nam} thành công! Nhân viên có thể xem trên ứng dụng di động.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi phát hành phiếu lương #{Id}", maKyLuong);
            TempData["Error"] = $"Không thể gửi phiếu lương: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { thang, nam });
    }

    [HttpGet]
    public async Task<IActionResult> ExportBankTransfer(int maKyLuong)
    {
        if (!IsHR && !IsBoss) return Forbid();

        try
        {
            var (fileName, content) = await _payrollService.ExportBankTransferFileAsync(maKyLuong);
            return File(content, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file chuyển khoản #{Id}", maKyLuong);
            TempData["Error"] = $"Không thể xuất file chuyển khoản: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}
