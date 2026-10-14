using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
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
            var phongBans = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
            var caLamViecs = await _context.CaLamViecs.AsNoTracking().OrderBy(c => c.GioBatDau).ToListAsync();

            model.PhongBanOptions = phongBans.Select(p => new SelectListItem(p.TenPhongBan, p.MaPhongBan.ToString(), p.MaPhongBan == phongBanId)).ToList();
            model.CaOptions = caLamViecs.Select(c => new SelectListItem($"{c.TenCa} ({c.GioBatDau:HH:mm} - {c.GioKetThuc:HH:mm})", c.MaCa.ToString(), c.MaCa == caId)).ToList();

            // 2. Truy vấn danh sách phân ca và chấm công trong khoảng ngày
            var query = _context.PhanCas.AsNoTracking()
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
                .Include(p => p.CaLamViec)
                .Include(p => p.ChamCong)
                .Where(p => p.NgayLamViec >= startDate && p.NgayLamViec <= endDate);

            if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                query = query.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phongBanId.Value);
            }

            if (caId.HasValue && caId.Value > 0)
            {
                query = query.Where(p => p.MaCa == caId.Value);
            }

            var allShifts = await query
                .OrderByDescending(p => p.NgayLamViec)
                .ThenBy(p => p.BatDauDuKien)
                .ToListAsync();

            var nowUtc = DateTimeOffset.UtcNow;

            // 3. Chuyển đổi sang danh sách Item ViewModel và phân tích trạng thái
            var allItems = allShifts.Select(p =>
            {
                var c = p.ChamCong;
                var status = EvaluateShiftAttendanceStatus(p, c, nowUtc);

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
                    TrangThai = status
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
}
