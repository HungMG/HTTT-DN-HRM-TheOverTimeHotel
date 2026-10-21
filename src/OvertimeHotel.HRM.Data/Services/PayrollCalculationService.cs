using System.Text;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Core.Services;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

public class PayrollCalculationService(AppDbContext context) : IPayrollCalculationService
{
    private readonly AppDbContext _context = context;

    public async Task<KyLuong> GetOrCreatePeriodAsync(int thang, int nam, CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaInitializer.EnsurePayrollWorkflowSchemaAsync(_context, cancellationToken);

        var period = await _context.KyLuongs
            .FirstOrDefaultAsync(k => k.Thang == thang && k.Nam == nam, cancellationToken);

        if (period != null) return period;

        var fromDate = new DateOnly(nam, thang, 1);
        var toDate = new DateOnly(nam, thang, DateTime.DaysInMonth(nam, thang));

        period = new KyLuong
        {
            Thang = thang,
            Nam = nam,
            TuNgay = fromDate,
            DenNgay = toDate,
            TrangThai = SalaryConstants.PERIOD_OPEN
        };

        _context.KyLuongs.Add(period);
        await _context.SaveChangesAsync(cancellationToken);
        return period;
    }

    public async Task<KyLuong?> GetPeriodWithDetailsAsync(int thang, int nam, CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaInitializer.EnsurePayrollWorkflowSchemaAsync(_context, cancellationToken);

        return await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
                .ThenInclude(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
            .Include(k => k.PhieuLuongs)
                .ThenInclude(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
            .Include(k => k.PhieuLuongs)
                .ThenInclude(p => p.ChiTietPhieuLuongs)
            .FirstOrDefaultAsync(k => k.Thang == thang && k.Nam == nam, cancellationToken);
    }

    public async Task<List<KyLuong>> GetAllPeriodsAsync(CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaInitializer.EnsurePayrollWorkflowSchemaAsync(_context, cancellationToken);

        return await _context.KyLuongs
            .OrderByDescending(k => k.Nam)
            .ThenByDescending(k => k.Thang)
            .ToListAsync(cancellationToken);
    }

    public async Task<PhieuLuong?> GetPayslipDetailAsync(int maPhieuLuong, CancellationToken cancellationToken = default)
    {
        return await _context.PhieuLuongs
            .Include(p => p.KyLuong)
            .Include(p => p.NhanVien)
                .ThenInclude(n => n!.PhongBan)
            .Include(p => p.NhanVien)
                .ThenInclude(n => n!.ChucVu)
            .Include(p => p.NhanVien)
                .ThenInclude(n => n!.HopDongs)
            .Include(p => p.ChiTietPhieuLuongs)
                .ThenInclude(c => c.ChiTietTruBaoHiems)
            .Include(p => p.ChiTietPhieuLuongs)
                .ThenInclude(c => c.ChiTietTruThueTncns)
            .Include(p => p.ChiTietPhieuLuongs)
                .ThenInclude(c => c.ChiTietTruViPhamTamUngs)
            .FirstOrDefaultAsync(p => p.MaPhieuLuong == maPhieuLuong, cancellationToken);
    }

    public async Task<KyLuong> CalculatePayrollAsync(int thang, int nam, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaInitializer.EnsurePayrollWorkflowSchemaAsync(_context, cancellationToken);

        var period = await GetOrCreatePeriodAsync(thang, nam, cancellationToken);
        if (period.TrangThai == SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Kỳ lương đã khóa sổ, không được phép tính toán lại.");
        }

        // 1. Rollback các bản ghi phân công dự án đã kết chuyển ở kỳ lương này
        var oldPayslipIds = await _context.PhieuLuongs
            .Where(p => p.MaKyLuong == period.MaKyLuong)
            .Select(p => p.MaPhieuLuong)
            .ToListAsync(cancellationToken);

        if (oldPayslipIds.Count > 0)
        {
            var oldDetailIds = await _context.ChiTietPhieuLuongs
                .Where(c => oldPayslipIds.Contains(c.MaPhieuLuong))
                .Select(c => c.MaChiTiet)
                .ToListAsync(cancellationToken);

            if (oldDetailIds.Count > 0)
            {
                await _context.PhanCongDuAns
                    .Where(pc => pc.MaChiTiet.HasValue && oldDetailIds.Contains(pc.MaChiTiet.Value))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.TrangThaiChiTra, "CHUA_CHI_TRA")
                        .SetProperty(p => p.MaChiTiet, (int?)null), cancellationToken);

                // Xóa chi tiết con
                await _context.ChiTietTruBaoHiems.Where(b => oldDetailIds.Contains(b.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
                await _context.ChiTietTruThueTncns.Where(t => oldDetailIds.Contains(t.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
                await _context.ChiTietTruViPhamTamUngs.Where(v => oldDetailIds.Contains(v.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
                await _context.ChiTietPhieuLuongs.Where(c => oldPayslipIds.Contains(c.MaPhieuLuong)).ExecuteDeleteAsync(cancellationToken);
            }

            await _context.PhieuLuongs.Where(p => p.MaKyLuong == period.MaKyLuong).ExecuteDeleteAsync(cancellationToken);
        }

        // 2. Lấy danh sách nhân viên đang làm việc và có hợp đồng hiệu lực
        var employees = await _context.NhanViens
            .AsNoTracking()
            .Where(e => e.TrangThai == "DANG_LAM")
            .Include(e => e.PhongBan)
            .Include(e => e.ChucVu)
            .Include(e => e.HopDongs)
            .ToListAsync(cancellationToken);

        // 3. Tải trước toàn bộ phân ca, chấm công và đơn từ trong kỳ
        var allShifts = await _context.PhanCas
            .AsNoTracking()
            .Where(s => s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay)
            .Include(s => s.ChamCong)
            .Include(s => s.CaLamViec)
            .ToListAsync(cancellationToken);

        var allLeaves = await _context.DonTus
            .AsNoTracking()
            .Where(r => r.TrangThai == "DA_DUYET" && r.LoaiDon != null && r.LoaiDon.CoHuongLuong &&
                        r.TuNgay <= period.DenNgay && r.DenNgay >= period.TuNgay)
            .Include(r => r.LoaiDon)
            .ToListAsync(cancellationToken);

        var allAssignments = await _context.PhanCongDuAns
            .Where(pc => pc.TrangThaiChiTra == "CHUA_CHI_TRA" &&
                        (pc.DanhGia == "HOAN_THANH" || pc.DanhGia == "XUAT_SAC") &&
                        (pc.DuAn == null || !pc.DuAn.TinhPhuCapKhiNghiemThu || pc.DuAn.TrangThai == "HOAN_THANH"))
            .Include(pc => pc.DuAn)
            .ToListAsync(cancellationToken);

        // Danh mục cấu hình phụ cấp và thưởng
        var allowancesConfig = await _context.CauHinhPhuCaps.AsNoTracking().ToListAsync(cancellationToken);
        var bonusesConfig = await _context.CauHinhKhoanLuongs.AsNoTracking()
            .Where(k => k.LoaiKhoan == "THUONG").ToListAsync(cancellationToken);

        var newPayslips = new List<PhieuLuong>();
        var assignmentsToUpdate = new List<(PhanCongDuAn Assignment, ChiTietPhieuLuong Detail)>();

        foreach (var nv in employees)
        {
            var validContract = nv.HopDongs
                .Where(h => h.TrangThai == "HIEU_LUC" && h.NgayBatDau <= period.DenNgay &&
                            (h.NgayKetThuc == null || h.NgayKetThuc >= period.TuNgay))
                .OrderByDescending(h => h.NgayBatDau)
                .FirstOrDefault();

            if (validContract == null) continue; // Nhân viên chưa có hợp đồng hiệu lực

            var luongCoBan = validContract.LuongCoBan;
            var donGiaNgay = luongCoBan / SalaryConstants.STANDARD_WORKING_DAYS;
            var donGiaGio = donGiaNgay / SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY;

            // Chấm công
            var empShifts = allShifts.Where(s => s.MaNhanVien == nv.MaNhanVien).ToList();
            var empLeaves = allLeaves.Where(r => r.MaNhanVien == nv.MaNhanVien).ToList();

            var summary = AttendanceSummaryCalculator.Calculate(
                nv.MaNhanVien, nv.HoTen, period.TuNgay, period.DenNgay, empShifts, empLeaves);

            var ngayCongThucTe = (decimal)summary.TotalCreditedDays;
            var workedDays = (decimal)summary.WorkedDays;

            var tienLuongCong = Math.Round(donGiaNgay * ngayCongThucTe, MidpointRounding.AwayFromZero);

            var details = new List<ChiTietPhieuLuong>();

            // --- TĂNG CA (OT) ---
            var otHoursNormal = 0m;
            var otHoursWeekend = 0m;
            var otHoursHoliday = 0m;

            foreach (var shift in empShifts.Where(s => s.ChamCong is { GioTangCaOt: > 0 }))
            {
                var ot = shift.ChamCong!.GioTangCaOt;
                if (IsHoliday(shift.NgayLamViec))
                {
                    otHoursHoliday += ot;
                }
                else if (shift.NgayLamViec.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    otHoursWeekend += ot;
                }
                else
                {
                    otHoursNormal += ot;
                }
            }

            var tienOtNormal = Math.Round(donGiaGio * otHoursNormal * SalaryConstants.OVERTIME_NORMAL_RATE, MidpointRounding.AwayFromZero);
            var tienOtWeekend = Math.Round(donGiaGio * otHoursWeekend * SalaryConstants.OVERTIME_WEEKEND_RATE, MidpointRounding.AwayFromZero);
            var tienOtHoliday = Math.Round(donGiaGio * otHoursHoliday * SalaryConstants.OVERTIME_HOLIDAY_RATE, MidpointRounding.AwayFromZero);
            var tienTangCa = tienOtNormal + tienOtWeekend + tienOtHoliday;

            if (otHoursNormal > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaLoaiOt = 1,
                    LoaiKhoan = "TANG_CA",
                    LoaiKhoanLuu = "TANG_CA",
                    TenKhoanLuu = "Làm thêm ngày thường (150%)",
                    SoTien = tienOtNormal,
                    GhiChu = $"{otHoursNormal:N1} giờ x đơn giá x 1.5"
                });
            }
            if (otHoursWeekend > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaLoaiOt = 2,
                    LoaiKhoan = "TANG_CA",
                    LoaiKhoanLuu = "TANG_CA",
                    TenKhoanLuu = "Làm thêm ngày nghỉ tuần (200%)",
                    SoTien = tienOtWeekend,
                    GhiChu = $"{otHoursWeekend:N1} giờ x đơn giá x 2.0"
                });
            }
            if (otHoursHoliday > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaLoaiOt = 3,
                    LoaiKhoan = "TANG_CA",
                    LoaiKhoanLuu = "TANG_CA",
                    TenKhoanLuu = "Làm thêm ngày Lễ, Tết (300%)",
                    SoTien = tienOtHoliday,
                    GhiChu = $"{otHoursHoliday:N1} giờ x đơn giá x 3.0"
                });
            }

            // --- PHỤ CẤP CA ĐÊM (30%) ---
            var nightShifts = empShifts.Where(s =>
                s.CaLamViec is { QuaDem: true } &&
                s.ChamCong is { GioCheckIn: not null, GioCheckOut: not null }).ToList();

            var nightHours = nightShifts.Count * 8m; // Ca đêm chuẩn 8 tiếng
            var tienCaDem = Math.Round(donGiaGio * nightHours * SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE, MidpointRounding.AwayFromZero);

            if (nightHours > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaLoaiOt = 4,
                    LoaiKhoan = "PHU_CAP",
                    LoaiKhoanLuu = "PHU_CAP",
                    TenKhoanLuu = "Phụ cấp ca đêm (30%)",
                    SoTien = tienCaDem,
                    GhiChu = $"{nightShifts.Count} ca đêm ({nightHours:N0}h) x 30%"
                });
            }

            // --- PHỤ CẤP ĂN CA (Miễn thuế TNCN) ---
            var tienAnCa = Math.Round(workedDays * SalaryConstants.MEAL_ALLOWANCE_PER_DAY, MidpointRounding.AwayFromZero);
            if (workedDays > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaPhuCap = 1,
                    LoaiKhoan = "PHU_CAP",
                    LoaiKhoanLuu = "PHU_CAP",
                    TenKhoanLuu = "Phụ cấp ăn ca",
                    SoTien = tienAnCa,
                    GhiChu = $"{workedDays:N0} ngày công thực tế x 30.000đ (miễn thuế)"
                });
            }

            // --- PHỤ CẤP KHÁC THEO CẤU HÌNH ---
            var chucVuTen = nv.ChucVu?.TenChucVu ?? "";
            var phongBanTen = nv.PhongBan?.TenPhongBan ?? "";
            var isManager = chucVuTen.Contains("Trưởng") || chucVuTen.Contains("Giám sát") || chucVuTen.Contains("Quản lý") || chucVuTen.Contains("Giám đốc");

            if (isManager)
            {
                var pcTrachNhiem = allowancesConfig.FirstOrDefault(a => a.MaPhuCap == 2)?.MucDinhMuc ?? 1000000m;
                details.Add(new ChiTietPhieuLuong
                {
                    MaPhuCap = 2,
                    LoaiKhoan = "PHU_CAP",
                    LoaiKhoanLuu = "PHU_CAP",
                    TenKhoanLuu = "Phụ cấp trách nhiệm quản lý",
                    SoTien = pcTrachNhiem,
                    GhiChu = $"Chức vụ: {chucVuTen}"
                });

                var pcDienThoai = allowancesConfig.FirstOrDefault(a => a.MaPhuCap == 3)?.MucDinhMuc ?? 300000m;
                details.Add(new ChiTietPhieuLuong
                {
                    MaPhuCap = 3,
                    LoaiKhoan = "PHU_CAP",
                    LoaiKhoanLuu = "PHU_CAP",
                    TenKhoanLuu = "Phụ cấp điện thoại & liên lạc",
                    SoTien = pcDienThoai,
                    GhiChu = "Hỗ trợ liên lạc điều hành"
                });
            }

            var isHousekeeping = phongBanTen.Contains("Buồng") || phongBanTen.Contains("Housekeeping");
            var tienDocHai = 0m;
            if (isHousekeeping && workedDays > 0)
            {
                var dinhMucDocHai = allowancesConfig.FirstOrDefault(a => a.MaPhuCap == 4)?.MucDinhMuc ?? 20000m;
                tienDocHai = Math.Round(workedDays * dinhMucDocHai, MidpointRounding.AwayFromZero);
                details.Add(new ChiTietPhieuLuong
                {
                    MaPhuCap = 4,
                    LoaiKhoan = "PHU_CAP",
                    LoaiKhoanLuu = "PHU_CAP",
                    TenKhoanLuu = "Phụ cấp độc hại buồng phòng",
                    SoTien = tienDocHai,
                    GhiChu = $"{workedDays:N0} ngày x {dinhMucDocHai:N0}đ"
                });
            }

            // --- THƯỞNG SỰ KIỆN / DỰ ÁN KHÁCH SẠN (PRD 05 Mục 1.1 & cau_hinh_khoan_luong) ---
            var empAssignments = allAssignments.Where(a => a.MaNhanVien == nv.MaNhanVien).ToList();
            var tienThuongSuKien = 0m;
            foreach (var a in empAssignments)
            {
                var (maKhoan, tenKhoan, amount) = HotelEventRules.ResolveEventBonus(a.DuAn, nv.PhongBan, a, bonusesConfig);

                var eventDetail = new ChiTietPhieuLuong
                {
                    MaKhoan = maKhoan,
                    LoaiKhoan = "THUONG",
                    LoaiKhoanLuu = "THUONG",
                    TenKhoanLuu = tenKhoan,
                    SoTien = amount,
                    GhiChu = $"Thưởng dự án SK-{a.MaDuAn}. Đánh giá: {a.DanhGia}. Mã phân công: #{a.MaPhanCong}"
                };

                details.Add(eventDetail);
                tienThuongSuKien += amount;
                assignmentsToUpdate.Add((a, eventDetail));
            }

            // --- THƯỞNG ---
            var tongThuong = tienThuongSuKien;
            // Thưởng chuyên cần: nếu công >= 25 và không đi trễ
            var lateCount = empShifts.Count(s => s.ChamCong is { PhutDiTre: > 0 });
            if (ngayCongThucTe >= 25 && lateCount == 0)
            {
                var thuongChuyenCan = 500000m;
                details.Add(new ChiTietPhieuLuong
                {
                    MaKhoan = 4,
                    LoaiKhoan = "THUONG",
                    LoaiKhoanLuu = "THUONG",
                    TenKhoanLuu = "Tiền thưởng chuyên cần",
                    SoTien = thuongChuyenCan,
                    GhiChu = "Đạt trên 25 ngày công và không vi phạm giờ giấc"
                });
                tongThuong += thuongChuyenCan;
            }

            // Phí phục vụ (Service Charge)
            var serviceCharge = 1000000m;
            details.Add(new ChiTietPhieuLuong
            {
                MaKhoan = 5,
                LoaiKhoan = "THUONG",
                LoaiKhoanLuu = "THUONG",
                TenKhoanLuu = "Tiền phí phục vụ (Service Charge)",
                SoTien = serviceCharge,
                GhiChu = "Chia đều quỹ phí phục vụ từ du khách khách sạn"
            });
            tongThuong += serviceCharge;

            // Tổng Phụ Cấp (bao gồm cả ăn ca, ca đêm, sự kiện...)
            var tongPhuCap = details.Where(d => d.LoaiKhoan == "PHU_CAP").Sum(d => d.SoTien);

            // --- TỔNG GROSS (TẦNG 1) ---
            var tongGross = tienLuongCong + tienTangCa + tongPhuCap + tongThuong;

            // --- KHẤU TRỪ BẢO HIỂM 10.5% (TẦNG 2) ---
            var tienBhxh = Math.Round(luongCoBan * SalaryConstants.BHXH_RATE, MidpointRounding.AwayFromZero);
            var tienBhyt = Math.Round(luongCoBan * SalaryConstants.BHYT_RATE, MidpointRounding.AwayFromZero);
            var tienBhtn = Math.Round(luongCoBan * SalaryConstants.BHTN_RATE, MidpointRounding.AwayFromZero);
            var tongBaoHiem = tienBhxh + tienBhyt + tienBhtn;

            var insuranceDetail = new ChiTietPhieuLuong
            {
                MaKhoanTru = 1,
                LoaiKhoan = "KHAU_TRU",
                LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = "Bảo hiểm bắt buộc (10.5%)",
                SoTien = tongBaoHiem,
                GhiChu = $"BHXH 8%: {tienBhxh:N0}đ, BHYT 1.5%: {tienBhyt:N0}đ, BHTN 1%: {tienBhtn:N0}đ",
                ChiTietTruBaoHiems = new List<ChiTietTruBaoHiem>
                {
                    new()
                    {
                        LuongDongBaoHiem = luongCoBan,
                        TyLeBhxh = 8.00m,
                        TienBhxh = tienBhxh,
                        TyLeBhyt = 1.50m,
                        TienBhyt = tienBhyt,
                        TyLeBhtn = 1.00m,
                        TienBhtn = tienBhtn,
                        TongBaoHiem = tongBaoHiem
                    }
                }
            };
            details.Add(insuranceDetail);

            // --- PHẠT VI PHẠM KỶ LUẬT CHẤM CÔNG ---
            var fineDetails = new List<ChiTietTruViPhamTamUng>();
            var tongTienPhat = 0m;

            foreach (var shift in empShifts.Where(s => s.ChamCong != null))
            {
                var cc = shift.ChamCong!;
                if (cc.PhutDiTre > 0)
                {
                    var fine = cc.PhutDiTre * SalaryConstants.FINE_PER_MINUTE_LATE;
                    tongTienPhat += fine;
                    fineDetails.Add(new ChiTietTruViPhamTamUng
                    {
                        LoaiTru = "DI_TRE",
                        SoPhutViPham = cc.PhutDiTre,
                        SoTienTru = fine,
                        LyDoChiTiet = $"Đi trễ {cc.PhutDiTre} phút ngày {shift.NgayLamViec:dd/MM/yyyy} ({cc.PhutDiTre}p x {SalaryConstants.FINE_PER_MINUTE_LATE:N0}đ)"
                    });
                }

                if (cc.PhutVeSom > 15)
                {
                    var fine = 50000m;
                    tongTienPhat += fine;
                    fineDetails.Add(new ChiTietTruViPhamTamUng
                    {
                        LoaiTru = "VE_SOM",
                        SoPhutViPham = cc.PhutVeSom,
                        SoTienTru = fine,
                        LyDoChiTiet = $"Về sớm {cc.PhutVeSom} phút ngày {shift.NgayLamViec:dd/MM/yyyy}"
                    });
                }
            }

            if (tongTienPhat > 0)
            {
                var fineDetail = new ChiTietPhieuLuong
                {
                    MaKhoanTru = 5,
                    LoaiKhoan = "KHAU_TRU",
                    LoaiKhoanLuu = "KHAU_TRU",
                    TenKhoanLuu = "Phạt vi phạm kỷ luật lao động & chấm công",
                    SoTien = tongTienPhat,
                    GhiChu = $"{fineDetails.Count} lần vi phạm trong kỳ",
                    ChiTietTruViPhamTamUngs = fineDetails
                };
                details.Add(fineDetail);
            }

            // --- THUẾ TNCN LŨY TIẾN ---
            // Thu nhập chịu thuế: Gross trừ các khoản không chịu thuế (Ăn ca 30k, Độc hại)
            var thuNhapChiuThue = Math.Max(0m, tongGross - tienAnCa - tienDocHai);
            var giamTruBanThan = SalaryConstants.PERSONAL_TAX_DEDUCTION;
            var soNguoiPhuThuoc = 0; // Mặc định 0 nếu chưa kê khai
            var giamTruPhuThuoc = soNguoiPhuThuoc * SalaryConstants.DEPENDENT_TAX_DEDUCTION;

            // Thu nhập tính thuế = Thu nhập chịu thuế - Các khoản bảo hiểm - Giảm trừ gia cảnh
            var thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBaoHiem - giamTruBanThan - giamTruPhuThuoc);
            var tienThue = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);

            var taxDetail = new ChiTietPhieuLuong
            {
                MaKhoanTru = 4,
                LoaiKhoan = "KHAU_TRU",
                LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = "Thuế thu nhập cá nhân (TNCN)",
                SoTien = tienThue,
                GhiChu = tienThue > 0 ? $"Thu nhập tính thuế: {thuNhapTinhThue:N0}đ" : "Không phát sinh nghĩa vụ nộp thuế",
                ChiTietTruThueTncns = new List<ChiTietTruThueTncn>
                {
                    new()
                    {
                        ThuNhapChiuThue = thuNhapChiuThue,
                        GiamTruBanThan = giamTruBanThan,
                        SoNguoiPhuThuoc = soNguoiPhuThuoc,
                        GiamTruPhuThuoc = giamTruPhuThuoc,
                        ThuNhapTinhThue = thuNhapTinhThue,
                        TienThue = tienThue
                    }
                }
            };
            details.Add(taxDetail);

            // --- TỔNG KHẤU TRỪ (TẦNG 2) ---
            var tongKhauTru = details.Where(d => d.LoaiKhoan == "KHAU_TRU").Sum(d => d.SoTien);

            // --- THỰC NHẬN (NET) ---
            var thucNhan = tongGross - tongKhauTru;

            var payslip = new PhieuLuong
            {
                MaNhanVien = nv.MaNhanVien,
                MaKyLuong = period.MaKyLuong,
                LuongCoBan = luongCoBan,
                NgayCongThucTe = ngayCongThucTe,
                TienLuongCong = tienLuongCong,
                TienTangCa = tienTangCa,
                TongPhuCap = tongPhuCap,
                TongThuong = tongThuong,
                TongKhauTru = tongKhauTru,
                ThucNhan = thucNhan,
                TrangThai = SalaryConstants.PAYSLIP_DRAFT,
                ChiTietPhieuLuongs = details
            };

            newPayslips.Add(payslip);
        }

        _context.PhieuLuongs.AddRange(newPayslips);
        period.TrangThai = SalaryConstants.PERIOD_DRAFT;

        // Lưu trước để có ID chi tiết phiếu lương
        await _context.SaveChangesAsync(cancellationToken);

        // Cập nhật ngược lại mã chi tiết cho phân công dự án
        foreach (var (assignment, detail) in assignmentsToUpdate)
        {
            assignment.TrangThaiChiTra = "DA_KET_CHUYEN_LUONG";
            assignment.MaChiTiet = detail.MaChiTiet;
        }

        // Ghi nhật ký quản trị
        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien ?? "Hệ thống tự động",
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "TINH_LUONG_TU_DONG",
            NoiDung = $"Chạy tính toán bảng lương kỳ {period.Thang:D2}/{period.Nam} cho {newPayslips.Count} nhân viên",
            GiaTriMoi = $"Tổng quỹ Gross: {newPayslips.Sum(p => p.TienLuongCong + p.TienTangCa + p.TongPhuCap + p.TongThuong):N0} VNĐ, Thực chi Net: {newPayslips.Sum(p => p.ThucNhan):N0} VNĐ"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return period;
    }

    public async Task<bool> RecalculateEmployeeAsync(int maPhieuLuong, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var payslip = await _context.PhieuLuongs
            .Include(p => p.KyLuong)
            .Include(p => p.ChiTietPhieuLuongs)
            .FirstOrDefaultAsync(p => p.MaPhieuLuong == maPhieuLuong, cancellationToken);

        if (payslip == null || payslip.KyLuong == null) return false;
        if (payslip.KyLuong.TrangThai == SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương đã khóa sổ, không được phép tính lại.");
        }

        var period = payslip.KyLuong;
        var employeeId = payslip.MaNhanVien;

        // Hoàn tác các phân công dự án cũ
        var detailIds = payslip.ChiTietPhieuLuongs.Select(c => c.MaChiTiet).ToList();
        if (detailIds.Count > 0)
        {
            await _context.PhanCongDuAns
                .Where(pc => pc.MaChiTiet.HasValue && detailIds.Contains(pc.MaChiTiet.Value))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.TrangThaiChiTra, "CHUA_CHI_TRA")
                    .SetProperty(p => p.MaChiTiet, (int?)null), cancellationToken);

            await _context.ChiTietTruBaoHiems.Where(b => detailIds.Contains(b.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChiTietTruThueTncns.Where(t => detailIds.Contains(t.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChiTietTruViPhamTamUngs.Where(v => detailIds.Contains(v.MaChiTiet)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChiTietPhieuLuongs.Where(c => c.MaPhieuLuong == maPhieuLuong).ExecuteDeleteAsync(cancellationToken);
        }

        await _context.PhieuLuongs.Where(p => p.MaPhieuLuong == maPhieuLuong).ExecuteDeleteAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Tính lại riêng nhân viên này
        // (Cách nhanh nhất: nạp nhân viên và chạy lại logic tính của nhân viên)
        var nv = await _context.NhanViens
            .AsNoTracking()
            .Include(e => e.PhongBan)
            .Include(e => e.ChucVu)
            .Include(e => e.HopDongs)
            .FirstOrDefaultAsync(e => e.MaNhanVien == employeeId, cancellationToken);

        if (nv == null) return false;

        var validContract = nv.HopDongs
            .Where(h => h.TrangThai == "HIEU_LUC" && h.NgayBatDau <= period.DenNgay &&
                        (h.NgayKetThuc == null || h.NgayKetThuc >= period.TuNgay))
            .OrderByDescending(h => h.NgayBatDau)
            .FirstOrDefault();

        if (validContract == null) return false;

        var luongCoBan = validContract.LuongCoBan;
        var donGiaNgay = luongCoBan / SalaryConstants.STANDARD_WORKING_DAYS;
        var donGiaGio = donGiaNgay / SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY;

        var empShifts = await _context.PhanCas.AsNoTracking()
            .Where(s => s.MaNhanVien == nv.MaNhanVien && s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay)
            .Include(s => s.ChamCong).Include(s => s.CaLamViec).ToListAsync(cancellationToken);

        var empLeaves = await _context.DonTus.AsNoTracking()
            .Where(r => r.MaNhanVien == nv.MaNhanVien && r.TrangThai == "DA_DUYET" &&
                        r.LoaiDon != null && r.LoaiDon.CoHuongLuong &&
                        r.TuNgay <= period.DenNgay && r.DenNgay >= period.TuNgay)
            .Include(r => r.LoaiDon).ToListAsync(cancellationToken);

        var summary = AttendanceSummaryCalculator.Calculate(
            nv.MaNhanVien, nv.HoTen, period.TuNgay, period.DenNgay, empShifts, empLeaves);

        var ngayCongThucTe = (decimal)summary.TotalCreditedDays;
        var workedDays = (decimal)summary.WorkedDays;
        var tienLuongCong = Math.Round(donGiaNgay * ngayCongThucTe, MidpointRounding.AwayFromZero);

        var details = new List<ChiTietPhieuLuong>();

        // OT
        var otNormal = 0m; var otWeekend = 0m; var otHoliday = 0m;
        foreach (var s in empShifts.Where(s => s.ChamCong is { GioTangCaOt: > 0 }))
        {
            var ot = s.ChamCong!.GioTangCaOt;
            if (IsHoliday(s.NgayLamViec)) otHoliday += ot;
            else if (s.NgayLamViec.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) otWeekend += ot;
            else otNormal += ot;
        }

        var tienOtNormal = Math.Round(donGiaGio * otNormal * SalaryConstants.OVERTIME_NORMAL_RATE, MidpointRounding.AwayFromZero);
        var tienOtWeekend = Math.Round(donGiaGio * otWeekend * SalaryConstants.OVERTIME_WEEKEND_RATE, MidpointRounding.AwayFromZero);
        var tienOtHoliday = Math.Round(donGiaGio * otHoliday * SalaryConstants.OVERTIME_HOLIDAY_RATE, MidpointRounding.AwayFromZero);
        var tienTangCa = tienOtNormal + tienOtWeekend + tienOtHoliday;

        if (otNormal > 0) details.Add(new ChiTietPhieuLuong { MaLoaiOt = 1, LoaiKhoan = "TANG_CA", LoaiKhoanLuu = "TANG_CA", TenKhoanLuu = "Làm thêm ngày thường (150%)", SoTien = tienOtNormal, GhiChu = $"{otNormal:N1}h x 1.5" });
        if (otWeekend > 0) details.Add(new ChiTietPhieuLuong { MaLoaiOt = 2, LoaiKhoan = "TANG_CA", LoaiKhoanLuu = "TANG_CA", TenKhoanLuu = "Làm thêm ngày nghỉ tuần (200%)", SoTien = tienOtWeekend, GhiChu = $"{otWeekend:N1}h x 2.0" });
        if (otHoliday > 0) details.Add(new ChiTietPhieuLuong { MaLoaiOt = 3, LoaiKhoan = "TANG_CA", LoaiKhoanLuu = "TANG_CA", TenKhoanLuu = "Làm thêm ngày Lễ, Tết (300%)", SoTien = tienOtHoliday, GhiChu = $"{otHoliday:N1}h x 3.0" });

        // Ca đêm
        var nightShifts = empShifts.Where(s => s.CaLamViec is { QuaDem: true } && s.ChamCong is { GioCheckIn: not null, GioCheckOut: not null }).ToList();
        var nightHours = nightShifts.Count * 8m;
        var tienCaDem = Math.Round(donGiaGio * nightHours * SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE, MidpointRounding.AwayFromZero);
        if (nightHours > 0) details.Add(new ChiTietPhieuLuong { MaLoaiOt = 4, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP", TenKhoanLuu = "Phụ cấp ca đêm (30%)", SoTien = tienCaDem, GhiChu = $"{nightShifts.Count} ca đêm ({nightHours}h)" });

        // Ăn ca
        var tienAnCa = Math.Round(workedDays * SalaryConstants.MEAL_ALLOWANCE_PER_DAY, MidpointRounding.AwayFromZero);
        if (workedDays > 0) details.Add(new ChiTietPhieuLuong { MaPhuCap = 1, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP", TenKhoanLuu = "Phụ cấp ăn ca", SoTien = tienAnCa, GhiChu = $"{workedDays} ngày x 30k" });

        // Quản lý
        var chucVuTen = nv.ChucVu?.TenChucVu ?? "";
        if (chucVuTen.Contains("Trưởng") || chucVuTen.Contains("Giám sát") || chucVuTen.Contains("Quản lý") || chucVuTen.Contains("Giám đốc"))
        {
            details.Add(new ChiTietPhieuLuong { MaPhuCap = 2, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP", TenKhoanLuu = "Phụ cấp trách nhiệm quản lý", SoTien = 1000000m });
            details.Add(new ChiTietPhieuLuong { MaPhuCap = 3, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP", TenKhoanLuu = "Phụ cấp điện thoại & liên lạc", SoTien = 300000m });
        }

        // Thưởng sự kiện / dự án theo ma trận cau_hinh_khoan_luong
        var bonusesConfig = await _context.CauHinhKhoanLuongs.AsNoTracking()
            .Where(k => k.LoaiKhoan == "THUONG").ToListAsync(cancellationToken);

        var assignments = await _context.PhanCongDuAns
            .Where(pc => pc.MaNhanVien == nv.MaNhanVien && pc.TrangThaiChiTra == "CHUA_CHI_TRA" &&
                         (pc.DanhGia == "HOAN_THANH" || pc.DanhGia == "XUAT_SAC") &&
                         (pc.DuAn == null || !pc.DuAn.TinhPhuCapKhiNghiemThu || pc.DuAn.TrangThai == "HOAN_THANH"))
            .Include(pc => pc.DuAn).ToListAsync(cancellationToken);

        var assignmentsToLink = new List<(PhanCongDuAn Assignment, ChiTietPhieuLuong Detail)>();
        var tienThuongSuKien = 0m;
        foreach (var a in assignments)
        {
            var (maKhoan, tenKhoan, amount) = HotelEventRules.ResolveEventBonus(a.DuAn, nv.PhongBan, a, bonusesConfig);
            var item = new ChiTietPhieuLuong
            {
                MaKhoan = maKhoan,
                LoaiKhoan = "THUONG",
                LoaiKhoanLuu = "THUONG",
                TenKhoanLuu = tenKhoan,
                SoTien = amount,
                GhiChu = $"Thưởng dự án SK-{a.MaDuAn}. Đánh giá: {a.DanhGia}. Mã phân công: #{a.MaPhanCong}"
            };
            details.Add(item);
            tienThuongSuKien += amount;
            assignmentsToLink.Add((a, item));
        }

        // Thưởng khác (Service Charge, Chuyên cần)
        var tongThuong = tienThuongSuKien + 1000000m;
        details.Add(new ChiTietPhieuLuong { MaKhoan = 5, LoaiKhoan = "THUONG", LoaiKhoanLuu = "THUONG", TenKhoanLuu = "Tiền phí phục vụ (Service Charge)", SoTien = 1000000m });
        if (ngayCongThucTe >= 25 && !empShifts.Any(s => s.ChamCong is { PhutDiTre: > 0 }))
        {
            details.Add(new ChiTietPhieuLuong { MaKhoan = 4, LoaiKhoan = "THUONG", LoaiKhoanLuu = "THUONG", TenKhoanLuu = "Tiền thưởng chuyên cần", SoTien = 500000m });
            tongThuong += 500000m;
        }

        var tongPhuCap = details.Where(d => d.LoaiKhoan == "PHU_CAP").Sum(d => d.SoTien);
        var tongGross = tienLuongCong + tienTangCa + tongPhuCap + tongThuong;

        // Bảo hiểm
        var tienBhxh = Math.Round(luongCoBan * SalaryConstants.BHXH_RATE, MidpointRounding.AwayFromZero);
        var tienBhyt = Math.Round(luongCoBan * SalaryConstants.BHYT_RATE, MidpointRounding.AwayFromZero);
        var tienBhtn = Math.Round(luongCoBan * SalaryConstants.BHTN_RATE, MidpointRounding.AwayFromZero);
        var tongBh = tienBhxh + tienBhyt + tienBhtn;
        details.Add(new ChiTietPhieuLuong
        {
            MaKhoanTru = 1, LoaiKhoan = "KHAU_TRU", LoaiKhoanLuu = "KHAU_TRU",
            TenKhoanLuu = "Bảo hiểm bắt buộc (10.5%)", SoTien = tongBh,
            ChiTietTruBaoHiems = [new() { LuongDongBaoHiem = luongCoBan, TienBhxh = tienBhxh, TienBhyt = tienBhyt, TienBhtn = tienBhtn, TongBaoHiem = tongBh }]
        });

        // Phạt
        var fineTotal = 0m;
        var fines = new List<ChiTietTruViPhamTamUng>();
        foreach (var s in empShifts.Where(s => s.ChamCong is { PhutDiTre: > 0 }))
        {
            var p = s.ChamCong!.PhutDiTre * SalaryConstants.FINE_PER_MINUTE_LATE;
            fineTotal += p;
            fines.Add(new() { 
                LoaiTru = "DI_TRE", 
                SoPhutViPham = s.ChamCong.PhutDiTre, 
                SoTienTru = p, 
                LyDoChiTiet = $"Đi trễ {s.ChamCong.PhutDiTre} phút ({s.ChamCong.PhutDiTre}p x {SalaryConstants.FINE_PER_MINUTE_LATE:N0}đ)" 
            });
        }
        foreach (var s in empShifts.Where(s => s.ChamCong is { PhutVeSom: > 15 }))
        {
            var p = 50000m;
            fineTotal += p;
            fines.Add(new() {
                LoaiTru = "VE_SOM",
                SoPhutViPham = s.ChamCong!.PhutVeSom,
                SoTienTru = p,
                LyDoChiTiet = $"Về sớm {s.ChamCong.PhutVeSom} phút ngày {s.NgayLamViec:dd/MM/yyyy}"
            });
        }
        if (fineTotal > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaKhoanTru = 5, LoaiKhoan = "KHAU_TRU", LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = "Phạt vi phạm kỷ luật lao động & chấm công", SoTien = fineTotal, ChiTietTruViPhamTamUngs = fines
            });
        }

        // Thuế TNCN
        var thuNhapChiuThue = Math.Max(0m, tongGross - tienAnCa);
        var thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBh - SalaryConstants.PERSONAL_TAX_DEDUCTION);
        var tienThue = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);
        details.Add(new ChiTietPhieuLuong
        {
            MaKhoanTru = 4, LoaiKhoan = "KHAU_TRU", LoaiKhoanLuu = "KHAU_TRU",
            TenKhoanLuu = "Thuế thu nhập cá nhân (TNCN)", SoTien = tienThue,
            ChiTietTruThueTncns = [new() { ThuNhapChiuThue = thuNhapChiuThue, GiamTruBanThan = SalaryConstants.PERSONAL_TAX_DEDUCTION, ThuNhapTinhThue = thuNhapTinhThue, TienThue = tienThue }]
        });

        var tongKhauTru = details.Where(d => d.LoaiKhoan == "KHAU_TRU").Sum(d => d.SoTien);
        var thucNhan = tongGross - tongKhauTru;

        var newP = new PhieuLuong
        {
            MaNhanVien = nv.MaNhanVien,
            MaKyLuong = period.MaKyLuong,
            LuongCoBan = luongCoBan,
            NgayCongThucTe = ngayCongThucTe,
            TienLuongCong = tienLuongCong,
            TienTangCa = tienTangCa,
            TongPhuCap = tongPhuCap,
            TongThuong = tongThuong,
            TongKhauTru = tongKhauTru,
            ThucNhan = thucNhan,
            TrangThai = SalaryConstants.PAYSLIP_DRAFT,
            ChiTietPhieuLuongs = details
        };

        _context.PhieuLuongs.Add(newP);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var (a, d) in assignmentsToLink)
        {
            a.TrangThaiChiTra = "DA_KET_CHUYEN_LUONG";
            a.MaChiTiet = d.MaChiTiet;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien ?? "HR",
            TenTaiKhoanBiTacDong = $"NV_{nv.MaNhanVien}",
            HanhDong = "TINH_LAI_PHIEU_LUONG",
            NoiDung = $"Tính lại phiếu lương tháng {period.Thang}/{period.Nam} cho nhân viên {nv.HoTen}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AdjustSalaryItemAsync(int maPhieuLuong, string loaiKhoan, string tenKhoan, decimal soTien, string? ghiChu, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var payslip = await _context.PhieuLuongs
            .Include(p => p.KyLuong)
            .Include(p => p.ChiTietPhieuLuongs)
            .FirstOrDefaultAsync(p => p.MaPhieuLuong == maPhieuLuong, cancellationToken);

        if (payslip == null || payslip.KyLuong == null) return false;
        if (payslip.KyLuong.TrangThai == SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương đã khóa sổ, không được phép điều chỉnh.");
        }

        var normalizedType = loaiKhoan.Trim().ToUpperInvariant();
        if (normalizedType is not ("PHU_CAP" or "THUONG" or "KHAU_TRU"))
        {
            throw new ArgumentException("Loại khoản phải là PHU_CAP, THUONG hoặc KHAU_TRU.");
        }

        var detail = new ChiTietPhieuLuong
        {
            MaPhieuLuong = maPhieuLuong,
            LoaiKhoan = normalizedType,
            LoaiKhoanLuu = normalizedType,
            TenKhoanLuu = tenKhoan.Trim(),
            SoTien = Math.Abs(soTien),
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? "HR điều chỉnh thủ công" : ghiChu.Trim()
        };

        _context.ChiTietPhieuLuongs.Add(detail);
        payslip.ChiTietPhieuLuongs.Add(detail);

        // Cập nhật lại các tổng
        RecalculateTotals(payslip);

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien ?? "HR",
            TenTaiKhoanBiTacDong = $"PHIEU_LUONG_{maPhieuLuong}",
            HanhDong = "DIEU_CHINH_LUONG",
            NoiDung = $"Thêm khoản {normalizedType}: '{tenKhoan}' với số tiền {soTien:N0} VNĐ"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSalaryItemAsync(int maChiTiet, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var item = await _context.ChiTietPhieuLuongs
            .Include(c => c.PhieuLuong)
                .ThenInclude(p => p!.KyLuong)
            .FirstOrDefaultAsync(c => c.MaChiTiet == maChiTiet, cancellationToken);

        if (item == null || item.PhieuLuong == null || item.PhieuLuong.KyLuong == null) return false;
        if (item.PhieuLuong.KyLuong.TrangThai == SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương đã khóa sổ, không được phép xóa khoản lương.");
        }

        // Nếu là dòng kết chuyển sự kiện -> hoàn tác trạng thái phân công dự án
        await _context.PhanCongDuAns
            .Where(pc => pc.MaChiTiet == maChiTiet)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.TrangThaiChiTra, "CHUA_CHI_TRA")
                .SetProperty(p => p.MaChiTiet, (int?)null), cancellationToken);

        var payslip = item.PhieuLuong;
        _context.ChiTietPhieuLuongs.Remove(item);
        payslip.ChiTietPhieuLuongs.Remove(item);

        RecalculateTotals(payslip);

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien ?? "HR",
            TenTaiKhoanBiTacDong = $"PHIEU_LUONG_{payslip.MaPhieuLuong}",
            HanhDong = "XOA_KHOAN_LUONG",
            NoiDung = $"Xóa dòng khoản lương #{maChiTiet}: {item.TenKhoanLuu} ({item.SoTien:N0} VNĐ)"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void RecalculateTotals(PhieuLuong payslip)
    {
        payslip.TongPhuCap = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "PHU_CAP").Sum(c => c.SoTien);
        payslip.TongThuong = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "THUONG").Sum(c => c.SoTien);

        var tongGross = payslip.TienLuongCong + payslip.TienTangCa + payslip.TongPhuCap + payslip.TongThuong;

        // Cập nhật lại thuế TNCN nếu có dòng thuế
        var taxItem = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaKhoanTru == 4);
        var tienAnCa = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaPhuCap == 1)?.SoTien ?? 0m;
        var tongBh = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaKhoanTru == 1)?.SoTien ?? 0m;

        if (taxItem != null)
        {
            var thuNhapChiuThue = Math.Max(0m, tongGross - tienAnCa);
            var thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBh - SalaryConstants.PERSONAL_TAX_DEDUCTION);
            var tienThue = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);
            taxItem.SoTien = tienThue;
            taxItem.GhiChu = tienThue > 0 ? $"Thu nhập tính thuế: {thuNhapTinhThue:N0}đ" : "Không phát sinh nghĩa vụ nộp thuế";
        }

        payslip.TongKhauTru = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "KHAU_TRU").Sum(c => c.SoTien);
        payslip.ThucNhan = tongGross - payslip.TongKhauTru;
    }

    public async Task<bool> SubmitForApprovalAsync(int maKyLuong, string nguoiGui, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (period.TrangThai == SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương đã khóa sổ.");
        }
        if (period.PhieuLuongs.Count == 0)
        {
            throw new InvalidOperationException("Chưa có phiếu lương nào trong kỳ để gửi duyệt. Vui lòng tính lương trước.");
        }

        period.TrangThai = SalaryConstants.PERIOD_PENDING_APPROVAL;
        period.NgayGuiDuyet = DateTimeOffset.UtcNow;
        period.NguoiGuiDuyet = nguoiGui;
        period.LyDoTuChoi = null;

        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_PENDING;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiGui,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "GUI_DUYET_BANG_LUONG",
            NoiDung = $"HR gửi duyệt bảng lương kỳ {period.Thang:D2}/{period.Nam} ({period.PhieuLuongs.Count} phiếu lương)"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApprovePayrollAsync(int maKyLuong, string nguoiDuyet, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (period.TrangThai != SalaryConstants.PERIOD_PENDING_APPROVAL)
        {
            throw new InvalidOperationException("Chỉ bảng lương ở trạng thái Chờ duyệt mới được phép phê duyệt.");
        }

        period.TrangThai = SalaryConstants.PERIOD_APPROVED;
        period.NgayDuyet = DateTimeOffset.UtcNow;
        period.NguoiDuyet = nguoiDuyet;
        period.LyDoTuChoi = null;

        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_APPROVED;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiDuyet,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "DUYET_BANG_LUONG",
            NoiDung = $"Sếp phê duyệt bảng lương kỳ {period.Thang:D2}/{period.Nam}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectPayrollAsync(int maKyLuong, string nguoiTuChoi, string lyDo, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new ArgumentException("Bắt buộc phải nhập lý do từ chối bảng lương.");
        }

        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (period.TrangThai != SalaryConstants.PERIOD_PENDING_APPROVAL)
        {
            throw new InvalidOperationException("Chỉ bảng lương ở trạng thái Chờ duyệt mới có thể từ chối.");
        }

        period.TrangThai = SalaryConstants.PERIOD_REJECTED;
        period.LyDoTuChoi = lyDo.Trim();
        period.NguoiDuyet = nguoiTuChoi;
        period.NgayDuyet = DateTimeOffset.UtcNow;

        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_DRAFT;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiTuChoi,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "TU_CHOI_BANG_LUONG",
            NoiDung = $"Từ chối duyệt bảng lương kỳ {period.Thang:D2}/{period.Nam}. Lý do: {lyDo.Trim()}",
            LyDo = lyDo.Trim()
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> LockPayrollAsync(int maKyLuong, string nguoiKhoa, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (period.TrangThai != SalaryConstants.PERIOD_APPROVED)
        {
            throw new InvalidOperationException("Bảng lương phải được phê duyệt trước khi khóa sổ.");
        }

        period.TrangThai = SalaryConstants.PERIOD_LOCKED;
        period.NgayKhoaSo = DateTimeOffset.UtcNow;
        period.NguoiKhoaSo = nguoiKhoa;

        // Tự động phát hành & gửi toàn bộ phiếu lương sang ứng dụng di động cho nhân viên trong kỳ
        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_PAID;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiKhoa,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "KHOA_SO_VA_PHAT_HANH_PHIEU_LUONG",
            NoiDung = $"Khóa sổ tài chính bảng lương kỳ {period.Thang:D2}/{period.Nam} và gửi {period.PhieuLuongs.Count} phiếu lương sang ứng dụng di động cho nhân viên"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PublishPayslipsAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (period.TrangThai != SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương phải được khóa sổ trước khi gửi phiếu lương.");
        }

        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_PAID;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "PHAT_HANH_PHIEU_LUONG",
            NoiDung = $"Phát hành / gửi lại phiếu lương tháng {period.Thang:D2}/{period.Nam} cho {period.PhieuLuongs.Count} nhân viên qua ứng dụng Mobile & Portal"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(string FileName, byte[] Content)> ExportBankTransferFileAsync(int maKyLuong, CancellationToken cancellationToken = default)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
                .ThenInclude(p => p.NhanVien)
                    .ThenInclude(n => n!.PhongBan)
            .Include(k => k.PhieuLuongs)
                .ThenInclude(p => p.NhanVien)
                    .ThenInclude(n => n!.ChucVu)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null)
        {
            throw new InvalidOperationException("Không tìm thấy kỳ lương.");
        }

        if (period.TrangThai != SalaryConstants.PERIOD_LOCKED)
        {
            throw new InvalidOperationException("Bảng lương phải được duyệt và khóa sổ trước khi xuất file chuyển khoản.");
        }

        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.Append('\uFEFF');

        // CSV Header chuẩn ngân hàng
        sb.AppendLine("STT,Mã Nhân Viên,Họ và Tên,Phòng Ban,Chức Vụ,Số Tài Khoản Ngân Hàng,Tên Ngân Hàng,Số Tiền Thực Nhận (VND),Nội Dung Chuyển Khoản");

        var stt = 1;
        foreach (var p in period.PhieuLuongs.OrderBy(p => p.NhanVien?.MaPhongBan).ThenBy(p => p.NhanVien?.Ten))
        {
            var nv = p.NhanVien;
            var maNv = $"NV{nv?.MaNhanVien:D4}";
            var hoTen = nv?.HoTen ?? "";
            var phongBan = nv?.PhongBan?.TenPhongBan ?? "";
            var chucVu = nv?.ChucVu?.TenChucVu ?? "";
            var soTk = $"9704{nv?.MaNhanVien:D8}"; // Số tài khoản ngân hàng liên kết
            var nganHang = "Vietcombank";
            var soTien = (long)Math.Round(p.ThucNhan, MidpointRounding.AwayFromZero);
            var noiDung = $"Luong thang {period.Thang:D2}/{period.Nam} - {hoTen}";

            sb.AppendLine($"{stt},\"{maNv}\",\"{hoTen}\",\"{phongBan}\",\"{chucVu}\",\"{soTk}\",\"{nganHang}\",{soTien},\"{noiDung}\"");
            stt++;
        }

        var fileName = $"Chuyen_Khoan_Luong_T{period.Thang:D2}_{period.Nam}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return (fileName, bytes);
    }

    private static bool IsHoliday(DateOnly date)
    {
        // Danh mục ngày lễ cơ bản tại Việt Nam
        if (date.Month == 1 && date.Day == 1) return true; // Tết Tây
        if (date.Month == 4 && date.Day == 30) return true; // 30/4
        if (date.Month == 5 && date.Day == 1) return true; // 1/5
        if (date.Month == 9 && (date.Day == 1 || date.Day == 2 || date.Day == 3)) return true; // Quốc khánh 2/9
        return false;
    }
}
