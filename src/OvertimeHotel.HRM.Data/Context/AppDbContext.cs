using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // 17 DbSets ánh xạ 17 bảng CSDL chuẩn 3NF
    public DbSet<PhongBan> PhongBans => Set<PhongBan>();
    public DbSet<ChucVu> ChucVus => Set<ChucVu>();
    public DbSet<NhanVien> NhanViens => Set<NhanVien>();
    public DbSet<HopDong> HopDongs => Set<HopDong>();
    public DbSet<VaiTro> VaiTros => Set<VaiTro>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<Quyen> Quyens => Set<Quyen>();
    public DbSet<VaiTroQuyen> VaiTroQuyens => Set<VaiTroQuyen>();
    public DbSet<CaLamViec> CaLamViecs => Set<CaLamViec>();
    public DbSet<PhanCa> PhanCas => Set<PhanCa>();
    public DbSet<ChamCong> ChamCongs => Set<ChamCong>();
    public DbSet<LoaiDon> LoaiDons => Set<LoaiDon>();
    public DbSet<DonTu> DonTus => Set<DonTu>();
    public DbSet<KyLuong> KyLuongs => Set<KyLuong>();
    public DbSet<CauHinhKhoanLuong> CauHinhKhoanLuongs => Set<CauHinhKhoanLuong>();
    public DbSet<PhieuLuong> PhieuLuongs => Set<PhieuLuong>();
    public DbSet<ChiTietPhieuLuong> ChiTietPhieuLuongs => Set<ChiTietPhieuLuong>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. PHONG_BAN
        modelBuilder.Entity<PhongBan>(entity =>
        {
            entity.ToTable("phong_ban");
            entity.HasKey(e => e.MaPhongBan);
            entity.Property(e => e.MaPhongBan).HasColumnName("ma_phong_ban");
            entity.Property(e => e.TenPhongBan).HasColumnName("ten_phong_ban").HasMaxLength(150).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);
            entity.HasIndex(e => e.TenPhongBan).IsUnique();
        });

        // 2. CHUC_VU
        modelBuilder.Entity<ChucVu>(entity =>
        {
            entity.ToTable("chuc_vu");
            entity.HasKey(e => e.MaChucVu);
            entity.Property(e => e.MaChucVu).HasColumnName("ma_chuc_vu");
            entity.Property(e => e.TenChucVu).HasColumnName("ten_chuc_vu").HasMaxLength(100).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);
            entity.HasIndex(e => e.TenChucVu).IsUnique();
        });

        // 3. NHAN_VIEN
        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.ToTable("nhan_vien");
            entity.HasKey(e => e.MaNhanVien);
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaPhongBan).HasColumnName("ma_phong_ban");
            entity.Property(e => e.MaChucVu).HasColumnName("ma_chuc_vu");
            entity.Property(e => e.Ho).HasColumnName("ho").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Ten).HasColumnName("ten").HasMaxLength(50).IsRequired();
            entity.Property(e => e.NgaySinh).HasColumnName("ngay_sinh").IsRequired();
            entity.Property(e => e.GioiTinh).HasColumnName("gioi_tinh").HasMaxLength(10).IsRequired();
            entity.Property(e => e.DienThoai).HasColumnName("dien_thoai").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(e => e.DiaChi).HasColumnName("dia_chi").HasMaxLength(255);
            entity.Property(e => e.TrinhDo).HasColumnName("trinh_do").HasMaxLength(50).IsRequired();
            entity.Property(e => e.NgayVaoLam).HasColumnName("ngay_vao_lam").IsRequired();
            entity.Property(e => e.NgayThoiViec).HasColumnName("ngay_thoi_viec");
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("DANG_LAM").IsRequired();

            entity.Ignore(e => e.HoTen);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => new { e.Ten, e.Ho }).HasDatabaseName("idx_nhan_vien_ten");

            entity.HasOne(e => e.PhongBan)
                .WithMany(p => p.NhanViens)
                .HasForeignKey(e => e.MaPhongBan)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ChucVu)
                .WithMany(c => c.NhanViens)
                .HasForeignKey(e => e.MaChucVu)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. HOP_DONG
        modelBuilder.Entity<HopDong>(entity =>
        {
            entity.ToTable("hop_dong");
            entity.HasKey(e => e.MaHopDong);
            entity.Property(e => e.MaHopDong).HasColumnName("ma_hop_dong");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.SoHopDong).HasColumnName("so_hop_dong").HasMaxLength(50).IsRequired();
            entity.Property(e => e.LoaiHopDong).HasColumnName("loai_hop_dong").HasMaxLength(50).IsRequired();
            entity.Property(e => e.NgayBatDau).HasColumnName("ngay_bat_dau").IsRequired();
            entity.Property(e => e.NgayKetThuc).HasColumnName("ngay_ket_thuc");
            entity.Property(e => e.LuongCoBan).HasColumnName("luong_co_ban").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("HIEU_LUC").IsRequired();

            entity.HasIndex(e => e.SoHopDong).IsUnique();

            entity.HasOne(e => e.NhanVien)
                .WithMany(n => n.HopDongs)
                .HasForeignKey(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. VAI_TRO
        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.ToTable("vai_tro");
            entity.HasKey(e => e.MaVaiTro);
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.TenVaiTro).HasColumnName("ten_vai_tro").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);
            entity.HasIndex(e => e.TenVaiTro).IsUnique();
        });

        // 6. TAI_KHOAN
        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.ToTable("tai_khoan");
            entity.HasKey(e => e.MaTaiKhoan);
            entity.Property(e => e.MaTaiKhoan).HasColumnName("ma_tai_khoan");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.TenDangNhap).HasColumnName("ten_dang_nhap").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MatKhauBam).HasColumnName("mat_khau_bam").HasMaxLength(255).IsRequired();
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasDefaultValue(true);

            entity.HasIndex(e => e.TenDangNhap).IsUnique();
            entity.HasIndex(e => e.MaNhanVien).IsUnique();

            entity.HasOne(e => e.NhanVien)
                .WithOne(n => n.TaiKhoan)
                .HasForeignKey<TaiKhoan>(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VaiTro)
                .WithMany(v => v.TaiKhoans)
                .HasForeignKey(e => e.MaVaiTro)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. QUYEN
        modelBuilder.Entity<Quyen>(entity =>
        {
            entity.ToTable("quyen");
            entity.HasKey(e => e.MaQuyen);
            entity.Property(e => e.MaQuyen).HasColumnName("ma_quyen");
            entity.Property(e => e.MaQuyenCode).HasColumnName("ma_quyen_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TenQuyen).HasColumnName("ten_quyen").HasMaxLength(150).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);
            entity.HasIndex(e => e.MaQuyenCode).IsUnique();
        });

        // 8. VAI_TRO_QUYEN
        modelBuilder.Entity<VaiTroQuyen>(entity =>
        {
            entity.ToTable("vai_tro_quyen");
            entity.HasKey(e => new { e.MaVaiTro, e.MaQuyen });
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.MaQuyen).HasColumnName("ma_quyen");

            entity.HasOne(e => e.VaiTro)
                .WithMany(v => v.VaiTroQuyens)
                .HasForeignKey(e => e.MaVaiTro)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Quyen)
                .WithMany(q => q.VaiTroQuyens)
                .HasForeignKey(e => e.MaQuyen)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 9. CA_LAM_VIEC
        modelBuilder.Entity<CaLamViec>(entity =>
        {
            entity.ToTable("ca_lam_viec");
            entity.HasKey(e => e.MaCa);
            entity.Property(e => e.MaCa).HasColumnName("ma_ca");
            entity.Property(e => e.TenCa).HasColumnName("ten_ca").HasMaxLength(50).IsRequired();
            entity.Property(e => e.GioBatDau).HasColumnName("gio_bat_dau").IsRequired();
            entity.Property(e => e.GioKetThuc).HasColumnName("gio_ket_thuc").IsRequired();
            entity.Property(e => e.QuaDem).HasColumnName("qua_dem").HasDefaultValue(false);
            entity.HasIndex(e => e.TenCa).IsUnique();
        });

        // 10. PHAN_CA
        modelBuilder.Entity<PhanCa>(entity =>
        {
            entity.ToTable("phan_ca");
            entity.HasKey(e => e.MaPhanCa);
            entity.Property(e => e.MaPhanCa).HasColumnName("ma_phan_ca");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaCa).HasColumnName("ma_ca");
            entity.Property(e => e.NgayLamViec).HasColumnName("ngay_lam_viec").IsRequired();
            entity.Property(e => e.BatDauDuKien).HasColumnName("bat_dau_du_kien").IsRequired();
            entity.Property(e => e.KetThucDuKien).HasColumnName("ket_thuc_du_kien").IsRequired();
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("DA_PHAN").IsRequired();

            entity.HasOne(e => e.NhanVien)
                .WithMany(n => n.PhanCas)
                .HasForeignKey(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CaLamViec)
                .WithMany(c => c.PhanCas)
                .HasForeignKey(e => e.MaCa)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 11. CHAM_CONG
        modelBuilder.Entity<ChamCong>(entity =>
        {
            entity.ToTable("cham_cong");
            entity.HasKey(e => e.MaChamCong);
            entity.Property(e => e.MaChamCong).HasColumnName("ma_cham_cong");
            entity.Property(e => e.MaPhanCa).HasColumnName("ma_phan_ca");
            entity.Property(e => e.GioCheckIn).HasColumnName("gio_check_in");
            entity.Property(e => e.GioCheckOut).HasColumnName("gio_check_out");
            entity.Property(e => e.PhutDiTre).HasColumnName("phut_di_tre").HasDefaultValue(0);
            entity.Property(e => e.PhutVeSom).HasColumnName("phut_ve_som").HasDefaultValue(0);
            entity.Property(e => e.GioTangCaOt).HasColumnName("gio_tang_ca_ot").HasPrecision(4, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("DUNG_GIO").IsRequired();

            entity.HasIndex(e => e.MaPhanCa).IsUnique();

            entity.HasOne(e => e.PhanCa)
                .WithOne(p => p.ChamCong)
                .HasForeignKey<ChamCong>(e => e.MaPhanCa)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. LOAI_DON
        modelBuilder.Entity<LoaiDon>(entity =>
        {
            entity.ToTable("loai_don");
            entity.HasKey(e => e.MaLoaiDon);
            entity.Property(e => e.MaLoaiDon).HasColumnName("ma_loai_don");
            entity.Property(e => e.TenLoaiDon).HasColumnName("ten_loai_don").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CoHuongLuong).HasColumnName("co_huong_luong").HasDefaultValue(false);
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);
            entity.HasIndex(e => e.TenLoaiDon).IsUnique();
        });

        // 13. DON_TU
        modelBuilder.Entity<DonTu>(entity =>
        {
            entity.ToTable("don_tu");
            entity.HasKey(e => e.MaDon);
            entity.Property(e => e.MaDon).HasColumnName("ma_don");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaLoaiDon).HasColumnName("ma_loai_don");
            entity.Property(e => e.MaNguoiDuyet).HasColumnName("ma_nguoi_duyet");
            entity.Property(e => e.TuNgay).HasColumnName("tu_ngay").IsRequired();
            entity.Property(e => e.DenNgay).HasColumnName("den_ngay").IsRequired();
            entity.Property(e => e.LyDo).HasColumnName("ly_do").IsRequired();
            entity.Property(e => e.NgayGui).HasColumnName("ngay_gui").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("CHO_DUYET").IsRequired();
            entity.Property(e => e.PhanHoiDuyet).HasColumnName("phan_hoi_duyet").HasMaxLength(255);
            entity.Property(e => e.NgayDuyet).HasColumnName("ngay_duyet");

            entity.HasOne(e => e.NhanVien)
                .WithMany(n => n.DonTus)
                .HasForeignKey(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LoaiDon)
                .WithMany(l => l.DonTus)
                .HasForeignKey(e => e.MaLoaiDon)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.NguoiDuyet)
                .WithMany(n => n.DonTuDaDuyets)
                .HasForeignKey(e => e.MaNguoiDuyet)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 14. KY_LUONG
        modelBuilder.Entity<KyLuong>(entity =>
        {
            entity.ToTable("ky_luong");
            entity.HasKey(e => e.MaKyLuong);
            entity.Property(e => e.MaKyLuong).HasColumnName("ma_ky_luong");
            entity.Property(e => e.Thang).HasColumnName("thang").IsRequired();
            entity.Property(e => e.Nam).HasColumnName("nam").IsRequired();
            entity.Property(e => e.TuNgay).HasColumnName("tu_ngay").IsRequired();
            entity.Property(e => e.DenNgay).HasColumnName("den_ngay").IsRequired();
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("MO").IsRequired();

            entity.HasIndex(e => new { e.Thang, e.Nam }).IsUnique();
        });

        // 15. CAU_HINH_KHOAN_LUONG
        modelBuilder.Entity<CauHinhKhoanLuong>(entity =>
        {
            entity.ToTable("cau_hinh_khoan_luong");
            entity.HasKey(e => e.MaKhoan);
            entity.Property(e => e.MaKhoan).HasColumnName("ma_khoan");
            entity.Property(e => e.TenKhoan).HasColumnName("ten_khoan").HasMaxLength(150).IsRequired();
            entity.Property(e => e.LoaiKhoan).HasColumnName("loai_khoan").HasMaxLength(30).IsRequired();
            entity.Property(e => e.GiaTriMacDinh).HasColumnName("gia_tri_mac_dinh").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);

            entity.HasIndex(e => e.TenKhoan).IsUnique();
        });

        // 16. PHIEU_LUONG
        modelBuilder.Entity<PhieuLuong>(entity =>
        {
            entity.ToTable("phieu_luong");
            entity.HasKey(e => e.MaPhieuLuong);
            entity.Property(e => e.MaPhieuLuong).HasColumnName("ma_phieu_luong");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaKyLuong).HasColumnName("ma_ky_luong");
            entity.Property(e => e.LuongCoBan).HasColumnName("luong_co_ban").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.NgayCongThucTe).HasColumnName("ngay_cong_thuc_te").HasPrecision(4, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TienLuongCong).HasColumnName("tien_luong_cong").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TienTangCa).HasColumnName("tien_tang_ca").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TongPhuCap).HasColumnName("tong_phu_cap").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TongThuong).HasColumnName("tong_thuong").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.TongKhauTru).HasColumnName("tong_khau_tru").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.ThucNhan).HasColumnName("thuc_nhan").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("NHAP").IsRequired();

            entity.HasIndex(e => new { e.MaNhanVien, e.MaKyLuong }).IsUnique();

            entity.HasOne(e => e.NhanVien)
                .WithMany(n => n.PhieuLuongs)
                .HasForeignKey(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.KyLuong)
                .WithMany(k => k.PhieuLuongs)
                .HasForeignKey(e => e.MaKyLuong)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 17. CHI_TIET_PHIEU_LUONG
        modelBuilder.Entity<ChiTietPhieuLuong>(entity =>
        {
            entity.ToTable("chi_tiet_phieu_luong");
            entity.HasKey(e => e.MaChiTiet);
            entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
            entity.Property(e => e.MaPhieuLuong).HasColumnName("ma_phieu_luong");
            entity.Property(e => e.MaKhoan).HasColumnName("ma_khoan");
            entity.Property(e => e.TenKhoanLuu).HasColumnName("ten_khoan_luu").HasMaxLength(150).IsRequired();
            entity.Property(e => e.LoaiKhoanLuu).HasColumnName("loai_khoan_luu").HasMaxLength(30).IsRequired();
            entity.Property(e => e.SoTien).HasColumnName("so_tien").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu").HasMaxLength(255);

            entity.HasOne(e => e.PhieuLuong)
                .WithMany(p => p.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaPhieuLuong)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CauHinhKhoanLuong)
                .WithMany(c => c.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaKhoan)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
