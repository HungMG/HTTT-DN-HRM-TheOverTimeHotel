using OvertimeHotel.HRM.WebAdmin.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Quản lý Nhật ký Chấm công & Quẹt thẻ Check-in / Check-out 24/7
/// Phục vụ theo dõi giờ làm việc thực tế, giao nhận ca và tính toán đi trễ / về sớm / tăng ca OT
/// </summary>
[Authorize]
public class ChamCongController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ChamCongController> _logger;

    public ChamCongController(AppDbContext context, ILogger<ChamCongController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Màn hình danh sách nhật ký chấm công check-in / check-out
    /// Hỗ trợ nạp tại chỗ qua AJAX (In-Place Transition) không reload toàn trang
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? rangeType,
        DateOnly? date,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? phongBanId,
        int? caId,
        string? trangThai,
        string? keyword,
        int page = 1,
        int pageSize = 15,
        bool isPartial = false)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        rangeType = string.IsNullOrWhiteSpace(rangeType) ? "today" : rangeType.ToLowerInvariant();

        DateOnly startDate;
        DateOnly endDate;

        switch (rangeType)
        {
            case "yesterday":
                startDate = today.AddDays(-1);
                endDate = startDate;
                break;
            case "week":
                int diff = (7 + ((int)today.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
                startDate = today.AddDays(-diff);
                endDate = startDate.AddDays(6);
                break;
            case "month":
                startDate = new DateOnly(today.Year, today.Month, 1);
                endDate = startDate.AddMonths(1).AddDays(-1);
                break;
            case "custom":
                startDate = fromDate ?? today;
                endDate = toDate ?? startDate;
                if (endDate < startDate)
                {
                    endDate = startDate;
                }
                break;
            case "today":
            default:
                rangeType = "today";
                startDate = date ?? today;
                endDate = startDate;
                break;
        }

        var model = new ChamCongIndexViewModel
        {
            RangeType = rangeType,
            NgayChon = startDate,
            TuNgay = startDate,
            DenNgay = endDate,
            PhongBanId = phongBanId,
            CaId = caId,
            TrangThai = trangThai,
            Keyword = keyword,
            PageNumber = page,
            PageSize = pageSize
        };

        try
        {
            // 1. Tải danh mục phục vụ bộ lọc
            var phongBans = await this.SearchDataAsync("attendance:departments", () => _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync());
            var caLamViecs = await this.SearchDataAsync("attendance:shifts", () => _context.CaLamViecs.AsNoTracking().OrderBy(c => c.GioBatDau).ToListAsync());

            model.PhongBanOptions = phongBans.Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan.ToString(), p.MaPhongBan == phongBanId)).ToList();
            model.CaOptions = caLamViecs.Select(c => new SelectListItem($"{c.TenCa} ({c.GioBatDau:HH:mm} - {c.GioKetThuc:HH:mm})", c.MaCa.ToString(), c.MaCa == caId)).ToList();

            // 2. Truy vấn danh sách phân ca và chấm công trong khoảng ngày (dùng AsSplitQuery triệt tiêu Cartesian)
            var query = _context.PhanCas.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .Where(p => p.NgayLamViec >= startDate && p.NgayLamViec <= endDate);

            // Quản lý bộ phận chỉ thấy nhân sự của mình: bộ lọc phòng ban do họ
            // chọn bị ghi đè bởi phạm vi quyền, nếu không chỉ cần sửa query string
            // là xem được giờ công của bộ phận khác.
            var scopeDept = GetManagerDepartmentScope();
            if (scopeDept.HasValue)
            {
                query = scopeDept.Value <= 0
                    ? query.Where(_ => false)
                    : query.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == scopeDept.Value);
            }
            else if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                query = query.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phongBanId.Value);
            }

            if (caId.HasValue && caId.Value > 0)
            {
                query = query.Where(p => p.MaCa == caId.Value);
            }

            // One snapshot prevents stale shift IDs from poisoning a freshly loaded event map.
            var snapshot = await this.SearchDataAsync($"attendance:list:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:scope:{scopeDept}:department:{phongBanId}:shift:{caId}", async () =>
            {
                var shifts = await query.OrderByDescending(p => p.NgayLamViec)
                    .ThenBy(p => p.BatDauDuKien).ToListAsync();
                var shiftIds = shifts.Select(p => p.MaPhanCa).ToHashSet();

                if (shiftIds.Count == 0)
                {
                    return (Shifts: shifts, Events: new List<PhanCongDuAn>());
                }

                // Truy vấn sự kiện qua chỉ mục ngày của phân ca thay vì nhồi mảng IN khổng lồ
                var events = await _context.PhanCongDuAns.AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.DuAn).Include(p => p.CongThucTes)
                    .Where(p => p.MaPhanCa.HasValue &&
                                p.PhanCa != null &&
                                p.PhanCa.NgayLamViec >= startDate &&
                                p.PhanCa.NgayLamViec <= endDate &&
                                p.TrangThaiThamGia != "HUY_PHAN_CONG")
                    .ToListAsync();

                var relevantEvents = events.Where(p => shiftIds.Contains(p.MaPhanCa!.Value)).ToList();
                return (Shifts: shifts, Events: relevantEvents);
            });
            var allShifts = snapshot.Shifts;
            var eventAssignments = snapshot.Events;

            var eventByShift = eventAssignments
                .GroupBy(p => p.MaPhanCa!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var nowUtc = DateTimeOffset.UtcNow;

            // 3. Chuyển đổi sang danh sách Item ViewModel và phân tích trạng thái
            var allItems = allShifts.Select(p =>
            {
                var c = p.ChamCong;
                var status = EvaluateShiftAttendanceStatus(p, c, nowUtc);
                eventByShift.TryGetValue(p.MaPhanCa, out var ev);

                decimal? actualEventHours = null;
                if (ev != null && ev.CongThucTes.Count > 0)
                {
                    actualEventHours = Math.Round((decimal)ev.CongThucTes.Sum(ct => (ct.KetThuc - ct.BatDau).TotalHours), 2);
                }

                return new ChamCongItemViewModel
                {
                    MaPhanCa = p.MaPhanCa,
                    MaChamCong = c?.MaChamCong,
                    MaNhanVien = p.MaNhanVien,
                    HoTen = p.NhanVien?.HoTen ?? $"Nhân sự #{p.MaNhanVien}",
                    TenPhongBan = p.NhanVien?.PhongBan?.TenPhongBan ?? "Chưa phân bổ",
                    TenChucVu = p.NhanVien?.ChucVu?.TenChucVu ?? "Nhân viên",
                    NgayLamViec = p.NgayLamViec,
                    MaCa = p.MaCa,
                    TenCa = p.CaLamViec?.TenCa ?? "Ca trực",
                    GioBatDauCa = p.CaLamViec?.GioBatDau ?? TimeOnly.MinValue,
                    GioKetThucCa = p.CaLamViec?.GioKetThuc ?? TimeOnly.MinValue,
                    QuaDem = p.CaLamViec?.QuaDem ?? false,
                    BatDauDuKien = p.BatDauDuKien,
                    KetThucDuKien = p.KetThucDuKien,
                    GioCheckIn = c?.GioCheckIn,
                    GioCheckOut = c?.GioCheckOut,
                    PhutDiTre = c?.PhutDiTre ?? 0,
                    PhutVeSom = c?.PhutVeSom ?? 0,
                    GioTangCaOt = c?.GioTangCaOt ?? 0m,
                    PhuongThucCheckIn = c?.PhuongThucCheckIn,
                    ToaDoCheckIn = c?.ToaDoCheckIn,
                    ThietBiCheckIn = c?.ThietBiCheckIn,
                    TrangThai = status,
                    CoSuKien = ev != null,
                    TenSuKien = ev?.DuAn?.TenDuAn,
                    VaiTroSuKien = ev?.VaiTroDuAn,
                    SoGioSuKienThucTe = actualEventHours
                };
            }).ToList();

            // 4. Tính toán số liệu thống kê KPI trên toàn bộ khoảng ngày
            model.TongSoCaPhan = allItems.Count;
            model.SoDaCheckIn = allItems.Count(i => i.DaCheckIn);
            model.SoDangLamViec = allItems.Count(i => i.DangLamViec);
            model.SoDungGio = allItems.Count(i => i.TrangThai == AttendanceCalculator.STATUS_DUNG_GIO);
            model.SoDiTre = allItems.Count(i => i.TrangThai is AttendanceCalculator.STATUS_DI_TRE or AttendanceCalculator.STATUS_TRE_VA_SOM);
            model.SoVeSom = allItems.Count(i => i.TrangThai is AttendanceCalculator.STATUS_VE_SOM or AttendanceCalculator.STATUS_TRE_VA_SOM);
            model.SoVangMat = allItems.Count(i => i.TrangThai == AttendanceCalculator.STATUS_VANG);

            double totalHours = 0;
            foreach (var item in allItems)
            {
                if (item.SoGioLamThucTe.HasValue)
                {
                    totalHours += item.SoGioLamThucTe.Value;
                }
            }
            model.TongGioLamThucTe = Math.Round((decimal)totalHours, 1);
            model.TongGioOt = allItems.Sum(i => i.GioTangCaOt);
            model.TongGioSuKien = allItems.Sum(i => i.SoGioSuKienThucTe ?? 0m);

            // 5. Lọc theo Keyword và Trạng thái
            var filtered = allItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                filtered = filtered.Where(i =>
                    i.HoTen.ToLowerInvariant().Contains(kw) ||
                    i.MaNhanVienFormat.ToLowerInvariant().Contains(kw) ||
                    i.TenPhongBan.ToLowerInvariant().Contains(kw) ||
                    i.TenCa.ToLowerInvariant().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                switch (trangThai.ToUpperInvariant())
                {
                    case "DUNG_GIO":
                        filtered = filtered.Where(i => i.TrangThai == AttendanceCalculator.STATUS_DUNG_GIO);
                        break;
                    case "DI_TRE":
                        filtered = filtered.Where(i => i.TrangThai is AttendanceCalculator.STATUS_DI_TRE or AttendanceCalculator.STATUS_TRE_VA_SOM);
                        break;
                    case "VE_SOM":
                        filtered = filtered.Where(i => i.TrangThai is AttendanceCalculator.STATUS_VE_SOM or AttendanceCalculator.STATUS_TRE_VA_SOM);
                        break;
                    case "DANG_LAM":
                        filtered = filtered.Where(i => i.DangLamViec);
                        break;
                    case "VANG":
                        filtered = filtered.Where(i => i.TrangThai == AttendanceCalculator.STATUS_VANG);
                        break;
                    case "CHUA_CHECKIN":
                        filtered = filtered.Where(i => i.TrangThai == "CHUA_CHECKIN");
                        break;
                }
            }

            var filteredList = filtered.ToList();
            model.TotalFilteredItems = filteredList.Count;
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalFilteredItems / (double)pageSize));
            model.PageNumber = Math.Clamp(page, 1, model.TotalPages);

            model.Items = filteredList
                .Skip((model.PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (isPartial || Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_AttendanceTablePartial", model);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nạp dữ liệu nhật ký chấm công từ CSDL Supabase.");
            TempData["Error"] = "Lỗi kết nối CSDL Supabase: " + ex.Message;
            return View(model);
        }
    }

    /// <summary>
    /// Bảng tổng hợp công tháng theo từng nhân viên (PRD 4.4).
    ///
    /// <para>
    /// Khác với <see cref="Index"/> — vốn liệt kê từng lượt ca — màn này gộp theo
    /// người để HR chấm công tháng và đối chiếu tỷ lệ chuyên cần trước khi chuyển
    /// sang bảng lương.
    /// </para>
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ViewDepartmentAttendance)]
    public async Task<IActionResult> CongThang(int? nam, int? thang, int? phongBanId)
    {
        var model = await BuildCongThangAsync(nam, thang, phongBanId);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return PartialView("_CongThangTable", model);
        }

        return View(model);
    }

    /// <summary>
    /// Xuất bảng công tháng ra CSV cho bước đối soát lương (PRD 4.4).
    ///
    /// <para>
    /// Dùng chung <see cref="BuildCongThangAsync"/> với màn hình nên tệp xuất luôn
    /// khớp đúng số liệu đang thấy, kể cả phạm vi phòng ban mà Quản lý bị khóa.
    /// UTF-8 kèm BOM để Excel đọc đúng tiếng Việt.
    /// </para>
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ViewDepartmentAttendance)]
    public async Task<IActionResult> XuatCongThang(int? nam, int? thang, int? phongBanId)
    {
        var model = await BuildCongThangAsync(nam, thang, phongBanId);
        var content = BuildCongThangCsv(model);
        return File(content, "text/csv; charset=utf-8", $"CongThang_{model.Thang:00}-{model.Nam}.csv");
    }

    private static byte[] BuildCongThangCsv(CongThangViewModel model)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('﻿');

        sb.AppendLine($"BẢNG TỔNG HỢP CÔNG THÁNG {model.Thang:00}/{model.Nam}");
        sb.AppendLine($"Kỳ đối soát: {model.TuNgay:dd/MM/yyyy} - {model.DenNgay:dd/MM/yyyy}");
        sb.AppendLine();

        sb.AppendLine("Mã NV,Họ tên,Phòng ban,Chức vụ,Ca phân,Đã kết thúc,Đúng giờ,Trễ,Về sớm,Trễ và sớm,Vắng,Chưa quẹt,Phút trễ,Phút về sớm,Giờ OT,Giờ thực tế,Chuyên cần (%)");

        foreach (var row in model.Report.Rows)
        {
            // Bọc mỗi ô trong ngoặc kép và nhân đôi dấu nháy — tên phòng ban có
            // thể chứa dấu phẩy, nếu không bọc Excel sẽ tách sai cột.
            static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

            sb.AppendLine(string.Join(',',
                Csv(row.MaNhanVienFormat),
                Csv(row.EmployeeName),
                Csv(row.DepartmentName),
                Csv(row.PositionName),
                row.TongCaPhanCong,
                row.SoCaDaKetThuc,
                row.SoCaDungGio,
                row.SoCaDiTre,
                row.SoCaVeSom,
                row.SoCaTreVaSom,
                row.SoCaVangMat,
                row.SoCaChuaCheckIn,
                row.TongPhutTre,
                row.TongPhutVeSom,
                row.TongGioOt.ToString("0.##"),
                row.TongGioLamThucTe.ToString("0.##"),
                row.TyLeChuyenCan.ToString("0.##")));
        }

        sb.AppendLine();
        sb.AppendLine("TỔNG CỘNG,"
            + $"{model.Report.TongNhanVien} nhân viên,"
            + $",,"
            + $"{model.Report.TongCaPhanCong},"
            + $"{model.Report.TongCaDaKetThuc},"
            + $"{model.Report.TongCaDungGio},"
            + $",,,,"
            + $"{model.Report.TongCaVangMat},"
            + $","
            + $"{model.Report.TongPhutTre},"
            + $"{model.Report.TongPhutVeSom},"
            + $"{model.Report.TongGioOt:0.##},"
            + $"{model.Report.TongGioLamThucTe:0.##},"
            + $"{model.Report.TyLeChuyenCan:0.##}");

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Dựng số liệu tổng hợp công tháng cho kỳ và phòng ban đang chọn.
    /// Dùng chung cho màn hình, phần AJAX và tệp xuất CSV.
    /// </summary>
    private async Task<CongThangViewModel> BuildCongThangAsync(int? nam, int? thang, int? phongBanId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var namChon = nam is >= 2000 and <= 2100 ? nam.Value : today.Year;
        var thangChon = thang is >= 1 and <= 12 ? thang.Value : today.Month;

        // Kẹp về tháng hiện tại: bảng công tương lai chưa có gì để tổng hợp và
        // chỉ làm người dùng tưởng hệ thống mất dữ liệu.
        var thangHienTai = new DateOnly(today.Year, today.Month, 1);
        var thangDau = new DateOnly(namChon, thangChon, 1);
        if (thangDau > thangHienTai)
        {
            namChon = today.Year;
            thangChon = today.Month;
            thangDau = thangHienTai;
        }

        var tuNgay = thangDau;
        var denNgay = thangDau.AddMonths(1).AddDays(-1);

        var scopeDept = GetManagerDepartmentScope();
        var phongBanLoc = scopeDept ?? (phongBanId is > 0 ? phongBanId : null);

        var model = new CongThangViewModel
        {
            Nam = namChon,
            Thang = thangChon,
            PhongBanId = phongBanLoc,
            TuNgay = tuNgay,
            DenNgay = denNgay,
            // Quản lý bộ phận đã bị khóa vào phòng ban của mình ở trên; chỉ người
            // có phạm vi toàn khách sạn mới được đổi bộ lọc phòng ban.
            CoQuyenLocPhongBan = !scopeDept.HasValue
        };

        try
        {
            var phongBans = await _context.PhongBans.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .ToListAsync();

            model.PhongBanOptions = phongBans
                .Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan.ToString(), p.MaPhongBan == phongBanLoc))
                .ToList();

            model.ThangOptions = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem($"Tháng {m:00}", m.ToString(), m == thangChon))
                .ToList();

            model.NamOptions = Enumerable.Range(today.Year - 3, 4)
                .Reverse()
                .Select(y => new SelectListItem($"Năm {y}", y.ToString(), y == namChon))
                .ToList();

            // Chiếu thẳng ra DTO thay vì Include navigation: câu truy vấn chỉ lấy
            // đúng các cột cần tính, không kéo theo cả entity nhân viên.
            var shiftQuery = _context.PhanCas
                .AsNoTracking()
                .Where(p => p.NgayLamViec >= tuNgay && p.NgayLamViec <= denNgay);

            if (phongBanLoc.HasValue)
            {
                shiftQuery = shiftQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phongBanLoc.Value);
            }

            var shifts = await shiftQuery
                .Select(p => new MonthlyAttendanceShiftInput(
                    p.MaNhanVien,
                    p.NgayLamViec,
                    p.BatDauDuKien,
                    p.KetThucDuKien,
                    p.TrangThai,
                    p.ChamCong == null ? null : p.ChamCong.GioCheckIn,
                    p.ChamCong == null ? null : p.ChamCong.GioCheckOut,
                    p.ChamCong == null ? 0 : p.ChamCong.PhutDiTre,
                    p.ChamCong == null ? 0 : p.ChamCong.PhutVeSom,
                    p.ChamCong == null ? 0m : p.ChamCong.GioTangCaOt,
                    p.ChamCong == null ? null : p.ChamCong.TrangThai,
                    // Giờ làm chuẩn của ca để chặn phần vượt khung do nghỉ trưa —
                    // ca hành chính 07:00–17:00 quẹt đủ là 10 tiếng trên đồng hồ
                    // nhưng chỉ 8 tiếng được tính công.
                    p.CaLamViec == null ? null : (decimal?)p.CaLamViec.GioLamChuan))
                .ToListAsync();

            var employeeIds = shifts.Select(s => s.EmployeeId).Distinct().ToList();

            var employees = await _context.NhanViens
                .AsNoTracking()
                .Where(n => employeeIds.Contains(n.MaNhanVien))
                .Select(n => new
                {
                    n.MaNhanVien,
                    HoTen = (n.Ho + " " + n.Ten).Trim(),
                    PhongBan = n.PhongBan == null ? null : n.PhongBan.TenPhongBan,
                    ChucVu = n.ChucVu == null ? null : n.ChucVu.TenChucVu
                })
                .ToDictionaryAsync(
                    n => n.MaNhanVien,
                    n => (n.HoTen, n.PhongBan, n.ChucVu));

            model.Report = MonthlyAttendanceCalculator.Calculate(
                tuNgay, denNgay, shifts, employees, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tổng hợp công tháng {Month}/{Year}.", thangChon, namChon);
            TempData["Error"] = "Lỗi kết nối CSDL Supabase: " + ex.Message;
        }

        return model;
    }

    /// <summary>
    /// API trả về chi tiết 1 bản ghi chấm công dạng JSON phục vụ Modal chi tiết
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ChiTiet(int maPhanCa)
    {
        var phanCa = await _context.PhanCas.AsNoTracking()
            .Include(p => p.NhanVien)
                .ThenInclude(n => n!.PhongBan)
            .Include(p => p.NhanVien)
                .ThenInclude(n => n!.ChucVu)
            .Include(p => p.CaLamViec)
            .Include(p => p.ChamCong)
            .FirstOrDefaultAsync(p => p.MaPhanCa == maPhanCa);

        if (phanCa == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy thông tin ca trực yêu cầu." });
        }

        var c = phanCa.ChamCong;
        var nowUtc = DateTimeOffset.UtcNow;
        var status = EvaluateShiftAttendanceStatus(phanCa, c, nowUtc);

        var vnOffset = TimeSpan.FromHours(7);
        var checkInDisplay = c?.GioCheckIn?.ToOffset(vnOffset).ToString("HH:mm:ss dd/MM/yyyy") ?? "Chưa check-in";
        var checkOutDisplay = c?.GioCheckOut?.ToOffset(vnOffset).ToString("HH:mm:ss dd/MM/yyyy") ?? (c?.GioCheckIn != null ? "Đang trực ca (Chưa check-out)" : "Chưa check-out");

        double totalHours = 0;
        if (c?.GioCheckIn != null && c?.GioCheckOut != null)
        {
            totalHours = Math.Max(0, Math.Round((c.GioCheckOut.Value - c.GioCheckIn.Value).TotalHours, 2));
        }

        return Json(new
        {
            success = true,
            data = new
            {
                maPhanCa = phanCa.MaPhanCa,
                maChamCong = c?.MaChamCong,
                maNhanVien = phanCa.MaNhanVien,
                hoTen = phanCa.NhanVien?.HoTen ?? $"NV #{phanCa.MaNhanVien}",
                maNhanVienFormat = $"NV-{phanCa.MaNhanVien}",
                tenPhongBan = phanCa.NhanVien?.PhongBan?.TenPhongBan ?? "—",
                tenChucVu = phanCa.NhanVien?.ChucVu?.TenChucVu ?? "—",
                tenCa = phanCa.CaLamViec?.TenCa ?? "Ca trực",
                ngayLamViec = phanCa.NgayLamViec.ToString("dd/MM/yyyy"),
                gioBatDauCa = phanCa.CaLamViec?.GioBatDau.ToString("HH:mm") ?? "—",
                gioKetThucCa = phanCa.CaLamViec?.GioKetThuc.ToString("HH:mm") ?? "—",
                batDauDuKien = phanCa.BatDauDuKien.ToOffset(vnOffset).ToString("HH:mm:ss dd/MM/yyyy"),
                ketThucDuKien = phanCa.KetThucDuKien.ToOffset(vnOffset).ToString("HH:mm:ss dd/MM/yyyy"),
                gioCheckIn = checkInDisplay,
                gioCheckOut = checkOutDisplay,
                phutDiTre = c?.PhutDiTre ?? 0,
                phutVeSom = c?.PhutVeSom ?? 0,
                gioTangCaOt = c?.GioTangCaOt ?? 0m,
                soGioLamThucTe = totalHours,
                phuongThucCheckIn = c?.PhuongThucCheckIn ?? "Không xác định",
                toaDoCheckIn = c?.ToaDoCheckIn ?? "Không có dữ liệu GPS",
                thietBiCheckIn = c?.ThietBiCheckIn ?? "Không xác định",
                trangThai = status
            }
        });
    }

    /// <summary>
    /// Đánh giá trạng thái chấm công dựa trên thông tin ca và dữ liệu quẹt thẻ thực tế
    /// </summary>
    private static string EvaluateShiftAttendanceStatus(PhanCa p, ChamCong? c, DateTimeOffset nowUtc)
    {
        if (c != null && !string.IsNullOrEmpty(c.TrangThai))
        {
            if (c.GioCheckIn.HasValue && !c.GioCheckOut.HasValue)
            {
                return "DANG_LAM";
            }
            return c.TrangThai;
        }

        if (c != null && c.GioCheckIn.HasValue)
        {
            if (!c.GioCheckOut.HasValue)
            {
                return "DANG_LAM";
            }
            return AttendanceCalculator.STATUS_DUNG_GIO;
        }

        // Chưa có dữ liệu chấm công
        if (p.KetThucDuKien < nowUtc)
        {
            return AttendanceCalculator.STATUS_VANG;
        }

        if (p.BatDauDuKien <= nowUtc && nowUtc <= p.KetThucDuKien)
        {
            return "CHUA_CHECKIN";
        }

        return "CHUA_DEN_GIO";
    }

    /// <summary>
    /// Bộ phận mà Quản lý bị giới hạn trong đó. <c>null</c> = không giới hạn
    /// (HR/Admin/IT nhìn toàn khách sạn). <c>-1</c> = Manager nhưng thiếu claim
    /// phòng ban ⇒ không được thấy bộ phận nào.
    /// </summary>
    private int? GetManagerDepartmentScope() => User.GetManagedDepartmentScope();
}
