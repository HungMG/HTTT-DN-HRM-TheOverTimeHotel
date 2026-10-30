using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Enums;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Quản lý Đơn từ trực tuyến: Duyệt và xem lại đơn từ của nhân viên nộp từ Mobile App.
/// </summary>
[Authorize]
public class DonTuController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<DonTuController> _logger;

    public DonTuController(AppDbContext context, ILogger<DonTuController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Màn hình chính danh sách đơn từ trực tuyến (Chờ duyệt, Đã xử lý / Xem lại, Tất cả).
    /// Hỗ trợ lọc theo loại đơn, phòng ban, trạng thái, khoảng ngày và từ khóa.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string view = "pending",
        string type = "all",
        int? phongBanId = null,
        string? status = null,
        string? keyword = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int page = 1,
        int pageSize = 10,
        bool isPartial = false)
    {
        var role = GetCurrentRoleName();
        if (!LeaveApprovalPolicy.CanAccessWebPortal(role))
        {
            return Forbid();
        }

        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        if (normalizedRole == null || normalizedRole == RoleType.Employee)
        {
            TempData["Error"] = "Tài khoản nhân viên không có thẩm quyền truy cập duyệt đơn từ.";
            return RedirectToAction("Index", "Home");
        }

        var currentEmployeeId = GetCurrentEmployeeId();
        var currentDeptId = GetCurrentDepartmentId();

        // 1. Base Query kèm đầy đủ liên kết
        var baseQuery = _context.DonTus
            .AsNoTracking()
            .Include(d => d.LoaiDon)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .Include(d => d.NguoiDuyet)
                .ThenInclude(nd => nd!.ChucVu)
            .AsQueryable();

        // 2. Phân quyền xem dữ liệu theo RBAC
        IQueryable<DonTu> authorizedQuery;
        if (normalizedRole == RoleType.Admin)
        {
            authorizedQuery = baseQuery;
        }
        else if (normalizedRole == RoleType.HR)
        {
            // HR xử lý toàn bộ 5 loại đơn trên toàn khách sạn
            authorizedQuery = baseQuery.Where(d => d.MaLoaiDon >= 1 && d.MaLoaiDon <= 5);
        }
        else // Manager
        {
            if (!currentDeptId.HasValue)
            {
                TempData["Error"] = "Tài khoản quản lý chưa được liên kết với phòng ban.";
                return RedirectToAction("Index", "Home");
            }

            var managerDept = currentDeptId.Value;
            authorizedQuery = baseQuery.Where(d =>
                (d.MaLoaiDon == 1 || d.MaLoaiDon == 2 || d.MaLoaiDon == 4) &&
                d.NhanVien != null &&
                d.NhanVien.MaPhongBan == managerDept);
        }

        var currentYear = DateTime.Today.Year;
        // Cache the source and its dependent maps together, under the same generation.
        var snapshot = await this.SearchDataAsync(
            $"leave-requests:year:{currentYear}:role:{normalizedRole}:department:{currentDeptId}:employee:{currentEmployeeId}",
            async () =>
            {
                var loadedRequests = await authorizedQuery.ToListAsync();
                // Tải số ngày phép đã dùng cho toàn bộ nhân viên trong phạm vi được xem.
                var empIds = loadedRequests.Select(d => d.MaNhanVien).Distinct().ToList();
                var approvedLeaveDaysMap = await _context.DonTus
                    .AsNoTracking()
                    .Where(d => empIds.Contains(d.MaNhanVien) && d.MaLoaiDon == 1 && d.TrangThai == "DA_DUYET" && d.TuNgay.Year == currentYear)
                    .GroupBy(d => d.MaNhanVien)
                    .Select(g => new
                    {
                        MaNhanVien = g.Key,
                        TongNgay = g.Sum(x => x.DenNgay.DayNumber - x.TuNgay.DayNumber + 1)
                    })
                    .ToDictionaryAsync(x => x.MaNhanVien, x => x.TongNgay);

                // Tải hợp đồng gần nhất cho đơn thôi việc
                var contractsMap = await _context.HopDongs
                    .AsNoTracking()
                    .Where(h => empIds.Contains(h.MaNhanVien) && h.TrangThai == "DANG_HIEU_LUC")
                    .GroupBy(h => h.MaNhanVien)
                    .Select(g => g.OrderByDescending(h => h.NgayBatDau).First())
                    .ToDictionaryAsync(h => h.MaNhanVien, h => h);
                return (Requests: loadedRequests, AnnualLeaveDays: approvedLeaveDaysMap, Contracts: contractsMap);
            });
        authorizedQuery = snapshot.Requests.AsQueryable();

        // 3. Đếm các chỉ số KPI
        var totalPending = authorizedQuery.Count(d => d.TrangThai == "CHO_DUYET");
        var totalApproved = authorizedQuery.Count(d => d.TrangThai == "DA_DUYET");
        var totalRejected = authorizedQuery.Count(d => d.TrangThai == "TU_CHOI");
        var totalAll = authorizedQuery.Count();

        // 4. Áp dụng các bộ lọc
        var filteredQuery = authorizedQuery;

        // Góc nhìn View (pending, reviewed, all)
        view = view.ToLowerInvariant();
        if (view == "pending")
        {
            filteredQuery = filteredQuery.Where(d => d.TrangThai == "CHO_DUYET");
        }
        else if (view == "reviewed")
        {
            filteredQuery = filteredQuery.Where(d => d.TrangThai != "CHO_DUYET");
        }
        else if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
        {
            filteredQuery = filteredQuery.Where(d => d.TrangThai == status);
        }

        // Lọc theo loại đơn
        if (!string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) && int.TryParse(type, out var maLoai))
        {
            filteredQuery = filteredQuery.Where(d => d.MaLoaiDon == maLoai);
        }

        // Lọc theo phòng ban (nếu là Admin hoặc HR)
        if (phongBanId.HasValue && phongBanId.Value > 0 && (normalizedRole == RoleType.Admin || normalizedRole == RoleType.HR))
        {
            filteredQuery = filteredQuery.Where(d => d.NhanVien != null && d.NhanVien.MaPhongBan == phongBanId.Value);
        }

        // Lọc theo từ khóa
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            filteredQuery = filteredQuery.Where(d =>
                (d.NhanVien != null && (d.NhanVien.Ho.ToLower().Contains(kw) || d.NhanVien.Ten.ToLower().Contains(kw))) ||
                d.MaDon.ToString().Contains(kw) ||
                d.MaNhanVien.ToString().Contains(kw) ||
                d.LyDo.ToLower().Contains(kw));
        }

        // Lọc theo khoảng ngày nghỉ
        if (fromDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(d => d.DenNgay >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(d => d.TuNgay <= toDate.Value);
        }

        // Sắp xếp
        if (view == "pending")
        {
            filteredQuery = filteredQuery.OrderByDescending(d => d.NgayGui);
        }
        else
        {
            filteredQuery = filteredQuery.OrderByDescending(d => d.NgayDuyet ?? d.NgayGui);
        }

        // 5. Phân trang
        var totalFiltered = filteredQuery.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
        var validPage = Math.Clamp(page, 1, totalPages);

        var pagedList = filteredQuery
            .Skip((validPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 6. Tính toán quyền duyệt & bối cảnh đơn (quỹ phép năm, hợp đồng)
        var items = new List<DonTuItemViewModel>();

        var approvedLeaveDaysMap = snapshot.AnnualLeaveDays;
        var contractsMap = snapshot.Contracts;

        foreach (var d in pagedList)
        {
            var (canReview, errCode, errMsg) = LeaveApprovalPolicy.ValidateReviewAction(
                role: role,
                reviewerDeptId: currentDeptId,
                requestDeptId: d.NhanVien?.MaPhongBan,
                maLoaiDon: d.MaLoaiDon,
                currentStatus: d.TrangThai,
                approved: true,
                phanHoi: null,
                reviewerEmployeeId: currentEmployeeId,
                requestEmployeeId: d.MaNhanVien
            );

            int? phepDaDung = null;
            int? phepConLai = null;
            if (d.MaLoaiDon == 1)
            {
                phepDaDung = approvedLeaveDaysMap.GetValueOrDefault(d.MaNhanVien, 0);
                phepConLai = Math.Max(0, LeaveApprovalPolicy.AnnualLeaveQuotaDays - phepDaDung.Value);
            }

            string? loaiHopDong = null;
            int? ngayBaoTruocThucTe = null;
            int? ngayBaoTruocYeuCau = null;
            if (d.MaLoaiDon == 5 && contractsMap.TryGetValue(d.MaNhanVien, out var hopDong))
            {
                loaiHopDong = hopDong.LoaiHopDong;
                // Trước đây chỗ này so tuyệt đối với "KHONG_XAC_DINH" — một chuỗi
                // không tồn tại trong dữ liệu — nên hợp đồng vô thời hạn bị đọc
                // thành có thời hạn và chỉ đòi báo trước 30 ngày thay vì 45.
                var laKhongXacDinh = ContractTypes.IsIndefinite(hopDong.LoaiHopDong);
                ngayBaoTruocYeuCau = LeaveApprovalPolicy.ResignationNoticeDays[laKhongXacDinh];
                ngayBaoTruocThucTe = d.TuNgay.DayNumber - DateOnly.FromDateTime(d.NgayGui.DateTime).DayNumber;
            }

            items.Add(new DonTuItemViewModel
            {
                MaDon = d.MaDon,
                MaNhanVien = d.MaNhanVien,
                HoTenNhanVien = d.NhanVien != null ? $"{d.NhanVien.Ho} {d.NhanVien.Ten}".Trim() : $"NV#{d.MaNhanVien}",
                ChucVu = d.NhanVien?.ChucVu?.TenChucVu,
                MaPhongBan = d.NhanVien?.MaPhongBan,
                TenPhongBan = d.NhanVien?.PhongBan?.TenPhongBan,
                EmailCongVu = d.NhanVien?.EmailCongVu,
                MaLoaiDon = d.MaLoaiDon,
                TenLoaiDon = d.LoaiDon?.TenLoaiDon ?? $"Loại #{d.MaLoaiDon}",
                CoHuongLuong = d.LoaiDon?.CoHuongLuong ?? false,
                TuNgay = d.TuNgay,
                DenNgay = d.DenNgay,
                LyDo = d.LyDo,
                NgayGui = d.NgayGui,
                TrangThai = d.TrangThai,
                MaNguoiDuyet = d.MaNguoiDuyet,
                HoTenNguoiDuyet = d.NguoiDuyet != null ? $"{d.NguoiDuyet.Ho} {d.NguoiDuyet.Ten}".Trim() : null,
                ChucVuNguoiDuyet = d.NguoiDuyet?.ChucVu?.TenChucVu,
                NgayDuyet = d.NgayDuyet,
                PhanHoiDuyet = d.PhanHoiDuyet,
                CanCurrentUserReview = canReview,
                CannotReviewReason = errMsg,
                PhepNamDaDung = phepDaDung,
                PhepNamConLai = phepConLai,
                LoaiHopDong = loaiHopDong,
                SoNgayBaoTruocThucTe = ngayBaoTruocThucTe,
                SoNgayBaoTruocYeuCau = ngayBaoTruocYeuCau
            });
        }

        // 7. Tạo danh sách chọn lọc cho form
        var loaiDons = await this.SearchDataAsync("leave-requests:types", () => _context.LoaiDons.AsNoTracking().OrderBy(l => l.MaLoaiDon).ToListAsync());
        var phongBans = await this.SearchDataAsync("leave-requests:departments", () => _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync());

        var loaiDonOptions = new List<SelectListItem>
        {
            new("Tất cả loại đơn", "all", type == "all")
        };
        loaiDonOptions.AddRange(loaiDons.Select(l => new SelectListItem(l.TenLoaiDon, l.MaLoaiDon.ToString(), type == l.MaLoaiDon.ToString())));

        var phongBanOptions = new List<SelectListItem>
        {
            new("Tất cả phòng ban", "", !phongBanId.HasValue)
        };
        phongBanOptions.AddRange(phongBans.Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan.ToString(), phongBanId == p.MaPhongBan)));

        var statusOptions = new List<SelectListItem>
        {
            new("Tất cả trạng thái", "ALL", string.IsNullOrEmpty(status) || status == "ALL"),
            new("Chờ duyệt", "CHO_DUYET", status == "CHO_DUYET"),
            new("Đã duyệt", "DA_DUYET", status == "DA_DUYET"),
            new("Từ chối", "TU_CHOI", status == "TU_CHOI"),
            new("Đã rút", "DA_HUY", status == "DA_HUY")
        };

        var viewModel = new DonTuIndexViewModel
        {
            View = view,
            Type = type,
            PhongBanId = phongBanId,
            Status = status,
            Keyword = keyword,
            FromDate = fromDate,
            ToDate = toDate,
            Items = items,
            TotalPendingCount = totalPending,
            TotalApprovedCount = totalApproved,
            TotalRejectedCount = totalRejected,
            TotalCount = totalAll,
            TotalFiltered = totalFiltered,
            LoaiDonOptions = loaiDonOptions,
            PhongBanOptions = phongBanOptions,
            StatusOptions = statusOptions,
            PageNumber = validPage,
            PageSize = pageSize,
            TotalPages = totalPages,
            CurrentRole = role ?? string.Empty,
            CurrentEmployeeId = currentEmployeeId,
            CurrentDepartmentId = currentDeptId
        };

        if (isPartial || IsAjaxRequest())
        {
            Response.Headers["X-Total-Pending"] = totalPending.ToString();
            return PartialView("_DonTuTablePartial", viewModel);
        }

        return View(viewModel);
    }

    /// <summary>
    /// Lấy chi tiết đơn từ dưới dạng JSON phục vụ hiển thị Modal chi tiết hoặc xem lại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var role = GetCurrentRoleName();
        var currentEmployeeId = GetCurrentEmployeeId();
        var currentDeptId = GetCurrentDepartmentId();

        var donTu = await _context.DonTus
            .AsNoTracking()
            .Include(d => d.LoaiDon)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.ChucVu)
            .Include(d => d.NguoiDuyet)
                .ThenInclude(nd => nd!.ChucVu)
            .FirstOrDefaultAsync(d => d.MaDon == id);

        if (donTu == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy đơn từ." });
        }

        var (canReview, _, errMsg) = LeaveApprovalPolicy.ValidateReviewAction(
            role: role,
            reviewerDeptId: currentDeptId,
            requestDeptId: donTu.NhanVien?.MaPhongBan,
            maLoaiDon: donTu.MaLoaiDon,
            currentStatus: donTu.TrangThai,
            approved: true,
            phanHoi: null,
            reviewerEmployeeId: currentEmployeeId,
            requestEmployeeId: donTu.MaNhanVien
        );

        // Tính ngày phép năm đã dùng nếu là loại 1
        int? phepDaDung = null;
        int? phepConLai = null;
        if (donTu.MaLoaiDon == 1)
        {
            var curYear = DateTime.Today.Year;
            phepDaDung = await _context.DonTus
                .AsNoTracking()
                .Where(d => d.MaNhanVien == donTu.MaNhanVien && d.MaLoaiDon == 1 && d.TrangThai == "DA_DUYET" && d.TuNgay.Year == curYear)
                .SumAsync(d => (int?)(d.DenNgay.DayNumber - d.TuNgay.DayNumber + 1)) ?? 0;
            phepConLai = Math.Max(0, LeaveApprovalPolicy.AnnualLeaveQuotaDays - phepDaDung.Value);
        }

        // Thông tin hợp đồng nếu là loại 5 thôi việc
        string? loaiHopDong = null;
        int? ngayBaoTruocThucTe = null;
        int? ngayBaoTruocYeuCau = null;
        if (donTu.MaLoaiDon == 5)
        {
            var hopDong = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.MaNhanVien == donTu.MaNhanVien && h.TrangThai == "DANG_HIEU_LUC")
                .OrderByDescending(h => h.NgayBatDau)
                .FirstOrDefaultAsync();

            if (hopDong != null)
            {
                loaiHopDong = hopDong.LoaiHopDong;
                // Trước đây chỗ này so tuyệt đối với "KHONG_XAC_DINH" — một chuỗi
                // không tồn tại trong dữ liệu — nên hợp đồng vô thời hạn bị đọc
                // thành có thời hạn và chỉ đòi báo trước 30 ngày thay vì 45.
                var laKhongXacDinh = ContractTypes.IsIndefinite(hopDong.LoaiHopDong);
                ngayBaoTruocYeuCau = LeaveApprovalPolicy.ResignationNoticeDays[laKhongXacDinh];
                ngayBaoTruocThucTe = donTu.TuNgay.DayNumber - DateOnly.FromDateTime(donTu.NgayGui.DateTime).DayNumber;
            }
        }

        return Json(new
        {
            success = true,
            data = new
            {
                maDon = donTu.MaDon,
                maNhanVien = donTu.MaNhanVien,
                hoTenNhanVien = donTu.NhanVien != null ? $"{donTu.NhanVien.Ho} {donTu.NhanVien.Ten}".Trim() : $"NV#{donTu.MaNhanVien}",
                maNhanVienCode = $"NV{donTu.MaNhanVien:D3}",
                emailCongVu = donTu.NhanVien?.EmailCongVu,
                soDienThoai = donTu.NhanVien?.DienThoai,
                tenPhongBan = donTu.NhanVien?.PhongBan?.TenPhongBan ?? "Chưa phân phòng",
                tenChucVu = donTu.NhanVien?.ChucVu?.TenChucVu ?? "Chưa phân chức vụ",
                maLoaiDon = donTu.MaLoaiDon,
                tenLoaiDon = donTu.LoaiDon?.TenLoaiDon ?? $"Loại #{donTu.MaLoaiDon}",
                coHuongLuong = donTu.LoaiDon?.CoHuongLuong ?? false,
                tuNgay = donTu.TuNgay.ToString("dd/MM/yyyy"),
                denNgay = donTu.DenNgay.ToString("dd/MM/yyyy"),
                soNgayNghi = Math.Max(1, donTu.DenNgay.DayNumber - donTu.TuNgay.DayNumber + 1),
                lyDo = donTu.LyDo,
                ngayGui = donTu.NgayGui.ToLocalTime().ToString("HH:mm dd/MM/yyyy"),
                trangThai = donTu.TrangThai,
                trangThaiLabel = donTu.TrangThai switch
                {
                    "CHO_DUYET" => "Chờ duyệt",
                    "DA_DUYET" => "Đã duyệt",
                    "TU_CHOI" => "Từ chối",
                    "DA_HUY" => "Đã rút",
                    _ => donTu.TrangThai
                },
                maNguoiDuyet = donTu.MaNguoiDuyet,
                hoTenNguoiDuyet = donTu.NguoiDuyet != null ? $"{donTu.NguoiDuyet.Ho} {donTu.NguoiDuyet.Ten}".Trim() : null,
                chucVuNguoiDuyet = donTu.NguoiDuyet?.ChucVu?.TenChucVu,
                ngayDuyet = donTu.NgayDuyet?.ToLocalTime().ToString("HH:mm dd/MM/yyyy"),
                phanHoiDuyet = donTu.PhanHoiDuyet,
                canReview = canReview,
                cannotReviewReason = errMsg,
                phepDaDung = phepDaDung,
                phepConLai = phepConLai,
                dinhMucPhep = LeaveApprovalPolicy.AnnualLeaveQuotaDays,
                loaiHopDong = loaiHopDong,
                ngayBaoTruocThucTe = ngayBaoTruocThucTe,
                ngayBaoTruocYeuCau = ngayBaoTruocYeuCau,
                hopLeBaoTruoc = ngayBaoTruocThucTe >= ngayBaoTruocYeuCau
            }
        });
    }

    /// <summary>
    /// Xử lý Phê duyệt đơn từ (Approved = true) hoặc Từ chối đơn từ (Approved = false).
    /// Áp dụng cơ chế kiểm tra đồng thời (Optimistic Concurrency) và ghi Nhật ký quản trị.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review([FromBody] DonTuReviewInputModel input)
    {
        if (input == null || input.MaDon <= 0)
        {
            return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        var role = GetCurrentRoleName();
        var currentEmployeeId = GetCurrentEmployeeId();
        var currentDeptId = GetCurrentDepartmentId();
        var currentAccountId = GetCurrentAccountId();
        var currentUsername = User.Identity?.Name ?? "NguoiDung";

        // 1. Tìm đơn từ
        var donTu = await _context.DonTus
            .Include(d => d.NhanVien)
                .ThenInclude(nv => nv!.PhongBan)
            .Include(d => d.LoaiDon)
            .FirstOrDefaultAsync(d => d.MaDon == input.MaDon);

        if (donTu == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy đơn từ yêu cầu." });
        }

        // 2. Thẩm định qua LeaveApprovalPolicy
        var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateReviewAction(
            role: role,
            reviewerDeptId: currentDeptId,
            requestDeptId: donTu.NhanVien?.MaPhongBan,
            maLoaiDon: donTu.MaLoaiDon,
            currentStatus: donTu.TrangThai,
            approved: input.Approved,
            phanHoi: input.PhanHoi,
            reviewerEmployeeId: currentEmployeeId,
            requestEmployeeId: donTu.MaNhanVien
        );

        if (!isValid)
        {
            return BadRequest(new { success = false, code = errorCode, message = errorMessage });
        }

        // 3. Cập nhật nguyên tử ngăn chặn Race Condition
        var newStatus = input.Approved ? "DA_DUYET" : "TU_CHOI";
        var now = DateTimeOffset.UtcNow;
        var feedback = string.IsNullOrWhiteSpace(input.PhanHoi) ? null : input.PhanHoi.Trim();

        var affected = await _context.DonTus
            .Where(d => d.MaDon == input.MaDon && d.TrangThai == "CHO_DUYET")
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TrangThai, newStatus)
                .SetProperty(d => d.NgayDuyet, now)
                .SetProperty(d => d.MaNguoiDuyet, currentEmployeeId)
                .SetProperty(d => d.PhanHoiDuyet, feedback)
            );

        if (affected == 0)
        {
            return Conflict(new { success = false, message = "Đơn từ đã được xử lý bởi người khác trước đó." });
        }

        // 4. Ghi Nhật ký quản trị (Audit Log)
        try
        {
            var actionName = input.Approved ? "DUYET_DON_TU" : "TU_CHOI_DON_TU";
            var actionText = input.Approved ? "Phê duyệt" : "Từ chối";
            var empName = donTu.NhanVien != null ? $"{donTu.NhanVien.Ho} {donTu.NhanVien.Ten}".Trim() : $"NV#{donTu.MaNhanVien}";
            var typeName = donTu.LoaiDon?.TenLoaiDon ?? $"Loại #{donTu.MaLoaiDon}";

            _context.NhatKyQuanTris.Add(new NhatKyQuanTri
            {
                MaTaiKhoanThucHien = currentAccountId,
                TenNguoiThucHien = currentUsername,
                TenTaiKhoanBiTacDong = empName,
                HanhDong = actionName,
                NoiDung = $"{actionText} đơn #{donTu.MaDon} ({typeName}) của nhân viên '{empName}'. Lý do/phản hồi: '{feedback ?? "Không ghi chú"}'.",
                GiaTriCu = "CHO_DUYET",
                GiaTriMoi = newStatus,
                LyDo = feedback,
                DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                ThoiGian = now
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi nhật ký quản trị cho thao tác duyệt đơn #{MaDon}", input.MaDon);
        }

        _logger.LogInformation("Người dùng {User} ({Role}) đã {Action} đơn #{MaDon} thành công.",
            currentUsername, role, input.Approved ? "phê duyệt" : "từ chối", input.MaDon);

        return Json(new
        {
            success = true,
            status = newStatus,
            message = input.Approved ? "Phê duyệt đơn từ thành công!" : "Đã từ chối đơn từ thành công."
        });
    }

    /// <summary>
    /// API trả về số lượng đơn đang chờ duyệt thực tế của user hiện tại (để cập nhật Badge).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PendingCount()
    {
        var role = GetCurrentRoleName();
        var normalizedRole = LeaveApprovalPolicy.NormalizeRole(role);
        if (normalizedRole == null || normalizedRole == RoleType.Employee)
        {
            return Json(new { count = 0 });
        }

        var currentEmployeeId = GetCurrentEmployeeId();
        var currentDeptId = GetCurrentDepartmentId();

        var query = _context.DonTus.AsNoTracking().Where(d => d.TrangThai == "CHO_DUYET");

        if (normalizedRole == RoleType.Admin)
        {
            // Admin thấy tất cả
        }
        else if (normalizedRole == RoleType.HR)
        {
            query = query.Where(d => d.MaLoaiDon >= 1 && d.MaLoaiDon <= 5 && d.MaNhanVien != currentEmployeeId);
        }
        else // Manager
        {
            if (!currentDeptId.HasValue) return Json(new { count = 0 });
            var managerDept = currentDeptId.Value;
            query = query.Where(d =>
                (d.MaLoaiDon == 1 || d.MaLoaiDon == 2 || d.MaLoaiDon == 4) &&
                d.NhanVien != null &&
                d.NhanVien.MaPhongBan == managerDept &&
                d.MaNhanVien != currentEmployeeId);
        }

        var count = await query.CountAsync();
        return Json(new { count });
    }

    #region Helper Methods

    private string? GetCurrentRoleName() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetCurrentEmployeeId() =>
        int.TryParse(User.FindFirstValue("EmployeeId"), out var id) ? id : null;

    private int? GetCurrentDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var d) && d > 0 ? d : null;

    private int? GetCurrentAccountId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
        Request.GetTypedHeaders().Accept?.Any(v => string.Equals(v.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) == true;

    #endregion
}
