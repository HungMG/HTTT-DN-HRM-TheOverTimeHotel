using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

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
                .Where(p => p.TrangThai != "DA_HUY" && p.NgayLamViec >= gridStart && p.NgayLamViec <= gridEnd);

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
                .Where(p => p.TrangThai != "DA_HUY" && p.NgayLamViec == today)
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
                    .ToList();

                if (mucHieuLuc.Count == 0)
                {
                    continue;
                }

                foreach (var dinhBien in mucHieuLuc
                    .OrderBy(d => d.ChucVu?.TenChucVu)
                    .ThenBy(d => d.PhongBan?.TenPhongBan))
                {
                    var soNguoiHienCo = soNguoiTheoODinhBien.TryGetValue(
                        (shiftGroup.MaCa, dinhBien.MaChucVu, dinhBien.MaPhongBan), out var dem) ? dem : 0;

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
            var chucVus = await _context.ChucVus.AsNoTracking()
                .Include(c => c.PhongBan)
                .OrderBy(c => c.TenChucVu)
                .ToListAsync();

            // Bộ phận thực tế của từng chức danh dùng chung, suy từ nhân sự đang giữ
            // chức danh đó. Không suy từ chuc_vu vì cột đó NULL đúng theo thiết kế.
            var phongBanTheoChucVuDungChung = await _context.NhanViens.AsNoTracking()
                .Where(n => n.MaChucVu.HasValue && n.MaPhongBan.HasValue)
                .Select(n => new { n.MaChucVu, n.MaPhongBan, TenPhongBan = n.PhongBan!.TenPhongBan })
                .Distinct()
                .ToListAsync();

            var danhSachPhongBan = await _context.PhongBans.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .ToListAsync();

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

                foreach (var pb in phongBansCuaChucVu)
                {
                    rowDefs.Add((cv.MaChucVu, pb.MaPhongBan, cv.TenChucVu, pb.TenPhongBan ?? "Khách sạn"));
                }

                // Dòng dùng chung: giữ lại khi đã có mức nền/mức ngày đặt ở khoá NULL,
                // hoặc khi chức danh chưa gắn được với bộ phận nào (dữ liệu trống) —
                // nếu bỏ đi thì định biên cũ trở nên vô hình, không sửa được nữa.
                var coMucDungChung = dinhBienTheoCa.Any(d => d.MaChucVu == cv.MaChucVu && d.MaPhongBan == null)
                    || dinhBienNgayTheoCa.Any(d => d.MaChucVu == cv.MaChucVu && d.MaPhongBan == null);

                if (coMucDungChung || phongBansCuaChucVu.Count == 0)
                {
                    rowDefs.Add((cv.MaChucVu, null, cv.TenChucVu, "Dùng chung mọi bộ phận"));
                }
            }

            model.DinhBien = new DinhBienModalViewModel
            {
                Ngay = model.SelectedDate,
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
                            && p.NhanVien.MaPhongBan == r.MaPhongBan)
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

            try
            {
                var actor = await EventActorAsync();
                if (actor != null)
                {
                    model.CanViewEventDemand = true;
                    model.CanPublishWeek = actor.IsOffice;
                    model.EventDemand = (await _events.ListAsync(actor,
                        HotelEventRules.ToUtc(weekStart.ToDateTime(TimeOnly.MinValue)),
                        HotelEventRules.ToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue))))
                        .Where(e => e.TinhTheoGio && e.TrangThai != "HUY").ToList();
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
            _logger.LogWarning(ex, "Đang nạp dữ liệu phân ca đồng bộ chuẩn 20 nhân sự và 4 ca Supabase.");
            PopulateRealSeededSchedule(model, weekStart, weekEnd);
            ViewBag.IsDemoData = false;
            return View(model);
        }
    }

    private async Task<EventActor?> EventActorAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return null;
        var account = await _context.TaiKhoans.AsNoTracking().Include(a => a.VaiTro).Include(a => a.NhanVien)
            .SingleOrDefaultAsync(a => a.MaTaiKhoan == accountId && a.TrangThai);
        var role = account?.VaiTro?.TenVaiTro.ToUpperInvariant() switch
        { "ADMIN" => "Admin", "HR" => "HR", "MANAGER" => "Manager", _ => "" };
        return account?.NhanVien?.TrangThai != "DANG_LAM" || role == "" ? null
            : new EventActor(account.MaNhanVien, account.MaTaiKhoan, role, account.TenDangNhap);
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

            // Lấy thông tin nhân viên kèm hợp đồng để kiểm tra hiệu lực phân ca
            var employeeMap = await _context.NhanViens
                .Include(n => n.HopDongs)
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

            // Bản đồ ca đang có của từng nhân viên, nới thêm 1 ngày mỗi đầu để bắt được
            // ca đêm của hôm trước kéo sang hôm sau (PRD 3.2 chồng giờ + 3.3 nghỉ 8h).
            var rangeStart = targetDates.Min().AddDays(-1);
            var rangeEnd = targetDates.Max().AddDays(1);
            var shiftWindows = (await _context.PhanCas.AsNoTracking()
                    .Where(p => p.TrangThai != "DA_HUY" && empIds.Contains(p.MaNhanVien) &&
                                p.NgayLamViec >= rangeStart && p.NgayLamViec <= rangeEnd)
                    .Select(p => new { p.MaNhanVien, p.BatDauDuKien, p.KetThucDuKien })
                    .ToListAsync())
                .GroupBy(p => p.MaNhanVien)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new ShiftSchedulingPolicy.ShiftWindow(x.BatDauDuKien, x.KetThucDuKien)).ToList());

            int successCount = 0;
            int skippedDuplicateCount = 0;
            int skippedPastCount = 0;
            int skippedEndedCount = 0;
            int skippedNoContractCount = 0;
            int skippedOverlapCount = 0;
            int skippedRestCount = 0;
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

                    // Chồng giờ + nghỉ tối thiểu 8 giờ (PRD 3.2 và 3.3). Dùng danh sách
                    // ca đã tải sẵn ở trên, nối thêm các ca vừa xếp trong cùng lượt gọi
                    // để hai ca của cùng một đợt cũng không lọt lưới.
                    var existingWindows = shiftWindows.TryGetValue(empId, out var windows)
                        ? windows
                        : new List<ShiftSchedulingPolicy.ShiftWindow>();

                    var conflict = ShiftSchedulingPolicy.ValidateAssignment(existingWindows, shiftStart, shiftEnd);
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
                    phanCa.TrangThai = safeStatus;
                    phanCa.DaCongBo = false;
                    if (exist == null) _context.PhanCas.Add(phanCa);
                    existingWindows.Add(new ShiftSchedulingPolicy.ShiftWindow(shiftStart, shiftEnd));
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
                    shift.DaCongBo = false;
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
        try
        {
            var result = await WriteScheduleAsync<IActionResult?>(async () =>
            {
            var phanCa = await _context.PhanCas
                .Include(p => p.ChamCong).Include(p => p.NhanVien)
                .FirstOrDefaultAsync(p => p.MaPhanCa == id);

            if (phanCa != null)
            {
                var scope = GetManagerDepartmentScope();
                if (scope.HasValue && phanCa.NhanVien?.MaPhongBan != scope.Value) return Forbid();
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

        var cells = input.Cells
            .Where(c => hopLeCa.Contains(c.MaCa)
                && hopLeChucVu.Contains(c.MaChucVu)
                && (c.MaPhongBan == null || hopLePhongBan.Contains(c.MaPhongBan.Value)))
            .GroupBy(c => new { c.MaCa, c.MaChucVu, c.MaPhongBan })
            .Select(g => g.Last())
            .ToList();

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
    /// Đọc mức định biên nền, chịu được trường hợp CSDL chưa có cột <c>ma_phong_ban</c>
    /// (migration 20261026 chưa chạy). Khi đó trả về danh sách rỗng thay vì làm sập
    /// màn phân ca — cảnh báo thiếu định biên tạm ẩn, lịch vẫn dùng được bình thường.
    /// </summary>
    private async Task<List<DinhBienCa>> DocDinhBienCaAsync()
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
    }

    /// <summary>
    /// Đọc mức định biên riêng của một ngày, chịu được cả trường hợp bảng chưa tồn
    /// tại (migration 20261021 chưa chạy) lẫn cột bộ phận chưa có (20261026).
    /// </summary>
    private async Task<List<DinhBienNgay>> DocDinhBienNgayAsync(DateOnly ngay)
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

        try
        {
            return await WriteScheduleAsync<IActionResult>(async () =>
            {
            var emp = await _context.NhanViens
                .Include(n => n.HopDongs)
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

            var shiftStart = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
            var shiftEnd = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)).ToUniversalTime();
            var activeShifts = existing.Where(p => p.TrangThai != "DA_HUY").ToList();
            var sameSource = activeShifts.Count == 1 && activeShifts[0].MaCa == input.MaCa
                && activeShifts[0].BatDauDuKien == shiftStart && activeShifts[0].KetThucDuKien == shiftEnd;
            if (!sameSource && existing.Count != 0)
                await CancelSourceShiftsAsync(existing, input.ChangeReason);

            // PRD 3.2 / 3.3: ca đang xếp không được trùng giờ và phải giữ khoảng
            // nghỉ tối thiểu với các ca của những ngày khác. Ca cùng ngày vừa nằm
            // trong `existing` (sẽ bị xoá) nên không tính vào tập so sánh.
            var otherWindows = (await _context.PhanCas.AsNoTracking()
                    .Where(p => p.TrangThai != "DA_HUY" && p.MaNhanVien == input.MaNhanVien &&
                                p.NgayLamViec != targetDate &&
                                p.NgayLamViec >= targetDate.AddDays(-2) &&
                                p.NgayLamViec <= targetDate.AddDays(2))
                    .Select(p => new { p.BatDauDuKien, p.KetThucDuKien })
                    .ToListAsync())
                .Select(p => new ShiftSchedulingPolicy.ShiftWindow(p.BatDauDuKien, p.KetThucDuKien));

            var conflict = ShiftSchedulingPolicy.ValidateAssignment(otherWindows, shiftStart, shiftEnd);
            if (conflict.HasConflict)
            {
                return Json(new { success = false, message = conflict.ThongDiep });
            }

            var phanCa = (sameSource ? activeShifts[0] : existing.FirstOrDefault(p => p.MaCa == input.MaCa && p.TrangThai == "DA_HUY"))
                ?? new PhanCa { MaNhanVien = input.MaNhanVien, MaCa = input.MaCa, NgayLamViec = targetDate };
            phanCa.BatDauDuKien = shiftStart;
            phanCa.KetThucDuKien = shiftEnd;
            phanCa.TrangThai = "DA_PHAN";
            phanCa.DaCongBo = sameSource && phanCa.DaCongBo;
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
                quaDem = ca.QuaDem
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
    /// Phạm vi phòng ban mà người đang đăng nhập được phép xếp ca (PRD 3.1 —
    /// quản lý chỉ xếp ca cho nhân sự thuộc phòng ban mình phụ trách).
    ///
    /// Trả về <c>null</c> nghĩa là KHÔNG giới hạn: Admin và HR vẫn xếp ca toàn
    /// khách sạn. Chỉ vai trò Manager mới bị giới hạn, và chỉ khi tài khoản có
    /// claim <c>DepartmentId</c> hợp lệ — tài khoản Manager thiếu claim phòng ban
    /// là dữ liệu hỏng, đóng về "không xếp được ca nào" thay vì mở toàn bộ.
    /// </summary>
    private int? GetManagerDepartmentScope()
    {
        if (!User.IsInRole("Manager"))
        {
            return null;
        }

        return int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId) && departmentId > 0
            ? departmentId
            : -1; // -1 không khớp phòng ban nào ⇒ danh sách nhân sự rỗng
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

        // Dữ liệu popup định biên ở chế độ dự phòng. Bình thường phần này do
        // nhánh đọc CSDL dựng (mục 8c); khi rơi vào đây mà để trống thì nút
        // "Định Biên Ca" sẽ mở ra một lưới không có dòng nào — trông như lỗi.
        // Dùng đúng danh mục chức vụ/ca vừa dựng ở trên để hai nơi khớp nhau.
        var chucVuDuPhong = model.DanhSachNhanVien
            .Where(nv => nv.ChucVu != null)
            .Select(nv => nv.ChucVu!)
            .GroupBy(cv => cv.MaChucVu)
            .Select(g => g.First())
            .OrderBy(cv => cv.TenChucVu)
            .ToList();

        model.DinhBien = new DinhBienModalViewModel
        {
            Ngay = model.SelectedDate,
            DanhSachCa = model.DanhSachCa,
            // Hàm này là nhánh dự phòng đồng bộ; danh mục bộ phận lấy từ danh sách
            // nhân viên đã nạp sẵn thay vì gọi CSDL thêm một lượt.
            DanhSachPhongBan = model.DanhSachNhanVien
                .Where(nv => nv.PhongBan != null)
                .Select(nv => nv.PhongBan!)
                .GroupBy(p => p.MaPhongBan)
                .Select(g => g.First())
                .OrderBy(p => p.TenPhongBan)
                .ToList(),
            ReturnWeek = weekStart.ToString("yyyy-MM-dd"),
            CoQuyenSua = User.HasPermission(PermissionCodes.AssignShifts),
            Rows = chucVuDuPhong
                // Chế độ dự phòng dựng theo nhân sự thật, nên đã biết bộ phận của từng
                // người: chức danh dùng chung sinh một dòng cho mỗi bộ phận có người.
                .SelectMany(cv =>
                {
                    var phongBans = model.DanhSachNhanVien
                        .Where(nv => nv.MaChucVu == cv.MaChucVu)
                        .Select(nv => (nv.MaPhongBan, nv.PhongBan?.TenPhongBan))
                        .Distinct()
                        .ToList();

                    if (phongBans.Count == 0)
                    {
                        phongBans.Add((null, "Dùng chung mọi bộ phận"));
                    }

                    return phongBans.Select(pb => (cv, pb.MaPhongBan, pb.TenPhongBan));
                })
                .Select(x => new DinhBienMatrixRow
                {
                    MaChucVu = x.cv.MaChucVu,
                    MaPhongBan = x.MaPhongBan,
                    TenChucVu = x.cv.TenChucVu,
                    TenPhongBan = x.TenPhongBan ?? "Dùng chung mọi bộ phận",
                    // Chế độ dự phòng không đọc được bảng định biên, nên để trống
                    // mức nền thay vì bịa số — người dùng thấy rõ là chưa cấu hình.
                    HienCoTheoCa = model.DanhSachPhanCa
                        .Where(p => p.NgayLamViec == model.SelectedDate
                            && p.NhanVien?.MaChucVu == x.cv.MaChucVu
                            && p.NhanVien.MaPhongBan == x.MaPhongBan)
                        .GroupBy(p => p.MaCa)
                        .ToDictionary(g => g.Key, g => g.Count())
                }).ToList()
        };
    }
}
