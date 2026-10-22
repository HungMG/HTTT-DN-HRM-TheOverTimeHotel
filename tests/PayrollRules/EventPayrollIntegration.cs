using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;

internal static class EventPayrollIntegration
{
    public static async Task<int> Run()
    {
        var connection = Environment.GetEnvironmentVariable("EVENT_TEST_CONNECTION")
            ?? throw new InvalidOperationException("EVENT_TEST_CONNECTION must identify a disposable test database.");
        if (new NpgsqlConnectionStringBuilder(connection).Database?.StartsWith("event_hourly_test_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Only the isolated event test database is permitted.");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        var failures = 0;
        async Task Check(string name, Func<AppDbContext, Task> test)
        {
            await using var db = new AppDbContext(options);
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO cau_hinh_khoan_luong(ma_khoan,ten_khoan,loai_khoan) VALUES
                        (4,'Payroll fixture attendance bonus','THUONG'),(5,'Payroll fixture service charge','THUONG') ON CONFLICT DO NOTHING;
                    INSERT INTO cau_hinh_khau_tru(ma_khoan_tru,ten_khoan_tru,loai_khoan_tru) VALUES
                        (1,'Payroll fixture insurance','BAO_HIEM'),(4,'Payroll fixture tax','THUE'),(5,'Payroll fixture fine','VI_PHAM') ON CONFLICT DO NOTHING;
                    INSERT INTO cau_hinh_phu_cap(ma_phu_cap,ten_phu_cap,loai_tinh) VALUES
                        (1,'Payroll fixture meal','THEO_NGAY_CONG'),(2,'Payroll fixture management','CO_DINH_THANG'),
                        (3,'Payroll fixture telephone','CO_DINH_THANG'),(4,'Payroll fixture housekeeping','THEO_NGAY_CONG') ON CONFLICT DO NOTHING;
                    INSERT INTO cau_hinh_tang_ca(ma_loai_ot,ten_loai_ot,he_so) VALUES
                        (1,'Payroll fixture regular OT',1.5),(2,'Payroll fixture weekend OT',2),
                        (3,'Payroll fixture holiday OT',3),(4,'Payroll fixture night shift',0.3) ON CONFLICT DO NOTHING;
                    """);
                await test(db); Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex}"); }
            finally { await transaction.RollbackAsync(); }
        }
        await Check("Approved periods reject calculation without changing existing payslips", async db =>
        {
            var period = Period(); period.TrangThai = "DA_DUYET";
            var employee = Employee();
            db.AddRange(period, employee); await db.SaveChangesAsync();
            var slip = new PhieuLuong { MaNhanVien = employee.MaNhanVien, MaKyLuong = period.MaKyLuong, TongThuong = 123m, ThucNhan = 123m };
            db.Add(slip); await db.SaveChangesAsync();
            await Reject(() => new PayrollCalculationService(db).CalculatePayrollAsync(period.Thang, period.Nam, "test", null));
            Require(await db.PhieuLuongs.AnyAsync(p => p.MaPhieuLuong == slip.MaPhieuLuong && p.ThucNhan == 123m), "Existing approved payslip was changed.");
        });
        await Check("Retired contractless employees receive frozen event pay once through full and individual recalculation", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            var payroll = new PayrollCalculationService(db);
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var slip = await db.PhieuLuongs.AsNoTracking().SingleAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien);
            Require(slip.LuongCoBan == 0m && slip.TienLuongCong == 0m && slip.TongThuong == 123456m && slip.ThucNhan == 123456m, "Frozen event pay or bonus-only payslip is wrong.");
            Require(await payroll.RecalculateEmployeeAsync(slip.MaPhieuLuong, "test", null), "Individual recalculation rejected event-only employee.");
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var rows = await db.ChiTietPhieuLuongs.AsNoTracking().Where(d => d.PhieuLuong!.MaKyLuong == period.MaKyLuong && d.PhieuLuong.MaNhanVien == employee.MaNhanVien).ToListAsync();
            Require(rows.Count == 1 && rows[0].SoTien == 123456m, "Recalculation duplicated or repriced event pay.");
            Require(await db.QuyetToanSuKiens.AsNoTracking().AnyAsync(q => q.MaQuyetToan == settlement.MaQuyetToan && q.MaChiTiet == rows[0].MaChiTiet), "Settlement link was not rebuilt.");
            await payroll.DeleteSalaryItemAsync(rows[0].MaChiTiet, "test", null);
            Require(await db.QuyetToanSuKiens.AsNoTracking().AnyAsync(q => q.MaQuyetToan == settlement.MaQuyetToan && q.MaChiTiet == null), "Deleting payroll detail did not reset settlement link.");
        });
        await Check("Only completed event settlements assigned to the selected payout period are paid", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            settlement.DuAn!.TrangThai = "CHO_NGHIEM_THU"; await db.SaveChangesAsync();
            await new PayrollCalculationService(db).CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            Require(!await db.PhieuLuongs.AnyAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien), "Unaccepted event was paid.");
            settlement.DuAn.TrangThai = "HOAN_THANH"; await db.SaveChangesAsync();
            await new PayrollCalculationService(db).CalculatePayrollAsync(11, period.Nam, "test", null);
            Require(!await db.PhieuLuongs.AnyAsync(p => p.KyLuong!.Thang == 11 && p.KyLuong.Nam == period.Nam && p.MaNhanVien == employee.MaNhanVien), "Settlement was paid in an unassigned period.");
        });
        await Check("Pending approved and locked payroll reject every money mutation", async db =>
        {
            var (period, employee, _) = await Settlement(db);
            var payroll = new PayrollCalculationService(db);
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var slip = await db.PhieuLuongs.AsNoTracking().SingleAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien);
            var detail = await db.ChiTietPhieuLuongs.AsNoTracking().SingleAsync(d => d.MaPhieuLuong == slip.MaPhieuLuong);
            foreach (var status in new[] { "CHO_DUYET", "DA_DUYET", "KHOA_SO" })
            {
                await db.KyLuongs.Where(p => p.MaKyLuong == period.MaKyLuong).ExecuteUpdateAsync(s => s.SetProperty(p => p.TrangThai, status));
                await Reject(() => payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null));
                await Reject(() => payroll.RecalculateEmployeeAsync(slip.MaPhieuLuong, "test", null));
                await Reject(() => payroll.AdjustSalaryItemAsync(slip.MaPhieuLuong, "THUONG", "manual", 1m, null, "test", null));
                await Reject(() => payroll.DeleteSalaryItemAsync(detail.MaChiTiet, "test", null));
                await Reject(() => payroll.SubmitForApprovalAsync(period.MaKyLuong, "test", null));
            }
        });
        await Check("Failure after payroll deletion and replacement rolls back details and settlement links", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            var payroll = new PayrollCalculationService(db);
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var slip = await db.PhieuLuongs.AsNoTracking().SingleAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien);
            var originalDetail = await db.ChiTietPhieuLuongs.AsNoTracking().SingleAsync(d => d.MaPhieuLuong == slip.MaPhieuLuong);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION pg_temp.reject_payroll_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Injected payroll failure'; END $$;
                CREATE TRIGGER payroll_test_failure BEFORE INSERT ON public.nhat_ky_quan_tri
                FOR EACH ROW WHEN (NEW.hanh_dong = 'TINH_LUONG_TU_DONG') EXECUTE FUNCTION pg_temp.reject_payroll_audit();
                """);
            var failed = false;
            try { await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null); }
            catch (DbUpdateException) { failed = true; }
            Require(failed, "Injected payroll failure did not execute.");
            Require(await db.PhieuLuongs.AsNoTracking().AnyAsync(p => p.MaPhieuLuong == slip.MaPhieuLuong), "Failure deleted the original payslip.");
            Require(await db.ChiTietPhieuLuongs.AsNoTracking().AnyAsync(p => p.MaChiTiet == originalDetail.MaChiTiet && p.SoTien == 123456m), "Failure deleted the frozen detail.");
            Require(await db.QuyetToanSuKiens.AsNoTracking().AnyAsync(q => q.MaQuyetToan == settlement.MaQuyetToan && q.MaChiTiet == originalDetail.MaChiTiet), "Failure changed the settlement link.");
        });
        await Check("Submit refuses an outstanding accepted settlement and succeeds after calculation", async db =>
        {
            var (period, employee, _) = await Settlement(db);
            var payroll = new PayrollCalculationService(db);
            db.Add(new PhieuLuong { MaNhanVien = employee.MaNhanVien, MaKyLuong = period.MaKyLuong });
            await db.SaveChangesAsync();
            await Reject(() => payroll.SubmitForApprovalAsync(period.MaKyLuong, "test", null));
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            Require(await payroll.SubmitForApprovalAsync(period.MaKyLuong, "test", null), "Calculated payroll could not be submitted.");
            Require(await payroll.RejectPayrollAsync(period.MaKyLuong, "test", "recheck", null), "Pending payroll could not be rejected.");
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            Require(await payroll.SubmitForApprovalAsync(period.MaKyLuong, "test", null), "Rejected payroll could not be calculated and resubmitted.");
            Require(await payroll.ApprovePayrollAsync(period.MaKyLuong, "test", null), "Pending payroll could not be approved.");
            Require(await payroll.LockPayrollAsync(period.MaKyLuong, "test", null), "Approved payroll could not be locked.");
            Require(await payroll.PublishPayslipsAsync(period.MaKyLuong, "test", null), "Locked payroll could not be published.");
        });
        await Check("Legacy pay requires explicit reconciliation and consumes the frozen amount once", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            var eventId = settlement.MaDuAn;
            settlement.DuAn!.TinhTheoGio = false;
            var assignment = new PhanCongDuAn
            {
                MaDuAn = eventId, MaNhanVien = employee.MaNhanVien, VaiTroDuAn = "Fixture",
                BatDau = settlement.DuAn.NgayBatDau, KetThuc = settlement.DuAn.NgayKetThuc,
                SoTienPhuCap = 432123m, PhuCapDuKien = 999999m, DanhGia = "XUAT_SAC"
            };
            db.Add(assignment); db.Remove(settlement); await db.SaveChangesAsync();
            var payroll = new PayrollCalculationService(db);
            await Reject(() => payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null));
            Require(!await db.PhieuLuongs.AnyAsync(p => p.MaKyLuong == period.MaKyLuong), "Unreconciled legacy event was paid.");
            db.Add(new QuyetToanSuKien
            {
                MaDuAn = eventId, MaNhanVien = employee.MaNhanVien, MaKyChiTra = period.MaKyLuong,
                ThangPhatSinh = new(2096, 9, 1), SoTien = 432123m, CanCuTinh = "{\"Policy\":\"LEGACY_RECONCILED\"}",
                MaNguoiDuyet = employee.MaNhanVien, NgayDuyet = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var rows = await db.ChiTietPhieuLuongs.AsNoTracking().Where(d => d.PhieuLuong!.MaKyLuong == period.MaKyLuong && d.PhieuLuong.MaNhanVien == employee.MaNhanVien).ToListAsync();
            Require(rows.Count == 1 && rows[0].SoTien == 432123m, "Legacy amount was repriced or paid from both sources.");
        });
        await Check("Historical legacy links prevent recalculation and direct detail deletion", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            var payroll = new PayrollCalculationService(db);
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            var slip = await db.PhieuLuongs.AsNoTracking().SingleAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien);
            var detail = await db.ChiTietPhieuLuongs.AsNoTracking().SingleAsync(d => d.MaPhieuLuong == slip.MaPhieuLuong);
            var item = await db.DuAns.SingleAsync(e => e.MaDuAn == settlement.MaDuAn);
            item.TinhTheoGio = false;
            db.Add(new PhanCongDuAn
            {
                MaDuAn = item.MaDuAn, MaNhanVien = employee.MaNhanVien, VaiTroDuAn = "Fixture",
                BatDau = item.NgayBatDau, KetThuc = item.NgayKetThuc, SoTienPhuCap = 123456m,
                DanhGia = "HOAN_THANH", TrangThaiChiTra = "DA_KET_CHUYEN_LUONG", MaChiTiet = detail.MaChiTiet
            });
            await db.SaveChangesAsync();
            await Reject(() => payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null));
            await Reject(() => payroll.RecalculateEmployeeAsync(slip.MaPhieuLuong, "test", null));
            await Reject(() => payroll.DeleteSalaryItemAsync(detail.MaChiTiet, "test", null));
            Require(await db.ChiTietPhieuLuongs.AsNoTracking().AnyAsync(d => d.MaChiTiet == detail.MaChiTiet && d.SoTien == 123456m), "Historical legacy pay changed.");
        });
        await Check("Canceled legacy assignments do not require payout reconciliation", async db =>
        {
            var (period, employee, settlement) = await Settlement(db);
            settlement.DuAn!.TinhTheoGio = false;
            db.Add(new PhanCongDuAn
            {
                MaDuAn = settlement.MaDuAn, MaNhanVien = employee.MaNhanVien, VaiTroDuAn = "Fixture",
                BatDau = settlement.DuAn.NgayBatDau, KetThuc = settlement.DuAn.NgayKetThuc,
                SoTienPhuCap = 123456m, DanhGia = "HOAN_THANH", TrangThaiThamGia = "HUY_PHAN_CONG"
            });
            db.Remove(settlement); await db.SaveChangesAsync();
            await new PayrollCalculationService(db).CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            Require(!await db.PhieuLuongs.AnyAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien), "Canceled legacy assignment generated payroll.");
        });
        await Check("Canceled source shifts contribute no base credit absence overtime night allowance or fines", async db =>
        {
            var period = Period(); var employee = Employee(); employee.TrangThai = "DANG_LAM";
            db.AddRange(period, employee); await db.SaveChangesAsync();
            db.Add(new HopDong
            {
                MaNhanVien = employee.MaNhanVien, SoHopDong = $"P-{Guid.NewGuid():N}",
                LoaiHopDong = "KHONG_XAC_DINH", NgayBatDau = new(2096, 1, 1), LuongCoBan = 10400000m
            });
            var day = new CaLamViec { TenCa = $"PD-{Guid.NewGuid():N}", GioBatDau = new(8, 0), GioKetThuc = new(16, 0) };
            var night = new CaLamViec { TenCa = $"PN-{Guid.NewGuid():N}", GioBatDau = new(22, 0), GioKetThuc = new(6, 0), QuaDem = true };
            db.AddRange(day, night); await db.SaveChangesAsync();
            PhanCa Shift(int date, CaLamViec template, string status, bool worked) => new()
            {
                MaNhanVien = employee.MaNhanVien, MaCa = template.MaCa, NgayLamViec = new(2096, 10, date),
                BatDauDuKien = new(2096, 10, date, 8, 0, 0, TimeSpan.Zero), KetThucDuKien = new(2096, 10, date, 16, 0, 0, TimeSpan.Zero),
                TrangThai = status,
                ChamCong = worked ? new() { GioCheckIn = new(2096, 10, date, 8, 0, 0, TimeSpan.Zero), GioCheckOut = new(2096, 10, date, 16, 0, 0, TimeSpan.Zero) } : null
            };
            var regular = Shift(2, day, "HOAN_THANH", true);
            var canceled = Shift(3, night, "DA_HUY", true);
            canceled.ChamCong!.GioTangCaOt = 2m; canceled.ChamCong.PhutDiTre = 10; canceled.ChamCong.PhutVeSom = 20;
            db.AddRange(regular, canceled, Shift(4, day, "DA_HUY", false)); await db.SaveChangesAsync();
            var summary = await new AttendanceSummaryService(db).GetAsync(employee.MaNhanVien, period.TuNgay, period.DenNgay);
            Require(summary is { ScheduledDays: 1, WorkedDays: 1, TotalCreditedDays: 1 }, "Canceled source shift changed attendance credit or scheduled absence count.");
            var payroll = new PayrollCalculationService(db);
            await payroll.CalculatePayrollAsync(period.Thang, period.Nam, "test", null);
            async Task<PhieuLuong> Payslip() => await db.PhieuLuongs.AsNoTracking().Include(p => p.ChiTietPhieuLuongs)
                .SingleAsync(p => p.MaKyLuong == period.MaKyLuong && p.MaNhanVien == employee.MaNhanVien);
            void CheckSlip(PhieuLuong slip)
            {
                Require(slip.NgayCongThucTe == 1m && slip.TienLuongCong == 400000m, "Canceled source shift changed normal base pay.");
                Require(slip.TienTangCa == 0m && slip.TongPhuCap == 30000m, "Canceled source shift generated overtime or night allowance.");
                Require(slip.ChiTietPhieuLuongs.All(d => d.MaKhoanTru != 5), "Canceled source shift generated an attendance fine.");
            }
            var full = await Payslip(); CheckSlip(full);
            Require(await payroll.RecalculateEmployeeAsync(full.MaPhieuLuong, "test", null), "Individual recalculation failed.");
            CheckSlip(await Payslip());
        });
        return failures == 0 ? 0 : 1;
    }

    private static KyLuong Period() => new() { Thang = 10, Nam = 2096, TuNgay = new(2096, 10, 1), DenNgay = new(2096, 10, 31), TrangThai = "NHAP" };
    private static NhanVien Employee() => new() { Ho = "Payroll", Ten = "Fixture", NgaySinh = new(1990, 1, 1), NgayVaoLam = new(2020, 1, 1), TrangThai = "DA_THOI_VIEC", TrinhDo = "DAI_HOC", DienThoai = Guid.NewGuid().ToString("N")[..10], EmailCongVu = $"payroll-{Guid.NewGuid():N}@example.test", PhongBan = new() { TenPhongBan = $"Payroll-{Guid.NewGuid():N}" }, ChucVu = new() { TenChucVu = $"Payroll-{Guid.NewGuid():N}" } };
    private static async Task<(KyLuong, NhanVien, QuyetToanSuKien)> Settlement(AppDbContext db)
    {
        var period = Period(); var employee = Employee();
        db.AddRange(period, employee); await db.SaveChangesAsync();
        var item = new DuAn { TenDuAn = $"Payroll-{Guid.NewGuid():N}", DiaDiem = "Test", NgayBatDau = DateTimeOffset.UtcNow.AddDays(-2), NgayKetThuc = DateTimeOffset.UtcNow.AddDays(-1), MaNguoiQuanLy = employee.MaNhanVien, TrangThai = "HOAN_THANH", TinhTheoGio = true };
        db.Add(item); await db.SaveChangesAsync();
        var settlement = new QuyetToanSuKien { MaDuAn = item.MaDuAn, DuAn = item, MaNhanVien = employee.MaNhanVien, MaKyChiTra = period.MaKyLuong, ThangPhatSinh = new(2096, 9, 1), SoPhut = 120m, SoTien = 123456m, CanCuTinh = "[]", MaNguoiDuyet = employee.MaNhanVien, NgayDuyet = DateTimeOffset.UtcNow };
        db.Add(settlement); await db.SaveChangesAsync();
        return (period, employee, settlement);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Reject(Func<Task> action) { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Unsafe payroll mutation was accepted."); }
}

