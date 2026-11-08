using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Context;

internal static class EventOperationsConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var needs = Table<NhuCauSuKien>(model, "nhu_cau_su_kien");
        needs.HasKey(e => e.MaNhuCau);
        needs.HasIndex(e => new { e.MaDuAn, e.MaPhongBan, e.VaiTro }).IsUnique().HasDatabaseName("uq_event_need_role");
        needs.HasOne(e => e.DuAn).WithMany(e => e.NhuCaus).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade);
        needs.Property(e => e.MaPhongBan).HasMaxLength(30);
        needs.Property(e => e.VaiTro).HasMaxLength(100);

        var work = Table<CongSuKien>(model, "cong_su_kien");
        work.HasKey(e => e.MaCong);
        work.HasOne(e => e.PhanCong).WithMany(e => e.CongThucTes).HasForeignKey(e => e.MaPhanCong).OnDelete(DeleteBehavior.Restrict);

        var settlement = Table<QuyetToanSuKien>(model, "quyet_toan_su_kien");
        settlement.HasKey(e => e.MaQuyetToan);
        settlement.HasIndex(e => new { e.MaDuAn, e.MaNhanVien, e.ThangPhatSinh }).IsUnique();
        settlement.HasIndex(e => e.MaChiTiet).IsUnique();
        settlement.HasOne(e => e.DuAn).WithMany(e => e.QuyetToans).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Restrict);
        settlement.HasOne(e => e.NhanVien).WithMany().HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
        settlement.HasOne(e => e.KyChiTra).WithMany().HasForeignKey(e => e.MaKyChiTra).OnDelete(DeleteBehavior.Restrict);
        settlement.HasOne<ChiTietPhieuLuong>().WithMany().HasForeignKey(e => e.MaChiTiet).OnDelete(DeleteBehavior.SetNull);
        settlement.Property(e => e.SoPhut).HasPrecision(18, 6);
        settlement.Property(e => e.SoTien).HasPrecision(15, 2);

        var week = Table<CongBoLichTuan>(model, "cong_bo_lich_tuan");
        week.HasKey(e => e.DauTuan);
        week.Property(e => e.Version).IsConcurrencyToken();
        week.Property(e => e.LyDo).HasMaxLength(500);

        var events = model.Entity<DuAn>();
        events.Property(e => e.TinhTheoGio).HasColumnName("tinh_theo_gio");
        events.Property(e => e.KetThucThucTe).HasColumnName("ket_thuc_thuc_te");
        events.Property(e => e.LyDoDung).HasColumnName("ly_do_dung").HasMaxLength(500);
        events.Property(e => e.MaNguoiNghiemThu).HasColumnName("ma_nguoi_nghiem_thu");
        events.Property(e => e.NgayNghiemThu).HasColumnName("ngay_nghiem_thu");

        var assignment = model.Entity<PhanCongDuAn>();
        assignment.Property(e => e.MaPhanCa).HasColumnName("ma_phan_ca");
        assignment.Property(e => e.TrangThaiThamGia).HasColumnName("trang_thai_tham_gia").HasMaxLength(30);
        assignment.Property(e => e.LyDoKetThuc).HasColumnName("ly_do_ket_thuc").HasMaxLength(500);
        assignment.HasOne(e => e.PhanCa).WithMany().HasForeignKey(e => e.MaPhanCa).OnDelete(DeleteBehavior.Restrict);
        assignment.HasIndex(e => new { e.MaDuAn, e.MaNhanVien }).IsUnique(false);
        model.Entity<PhanCa>().Property(e => e.DaCongBo).HasColumnName("da_cong_bo").HasDefaultValue(true);
        model.Entity<LoaiDon>().Property(e => e.LaNghiPhep).HasColumnName("la_nghi_phep");
    }

    private static EntityTypeBuilder<T> Table<T>(ModelBuilder model, string name) where T : class
    {
        var builder = model.Entity<T>();
        builder.ToTable(name);
        foreach (var property in builder.Metadata.GetProperties())
            property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
        return builder;
    }
}
