using System.Globalization;
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

    private async Task<T> ExecutePayrollWriteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is { } ambient)
        {
            var savepoint = "payroll_" + Guid.NewGuid().ToString("N");
            await ambient.CreateSavepointAsync(savepoint, cancellationToken);
            try
            {
                await LockPayrollOperationsAsync(cancellationToken);
                var result = await operation();
                await ambient.ReleaseSavepointAsync(savepoint, cancellationToken);
                return result;
            }
            catch
            {
                await ambient.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await LockPayrollOperationsAsync(cancellationToken);
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task LockPayrollOperationsAsync(CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)", cancellationToken);
        // A reused scoped context must validate the state committed by the preceding lock holder.
        foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Unchanged &&
                     e.Entity is KyLuong or PhieuLuong or ChiTietPhieuLuong or QuyetToanSuKien or PhanCongDuAn
                         or ChiTietTruBaoHiem or ChiTietTruThueTncn or ChiTietTruViPhamTamUng).ToList())
            entry.State = EntityState.Detached;
    }

    private static void EnsureWritablePeriod(KyLuong period)
    {
        if (period.TrangThai is SalaryConstants.PERIOD_LOCKED or "DA_THANH_TOAN")
            throw new InvalidOperationException("Kỳ lương đã khóa sổ, không được phép tính lại hoặc chỉnh sửa.");
        if (period.NgayDuyet.HasValue || period.TrangThai == "DA_DUYET")
            throw new InvalidOperationException("Bảng lương đã được duyệt, không thể tính lại hoặc chỉnh sửa.");
        if (period.NgayGuiDuyet.HasValue && !period.NgayDuyet.HasValue && string.IsNullOrEmpty(period.LyDoTuChoi))
            throw new InvalidOperationException("Bảng lương đang chờ Ban Giám Đốc duyệt, không thể tính lại hoặc chỉnh sửa.");
    }

    private IQueryable<QuyetToanSuKien> OutstandingSettlements(int periodId) =>
        _context.QuyetToanSuKiens.Where(q => q.MaKyChiTra == periodId && q.MaChiTiet == null &&
            q.DuAn != null && q.DuAn.TrangThai == "HOAN_THANH");

    private Task ResetSettlementLinksAsync(List<int> detailIds, CancellationToken cancellationToken) =>
        _context.QuyetToanSuKiens.Where(q => q.MaChiTiet.HasValue && detailIds.Contains(q.MaChiTiet.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.MaChiTiet, (int?)null), cancellationToken);

    private async Task EnsureNoHistoricalLegacyLinksAsync(List<int> detailIds, CancellationToken cancellationToken)
    {
        if (await _context.PhanCongDuAns.AnyAsync(p => p.MaChiTiet.HasValue && detailIds.Contains(p.MaChiTiet.Value) &&
                p.DuAn != null && !p.DuAn.TinhTheoGio, cancellationToken))
            throw new InvalidOperationException("Phiếu lương có khoản sự kiện cũ đã kết chuyển. HR cần đối soát; không được tự động thay đổi khoản tiền đã chốt.");
    }

    private async Task EnsureLegacyReconciledAsync(int? employeeId, CancellationToken cancellationToken)
    {
        if (await _context.PhanCongDuAns.AnyAsync(p => (!employeeId.HasValue || p.MaNhanVien == employeeId) &&
                p.MaChiTiet == null && p.TrangThaiChiTra == "CHUA_CHI_TRA" && p.TrangThaiThamGia != "HUY_PHAN_CONG" &&
                (p.DanhGia == "HOAN_THANH" || p.DanhGia == "XUAT_SAC") &&
                p.DuAn != null && !p.DuAn.TinhTheoGio && p.DuAn.TrangThai == "HOAN_THANH" &&
                !_context.QuyetToanSuKiens.Any(q => q.MaDuAn == p.MaDuAn && q.MaNhanVien == p.MaNhanVien), cancellationToken))
            throw new InvalidOperationException("Còn phụ cấp sự kiện cũ chưa xác định tháng phát sinh và kỳ chi trả. HR cần đối soát số tiền đã chốt và lập quyết toán trước khi tính lương.");
    }

    private static ChiTietPhieuLuong SettlementDetail(QuyetToanSuKien settlement) => new()
    {
        LoaiKhoan = "THUONG",
        LoaiKhoanLuu = "THUONG",
        TenKhoanLuu = $"Tiền sự kiện SK-{settlement.MaDuAn}",
        SoTien = settlement.SoTien,
        GhiChu = $"Quyết toán sự kiện #{settlement.MaQuyetToan}; SK-{settlement.MaDuAn}; tháng phát sinh {settlement.ThangPhatSinh:MM/yyyy}"
    };

    private static PhieuLuong BonusOnlyPayslip(int periodId, int employeeId, int soNguoiPhuThuoc, List<ChiTietPhieuLuong> details)
    {
        var bonus = details.Sum(d => d.SoTien);
        var giamTruBanThan = SalaryConstants.PERSONAL_TAX_DEDUCTION;
        var giamTruPhuThuoc = Math.Max(0, soNguoiPhuThuoc) * SalaryConstants.DEPENDENT_TAX_DEDUCTION;
        var tongGiamTru = giamTruBanThan + giamTruPhuThuoc;
        var taxable = Math.Max(0m, bonus - tongGiamTru);
        var tax = PersonalIncomeTaxCalculator.CalculateTax(taxable);
        if (tax > 0m)
            details.Add(new ChiTietPhieuLuong
            {
                MaKhoanTru = 4, LoaiKhoan = "KHAU_TRU", LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = "Thuế thu nhập cá nhân (TNCN)", SoTien = tax,
                ChiTietTruThueTncns = [new()
                {
                    ThuNhapChiuThue = bonus,
                    GiamTruBanThan = giamTruBanThan,
                    SoNguoiPhuThuoc = Math.Max(0, soNguoiPhuThuoc),
                    GiamTruPhuThuoc = giamTruPhuThuoc,
                    ThuNhapTinhThue = taxable,
                    TienThue = tax
                }]
            });
        return new PhieuLuong
        {
            MaKyLuong = periodId, MaNhanVien = employeeId, TongThuong = bonus,
            TongKhauTru = tax, ThucNhan = bonus - tax, TrangThai = SalaryConstants.PAYSLIP_DRAFT,
            ChiTietPhieuLuongs = details
        };
    }

    public Task<KyLuong> GetOrCreatePeriodAsync(int thang, int nam, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => GetOrCreatePeriodCoreAsync(thang, nam, cancellationToken), cancellationToken);

    private async Task<KyLuong> GetOrCreatePeriodCoreAsync(int thang, int nam, CancellationToken cancellationToken)
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

    public Task<KyLuong?> GetPeriodWithDetailsAsync(int thang, int nam, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => GetPeriodWithDetailsCoreAsync(thang, nam, cancellationToken), cancellationToken);

    private async Task<KyLuong?> GetPeriodWithDetailsCoreAsync(int thang, int nam, CancellationToken cancellationToken)
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

    public Task<List<KyLuong>> GetAllPeriodsAsync(CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => GetAllPeriodsCoreAsync(cancellationToken), cancellationToken);

    private async Task<List<KyLuong>> GetAllPeriodsCoreAsync(CancellationToken cancellationToken)
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

    private sealed class PayrollConfigContext
    {
        public decimal OtNormalRate { get; set; } = SalaryConstants.OVERTIME_NORMAL_RATE;
        public decimal OtWeekendRate { get; set; } = SalaryConstants.OVERTIME_WEEKEND_RATE;
        public decimal OtHolidayRate { get; set; } = SalaryConstants.OVERTIME_HOLIDAY_RATE;
        public decimal NightShiftRate { get; set; } = SalaryConstants.NIGHT_SHIFT_ALLOWANCE_RATE;

        public decimal BhxhRate { get; set; } = SalaryConstants.BHXH_RATE;
        public decimal BhytRate { get; set; } = SalaryConstants.BHYT_RATE;
        public decimal BhtnRate { get; set; } = SalaryConstants.BHTN_RATE;

        public decimal FinePerMinuteLate { get; set; } = SalaryConstants.FINE_PER_MINUTE_LATE;
        public decimal FineEarlyDeparture { get; set; } = 50000m;
        public decimal FineAbsencePenalty { get; set; } = SalaryConstants.FINE_ABSENCE_PENALTY;

        public decimal MealAllowancePerDay { get; set; } = SalaryConstants.MEAL_ALLOWANCE_PER_DAY;
        public decimal ManagerResponsibilityAllowance { get; set; } = 1000000m;
        public decimal HousekeepingHazardAllowancePerDay { get; set; } = 20000m;

        public decimal AttendanceBonus { get; set; } = 500000m;
        public decimal ServiceChargePool { get; set; } = 1000000m;
        public Dictionary<int, string> PositionNames { get; set; } = [];
        public Dictionary<int, string> DepartmentNames { get; set; } = [];
    }

    private async Task<PayrollConfigContext> LoadPayrollConfigAsync(CancellationToken cancellationToken)
    {
        var config = new PayrollConfigContext();

        var tangCas = await _context.CauHinhTangCas.AsNoTracking().ToListAsync(cancellationToken);
        if (tangCas.FirstOrDefault(x => x.MaLoaiOt == 1) is { HeSo: > 0 } ot1) config.OtNormalRate = ot1.HeSo;
        if (tangCas.FirstOrDefault(x => x.MaLoaiOt == 2) is { HeSo: > 0 } ot2) config.OtWeekendRate = ot2.HeSo;
        if (tangCas.FirstOrDefault(x => x.MaLoaiOt == 3) is { HeSo: > 0 } ot3) config.OtHolidayRate = ot3.HeSo;
        if (tangCas.FirstOrDefault(x => x.MaLoaiOt == 4) is { HeSo: > 0 } ot4) config.NightShiftRate = ot4.HeSo;

        var khauTrus = await _context.CauHinhKhauTrus.AsNoTracking().ToListAsync(cancellationToken);
        if (khauTrus.FirstOrDefault(x => x.MaKhoanTru == 1) is { TyLeHoacDinhMuc: > 0 } kt1)
            config.BhxhRate = kt1.TyLeHoacDinhMuc >= 1m ? kt1.TyLeHoacDinhMuc / 100m : kt1.TyLeHoacDinhMuc;
        if (khauTrus.FirstOrDefault(x => x.MaKhoanTru == 2) is { TyLeHoacDinhMuc: > 0 } kt2)
            config.BhytRate = kt2.TyLeHoacDinhMuc >= 1m ? kt2.TyLeHoacDinhMuc / 100m : kt2.TyLeHoacDinhMuc;
        if (khauTrus.FirstOrDefault(x => x.MaKhoanTru == 3) is { TyLeHoacDinhMuc: > 0 } kt3)
            config.BhtnRate = kt3.TyLeHoacDinhMuc >= 1m ? kt3.TyLeHoacDinhMuc / 100m : kt3.TyLeHoacDinhMuc;
        if (khauTrus.FirstOrDefault(x => x.MaKhoanTru == 5) is { TyLeHoacDinhMuc: > 0 } kt5)
            config.FinePerMinuteLate = kt5.TyLeHoacDinhMuc;

        var khoanLuongs = await _context.CauHinhKhoanLuongs.AsNoTracking().ToListAsync(cancellationToken);

        if (khauTrus.FirstOrDefault(x => x.TenKhoanTru.Contains("về sớm", StringComparison.OrdinalIgnoreCase)) is { TyLeHoacDinhMuc: > 0 } ktEarly)
            config.FineEarlyDeparture = ktEarly.TyLeHoacDinhMuc;
        else if (khoanLuongs.FirstOrDefault(x => x.TenKhoan.Contains("về sớm", StringComparison.OrdinalIgnoreCase)) is { GiaTriMacDinh: > 0 } klEarly)
            config.FineEarlyDeparture = klEarly.GiaTriMacDinh;

        var phuCaps = await _context.CauHinhPhuCaps.AsNoTracking().ToListAsync(cancellationToken);
        if (phuCaps.FirstOrDefault(x => x.MaPhuCap == 1) is { MucDinhMuc: > 0 } pc1) config.MealAllowancePerDay = pc1.MucDinhMuc;
        if (phuCaps.FirstOrDefault(x => x.MaPhuCap == 2) is { MucDinhMuc: > 0 } pc2) config.ManagerResponsibilityAllowance = pc2.MucDinhMuc;
        if (phuCaps.FirstOrDefault(x => x.MaPhuCap == 4) is { MucDinhMuc: > 0 } pc4) config.HousekeepingHazardAllowancePerDay = pc4.MucDinhMuc;

        if (khoanLuongs.FirstOrDefault(x => x.MaKhoan == 4) is { GiaTriMacDinh: > 0 } kl4) config.AttendanceBonus = kl4.GiaTriMacDinh;
        if (khoanLuongs.FirstOrDefault(x => x.MaKhoan == 5) is { GiaTriMacDinh: > 0 } kl5) config.ServiceChargePool = kl5.GiaTriMacDinh;

        config.PositionNames = await _context.ChucVus.AsNoTracking()
            .ToDictionaryAsync(x => x.MaChucVu, x => x.TenChucVu, cancellationToken);
        config.DepartmentNames = await _context.PhongBans.AsNoTracking()
            .ToDictionaryAsync(x => x.MaPhongBan, x => x.TenPhongBan, cancellationToken);
        return config;
    }

    private PhieuLuong BuildEmployeePayslip(
        NhanVien nv,
        IReadOnlyList<SalarySegment> salarySegments,
        KyLuong period,
        List<PhanCa> empShifts,
        List<DonTu> empLeaves,
        List<QuyetToanSuKien> empSettlements,
        PayrollConfigContext config,
        out List<(QuyetToanSuKien Settlement, ChiTietPhieuLuong Detail)> settlementsToLink)
    {
        var salaryByDay = salarySegments.SelectMany(segment => Enumerable.Range(segment.From.DayNumber,
                segment.To.DayNumber - segment.From.DayNumber + 1)
            .Select(number => (Date: DateOnly.FromDayNumber(number), segment.Salary)))
            .ToDictionary(x => x.Date, x => x.Salary);
        EffectiveSalary SalaryOn(DateOnly date) => salaryByDay.GetValueOrDefault(date)
            ?? throw new InvalidOperationException($"Nhân viên #{nv.MaNhanVien} có công/ca nhưng thiếu hợp đồng ngày {date:dd/MM/yyyy}.");
        decimal DailyRate(DateOnly date) => SalaryOn(date).MonthlySalary / SalaryConstants.STANDARD_WORKING_DAYS;
        decimal HourlyRate(DateOnly date) => DailyRate(date) / SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY;
        // The single monthly field remains the final effective base; detailed bases are snapshotted below.
        var luongCoBan = salarySegments.LastOrDefault()?.Salary.MonthlySalary ?? 0m;

        var summary = AttendanceSummaryCalculator.Calculate(
            nv.MaNhanVien, nv.HoTen, period.TuNgay, period.DenNgay, empShifts,
            empLeaves.Where(r => r.LoaiDon?.CoHuongLuong == true));

        var ngayCongThucTe = (decimal)summary.TotalCreditedDays;
        var workedDays = (decimal)summary.WorkedDays;

        foreach (var shift in empShifts) _ = SalaryOn(shift.NgayLamViec);
        var creditedDays = summary.Days.Where(d => d.CreditedDays > 0).ToList();
        var tienLuongCong = Math.Round(creditedDays.Sum(d => DailyRate(d.Date) * d.CreditedDays), MidpointRounding.AwayFromZero);

        var details = new List<ChiTietPhieuLuong>();

        // 1. Phụ cấp đi làm ngày Lễ, Tết (+200% cho giờ trong ca chuẩn để đạt tổng 300%)
        var holidayWorkShifts = empShifts
            .Where(s => IsHoliday(s.NgayLamViec) && s.ChamCong is { GioCheckIn: not null, GioCheckOut: not null })
            .ToList();
        var holidayWorkHours = holidayWorkShifts.Sum(s => s.CaLamViec?.GioLamChuan ?? 8m);
        var heSoPhuCapLe = Math.Max(0m, config.OtHolidayRate - 1.0m);
        var tienLamNgayLe = Math.Round(holidayWorkShifts.Sum(s => HourlyRate(s.NgayLamViec) * (s.CaLamViec?.GioLamChuan ?? 8m)) * heSoPhuCapLe, MidpointRounding.AwayFromZero);
        if (holidayWorkHours > 0 && heSoPhuCapLe > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaLoaiOt = 3,
                LoaiKhoan = "PHU_CAP",
                LoaiKhoanLuu = "PHU_CAP",
                TenKhoanLuu = $"Phụ cấp đi làm ngày Lễ, Tết (+{heSoPhuCapLe * 100:N0}%)",
                SoTien = tienLamNgayLe,
                GhiChu = $"{holidayWorkShifts.Count} ca ({holidayWorkHours:N1}h) x đơn giá x {heSoPhuCapLe:N1} (tổng đạt {config.OtHolidayRate * 100:N0}%)"
            });
        }

        // 2. Tăng ca (OT)
        var otHoursNormal = 0m;
        var otHoursWeekend = 0m;
        var otHoursHoliday = 0m;
        var otBaseNormal = 0m;
        var otBaseWeekend = 0m;
        var otBaseHoliday = 0m;

        foreach (var shift in empShifts.Where(s => s.ChamCong is { GioTangCaOt: > 0 }))
        {
            var ot = shift.ChamCong!.GioTangCaOt;
            if (IsHoliday(shift.NgayLamViec))
            {
                otHoursHoliday += ot;
                otBaseHoliday += HourlyRate(shift.NgayLamViec) * ot;
            }
            else if (shift.NgayLamViec.DayOfWeek == DayOfWeek.Sunday)
            {
                otHoursWeekend += ot;
                otBaseWeekend += HourlyRate(shift.NgayLamViec) * ot;
            }
            else
            {
                otHoursNormal += ot;
                otBaseNormal += HourlyRate(shift.NgayLamViec) * ot;
            }
        }

        var tienOtNormal = Math.Round(otBaseNormal * config.OtNormalRate, MidpointRounding.AwayFromZero);
        var tienOtWeekend = Math.Round(otBaseWeekend * config.OtWeekendRate, MidpointRounding.AwayFromZero);
        var tienOtHoliday = Math.Round(otBaseHoliday * config.OtHolidayRate, MidpointRounding.AwayFromZero);
        var tienTangCa = tienOtNormal + tienOtWeekend + tienOtHoliday;

        // Tính phần chênh lệch làm thêm giờ được miễn thuế theo Luật Thuế TNCN (Thông tư 111/2013/TT-BTC)
        var ratioNormal = config.OtNormalRate > 1.0m ? (config.OtNormalRate - 1.0m) / config.OtNormalRate : 0m;
        var ratioWeekend = config.OtWeekendRate > 1.0m ? (config.OtWeekendRate - 1.0m) / config.OtWeekendRate : 0m;
        var ratioHoliday = config.OtHolidayRate > 1.0m ? (config.OtHolidayRate - 1.0m) / config.OtHolidayRate : 0m;

        var tienMienThueOtNormal = Math.Round(tienOtNormal * ratioNormal, MidpointRounding.AwayFromZero);
        var tienMienThueOtWeekend = Math.Round(tienOtWeekend * ratioWeekend, MidpointRounding.AwayFromZero);
        var tienMienThueOtHoliday = Math.Round(tienOtHoliday * ratioHoliday, MidpointRounding.AwayFromZero);
        var tienMienThueOt = tienMienThueOtNormal + tienMienThueOtWeekend + tienMienThueOtHoliday;

        if (otHoursNormal > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaLoaiOt = 1,
                LoaiKhoan = "TANG_CA",
                LoaiKhoanLuu = "TANG_CA",
                TenKhoanLuu = $"Làm thêm ngày thường ({config.OtNormalRate * 100:N0}%)",
                SoTien = tienOtNormal,
                GhiChu = $"{otHoursNormal:N1} giờ x đơn giá x {config.OtNormalRate:N1}"
            });
        }
        if (otHoursWeekend > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaLoaiOt = 2,
                LoaiKhoan = "TANG_CA",
                LoaiKhoanLuu = "TANG_CA",
                TenKhoanLuu = $"Làm thêm ngày nghỉ tuần ({config.OtWeekendRate * 100:N0}%)",
                SoTien = tienOtWeekend,
                GhiChu = $"{otHoursWeekend:N1} giờ x đơn giá x {config.OtWeekendRate:N1}"
            });
        }
        if (otHoursHoliday > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaLoaiOt = 3,
                LoaiKhoan = "TANG_CA",
                LoaiKhoanLuu = "TANG_CA",
                TenKhoanLuu = $"Làm thêm ngày Lễ, Tết ({config.OtHolidayRate * 100:N0}%)",
                SoTien = tienOtHoliday,
                GhiChu = $"{otHoursHoliday:N1} giờ x đơn giá x {config.OtHolidayRate:N1}"
            });
        }

        // 3. Phụ cấp ca đêm (30%)
        var nightShifts = empShifts.Where(s =>
            s.CaLamViec is { QuaDem: true } &&
            s.ChamCong is { GioCheckIn: not null, GioCheckOut: not null }).ToList();
        var nightHours = nightShifts.Sum(s => s.CaLamViec?.GioLamChuan ?? 8m);
        var tienCaDem = Math.Round(nightShifts.Sum(s => HourlyRate(s.NgayLamViec) * (s.CaLamViec?.GioLamChuan ?? 8m)) * config.NightShiftRate, MidpointRounding.AwayFromZero);

        if (nightHours > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaLoaiOt = 4,
                LoaiKhoan = "PHU_CAP",
                LoaiKhoanLuu = "PHU_CAP",
                TenKhoanLuu = $"Phụ cấp ca đêm ({config.NightShiftRate * 100:N0}%)",
                SoTien = tienCaDem,
                GhiChu = $"{nightShifts.Count} ca đêm ({nightHours:N0}h) x {config.NightShiftRate * 100:N0}%"
            });
        }

        // 4. Phụ cấp ăn ca (Miễn thuế TNCN)
        var tienAnCa = Math.Round(workedDays * config.MealAllowancePerDay, MidpointRounding.AwayFromZero);
        if (workedDays > 0 && config.MealAllowancePerDay > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaPhuCap = 1,
                LoaiKhoan = "PHU_CAP",
                LoaiKhoanLuu = "PHU_CAP",
                TenKhoanLuu = "Phụ cấp ăn ca",
                SoTien = tienAnCa,
                GhiChu = $"{workedDays:N0} ngày công thực tế x {config.MealAllowancePerDay:N0}đ (miễn thuế)"
            });
        }

        // 5. Phụ cấp quản lý & Phụ cấp độc hại buồng phòng
        string PositionName(EffectiveSalary salary) => salary.PositionId is { } id
            ? config.PositionNames.GetValueOrDefault(id) ?? (nv.MaChucVu == id ? nv.ChucVu?.TenChucVu : null) ?? ""
            : nv.ChucVu?.TenChucVu ?? ""; // Legacy contracts have no organizational snapshot.
        string DepartmentName(EffectiveSalary salary) => salary.DepartmentId is { } id
            ? config.DepartmentNames.GetValueOrDefault(id) ?? (nv.MaPhongBan == id ? nv.PhongBan?.TenPhongBan : null) ?? ""
            : nv.PhongBan?.TenPhongBan ?? "";
        var managerDays = creditedDays.Count(d => IsManagerPosition(PositionName(SalaryOn(d.Date))));
        if (managerDays > 0 && config.ManagerResponsibilityAllowance > 0)
        {
            var tienTrachNhiem = Math.Round(config.ManagerResponsibilityAllowance *
                Math.Min(SalaryConstants.STANDARD_WORKING_DAYS, managerDays) / SalaryConstants.STANDARD_WORKING_DAYS,
                MidpointRounding.AwayFromZero);
            details.Add(new ChiTietPhieuLuong
            {
                MaPhuCap = 2, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP",
                TenKhoanLuu = "Phụ cấp trách nhiệm quản lý", SoTien = tienTrachNhiem,
                GhiChu = $"Phân bổ theo {managerDays}/{SalaryConstants.STANDARD_WORKING_DAYS} công giữ chức vụ quản lý trong kỳ"
            });
        }

        var hazardDays = summary.Days.Count(d => d.HasWorked && IsHazardDepartment(DepartmentName(SalaryOn(d.Date))));
        var tienDocHai = 0m;
        if (hazardDays > 0 && config.HousekeepingHazardAllowancePerDay > 0)
        {
            tienDocHai = Math.Round(hazardDays * config.HousekeepingHazardAllowancePerDay, MidpointRounding.AwayFromZero);
            details.Add(new ChiTietPhieuLuong
            {
                MaPhuCap = 4, LoaiKhoan = "PHU_CAP", LoaiKhoanLuu = "PHU_CAP",
                TenKhoanLuu = "Phụ cấp độc hại buồng phòng", SoTien = tienDocHai,
                GhiChu = $"{hazardDays} ngày tại bộ phận buồng phòng x {config.HousekeepingHazardAllowancePerDay:N0}đ (miễn thuế)"
            });
        }

        // 6. Sự kiện khách sạn
        var eventDetails = empSettlements
            .Select(q => (Settlement: q, Detail: SettlementDetail(q))).ToList();
        details.AddRange(eventDetails.Select(x => x.Detail));
        settlementsToLink = eventDetails;
        var tienThuongSuKien = eventDetails.Sum(x => x.Detail.SoTien);

        // 7. Thưởng chuyên cần & Phí phục vụ (Service Charge)
        var tongThuong = tienThuongSuKien;
        var hasLate = empShifts.Any(s => s.ChamCong is { PhutDiTre: > 0 });
        var hasEarly = empShifts.Any(s => s.ChamCong is { PhutVeSom: > 15 });
        var hasAbsence = empShifts.Any(s =>
        {
            var cc = s.ChamCong;
            var isShiftEnded = s.KetThucDuKien <= DateTimeOffset.UtcNow ||
                               s.NgayLamViec < DateOnly.FromDateTime(DateTime.Today) ||
                               (period.DenNgay < DateOnly.FromDateTime(DateTime.Today) && s.NgayLamViec <= period.DenNgay);
            var hasNoPunch = cc == null || (!cc.GioCheckIn.HasValue && !cc.GioCheckOut.HasValue);
            var isUnattendedScheduled = isShiftEnded && s.TrangThai == "DA_PHAN" && hasNoPunch;
            var isAbsent = s.TrangThai == "VANG_MAT" || cc?.TrangThai == "VANG_MAT" || isUnattendedScheduled;
            var hasApprovedLeave = empLeaves.Any(l => l.TuNgay <= s.NgayLamViec && l.DenNgay >= s.NgayLamViec);
            return isAbsent && !hasApprovedLeave;
        });

        var hasViolations = hasLate || hasEarly || hasAbsence;

        if (ngayCongThucTe >= 25 && !hasViolations && config.AttendanceBonus > 0)
        {
            details.Add(new ChiTietPhieuLuong
            {
                MaKhoan = 4,
                LoaiKhoan = "THUONG",
                LoaiKhoanLuu = "THUONG",
                TenKhoanLuu = "Tiền thưởng chuyên cần",
                SoTien = config.AttendanceBonus,
                GhiChu = $"Đạt {ngayCongThucTe:N1}/26 ngày công và không vi phạm giờ giấc / kỷ luật"
            });
            tongThuong += config.AttendanceBonus;
        }

        // Phân bổ phí phục vụ theo ngày công thực tế (tối đa bằng mức quy định khi đạt đủ 26 ngày công)
        if (config.ServiceChargePool > 0)
        {
            var serviceCharge = workedDays > 0
                ? Math.Round(config.ServiceChargePool * Math.Min(1m, workedDays / (decimal)SalaryConstants.STANDARD_WORKING_DAYS), MidpointRounding.AwayFromZero)
                : 0m;
            if (serviceCharge > 0)
            {
                details.Add(new ChiTietPhieuLuong
                {
                    MaKhoan = 5,
                    LoaiKhoan = "THUONG",
                    LoaiKhoanLuu = "THUONG",
                    TenKhoanLuu = "Tiền phí phục vụ (Service Charge)",
                    SoTien = serviceCharge,
                    GhiChu = $"{workedDays:N0}/{SalaryConstants.STANDARD_WORKING_DAYS} ngày công thực tế x {config.ServiceChargePool:N0}đ"
                });
                tongThuong += serviceCharge;
            }
        }

        // Tổng phụ cấp & Tổng Gross (Tầng 1)
        var tongPhuCap = details.Where(d => d.LoaiKhoan == "PHU_CAP").Sum(d => d.SoTien);
        var tongGross = tienLuongCong + tienTangCa + tongPhuCap + tongThuong;

        // 8. Khấu trừ bảo hiểm bắt buộc có trần (Theo Luật BHXH 2014 & BLLĐ 2019)
        // Điều kiện trích đóng bảo hiểm:
        // 1. Không áp dụng cho Hợp đồng thử việc (THU_VIEC).
        // 2. Không áp dụng nếu số ngày công thực tế < 14 ngày trong tháng (Khoản 3 Điều 85 Luật BHXH).
        // One monthly insurance assessment; probation days never count toward the formal-day threshold.
        var formalDays = creditedDays.Where(d => !SalaryOn(d.Date).IsProbation).ToList();
        var isProbation = formalDays.Count == 0 && creditedDays.Any(d => SalaryOn(d.Date).IsProbation);
        var isEligibleForInsurance = formalDays.Count >= 14;
        var insuranceSalary = formalDays.LastOrDefault() is { } lastFormalDay
            ? SalaryOn(lastFormalDay.Date).MonthlySalary : 0m;
        var luongDongBh = isEligibleForInsurance ? Math.Min(insuranceSalary, SalaryConstants.MAX_INSURANCE_SALARY_BASE) : 0m;
        var luongDongBhtn = isEligibleForInsurance ? Math.Min(insuranceSalary, SalaryConstants.MAX_UNEMPLOYMENT_INSURANCE_SALARY_BASE) : 0m;

        var tienBhxh = isEligibleForInsurance ? Math.Round(luongDongBh * config.BhxhRate, MidpointRounding.AwayFromZero) : 0m;
        var tienBhyt = isEligibleForInsurance ? Math.Round(luongDongBh * config.BhytRate, MidpointRounding.AwayFromZero) : 0m;
        var tienBhtn = isEligibleForInsurance ? Math.Round(luongDongBhtn * config.BhtnRate, MidpointRounding.AwayFromZero) : 0m;
        var tongBaoHiem = tienBhxh + tienBhyt + tienBhtn;
        var tongTyLeBh = (config.BhxhRate + config.BhytRate + config.BhtnRate) * 100m;

        if (isEligibleForInsurance && tongBaoHiem > 0)
        {
            var insuranceDetail = new ChiTietPhieuLuong
            {
                MaKhoanTru = 1,
                LoaiKhoan = "KHAU_TRU",
                LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = $"Bảo hiểm bắt buộc ({tongTyLeBh:N1}%)",
                SoTien = tongBaoHiem,
                GhiChu = $"BHXH {config.BhxhRate * 100:N1}%: {tienBhxh:N0}đ, BHYT {config.BhytRate * 100:N1}%: {tienBhyt:N0}đ, BHTN {config.BhtnRate * 100:N1}%: {tienBhtn:N0}đ",
                ChiTietTruBaoHiems = new List<ChiTietTruBaoHiem>
                {
                    new()
                    {
                        LuongDongBaoHiem = luongDongBh,
                        TyLeBhxh = config.BhxhRate * 100m,
                        TienBhxh = tienBhxh,
                        TyLeBhyt = config.BhytRate * 100m,
                        TienBhyt = tienBhyt,
                        TyLeBhtn = config.BhtnRate * 100m,
                        TienBhtn = tienBhtn,
                        TongBaoHiem = tongBaoHiem
                    }
                }
            };
            details.Add(insuranceDetail);
        }
        else
        {
            var lyDoKhongDong = isProbation
                ? "HĐ thử việc - Không thuộc đối tượng đóng BH bắt buộc"
                : $"Ngày công chính thức ({formalDays.Count}) < 14 ngày - Miễn đóng BH (Khoản 3 Điều 85 Luật BHXH)";

            var insuranceDetail = new ChiTietPhieuLuong
            {
                MaKhoanTru = 1,
                LoaiKhoan = "KHAU_TRU",
                LoaiKhoanLuu = "KHAU_TRU",
                TenKhoanLuu = "Bảo hiểm bắt buộc (Miễn đóng)",
                SoTien = 0m,
                GhiChu = lyDoKhongDong,
                ChiTietTruBaoHiems = new List<ChiTietTruBaoHiem>
                {
                    new()
                    {
                        LuongDongBaoHiem = 0m,
                        TyLeBhxh = 0m,
                        TienBhxh = 0m,
                        TyLeBhyt = 0m,
                        TienBhyt = 0m,
                        TyLeBhtn = 0m,
                        TienBhtn = 0m,
                        TongBaoHiem = 0m
                    }
                }
            };
            details.Add(insuranceDetail);
        }

        // Immutable salary bases on the payslip, including zero-pay segments, for later historical review.
        details.Single(d => d.MaKhoanTru == 1).GhiChu += "; Căn cứ lương: " + string.Join(" | ", salarySegments.Select(segment =>
            $"{segment.From:dd/MM/yyyy}–{segment.To:dd/MM/yyyy}: HĐ #{segment.Salary.Contract.MaHopDong}" +
            (segment.Salary.Amendment is { } appendix ? $", PL #{appendix.MaPhuLuc}" : "") +
            $", {segment.Salary.MonthlySalary:N0}đ/tháng, CV #{segment.Salary.PositionId}, PB #{segment.Salary.DepartmentId}"));

        // 9. Phạt vi phạm kỷ luật & chấm công (Bao gồm Bỏ ca 200k)
        var fineDetails = new List<ChiTietTruViPhamTamUng>();
        var tongTienPhat = 0m;

        foreach (var shift in empShifts)
        {
            var cc = shift.ChamCong;
            if (cc != null)
            {
                if (cc.PhutDiTre > 0)
                {
                    var fine = cc.PhutDiTre * config.FinePerMinuteLate;
                    tongTienPhat += fine;
                    fineDetails.Add(new ChiTietTruViPhamTamUng
                    {
                        LoaiTru = "DI_TRE",
                        SoPhutViPham = cc.PhutDiTre,
                        SoTienTru = fine,
                        LyDoChiTiet = $"Đi trễ {cc.PhutDiTre} phút ngày {shift.NgayLamViec:dd/MM/yyyy} ({cc.PhutDiTre}p x {config.FinePerMinuteLate:N0}đ)"
                    });
                }

                if (cc.PhutVeSom > 15)
                {
                    var fine = cc.PhutVeSom * config.FinePerMinuteLate;
                    tongTienPhat += fine;
                    fineDetails.Add(new ChiTietTruViPhamTamUng
                    {
                        LoaiTru = "VE_SOM",
                        SoPhutViPham = cc.PhutVeSom,
                        SoTienTru = fine,
                        LyDoChiTiet = $"Về sớm {cc.PhutVeSom} phút ngày {shift.NgayLamViec:dd/MM/yyyy} (>15p: {cc.PhutVeSom}p x {config.FinePerMinuteLate:N0}đ)"
                    });
                }
            }

            // Kiểm tra bỏ ca (Vắng mặt không phép)
            // 1. Quản lý hoặc hệ thống đã đánh dấu VANG_MAT
            // 2. HOẶC ca làm việc đã kết thúc (ngày làm việc đã qua hoặc ca đã hết giờ),
            //    nhân viên không có chấm công vào/ra và trạng thái vẫn là DA_PHAN
            var isShiftEnded = shift.KetThucDuKien <= DateTimeOffset.UtcNow ||
                               shift.NgayLamViec < DateOnly.FromDateTime(DateTime.Today) ||
                               (period.DenNgay < DateOnly.FromDateTime(DateTime.Today) && shift.NgayLamViec <= period.DenNgay);
            var hasNoPunch = cc == null || (!cc.GioCheckIn.HasValue && !cc.GioCheckOut.HasValue);
            var isUnattendedScheduled = isShiftEnded && shift.TrangThai == "DA_PHAN" && hasNoPunch;

            var isAbsent = shift.TrangThai == "VANG_MAT" || cc?.TrangThai == "VANG_MAT" || isUnattendedScheduled;
            var hasApprovedLeave = empLeaves.Any(l => l.TuNgay <= shift.NgayLamViec && l.DenNgay >= shift.NgayLamViec);
            if (isAbsent && !hasApprovedLeave)
            {
                var fine = config.FineAbsencePenalty;
                tongTienPhat += fine;
                fineDetails.Add(new ChiTietTruViPhamTamUng
                {
                    LoaiTru = "KY_LUAT",
                    SoPhutViPham = 480,
                    SoTienTru = fine,
                    LyDoChiTiet = $"Bỏ ca làm việc không phép ngày {shift.NgayLamViec:dd/MM/yyyy} (không tính ngày công & phạt {fine:N0}đ)"
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

        // 10. Thuế TNCN lũy tiến từng phần (sau khi loại trừ các khoản thu nhập được miễn thuế theo Thông tư 111/2013/TT-BTC)
        // Các khoản miễn thuế: Ăn ca, Độc hại buồng phòng, Phụ cấp làm việc ngày Lễ (+200%), Phụ cấp ca đêm (30%), và phần chênh lệch làm thêm giờ
        var tongThuNhapMienThue = tienAnCa + tienDocHai + tienLamNgayLe + tienCaDem + tienMienThueOt;
        var thuNhapChiuThue = Math.Max(0m, tongGross - tongThuNhapMienThue);
        var giamTruBanThan = SalaryConstants.PERSONAL_TAX_DEDUCTION;
        var soNguoiPhuThuoc = Math.Max(0, nv.SoNguoiPhuThuoc);
        var giamTruPhuThuoc = soNguoiPhuThuoc * SalaryConstants.DEPENDENT_TAX_DEDUCTION;

        var thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBaoHiem - giamTruBanThan - giamTruPhuThuoc);
        var tienThue = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);

        var taxDetail = new ChiTietPhieuLuong
        {
            MaKhoanTru = 4,
            LoaiKhoan = "KHAU_TRU",
            LoaiKhoanLuu = "KHAU_TRU",
            TenKhoanLuu = "Thuế thu nhập cá nhân (TNCN)",
            SoTien = tienThue,
            GhiChu = tienThue > 0
                ? $"Thu nhập tính thuế: {thuNhapTinhThue:N0}đ (Miễn thuế: {tongThuNhapMienThue:N0}đ)"
                : $"Không phát sinh nghĩa vụ nộp thuế (Miễn thuế: {tongThuNhapMienThue:N0}đ)",
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

        // 11. Tổng Khấu Trừ & Thực Nhận (Net)
        var tongKhauTru = details.Where(d => d.LoaiKhoan == "KHAU_TRU").Sum(d => d.SoTien);
        var thucNhan = tongGross - tongKhauTru;

        return new PhieuLuong
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
    }

    public Task<KyLuong> CalculatePayrollAsync(int thang, int nam, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => CalculatePayrollCoreAsync(thang, nam, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<KyLuong> CalculatePayrollCoreAsync(int thang, int nam, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var period = await GetOrCreatePeriodCoreAsync(thang, nam, cancellationToken);
        EnsureWritablePeriod(period);

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
                await EnsureNoHistoricalLegacyLinksAsync(oldDetailIds, cancellationToken);
                await ResetSettlementLinksAsync(oldDetailIds, cancellationToken);
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

        // 2. Tải trước toàn bộ phân ca, chấm công và đơn từ trong kỳ
        var allShifts = await _context.PhanCas
            .AsNoTracking()
            .Where(s => s.TrangThai != "DA_HUY" && s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay)
            .Include(s => s.ChamCong)
            .Include(s => s.CaLamViec)
            .ToListAsync(cancellationToken);

        var activeEmployeeIdsWithShifts = allShifts.Select(s => s.MaNhanVien).Distinct().ToList();

        // 3. Lấy danh sách nhân viên: đang làm việc HOẶC đã thôi việc nhưng thôi việc trong/sau kỳ, HOẶC có ca làm việc trong kỳ
        var employees = await _context.NhanViens
            .AsNoTracking()
            .Where(e => e.TrangThai == "DANG_LAM" ||
                        (e.NgayThoiViec.HasValue && e.NgayThoiViec.Value >= period.TuNgay) ||
                        activeEmployeeIdsWithShifts.Contains(e.MaNhanVien))
            .Include(e => e.PhongBan)
            .Include(e => e.ChucVu)
            .Include(e => e.HopDongs).ThenInclude(h => h.PhuLucLuongs)
            .ToListAsync(cancellationToken);

        var allLeaves = await _context.DonTus
            .AsNoTracking()
            .Where(r => r.TrangThai == "DA_DUYET" &&
                        r.TuNgay <= period.DenNgay && r.DenNgay >= period.TuNgay)
            .Include(r => r.LoaiDon)
            .ToListAsync(cancellationToken);

        await EnsureLegacyReconciledAsync(null, cancellationToken);
        var settlements = await OutstandingSettlements(period.MaKyLuong).ToListAsync(cancellationToken);

        var config = await LoadPayrollConfigAsync(cancellationToken);
        var newPayslips = new List<PhieuLuong>();
        var settlementsToLink = new List<(QuyetToanSuKien Settlement, ChiTietPhieuLuong Detail)>();

        foreach (var nv in employees)
        {
            var salarySegments = EffectiveSalaryResolver.BuildSegments(nv.HopDongs, period.TuNgay, period.DenNgay);
            if (salarySegments.Count == 0)
            {
                if (allShifts.Any(s => s.MaNhanVien == nv.MaNhanVien))
                    throw new InvalidOperationException($"Nhân viên {nv.HoTen} (#{nv.MaNhanVien}) có ca trong kỳ nhưng thiếu hợp đồng hiệu lực.");
                continue;
            }

            var empShifts = allShifts.Where(s => s.MaNhanVien == nv.MaNhanVien).ToList();
            var empLeaves = allLeaves.Where(r => r.MaNhanVien == nv.MaNhanVien).ToList();
            var empSettlements = settlements.Where(q => q.MaNhanVien == nv.MaNhanVien).ToList();

            var payslip = BuildEmployeePayslip(
                nv, salarySegments, period, empShifts, empLeaves, empSettlements, config,
                out var empSettlementsToLink);

            newPayslips.Add(payslip);
            settlementsToLink.AddRange(empSettlementsToLink);
        }

        foreach (var group in settlements.Where(q => newPayslips.All(p => p.MaNhanVien != q.MaNhanVien)).GroupBy(q => q.MaNhanVien))
        {
            var eventDetails = group.Select(q => (Settlement: q, Detail: SettlementDetail(q))).ToList();
            var emp = employees.FirstOrDefault(e => e.MaNhanVien == group.Key);
            var soNguoiPhuThuoc = emp?.SoNguoiPhuThuoc ?? 0;
            newPayslips.Add(BonusOnlyPayslip(period.MaKyLuong, group.Key, soNguoiPhuThuoc, eventDetails.Select(x => x.Detail).ToList()));
            settlementsToLink.AddRange(eventDetails);
        }

        _context.PhieuLuongs.AddRange(newPayslips);
        period.TrangThai = SalaryConstants.PERIOD_OPEN;
        period.NgayGuiDuyet = null;
        period.NgayDuyet = null;
        period.LyDoTuChoi = null;

        // Lưu trước để có ID chi tiết phiếu lương
        await _context.SaveChangesAsync(cancellationToken);

        // Cập nhật ngược lại mã chi tiết cho phân công dự án
        foreach (var (settlement, detail) in settlementsToLink)
            settlement.MaChiTiet = detail.MaChiTiet;

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

    public Task<bool> RecalculateEmployeeAsync(int maPhieuLuong, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => RecalculateEmployeeCoreAsync(maPhieuLuong, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> RecalculateEmployeeCoreAsync(int maPhieuLuong, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var payslip = await _context.PhieuLuongs
            .Include(p => p.KyLuong)
            .Include(p => p.ChiTietPhieuLuongs)
            .FirstOrDefaultAsync(p => p.MaPhieuLuong == maPhieuLuong, cancellationToken);

        if (payslip == null || payslip.KyLuong == null) return false;
        EnsureWritablePeriod(payslip.KyLuong);

        var period = payslip.KyLuong;
        var employeeId = payslip.MaNhanVien;
        await EnsureLegacyReconciledAsync(employeeId, cancellationToken);

        // Hoàn tác các phân công dự án cũ
        var detailIds = payslip.ChiTietPhieuLuongs.Select(c => c.MaChiTiet).ToList();
        if (detailIds.Count > 0)
        {
            await EnsureNoHistoricalLegacyLinksAsync(detailIds, cancellationToken);
            await ResetSettlementLinksAsync(detailIds, cancellationToken);
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
            .Include(e => e.HopDongs).ThenInclude(h => h.PhuLucLuongs)
            .FirstOrDefaultAsync(e => e.MaNhanVien == employeeId, cancellationToken);

        if (nv == null) throw new InvalidOperationException("Không tìm thấy nhân viên để tính lại lương.");

        var salarySegments = EffectiveSalaryResolver.BuildSegments(nv.HopDongs, period.TuNgay, period.DenNgay);
        var hasShiftsInPeriod = await _context.PhanCas.AnyAsync(s => s.MaNhanVien == employeeId &&
            s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay && s.TrangThai != "DA_HUY", cancellationToken);
        if (salarySegments.Count == 0 && hasShiftsInPeriod)
            throw new InvalidOperationException($"Nhân viên #{employeeId} có ca trong kỳ nhưng thiếu hợp đồng hiệu lực.");

        var isEligibleEmployee = nv.TrangThai == "DANG_LAM" ||
                                (nv.NgayThoiViec.HasValue && nv.NgayThoiViec.Value >= period.TuNgay) ||
                                await _context.PhanCas.AnyAsync(s => s.MaNhanVien == employeeId && s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay && s.TrangThai != "DA_HUY", cancellationToken);

        var settlements = await OutstandingSettlements(period.MaKyLuong)
            .Where(q => q.MaNhanVien == employeeId).ToListAsync(cancellationToken);
        if (salarySegments.Count == 0 || !isEligibleEmployee)
        {
            if (settlements.Count == 0)
                throw new InvalidOperationException("Nhân viên không có hợp đồng hiệu lực hoặc quyết toán sự kiện trong kỳ; cần HR đối soát phiếu lương.");
            var eventDetails = settlements.Select(q => (Settlement: q, Detail: SettlementDetail(q))).ToList();
            _context.PhieuLuongs.Add(BonusOnlyPayslip(period.MaKyLuong, employeeId, nv.SoNguoiPhuThuoc, eventDetails.Select(x => x.Detail).ToList()));
            await _context.SaveChangesAsync(cancellationToken);
            foreach (var (settlement, detail) in eventDetails) settlement.MaChiTiet = detail.MaChiTiet;
            _context.NhatKyQuanTris.Add(new NhatKyQuanTri
            {
                MaTaiKhoanThucHien = maTaiKhoan, TenNguoiThucHien = nguoiThucHien ?? "HR",
                TenTaiKhoanBiTacDong = $"NV_{employeeId}", HanhDong = "TINH_LAI_PHIEU_LUONG",
                NoiDung = $"Tính lại tiền sự kiện tháng {period.Thang}/{period.Nam} cho nhân viên {nv.HoTen}"
            });
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        var config = await LoadPayrollConfigAsync(cancellationToken);

        var empShifts = await _context.PhanCas.AsNoTracking()
            .Where(s => s.TrangThai != "DA_HUY" && s.MaNhanVien == nv.MaNhanVien && s.NgayLamViec >= period.TuNgay && s.NgayLamViec <= period.DenNgay)
            .Include(s => s.ChamCong).Include(s => s.CaLamViec).ToListAsync(cancellationToken);

        var empLeaves = await _context.DonTus.AsNoTracking()
            .Where(r => r.MaNhanVien == nv.MaNhanVien && r.TrangThai == "DA_DUYET" &&
                        r.TuNgay <= period.DenNgay && r.DenNgay >= period.TuNgay)
            .Include(r => r.LoaiDon).ToListAsync(cancellationToken);

        var newP = BuildEmployeePayslip(
            nv, salarySegments, period, empShifts, empLeaves, settlements, config,
            out var settlementsToLink);

        _context.PhieuLuongs.Add(newP);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var (settlement, detail) in settlementsToLink)
            settlement.MaChiTiet = detail.MaChiTiet;

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

    public Task<bool> AdjustSalaryItemAsync(int maPhieuLuong, string loaiKhoan, string tenKhoan, decimal soTien, string? ghiChu, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => AdjustSalaryItemCoreAsync(maPhieuLuong, loaiKhoan, tenKhoan, soTien, ghiChu, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> AdjustSalaryItemCoreAsync(int maPhieuLuong, string loaiKhoan, string tenKhoan, decimal soTien, string? ghiChu, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var payslip = await _context.PhieuLuongs
            .Include(p => p.KyLuong)
            .Include(p => p.ChiTietPhieuLuongs)
                .ThenInclude(c => c.ChiTietTruThueTncns)
            .FirstOrDefaultAsync(p => p.MaPhieuLuong == maPhieuLuong, cancellationToken);

        if (payslip == null || payslip.KyLuong == null) return false;
        EnsureWritablePeriod(payslip.KyLuong);

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
        var config = await LoadPayrollConfigAsync(cancellationToken);
        RecalculateTotals(payslip, config);

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

    public Task<bool> DeleteSalaryItemAsync(int maChiTiet, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => DeleteSalaryItemCoreAsync(maChiTiet, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> DeleteSalaryItemCoreAsync(int maChiTiet, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var item = await _context.ChiTietPhieuLuongs
            .Include(c => c.PhieuLuong)
                .ThenInclude(p => p!.KyLuong)
            .Include(c => c.PhieuLuong)
                .ThenInclude(p => p!.ChiTietPhieuLuongs)
                    .ThenInclude(c => c.ChiTietTruThueTncns)
            .FirstOrDefaultAsync(c => c.MaChiTiet == maChiTiet, cancellationToken);

        if (item == null || item.PhieuLuong == null || item.PhieuLuong.KyLuong == null) return false;
        EnsureWritablePeriod(item.PhieuLuong.KyLuong);

        await EnsureNoHistoricalLegacyLinksAsync([maChiTiet], cancellationToken);
        await ResetSettlementLinksAsync([maChiTiet], cancellationToken);

        // Nếu là dòng kết chuyển sự kiện -> hoàn tác trạng thái phân công dự án
        await _context.PhanCongDuAns
            .Where(pc => pc.MaChiTiet == maChiTiet)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.TrangThaiChiTra, "CHUA_CHI_TRA")
                .SetProperty(p => p.MaChiTiet, (int?)null), cancellationToken);

        var payslip = item.PhieuLuong;
        _context.ChiTietPhieuLuongs.Remove(item);
        payslip.ChiTietPhieuLuongs.Remove(item);

        var config = await LoadPayrollConfigAsync(cancellationToken);
        RecalculateTotals(payslip, config);

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

    private static void RecalculateTotals(PhieuLuong payslip, PayrollConfigContext? config = null)
    {
        payslip.TongPhuCap = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "PHU_CAP").Sum(c => c.SoTien);
        payslip.TongThuong = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "THUONG").Sum(c => c.SoTien);

        var tongGross = payslip.TienLuongCong + payslip.TienTangCa + payslip.TongPhuCap + payslip.TongThuong;

        // Cập nhật lại thuế TNCN nếu có dòng thuế
        var taxItem = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaKhoanTru == 4);
        var tongBh = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaKhoanTru == 1)?.SoTien ?? 0m;

        if (taxItem != null)
        {
            var tienAnCa = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaPhuCap == 1)?.SoTien ?? 0m;
            var tienDocHai = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaPhuCap == 4)?.SoTien ?? 0m;
            var tienLamNgayLe = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaPhuCap == 5 || (c.LoaiKhoan == "PHU_CAP" && c.TenKhoanLuu.Contains("ngày Lễ")))?.SoTien ?? 0m;
            var tienCaDem = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaLoaiOt == 4 || (c.LoaiKhoan == "PHU_CAP" && c.TenKhoanLuu.Contains("ca đêm")))?.SoTien ?? 0m;

            var tienOtNormal = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaLoaiOt == 1 && c.LoaiKhoan == "TANG_CA")?.SoTien ?? 0m;
            var tienOtWeekend = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaLoaiOt == 2 && c.LoaiKhoan == "TANG_CA")?.SoTien ?? 0m;
            var tienOtHoliday = payslip.ChiTietPhieuLuongs.FirstOrDefault(c => c.MaLoaiOt == 3 && c.LoaiKhoan == "TANG_CA")?.SoTien ?? 0m;

            var normalRate = config?.OtNormalRate ?? SalaryConstants.OVERTIME_NORMAL_RATE;
            var weekendRate = config?.OtWeekendRate ?? SalaryConstants.OVERTIME_WEEKEND_RATE;
            var holidayRate = config?.OtHolidayRate ?? SalaryConstants.OVERTIME_HOLIDAY_RATE;

            var ratioNormal = normalRate > 1.0m ? (normalRate - 1.0m) / normalRate : 0m;
            var ratioWeekend = weekendRate > 1.0m ? (weekendRate - 1.0m) / weekendRate : 0m;
            var ratioHoliday = holidayRate > 1.0m ? (holidayRate - 1.0m) / holidayRate : 0m;

            var tienMienThueOt = Math.Round(tienOtNormal * ratioNormal, MidpointRounding.AwayFromZero)
                               + Math.Round(tienOtWeekend * ratioWeekend, MidpointRounding.AwayFromZero)
                               + Math.Round(tienOtHoliday * ratioHoliday, MidpointRounding.AwayFromZero);

            var tongThuNhapMienThue = tienAnCa + tienDocHai + tienLamNgayLe + tienCaDem + tienMienThueOt;
            var thuNhapChiuThue = Math.Max(0m, tongGross - tongThuNhapMienThue);

            var subTax = taxItem.ChiTietTruThueTncns?.FirstOrDefault();
            var giamTruBanThan = subTax?.GiamTruBanThan ?? SalaryConstants.PERSONAL_TAX_DEDUCTION;
            var giamTruPhuThuoc = subTax?.GiamTruPhuThuoc ?? 0m;

            var thuNhapTinhThue = Math.Max(0m, thuNhapChiuThue - tongBh - giamTruBanThan - giamTruPhuThuoc);
            var tienThue = PersonalIncomeTaxCalculator.CalculateTax(thuNhapTinhThue);

            taxItem.SoTien = tienThue;
            taxItem.GhiChu = tienThue > 0
                ? $"Thu nhập tính thuế: {thuNhapTinhThue:N0}đ (Miễn thuế: {tongThuNhapMienThue:N0}đ)"
                : $"Không phát sinh nghĩa vụ nộp thuế (Miễn thuế: {tongThuNhapMienThue:N0}đ)";

            if (subTax != null)
            {
                subTax.ThuNhapChiuThue = thuNhapChiuThue;
                subTax.ThuNhapTinhThue = thuNhapTinhThue;
                subTax.TienThue = tienThue;
            }
        }

        payslip.TongKhauTru = payslip.ChiTietPhieuLuongs.Where(c => c.LoaiKhoan == "KHAU_TRU").Sum(c => c.SoTien);
        payslip.ThucNhan = tongGross - payslip.TongKhauTru;
    }

    public Task<bool> PublishDraftPayslipsAsync(int maKyLuong, DateTimeOffset hanPhanHoi, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => PublishDraftPayslipsCoreAsync(maKyLuong, hanPhanHoi, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> PublishDraftPayslipsCoreAsync(int maKyLuong, DateTimeOffset hanPhanHoi, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        EnsureWritablePeriod(period);

        if (period.PhieuLuongs.Count == 0)
        {
            throw new InvalidOperationException("Chưa có phiếu lương nào trong kỳ để phát hành bản nháp. Vui lòng tính lương trước.");
        }

        period.TrangThai = SalaryConstants.PERIOD_DRAFT_REVIEW;
        period.LyDoTuChoi = null;
        period.NgayPhatHanhNhap = DateTimeOffset.UtcNow;
        period.NguoiPhatHanhNhap = nguoiThucHien;
        period.HanPhanHoiNhap = hanPhanHoi;

        foreach (var p in period.PhieuLuongs)
        {
            p.TrangThai = SalaryConstants.PAYSLIP_DRAFT;
        }

        _context.NhatKyQuanTris.Add(new NhatKyQuanTri
        {
            MaTaiKhoanThucHien = maTaiKhoan,
            TenNguoiThucHien = nguoiThucHien,
            TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
            HanhDong = "PHAT_HANH_PHIEU_LUONG_NHAP",
            NoiDung = $"HR phát hành phiếu lương nháp kỳ {period.Thang:D2}/{period.Nam} cho {period.PhieuLuongs.Count} nhân viên kiểm tra đối soát. Hạn phản hồi: {hanPhanHoi:dd/MM/yyyy HH:mm}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> SubmitForApprovalAsync(int maKyLuong, string nguoiGui, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => SubmitForApprovalCoreAsync(maKyLuong, nguoiGui, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> SubmitForApprovalCoreAsync(int maKyLuong, string nguoiGui, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        EnsureWritablePeriod(period);
        if (!period.NgayPhatHanhNhap.HasValue)
            throw new InvalidOperationException("Cần phát hành phiếu lương nháp cho nhân viên trước khi gửi duyệt.");
        if (await OutstandingSettlements(period.MaKyLuong).AnyAsync(cancellationToken))
            throw new InvalidOperationException("Còn quyết toán sự kiện chưa được kết chuyển. Vui lòng tính lại lương trước khi gửi duyệt.");
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

    public Task<bool> ApprovePayrollAsync(int maKyLuong, string nguoiDuyet, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => ApprovePayrollCoreAsync(maKyLuong, nguoiDuyet, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> ApprovePayrollCoreAsync(int maKyLuong, string nguoiDuyet, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (!period.NgayGuiDuyet.HasValue || period.NgayDuyet.HasValue || !string.IsNullOrEmpty(period.LyDoTuChoi))
        {
            throw new InvalidOperationException("Chỉ bảng lương ở trạng thái Chờ duyệt mới được phép phê duyệt.");
        }

        period.TrangThai = SalaryConstants.PERIOD_OPEN;
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

    public Task<bool> RejectPayrollAsync(int maKyLuong, string nguoiTuChoi, string lyDo, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => RejectPayrollCoreAsync(maKyLuong, nguoiTuChoi, lyDo, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> RejectPayrollCoreAsync(int maKyLuong, string nguoiTuChoi, string lyDo, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new ArgumentException("Bắt buộc phải nhập lý do từ chối bảng lương.");
        }

        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (!period.NgayGuiDuyet.HasValue || period.NgayDuyet.HasValue || !string.IsNullOrEmpty(period.LyDoTuChoi))
        {
            throw new InvalidOperationException("Chỉ bảng lương ở trạng thái Chờ duyệt mới có thể từ chối.");
        }

        period.TrangThai = SalaryConstants.PERIOD_OPEN;
        period.LyDoTuChoi = lyDo.Trim();
        period.NguoiDuyet = nguoiTuChoi;
        period.NgayDuyet = null;
        period.NgayGuiDuyet = null;

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

    public Task<bool> LockPayrollAsync(int maKyLuong, string nguoiKhoa, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => LockPayrollCoreAsync(maKyLuong, nguoiKhoa, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> LockPayrollCoreAsync(int maKyLuong, string nguoiKhoa, int? maTaiKhoan, CancellationToken cancellationToken)
    {
        var period = await _context.KyLuongs
            .Include(k => k.PhieuLuongs)
            .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);

        if (period == null) return false;
        if (!period.NgayDuyet.HasValue)
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

    public Task<bool> PublishDirectlyAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(async () =>
        {
            var period = await _context.KyLuongs.Include(k => k.PhieuLuongs)
                .FirstOrDefaultAsync(k => k.MaKyLuong == maKyLuong, cancellationToken);
            if (period == null) return false;
            if (period.TrangThai == SalaryConstants.PERIOD_LOCKED)
                return await PublishPayslipsCoreAsync(maKyLuong, nguoiThucHien, maTaiKhoan, cancellationToken);
            if (!period.NgayDuyet.HasValue)
                throw new InvalidOperationException("Bảng lương phải được duyệt trước khi phát hành.");
            if (period.PhieuLuongs.Count == 0)
                throw new InvalidOperationException("Chưa có phiếu lương trong kỳ. Vui lòng tính lương trước.");
            if (await OutstandingSettlements(period.MaKyLuong).AnyAsync(cancellationToken))
                throw new InvalidOperationException("Còn quyết toán sự kiện chưa kết chuyển. Vui lòng tính lại lương trước khi phát hành.");

            period.TrangThai = SalaryConstants.PERIOD_LOCKED;
            period.NgayKhoaSo = DateTimeOffset.UtcNow;
            period.NguoiKhoaSo = nguoiThucHien;
            foreach (var payslip in period.PhieuLuongs) payslip.TrangThai = SalaryConstants.PAYSLIP_PAID;
            _context.NhatKyQuanTris.Add(new NhatKyQuanTri
            {
                MaTaiKhoanThucHien = maTaiKhoan, TenNguoiThucHien = nguoiThucHien,
                TenTaiKhoanBiTacDong = $"KY_LUONG_{period.Thang}_{period.Nam}",
                HanhDong = "HR_PHAT_HANH_PHIEU_LUONG",
                NoiDung = $"HR khóa sổ và phát hành {period.PhieuLuongs.Count} phiếu lương kỳ {period.Thang:D2}/{period.Nam}"
            });
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<bool> PublishPayslipsAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default) =>
        ExecutePayrollWriteAsync(() => PublishPayslipsCoreAsync(maKyLuong, nguoiThucHien, maTaiKhoan, cancellationToken), cancellationToken);

    private async Task<bool> PublishPayslipsCoreAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken)
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

    private static readonly ChineseLunisolarCalendar LunarCalendar = new();

    private static bool IsHoliday(DateOnly date)
    {
        // 1. Danh mục ngày lễ Dương lịch cơ bản tại Việt Nam
        if (date.Month == 1 && date.Day == 1) return true; // Tết Dương lịch
        if (date.Month == 4 && date.Day == 30) return true; // Ngày Chiến thắng 30/4
        if (date.Month == 5 && date.Day == 1) return true; // Quốc tế Lao động 1/5
        if (date.Month == 9 && (date.Day == 1 || date.Day == 2 || date.Day == 3)) return true; // Quốc khánh 2/9

        // 2. Danh mục ngày lễ Âm lịch (Giỗ Tổ Hùng Vương 10/3 ÂL & Tết Nguyên Đán)
        try
        {
            var dt = date.ToDateTime(TimeOnly.MinValue);
            var lYear = LunarCalendar.GetYear(dt);
            var lMonth = LunarCalendar.GetMonth(dt);
            var lDay = LunarCalendar.GetDayOfMonth(dt);
            var leapMonth = LunarCalendar.GetLeapMonth(lYear);

            var actualMonth = lMonth;
            if (leapMonth > 0 && lMonth >= leapMonth)
            {
                if (lMonth == leapMonth)
                    actualMonth = -1; // Tháng nhuận không tính lễ chính
                else
                    actualMonth = lMonth - 1;
            }

            // Giỗ Tổ Hùng Vương: 10/3 Âm lịch
            if (actualMonth == 3 && lDay == 10) return true;

            // Tết Nguyên Đán: 29, 30 tháng Chạp đến hết mùng 5 tháng Giêng
            if (actualMonth == 12 && lDay >= 29) return true;
            if (actualMonth == 1 && lDay is >= 1 and <= 5) return true;
        }
        catch
        {
            // Tránh ngắt tiến trình tính lương nếu ngày vượt quá biên hỗ trợ của ChineseLunisolarCalendar
        }

        return false;
    }
private static readonly string[] ManagerTitleKeywords =
    [
        "Trưởng", "Giám sát", "Quản lý", "Giám đốc", "Tổ trưởng", "Quản đốc", "Trưởng ca", "Bếp trưởng", "Leader", "Supervisor", "Manager", "Director"
    ];

    private static readonly string[] HazardDepartmentKeywords =
    [
        "Buồng", "Housekeeping", "Vệ sinh", "Laundry", "Giặt là"
    ];

    private static bool IsManagerPosition(string? title) =>
        !string.IsNullOrWhiteSpace(title) && ManagerTitleKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static bool IsHazardDepartment(string? deptName) =>
        !string.IsNullOrWhiteSpace(deptName) && HazardDepartmentKeywords.Any(k => deptName.Contains(k, StringComparison.OrdinalIgnoreCase));
}
