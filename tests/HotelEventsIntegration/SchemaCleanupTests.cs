using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;

static class SchemaCleanupTests
{
    private const string Migration = "20261026_event_schema_cleanup";

    private static void Guard(string connection)
    {
        var name = new NpgsqlConnectionStringBuilder(connection).Database ?? "";
        if (!Regex.IsMatch(name, "^event_hourly_test_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Schema tests require the guarded disposable database.");
    }

    public static async Task VerifySchema(Fixture f)
    {
        await using var db = f.Open();
        Guard(db.Database.GetConnectionString()!);
        var obsolete = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name AS "Value" FROM information_schema.columns
            WHERE table_schema='public' AND (
              (table_name='du_an' AND column_name IN ('ma_dia_diem','phut_chuan_bi','phut_thu_don')) OR
              (table_name='nhu_cau_su_kien' AND column_name IN ('ten_khung','bat_dau','ket_thuc')) OR
              (table_name='cong_su_kien' AND column_name IN ('giai_trinh','xac_nhan_ngoai_le')))
            """).ToListAsync();
        Require(obsolete.Count == 0, "Obsolete columns remain: " + string.Join(", ", obsolete));
    }

    public static async Task RunMigration(Fixture f, string root)
    {
        await using var db = f.Open();
        Guard(db.Database.GetConnectionString()!);
        var folder = Path.Combine(root, "src", "OvertimeHotel.HRM.Data", "migrations");
        async Task Apply(string suffix) => await db.Database.ExecuteSqlRawAsync(
            await File.ReadAllTextAsync(Path.Combine(folder, Migration + suffix + ".sql")));
        await Apply(".rollback");
        var date = DateTimeOffset.UtcNow.AddDays(10);
        var month = new DateOnly(date.Year, date.Month, 1);
        var period = await db.KyLuongs.SingleOrDefaultAsync(k => k.Thang == month.Month && k.Nam == month.Year);
        if (period == null)
        {
            period = new KyLuong { Thang = month.Month, Nam = month.Year, TuNgay = month, DenNgay = month.AddMonths(1).AddDays(-1) };
            db.KyLuongs.Add(period); await db.SaveChangesAsync();
            HourlyOperationsTests.TrackPeriod(period.MaKyLuong);
        }
        var payslip = new PhieuLuong { MaNhanVien = f.WorkerA, MaKyLuong = period.MaKyLuong };
        var detail = new ChiTietPhieuLuong { PhieuLuong = payslip, TenKhoanLuu = "Historical event", LoaiKhoanLuu = "THUONG", LoaiKhoan = "THUONG", SoTien = 123_456m };
        var venue = new DiaDiemSuKien { TenDiaDiem = f.Prefix + "-old-venue", SucChua = 300 };
        var item = new DuAn { TenDuAn = f.Prefix + "-schema", LoaiDuAn = "HOI_NGHI_MICE", DiaDiem = "Historical hall",
            MaNguoiQuanLy = f.HR.EmployeeId, NgayBatDau = date, NgayKetThuc = date.AddHours(2), TrangThai = "HOAN_THANH" };
        var assignment = new PhanCongDuAn { DuAn = item, MaNhanVien = f.WorkerA, VaiTroDuAn = "Phụ trách âm thanh và micro",
            BatDau = date, KetThuc = date.AddHours(2), PhuCapDuKien = 126_000m, SoTienPhuCap = 123_456m,
            TrangThaiChiTra = "DA_KET_CHUYEN_LUONG", DanhGia = "HOAN_THANH" };
        var work = new CongSuKien { PhanCong = assignment, BatDau = date, KetThuc = date.AddHours(1),
            MaNguoiXacNhan = f.HR.EmployeeId, NgayXacNhan = DateTimeOffset.UtcNow };
        db.AddRange(detail, venue, work); await db.SaveChangesAsync();
        assignment.MaChiTiet = detail.MaChiTiet; await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE du_an SET ma_dia_diem={venue.MaDiaDiem},phut_chuan_bi=45,phut_thu_don=30 WHERE ma_du_an={item.MaDuAn}");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhu_cau_su_kien(ma_du_an,ten_khung,ma_phong_ban,vai_tro,so_nguoi,bat_dau,ket_thuc,la_mau) VALUES({item.MaDuAn},'Historical need','KY_THUAT','Phụ trách âm thanh và micro',2,{date},{date.AddHours(2)},false)");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cong_su_kien SET giai_trinh='Historical explanation',xac_nhan_ngoai_le=true WHERE ma_cong={work.MaCong}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE nhu_cau_su_kien SET ket_thuc={date.AddHours(1)} WHERE ma_du_an={item.MaDuAn}");
        try { await Apply(""); throw new InvalidOperationException("Migration discarded a partial staffing window."); }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            await db.Database.ExecuteSqlRawAsync("ROLLBACK");
        }
        Require(!await db.NhatKyQuanTris.AnyAsync(a => a.TenTaiKhoanBiTacDong == $"SK-{item.MaDuAn}" && a.HanhDong == "TINH_GON_CAU_TRUC_SU_KIEN"),
            "A rejected migration left partial archive data.");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE nhu_cau_su_kien SET ket_thuc={date.AddHours(2)} WHERE ma_du_an={item.MaDuAn}");
        await Apply("");
        await VerifySchema(f);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhu_cau_su_kien(ma_du_an,ma_phong_ban,vai_tro,so_nguoi,la_mau) VALUES({item.MaDuAn},'KY_THUAT','Phụ trách âm thanh và micro',2,false)");
            throw new InvalidOperationException("Database allowed duplicate whole-event staffing roles.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") { }
        var audits = await db.NhatKyQuanTris.AsNoTracking().Where(a => a.TenTaiKhoanBiTacDong == $"SK-{item.MaDuAn}"
            && a.HanhDong == "TINH_GON_CAU_TRUC_SU_KIEN").ToListAsync();
        Require(audits.Count == 3 && audits.Any(a => a.GiaTriCu!.Contains("Historical explanation"))
            && audits.Any(a => a.GiaTriCu!.Contains("Historical need")) && audits.Any(a => a.GiaTriCu!.Contains("45")), "Removed values were not archived.");
        await Apply("");
        Require(await db.NhatKyQuanTris.CountAsync(a => a.TenTaiKhoanBiTacDong == $"SK-{item.MaDuAn}" && a.HanhDong == "TINH_GON_CAU_TRUC_SU_KIEN") == 3,
            "Rerunning migration duplicated archive records.");
        var paid = await db.PhanCongDuAns.AsNoTracking().SingleAsync(a => a.MaPhanCong == assignment.MaPhanCong);
        Require(paid.MaChiTiet == detail.MaChiTiet && paid.SoTienPhuCap == 123_456m && paid.PhuCapDuKien == 126_000m
            && paid.TrangThaiChiTra == "DA_KET_CHUYEN_LUONG" && await db.QuyetToanSuKiens.CountAsync(q => q.MaDuAn == item.MaDuAn) == 0,
            "Migration changed paid amounts, payroll links or created a second payment.");
        await Apply(".rollback");
        var restored = await db.Database.SqlQuery<int>($"SELECT phut_chuan_bi AS \"Value\" FROM du_an WHERE ma_du_an={item.MaDuAn}").SingleAsync();
        Require(restored == 45, "Rollback did not restore archived event data.");
        var explanation = await db.Database.SqlQuery<string>($"SELECT giai_trinh AS \"Value\" FROM cong_su_kien WHERE ma_cong={work.MaCong}").SingleAsync();
        Require(explanation == "Historical explanation", "Rollback did not restore archived work data.");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE du_an SET ma_dia_diem=NULL,phut_chuan_bi=0,phut_thu_don=0 WHERE ma_du_an={item.MaDuAn}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cong_su_kien SET giai_trinh=NULL,xac_nhan_ngoai_le=false WHERE ma_cong={work.MaCong}");
        await Apply("");
        await Apply(".rollback");
        var reset = await db.Database.SqlQuery<bool>($"SELECT ma_dia_diem IS NULL AND phut_chuan_bi=0 AND phut_thu_don=0 AS \"Value\" FROM du_an WHERE ma_du_an={item.MaDuAn}").SingleAsync();
        var resetWork = await db.Database.SqlQuery<bool>($"SELECT giai_trinh IS NULL AND NOT xac_nhan_ngoai_le AS \"Value\" FROM cong_su_kien WHERE ma_cong={work.MaCong}").SingleAsync();
        Require(reset && resetWork, "Repeated migration/rollback resurrected stale values after the old app reset them.");
        await Apply("");
        await VerifySchema(f);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
