using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

public partial class HopDongController
{
    private async Task<KhungLuongCapBac?> FindBandAsync(int? positionId, string? education, DateOnly joined, DateOnly date)
    {
        if (date < joined || date == default) return null;
        var group = await _context.ChucVus.Where(x => x.MaChucVu == positionId).Select(x => x.NhomLuong).FirstOrDefaultAsync();
        if (group == null) return null;
        var months = ReportPolicy.TenureMonths(joined, date);
        var bands = await _context.KhungLuongCapBacs.AsNoTracking().Where(x => x.NhomChucVu == group).ToListAsync();
        return SalaryBandLookup.Find(bands, group, education, months);
    }

    [HttpGet]
    public async Task<IActionResult> SalaryQuote(int? positionId, string? education, DateOnly startDate, int? employeeId)
    {
        var employee = employeeId.HasValue ? await _context.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == employeeId) : null;
        if (employeeId.HasValue && employee == null) return BadRequest(new { message = "Không tìm thấy nhân viên." });
        if (!positionId.HasValue && employee != null)
        {
            var contracts = await _context.HopDongs.AsNoTracking().Include(x => x.PhuLucLuongs).Where(x => x.MaNhanVien == employee.MaNhanVien).ToListAsync();
            try { positionId = EffectiveSalaryResolver.Resolve(contracts, startDate)?.PositionId ?? employee.MaChucVu; }
            catch (InvalidOperationException) { return BadRequest(new { message = "Lịch sử hợp đồng chồng lấn; cần HR đối soát." }); }
        }
        education = string.IsNullOrWhiteSpace(education) ? employee?.TrinhDo : education.Trim();
        var joined = employee?.NgayVaoLam ?? startDate;
        var band = await FindBandAsync(positionId, education, joined, startDate);
        if (band == null) return BadRequest(new { message = "Chọn chức vụ, trình độ hợp lệ và ngày bắt đầu để xác định khung lương." });
        return Json(new { minimum = band.LuongToiThieu, maximum = band.LuongToiDa, probationSalary = band.LuongToiThieu * .85m, education, tenureMonths = ReportPolicy.TenureMonths(joined, startDate) });
    }

    private async Task ValidateSalaryAsync(HopDongFormViewModel model, NhanVien? employee)
    {
        model.Contract.PhuLucLuongs = new List<PhuLucLuong>();
        model.TrinhDo = model.TrinhDo?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(model.TrinhDo) && employee != null) model.TrinhDo = employee.TrinhDo;
        var band = await FindBandAsync(model.PositionId, model.TrinhDo, employee?.NgayVaoLam ?? model.Contract.NgayBatDau, model.Contract.NgayBatDau);
        if (band == null) ModelState.AddModelError(nameof(model.TrinhDo), "Chưa xác định được khung lương; kiểm tra chức vụ, trình độ và ngày vào làm.");
        else if (model.Contract.HinhThucHopDong == "THU_VIEC")
        {
            if (model.Contract.NgayBatDau != DateOnly.FromDateTime(DateTime.Today))
                ModelState.AddModelError("Contract.NgayBatDau", "Hợp đồng thử việc mới bắt đầu từ hôm nay.");
            else model.Contract.NgayKetThuc = model.Contract.NgayBatDau.AddMonths(2).AddDays(-1);
            model.Contract.LuongCoBan = band.LuongToiThieu * .85m;
            ModelState.Remove("Contract.LuongCoBan");
            ModelState.Remove("Contract.NgayKetThuc");
        }
        else if (model.Contract.LuongCoBan < band.LuongToiThieu || model.Contract.LuongCoBan > band.LuongToiDa)
            ModelState.AddModelError("Contract.LuongCoBan", $"Lương phải nằm trong khoảng {band.LuongToiThieu:N0}–{band.LuongToiDa:N0} đ/tháng.");
        if (employee?.MaNhanVien > 0 && (employee.MaChucVu != model.PositionId || employee.MaPhongBan != model.DepartmentId))
            ModelState.AddModelError("PositionId", "Điều chuyển nhân viên hiện có qua phụ lục để lưu ngày hiệu lực và lịch sử.");
        model.Contract.TrinhDoKyKet = model.TrinhDo;
        model.Contract.MaPhongBanKyKet = model.DepartmentId;
        model.Contract.MaChucVuKyKet = model.PositionId;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSalaryAmendment(int contractId, DateOnly effectiveDate, decimal salary, string? reason, int? departmentId, int? positionId, bool confirmBaseline = false)
        => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            AddSalaryAmendmentCore(contractId, effectiveDate, salary, reason, departmentId, positionId, confirmBaseline));

    private async Task<IActionResult> AddSalaryAmendmentCore(int contractId, DateOnly effectiveDate, decimal salary, string? reason, int? departmentId, int? positionId, bool confirmBaseline)
    {
        if (!ModelState.IsValid) return BadRequest("Dữ liệu phụ lục không hợp lệ; kiểm tra ngày, mức lương và chức vụ.");
        var tx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await using var transaction = tx;
        if (_context.Database.IsNpgsql()) await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
        var contract = await _context.HopDongs.Include(x => x.NhanVien).Include(x => x.PhuLucLuongs).FirstOrDefaultAsync(x => x.MaHopDong == contractId);
        if (contract?.NhanVien == null) return NotFound();
        var employee = contract.NhanVien;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (effectiveDate.Day != 1 || effectiveDate < contract.NgayBatDau || contract.NgayKetThuc < effectiveDate || contract.TrangThai is "DA_CHAM_DUT" or "CHAM_DUT" || contract.HinhThucHopDong == "THU_VIEC")
            return BadRequest("Phụ lục chỉ áp dụng ngày 1 trong thời gian hợp đồng chính thức hợp lệ.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return BadRequest("Nhập lý do điều chỉnh, tối đa 1.000 ký tự.");
        if (await _context.KyLuongs.AnyAsync(x => x.DenNgay >= effectiveDate && (contract.NgayKetThuc == null || x.TuNgay <= contract.NgayKetThuc) && (x.NgayDuyet != null || x.TrangThai == "DA_DUYET" || x.TrangThai == "DA_THANH_TOAN" || x.TrangThai == "KHOA_SO" || x.TrangThai == "CHO_DUYET" || (x.NgayGuiDuyet != null && x.NgayDuyet == null && (x.LyDoTuChoi == null || x.LyDoTuChoi == "")))))
            return BadRequest("Ngày hiệu lực ảnh hưởng kỳ đã duyệt, đang duyệt hoặc khóa sổ.");
        if (contract.PhuLucLuongs.Any(x => x.NgayHieuLuc == effectiveDate)) return BadRequest("Đã có phụ lục cùng ngày hiệu lực.");
        var previous = contract.PhuLucLuongs.Where(x => x.NgayHieuLuc <= effectiveDate).OrderByDescending(x => x.NgayHieuLuc).FirstOrDefault(x => x.MaChucVuMoi != null);
        positionId ??= previous?.MaChucVuMoi ?? contract.MaChucVuKyKet ?? employee.MaChucVu;
        departmentId ??= previous?.MaPhongBanMoi ?? contract.MaPhongBanKyKet ?? employee.MaPhongBan;
        var department = await _context.PhongBans.FindAsync(departmentId);
        if (!await _context.ChucVus.Where(OrganizationPositionPolicy.ForDepartment(department)).AnyAsync(x => x.MaChucVu == positionId)) return BadRequest("Chức vụ không thuộc phòng ban đã chọn.");
        var band = await FindBandAsync(positionId, employee.TrinhDo, employee.NgayVaoLam, effectiveDate);
        if (band == null || salary < band.LuongToiThieu || salary > band.LuongToiDa) return BadRequest("Mức lương mới phải nằm trong khung của nhân viên tại ngày hiệu lực.");
        var oldPosition = previous?.MaChucVuMoi ?? contract.MaChucVuKyKet ?? employee.MaChucVu;
        var oldDepartment = previous?.MaPhongBanMoi ?? contract.MaPhongBanKyKet ?? employee.MaPhongBan;
        var moving = oldPosition != positionId || oldDepartment != departmentId;
        try
        {
        if (moving && contract.MaChucVuKyKet == null)
        {
            if (!confirmBaseline) return BadRequest("Đối chiếu và xác nhận chức vụ, phòng ban gốc trước khi điều chuyển hợp đồng cũ.");
            // HR explicitly verified the signed baseline; original salary/dates are preserved.
            contract.MaChucVuKyKet = employee.MaChucVu;
            contract.MaPhongBanKyKet = employee.MaPhongBan;
            contract.TrinhDoKyKet = employee.TrinhDo;
            await _context.SaveChangesAsync();
        }
        _context.PhuLucLuongs.Add(new PhuLucLuong { MaHopDong = contractId, NgayHieuLuc = effectiveDate, LuongCoBanMoi = salary, LyDo = reason.Trim(), NguoiTao = User.Identity?.Name ?? "HR", TaoLuc = DateTimeOffset.UtcNow, MaChucVuMoi = moving ? positionId : null, MaPhongBanMoi = moving ? departmentId : null });
        await _context.SaveChangesAsync();
        if (moving && effectiveDate <= today)
        {
            var active = await _context.HopDongs.Include(x => x.PhuLucLuongs).Where(x => x.MaNhanVien == employee.MaNhanVien).ToListAsync();
            var current = EffectiveSalaryResolver.Resolve(active, today);
            if (current != null) { employee.MaChucVu = current.PositionId ?? employee.MaChucVu; employee.MaPhongBan = current.DepartmentId ?? employee.MaPhongBan; }
            await _context.SaveChangesAsync();
        }
        if (tx != null) await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or Npgsql.PostgresException or InvalidOperationException) { return BadRequest("Không thể lưu phụ lục: kiểm tra ghế Tổng Giám Đốc, kỳ lương hoặc dữ liệu vừa thay đổi."); }
        TempData["Success"] = "Đã lập phụ lục điều chỉnh lương.";
        return Json(new { success = true, message = "Đã lập phụ lục điều chỉnh lương." });
    }
    [HttpGet]
    public async Task<IActionResult> SalaryAmendments(int id)
    {
        var contract = await _context.HopDongs.AsNoTracking().Include(x => x.NhanVien).Include(x => x.PhuLucLuongs).FirstOrDefaultAsync(x => x.MaHopDong == id);
        if (contract?.NhanVien == null) return NotFound();
        var positions = await _context.ChucVus.AsNoTracking().ToListAsync();
        var departments = await _context.PhongBans.AsNoTracking().ToListAsync();
        return PartialView("_SalaryAmendments", new HopDongSalaryAmendmentsViewModel
        {
            RequiresBaselineConfirmation = contract.MaChucVuKyKet == null,
            ContractId = id, EmployeeId = contract.MaNhanVien, ContractNumber = contract.SoHopDong,
            EmployeeName = contract.NhanVien.HoTen, Education = contract.NhanVien.TrinhDo,
            PositionId = contract.NhanVien.MaChucVu, DepartmentId = contract.NhanVien.MaPhongBan,
            CanAdd = contract.HinhThucHopDong != "THU_VIEC" && contract.TrangThai == "HIEU_LUC" && (contract.NgayKetThuc == null || contract.NgayKetThuc >= DateOnly.FromDateTime(DateTime.Today)),
            Items = contract.PhuLucLuongs.OrderByDescending(x => x.NgayHieuLuc).Select(x => new HopDongSalaryAmendmentItemViewModel { EffectiveDate = x.NgayHieuLuc, Salary = x.LuongCoBanMoi, Reason = x.LyDo, PositionName = positions.FirstOrDefault(p => p.MaChucVu == x.MaChucVuMoi)?.TenChucVu ?? "Giữ nguyên" }).ToList(),
            DepartmentOptions = departments.Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.TenPhongBan, x.MaPhongBan.ToString())).ToList(),
            PositionOptions = positions.Where(OrganizationPositionPolicy.ForDepartment(departments.FirstOrDefault(d => d.MaPhongBan == contract.NhanVien.MaPhongBan)).Compile()).Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.TenChucVu, x.MaChucVu.ToString())).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> SalaryReviews()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var contracts = await _context.HopDongs.AsNoTracking().Include(x => x.NhanVien).Include(x => x.PhuLucLuongs)
            .Where(x => x.TrangThai == "HIEU_LUC" && x.NgayBatDau <= today && (x.NgayKetThuc == null || x.NgayKetThuc >= today) && x.HinhThucHopDong != "THU_VIEC").ToListAsync();
        // Read the small fixed catalogs once, rather than two database trips per employee.
        var groups = contracts.Count == 0 ? new Dictionary<int, string?>()
            : await _context.ChucVus.AsNoTracking().ToDictionaryAsync(x => x.MaChucVu, x => x.NhomLuong);
        var bands = contracts.Count == 0 ? new List<KhungLuongCapBac>()
            : await _context.KhungLuongCapBacs.AsNoTracking().ToListAsync();
        var result = new List<SalaryReviewItem>();
        var activeByEmployee = contracts.ToLookup(x => x.MaNhanVien);
        foreach (var contract in contracts)
        {
            var employee = contract.NhanVien;
            if (employee == null || employee.TrangThai == "DA_THOI_VIEC") continue;
            var latest = contract.PhuLucLuongs.Where(x => x.NgayHieuLuc <= today).OrderByDescending(x => x.NgayHieuLuc).FirstOrDefault();
            var lastReview = latest?.NgayHieuLuc ?? contract.NgayBatDau;
            var tenure = ReportPolicy.TenureMonths(employee.NgayVaoLam, today);
            var previous = ReportPolicy.TenureMonths(employee.NgayVaoLam, lastReview);
            var tier = tenure < 12 ? 0 : tenure < 36 ? 1 : 2;
            var oldTier = previous < 12 ? 0 : previous < 36 ? 1 : 2;
            if (activeByEmployee[contract.MaNhanVien].Count() > 1)
            {
                result.Add(new SalaryReviewItem(contract.MaHopDong, employee.HoTen, tenure, null, null, null,
                    "Có nhiều hợp đồng cùng hiệu lực; cần đối soát thời gian hợp đồng."));
                continue;
            }
            EffectiveSalary? effective;
            try
            {
                effective = EffectiveSalaryResolver.Resolve(new[] { contract }, today);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Hợp đồng {ContractId} cần xác minh trong danh sách xét lương.", contract.MaHopDong);
                var recorded = latest?.LuongCoBanMoi ?? contract.LuongCoBan;
                result.Add(new SalaryReviewItem(contract.MaHopDong, employee.HoTen, tenure, null, null, null,
                    recorded <= 0 ? "Chưa có mức lương hợp lệ; cần kiểm tra hợp đồng/phụ lục."
                                  : "Lịch sử phụ lục chưa hợp lệ; cần kiểm tra ngày hiệu lực và các phụ lục trùng nhau."));
                continue;
            }
            if (effective == null) continue;
            var positionId = effective.PositionId ?? employee.MaChucVu;
            var group = positionId.HasValue ? groups.GetValueOrDefault(positionId.Value) : null;
            var band = employee.NgayVaoLam > today ? null : SalaryBandLookup.Find(bands, group, employee.TrinhDo, tenure);
            var salary = effective.MonthlySalary;
            var tenureReviewDue = band != null && band.NhomChucVu != "TONG_GIAM_DOC" && tier > oldTier;
            if (band == null || tenureReviewDue || salary < band.LuongToiThieu || salary > band.LuongToiDa)
            {
                var reason = band == null ? "Cần xác minh trình độ hoặc nhóm chức vụ để xác định khung lương."
                    : salary < band.LuongToiThieu || salary > band.LuongToiDa ? "Mức lương hiện tại nằm ngoài khung tham chiếu; cần HR xem xét."
                    : "Đã đến mốc thâm niên xét lương.";
                result.Add(new SalaryReviewItem(contract.MaHopDong, employee.HoTen, tenure, salary, band?.LuongToiThieu, band?.LuongToiDa, reason));
            }
        }
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return PartialView("_SalaryReviewList", result);
        return View("SalaryReviews", result);
    }

    public sealed record SalaryReviewItem(int ContractId, string Name, int Months, decimal? Salary, decimal? Minimum, decimal? Maximum, string Reason);

}
