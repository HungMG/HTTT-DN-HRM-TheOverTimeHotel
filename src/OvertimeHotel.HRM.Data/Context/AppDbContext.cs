using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // 25 DbSet ánh xạ 25 bảng CSDL chuẩn 3NF
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
    public DbSet<CauHinhTangCa> CauHinhTangCas => Set<CauHinhTangCa>();
    public DbSet<CauHinhPhuCap> CauHinhPhuCaps => Set<CauHinhPhuCap>();
    public DbSet<CauHinhKhauTru> CauHinhKhauTrus => Set<CauHinhKhauTru>();
    public DbSet<PhieuLuong> PhieuLuongs => Set<PhieuLuong>();
    public DbSet<ChiTietPhieuLuong> ChiTietPhieuLuongs => Set<ChiTietPhieuLuong>();
    public DbSet<ChiTietTruBaoHiem> ChiTietTruBaoHiems => Set<ChiTietTruBaoHiem>();
    public DbSet<ChiTietTruThueTncn> ChiTietTruThueTncns => Set<ChiTietTruThueTncn>();
    public DbSet<ChiTietTruViPhamTamUng> ChiTietTruViPhamTamUngs => Set<ChiTietTruViPhamTamUng>();
    public DbSet<NhatKyQuanTri> NhatKyQuanTris => Set<NhatKyQuanTri>();
    public DbSet<YeuCauTaiKhoan> YeuCauTaiKhoans => Set<YeuCauTaiKhoan>();

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
            entity.Property(e => e.MaPhongBan).HasColumnName("ma_phong_ban");
            entity.Property(e => e.TenChucVu).HasColumnName("ten_chuc_vu").HasMaxLength(100).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);

            entity.HasOne(e => e.PhongBan)
                .WithMany(p => p.ChucVus)
                .HasForeignKey(e => e.MaPhongBan)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(e => e.EmailCongVu).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EmailCaNhan).HasColumnName("email_ca_nhan").HasMaxLength(100);
            entity.Property(e => e.DiaChi).HasColumnName("dia_chi").HasMaxLength(255);
            entity.Property(e => e.TrinhDo).HasColumnName("trinh_do").HasMaxLength(50).IsRequired();
            entity.Property(e => e.NgayVaoLam).HasColumnName("ngay_vao_lam").IsRequired();
            entity.Property(e => e.NgayThoiViec).HasColumnName("ngay_thoi_viec");
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("DANG_LAM").IsRequired();

            entity.Ignore(e => e.HoTen);
            entity.HasIndex(e => e.EmailCongVu).IsUnique();
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
            entity.Property(e => e.HinhThucHopDong).HasColumnName("hinh_thuc_hop_dong").HasMaxLength(50).HasDefaultValue("CHINH_THUC").IsRequired();

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
            entity.Property(e => e.PhuongThucCheckIn).HasColumnName("phuong_thuc_checkin").HasMaxLength(20);
            entity.Property(e => e.ToaDoCheckIn).HasColumnName("toa_do_checkin").HasMaxLength(50);
            entity.Property(e => e.ThietBiCheckIn).HasColumnName("thiet_bi_checkin").HasMaxLength(100);

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

        // 17. CHI_TIET_PHIEU_LUONG (Bảng trung gian điều phối)
        modelBuilder.Entity<ChiTietPhieuLuong>(entity =>
        {
            entity.ToTable("chi_tiet_phieu_luong");
            entity.HasKey(e => e.MaChiTiet);
            entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
            entity.Property(e => e.MaPhieuLuong).HasColumnName("ma_phieu_luong");
            entity.Property(e => e.MaKhoan).HasColumnName("ma_khoan");
            entity.Property(e => e.MaLoaiOt).HasColumnName("ma_loai_ot");
            entity.Property(e => e.MaPhuCap).HasColumnName("ma_phu_cap");
            entity.Property(e => e.MaKhoanTru).HasColumnName("ma_khoan_tru");
            entity.Property(e => e.TenKhoanLuu).HasColumnName("ten_khoan_luu").HasMaxLength(150).IsRequired();
            entity.Property(e => e.LoaiKhoanLuu).HasColumnName("loai_khoan_luu").HasMaxLength(30).IsRequired();
            entity.Property(e => e.LoaiKhoan).HasColumnName("loai_khoan").HasMaxLength(30);
            entity.Property(e => e.SoTien).HasColumnName("so_tien").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu").HasMaxLength(255);

            entity.HasOne(e => e.PhieuLuong)
                .WithMany(p => p.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaPhieuLuong)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CauHinhKhoanLuong)
                .WithMany(c => c.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaKhoan)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CauHinhTangCa)
                .WithMany(c => c.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaLoaiOt)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CauHinhPhuCap)
                .WithMany(c => c.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaPhuCap)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CauHinhKhauTru)
                .WithMany(c => c.ChiTietPhieuLuongs)
                .HasForeignKey(e => e.MaKhoanTru)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 18. CAU_HINH_TANG_CA
        modelBuilder.Entity<CauHinhTangCa>(entity =>
        {
            entity.ToTable("cau_hinh_tang_ca");
            entity.HasKey(e => e.MaLoaiOt);
            entity.Property(e => e.MaLoaiOt).HasColumnName("ma_loai_ot");
            entity.Property(e => e.TenLoaiOt).HasColumnName("ten_loai_ot").HasMaxLength(100).IsRequired();
            entity.Property(e => e.HeSo).HasColumnName("he_so").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);

            entity.HasIndex(e => e.TenLoaiOt).IsUnique();
        });

        // 19. CAU_HINH_PHU_CAP
        modelBuilder.Entity<CauHinhPhuCap>(entity =>
        {
            entity.ToTable("cau_hinh_phu_cap");
            entity.HasKey(e => e.MaPhuCap);
            entity.Property(e => e.MaPhuCap).HasColumnName("ma_phu_cap");
            entity.Property(e => e.TenPhuCap).HasColumnName("ten_phu_cap").HasMaxLength(150).IsRequired();
            entity.Property(e => e.LoaiTinh).HasColumnName("loai_tinh").HasMaxLength(30).IsRequired();
            entity.Property(e => e.MucDinhMuc).HasColumnName("muc_dinh_muc").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.ChiuThueTncn).HasColumnName("chiu_thue_tncn").HasDefaultValue(false);
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);

            entity.HasIndex(e => e.TenPhuCap).IsUnique();
        });

        // 20. CAU_HINH_KHAU_TRU
        modelBuilder.Entity<CauHinhKhauTru>(entity =>
        {
            entity.ToTable("cau_hinh_khau_tru");
            entity.HasKey(e => e.MaKhoanTru);
            entity.Property(e => e.MaKhoanTru).HasColumnName("ma_khoan_tru");
            entity.Property(e => e.TenKhoanTru).HasColumnName("ten_khoan_tru").HasMaxLength(150).IsRequired();
            entity.Property(e => e.LoaiKhoanTru).HasColumnName("loai_khoan_tru").HasMaxLength(30).IsRequired();
            entity.Property(e => e.TyLeHoacDinhMuc).HasColumnName("ty_le_hoac_dinh_muc").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.MoTa).HasColumnName("mo_ta").HasMaxLength(255);

            entity.HasIndex(e => e.TenKhoanTru).IsUnique();
        });

        // 21. CHI_TIET_TRU_BAO_HIEM
        modelBuilder.Entity<ChiTietTruBaoHiem>(entity =>
        {
            entity.ToTable("chi_tiet_tru_bao_hiem");
            entity.HasKey(e => e.MaChiTietBh);
            entity.Property(e => e.MaChiTietBh).HasColumnName("ma_chi_tiet_bh");
            entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
            entity.Property(e => e.LuongDongBaoHiem).HasColumnName("luong_dong_bao_hiem").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TyLeBhxh).HasColumnName("ty_le_bhxh").HasPrecision(5, 2).HasDefaultValue(8.00m);
            entity.Property(e => e.TienBhxh).HasColumnName("tien_bhxh").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TyLeBhyt).HasColumnName("ty_le_bhyt").HasPrecision(5, 2).HasDefaultValue(1.50m);
            entity.Property(e => e.TienBhyt).HasColumnName("tien_bhyt").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TyLeBhtn).HasColumnName("ty_le_bhtn").HasPrecision(5, 2).HasDefaultValue(1.00m);
            entity.Property(e => e.TienBhtn).HasColumnName("tien_bhtn").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TongBaoHiem).HasColumnName("tong_bao_hiem").HasPrecision(15, 2).IsRequired();

            entity.HasOne(e => e.ChiTietPhieuLuong)
                .WithMany(c => c.ChiTietTruBaoHiems)
                .HasForeignKey(e => e.MaChiTiet)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 22. CHI_TIET_TRU_THUE_TNCN
        modelBuilder.Entity<ChiTietTruThueTncn>(entity =>
        {
            entity.ToTable("chi_tiet_tru_thue_tncn");
            entity.HasKey(e => e.MaChiTietThue);
            entity.Property(e => e.MaChiTietThue).HasColumnName("ma_chi_tiet_thue");
            entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
            entity.Property(e => e.ThuNhapChiuThue).HasColumnName("thu_nhap_chiu_thue").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.GiamTruBanThan).HasColumnName("giam_tru_ban_than").HasPrecision(15, 2).HasDefaultValue(11000000.00m);
            entity.Property(e => e.SoNguoiPhuThuoc).HasColumnName("so_nguoi_phu_thuoc").HasDefaultValue(0);
            entity.Property(e => e.GiamTruPhuThuoc).HasColumnName("giam_tru_phu_thuoc").HasPrecision(15, 2).HasDefaultValue(0.00m);
            entity.Property(e => e.ThuNhapTinhThue).HasColumnName("thu_nhap_tinh_thue").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.TienThue).HasColumnName("tien_thue").HasPrecision(15, 2).IsRequired();

            entity.HasOne(e => e.ChiTietPhieuLuong)
                .WithMany(c => c.ChiTietTruThueTncns)
                .HasForeignKey(e => e.MaChiTiet)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 23. CHI_TIET_TRU_VI_PHAM_TAM_UNG
        modelBuilder.Entity<ChiTietTruViPhamTamUng>(entity =>
        {
            entity.ToTable("chi_tiet_tru_vi_pham_tam_ung");
            entity.HasKey(e => e.MaChiTietVp);
            entity.Property(e => e.MaChiTietVp).HasColumnName("ma_chi_tiet_vp");
            entity.Property(e => e.MaChiTiet).HasColumnName("ma_chi_tiet");
            entity.Property(e => e.LoaiTru).HasColumnName("loai_tru").HasMaxLength(30).IsRequired();
            entity.Property(e => e.SoPhutViPham).HasColumnName("so_phut_vi_pham").HasDefaultValue(0);
            entity.Property(e => e.SoTienTru).HasColumnName("so_tien_tru").HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.LyDoChiTiet).HasColumnName("ly_do_chi_tiet").HasMaxLength(255).IsRequired();

            entity.HasOne(e => e.ChiTietPhieuLuong)
                .WithMany(c => c.ChiTietTruViPhamTamUngs)
                .HasForeignKey(e => e.MaChiTiet)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 24. NHAT_KY_QUAN_TRI
        modelBuilder.Entity<NhatKyQuanTri>(entity =>
        {
            entity.ToTable("nhat_ky_quan_tri");
            entity.HasKey(e => e.MaNhatKy);
            entity.Property(e => e.MaNhatKy).HasColumnName("ma_nhat_ky");
            entity.Property(e => e.MaTaiKhoanThucHien).HasColumnName("ma_tai_khoan_thuc_hien");
            entity.Property(e => e.MaTaiKhoanBiTacDong).HasColumnName("ma_tai_khoan_bi_tac_dong");
            entity.Property(e => e.TenNguoiThucHien).HasColumnName("ten_nguoi_thuc_hien").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TenTaiKhoanBiTacDong).HasColumnName("ten_tai_khoan_bi_tac_dong").HasMaxLength(100).IsRequired();
            entity.Property(e => e.HanhDong).HasColumnName("hanh_dong").HasMaxLength(40).IsRequired();
            entity.Property(e => e.NoiDung).HasColumnName("noi_dung").HasMaxLength(500).IsRequired();
            entity.Property(e => e.GiaTriCu).HasColumnName("gia_tri_cu").HasMaxLength(1000);
            entity.Property(e => e.GiaTriMoi).HasColumnName("gia_tri_moi").HasMaxLength(1000);
            entity.Property(e => e.LyDo).HasColumnName("ly_do").HasMaxLength(500);
            entity.Property(e => e.DiaChiIp).HasColumnName("dia_chi_ip").HasMaxLength(64);
            entity.Property(e => e.ThoiGian).HasColumnName("thoi_gian").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

            entity.HasIndex(e => e.ThoiGian).HasDatabaseName("idx_nhat_ky_quan_tri_thoi_gian");
            entity.HasIndex(e => e.MaTaiKhoanBiTacDong).HasDatabaseName("idx_nhat_ky_quan_tri_tai_khoan");

            entity.HasOne<TaiKhoan>()
                .WithMany()
                .HasForeignKey(e => e.MaTaiKhoanThucHien)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<TaiKhoan>()
                .WithMany()
                .HasForeignKey(e => e.MaTaiKhoanBiTacDong)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 25. YEU_CAU_TAI_KHOAN (Bảng trung gian: HR duyệt HĐ → IT cấp tài khoản)
        modelBuilder.Entity<YeuCauTaiKhoan>(entity =>
        {
            entity.ToTable("yeu_cau_tai_khoan");
            entity.HasKey(e => e.MaYeuCau);
            entity.Property(e => e.MaYeuCau).HasColumnName("ma_yeu_cau");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.MaHopDong).HasColumnName("ma_hop_dong");
            entity.Property(e => e.TenDangNhap).HasColumnName("ten_dang_nhap").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).HasDefaultValue("CHO_IT").IsRequired();
            entity.Property(e => e.NgayGui).HasColumnName("ngay_gui").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.MaNguoiGui).HasColumnName("ma_nguoi_gui");
            entity.Property(e => e.MaNguoiXuLy).HasColumnName("ma_nguoi_xu_ly");
            entity.Property(e => e.NgayXuLy).HasColumnName("ngay_xu_ly");
            entity.Property(e => e.GhiChuTuChoi).HasColumnName("ghi_chu_tu_choi").HasMaxLength(500);
            entity.Property(e => e.MaTaiKhoan).HasColumnName("ma_tai_khoan");

            // Index là index CÓ ĐIỀU KIỆN, không phải UNIQUE thuần: chỉ những yêu
            // cầu còn mở (CHO_IT/DANG_XU_LY) mới khoá trùng tên đăng nhập. Yêu
            // cầu đã TU_CHOI rồi gửi lại vẫn phải dùng được đúng tên cũ, nếu
            // khoá cứng thì nhân viên không bao giờ sửa nổi lỗi khiến mình bị
            // từ chối.
            entity.HasIndex(e => e.TenDangNhap)
                .IsUnique()
                .HasFilter("trang_thai IN ('CHO_IT','DANG_XU_LY')")
                .HasDatabaseName("uq_yeu_cau_ten_dang_nhap_dang_mo");

            // Index phục vụ màn hình "hộp thư của IT": yêu cầu đang chờ, cũ nhất
            // lên đầu.
            entity.HasIndex(e => e.NgayGui)
                .HasFilter("trang_thai IN ('CHO_IT','DANG_XU_LY')")
                .HasDatabaseName("idx_yeu_cau_tai_khoan_cho_it");

            entity.HasOne(e => e.NhanVien)
                .WithMany(n => n.YeuCauTaiKhoanCuaNhanVien)
                .HasForeignKey(e => e.MaNhanVien)
                .OnDelete(DeleteBehavior.Cascade);

            // Xoá hợp đồng không được xoá lịch sử IT đã xử lý — nên SetNull chứ
            // không Cascade, và cột FK cho phép NULL.
            entity.HasOne(e => e.HopDong)
                .WithMany(h => h.YeuCauTaiKhoans)
                .HasForeignKey(e => e.MaHopDong)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.VaiTro)
                .WithMany(v => v.YeuCauTaiKhoans)
                .HasForeignKey(e => e.MaVaiTro)
                .OnDelete(DeleteBehavior.Restrict);

            // Người gửi là nhân viên HR. Restrict: xoá hồ sơ nhân viên không
            // được xoá luôn dấu vết ai đã duyệt hợp đồng rồi gửi yêu cầu.
            entity.HasOne(e => e.NguoiGui)
                .WithMany(n => n.YeuCauTaiKhoanDaGui)
                .HasForeignKey(e => e.MaNguoiGui)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.NguoiXuLy)
                .WithMany(t => t.YeuCauTaiKhoanDaXuLy)
                .HasForeignKey(e => e.MaNguoiXuLy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TaiKhoanDuocCap)
                .WithMany(t => t.YeuCauTaiKhoanDuocTao)
                .HasForeignKey(e => e.MaTaiKhoan)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
