using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Context;

internal sealed class HotelEventConfiguration : IEntityTypeConfiguration<DuAn>
{
    public void Configure(EntityTypeBuilder<DuAn> entity)
    {
        entity.ToTable("du_an");
        entity.HasKey(e => e.MaDuAn);
        entity.Property(e => e.MaDuAn).HasColumnName("ma_du_an");
        entity.Property(e => e.TenDuAn).HasColumnName("ten_du_an").HasMaxLength(200).IsRequired();
        entity.Property(e => e.LoaiDuAn).HasColumnName("loai_du_an").HasMaxLength(50).IsRequired();
        entity.Property(e => e.DiaDiem).HasColumnName("dia_diem").HasMaxLength(255).IsRequired();
        entity.Property(e => e.NgayBatDau).HasColumnName("ngay_bat_dau");
        entity.Property(e => e.NgayKetThuc).HasColumnName("ngay_ket_thuc");
        entity.Property(e => e.NganSachPhuCap).HasColumnName("ngan_sach_phu_cap").HasPrecision(15, 2);
        entity.Property(e => e.TinhPhuCapKhiNghiemThu).HasColumnName("tinh_phu_cap_khi_nghiem_thu").HasDefaultValue(false);
        entity.Property(e => e.MaNguoiQuanLy).HasColumnName("ma_nguoi_quan_ly");
        entity.Property(e => e.SoKhachDuKien).HasColumnName("so_khach_du_kien");
        entity.Property(e => e.MoTa).HasColumnName("mo_ta");
        entity.Property(e => e.LyDoHuy).HasColumnName("ly_do_huy").HasMaxLength(500);
        entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).IsRequired();
        entity.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken().HasDefaultValue(1L);
        entity.HasIndex(e => e.TenDuAn).IsUnique();
        entity.HasOne(e => e.NguoiQuanLy).WithMany().HasForeignKey(e => e.MaNguoiQuanLy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HotelEventAssignmentConfiguration : IEntityTypeConfiguration<PhanCongDuAn>
{
    public void Configure(EntityTypeBuilder<PhanCongDuAn> entity)
    {
        entity.ToTable("phan_cong_du_an");
        entity.HasKey(e => e.MaPhanCong);
        entity.Property(e => e.MaPhanCong).HasColumnName("ma_phan_cong");
        entity.Property(e => e.MaDuAn).HasColumnName("ma_du_an");
        entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
        entity.Property(e => e.VaiTroDuAn).HasColumnName("vai_tro_du_an").HasMaxLength(100).IsRequired();
        entity.Property(e => e.MaPhongBanSuKien).HasColumnName("ma_phong_ban_su_kien").HasMaxLength(30);
        entity.Property(e => e.NhiemVuCuThe).HasColumnName("nhiem_vu_cu_the").HasMaxLength(255);
        entity.Property(e => e.PhuCapDuKien).HasColumnName("phu_cap_du_kien").HasPrecision(15, 2);
        entity.Property(e => e.SoTienPhuCap).HasColumnName("so_tien_phu_cap").HasPrecision(15, 2);
        entity.Property(e => e.BatDau).HasColumnName("bat_dau");
        entity.Property(e => e.KetThuc).HasColumnName("ket_thuc");
        entity.Property(e => e.DanhGia).HasColumnName("danh_gia").HasMaxLength(30).IsRequired();
        entity.Property(e => e.TrangThaiChiTra).HasColumnName("trang_thai_chi_tra").HasMaxLength(30).IsRequired();
        entity.Property(e => e.NgayDanhGia).HasColumnName("ngay_danh_gia");
        entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
        entity.HasIndex(e => new { e.MaDuAn, e.MaNhanVien }).IsUnique();
        entity.HasOne(e => e.DuAn).WithMany(e => e.PhanCongs).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.NhanVien).WithMany().HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ChiTietPhieuLuong>().WithMany().HasForeignKey(e => e.MaChiTiet).OnDelete(DeleteBehavior.SetNull);
    }
}
