using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize]
public class BangLuongController(
    IPayrollCalculationService payrollService,
    AppDbContext context,
    ILogger<BangLuongController> logger) : Controller
{
    private readonly IPayrollCalculationService _payrollService = payrollService;
    private readonly AppDbContext _context = context;
    private readonly ILogger<BangLuongController> _logger = logger;

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    private string CurrentUserName =>
        User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Người dùng";

    // Quản trị viên hệ thống kiêm Ban Giám Đốc (Sếp phê duyệt, khóa sổ)
    private static bool IsHoliday(DateOnly date)
    {
        if (date.Month == 1 && date.Day == 1) return true;
        if (date.Month == 4 && date.Day == 30) return true;
        if (date.Month == 5 && date.Day == 1) return true;
        if (date.Month == 9 && (date.Day == 1 || date.Day == 2 || date.Day == 3)) return true;
        return false;
    }

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

        var periods = await this.SearchDataAsync("payroll:periods", () => _payrollService.GetAllPeriodsAsync());
        var currentPeriod = await this.SearchDataAsync($"payroll:period:{targetNam}:{targetThang}",
            () => _payrollService.GetPeriodWithDetailsAsync(targetThang, targetNam));

        if (currentPeriod == null)
        {
            currentPeriod = await _payrollService.GetOrCreatePeriodAsync(targetThang, targetNam);
            currentPeriod = await this.SearchDataAsync($"payroll:period:{targetNam}:{targetThang}",
                () => _payrollService.GetPeriodWithDetailsAsync(targetThang, targetNam));
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
        var isLocked = periodStatus == SalaryConstants.PERIOD_LOCKED;
        var isApproved = !isLocked && (currentPeriod?.NgayDuyet != null || periodStatus == "DA_DUYET");
        var isPendingApproval = !isLocked && !isApproved && currentPeriod?.NgayGuiDuyet != null && currentPeriod?.NgayDuyet == null && string.IsNullOrEmpty(currentPeriod?.LyDoTuChoi);
        var isOpenOrDraft = !isLocked && !isPendingApproval && !isApproved;

        var showPayrollData = IsHR || isPendingApproval || isApproved || isLocked;

        var rows = showPayrollData ? query.Select(p => new PhieuLuongRowViewModel
        {
            MaPhieuLuong = p.MaPhieuLuong,
            MaNhanVien = p.MaNhanVien,
            HoTen = p.NhanVien != null ? p.NhanVien.HoTen : "N/A",
            PhongBan = p.NhanVien != null && p.NhanVien.PhongBan != null ? p.NhanVien.PhongBan.TenPhongBan : "N/A",
            ChucVu = p.NhanVien != null && p.NhanVien.ChucVu != null ? p.NhanVien.ChucVu.TenChucVu : "N/A",
            LuongCoBan = p.LuongCoBan,
            NgayCongThucTe = p.NgayCongThucTe,
            TienLuongCong = p.TienLuongCong,
            TienTangCa = p.TienTangCa,
            TongPhuCap = p.TongPhuCap,
            TongThuong = p.TongThuong,
            TienSuKien = p.ChiTietPhieuLuongs
                .Where(d => (d.LoaiKhoan == "PHU_CAP" && d.TenKhoanLuu.StartsWith("Phụ cấp sự kiện"))
                         || (d.LoaiKhoan == "THUONG" && (d.TenKhoanLuu.StartsWith("Thưởng sự kiện")
                                                      || d.TenKhoanLuu.StartsWith("Tiền sự kiện")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng Tiệc cưới")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng Đoàn VIP")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng Gala Dinner")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng MICE")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng Festival")
                                                      || d.TenKhoanLuu.StartsWith("Thưởng dự án")
                                                      || (d.GhiChu != null && d.GhiChu.Contains("phân công"))
                                                      || (d.GhiChu != null && d.GhiChu.Contains("Quyết toán sự kiện")))))
                .Sum(d => (decimal?)d.SoTien) ?? 0m,
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
            CanCalculate = IsHR && !isLocked && !isPendingApproval && !isApproved,
            CanSubmitApproval = IsHR && isOpenOrDraft && query.Any(),
            CanApprove = IsBoss && isPendingApproval,
            CanLock = IsBoss && isApproved,
            CanPublishAndExport = (IsBoss || IsHR) && isLocked,
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
        var (error, model) = await BuildPhieuLuongDetailAsync(id);
        if (error != null) return error;

        return View(model!);
    }

    /// <summary>
    /// Thân phiếu lương rời, phục vụ popup nhiều tab nạp tại chỗ ở trang danh sách
    /// (không dựng lại header/sidebar — đúng nguyên tắc Zero Full-Page Reload).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DetailPartial(int id)
    {
        var (error, model) = await BuildPhieuLuongDetailAsync(id);
        if (error != null) return error;

        return PartialView("_PayslipDetailBody", model!);
    }

    /// <summary>
    /// Dựng model phiếu lương dùng chung cho cả trang riêng và popup.
    /// Trả về (null, model) khi thành công; ngược lại trả về ActionResult lỗi/điều hướng.
    /// </summary>
    private async Task<(IActionResult? Error, PhieuLuongDetailViewModel? Model)> BuildPhieuLuongDetailAsync(int id)
    {
        var payslip = await _payrollService.GetPayslipDetailAsync(id);
        if (payslip == null) return (NotFound(), null);

        var ky = payslip.KyLuong;
        var periodStatus = ky?.TrangThai ?? SalaryConstants.PERIOD_OPEN;

        var isLockedPeriod = periodStatus == SalaryConstants.PERIOD_LOCKED;
        var isPendingOrApproved = isLockedPeriod || (ky?.NgayGuiDuyet != null && string.IsNullOrEmpty(ky?.LyDoTuChoi));
        if (!IsHR && !isPendingOrApproved)
        {
            TempData["Error"] = $"Bảng lương tháng {ky?.Thang:D2}/{ky?.Nam} đang ở giai đoạn rà soát nội bộ của bộ phận HR. Ban Giám Đốc chỉ xem chi tiết sau khi HR bấm 'Gửi duyệt'.";
            return (RedirectToAction(nameof(Index), new { thang = ky?.Thang, nam = ky?.Nam }), null);
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
            .Where(d => (d.LoaiKhoan == "PHU_CAP" && d.TenKhoanLuu.StartsWith("Phụ cấp sự kiện"))
                     || (d.LoaiKhoan == "THUONG" && (d.TenKhoanLuu.StartsWith("Thưởng sự kiện")
                                                  || d.TenKhoanLuu.StartsWith("Tiền sự kiện")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng Tiệc cưới")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng Đoàn VIP")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng Gala Dinner")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng MICE")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng Festival")
                                                  || d.TenKhoanLuu.StartsWith("Thưởng dự án")
                                                  || d.GhiChu?.Contains("phân công") == true
                                                  || d.GhiChu?.Contains("Quyết toán sự kiện") == true)))
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
            .Where(d => d.LoaiKhoan == "THUONG" && !eventItems.Any(e => e.MaChiTiet == d.MaChiTiet))
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

        // 1. Phân loại vi phạm giờ giấc & kỷ luật
        var lateItems = violationItems.Where(v => v.LoaiTru == "DI_TRE").ToList();
        var earlyItems = violationItems.Where(v => v.LoaiTru == "VE_SOM").ToList();
        var missItems = violationItems.Where(v => v.LoaiTru == "KY_LUAT" || v.LyDoChiTiet.Contains("Bỏ ca")).ToList();

        int soLanDiMuon = lateItems.Count;
        int tongPhutDiMuon = lateItems.Sum(v => v.SoPhutViPham);
        int soLanVeSom = earlyItems.Count;
        int tongPhutVeSom = earlyItems.Sum(v => v.SoPhutViPham);
        int soCaBo = missItems.Count;

        // 2. Nạp dữ liệu phân ca & đơn từ chấm công thực tế trong kỳ
        decimal ngayDiLam = 0;
        decimal ngayNghiPhep = 0;
        decimal ngayNghiKhongLuong = 0;
        decimal tongGioOt = 0;
        decimal gioOtThuong = 0;
        decimal gioOtCuoiTuan = 0;
        decimal gioOtNgayLe = 0;

        if (ky != null)
        {
            var empShifts = await _context.PhanCas
                .AsNoTracking()
                .Include(s => s.CaLamViec)
                .Include(s => s.ChamCong)
                .Where(s => s.MaNhanVien == payslip.MaNhanVien
                         && s.NgayLamViec >= ky.TuNgay
                         && s.NgayLamViec <= ky.DenNgay)
                .ToListAsync();

            var empLeaves = await _context.DonTus
                .AsNoTracking()
                .Include(r => r.LoaiDon)
                .Where(r => r.MaNhanVien == payslip.MaNhanVien
                         && r.TrangThai == "DA_DUYET"
                         && r.TuNgay <= ky.DenNgay
                         && r.DenNgay >= ky.TuNgay)
                .ToListAsync();

            if (empShifts.Count > 0)
            {
                var summary = AttendanceSummaryCalculator.Calculate(
                    payslip.MaNhanVien, nv?.HoTen ?? "", ky.TuNgay, ky.DenNgay, empShifts,
                    empLeaves.Where(r => r.LoaiDon?.CoHuongLuong == true));

                ngayDiLam = (decimal)summary.WorkedDays;
                ngayNghiPhep = (decimal)summary.PaidLeaveDays;

                var unpaidRequests = empLeaves.Where(r => r.LoaiDon?.CoHuongLuong == false).ToList();
                foreach (var r in unpaidRequests)
                {
                    var start = Math.Max(ky.TuNgay.DayNumber, r.TuNgay.DayNumber);
                    var end = Math.Min(ky.DenNgay.DayNumber, r.DenNgay.DayNumber);
                    if (end >= start) ngayNghiKhongLuong += (end - start + 1);
                }

                if (soCaBo == 0)
                {
                    soCaBo = empShifts.Count(s => s.TrangThai == "VANG_MAT" || (s.ChamCong != null && s.ChamCong.TrangThai == "VANG_MAT"));
                }

                foreach (var s in empShifts.Where(s => s.ChamCong is { GioTangCaOt: > 0 }))
                {
                    var ot = s.ChamCong!.GioTangCaOt;
                    tongGioOt += ot;
                    if (IsHoliday(s.NgayLamViec))
                        gioOtNgayLe += ot;
                    else if (s.NgayLamViec.DayOfWeek == DayOfWeek.Sunday)
                        gioOtCuoiTuan += ot;
                    else
                        gioOtThuong += ot;
                }
            }
            else
            {
                ngayDiLam = payslip.NgayCongThucTe;
            }
        }
        else
        {
            ngayDiLam = payslip.NgayCongThucTe;
        }

        if (ngayDiLam == 0 && ngayNghiPhep == 0 && payslip.NgayCongThucTe > 0)
        {
            ngayDiLam = payslip.NgayCongThucTe;
        }

        // Bổ sung phân tích giờ OT từ ghi chú nếu bảng ca chưa có chi tiết
        if (tongGioOt == 0 && overtimeItems.Count > 0)
        {
            foreach (var ot in overtimeItems)
            {
                if (!string.IsNullOrEmpty(ot.GhiChu))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(ot.GhiChu, @"([\d\.,]+)\s*giờ");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hours))
                    {
                        tongGioOt += hours;
                        if (ot.TenKhoan.Contains("Lễ") || ot.TenKhoan.Contains("Tết")) gioOtNgayLe += hours;
                        else if (ot.TenKhoan.Contains("nghỉ") || ot.TenKhoan.Contains("tuần")) gioOtCuoiTuan += hours;
                        else gioOtThuong += hours;
                    }
                }
            }
        }

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
            NgayDiLam = ngayDiLam,
            NgayNghiPhep = ngayNghiPhep,
            NgayNghiKhongLuong = ngayNghiKhongLuong,
            SoCaBo = soCaBo,
            TongGioOt = tongGioOt,
            GioOtThuong = gioOtThuong,
            GioOtCuoiTuan = gioOtCuoiTuan,
            GioOtNgayLe = gioOtNgayLe,
            SoLanDiMuon = soLanDiMuon,
            TongPhutDiMuon = tongPhutDiMuon,
            SoLanVeSom = soLanVeSom,
            TongPhutVeSom = tongPhutVeSom,
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
            CanEdit = IsHR && ky != null && ky.TrangThai != SalaryConstants.PERIOD_LOCKED && ky.TrangThai != "DA_DUYET"
                && !ky.NgayGuiDuyet.HasValue && !ky.NgayDuyet.HasValue,
            IsHR = IsHR,
            SuccessMessage = TempData["Success"]?.ToString(),
            ErrorMessage = TempData["Error"]?.ToString()
        };

        return (null, model);
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
    public async Task<IActionResult> Adjust(AdjustSalaryItemInput input, string? returnUrl = null)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền kiểm tra, đối soát và điều chỉnh số liệu bảng lương nháp.";
            return RedirectBack(returnUrl, input.MaPhieuLuong);
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu điều chỉnh không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectBack(returnUrl, input.MaPhieuLuong);
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

        return RedirectBack(returnUrl, input.MaPhieuLuong);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdjust(int chiTietId, int phieuLuongId, string? returnUrl = null)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền xóa khoản điều chỉnh.";
            return RedirectBack(returnUrl, phieuLuongId);
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

        return RedirectBack(returnUrl, phieuLuongId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateEmployee(int id, string? returnUrl = null)
    {
        if (!IsHR)
        {
            TempData["Error"] = "Chỉ tài khoản Chuyên viên Nhân sự (HR) mới có quyền tính lại số liệu nhân viên trong giai đoạn rà soát đối chiếu.";
            return RedirectBack(returnUrl, id);
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

        return RedirectBack(returnUrl, id);
    }

    private IActionResult RedirectBack(string? returnUrl, int phieuLuongId)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Detail), new { id = phieuLuongId });
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
            TempData["Success"] = $"Đã khóa sổ bảng lương tháng {thang:D2}/{nam}. Bảng lương đã đóng băng an toàn và tự động phát hành gửi toàn bộ phiếu lương tới ứng dụng Mobile của nhân viên!";
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
            TempData["Success"] = $"Đã gửi phiếu lương tháng {thang:D2}/{nam} tới ứng dụng di động của nhân viên thành công!";
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
