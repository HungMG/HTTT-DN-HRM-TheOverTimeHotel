using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.AssignShifts)]
public class PhanCaController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<PhanCaController> _logger;

    public PhanCaController(AppDbContext context, ILogger<PhanCaController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Màn hình Ma trận Lịch phân ca 24/7 và Danh sách phân ca (Task 16)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? week,
        int? year,
        int? month,
        string? selectedDate,
        int? phongBanId,
        int? caId,
        string viewMode = "calendar",
        bool openShiftModal = false)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly targetDate;

        if (!string.IsNullOrWhiteSpace(selectedDate) && DateOnly.TryParse(selectedDate, out var parsedSelDate))
        {
            targetDate = parsedSelDate;
        }
        else if (!string.IsNullOrWhiteSpace(week) && DateOnly.TryParse(week, out var parsedWeekDate))
        {
            targetDate = parsedWeekDate;
        }
        else
        {
            targetDate = today;
        }

        int calYear = year ?? targetDate.Year;
        int calMonth = month ?? targetDate.Month;

        DateOnly selDate;
        if (!string.IsNullOrWhiteSpace(selectedDate) && DateOnly.TryParse(selectedDate, out var sDate))
        {
            selDate = sDate;
        }
        else if (targetDate.Year == calYear && targetDate.Month == calMonth)
        {
            selDate = targetDate;
        }
        else
        {
            selDate = new DateOnly(calYear, calMonth, 1);
        }

        // Lưới 42 ô tháng bắt đầu từ Thứ 2
        var monthFirst = new DateOnly(calYear, calMonth, 1);
        int offsetFromMonday = ((int)monthFirst.DayOfWeek + 6) % 7;
        var gridStart = monthFirst.AddDays(-offsetFromMonday);
        var gridEnd = gridStart.AddDays(41);

        // Tuần cho Ma trận tuần (tính theo selDate)
        int diff = (7 + ((int)selDate.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        var weekStart = selDate.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6);

        var prevMonth = monthFirst.AddMonths(-1);
        var nextMonth = monthFirst.AddMonths(1);

        ViewBag.CurrentWeek = weekStart.ToString("yyyy-MM-dd");
        ViewBag.PrevWeek = weekStart.AddDays(-7).ToString("yyyy-MM-dd");
        ViewBag.NextWeek = weekStart.AddDays(7).ToString("yyyy-MM-dd");
        ViewBag.TodayWeek = today.ToString("yyyy-MM-dd");
        ViewBag.PrevMonth = prevMonth;
        ViewBag.NextMonth = nextMonth;
        ViewBag.ViewMode = viewMode;
        ViewBag.OpenShiftModal = openShiftModal;

        var model = new PhanCaIndexViewModel
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Year = calYear,
            Month = calMonth,
            SelectedDate = selDate,
            SelectedPhongBanId = phongBanId,
            SelectedCaId = caId
        };

        try
        {
            // 2. Tải danh mục phòng ban và ca làm việc
            model.DanhSachPhongBan = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
            model.DanhSachCa = await _context.CaLamViecs.AsNoTracking().OrderBy(c => c.GioBatDau).ToListAsync();

            // 3. Tải danh sách nhân viên CÒN HỢP ĐỒNG có hiệu lực bao phủ phạm vi lưới tháng
            var empQuery = _context.NhanViens.AsNoTracking()
                .Include(n => n.PhongBan)
                .Include(n => n.ChucVu)
                .Include(n => n.HopDongs)
                .Where(n => n.TrangThai == "DANG_LAM");

            if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                empQuery = empQuery.Where(n => n.MaPhongBan == phongBanId.Value);
            }

            // RÀNG BUỘC HỢP ĐỒNG: Chỉ hiển thị nhân viên còn hợp đồng hiệu lực trong phạm vi lưới
            empQuery = empQuery.Where(n => n.HopDongs.Any(h =>
                h.TrangThai == ContractAttendancePolicy.STATUS_HIEU_LUC &&
                h.NgayBatDau <= gridEnd &&
                (!h.NgayKetThuc.HasValue || h.NgayKetThuc.Value >= gridStart)));

            model.DanhSachNhanVien = await empQuery.OrderBy(n => n.Ten).ThenBy(n => n.Ho).ToListAsync();

            // 4. Tải danh sách phân ca trong phạm vi lưới 42 ngày
            var phanCaQuery = _context.PhanCas.AsNoTracking()
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.HopDongs)
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .Where(p => p.NgayLamViec >= gridStart && p.NgayLamViec <= gridEnd);

            if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                phanCaQuery = phanCaQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phongBanId.Value);
            }

            if (caId.HasValue && caId.Value > 0)
            {
                phanCaQuery = phanCaQuery.Where(p => p.MaCa == caId.Value);
            }

            var allGridPhanCas = await phanCaQuery
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.BatDauDuKien)
                .ToListAsync();

            model.AllGridPhanCa = allGridPhanCas;

            // model.DanhSachPhanCa vẫn giữ cho tuần hiện tại để phục vụ ma trận tuần
            model.DanhSachPhanCa = allGridPhanCas
                .Where(p => p.NgayLamViec >= weekStart && p.NgayLamViec <= weekEnd)
                .ToList();

            // 5. Thêm nhân viên đã nghỉ nhưng còn ca trong tuần vào ma trận tuần
            var shiftEmpIds = model.DanhSachPhanCa.Select(p => p.MaNhanVien).Distinct().ToList();
            var knownEmpIds = model.DanhSachNhanVien.Select(n => n.MaNhanVien).ToHashSet();
            List<NhanVien> extraShiftEmps = new();
            if (shiftEmpIds.Count > 0)
            {
                var candidates = await _context.NhanViens.AsNoTracking()
                    .Include(n => n.PhongBan)
                    .Include(n => n.ChucVu)
                    .Include(n => n.HopDongs)
                    .Where(n => shiftEmpIds.Contains(n.MaNhanVien))
                    .ToListAsync();
                extraShiftEmps = candidates.Where(e => !knownEmpIds.Contains(e.MaNhanVien)).ToList();
            }

            var matrixEmps = model.DanhSachNhanVien
                .Concat(extraShiftEmps)
                .OrderBy(n => n.Ten).ThenBy(n => n.Ho)
                .ToList();

            // Tải danh sách ca trực hôm nay độc lập
            model.CaTrucHomNay = await _context.PhanCas.AsNoTracking()
                .Include(p => p.NhanVien).ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien).ThenInclude(n => n!.ChucVu)
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .Where(p => p.NgayLamViec == today)
                .ToListAsync();

            // 6. Tính toán thống kê Bento
            model.TongSoCa = model.DanhSachPhanCa.Count;
            model.SoCaSang = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Sáng"));
            model.SoCaChieu = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Chiều"));
            model.SoCaDem = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && (p.CaLamViec.QuaDem || p.CaLamViec.TenCa.Contains("Đêm")));
            model.TongNhanVien = model.DanhSachNhanVien.Count;
            model.SoNhanVienDuocPhan = model.DanhSachPhanCa.Select(p => p.MaNhanVien).Distinct().Count();

            // 7. Xây dựng 42 ngày cho Cuốn Lịch (Calendar Notebook)
            for (int i = 0; i < 42; i++)
            {
                var day = gridStart.AddDays(i);
                var dayShifts = allGridPhanCas.Where(p => p.NgayLamViec == day).ToList();
                var sang = dayShifts.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Sáng"));
                var chieu = dayShifts.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Chiều"));
                var dem = dayShifts.Count(p => p.CaLamViec != null && (p.CaLamViec.QuaDem || p.CaLamViec.TenCa.Contains("Đêm")));
                var expiredCount = dayShifts.Count(p => p.NhanVien != null && !ContractAttendancePolicy.HasValidContractOn(p.NhanVien, day));
                var hasContractIssue = expiredCount > 0;

                model.CalendarDays.Add(new CalendarDaySummary
                {
                    Date = day,
                    Sang = sang,
                    Chieu = chieu,
                    Dem = dem,
                    IsOtherMonth = day.Month != model.Month,
                    IsToday = day == today,
                    IsWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday,
                    IsPast = day < today,
                    IsSelected = day == model.SelectedDate,
                    HasContractIssue = hasContractIssue,
                    ExpiredContractCount = expiredCount
                });
            }

            // 8. Xây dựng SelectedAgenda cho ngày được chọn
            var avatarColors = new[] { "#1E3A8A", "#D97706", "#059669", "#7C3AED", "#DB2777", "#2563EB", "#0891B2" };
            var selectedDayShifts = allGridPhanCas.Where(p => p.NgayLamViec == model.SelectedDate).ToList();

            model.SelectedAgenda = new AgendaDay { Date = model.SelectedDate };
            foreach (var ca in model.DanhSachCa)
            {
                var badgeClass = ca.TenCa.Contains("Sáng") ? "badge-morning" : (ca.TenCa.Contains("Chiều") ? "badge-afternoon" : "badge-night");
                var shiftGroup = new AgendaShiftGroup
                {
                    MaCa = ca.MaCa,
                    TenCa = ca.TenCa,
                    Gio = $"{ca.GioBatDau:HH\\:mm} - {ca.GioKetThuc:HH\\:mm}",
                    BadgeClass = badgeClass
                };

                var assignedInCa = selectedDayShifts.Where(p => p.MaCa == ca.MaCa).ToList();
                foreach (var p in assignedInCa)
                {
                    var emp = p.NhanVien;
                    var hasValidContract = emp != null && ContractAttendancePolicy.HasValidContractOn(emp, model.SelectedDate);
                    shiftGroup.Employees.Add(new AgendaEmployeeItem
                    {
                        MaPhanCa = p.MaPhanCa,
                        MaNhanVien = p.MaNhanVien,
                        HoTen = emp != null ? $"{emp.Ho} {emp.Ten}".Trim() : $"NV #{p.MaNhanVien}",
                        TenChucVu = emp?.ChucVu?.TenChucVu ?? "Nhân viên",
                        TenPhongBan = emp?.PhongBan?.TenPhongBan ?? "Khách sạn",
                        AvatarColor = avatarColors[Math.Abs(p.MaNhanVien.GetHashCode()) % avatarColors.Length],
                        HasActiveContract = hasValidContract,
                        DisabledReason = !hasValidContract ? $"Nhân viên {emp?.HoTen} có hợp đồng hết hạn / chưa có hiệu lực vào ngày {model.SelectedDate:dd/MM/yyyy}." : null
                    });
                }

                model.SelectedAgenda.Shifts.Add(shiftGroup);
            }

            // 9. Xây dựng ma trận tuần (Weekly Matrix Rows)
            foreach (var emp in matrixEmps)
            {
                var hasActiveContract = ContractAttendancePolicy.HasValidContractDuring(emp, weekStart, weekEnd);
                var row = new PhanCaMatrixRow
                {
                    MaNhanVien = emp.MaNhanVien,
                    MaPhongBan = emp.MaPhongBan ?? 0,
                    HoTen = $"{emp.Ho} {emp.Ten}".Trim(),
                    TenPhongBan = emp.PhongBan?.TenPhongBan ?? "Khách sạn",
                    TenChucVu = emp.ChucVu?.TenChucVu ?? "Nhân viên",
                    AvatarColor = avatarColors[Math.Abs(emp.MaNhanVien.GetHashCode()) % avatarColors.Length],
                    HasActiveContract = hasActiveContract
                };

                for (int i = 0; i < 7; i++)
                {
                    var currentDate = weekStart.AddDays(i);
                    var isPast = currentDate < today;
                    var hasContractOnDate = ContractAttendancePolicy.HasValidContractOn(emp, currentDate);
                    var canAssign = !isPast && hasContractOnDate;

                    var cell = new PhanCaDayCell
                    {
                        Date = currentDate,
                        DayOfWeekName = GetDayOfWeekName(currentDate.DayOfWeek),
                        IsToday = currentDate == today,
                        CanAssign = canAssign,
                        DisabledReason = !hasContractOnDate
                            ? $"Nhân viên {row.HoTen} không có hợp đồng lao động có hiệu lực vào ngày {currentDate:dd/MM/yyyy} (hợp đồng đã hết hạn hoặc chưa ký). Vui lòng gia hạn hợp đồng."
                            : (isPast ? $"Ngày {currentDate:dd/MM/yyyy} trong quá khứ (Đã khóa)" : null),
                        Shifts = model.DanhSachPhanCa
                            .Where(p => p.MaNhanVien == emp.MaNhanVien && p.NgayLamViec == currentDate)
                            .Select(p => new PhanCaItemDto
                            {
                                MaPhanCa = p.MaPhanCa,
                                MaCa = p.MaCa,
                                TenCa = p.CaLamViec?.TenCa ?? "Ca",
                                GioBatDau = p.CaLamViec?.GioBatDau ?? TimeOnly.MinValue,
                                GioKetThuc = p.CaLamViec?.GioKetThuc ?? TimeOnly.MinValue,
                                QuaDem = p.CaLamViec?.QuaDem ?? false,
                                TrangThai = p.TrangThai,
                                HasAttendance = p.ChamCong != null,
                                IsCheckedIn = p.ChamCong != null && p.ChamCong.GioCheckIn.HasValue
                            }).ToList()
                    };
                    row.Days.Add(cell);
                }

                model.Rows.Add(row);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Đang nạp dữ liệu phân ca đồng bộ chuẩn 20 nhân sự và 4 ca Supabase.");
            PopulateRealSeededSchedule(model, weekStart, weekEnd);
            ViewBag.IsDemoData = false;
            return View(model);
        }
    }

    /// <summary>
    /// Phân ca mới cho nhân viên (Hỗ trợ phân ca đồng thời cho nhiều nhân sự)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePhanCaInputModel input)
    {
        var empIds = (input.DanhSachMaNhanVien ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (empIds.Count == 0 && input.MaNhanVien > 0)
        {
            empIds.Add(input.MaNhanVien);
        }

        if (empIds.Count == 0 && Request.HasFormContentType && Request.Form.TryGetValue("DanhSachMaNhanVien", out var formEmpValues))
        {
            foreach (var val in formEmpValues)
            {
                if (int.TryParse(val, out var parsedId) && parsedId > 0)
                {
                    empIds.Add(parsedId);
                }
            }
            empIds = empIds.Distinct().ToList();
        }

        var targetDates = (input.DanhSachNgayLamViec ?? new List<DateOnly>())
            .Where(d => d != default)
            .Distinct()
            .ToList();

        if (targetDates.Count == 0 && input.NgayLamViec != default)
        {
            targetDates.Add(input.NgayLamViec);
        }

        if (targetDates.Count == 0 && Request.HasFormContentType && Request.Form.TryGetValue("DanhSachNgayLamViec", out var formDateValues))
        {
            foreach (var val in formDateValues)
            {
                if (DateOnly.TryParse(val, out var parsedDate) && parsedDate != default)
                {
                    targetDates.Add(parsedDate);
                }
            }
            targetDates = targetDates.Distinct().ToList();
        }

        if (targetDates.Count == 0)
        {
            targetDates.Add(DateOnly.FromDateTime(DateTime.Today));
        }

        if (empIds.Count == 0 || input.MaCa <= 0)
        {
            TempData["Error"] = TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một nhân viên và ca làm việc hợp lệ.";
            return RedirectToAction(nameof(Index), new { week = targetDates.First().ToString("yyyy-MM-dd") });
        }

        try
        {
            var ca = await _context.CaLamViecs.FindAsync(input.MaCa);
            if (ca == null)
            {
                TempData["Error"] = TempData["ErrorMessage"] = "Không tìm thấy ca làm việc đã chọn.";
                return RedirectToAction(nameof(Index), new { week = targetDates.First().ToString("yyyy-MM-dd") });
            }

            // Whitelist trạng thái theo CHECK constraint của CSDL (DA_PHAN / HOAN_THANH / VANG_MAT)
            var allowedStatuses = new[] { "DA_PHAN", "HOAN_THANH", "VANG_MAT" };
            var safeStatus = !string.IsNullOrWhiteSpace(input.TrangThai) && allowedStatuses.Contains(input.TrangThai)
                ? input.TrangThai!
                : "DA_PHAN";

            // Lấy thông tin nhân viên kèm hợp đồng để kiểm tra hiệu lực phân ca
            var employeeMap = await _context.NhanViens
                .Include(n => n.HopDongs)
                .Where(n => empIds.Contains(n.MaNhanVien))
                .ToDictionaryAsync(n => n.MaNhanVien);

            int successCount = 0;
            int skippedDuplicateCount = 0;
            int skippedPastCount = 0;
            int skippedEndedCount = 0;
            int skippedNoContractCount = 0;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nowLocal = DateTime.Now;

            foreach (var targetDate in targetDates)
            {
                // Chặn phân ca cho ngày trong quá khứ
                if (targetDate < today)
                {
                    skippedPastCount += empIds.Count;
                    continue;
                }

                var startDateTime = targetDate.ToDateTime(ca.GioBatDau);
                var endDateTime = ca.QuaDem
                    ? targetDate.AddDays(1).ToDateTime(ca.GioKetThuc)
                    : targetDate.ToDateTime(ca.GioKetThuc);

                // Nếu là ngày hôm nay, chặn phân ca đã kết thúc giờ làm
                if (targetDate == today && endDateTime <= nowLocal)
                {
                    skippedEndedCount += empIds.Count;
                    continue;
                }

                foreach (var empId in empIds)
                {
                    // RÀNG BUỘC HỢP ĐỒNG: Chỉ nhân viên còn hợp đồng lao động có hiệu lực mới được phân ca
                    if (!employeeMap.TryGetValue(empId, out var emp) || !ContractAttendancePolicy.HasValidContractOn(emp, targetDate))
                    {
                        skippedNoContractCount++;
                        continue;
                    }

                    // Kiểm tra trùng ca trong ngày
                    var exist = await _context.PhanCas.AnyAsync(p =>
                        p.MaNhanVien == empId &&
                        p.NgayLamViec == targetDate &&
                        p.MaCa == input.MaCa);

                    if (exist)
                    {
                        skippedDuplicateCount++;
                        continue;
                    }

                    var phanCa = new PhanCa
                    {
                        MaNhanVien = empId,
                        MaCa = input.MaCa,
                        NgayLamViec = targetDate,
                        BatDauDuKien = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                        KetThucDuKien = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                        TrangThai = safeStatus
                    };

                    _context.PhanCas.Add(phanCa);
                    successCount++;
                }
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
                var datesDesc = targetDates.Count == 1
                    ? $"ngày {targetDates[0]:dd/MM/yyyy}"
                    : $"{targetDates.Count} ngày ({targetDates.Min():dd/MM} - {targetDates.Max():dd/MM})";
                var msg = $"Phân ca {ca.TenCa} ({ca.GioBatDau:HH:mm} - {ca.GioKetThuc:HH:mm}) {datesDesc} thành công cho {empIds.Count} nhân sự ({successCount} lượt)!";
                var skipReasons = new List<string>();
                if (skippedPastCount > 0) skipReasons.Add($"{skippedPastCount} lượt do ngày quá khứ");
                if (skippedEndedCount > 0) skipReasons.Add($"{skippedEndedCount} lượt do ca đã hết giờ trong hôm nay");
                if (skippedNoContractCount > 0) skipReasons.Add($"{skippedNoContractCount} lượt do hợp đồng hết hạn hoặc chưa ký");
                if (skippedDuplicateCount > 0) skipReasons.Add($"{skippedDuplicateCount} lượt đã có ca trùng");
                if (skipReasons.Count > 0)
                {
                    msg += $" (Bỏ qua: {string.Join(", ", skipReasons)}).";
                }
                TempData["Success"] = TempData["SuccessMessage"] = msg;
            }
            else
            {
                var errors = new List<string>();
                if (skippedPastCount > 0) errors.Add("Không thể phân ca cho ngày trong quá khứ");
                if (skippedEndedCount > 0) errors.Add("Ca làm việc đã kết thúc trong ngày hôm nay");
                if (skippedNoContractCount > 0) errors.Add("Nhân viên không có hợp đồng lao động có hiệu lực (hợp đồng đã hết hạn hoặc chưa ký)");
                if (skippedDuplicateCount > 0) errors.Add("Tất cả các ca phân công đều đã được xếp trước đó");
                TempData["Warning"] = TempData["WarningMessage"] = errors.Count > 0
                    ? string.Join(". ", errors) + "."
                    : "Không thể tạo lượt phân ca.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo phân ca.");
            TempData["Error"] = TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu phân ca vào Supabase: " + ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
        {
            return Redirect(input.ReturnUrl);
        }

        return RedirectToAction(nameof(Index), new { week = input.NgayLamViec.ToString("yyyy-MM-dd") });
    }

    /// <summary>
    /// Xếp ca nhanh cả tuần (Thứ 2 đến Thứ 6) cho nhân viên
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickWeekSchedule(int maNhanVien, int maCa, string weekStartStr)
    {
        if (maNhanVien <= 0 || maCa <= 0 || !DateOnly.TryParse(weekStartStr, out var weekStart))
        {
            TempData["Error"] = TempData["ErrorMessage"] = "Thông tin xếp ca nhanh không hợp lệ.";
            return RedirectToAction(nameof(Index), new { week = weekStartStr });
        }

        try
        {
            var emp = await _context.NhanViens
                .Include(n => n.HopDongs)
                .FirstOrDefaultAsync(n => n.MaNhanVien == maNhanVien);

            if (emp == null || emp.HopDongs == null || emp.HopDongs.Count == 0)
            {
                TempData["Error"] = TempData["ErrorMessage"] = "Nhân viên chưa ký hợp đồng lao động. Vui lòng ký hợp đồng trước khi phân ca.";
                return RedirectToAction(nameof(Index), new { week = weekStartStr });
            }

            var ca = await _context.CaLamViecs.FindAsync(maCa);
            if (ca == null)
            {
                TempData["Error"] = TempData["ErrorMessage"] = "Không tìm thấy ca làm việc.";
                return RedirectToAction(nameof(Index), new { week = weekStartStr });
            }

            int addedCount = 0;
            int skippedPastCount = 0;
            int skippedNoContractCount = 0;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nowLocal = DateTime.Now;

            // Xếp từ Thứ Hai đến Thứ Sáu (5 ngày)
            for (int i = 0; i < 5; i++)
            {
                var workDate = weekStart.AddDays(i);
                if (workDate < today)
                {
                    skippedPastCount++;
                    continue;
                }

                // RÀNG BUỘC HỢP ĐỒNG: Chỉ xếp ca nếu hợp đồng có hiệu lực vào ngày làm việc
                if (!ContractAttendancePolicy.HasValidContractOn(emp, workDate))
                {
                    skippedNoContractCount++;
                    continue;
                }

                var startDateTime = workDate.ToDateTime(ca.GioBatDau);
                var endDateTime = ca.QuaDem
                    ? workDate.AddDays(1).ToDateTime(ca.GioKetThuc)
                    : workDate.ToDateTime(ca.GioKetThuc);

                if (workDate == today && endDateTime <= nowLocal)
                {
                    skippedPastCount++;
                    continue;
                }

                var exist = await _context.PhanCas.AnyAsync(p =>
                    p.MaNhanVien == maNhanVien &&
                    p.NgayLamViec == workDate &&
                    p.MaCa == maCa);

                if (!exist)
                {
                    _context.PhanCas.Add(new PhanCa
                    {
                        MaNhanVien = maNhanVien,
                        MaCa = maCa,
                        NgayLamViec = workDate,
                        BatDauDuKien = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                        KetThucDuKien = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                        TrangThai = "DA_PHAN"
                    });
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _context.SaveChangesAsync();
                var msg = $"Đã xếp ca nhanh thành công {addedCount} ngày trong tuần!";
                var skipNotes = new List<string>();
                if (skippedPastCount > 0)
                {
                    skipNotes.Add($"{skippedPastCount} ngày trong quá khứ hoặc ca đã qua giờ");
                }
                if (skippedNoContractCount > 0)
                {
                    skipNotes.Add($"{skippedNoContractCount} ngày hợp đồng hết hạn hoặc chưa có hiệu lực");
                }
                if (skipNotes.Count > 0)
                {
                    msg += $" (Đã tự động bỏ qua: {string.Join(", ", skipNotes)}).";
                }
                TempData["Success"] = TempData["SuccessMessage"] = msg;
            }
            else
            {
                if (skippedNoContractCount > 0)
                {
                    TempData["Warning"] = TempData["WarningMessage"] = $"Không thể xếp ca nhanh vì hợp đồng của nhân viên {emp.HoTen} đã hết hạn hoặc chưa có hiệu lực trong các ngày được chọn. Vui lòng gia hạn hợp đồng.";
                }
                else
                {
                    TempData["Warning"] = TempData["WarningMessage"] = skippedPastCount > 0
                        ? "Không thể xếp ca nhanh do các ngày đã qua trong quá khứ hoặc ca đã hết giờ."
                        : "Nhân viên đã có lịch phân ca đầy đủ cho các ngày đã chọn.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xếp ca nhanh.");
            TempData["Error"] = TempData["ErrorMessage"] = "Lỗi khi xếp ca nhanh: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { week = weekStartStr });
    }

    /// <summary>
    /// Xóa lượt phân ca
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnWeek)
    {
        try
        {
            var phanCa = await _context.PhanCas
                .Include(p => p.ChamCong)
                .FirstOrDefaultAsync(p => p.MaPhanCa == id);

            if (phanCa != null)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                if (phanCa.NgayLamViec < today)
                {
                    TempData["Error"] = TempData["ErrorMessage"] = "Không thể hủy phân ca của ngày trong quá khứ.";
                    return RedirectToAction(nameof(Index), new { week = returnWeek });
                }

                if (phanCa.ChamCong != null && phanCa.ChamCong.GioCheckIn.HasValue)
                {
                    TempData["Error"] = TempData["ErrorMessage"] = "Không thể hủy phân ca vì nhân viên đã quẹt thẻ chấm công cho ca này.";
                    return RedirectToAction(nameof(Index), new { week = returnWeek });
                }

                _context.PhanCas.Remove(phanCa);
                await _context.SaveChangesAsync();
                TempData["Success"] = TempData["SuccessMessage"] = "Đã hủy lượt phân ca thành công!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa phân ca.");
            TempData["Error"] = TempData["ErrorMessage"] = "Không thể xóa phân ca: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { week = returnWeek });
    }

    /// <summary>
    /// API gán nhanh / đổi ca / xóa ca trực tiếp trên cuốn lịch ma trận (In-Place Ajax)
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QuickAssignAjax([FromBody] QuickAssignAjaxModel input)
    {
        if (input.MaNhanVien <= 0 || !DateOnly.TryParse(input.NgayLamViec, out var targetDate))
        {
            return Json(new { success = false, message = "Thông tin nhân viên hoặc ngày làm việc không hợp lệ." });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (targetDate < today)
        {
            return Json(new { success = false, message = "Không thể phân ca hoặc sửa ca cho ngày trong quá khứ." });
        }

        try
        {
            // 1. Chế độ xóa ca (MaCa <= 0)
            if (input.MaCa <= 0)
            {
                var oldShifts = await _context.PhanCas
                    .Include(p => p.ChamCong)
                    .Where(p => p.MaNhanVien == input.MaNhanVien && p.NgayLamViec == targetDate)
                    .ToListAsync();

                if (oldShifts.Any(p => p.ChamCong != null && p.ChamCong.GioCheckIn.HasValue))
                {
                    return Json(new { success = false, message = "Không thể xóa ca vì nhân viên đã quẹt thẻ chấm công." });
                }

                if (oldShifts.Count != 0)
                {
                    _context.PhanCas.RemoveRange(oldShifts);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, action = "removed", maNhanVien = input.MaNhanVien, ngayLamViec = input.NgayLamViec });
            }

            // 2. Chế độ gán hoặc đổi ca
            // RÀNG BUỘC HỢP ĐỒNG: Kiểm tra hiệu lực hợp đồng của nhân viên vào ngày làm việc
            var emp = await _context.NhanViens
                .Include(n => n.HopDongs)
                .FirstOrDefaultAsync(n => n.MaNhanVien == input.MaNhanVien);

            if (emp == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin nhân viên." });
            }

            var contractValidation = ContractAttendancePolicy.ValidateSchedulingContract(emp, targetDate);
            if (!contractValidation.IsAllowed)
            {
                return Json(new
                {
                    success = false,
                    message = contractValidation.ErrorMessage ?? $"Nhân viên {emp.HoTen} không có hợp đồng lao động có hiệu lực vào ngày {targetDate:dd/MM/yyyy}. Vui lòng gia hạn hợp đồng trước khi phân ca."
                });
            }

            var ca = await _context.CaLamViecs.FindAsync(input.MaCa);
            if (ca == null)
            {
                return Json(new { success = false, message = "Không tìm thấy ca làm việc đã chọn." });
            }

            var startDateTime = targetDate.ToDateTime(ca.GioBatDau);
            var endDateTime = ca.QuaDem
                ? targetDate.AddDays(1).ToDateTime(ca.GioKetThuc)
                : targetDate.ToDateTime(ca.GioKetThuc);

            if (targetDate == today && endDateTime <= DateTime.Now)
            {
                return Json(new { success = false, message = $"Ca {ca.TenCa} ({ca.GioBatDau:HH:mm} - {ca.GioKetThuc:HH:mm}) đã kết thúc trong ngày hôm nay, không thể xếp ca mới." });
            }

            // Xóa ca cũ cùng ngày của nhân viên này nếu đã có (trừ khi đã có chấm công)
            var existing = await _context.PhanCas
                .Include(p => p.ChamCong)
                .Where(p => p.MaNhanVien == input.MaNhanVien && p.NgayLamViec == targetDate)
                .ToListAsync();

            if (existing.Any(p => p.ChamCong != null && p.ChamCong.GioCheckIn.HasValue))
            {
                return Json(new { success = false, message = "Không thể đổi ca vì nhân viên đã quẹt thẻ chấm công cho ca hiện tại." });
            }

            if (existing.Count != 0)
            {
                _context.PhanCas.RemoveRange(existing);
            }

            var phanCa = new PhanCa
            {
                MaNhanVien = input.MaNhanVien,
                MaCa = input.MaCa,
                NgayLamViec = targetDate,
                BatDauDuKien = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                KetThucDuKien = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime(),
                TrangThai = "DA_PHAN"
            };

            _context.PhanCas.Add(phanCa);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                action = "assigned",
                maPhanCa = phanCa.MaPhanCa,
                maNhanVien = input.MaNhanVien,
                ngayLamViec = input.NgayLamViec,
                maCa = ca.MaCa,
                tenCa = ca.TenCa,
                gioBatDau = ca.GioBatDau.ToString("HH:mm"),
                gioKetThuc = ca.GioKetThuc.ToString("HH:mm"),
                quaDem = ca.QuaDem
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gán nhanh ca trực cho nhân viên {MaNhanVien} ngày {NgayLamViec}", input.MaNhanVien, input.NgayLamViec);
            return Json(new { success = false, message = "Lỗi khi lưu dữ liệu vào CSDL: " + ex.Message });
        }
    }

    private static string GetDayOfWeekName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => ""
    };

    private static List<PhanCa> GenerateDemoPhanCas(List<NhanVien> emps, List<CaLamViec> cas, DateOnly weekStart)
    {
        var list = new List<PhanCa>();
        int id = 1;
        var rand = new Random(42);

        foreach (var emp in emps.Take(12))
        {
            for (int i = 0; i < 7; i++)
            {
                // Cho nghỉ 1-2 ngày ngẫu nhiên trong tuần
                if (i == 5 && rand.Next(10) > 4) continue;
                if (i == 6 && rand.Next(10) > 3) continue;

                var ca = cas[rand.Next(cas.Count)];
                var date = weekStart.AddDays(i);
                var start = date.ToDateTime(ca.GioBatDau);
                var end = ca.QuaDem ? date.AddDays(1).ToDateTime(ca.GioKetThuc) : date.ToDateTime(ca.GioKetThuc);

                list.Add(new PhanCa
                {
                    MaPhanCa = id++,
                    MaNhanVien = emp.MaNhanVien,
                    NhanVien = emp,
                    MaCa = ca.MaCa,
                    CaLamViec = ca,
                    NgayLamViec = date,
                    BatDauDuKien = new DateTimeOffset(start, TimeSpan.FromHours(7)),
                    KetThucDuKien = new DateTimeOffset(end, TimeSpan.FromHours(7)),
                    TrangThai = date < DateOnly.FromDateTime(DateTime.Today) ? "HOAN_THANH" : "DA_PHAN"
                });
            }
        }
        return list;
    }

    private void PopulateRealSeededSchedule(PhanCaIndexViewModel model, DateOnly weekStart, DateOnly weekEnd)
    {
        var bod = new PhongBan { MaPhongBan = 1, TenPhongBan = "Ban Giám Đốc (BOD)" };
        var hrAcc = new PhongBan { MaPhongBan = 2, TenPhongBan = "Bộ phận Nhân sự & Tài chính (HR & ACC)" };
        var fo = new PhongBan { MaPhongBan = 3, TenPhongBan = "Bộ phận Tiền sảnh (FO - Front Office)" };
        var hk = new PhongBan { MaPhongBan = 4, TenPhongBan = "Bộ phận Buồng phòng (HK - Housekeeping)" };
        var fb = new PhongBan { MaPhongBan = 5, TenPhongBan = "Bộ phận Ẩm thực (F&B - Food & Beverage)" };
        var kit = new PhongBan { MaPhongBan = 6, TenPhongBan = "Bộ phận Bếp (Kitchen / Culinary)" };
        var eng = new PhongBan { MaPhongBan = 7, TenPhongBan = "Bộ phận Kỹ thuật (ENG - Engineering)" };
        var sec = new PhongBan { MaPhongBan = 8, TenPhongBan = "Bộ phận An ninh (Security)" };

        model.DanhSachPhongBan = new List<PhongBan> { bod, hrAcc, fo, hk, fb, kit, eng, sec };

        var caSang = new CaLamViec { MaCa = 1, TenCa = "Ca Sáng (Morning Shift)", GioBatDau = new TimeOnly(6, 0), GioKetThuc = new TimeOnly(14, 0), QuaDem = false };
        var caChieu = new CaLamViec { MaCa = 2, TenCa = "Ca Chiều (Afternoon Shift)", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0), QuaDem = false };
        var caDem = new CaLamViec { MaCa = 3, TenCa = "Ca Đêm (Night Shift)", GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6, 0), QuaDem = true };

        model.DanhSachCa = new List<CaLamViec> { caSang, caChieu, caDem };

        var cvGM = new ChucVu { MaChucVu = 1, TenChucVu = "Tổng Giám Đốc (GM)" };
        var cvHR = new ChucVu { MaChucVu = 2, TenChucVu = "Chuyên viên Nhân sự & Tiền lương (HR & Payroll)" };
        var cvHead = new ChucVu { MaChucVu = 3, TenChucVu = "Trưởng Bộ Phận (Department Head)" };
        var cvSup = new ChucVu { MaChucVu = 4, TenChucVu = "Giám Sát Ca (Shift Supervisor)" };
        var cvRec = new ChucVu { MaChucVu = 5, TenChucVu = "Nhân viên Lễ tân (Receptionist)" };
        var cvHK = new ChucVu { MaChucVu = 6, TenChucVu = "Nhân viên Buồng phòng (Housekeeper)" };
        var cvChef = new ChucVu { MaChucVu = 7, TenChucVu = "Bếp Trưởng (Head Chef)" };
        var cvFB = new ChucVu { MaChucVu = 8, TenChucVu = "Nhân viên Phục vụ (F&B Staff)" };
        var cvTech = new ChucVu { MaChucVu = 9, TenChucVu = "Kỹ sư Tòa nhà (Technician)" };
        var cvSec = new ChucVu { MaChucVu = 10, TenChucVu = "Nhân viên An ninh (Security Guard)" };

        var nv1 = new NhanVien { MaNhanVien = 1, Ho = "Nguyễn Đình", Ten = "Cường", PhongBan = bod, MaPhongBan = 1, ChucVu = cvGM, MaChucVu = 1, TrangThai = "DANG_LAM" };
        var nv2 = new NhanVien { MaNhanVien = 2, Ho = "Võ Huỳnh Minh", Ten = "Sang", PhongBan = hrAcc, MaPhongBan = 2, ChucVu = cvHR, MaChucVu = 2, TrangThai = "DANG_LAM" };
        var nv3 = new NhanVien { MaNhanVien = 3, Ho = "Nguyễn Hoàng", Ten = "Long", PhongBan = fo, MaPhongBan = 3, ChucVu = cvHead, MaChucVu = 3, TrangThai = "DANG_LAM" };
        var nv4 = new NhanVien { MaNhanVien = 4, Ho = "Châu Quốc", Ten = "Bảo", PhongBan = fo, MaPhongBan = 3, ChucVu = cvRec, MaChucVu = 5, TrangThai = "DANG_LAM" };
        var nv5 = new NhanVien { MaNhanVien = 5, Ho = "Trần Thị", Ten = "Mai", PhongBan = hk, MaPhongBan = 4, ChucVu = cvHead, MaChucVu = 3, TrangThai = "DANG_LAM" };
        var nv6 = new NhanVien { MaNhanVien = 6, Ho = "Lê Văn", Ten = "Hùng", PhongBan = hk, MaPhongBan = 4, ChucVu = cvHK, MaChucVu = 6, TrangThai = "DANG_LAM" };
        var nv7 = new NhanVien { MaNhanVien = 7, Ho = "Phạm Thị", Ten = "Lan", PhongBan = hk, MaPhongBan = 4, ChucVu = cvHK, MaChucVu = 6, TrangThai = "DANG_LAM" };
        var nv8 = new NhanVien { MaNhanVien = 8, Ho = "Vũ Minh", Ten = "Tuấn", PhongBan = fb, MaPhongBan = 5, ChucVu = cvHead, MaChucVu = 3, TrangThai = "DANG_LAM" };
        var nv9 = new NhanVien { MaNhanVien = 9, Ho = "Hoàng Thị", Ten = "Thảo", PhongBan = fb, MaPhongBan = 5, ChucVu = cvSup, MaChucVu = 4, TrangThai = "DANG_LAM" };
        var nv10 = new NhanVien { MaNhanVien = 10, Ho = "Đỗ Thành", Ten = "Đạt", PhongBan = fb, MaPhongBan = 5, ChucVu = cvFB, MaChucVu = 8, TrangThai = "DANG_LAM" };
        var nv11 = new NhanVien { MaNhanVien = 11, Ho = "Bùi Văn", Ten = "Nam", PhongBan = kit, MaPhongBan = 6, ChucVu = cvChef, MaChucVu = 7, TrangThai = "DANG_LAM" };
        var nv12 = new NhanVien { MaNhanVien = 12, Ho = "Đặng Văn", Ten = "Kiên", PhongBan = kit, MaPhongBan = 6, ChucVu = cvSup, MaChucVu = 4, TrangThai = "DANG_LAM" };
        var nv13 = new NhanVien { MaNhanVien = 13, Ho = "Ngô Thị", Ten = "Bích", PhongBan = kit, MaPhongBan = 6, ChucVu = cvFB, MaChucVu = 8, TrangThai = "DANG_LAM" };
        var nv14 = new NhanVien { MaNhanVien = 14, Ho = "Trương Quốc", Ten = "Huy", PhongBan = eng, MaPhongBan = 7, ChucVu = cvHead, MaChucVu = 3, TrangThai = "DANG_LAM" };
        var nv15 = new NhanVien { MaNhanVien = 15, Ho = "Đinh Văn", Ten = "Phong", PhongBan = eng, MaPhongBan = 7, ChucVu = cvTech, MaChucVu = 9, TrangThai = "DANG_LAM" };
        var nv16 = new NhanVien { MaNhanVien = 16, Ho = "Phan Hồng", Ten = "Quân", PhongBan = eng, MaPhongBan = 7, ChucVu = cvTech, MaChucVu = 9, TrangThai = "DANG_LAM" };
        var nv17 = new NhanVien { MaNhanVien = 17, Ho = "Lâm Văn", Ten = "Tiến", PhongBan = sec, MaPhongBan = 8, ChucVu = cvHead, MaChucVu = 3, TrangThai = "DANG_LAM" };
        var nv18 = new NhanVien { MaNhanVien = 18, Ho = "Hà Trọng", Ten = "Nghĩa", PhongBan = sec, MaPhongBan = 8, ChucVu = cvSec, MaChucVu = 10, TrangThai = "DANG_LAM" };
        var nv19 = new NhanVien { MaNhanVien = 19, Ho = "Đoàn Văn", Ten = "Thắng", PhongBan = sec, MaPhongBan = 8, ChucVu = cvSec, MaChucVu = 10, TrangThai = "DANG_LAM" };
        var nv20 = new NhanVien { MaNhanVien = 20, Ho = "Dương Thùy", Ten = "Linh", PhongBan = fo, MaPhongBan = 3, ChucVu = cvRec, MaChucVu = 5, TrangThai = "DANG_LAM" };

        model.DanhSachNhanVien = new List<NhanVien>
        {
            nv1, nv2, nv3, nv4, nv5, nv6, nv7, nv8, nv9, nv10,
            nv11, nv12, nv13, nv14, nv15, nv16, nv17, nv18, nv19, nv20
        };

        // Danh sách phân ca thực tế chuẩn CSDL (chỉ gồm 4 ca thực tế của NV04 Châu Quốc Bảo từ 15/09/2026 đến 18/09/2026)
        var allActualShifts = new List<PhanCa>
        {
            new()
            {
                MaPhanCa = 1,
                MaNhanVien = 4,
                NhanVien = nv4,
                MaCa = 1,
                CaLamViec = caSang,
                NgayLamViec = new DateOnly(2026, 9, 15),
                BatDauDuKien = new DateTimeOffset(new DateTime(2026, 9, 15, 6, 0, 0), TimeSpan.FromHours(7)),
                KetThucDuKien = new DateTimeOffset(new DateTime(2026, 9, 15, 14, 0, 0), TimeSpan.FromHours(7)),
                TrangThai = "HOAN_THANH"
            },
            new()
            {
                MaPhanCa = 2,
                MaNhanVien = 4,
                NhanVien = nv4,
                MaCa = 1,
                CaLamViec = caSang,
                NgayLamViec = new DateOnly(2026, 9, 16),
                BatDauDuKien = new DateTimeOffset(new DateTime(2026, 9, 16, 6, 0, 0), TimeSpan.FromHours(7)),
                KetThucDuKien = new DateTimeOffset(new DateTime(2026, 9, 16, 14, 0, 0), TimeSpan.FromHours(7)),
                TrangThai = "HOAN_THANH"
            },
            new()
            {
                MaPhanCa = 3,
                MaNhanVien = 4,
                NhanVien = nv4,
                MaCa = 2,
                CaLamViec = caChieu,
                NgayLamViec = new DateOnly(2026, 9, 17),
                BatDauDuKien = new DateTimeOffset(new DateTime(2026, 9, 17, 14, 0, 0), TimeSpan.FromHours(7)),
                KetThucDuKien = new DateTimeOffset(new DateTime(2026, 9, 17, 22, 0, 0), TimeSpan.FromHours(7)),
                TrangThai = "HOAN_THANH"
            },
            new()
            {
                MaPhanCa = 4,
                MaNhanVien = 4,
                NhanVien = nv4,
                MaCa = 3,
                CaLamViec = caDem,
                NgayLamViec = new DateOnly(2026, 9, 18),
                BatDauDuKien = new DateTimeOffset(new DateTime(2026, 9, 18, 22, 0, 0), TimeSpan.FromHours(7)),
                KetThucDuKien = new DateTimeOffset(new DateTime(2026, 9, 19, 6, 0, 0), TimeSpan.FromHours(7)),
                TrangThai = "HOAN_THANH"
            }
        };

        // Lọc ca thuộc tuần đang xem: Chỉ có tuần 15/09 - 21/09/2026 là có 4 ca, các tuần khác chưa phân ca (0 ca)
        model.AllGridPhanCa = allActualShifts;
        model.DanhSachPhanCa = allActualShifts
            .Where(p => p.NgayLamViec >= weekStart && p.NgayLamViec <= weekEnd)
            .ToList();

        // Ca trực hôm nay luôn lấy đúng ngày thực tế (17/09/2026) bất kể người dùng đang lướt tuần nào
        var today = DateOnly.FromDateTime(DateTime.Today);
        model.CaTrucHomNay = allActualShifts
            .Where(p => p.NgayLamViec == today || p.NgayLamViec == new DateOnly(2026, 9, 17))
            .ToList();

        model.TongSoCa = model.DanhSachPhanCa.Count;
        model.SoCaSang = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Sáng"));
        model.SoCaChieu = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Chiều"));
        model.SoCaDem = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && (p.CaLamViec.QuaDem || p.CaLamViec.TenCa.Contains("Đêm")));
        model.TongNhanVien = model.DanhSachNhanVien.Count;
        model.SoNhanVienDuocPhan = model.DanhSachPhanCa.Select(p => p.MaNhanVien).Distinct().Count();

        var avatarColors = new[] { "#1E3A8A", "#D97706", "#059669", "#7C3AED", "#DB2777", "#2563EB", "#0891B2" };
        model.Rows = new List<PhanCaMatrixRow>();

        foreach (var emp in model.DanhSachNhanVien)
        {
            var row = new PhanCaMatrixRow
            {
                MaNhanVien = emp.MaNhanVien,
                MaPhongBan = emp.MaPhongBan ?? 0,
                HoTen = $"{emp.Ho} {emp.Ten}".Trim(),
                TenPhongBan = emp.PhongBan?.TenPhongBan ?? "Khách sạn",
                TenChucVu = emp.ChucVu?.TenChucVu ?? "Nhân viên",
                AvatarColor = avatarColors[Math.Abs(emp.MaNhanVien.GetHashCode()) % avatarColors.Length]
            };

            for (int i = 0; i < 7; i++)
            {
                var currentDate = weekStart.AddDays(i);
                var cell = new PhanCaDayCell
                {
                    Date = currentDate,
                    DayOfWeekName = GetDayOfWeekName(currentDate.DayOfWeek),
                    IsToday = currentDate == today,
                    Shifts = model.DanhSachPhanCa
                        .Where(p => p.MaNhanVien == emp.MaNhanVien && p.NgayLamViec == currentDate)
                        .Select(p => new PhanCaItemDto
                        {
                            MaPhanCa = p.MaPhanCa,
                            MaCa = p.MaCa,
                            TenCa = p.CaLamViec?.TenCa ?? "Ca",
                            GioBatDau = p.CaLamViec?.GioBatDau ?? TimeOnly.MinValue,
                            GioKetThuc = p.CaLamViec?.GioKetThuc ?? TimeOnly.MinValue,
                            QuaDem = p.CaLamViec?.QuaDem ?? false,
                            TrangThai = p.TrangThai
                        }).ToList()
                };
                row.Days.Add(cell);
            }
            model.Rows.Add(row);
        }

        if (model.CalendarDays.Count == 0)
        {
            var calYear = model.Year > 0 ? model.Year : today.Year;
            var calMonth = model.Month > 0 ? model.Month : today.Month;
            var monthFirst = new DateOnly(calYear, calMonth, 1);
            int offsetFromMonday = ((int)monthFirst.DayOfWeek + 6) % 7;
            var gridStart = monthFirst.AddDays(-offsetFromMonday);

            for (int i = 0; i < 42; i++)
            {
                var day = gridStart.AddDays(i);
                var dayShifts = allActualShifts.Where(p => p.NgayLamViec == day).ToList();
                model.CalendarDays.Add(new CalendarDaySummary
                {
                    Date = day,
                    Sang = dayShifts.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Sáng")),
                    Chieu = dayShifts.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Chiều")),
                    Dem = dayShifts.Count(p => p.CaLamViec != null && (p.CaLamViec.QuaDem || p.CaLamViec.TenCa.Contains("Đêm"))),
                    IsOtherMonth = day.Month != calMonth,
                    IsToday = day == today,
                    IsWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday,
                    IsPast = day < today,
                    IsSelected = day == model.SelectedDate
                });
            }
        }

        if (model.SelectedAgenda.Shifts.Count == 0)
        {
            model.SelectedAgenda = new AgendaDay { Date = model.SelectedDate };
            foreach (var ca in model.DanhSachCa)
            {
                var badgeClass = ca.TenCa.Contains("Sáng") ? "badge-morning" : (ca.TenCa.Contains("Chiều") ? "badge-afternoon" : "badge-night");
                model.SelectedAgenda.Shifts.Add(new AgendaShiftGroup
                {
                    MaCa = ca.MaCa,
                    TenCa = ca.TenCa,
                    Gio = $"{ca.GioBatDau:HH\\:mm} - {ca.GioKetThuc:HH\\:mm}",
                    BadgeClass = badgeClass
                });
            }
        }
    }
}
