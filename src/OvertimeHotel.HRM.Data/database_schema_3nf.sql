-- ======================================================================================
-- HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG KHÁCH SẠN THE OVERTIME HOTEL
-- DDL SCRIPT TẠO 17 BẢNG CHUẨN 3NF (POSTGRESQL / SUPABASE CLOUD DATABASE)
-- ======================================================================================

-- 1. BẢNG PHONG_BAN (Phòng ban / Bộ phận)
CREATE TABLE IF NOT EXISTS PHONG_BAN (
    ma_phong_ban SERIAL PRIMARY KEY,
    ten_phong_ban VARCHAR(150) NOT NULL UNIQUE,
    mo_ta VARCHAR(255)
);

-- 2. BẢNG CHUC_VU (Chức vụ / Vị trí công tác)
CREATE TABLE IF NOT EXISTS CHUC_VU (
    ma_chuc_vu SERIAL PRIMARY KEY,
    ten_chuc_vu VARCHAR(100) NOT NULL UNIQUE,
    mo_ta VARCHAR(255)
);

-- 3. BẢNG NHAN_VIEN (Hồ sơ nhân viên - Thực thể trung tâm)
CREATE TABLE IF NOT EXISTS NHAN_VIEN (
    ma_nhan_vien SERIAL PRIMARY KEY,
    ma_phong_ban INT NOT NULL REFERENCES PHONG_BAN(ma_phong_ban) ON DELETE RESTRICT,
    ma_chuc_vu INT NOT NULL REFERENCES CHUC_VU(ma_chuc_vu) ON DELETE RESTRICT,
    ho VARCHAR(100) NOT NULL,
    ten VARCHAR(50) NOT NULL,
    ngay_sinh DATE NOT NULL,
    gioi_tinh VARCHAR(10) NOT NULL CHECK (gioi_tinh IN ('NAM', 'NU', 'KHAC')),
    dien_thoai VARCHAR(20) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    dia_chi VARCHAR(255),
    trinh_do VARCHAR(50) NOT NULL,
    ngay_vao_lam DATE NOT NULL,
    ngay_thoi_viec DATE,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DANG_LAM' CHECK (trang_thai IN ('DANG_LAM', 'NGHI_PHEP', 'DA_THOI_VIEC'))
);

-- 4. BẢNG HOP_DONG (Hợp đồng lao động)
CREATE TABLE IF NOT EXISTS HOP_DONG (
    ma_hop_dong SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    so_hop_dong VARCHAR(50) NOT NULL UNIQUE,
    loai_hop_dong VARCHAR(50) NOT NULL,
    ngay_bat_dau DATE NOT NULL,
    ngay_ket_thuc DATE,
    luong_co_ban DECIMAL(15,2) NOT NULL CHECK (luong_co_ban >= 0),
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'HIEU_LUC' CHECK (trang_thai IN ('HIEU_LUC', 'HET_HAN', 'DA_CHAM_DUT'))
);

-- 5. BẢNG VAI_TRO (Vai trò người dùng - RBAC)
CREATE TABLE IF NOT EXISTS VAI_TRO (
    ma_vai_tro SERIAL PRIMARY KEY,
    ten_vai_tro VARCHAR(50) NOT NULL UNIQUE,
    mo_ta VARCHAR(255)
);

-- 6. BẢNG TAI_KHOAN (Tài khoản người dùng)
CREATE TABLE IF NOT EXISTS TAI_KHOAN (
    ma_tai_khoan SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL UNIQUE REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    ma_vai_tro INT NOT NULL REFERENCES VAI_TRO(ma_vai_tro) ON DELETE RESTRICT,
    ten_dang_nhap VARCHAR(50) NOT NULL UNIQUE,
    mat_khau_bam VARCHAR(255) NOT NULL,
    trang_thai BOOLEAN NOT NULL DEFAULT TRUE
);

-- 7. BẢNG QUYEN (Danh mục quyền chức năng)
CREATE TABLE IF NOT EXISTS QUYEN (
    ma_quyen SERIAL PRIMARY KEY,
    ma_quyen_code VARCHAR(50) NOT NULL UNIQUE,
    ten_quyen VARCHAR(150) NOT NULL,
    mo_ta VARCHAR(255)
);

-- 8. BẢNG VAI_TRO_QUYEN (Bảng trung gian phân quyền RBAC M:N)
CREATE TABLE IF NOT EXISTS VAI_TRO_QUYEN (
    ma_vai_tro INT NOT NULL REFERENCES VAI_TRO(ma_vai_tro) ON DELETE CASCADE,
    ma_quyen INT NOT NULL REFERENCES QUYEN(ma_quyen) ON DELETE CASCADE,
    PRIMARY KEY (ma_vai_tro, ma_quyen)
);

-- 9. BẢNG CA_LAM_VIEC (Danh mục ca làm chuẩn 24/7)
CREATE TABLE IF NOT EXISTS CA_LAM_VIEC (
    ma_ca SERIAL PRIMARY KEY,
    ten_ca VARCHAR(50) NOT NULL UNIQUE,
    gio_bat_dau TIME NOT NULL,
    gio_ket_thuc TIME NOT NULL,
    qua_dem BOOLEAN NOT NULL DEFAULT FALSE
);

-- 10. BẢNG PHAN_CA (Lịch phân ca làm việc)
CREATE TABLE IF NOT EXISTS PHAN_CA (
    ma_phan_ca SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    ma_ca INT NOT NULL REFERENCES CA_LAM_VIEC(ma_ca) ON DELETE RESTRICT,
    ngay_lam_viec DATE NOT NULL,
    bat_dau_du_kien TIMESTAMPTZ NOT NULL,
    ket_thuc_du_kien TIMESTAMPTZ NOT NULL,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DA_PHAN' CHECK (trang_thai IN ('DA_PHAN', 'HOAN_THANH', 'VANG_MAT'))
);

-- 11. BẢNG CHAM_CONG (Bản ghi quét thẻ thực tế)
CREATE TABLE IF NOT EXISTS CHAM_CONG (
    ma_cham_cong SERIAL PRIMARY KEY,
    ma_phan_ca INT NOT NULL UNIQUE REFERENCES PHAN_CA(ma_phan_ca) ON DELETE CASCADE,
    gio_check_in TIMESTAMPTZ,
    gio_check_out TIMESTAMPTZ,
    phut_di_tre INT NOT NULL DEFAULT 0,
    phut_ve_som INT NOT NULL DEFAULT 0,
    gio_tang_ca_ot DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DUNG_GIO' CHECK (trang_thai IN ('DUNG_GIO', 'DI_TRE', 'VE_SOM', 'VANG_MAT'))
);

-- 12. BẢNG LOAI_DON (Danh mục loại đơn từ)
CREATE TABLE IF NOT EXISTS LOAI_DON (
    ma_loai_don SERIAL PRIMARY KEY,
    ten_loai_don VARCHAR(100) NOT NULL UNIQUE,
    co_huong_luong BOOLEAN NOT NULL DEFAULT FALSE,
    mo_ta VARCHAR(255)
);

-- 13. BẢNG DON_TU (Đơn từ trực tuyến & Duyệt đơn)
CREATE TABLE IF NOT EXISTS DON_TU (
    ma_don SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    ma_loai_don INT NOT NULL REFERENCES LOAI_DON(ma_loai_don) ON DELETE RESTRICT,
    ma_nguoi_duyet INT REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE SET NULL,
    tu_ngay DATE NOT NULL,
    den_ngay DATE NOT NULL,
    ly_do TEXT NOT NULL,
    ngay_gui TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'CHO_DUYET' CHECK (trang_thai IN ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI')),
    phan_hoi_duyet VARCHAR(255),
    ngay_duyet TIMESTAMPTZ
);

-- 14. BẢNG KY_LUONG (Kỳ tính lương tháng)
CREATE TABLE IF NOT EXISTS KY_LUONG (
    ma_ky_luong SERIAL PRIMARY KEY,
    thang INT NOT NULL CHECK (thang BETWEEN 1 AND 12),
    nam INT NOT NULL CHECK (nam >= 2000),
    tu_ngay DATE NOT NULL,
    den_ngay DATE NOT NULL,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'MO' CHECK (trang_thai IN ('MO', 'DANG_TINH', 'KHOA_SO')),
    CONSTRAINT uq_ky_luong_thang_nam UNIQUE (thang, nam)
);

-- 15. BẢNG CAU_HINH_KHOAN_LUONG (Cấu hình khoản phụ cấp, thưởng, phạt)
CREATE TABLE IF NOT EXISTS CAU_HINH_KHOAN_LUONG (
    ma_khoan SERIAL PRIMARY KEY,
    ten_khoan VARCHAR(150) NOT NULL UNIQUE,
    loai_khoan VARCHAR(30) NOT NULL CHECK (loai_khoan IN ('PHU_CAP', 'THUONG', 'KHAU_TRU')),
    gia_tri_mac_dinh DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    mo_ta VARCHAR(255)
);

-- 16. BẢNG PHIEU_LUONG (Bảng lương tháng nhân viên)
CREATE TABLE IF NOT EXISTS PHIEU_LUONG (
    ma_phieu_luong SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE RESTRICT,
    ma_ky_luong INT NOT NULL REFERENCES KY_LUONG(ma_ky_luong) ON DELETE RESTRICT,
    luong_co_ban DECIMAL(15,2) NOT NULL CHECK (luong_co_ban >= 0),
    ngay_cong_thuc_te DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    tien_luong_cong DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    tien_tang_ca DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    tong_phu_cap DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    tong_thuong DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    tong_khau_tru DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    thuc_nhan DECIMAL(15,2) NOT NULL,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP' CHECK (trang_thai IN ('NHAP', 'DA_DUYET', 'DA_THANH_TOAN')),
    CONSTRAINT uq_phieu_luong_nv_ky UNIQUE (ma_nhan_vien, ma_ky_luong)
);

-- 17. BẢNG CHI_TIET_PHIEU_LUONG (Bản chụp từng khoản thu chi)
CREATE TABLE IF NOT EXISTS CHI_TIET_PHIEU_LUONG (
    ma_chi_tiet SERIAL PRIMARY KEY,
    ma_phieu_luong INT NOT NULL REFERENCES PHIEU_LUONG(ma_phieu_luong) ON DELETE CASCADE,
    ma_khoan INT NOT NULL REFERENCES CAU_HINH_KHOAN_LUONG(ma_khoan) ON DELETE RESTRICT,
    ten_khoan_luu VARCHAR(150) NOT NULL,
    loai_khoan_luu VARCHAR(30) NOT NULL,
    so_tien DECIMAL(15,2) NOT NULL,
    ghi_chu VARCHAR(255)
);

-- 18. BẢNG NHAT_KY_QUAN_TRI (Audit trail thao tác quản trị tài khoản)
CREATE TABLE IF NOT EXISTS NHAT_KY_QUAN_TRI (
    ma_nhat_ky BIGSERIAL PRIMARY KEY,
    ma_tai_khoan_thuc_hien INT NULL REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE SET NULL,
    ma_tai_khoan_bi_tac_dong INT NULL REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE SET NULL,
    ten_nguoi_thuc_hien VARCHAR(100) NOT NULL,
    ten_tai_khoan_bi_tac_dong VARCHAR(100) NOT NULL,
    hanh_dong VARCHAR(40) NOT NULL,
    noi_dung VARCHAR(500) NOT NULL,
    gia_tri_cu VARCHAR(1000),
    gia_tri_moi VARCHAR(1000),
    ly_do VARCHAR(500),
    dia_chi_ip VARCHAR(64),
    thoi_gian TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- TẠO CHỈ MỤC (INDEXES) TỐI ƯU HIỆU NĂNG TRUY VẤN
CREATE INDEX IF NOT EXISTS idx_nhan_vien_phong_ban ON NHAN_VIEN(ma_phong_ban);
CREATE INDEX IF NOT EXISTS idx_nhan_vien_chuc_vu ON NHAN_VIEN(ma_chuc_vu);
CREATE INDEX IF NOT EXISTS idx_nhan_vien_ten ON NHAN_VIEN(ten, ho);
CREATE INDEX IF NOT EXISTS idx_phan_ca_ngay ON PHAN_CA(ngay_lam_viec, ma_nhan_vien);
CREATE INDEX IF NOT EXISTS idx_cham_cong_phan_ca ON CHAM_CONG(ma_phan_ca);
CREATE INDEX IF NOT EXISTS idx_don_tu_nhan_vien ON DON_TU(ma_nhan_vien, trang_thai);
CREATE INDEX IF NOT EXISTS idx_phieu_luong_ky ON PHIEU_LUONG(ma_ky_luong);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_phieu ON CHI_TIET_PHIEU_LUONG(ma_phieu_luong);
CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_thoi_gian ON NHAT_KY_QUAN_TRI(thoi_gian DESC);
CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_tai_khoan ON NHAT_KY_QUAN_TRI(ma_tai_khoan_bi_tac_dong);
