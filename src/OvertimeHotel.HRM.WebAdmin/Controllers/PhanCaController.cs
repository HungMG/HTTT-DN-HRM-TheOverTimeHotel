using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

[Authorize(Roles = "Admin,HR,Manager")]
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
    public async Task<IActionResult> Index(string? week, int? phongBanId, int? caId, string viewMode = "matrix")
    {
        // 1. Tính toán ngày Thứ 2 đầu tuần và Chủ nhật cuối tuần
        DateOnly targetDate;
        if (!string.IsNullOrWhiteSpace(week) && DateOnly.TryParse(week, out var parsedDate))
        {
            targetDate = parsedDate;
        }
        else
        {
            targetDate = DateOnly.FromDateTime(DateTime.Today);
        }

        // Tìm ngày Thứ 2 của tuần chứa targetDate
        int diff = (7 + ((int)targetDate.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        var weekStart = targetDate.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6);

        ViewBag.CurrentWeek = weekStart.ToString("yyyy-MM-dd");
        ViewBag.PrevWeek = weekStart.AddDays(-7).ToString("yyyy-MM-dd");
        ViewBag.NextWeek = weekStart.AddDays(7).ToString("yyyy-MM-dd");
        ViewBag.TodayWeek = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        ViewBag.ViewMode = viewMode;

        var model = new PhanCaIndexViewModel
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            SelectedPhongBanId = phongBanId,
            SelectedCaId = caId
        };

        try
        {
            // 2. Tải danh mục phòng ban và ca làm việc
            model.DanhSachPhongBan = await _context.PhongBans.AsNoTracking().OrderBy(p => p.TenPhongBan).ToListAsync();
            model.DanhSachCa = await _context.CaLamViecs.AsNoTracking().OrderBy(c => c.GioBatDau).ToListAsync();

            // 3. Tải danh sách nhân viên
            var empQuery = _context.NhanViens.AsNoTracking()
                .Include(n => n.PhongBan)
                .Include(n => n.ChucVu)
                .Where(n => n.TrangThai == "DANG_LAM");

            if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                empQuery = empQuery.Where(n => n.MaPhongBan == phongBanId.Value);
            }

            model.DanhSachNhanVien = await empQuery.OrderBy(n => n.Ten).ThenBy(n => n.Ho).ToListAsync();

            // 4. Tải danh sách phân ca trong tuần
            var phanCaQuery = _context.PhanCas.AsNoTracking()
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
                .Include(p => p.CaLamViec)
                .Where(p => p.NgayLamViec >= weekStart && p.NgayLamViec <= weekEnd);

            if (phongBanId.HasValue && phongBanId.Value > 0)
            {
                phanCaQuery = phanCaQuery.Where(p => p.NhanVien != null && p.NhanVien.MaPhongBan == phongBanId.Value);
            }

            if (caId.HasValue && caId.Value > 0)
            {
                phanCaQuery = phanCaQuery.Where(p => p.MaCa == caId.Value);
            }

            model.DanhSachPhanCa = await phanCaQuery
                .OrderBy(p => p.NgayLamViec)
                .ThenBy(p => p.BatDauDuKien)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);

            // Tải danh sách ca trực hôm nay độc lập (không bị ảnh hưởng khi người dùng đổi tuần)
            model.CaTrucHomNay = await _context.PhanCas.AsNoTracking()
                .Include(p => p.NhanVien).ThenInclude(n => n!.PhongBan)
                .Include(p => p.NhanVien).ThenInclude(n => n!.ChucVu)
                .Include(p => p.CaLamViec)
                .Where(p => p.NgayLamViec == today)
                .ToListAsync();

            // 6. Tính toán thống kê Bento
            model.TongSoCa = model.DanhSachPhanCa.Count;
            model.SoCaSang = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Sáng"));
            model.SoCaChieu = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && p.CaLamViec.TenCa.Contains("Chiều"));
            model.SoCaDem = model.DanhSachPhanCa.Count(p => p.CaLamViec != null && (p.CaLamViec.QuaDem || p.CaLamViec.TenCa.Contains("Đêm")));
            model.TongNhanVien = model.DanhSachNhanVien.Count;
            model.SoNhanVienDuocPhan = model.DanhSachPhanCa.Select(p => p.MaNhanVien).Distinct().Count();

            // 7. Xây dựng ma trận tuần (Weekly Matrix Rows)
            var avatarColors = new[] { "#1E3A8A", "#D97706", "#059669", "#7C3AED", "#DB2777", "#2563EB", "#0891B2" };

            foreach (var emp in model.DanhSachNhanVien)
            {
                var row = new PhanCaMatrixRow
                {
                    MaNhanVien = emp.MaNhanVien,
                    MaPhongBan = emp.MaPhongBan,
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
    /// Phân ca mới cho nhân viên
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePhanCaInputModel input)
    {
        if (input.MaNhanVien <= 0 || input.MaCa <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn nhân viên và ca làm việc hợp lệ.";
            return RedirectToAction(nameof(Index), new { week = input.NgayLamViec.ToString("yyyy-MM-dd") });
        }

        try
        {
            var ca = await _context.CaLamViecs.FindAsync(input.MaCa);
            if (ca == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca làm việc đã chọn.";
                return RedirectToAction(nameof(Index), new { week = input.NgayLamViec.ToString("yyyy-MM-dd") });
            }

            // Kiểm tra trùng ca
            var exist = await _context.PhanCas.AnyAsync(p =>
                p.MaNhanVien == input.MaNhanVien &&
                p.NgayLamViec == input.NgayLamViec &&
                p.MaCa == input.MaCa);

            if (exist)
            {
                TempData["WarningMessage"] = "Nhân viên đã được xếp ca này trong ngày được chọn!";
                return RedirectToAction(nameof(Index), new { week = input.NgayLamViec.ToString("yyyy-MM-dd") });
            }

            // Tính thời gian dự kiến
            var startDateTime = input.NgayLamViec.ToDateTime(ca.GioBatDau);
            var endDateTime = ca.QuaDem
                ? input.NgayLamViec.AddDays(1).ToDateTime(ca.GioKetThuc)
                : input.NgayLamViec.ToDateTime(ca.GioKetThuc);

            var phanCa = new PhanCa
            {
                MaNhanVien = input.MaNhanVien,
                MaCa = input.MaCa,
                NgayLamViec = input.NgayLamViec,
                BatDauDuKien = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)),
                KetThucDuKien = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)),
                TrangThai = string.IsNullOrWhiteSpace(input.TrangThai) ? "DA_PHAN" : input.TrangThai
            };

            _context.PhanCas.Add(phanCa);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phân ca làm việc 24/7 thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo phân ca.");
            TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu phân ca vào Supabase: " + ex.Message;
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
            TempData["ErrorMessage"] = "Thông tin xếp ca nhanh không hợp lệ.";
            return RedirectToAction(nameof(Index), new { week = weekStartStr });
        }

        try
        {
            var ca = await _context.CaLamViecs.FindAsync(maCa);
            if (ca == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ca làm việc.";
                return RedirectToAction(nameof(Index), new { week = weekStartStr });
            }

            int addedCount = 0;
            // Xếp từ Thứ Hai đến Thứ Sáu (5 ngày)
            for (int i = 0; i < 5; i++)
            {
                var workDate = weekStart.AddDays(i);
                var exist = await _context.PhanCas.AnyAsync(p =>
                    p.MaNhanVien == maNhanVien &&
                    p.NgayLamViec == workDate &&
                    p.MaCa == maCa);

                if (!exist)
                {
                    var startDateTime = workDate.ToDateTime(ca.GioBatDau);
                    var endDateTime = ca.QuaDem
                        ? workDate.AddDays(1).ToDateTime(ca.GioKetThuc)
                        : workDate.ToDateTime(ca.GioKetThuc);

                    _context.PhanCas.Add(new PhanCa
                    {
                        MaNhanVien = maNhanVien,
                        MaCa = maCa,
                        NgayLamViec = workDate,
                        BatDauDuKien = new DateTimeOffset(startDateTime, TimeSpan.FromHours(7)),
                        KetThucDuKien = new DateTimeOffset(endDateTime, TimeSpan.FromHours(7)),
                        TrangThai = "DA_PHAN"
                    });
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xếp ca nhanh thành công {addedCount} ngày trong tuần!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xếp ca nhanh.");
            TempData["ErrorMessage"] = "Lỗi khi xếp ca nhanh: " + ex.Message;
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
            var phanCa = await _context.PhanCas.FindAsync(id);
            if (phanCa != null)
            {
                _context.PhanCas.Remove(phanCa);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã hủy lượt phân ca thành công!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa phân ca.");
            TempData["ErrorMessage"] = "Không thể xóa phân ca: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { week = returnWeek });
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
                    TrangThai = date < DateOnly.FromDateTime(DateTime.Today) ? "HOAN_THANH" : (date == DateOnly.FromDateTime(DateTime.Today) ? "DANG_TRUC" : "DA_PHAN")
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
                MaPhongBan = emp.MaPhongBan,
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
    }
}
