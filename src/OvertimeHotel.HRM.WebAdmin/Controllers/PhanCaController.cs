using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Policy = PermissionCodes.AssignShifts)]
public class PhanCaController : Controller
{
    /// <summary>
    /// Thông báo dùng chung khi bảng định biên theo ngày chưa được tạo trên CSDL.
    /// </summary>
    private const string MissingDinhBienNgayMessage =
        "Chưa lưu được định biên riêng theo ngày vì bảng dinh_bien_ngay chưa có trên CSDL. " +
        "Hãy chạy migration 20261021_dinh_bien_ngay_override.sql rồi thử lại.";

    private const string MissingDinhBienBoPhanMessage =
        "Chưa lưu được định biên theo bộ phận vì CSDL chưa có cột ma_phong_ban. " +
        "Hãy chạy migration 20261026_dinh_bien_theo_bo_phan.sql rồi thử lại.";

    private sealed record SharedPositionDept(int MaChucVu, int MaPhongBan, string? TenPhongBan);

    private readonly AppDbContext _context;
    private readonly ILogger<PhanCaController> _logger;
    private readonly IHotelEventService _events;

    public PhanCaController(AppDbContext context, ILogger<PhanCaController> logger, IHotelEventService events)
    {
        _context = context;
        _logger = logger;
        _events = events;
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
        bool openShiftModal = false,
        bool openDinhBienModal = false)
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
        ViewBag.OpenDinhBienModal = openDinhBienModal;

        // PHẠM VI BỘ PHẬN (PRD 3.2): Quản lý chỉ thấy lịch của bộ phận mình, Admin/HR/IT
        // thấy toàn khách sạn và tự chọn bộ phận để lọc. Quyết định ở server rồi mới
        // truyền xuống view — lọc bằng tham số `phongBanId` không đủ, vì tham số đó
        // sửa được trên thanh địa chỉ và bỏ trống là thấy hết.
        var phamViPhongBanId = User.ResolveEffectiveDepartmentId(phongBanId);
        var biGioiHanTheoBoPhan = User.GetManagedDepartmentScope().HasValue;
        var isManager = biGioiHanTheoBoPhan;
        var isWindowClosedForUser = isManager && !ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen();

        var model = new PhanCaIndexViewModel
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Year = calYear,
            Month = calMonth,
            SelectedDate = selDate,
            SelectedPhongBanId = phongBanId,
            SelectedCaId = caId,
            PhamViPhongBanId = phamViPhongBanId,
            BiGioiHanTheoBoPhan = biGioiHanTheoBoPhan,
            IsManagerSchedulingWindowClosed = isWindowClosedForUser
        };

        try
        {
            // 2. Tải danh mục phòng ban và ca làm việc (tái sử dụng từ bộ nhớ đệm SearchListCache)
            var allPhongBans = await this.SearchDataAsync("phanca:departments", () =>
                _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync());

            model.DanhSachPhongBan = phamViPhongBanId.HasValue
                ? allPhongBans.Where(p => p.MaPhongBan == phamViPhongBanId.Value).ToList()
                : allPhongBans;
            model.TenPhamViPhongBan = model.DanhSachPhongBan.FirstOrDefault()?.TenPhongBan;

            model.DanhSachCa = await this.SearchDataAsync("phanca:shifts", () =>
                _context.CaLamViecs.AsNoTracking()
                    .Where(c => !c.TenCa.StartsWith("EVT-") && !c.TenCa.StartsWith("TEST-"))
                    .OrderBy(c => c.GioBatDau)
                    .ToListAsync());

            // 3. Tải danh sách nhân viên CÒN HỢP ĐỒNG có hiệu lực bao phủ phạm vi lưới tháng (tái sử dụng bộ đệm)
            var empCacheKey = $"phanca:employees:{gridStart:yyyyMMdd}:{gridEnd:yyyyMMdd}:scope:{phamViPhongBanId}";
            model.DanhSachNhanVien = await this.SearchDataAsync(empCacheKey, () =>
            {
                var empQuery = _context.NhanViens.AsNoTracking()
                    .AsSplitQuery()
                    .Include(n => n.PhongBan)
                    .Include(n => n.ChucVu)
                    .Include(n => n.HopDongs)
                    .Where(n => n.TrangThai == "DANG_LAM");

                if (phamViPhongBanId.HasValue)
                {
                    empQuery = empQuery.Where(n => n.MaPhongBan == phamViPhongBanId.Value);
                }

                // RÀNG BUỘC HỢP ĐỒNG: Chỉ hiển thị nhân viên còn hợp đồng hiệu lực trong phạm vi lưới
                empQuery = empQuery.Where(n => n.HopDongs.Any(h =>
                    h.TrangThai == ContractAttendancePolicy.STATUS_HIEU_LUC &&
                    h.NgayBatDau <= gridEnd &&
                    (!h.NgayKetThuc.HasValue || h.NgayKetThuc.Value >= gridStart)));

                return empQuery.OrderBy(n => n.Ten).ThenBy(n => n.Ho).ToListAsync();
            });

            // 4. Tải danh sách phân ca trong phạm vi lưới 42 ngày (tái sử dụng bộ đệm, AsSplitQuery triệt tiêu Cartesian)
            var gridCacheKey = $"phanca:grid:{gridStart:yyyyMMdd}:{gridEnd:yyyyMMdd}:scope:{phamViPhongBanId}:ca:{caId}";
            var allGridPhanCas = await this.SearchDataAsync(gridCacheKey, () =>
            {
                var phanCaQuery = _context.PhanCas.AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.NhanVien)
                        .ThenInclude(n => n!.PhongBan)
                    .Include(p => p.NhanVien)
                        .ThenInclude(n => n!.ChucVu)
                    .Include(p => p.NhanVien)
                        .ThenInclude(n => n!.HopDongs)
                    .Include(p => p.CaLamViec)
                    .Include(p => p.ChamCong)
                    .Where(p => p.TrangThai != "DA_HUY" && p.NgayLamViec >= gridStart && p.NgayLamViec <= gridEnd);

                if (phamViPhongBanId.HasValue)
                {
                    phanCaQuery = phanCaQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phamViPhongBanId.Value);
                }

                if (caId.HasValue && caId.Value > 0)
                {
                    phanCaQuery = phanCaQuery.Where(p => p.MaCa == caId.Value);
                }

                return phanCaQuery
                    .OrderBy(p => p.NgayLamViec)
                    .ThenBy(p => p.BatDauDuKien)
                    .ToListAsync();
            });

            model.AllGridPhanCa = allGridPhanCas;

            // 4b. Tải danh mục các ca đã hoán đổi thành công qua quy trình Đổi ca (HR / Quản lý duyệt)
            // để gắn nhãn trực quan trên ma trận tuần và panel nhật ký
            var swappedShiftsDict = await this.SearchDataAsync(
                $"phanca:swaps:{gridStart:yyyyMMdd}:{gridEnd:yyyyMMdd}",
                async () =>
                {
                    var approvedSwaps = await _context.YeuCauDoiCas
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Include(y => y.NhanVienGui)
                        .Include(y => y.NhanVienNhan)
                        .Include(y => y.PhanCaGui)
                        .Include(y => y.PhanCaNhan)
                        .Where(y => y.TrangThai == "DA_DUYET" &&
                            ((y.PhanCaGui != null && y.PhanCaGui.NgayLamViec >= gridStart && y.PhanCaGui.NgayLamViec <= gridEnd) ||
                             (y.PhanCaNhan != null && y.PhanCaNhan.NgayLamViec >= gridStart && y.PhanCaNhan.NgayLamViec <= gridEnd)))
                        .ToListAsync();

                    var map = new Dictionary<int, string>();
                    foreach (var s in approvedSwaps)
                    {
                        var tenGui = s.NhanVienGui != null ? $"{s.NhanVienGui.Ho} {s.NhanVienGui.Ten}".Trim() : $"NV #{s.MaNhanVienGui}";
                        var tenNhan = s.NhanVienNhan != null ? $"{s.NhanVienNhan.Ho} {s.NhanVienNhan.Ten}".Trim() : $"NV #{s.MaNhanVienNhan}";
                        if (s.MaPhanCaGui > 0)
                        {
                            map[s.MaPhanCaGui] = $"Đã đổi ca: {tenNhan} nhận trực thay cho {tenGui}";
                        }
                        if (s.MaPhanCaNhan > 0)
                        {
                            map[s.MaPhanCaNhan] = $"Đã đổi ca: {tenGui} nhận trực thay cho {tenNhan}";
                        }
                    }
                    return map;
                });

            // model.DanhSachPhanCa vẫn giữ cho tuần hiện tại để phục vụ ma trận tuần
            model.DanhSachPhanCa = allGridPhanCas
                .Where(p => p.NgayLamViec >= weekStart && p.NgayLamViec <= weekEnd)
                .ToList();

            // 5. Thêm nhân viên đã nghỉ nhưng còn ca trong tuần vào ma trận tuần
            // Tối ưu: Tái sử dụng p.NhanVien đã nạp sẵn trong model.DanhSachPhanCa, triệt tiêu query lại CSDL
            var knownEmpIds = model.DanhSachNhanVien.Select(n => n.MaNhanVien).ToHashSet();
            var extraShiftEmps = model.DanhSachPhanCa
                .Where(p => p.NhanVien != null && !knownEmpIds.Contains(p.MaNhanVien))
                .Select(p => p.NhanVien!)
                .DistinctBy(n => n.MaNhanVien)
                .ToList();

            var matrixEmps = model.DanhSachNhanVien
                .Concat(extraShiftEmps)
                .OrderBy(n => n.Ten).ThenBy(n => n.Ho)
                .ToList();

            // Tải danh sách ca trực hôm nay:
            // Tối ưu: Nếu hôm nay nằm trong lưới 42 ngày và không lọc ca cụ thể,
            // tái sử dụng trực tiếp allGridPhanCas từ bộ nhớ để triệt tiêu query thừa.
            if ((!caId.HasValue || caId.Value <= 0) && today >= gridStart && today <= gridEnd)
            {
                model.CaTrucHomNay = allGridPhanCas.Where(p => p.NgayLamViec == today).ToList();
            }
            else
            {
                model.CaTrucHomNay = await this.SearchDataAsync($"phanca:today:{today:yyyyMMdd}:scope:{phamViPhongBanId}", () =>
                {
                    var homNayQuery = _context.PhanCas.AsNoTracking()
                        .AsSplitQuery()
                        .Include(p => p.NhanVien).ThenInclude(n => n!.PhongBan)
                        .Include(p => p.NhanVien).ThenInclude(n => n!.ChucVu)
                        .Include(p => p.CaLamViec)
                        .Include(p => p.ChamCong)
                        .Where(p => p.TrangThai != "DA_HUY" && p.NgayLamViec == today);

                    if (phamViPhongBanId.HasValue)
                    {
                        homNayQuery = homNayQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phamViPhongBanId.Value);
                    }

                    return homNayQuery.ToListAsync();
                });
            }

            // 6. Tính toán thống kê Bento
            model.TongSoCa = model.DanhSachPhanCa.Count;
            model.TongNhanVien = model.DanhSachNhanVien.Count;
            model.SoNhanVienDuocPhan = model.DanhSachPhanCa.Select(p => p.MaNhanVien).Distinct().Count();
            model.TongGioOtTuan = model.DanhSachPhanCa.Where(p => p.TrangThai != "DA_HUY").Sum(p => p.GioTangCaDuKien);

            // 7. Xây dựng 42 ngày cho Cuốn Lịch (Calendar Notebook)
            for (int i = 0; i < 42; i++)
            {
                var day = gridStart.AddDays(i);
                var dayShifts = allGridPhanCas.Where(p => p.NgayLamViec == day).ToList();
                var expiredCount = dayShifts.Count(p => p.NhanVien != null && !ContractAttendancePolicy.HasValidContractOn(p.NhanVien, day));
                var hasContractIssue = expiredCount > 0;

                model.CalendarDays.Add(new CalendarDaySummary
                {
                    Date = day,
                    // Gom theo MaCa chứ không dò chuỗi tên ca: ca thứ tư trở đi
                    // (Ca Hành Chính) phải có ô đếm riêng thay vì rơi mất.
                    SoLuotTheoCa = dayShifts
                        .GroupBy(p => p.MaCa)
                        .ToDictionary(g => g.Key, g => g.Count()),
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
                var shiftGroup = new AgendaShiftGroup
                {
                    MaCa = ca.MaCa,
                    TenCa = ca.TenCa,
                    Gio = $"{ca.GioBatDau:HH\\:mm} - {ca.GioKetThuc:HH\\:mm}",
                    // Sắc thái lấy từ danh mục ca, không dò tên: ca hành chính
                    // không có "Sáng"/"Chiều"/"Đêm" trong tên nên cách dò cũ
                    // luôn gán nhầm cho nó màu ca đêm.
                    SacThai = ca.SacThaiCa,
                    LoaiCa = ca.LoaiCa,
                    QuaDem = ca.QuaDem
                };

                var assignedInCa = selectedDayShifts.Where(p => p.MaCa == ca.MaCa).ToList();
                foreach (var p in assignedInCa)
                {
                    var emp = p.NhanVien;
                    var hasValidContract = emp != null && ContractAttendancePolicy.HasValidContractOn(emp, model.SelectedDate);
                    var isSwapped = swappedShiftsDict.TryGetValue(p.MaPhanCa, out var swapNote);
                    shiftGroup.Employees.Add(new AgendaEmployeeItem
                    {
                        MaPhanCa = p.MaPhanCa,
                        MaNhanVien = p.MaNhanVien,
                        HoTen = emp != null ? $"{emp.Ho} {emp.Ten}".Trim() : $"NV #{p.MaNhanVien}",
                        TenChucVu = emp?.ChucVu?.TenChucVu ?? "Nhân viên",
                        TenPhongBan = emp?.PhongBan?.TenPhongBan ?? "Khách sạn",
                        AvatarColor = avatarColors[Math.Abs(p.MaNhanVien.GetHashCode()) % avatarColors.Length],
                        HasActiveContract = hasValidContract,
                        DisabledReason = !hasValidContract ? $"Nhân viên {emp?.HoTen} có hợp đồng hết hạn / chưa có hiệu lực vào ngày {model.SelectedDate:dd/MM/yyyy}." : null,
                        GioTangCaDuKien = p.GioTangCaDuKien,
                        GhiChuOt = p.GhiChuOt,
                        DaDoiCa = isSwapped,
                        ThongTinDoiCa = swapNote
                    });
                }

                model.SelectedAgenda.Shifts.Add(shiftGroup);
            }

            // 8b. Đối chiếu định biên tối thiểu theo chức vụ (PRD 3.5).
            // Mức nền nằm ở dinh_bien_ca; ngày đang xem có thể có dòng riêng ở
            // dinh_bien_ngay đè lên. Ô không có dòng riêng thì vẫn theo mức nền.
            //
            // Hai bảng này được mở rộng dần qua nhiều migration (20261020 tạo bảng,
            // 20261021 thêm mức theo ngày, 20261026 thêm chiều bộ phận). CSDL cloud
            // có thể chậm hơn code một nhịp, nên cả hai lần đọc đều chịu được trường
            // hợp cột/bảng chưa có: khi đó bỏ qua phần thiếu và vẫn hiển thị được
            // lịch, thay vì ném lỗi làm sập cả màn phân ca.
            var dinhBienTheoCa = await DocDinhBienCaAsync();
            var dinhBienNgayTheoCa = await DocDinhBienNgayAsync(model.SelectedDate);

            // Đếm người đã xếp theo bộ ba (ca, chức vụ, bộ phận) — khớp đúng khoá của
            // bảng định biên. Đếm theo chức vụ không thôi thì dòng "Giám Sát Ca" của
            // bộ phận Bếp sẽ cộng cả người của bộ phận Ẩm thực vào.
            var soNguoiTheoODinhBien = selectedDayShifts
                .Where(p => p.TrangThai != "VANG_MAT" && p.NhanVien?.MaChucVu != null)
                .GroupBy(p => (p.MaCa, MaChucVu: p.NhanVien!.MaChucVu!.Value, p.NhanVien.MaPhongBan))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var shiftGroup in model.SelectedAgenda.Shifts)
            {
                var mucNenCuaCa = dinhBienTheoCa.Where(d => d.MaCa == shiftGroup.MaCa).ToList();
                var mucNgayCuaCa = dinhBienNgayTheoCa.Where(d => d.MaCa == shiftGroup.MaCa).ToList();

                // Gộp hai nguồn theo bộ ba (chức vụ, bộ phận): số của ngày đè lên số
                // nền. Khoá gộp phải ba chiều — gộp theo chức vụ không thôi thì dòng
                // đè của bộ phận Bếp sẽ nuốt luôn mức nền của bộ phận Ẩm thực.
                var oCoDinhBienNgay = mucNgayCuaCa
                    .Select(d => (d.MaChucVu, d.MaPhongBan))
                    .ToHashSet();
                var mucHieuLuc = mucNgayCuaCa
                    .Select(d => (d.MaChucVu, d.MaPhongBan, d.SoNguoiToiThieu, d.ChucVu, d.PhongBan))
                    .Concat(mucNenCuaCa
                        .Where(nen => !oCoDinhBienNgay.Contains((nen.MaChucVu, nen.MaPhongBan)))
                        .Select(nen => (nen.MaChucVu, nen.MaPhongBan, nen.SoNguoiToiThieu, nen.ChucVu, nen.PhongBan)))
                    .Where(d => d.SoNguoiToiThieu > 0)
                    .ToList();

                // PHẠM VI BỘ PHẬN: Quản lý chỉ nhận cảnh báo định biên của các chức vụ
                // thuộc bộ phận mình. Nếu không lọc, Quản lý Bếp sẽ thấy cảnh báo thiếu
                // nhân sự Lễ tân / Buồng phòng (vì lịch đã lọc không có người các bộ phận đó).
                if (phamViPhongBanId.HasValue)
                {
                    var inScopeChucVuIds = model.DanhSachNhanVien
                        .Where(n => n.MaChucVu.HasValue)
                        .Select(n => n.MaChucVu!.Value)
                        .ToHashSet();

                    mucHieuLuc = mucHieuLuc.Where(d =>
                        (d.MaPhongBan.HasValue && d.MaPhongBan.Value == phamViPhongBanId.Value) ||
                        (!d.MaPhongBan.HasValue && d.ChucVu?.MaPhongBan == phamViPhongBanId.Value) ||
                        (!d.MaPhongBan.HasValue && d.ChucVu?.MaPhongBan == null && inScopeChucVuIds.Contains(d.MaChucVu))
                    ).ToList();
                }

                if (mucHieuLuc.Count == 0)
                {
                    continue;
                }

                foreach (var dinhBien in mucHieuLuc
                    .OrderBy(d => d.ChucVu?.TenChucVu)
                    .ThenBy(d => d.PhongBan?.TenPhongBan))
                {
                    // Đếm số người hiện có:
                    // - Định biên cụ thể bộ phận (dinhBien.MaPhongBan.HasValue): đếm nhân viên thuộc bộ phận đó.
                    // - Định biên dùng chung mọi bộ phận (!dinhBien.MaPhongBan.HasValue): đếm tất cả nhân sự có chức vụ này trong ca.
                    var soNguoiHienCo = selectedDayShifts.Count(p =>
                        p.MaCa == shiftGroup.MaCa
                        && p.TrangThai != "VANG_MAT"
                        && p.NhanVien?.MaChucVu == dinhBien.MaChucVu
                        && (!dinhBien.MaPhongBan.HasValue || (p.NhanVien.MaPhongBan ?? p.NhanVien.ChucVu?.MaPhongBan) == dinhBien.MaPhongBan.Value));

                    shiftGroup.DinhBien.Add(new DinhBienCheckItem
                    {
                        MaChucVu = dinhBien.MaChucVu,
                        TenChucVu = dinhBien.ChucVu?.TenChucVu ?? $"Chức vụ #{dinhBien.MaChucVu}",
                        MaPhongBan = dinhBien.MaPhongBan,
                        TenPhongBan = dinhBien.PhongBan?.TenPhongBan ?? string.Empty,
                        SoNguoiToiThieu = dinhBien.SoNguoiToiThieu,
                        SoNguoiHienCo = soNguoiHienCo,
                        LaMucRiengNgay = oCoDinhBienNgay.Contains((dinhBien.MaChucVu, dinhBien.MaPhongBan))
                    });
                }
            }

            model.TongCaThieuDinhBien = model.SelectedAgenda.Shifts.Count(s => s.ThieuDinhBien);

            // 8c. Dữ liệu cho popup cấu hình định biên (PRD 3.5).
            // Dựng theo danh mục chức vụ × bộ phận: chức danh gắn cứng một bộ phận
            // sinh đúng một dòng; chức danh có ma_phong_ban = NULL (Trưởng Bộ Phận,
            // Giám Sát Ca) trải trên nhiều bộ phận nên sinh một dòng cho mỗi bộ phận
            // thực sự có người giữ chức danh đó, cộng một dòng "dùng chung" nếu đã
            // cấu hình mức chung hoặc nếu chưa xác định được bộ phận nào.
            // Chức danh gắn cứng vào bộ phận khác thì Quản lý không cần thấy: họ không
            // đặt được định biên cho bộ phận đó (SaveDinhBien chặn), nên hiển thị chỉ
            // tạo cảm giác sửa được. Chức danh dùng chung (ma_phong_ban NULL) vẫn giữ
            // để còn dòng theo bộ phận của chính họ bên dưới.
            var allChucVus = await this.SearchDataAsync("phanca:positions", () =>
                _context.ChucVus.AsNoTracking().Include(c => c.PhongBan).OrderBy(c => c.TenChucVu).ToListAsync());

            var chucVus = phamViPhongBanId.HasValue
                ? allChucVus.Where(c => c.MaPhongBan == null || c.MaPhongBan == phamViPhongBanId.Value).ToList()
                : allChucVus;

            // Bộ phận thực tế của từng chức danh dùng chung, suy từ nhân sự đang giữ
            // chức danh đó. Tái sử dụng qua bộ đệm SearchListCache.
            var allSharedPositionDepts = await this.SearchDataAsync("phanca:shared-position-depts", () =>
                _context.NhanViens.AsNoTracking()
                    .Where(n => n.MaChucVu.HasValue && n.MaPhongBan.HasValue)
                    .Select(n => new SharedPositionDept(n.MaChucVu!.Value, n.MaPhongBan!.Value, n.PhongBan!.TenPhongBan))
                    .Distinct()
                    .ToListAsync());

            var phongBanTheoChucVuDungChung = phamViPhongBanId.HasValue
                ? allSharedPositionDepts.Where(n => n.MaPhongBan == phamViPhongBanId.Value).ToList()
                : allSharedPositionDepts;

            // Dùng lại danh mục đã nạp ở bước 2 (đã co theo phạm vi) thay vì truy vấn
            // lại toàn bộ bảng — truy vấn lại là đường vòng đưa danh sách đầy đủ trở
            // về popup định biên của Quản lý.
            var danhSachPhongBan = model.DanhSachPhongBan;

            var rowDefs = new List<(int MaChucVu, int? MaPhongBan, string TenChucVu, string TenPhongBan)>();

            foreach (var cv in chucVus)
            {
                if (cv.MaPhongBan.HasValue)
                {
                    rowDefs.Add((cv.MaChucVu, cv.MaPhongBan, cv.TenChucVu, cv.PhongBan?.TenPhongBan ?? "Khách sạn"));
                    continue;
                }

                // Chức danh dùng chung: một dòng cho mỗi bộ phận đang có người giữ.
                var phongBansCuaChucVu = phongBanTheoChucVuDungChung
                    .Where(p => p.MaChucVu == cv.MaChucVu)
                    .OrderBy(p => p.TenPhongBan)
                    .ToList();

                if (phamViPhongBanId.HasValue)
                {
                    // Với Quản lý: chỉ định biên cho chính bộ phận của mình.
                    var pbManager = phongBansCuaChucVu.FirstOrDefault(p => p.MaPhongBan == phamViPhongBanId.Value);
                    var tenPb = pbManager?.TenPhongBan ?? model.TenPhamViPhongBan ?? "Bộ phận của bạn";
                    rowDefs.Add((cv.MaChucVu, phamViPhongBanId.Value, cv.TenChucVu, tenPb));
                    continue;
                }

                foreach (var pb in phongBansCuaChucVu)
                {
                    rowDefs.Add((cv.MaChucVu, pb.MaPhongBan, cv.TenChucVu, pb.TenPhongBan ?? "Khách sạn"));
                }

                // Dòng dùng chung: chỉ dành cho Admin/HR khi đã có mức nền/mức ngày đặt ở khoá NULL,
                // hoặc khi chức danh chưa gắn được với bộ phận nào (dữ liệu trống) —
                // nếu bỏ đi thì định biên cũ trở nên vô hình, không sửa được nữa.
                var coMucDungChung = dinhBienTheoCa.Any(d => d.MaChucVu == cv.MaChucVu && d.MaPhongBan == null)
                    || dinhBienNgayTheoCa.Any(d => d.MaChucVu == cv.MaChucVu && d.MaPhongBan == null);

                if (coMucDungChung || phongBansCuaChucVu.Count == 0)
                {
                    rowDefs.Add((cv.MaChucVu, null, cv.TenChucVu, "Dùng chung mọi bộ phận"));
                }
            }

            var dowNames = new[] { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" };
            var allDinhBienNgayInWeek = await this.SearchDataAsync($"phanca:dinhbien:overrides:{weekStart:yyyyMMdd}:{weekEnd:yyyyMMdd}", () =>
                _context.DinhBienNgays.AsNoTracking()
                    .Where(d => d.Ngay >= weekStart && d.Ngay <= weekEnd)
                    .Select(d => d.Ngay)
                    .Distinct()
                    .ToListAsync());
            var daysWithOverride = allDinhBienNgayInWeek.ToHashSet();

            var weekDayOptions = new List<DinhBienDayOption>();
            for (int i = 0; i < 7; i++)
            {
                var d = weekStart.AddDays(i);
                weekDayOptions.Add(new DinhBienDayOption
                {
                    Date = d,
                    DayName = dowNames[i],
                    FormattedDate = d.ToString("dd/MM"),
                    IsSelected = d == model.SelectedDate,
                    HasCustomQuota = daysWithOverride.Contains(d)
                });
            }

            model.DinhBien = new DinhBienModalViewModel
            {
                Ngay = model.SelectedDate,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                WeekDays = weekDayOptions,
                DanhSachCa = model.DanhSachCa,
                DanhSachPhongBan = danhSachPhongBan,
                ReturnWeek = weekStart.ToString("yyyy-MM-dd"),
                CoQuyenSua = User.HasPermission(PermissionCodes.AssignShifts),
                Rows = rowDefs.Select(r => new DinhBienMatrixRow
                {
                    MaChucVu = r.MaChucVu,
                    MaPhongBan = r.MaPhongBan,
                    TenChucVu = r.TenChucVu,
                    TenPhongBan = r.TenPhongBan,
                    MucNenTheoCa = dinhBienTheoCa
                        .Where(d => d.MaChucVu == r.MaChucVu && d.MaPhongBan == r.MaPhongBan)
                        .ToDictionary(d => d.MaCa, d => d.SoNguoiToiThieu),
                    MucNgayTheoCa = dinhBienNgayTheoCa
                        .Where(d => d.MaChucVu == r.MaChucVu && d.MaPhongBan == r.MaPhongBan)
                        .ToDictionary(d => d.MaCa, d => d.SoNguoiToiThieu),
                    HienCoTheoCa = selectedDayShifts
                        .Where(p => p.TrangThai != "VANG_MAT"
                            && p.NhanVien?.MaChucVu == r.MaChucVu
                            && (!r.MaPhongBan.HasValue || (p.NhanVien.MaPhongBan ?? p.NhanVien.ChucVu?.MaPhongBan) == r.MaPhongBan.Value))
                        .GroupBy(p => p.MaCa)
                        .ToDictionary(g => g.Key, g => g.Count())
                }).ToList()
            };

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
                            .Select(p =>
                            {
                                var isShiftSwapped = swappedShiftsDict.TryGetValue(p.MaPhanCa, out var shiftSwapNote);
                                return new PhanCaItemDto
                                {
                                    MaPhanCa = p.MaPhanCa,
                                    MaCa = p.MaCa,
                                    TenCa = p.CaLamViec?.TenCa ?? "Ca",
                                    GioBatDau = p.CaLamViec?.GioBatDau ?? TimeOnly.MinValue,
                                    GioKetThuc = p.CaLamViec?.GioKetThuc ?? TimeOnly.MinValue,
                                    QuaDem = p.CaLamViec?.QuaDem ?? false,
                                    LoaiCa = p.CaLamViec?.LoaiCa ?? ShiftWorkModes.XOAY_CA,
                                    SacThai = p.CaLamViec?.SacThaiCa ?? "morning",
                                    TrangThai = p.TrangThai,
                                    HasAttendance = p.ChamCong != null,
                                    IsCheckedIn = p.ChamCong != null && p.ChamCong.GioCheckIn.HasValue,
                                    GioTangCaDuKien = p.GioTangCaDuKien,
                                    GhiChuOt = p.GhiChuOt,
                                    DaDoiCa = isShiftSwapped,
                                    ThongTinDoiCa = shiftSwapNote
                                };
                            }).ToList()
                    };
                    row.Days.Add(cell);
                }

                model.Rows.Add(row);
            }

            try
            {
                var actor = await EventActorAsync();
                if (actor != null && User.HasPermission(PermissionCodes.ManageHotelEvents))
                {
                    model.CanViewEventDemand = actor.IsOffice;
                    model.CanPublishWeek = actor.IsOffice;
                    if (model.CanViewEventDemand)
                    {
                        model.EventDemand = (await _events.ListAsync(actor,
                            HotelEventRules.ToUtc(weekStart.ToDateTime(TimeOnly.MinValue)),
                            HotelEventRules.ToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue))))
                            .Where(e => e.TinhTheoGio && e.TrangThai != "HUY").ToList();
                    }
                    model.PublishedWeek = await _events.PublishedWeekAsync(weekStart);
                }
            }
            catch (NpgsqlException ex)
            {
                _logger.LogWarning(ex, "Không thể tải nhu cầu sự kiện và công bố lịch tuần.");
                model.EventDemandUnavailable = true;
                model.CanPublishWeek = false;
            }
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nạp dữ liệu phân ca cho tuần {WeekStart}.", weekStart);
            TempData["Error"] = "Không thể tải dữ liệu phân ca từ hệ thống CSDL: " + ex.Message;
            return View(model);
        }
    }

    private async Task<EventActor?> EventActorAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return null;
        return await this.SearchDataAsync($"phanca:actor:{accountId}", async () =>
        {
            var account = await _context.TaiKhoans.AsNoTracking().Include(a => a.VaiTro).Include(a => a.NhanVien)
                .SingleOrDefaultAsync(a => a.MaTaiKhoan == accountId && a.TrangThai);
            var role = account?.VaiTro?.TenVaiTro.ToUpperInvariant() switch
            { "ADMIN" => "Admin", "HR" => "HR", "MANAGER" => "Manager", _ => "" };
            return account?.NhanVien?.TrangThai != "DANG_LAM" || role == "" ? null
                : new EventActor(account.MaNhanVien, account.MaTaiKhoan, role, account.TenDangNhap);
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishWeek(DateOnly weekStart, long version, string? reason)
    {
        var actor = await EventActorAsync();
        if (actor?.IsOffice != true) return Forbid();
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            await _events.PublishWeekAsync(actor, weekStart, version, reason);
            TempData["Success"] = "Đã công bố lịch tuần chính thức.";
        }
        catch (EventOperationException ex)
        {
            if (ex.Forbidden) return Forbid();
            TempData["Error"] = ex.Message;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Không thể công bố lịch tuần {WeekStart}.", weekStart);
            TempData["Error"] = "Lịch tuần vừa thay đổi; tải lại trước khi công bố.";
        }
        return RedirectToAction(nameof(Index), new { week = weekStart.ToString("yyyy-MM-dd") });
    }

    /// <summary>
    /// Phân ca mới cho nhân viên (Hỗ trợ phân ca đồng thời cho nhiều nhân sự)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePhanCaInputModel input)
    {
        // KHUNG GIỜ PHÂN CA QUẢN LÝ: cổng mở từ 00:00 Thứ Sáu đến 16:00 Chủ Nhật
        // hàng tuần. Sau 16:00 Chủ Nhật cổng đóng, HR/Admin có 1 giờ rà soát rồi
        // công bố lịch tuần lúc 17:00. Ngoài khung giờ này, chỉ Admin và HR mới
        // có quyền điều phối đột xuất.
        if (GetManagerDepartmentScope().HasValue && !ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen())
        {
            TempData["Error"] = TempData["ErrorMessage"] = $"Cổng phân ca định kỳ dành cho Quản lý chỉ mở {ShiftSchedulingPolicy.ManagerWindowLabel}. Hiện tại cổng đang đóng ({ShiftSchedulingPolicy.ManagerWindowClosedPeriod}). Vui lòng liên hệ Admin hoặc HR nếu cần phân ca đột xuất.";
            if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
            {
                return Redirect(input.ReturnUrl);
            }
            return RedirectToAction(nameof(Index), new { week = input.NgayLamViec != default ? input.NgayLamViec.ToString("yyyy-MM-dd") : null });
        }

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
            var result = await WriteScheduleAsync<IActionResult?>(async () =>
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

            // Lấy thông tin nhân viên kèm hợp đồng để kiểm tra hiệu lực phân ca, và
            // kèm chức vụ để đối chiếu chế độ làm việc với loại ca.
            var employeeMap = await _context.NhanViens
                .Include(n => n.HopDongs)
                .Include(n => n.ChucVu)
                .Where(n => empIds.Contains(n.MaNhanVien))
                .ToDictionaryAsync(n => n.MaNhanVien);

            // PHẠM VI PHÒNG BAN (PRD 3.2): Quản lý chỉ được xếp ca cho nhân sự thuộc
            // bộ phận mình. Admin và HR vẫn xếp được toàn khách sạn vì họ điều phối
            // liên bộ phận khi cao điểm. Lọc ở đây chứ không chỉ ẩn trên giao diện —
            // form có thể bị gửi lại bằng tay hoặc sửa request.
            var managerDeptId = GetManagerDepartmentScope();
            var skippedOutOfScopeCount = 0;
            if (managerDeptId.HasValue)
            {
                var inScope = employeeMap.Values
                    .Where(n => n.MaPhongBan == managerDeptId.Value)
                    .Select(n => n.MaNhanVien)
                    .ToHashSet();
                skippedOutOfScopeCount = empIds.Count(id => !inScope.Contains(id));
                empIds = empIds.Where(inScope.Contains).ToList();
            }

            if (empIds.Count == 0)
            {
                TempData["Error"] = TempData["ErrorMessage"] =
                    "Bạn chỉ được phân ca cho nhân sự thuộc phòng ban mình phụ trách.";
                return RedirectToAction(nameof(Index), new { week = targetDates.First().ToString("yyyy-MM-dd") });
            }

            // Kiểm tra số giờ tăng ca hợp lệ theo Điều 107 Bộ luật Lao động 2019
            if (input.GioTangCaDuKien > 0)
            {
                var otConflict = ShiftSchedulingPolicy.ValidateOvertime(input.GioTangCaDuKien);
                if (otConflict.HasConflict)
                {
                    TempData["Error"] = TempData["ErrorMessage"] = otConflict.ThongDiep;
                    return RedirectToAction(nameof(Index), new { week = targetDates.First().ToString("yyyy-MM-dd") });
                }
            }

            // Bản đồ ca đang có của từng nhân viên, nới thêm 1 ngày mỗi đầu để bắt được
            // ca đêm của hôm trước kéo sang hôm sau (PRD 3.2 chồng giờ + 3.3 nghỉ 8h).
            // Mốc kết thúc ca được tính kèm cả giờ tăng ca OT để đảm bảo khoảng nghỉ hợp lệ.
            var rangeStart = targetDates.Min().AddDays(-1);
            var rangeEnd = targetDates.Max().AddDays(1);
            var shiftWindows = (await _context.PhanCas.AsNoTracking()
                    .Where(p => p.TrangThai != "DA_HUY" && empIds.Contains(p.MaNhanVien) &&
                                p.NgayLamViec >= rangeStart && p.NgayLamViec <= rangeEnd)
                    .Select(p => new { p.MaNhanVien, p.BatDauDuKien, p.KetThucDuKien, p.GioTangCaDuKien })
                    .ToListAsync())
                .GroupBy(p => p.MaNhanVien)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new ShiftSchedulingPolicy.ShiftWindow(x.BatDauDuKien, x.KetThucDuKien.AddHours((double)x.GioTangCaDuKien))).ToList());

            int successCount = 0;
            int skippedDuplicateCount = 0;
            int skippedPastCount = 0;
            int skippedEndedCount = 0;
            int skippedNoContractCount = 0;
            int skippedOverlapCount = 0;
            int skippedRestCount = 0;
            int skippedWorkModeCount = 0;
            string? firstConflictMessage = null;
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

                    // RÀNG BUỘC CHẾ ĐỘ LÀM VIỆC: người hành chính không vào ca xoay, và
                    // ngược lại. Chặn cứng như luật hợp đồng — lịch sai nghiệp vụ thì
                    // dữ liệu demo cũng sai theo.
                    var cheDoConflict = ShiftSchedulingPolicy.ValidateWorkMode(
                        emp.ChucVu?.CheDoLamViec, ca.LoaiCa, emp.ChucVu?.TenChucVu, ca.TenCa);
                    if (cheDoConflict.HasConflict)
                    {
                        skippedWorkModeCount++;
                        firstConflictMessage ??= cheDoConflict.ThongDiep;
                        continue;
                    }

                    // Kiểm tra trùng đúng mã ca trong ngày
                    var exist = await _context.PhanCas.FirstOrDefaultAsync(p =>
                        p.MaNhanVien == empId &&
                        p.NgayLamViec == targetDate &&
                        p.MaCa == input.MaCa);

                    if (exist != null && exist.TrangThai != "DA_HUY")
                    {
                        skippedDuplicateCount++;
                        continue;
                    }

                    var shiftStart = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
                    var shiftEnd = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
                    var shiftEndWithOt = shiftEnd.AddHours((double)input.GioTangCaDuKien);

                    // Chồng giờ + nghỉ tối thiểu 8 giờ (PRD 3.2 và 3.3). Dùng danh sách
                    // ca đã tải sẵn ở trên, nối thêm các ca vừa xếp trong cùng lượt gọi
                    // để hai ca của cùng một đợt cũng không lọt lưới.
                    var existingWindows = shiftWindows.TryGetValue(empId, out var windows)
                        ? windows
                        : new List<ShiftSchedulingPolicy.ShiftWindow>();

                    var conflict = ShiftSchedulingPolicy.ValidateAssignment(existingWindows, shiftStart, shiftEndWithOt);
                    if (conflict.HasConflict)
                    {
                        if (conflict.Kind == ShiftConflictKind.Overlap)
                        {
                            skippedOverlapCount++;
                        }
                        else
                        {
                            skippedRestCount++;
                        }

                        firstConflictMessage ??= conflict.ThongDiep;
                        continue;
                    }

                    var phanCa = exist ?? new PhanCa { MaNhanVien = empId, MaCa = input.MaCa, NgayLamViec = targetDate };
                    phanCa.BatDauDuKien = shiftStart;
                    phanCa.KetThucDuKien = shiftEnd;
                    phanCa.GioTangCaDuKien = input.GioTangCaDuKien;
                    phanCa.GhiChuOt = string.IsNullOrWhiteSpace(input.GhiChuOt) ? null : input.GhiChuOt.Trim();
                    phanCa.TrangThai = safeStatus;
                    phanCa.DaCongBo = true;
                    if (exist == null) _context.PhanCas.Add(phanCa);
                    existingWindows.Add(new ShiftSchedulingPolicy.ShiftWindow(shiftStart, shiftEndWithOt));
                    shiftWindows[empId] = existingWindows;
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
                if (skippedOverlapCount > 0) skipReasons.Add($"{skippedOverlapCount} lượt trùng giờ với ca khác");
                if (skippedRestCount > 0) skipReasons.Add($"{skippedRestCount} lượt nghỉ giữa ca dưới {ShiftSchedulingPolicy.MIN_REST_HOURS:0} giờ");
                if (skippedWorkModeCount > 0) skipReasons.Add($"{skippedWorkModeCount} lượt lệch chế độ làm việc (hành chính ↔ ca xoay)");
                if (skippedOutOfScopeCount > 0) skipReasons.Add($"{skippedOutOfScopeCount} lượt ngoài phòng ban phụ trách");
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
                if (skippedOverlapCount > 0) errors.Add("Ca đang xếp trùng giờ với ca khác của nhân viên");
                if (skippedRestCount > 0) errors.Add($"Khoảng nghỉ giữa hai ca ngắn hơn {ShiftSchedulingPolicy.MIN_REST_HOURS:0} giờ theo {ShiftSchedulingPolicy.REST_RULE_CITATION}");
                if (skippedWorkModeCount > 0) errors.Add("Chế độ làm việc của chức danh không khớp loại ca (giờ hành chính ↔ ca xoay 24/7)");
                if (skippedOutOfScopeCount > 0) errors.Add("Có nhân sự ngoài phòng ban bạn phụ trách");

                // Thông điệp xung đột đầu tiên nêu rõ mốc giờ để người xếp ca biết
                // phải dời ca đi đâu, thay vì chỉ báo "không hợp lệ".
                if (firstConflictMessage != null)
                {
                    errors.Add(firstConflictMessage);
                }

                TempData["Warning"] = TempData["WarningMessage"] = errors.Count > 0
                    ? string.Join(" ", errors)
                    : "Không thể tạo lượt phân ca.";
            }
                return null;
            });
            if (result != null) return result;
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
        // KHUNG GIỜ PHÂN CA QUẢN LÝ: cổng mở từ 00:00 Thứ Sáu đến 16:00 Chủ Nhật.
        if (GetManagerDepartmentScope().HasValue && !ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen())
        {
            TempData["Error"] = TempData["ErrorMessage"] = $"Cổng phân ca định kỳ dành cho Quản lý chỉ mở {ShiftSchedulingPolicy.ManagerWindowLabel}. Hiện tại cổng đang đóng ({ShiftSchedulingPolicy.ManagerWindowClosedPeriod}). Vui lòng liên hệ Admin hoặc HR nếu cần xếp ca đột xuất.";
            return RedirectToAction(nameof(Index), new { week = weekStartStr });
        }

        if (maNhanVien <= 0 || maCa <= 0 || !DateOnly.TryParse(weekStartStr, out var weekStart))
        {
            TempData["Error"] = TempData["ErrorMessage"] = "Thông tin xếp ca nhanh không hợp lệ.";
            return RedirectToAction(nameof(Index), new { week = weekStartStr });
        }

        try
        {
            var result = await WriteScheduleAsync<IActionResult?>(async () =>
            {
            var emp = await _context.NhanViens
                .Include(n => n.HopDongs)
                .Include(n => n.ChucVu)
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

            // Nhân viên ngoài phòng ban phụ trách của quản lý thì không xếp ca được.
            var scopeDeptId = GetManagerDepartmentScope();
            if (scopeDeptId.HasValue && emp.MaPhongBan != scopeDeptId.Value)
            {
                TempData["Error"] = TempData["ErrorMessage"] = "Bạn chỉ được xếp ca cho nhân sự thuộc phòng ban mình phụ trách.";
                return RedirectToAction(nameof(Index), new { week = weekStartStr });
            }

            // Chế độ làm việc lệch loại ca thì chặn ngay từ đầu, không xếp được ngày
            // nào — báo một lần thay vì để từng ngày bị bỏ qua lặng lẽ.
            var cheDoConflict = ShiftSchedulingPolicy.ValidateWorkMode(
                emp.ChucVu?.CheDoLamViec, ca.LoaiCa, emp.ChucVu?.TenChucVu, ca.TenCa);
            if (cheDoConflict.HasConflict)
            {
                TempData["Error"] = TempData["ErrorMessage"] = cheDoConflict.ThongDiep;
                return RedirectToAction(nameof(Index), new { week = weekStartStr });
            }

            int addedCount = 0;
            int skippedPastCount = 0;
            int skippedNoContractCount = 0;
            int skippedOverlapCount = 0;
            int skippedRestCount = 0;
            string? firstConflictMessage = null;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nowLocal = DateTime.Now;

            // Nạp sẵn lịch hiện có của nhân viên trong tuần (rộng 1 ngày mỗi đầu để
            // bắt được ca đêm của ngày kề bên vắt qua ranh giới tuần).
            var existingWindows = (await _context.PhanCas.AsNoTracking()
                    .Where(p => p.TrangThai != "DA_HUY" && p.MaNhanVien == maNhanVien &&
                                p.NgayLamViec >= weekStart.AddDays(-1) &&
                                p.NgayLamViec <= weekStart.AddDays(6))
                    .Select(p => new { p.BatDauDuKien, p.KetThucDuKien })
                    .ToListAsync())
                .Select(p => new ShiftSchedulingPolicy.ShiftWindow(p.BatDauDuKien, p.KetThucDuKien))
                .ToList();

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

                var exist = await _context.PhanCas.FirstOrDefaultAsync(p =>
                    p.MaNhanVien == maNhanVien &&
                    p.NgayLamViec == workDate &&
                    p.MaCa == maCa);

                if (exist == null || exist.TrangThai == "DA_HUY")
                {
                    var shiftStart = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
                    var shiftEnd = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime();

                    // PRD 3.2 / 3.3: chặn trùng giờ và chặn nghỉ dưới 8 giờ.
                    var conflict = ShiftSchedulingPolicy.ValidateAssignment(existingWindows, shiftStart, shiftEnd);
                    if (conflict.HasConflict)
                    {
                        if (conflict.Kind == ShiftConflictKind.Overlap) skippedOverlapCount++;
                        else skippedRestCount++;
                        firstConflictMessage ??= conflict.ThongDiep;
                        continue;
                    }

                    var shift = exist ?? new PhanCa { MaNhanVien = maNhanVien, MaCa = maCa, NgayLamViec = workDate };
                    shift.BatDauDuKien = shiftStart;
                    shift.KetThucDuKien = shiftEnd;
                    shift.TrangThai = "DA_PHAN";
                    shift.DaCongBo = true;
                    if (exist == null) _context.PhanCas.Add(shift);
                    existingWindows.Add(new ShiftSchedulingPolicy.ShiftWindow(shiftStart, shiftEnd));
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
                if (skippedOverlapCount > 0)
                {
                    skipNotes.Add($"{skippedOverlapCount} ngày trùng giờ với ca khác");
                }
                if (skippedRestCount > 0)
                {
                    skipNotes.Add($"{skippedRestCount} ngày nghỉ giữa ca dưới {ShiftSchedulingPolicy.MIN_REST_HOURS:0} giờ");
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
                else if (skippedOverlapCount > 0 || skippedRestCount > 0)
                {
                    var reasons = new List<string>();
                    if (skippedOverlapCount > 0)
                    {
                        reasons.Add($"{skippedOverlapCount} ngày trùng giờ với ca khác");
                    }
                    if (skippedRestCount > 0)
                    {
                        reasons.Add($"{skippedRestCount} ngày nghỉ giữa ca dưới {ShiftSchedulingPolicy.MIN_REST_HOURS:0} giờ theo {ShiftSchedulingPolicy.REST_RULE_CITATION}");
                    }

                    TempData["Warning"] = TempData["WarningMessage"] =
                        $"Không xếp được ca cho {emp.HoTen}: {string.Join(", ", reasons)}."
                        + (firstConflictMessage != null ? " " + firstConflictMessage : string.Empty);
                }
                else
                {
                    TempData["Warning"] = TempData["WarningMessage"] = skippedPastCount > 0
                        ? "Không thể xếp ca nhanh do các ngày đã qua trong quá khứ hoặc ca đã hết giờ."
                        : "Nhân viên đã có lịch phân ca đầy đủ cho các ngày đã chọn.";
                }
            }
                return null;
            });
            if (result != null) return result;
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
    public async Task<IActionResult> Delete(int id, string? returnWeek, string? changeReason)
    {
        // KHUNG GIỜ PHÂN CA QUẢN LÝ: cổng mở từ 00:00 Thứ Sáu đến 16:00 Chủ Nhật.
        if (GetManagerDepartmentScope().HasValue && !ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen())
        {
            TempData["Error"] = TempData["ErrorMessage"] = $"Cổng điều chỉnh phân ca dành cho Quản lý chỉ mở {ShiftSchedulingPolicy.ManagerWindowLabel}. Hiện tại cổng đang đóng ({ShiftSchedulingPolicy.ManagerWindowClosedPeriod}). Vui lòng liên hệ Admin hoặc HR nếu cần hủy ca đột xuất.";
            return RedirectToAction(nameof(Index), new { week = returnWeek });
        }

        try
        {
            var result = await WriteScheduleAsync<IActionResult?>(async () =>
            {
            var phanCa = await _context.PhanCas
                .Include(p => p.ChamCong)
                .Include(p => p.NhanVien)
                .FirstOrDefaultAsync(p => p.MaPhanCa == id);

            if (phanCa != null)
            {
                // PHẠM VI BỘ PHẬN: mã phân ca đến từ form, không phải từ danh sách đã
                // lọc — thiếu bước này thì Quản lý bộ phận A vẫn xoá được ca của bộ
                // phận B bằng cách sửa id. Kiểm tra theo nhân sự của ca, không theo
                // bộ phận trên URL.
                var phamViXoaId = GetManagerDepartmentScope();
                if (phamViXoaId.HasValue && phanCa.NhanVien?.MaPhongBan != phamViXoaId.Value)
                {
                    TempData["Error"] = TempData["ErrorMessage"] = "Bạn chỉ được hủy ca trực của nhân sự thuộc phòng ban mình phụ trách.";
                    return RedirectToAction(nameof(Index), new { week = returnWeek });
                }
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

                    await CancelSourceShiftsAsync([phanCa], changeReason);
                await _context.SaveChangesAsync();
                    TempData["Success"] = TempData["SuccessMessage"] = "Đã hủy lượt phân ca thành công!";
            }
                return null;
            });
            if (result != null) return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa phân ca.");
            TempData["Error"] = TempData["ErrorMessage"] = "Không thể xóa phân ca: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { week = returnWeek });
    }

    /// <summary>
    /// Lưu lưới định biên ca (PRD 3.5).
    ///
    /// <para>
    /// Một lần lưu có thể ghi cả ba loại thay đổi trên cùng lưới: thêm ô mới, đổi
    /// số ô đã có, và xoá ô (khi người dùng nhập 0 hoặc để trống). Vì vậy mỗi
    /// ô phải được xử lý theo đúng ý người nhập chứ không phải "ghi đè toàn bộ":
    /// xoá nhầm một ô mức nền sẽ làm cả tháng mất cảnh báo ở ô đó.
    /// </para>
    ///
    /// <para>
    /// <paramref name="input"/>.<see cref="DinhBienSaveInput.Ngay"/> rỗng là lưu
    /// mức nền vào <c>dinh_bien_ca</c>; có ngày là lưu mức riêng vào
    /// <c>dinh_bien_ngay</c>. Hai bảng có cùng ràng buộc <c>so_nguoi &gt;= 1</c>
    /// nên số 0 luôn được hiểu là DELETE.
    /// </para>
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDinhBien(DinhBienSaveInput input)
    {
        var returnUrl = Url.Action(nameof(Index), new { week = input.ReturnWeek, openDinhBienModal = true });

        DateOnly? ngayApDung = null;
        if (!string.IsNullOrWhiteSpace(input.Ngay))
        {
            if (!DateOnly.TryParse(input.Ngay, out var parsed))
            {
                TempData["Error"] = "Ngày áp dụng định biên không hợp lệ.";
                return SafeRedirect(returnUrl);
            }
            ngayApDung = parsed;
        }

        // Mã ca / chức vụ phải tồn tại thật. Lọc trước để không gửi rác xuống
        // CSDL rồi nhận lỗi khoá ngoại khó hiểu cho người dùng.
        var hopLeCa = (await _context.CaLamViecs.AsNoTracking()
            .Select(c => c.MaCa).ToListAsync()).ToHashSet();
        var hopLeChucVu = (await _context.ChucVus.AsNoTracking()
            .Select(c => c.MaChucVu).ToListAsync()).ToHashSet();

        // Bộ phận gửi lên phải tồn tại thật; null là hợp lệ (dùng chung mọi bộ phận).
        var hopLePhongBan = (await _context.PhongBans.AsNoTracking()
            .Select(p => p.MaPhongBan).ToListAsync()).ToHashSet();

        // PHẠM VI BỘ PHẬN: Quản lý chỉ đặt định biên cho bộ phận mình. Ô "dùng chung
        // mọi bộ phận" (ma_phong_ban NULL) là mức toàn khách sạn — để Quản lý sửa
        // nghĩa là họ âm thầm đổi định biên của mọi bộ phận khác. Bỏ các ô đó thay
        // vì ghi đè chúng về bộ phận của họ: payload là dữ liệu client gửi lên.
        var phamViDinhBienId = User.GetManagedDepartmentScope();

        var cells = input.Cells
            .Where(c => hopLeCa.Contains(c.MaCa)
                && hopLeChucVu.Contains(c.MaChucVu)
                && (c.MaPhongBan == null || hopLePhongBan.Contains(c.MaPhongBan.Value))
                && (!phamViDinhBienId.HasValue || c.MaPhongBan == phamViDinhBienId.Value))
            .GroupBy(c => new { c.MaCa, c.MaChucVu, c.MaPhongBan })
            .Select(g => g.Last())
            .ToList();

        if (phamViDinhBienId.HasValue && cells.Count == 0)
        {
            TempData["Warning"] = "Bạn chỉ được đặt định biên cho bộ phận mình phụ trách.";
            return SafeRedirect(returnUrl);
        }

        if (cells.Count == 0)
        {
            TempData["Warning"] = "Không có ô định biên nào cần lưu.";
            return SafeRedirect(returnUrl);
        }

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (ngayApDung is null)
                {
                    await UpsertDinhBienMucNenAsync(cells);
                }
                else
                {
                    await UpsertDinhBienNgayAsync(ngayApDung.Value, cells);
                }

                await transaction.CommitAsync();
            });

            TempData["Success"] = ngayApDung is null
                ? $"Đã cập nhật định biên chung cho {cells.Count} ô."
                : $"Đã cập nhật định biên riêng ngày {ngayApDung:dd/MM/yyyy} cho {cells.Count} ô.";
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogError(ex, "Bảng dinh_bien_ngay chưa tồn tại khi lưu định biên theo ngày.");
            TempData["Error"] = MissingDinhBienNgayMessage;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogError(ex, "Cột ma_phong_ban chưa có khi lưu định biên — chưa chạy migration 20261026.");
            TempData["Error"] = MissingDinhBienBoPhanMessage;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable })
        {
            // SaveChanges bọc lỗi Postgres trong DbUpdateException, nên phải soi cả
            // lớp trong — nếu không, lỗi "chưa chạy migration" sẽ hiện thành thông
            // báo chung chung "kiểm tra lại số người tối thiểu", dẫn người dùng đi sai hướng.
            _logger.LogError(ex, "Bảng dinh_bien_ngay chưa tồn tại khi lưu định biên theo ngày.");
            TempData["Error"] = MissingDinhBienNgayMessage;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedColumn })
        {
            _logger.LogError(ex, "Cột ma_phong_ban chưa có khi lưu định biên — chưa chạy migration 20261026.");
            TempData["Error"] = MissingDinhBienBoPhanMessage;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể lưu định biên ca.");
            TempData["Error"] = "Không thể lưu định biên. Vui lòng kiểm tra lại số người tối thiểu.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi lưu định biên ca.");
            TempData["Error"] = "Lỗi khi lưu định biên: " + ex.Message;
        }

        return SafeRedirect(returnUrl);
    }

    /// <summary>
    /// API đọc dữ liệu định biên riêng và số nhân sự thực tế theo từng ô ca × chức vụ × bộ phận
    /// của một ngày được chọn (phục vụ chuyển ngày tức thì trong popup định biên ca).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDinhBienNgayAjax(string ngay)
    {
        if (!DateOnly.TryParse(ngay, out var parsedDate))
        {
            return Json(new { success = false, message = "Ngày áp dụng không hợp lệ." });
        }

        var dinhBienNgay = await DocDinhBienNgayAsync(parsedDate);
        var phamViId = GetManagerDepartmentScope();

        var shiftsQuery = _context.PhanCas.AsNoTracking()
            .Include(p => p.NhanVien)
            .Where(p => p.NgayLamViec == parsedDate && p.TrangThai != "VANG_MAT");

        if (phamViId.HasValue)
        {
            shiftsQuery = shiftsQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phamViId.Value);
        }

        var shiftsInDay = await shiftsQuery.ToListAsync();

        var items = dinhBienNgay
            .Where(d => !phamViId.HasValue || d.MaPhongBan == phamViId.Value)
            .Select(d => new
            {
                maCa = d.MaCa,
                maChucVu = d.MaChucVu,
                maPhongBan = d.MaPhongBan,
                mucNgay = d.SoNguoiToiThieu
            }).ToList();

        var hienCo = shiftsInDay
            .Where(p => p.NhanVien?.MaChucVu != null)
            .GroupBy(p => new
            {
                p.MaCa,
                MaChucVu = p.NhanVien!.MaChucVu!.Value,
                MaPhongBan = p.NhanVien.MaPhongBan ?? p.NhanVien.ChucVu?.MaPhongBan
            })
            .Select(g => new
            {
                maCa = g.Key.MaCa,
                maChucVu = g.Key.MaChucVu,
                maPhongBan = g.Key.MaPhongBan,
                soLuong = g.Count()
            })
            .ToList();

        return Json(new
        {
            success = true,
            ngay = parsedDate.ToString("yyyy-MM-dd"),
            ngayHienThi = parsedDate.ToString("dd/MM/yyyy"),
            dinhBienNgay = items,
            hienCo = hienCo
        });
    }

    /// <summary>
    /// Đọc mức định biên nền, chịu được trường hợp CSDL chưa có cột <c>ma_phong_ban</c>
    /// (migration 20261026 chưa chạy). Khi đó trả về danh sách rỗng thay vì làm sập
    /// màn phân ca — cảnh báo thiếu định biên tạm ẩn, lịch vẫn dùng được bình thường.
    /// </summary>
    private async Task<List<DinhBienCa>> DocDinhBienCaAsync()
    {
        return await this.SearchDataAsync("phanca:dinhbien:baseline", async () =>
        {
            try
            {
                return await _context.DinhBienCas.AsNoTracking()
                    .Include(d => d.ChucVu)
                    .Include(d => d.PhongBan)
                    .ToListAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning(ex, "Bảng dinh_bien_ca chưa tồn tại — bỏ qua đối chiếu định biên.");
                return new List<DinhBienCa>();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Cột ma_phong_ban chưa có trên dinh_bien_ca — chưa chạy migration 20261026.");
                return new List<DinhBienCa>();
            }
        });
    }

    /// <summary>
    /// Đọc mức định biên riêng của một ngày, chịu được cả trường hợp bảng chưa tồn
    /// tại (migration 20261021 chưa chạy) lẫn cột bộ phận chưa có (20261026).
    /// </summary>
    private async Task<List<DinhBienNgay>> DocDinhBienNgayAsync(DateOnly ngay)
    {
        return await this.SearchDataAsync($"phanca:dinhbien:day:{ngay:yyyyMMdd}", async () =>
        {
            try
            {
                return await _context.DinhBienNgays.AsNoTracking()
                    .Include(d => d.ChucVu)
                    .Include(d => d.PhongBan)
                    .Where(d => d.Ngay == ngay)
                    .ToListAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning(ex, "Bảng dinh_bien_ngay chưa tồn tại — chỉ dùng mức định biên chung.");
                return new List<DinhBienNgay>();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Cột ma_phong_ban chưa có trên dinh_bien_ngay — chưa chạy migration 20261026.");
                return new List<DinhBienNgay>();
            }
        });
    }

    /// <summary>
    /// Ghi mức nền vào <c>dinh_bien_ca</c>: số &gt;= 1 là upsert, 0 là xoá dòng.
    /// </summary>
    private async Task UpsertDinhBienMucNenAsync(List<DinhBienCellInput> cells)
    {
        // Nạp đúng các cặp cần đụng tới, không nạp cả bảng — bảng có thể nhiều dòng
        // (số ca × số chức vụ) trong khi mỗi lần lưu chỉ vài ô.
        var maCaCanXuLy = cells.Select(c => c.MaCa).Distinct().ToList();
        var hienCo = await _context.DinhBienCas
            .Where(d => maCaCanXuLy.Contains(d.MaCa))
            .ToListAsync();

        foreach (var cell in cells)
        {
            var dong = hienCo.FirstOrDefault(d => d.MaCa == cell.MaCa
                && d.MaChucVu == cell.MaChucVu
                && d.MaPhongBan == cell.MaPhongBan);

            if (cell.SoNguoi <= 0)
            {
                if (dong is not null)
                {
                    _context.DinhBienCas.Remove(dong);
                }
                continue;
            }

            if (dong is null)
            {
                _context.DinhBienCas.Add(new DinhBienCa
                {
                    MaCa = cell.MaCa,
                    MaChucVu = cell.MaChucVu,
                    MaPhongBan = cell.MaPhongBan,
                    SoNguoiToiThieu = cell.SoNguoi
                });
            }
            else
            {
                dong.SoNguoiToiThieu = cell.SoNguoi;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Ghi mức riêng của một ngày vào <c>dinh_bien_ngay</c>: số &gt;= 1 là upsert,
    /// 0 là xoá dòng để ô đó quay về theo mức nền.
    /// </summary>
    private async Task UpsertDinhBienNgayAsync(DateOnly ngay, List<DinhBienCellInput> cells)
    {
        var hienCo = await _context.DinhBienNgays
            .Where(d => d.Ngay == ngay)
            .ToListAsync();

        foreach (var cell in cells)
        {
            var dong = hienCo.FirstOrDefault(d => d.MaCa == cell.MaCa
                && d.MaChucVu == cell.MaChucVu
                && d.MaPhongBan == cell.MaPhongBan);

            if (cell.SoNguoi <= 0)
            {
                if (dong is not null)
                {
                    _context.DinhBienNgays.Remove(dong);
                }
                continue;
            }

            if (dong is null)
            {
                _context.DinhBienNgays.Add(new DinhBienNgay
                {
                    Ngay = ngay,
                    MaCa = cell.MaCa,
                    MaChucVu = cell.MaChucVu,
                    MaPhongBan = cell.MaPhongBan,
                    SoNguoiToiThieu = cell.SoNguoi
                });
            }
            else
            {
                dong.SoNguoiToiThieu = cell.SoNguoi;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Chuyển hướng về URL nội bộ, rơi về Index nếu URL rỗng hoặc không hợp lệ.
    /// </summary>
    private IActionResult SafeRedirect(string? url) =>
        string.IsNullOrWhiteSpace(url) || !Url.IsLocalUrl(url)
            ? RedirectToAction(nameof(Index))
            : LocalRedirect(url);

    /// <summary>
    /// API gán nhanh / đổi ca / xóa ca trực tiếp trên cuốn lịch ma trận (In-Place Ajax)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAssignAjax([FromBody] QuickAssignAjaxModel? input)
    {
        if (input == null || !ModelState.IsValid || input.MaNhanVien <= 0 || !DateOnly.TryParse(input.NgayLamViec, out var targetDate))
        {
            return Json(new { success = false, message = "Thông tin nhân viên hoặc ngày làm việc không hợp lệ." });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (targetDate < today)
        {
            return Json(new { success = false, message = "Không thể phân ca hoặc sửa ca cho ngày trong quá khứ." });
        }

        // KHUNG GIỜ PHÂN CA QUẢN LÝ: cổng mở từ 00:00 Thứ Sáu đến 16:00 Chủ Nhật.
        if (GetManagerDepartmentScope().HasValue && !ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen())
        {
            return Json(new { success = false, message = $"Cổng phân ca định kỳ dành cho Quản lý chỉ mở {ShiftSchedulingPolicy.ManagerWindowLabel}. Hiện tại cổng đang đóng ({ShiftSchedulingPolicy.ManagerWindowClosedPeriod}). Vui lòng liên hệ Admin hoặc HR nếu cần điều phối đột xuất." });
        }

        try
        {
            return await WriteScheduleAsync<IActionResult>(async () =>
            {
            var emp = await _context.NhanViens
                .Include(n => n.HopDongs)
                .Include(n => n.ChucVu)
                .FirstOrDefaultAsync(n => n.MaNhanVien == input.MaNhanVien);

            if (emp == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin nhân viên." });
            }

            // PRD 3.1: quản lý chỉ xếp ca trong phòng ban mình phụ trách.
            var scopeDeptId = GetManagerDepartmentScope();
            if (scopeDeptId.HasValue && emp.MaPhongBan != scopeDeptId.Value)
            {
                return Json(new { success = false, message = "Bạn chỉ được xếp ca cho nhân sự thuộc phòng ban mình phụ trách." });
            }

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
                    await CancelSourceShiftsAsync(oldShifts, input.ChangeReason);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, action = "removed", maNhanVien = input.MaNhanVien, ngayLamViec = input.NgayLamViec });
            }

            // 2. Chế độ gán hoặc đổi ca
            // RÀNG BUỘC HỢP ĐỒNG: Kiểm tra hiệu lực hợp đồng của nhân viên vào ngày làm việc
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

            // Chặn cứng lệch chế độ làm việc. Kéo thả trên lưới ma trận là đường
            // dễ lách nhất — người dùng chỉ thấy icon ca rồi thả, không đọc lại
            // chức danh.
            var cheDoConflict = ShiftSchedulingPolicy.ValidateWorkMode(
                emp.ChucVu?.CheDoLamViec, ca.LoaiCa, emp.ChucVu?.TenChucVu, ca.TenCa);
            if (cheDoConflict.HasConflict)
            {
                return Json(new { success = false, message = cheDoConflict.ThongDiep });
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

            if (input.GioTangCaDuKien > 0)
            {
                var otConflict = ShiftSchedulingPolicy.ValidateOvertime(input.GioTangCaDuKien);
                if (otConflict.HasConflict)
                {
                    return Json(new { success = false, message = otConflict.ThongDiep });
                }
            }

            var shiftStart = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
            var shiftEnd = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
            var shiftEndWithOt = shiftEnd.AddHours((double)input.GioTangCaDuKien);
            var activeShifts = existing.Where(p => p.TrangThai != "DA_HUY").ToList();
            var sameSource = activeShifts.Count == 1 && activeShifts[0].MaCa == input.MaCa
                && activeShifts[0].BatDauDuKien == shiftStart && activeShifts[0].KetThucDuKien == shiftEnd
                && activeShifts[0].GioTangCaDuKien == input.GioTangCaDuKien;
            if (!sameSource && existing.Count != 0)
                await CancelSourceShiftsAsync(existing, input.ChangeReason);

            // PRD 3.2 / 3.3: ca đang xếp không được trùng giờ và phải giữ khoảng
            // nghỉ tối thiểu với các ca của những ngày khác (kể cả thời gian tăng ca OT). Ca cùng ngày vừa nằm
            // trong `existing` (sẽ bị xoá) nên không tính vào tập so sánh.
            var otherWindowsRaw = await _context.PhanCas.AsNoTracking()
                    .Where(p => p.TrangThai != "DA_HUY" && p.MaNhanVien == input.MaNhanVien &&
                                p.NgayLamViec != targetDate &&
                                p.NgayLamViec >= targetDate.AddDays(-2) &&
                                p.NgayLamViec <= targetDate.AddDays(2))
                    .Select(p => new { p.BatDauDuKien, p.KetThucDuKien, p.GioTangCaDuKien })
                    .ToListAsync();
            var otherWindows = otherWindowsRaw
                .Select(p => new ShiftSchedulingPolicy.ShiftWindow(p.BatDauDuKien, p.KetThucDuKien.AddHours((double)p.GioTangCaDuKien)));

            var conflict = ShiftSchedulingPolicy.ValidateAssignment(otherWindows, shiftStart, shiftEndWithOt);
            if (conflict.HasConflict)
            {
                return Json(new { success = false, message = conflict.ThongDiep });
            }

            var phanCa = (sameSource ? activeShifts[0] : existing.FirstOrDefault(p => p.MaCa == input.MaCa && p.TrangThai == "DA_HUY"))
                ?? new PhanCa { MaNhanVien = input.MaNhanVien, MaCa = input.MaCa, NgayLamViec = targetDate };
            phanCa.BatDauDuKien = shiftStart;
            phanCa.KetThucDuKien = shiftEnd;
            phanCa.GioTangCaDuKien = input.GioTangCaDuKien;
            phanCa.GhiChuOt = string.IsNullOrWhiteSpace(input.GhiChuOt) ? null : input.GhiChuOt.Trim();
            phanCa.TrangThai = "DA_PHAN";
            phanCa.DaCongBo = true;
            if (phanCa.MaPhanCa == 0) _context.PhanCas.Add(phanCa);
            else _context.Entry(phanCa).State = EntityState.Modified;
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
                quaDem = ca.QuaDem,
                gioTangCaDuKien = phanCa.GioTangCaDuKien,
                ghiChuOt = phanCa.GhiChuOt,
                coTangCa = phanCa.CoTangCa,
                // Trả về loại ca + sắc thái để JS chọn đúng màu thẻ: trước đây ca
                // hành chính (không có "Sáng"/"Chiều"/"Đêm" trong tên) bị tô như ca sáng.
                loaiCa = ca.LoaiCa,
                sacThai = ca.SacThaiCa
            });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gán nhanh ca trực cho nhân viên {MaNhanVien} ngày {NgayLamViec}", input.MaNhanVien, input.NgayLamViec);
            return Json(new { success = false, message = "Lỗi khi lưu dữ liệu vào CSDL: " + ex.Message });
        }
    }

    private Task<T> WriteScheduleAsync<T>(Func<Task<T>> operation) =>
        _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
            var result = await operation();
            await transaction.CommitAsync();
            return result;
        });

    private async Task CancelSourceShiftsAsync(IReadOnlyList<PhanCa> shifts, string? reason)
    {
        var active = shifts.Where(s => s.TrangThai != "DA_HUY").ToList();
        if (active.Count == 0) return;
        reason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new InvalidOperationException("Ghi lý do hủy / đổi ca (tối đa 500 ký tự). Phân công sự kiện từ ca này sẽ cần xác nhận lại.");
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.shift_change_reason', {reason}, true)");
        var ids = active.Select(s => s.MaPhanCa).ToList();
        var referenced = await _context.PhanCongDuAns.Where(p => p.MaPhanCa.HasValue && ids.Contains(p.MaPhanCa.Value))
            .Select(p => p.MaPhanCa!.Value).Distinct().ToListAsync();
        foreach (var shift in active)
        {
            if (referenced.Contains(shift.MaPhanCa)) shift.TrangThai = "DA_HUY";
            else _context.PhanCas.Remove(shift);
        }
        var name = User.Identity?.Name ?? "Người xếp ca";
        _context.NhatKyQuanTris.Add(new NhatKyQuanTri {
            MaTaiKhoanThucHien = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : null,
            TenNguoiThucHien = name.Length > 100 ? name[..100] : name,
            TenTaiKhoanBiTacDong = "LICH-CA", HanhDong = "HUY_DOI_CA",
            NoiDung = "Hủy / thay thế ca #" + string.Join(", #", ids) + "; giữ ca có lịch sử sự kiện.", LyDo = reason
        });
    }

    /// <summary>
    /// Phạm vi phòng ban mà người đang đăng nhập được phép xếp ca (PRD 3.1).
    ///
    /// Chỉ là lớp gọi mỏng sang <see cref="DepartmentScope.GetManagedDepartmentScope"/>
    /// — quy tắc nằm ở một chỗ duy nhất dùng chung với Chấm công, Giải trình và
    /// Yêu cầu đổi ca, để sửa phân quyền không phải sửa bốn bản sao.
    /// </summary>
    private int? GetManagerDepartmentScope() => User.GetManagedDepartmentScope();

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
}
