-- ======================================================================================
-- HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG KHÁCH SẠN THE OVERTIME HOTEL
-- DDL SCRIPT TẠO 24 BẢNG CHUẨN 3NF (POSTGRESQL / SUPABASE CLOUD DATABASE)
-- Bao gồm bảng Audit Trail (NHAT_KY_QUAN_TRI) và Phân hệ Lương phân tách 3 cấp
-- ======================================================================================

-- 1. BẢNG PHONG_BAN (Phòng ban / Bộ phận)
CREATE TABLE IF NOT EXISTS PHONG_BAN (
    ma_phong_ban SERIAL PRIMARY KEY,
    ten_phong_ban VARCHAR(150) NOT NULL UNIQUE,
    mo_ta VARCHAR(255)
);

-- 2. BẢNG CHUC_VU (Chức vụ / Vị trí)
CREATE TABLE IF NOT EXISTS CHUC_VU (
    ma_chuc_vu SERIAL PRIMARY KEY,
    ma_phong_ban INT REFERENCES PHONG_BAN(ma_phong_ban) ON DELETE RESTRICT,
    ten_chuc_vu VARCHAR(100) NOT NULL,
    mo_ta VARCHAR(255)
);

-- 3. BẢNG NHAN_VIEN (Hồ sơ nhân sự)
CREATE TABLE IF NOT EXISTS NHAN_VIEN (
    ma_nhan_vien SERIAL PRIMARY KEY,
    ma_phong_ban INT NOT NULL REFERENCES PHONG_BAN(ma_phong_ban) ON DELETE RESTRICT,
    ma_chuc_vu INT NOT NULL REFERENCES CHUC_VU(ma_chuc_vu) ON DELETE RESTRICT,
    ho_ten VARCHAR(150) NOT NULL,
    is_profile_complete BOOLEAN NOT NULL DEFAULT true,
    ngay_sinh DATE,
    gioi_tinh VARCHAR(10) CHECK (gioi_tinh IN ('NAM', 'NU', 'KHAC')),
    dien_thoai VARCHAR(20) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    dia_chi VARCHAR(255),
    trinh_do VARCHAR(50) NOT NULL,
    ngay_vao_lam DATE NOT NULL,
    ngay_thoi_viec DATE,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DANG_LAM' CHECK (trang_thai IN ('DANG_LAM', 'NGHI_PHEP', 'THOI_VIEC')),
    CONSTRAINT chk_ngay_thoi_viec CHECK (ngay_thoi_viec IS NULL OR ngay_thoi_viec >= ngay_vao_lam)
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
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'HIEU_LUC' CHECK (trang_thai IN ('HIEU_LUC', 'HET_HAN', 'DA_CHAM_DUT')),
    hinh_thuc_hop_dong VARCHAR(50) NOT NULL DEFAULT 'CHINH_THUC',
    loai_cham_dut VARCHAR(30),
    ly_do_cham_dut VARCHAR(500),
    CONSTRAINT chk_ngay_hop_dong CHECK (ngay_ket_thuc IS NULL OR ngay_ket_thuc >= ngay_bat_dau)
);

-- 5. BẢNG VAI_TRO (Vai trò RBAC)
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

-- 7. BẢNG QUYEN (Quyền hạn chức năng)
CREATE TABLE IF NOT EXISTS QUYEN (
    ma_quyen SERIAL PRIMARY KEY,
    ma_quyen_code VARCHAR(50) NOT NULL UNIQUE,
    ten_quyen VARCHAR(150) NOT NULL,
    mo_ta VARCHAR(255)
);

-- 8. BẢNG VAI_TRO_QUYEN (Bảng trung gian Vai trò - Quyền)
CREATE TABLE IF NOT EXISTS VAI_TRO_QUYEN (
    ma_vai_tro INT NOT NULL REFERENCES VAI_TRO(ma_vai_tro) ON DELETE CASCADE,
    ma_quyen INT NOT NULL REFERENCES QUYEN(ma_quyen) ON DELETE CASCADE,
    PRIMARY KEY (ma_vai_tro, ma_quyen)
);

-- 9. BẢNG NHAT_KY_QUAN_TRI (Audit trail kiểm toán hệ thống)
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

-- 10. BẢNG CA_LAM_VIEC (Ca làm việc chuẩn)
CREATE TABLE IF NOT EXISTS CA_LAM_VIEC (
    ma_ca SERIAL PRIMARY KEY,
    ten_ca VARCHAR(50) NOT NULL UNIQUE,
    gio_bat_dau TIME NOT NULL,
    gio_ket_thuc TIME NOT NULL,
    qua_dem BOOLEAN NOT NULL DEFAULT FALSE
);

-- 11. BẢNG PHAN_CA (Phân ca lịch trực)
CREATE TABLE IF NOT EXISTS PHAN_CA (
    ma_phan_ca SERIAL PRIMARY KEY,
    ma_nhan_vien INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    ma_ca INT NOT NULL REFERENCES CA_LAM_VIEC(ma_ca) ON DELETE RESTRICT,
    ngay_lam_viec DATE NOT NULL,
    bat_dau_du_kien TIMESTAMPTZ NOT NULL,
    ket_thuc_du_kien TIMESTAMPTZ NOT NULL,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DA_PHAN' CHECK (trang_thai IN ('DA_PHAN', 'HOAN_THANH', 'VANG')),
    CONSTRAINT uq_phan_ca_nv_ngay UNIQUE (ma_nhan_vien, ngay_lam_viec, ma_ca)
);

-- 12. BẢNG CHAM_CONG (Ghi nhận quét thẻ thực tế)
CREATE TABLE IF NOT EXISTS CHAM_CONG (
    ma_cham_cong SERIAL PRIMARY KEY,
    ma_phan_ca INT NOT NULL UNIQUE REFERENCES PHAN_CA(ma_phan_ca) ON DELETE CASCADE,
    gio_check_in TIMESTAMPTZ,
    gio_check_out TIMESTAMPTZ,
    phut_di_tre INT NOT NULL DEFAULT 0 CHECK (phut_di_tre >= 0),
    phut_ve_som INT NOT NULL DEFAULT 0 CHECK (phut_ve_som >= 0),
    gio_tang_ca_ot DECIMAL(4,2) NOT NULL DEFAULT 0.00 CHECK (gio_tang_ca_ot >= 0),
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DUNG_GIO' CHECK (trang_thai IN ('DUNG_GIO', 'DI_TRE', 'VE_SOM', 'TRE_VA_SOM', 'VANG'))
);

-- 13. BẢNG LOAI_DON (Danh mục loại đơn từ)
CREATE TABLE IF NOT EXISTS LOAI_DON (
    ma_loai_don SERIAL PRIMARY KEY,
    ten_loai_don VARCHAR(100) NOT NULL UNIQUE,
    co_huong_luong BOOLEAN NOT NULL DEFAULT FALSE,
    mo_ta VARCHAR(255)
);

-- 14. BẢNG DON_TU (Đơn từ trực tuyến)
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

-- 15. BẢNG KY_LUONG (Kỳ tính lương tháng)
CREATE TABLE IF NOT EXISTS KY_LUONG (
    ma_ky_luong SERIAL PRIMARY KEY,
    thang INT NOT NULL CHECK (thang BETWEEN 1 AND 12),
    nam INT NOT NULL CHECK (nam >= 2000),
    tu_ngay DATE NOT NULL,
    den_ngay DATE NOT NULL,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'MO' CHECK (trang_thai IN ('MO', 'NHAP', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA_SO')),
    ngay_gui_duyet TIMESTAMPTZ NULL,
    nguoi_gui_duyet VARCHAR(100) NULL,
    ngay_duyet TIMESTAMPTZ NULL,
    nguoi_duyet VARCHAR(100) NULL,
    ly_do_tu_choi TEXT NULL,
    ngay_khoa_so TIMESTAMPTZ NULL,
    nguoi_khoa_so VARCHAR(100) NULL,
    CONSTRAINT uq_ky_luong_thang_nam UNIQUE (thang, nam)
);

-- 16. BẢNG PHIEU_LUONG (Bảng lương tháng nhân viên - Master Header)
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
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'NHAP' CHECK (trang_thai IN ('NHAP', 'CHO_DUYET', 'DA_DUYET', 'DA_THANH_TOAN')),
    CONSTRAINT uq_phieu_luong_nv_ky UNIQUE (ma_nhan_vien, ma_ky_luong)
);

-- 17. BẢNG CAU_HINH_TANG_CA (Cấu hình loại OT - Tách riêng)
CREATE TABLE IF NOT EXISTS CAU_HINH_TANG_CA (
    ma_loai_ot SERIAL PRIMARY KEY,
    ten_loai_ot VARCHAR(100) NOT NULL UNIQUE,
    he_so DECIMAL(4,2) NOT NULL CHECK (he_so > 0),
    mo_ta VARCHAR(255)
);

-- 18. BẢNG CAU_HINH_PHU_CAP (Cấu hình định mức phụ cấp - Tách riêng)
CREATE TABLE IF NOT EXISTS CAU_HINH_PHU_CAP (
    ma_phu_cap SERIAL PRIMARY KEY,
    ten_phu_cap VARCHAR(150) NOT NULL UNIQUE,
    loai_tinh VARCHAR(30) NOT NULL CHECK (loai_tinh IN ('THEO_NGAY_CONG', 'CO_DINH_THANG')),
    muc_dinh_muc DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    chiu_thue_tncn BOOLEAN NOT NULL DEFAULT FALSE,
    mo_ta VARCHAR(255)
);

-- 19. BẢNG CAU_HINH_KHAU_TRU (Cấu hình danh mục khoản trừ - Tách riêng)
CREATE TABLE IF NOT EXISTS CAU_HINH_KHAU_TRU (
    ma_khoan_tru SERIAL PRIMARY KEY,
    ten_khoan_tru VARCHAR(150) NOT NULL UNIQUE,
    loai_khoan_tru VARCHAR(30) NOT NULL CHECK (loai_khoan_tru IN ('BAO_HIEM', 'THUE', 'VI_PHAM', 'TAM_UNG')),
    ty_le_hoac_dinh_muc DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    mo_ta VARCHAR(255)
);

-- 20. BẢNG CHI_TIET_PHIEU_LUONG (Bảng trung gian điều phối dòng lương)
CREATE TABLE IF NOT EXISTS CHI_TIET_PHIEU_LUONG (
    ma_chi_tiet SERIAL PRIMARY KEY,
    ma_phieu_luong INT NOT NULL REFERENCES PHIEU_LUONG(ma_phieu_luong) ON DELETE CASCADE,
    ma_loai_ot INT NULL REFERENCES CAU_HINH_TANG_CA(ma_loai_ot) ON DELETE RESTRICT,
    ma_phu_cap INT NULL REFERENCES CAU_HINH_PHU_CAP(ma_phu_cap) ON DELETE RESTRICT,
    ma_khoan_tru INT NULL REFERENCES CAU_HINH_KHAU_TRU(ma_khoan_tru) ON DELETE RESTRICT,
    loai_khoan VARCHAR(30) NOT NULL CHECK (loai_khoan IN ('TANG_CA', 'PHU_CAP', 'THUONG', 'KHAU_TRU')),
    ten_khoan_luu VARCHAR(150) NOT NULL,
    so_tien DECIMAL(15,2) NOT NULL,
    ghi_chu VARCHAR(255)
);

-- 21. BẢNG CHI_TIET_TRU_BAO_HIEM (Chi tiết khấu trừ bảo hiểm bắt buộc - Bảng con 1)
CREATE TABLE IF NOT EXISTS CHI_TIET_TRU_BAO_HIEM (
    ma_chi_tiet_bh SERIAL PRIMARY KEY,
    ma_chi_tiet INT NOT NULL REFERENCES CHI_TIET_PHIEU_LUONG(ma_chi_tiet) ON DELETE CASCADE,
    luong_dong_bao_hiem DECIMAL(15,2) NOT NULL CHECK (luong_dong_bao_hiem >= 0),
    ty_le_bhxh DECIMAL(5,2) NOT NULL DEFAULT 8.00,
    tien_bhxh DECIMAL(15,2) NOT NULL,
    ty_le_bhyt DECIMAL(5,2) NOT NULL DEFAULT 1.50,
    tien_bhyt DECIMAL(15,2) NOT NULL,
    ty_le_bhtn DECIMAL(5,2) NOT NULL DEFAULT 1.00,
    tien_bhtn DECIMAL(15,2) NOT NULL,
    tong_bao_hiem DECIMAL(15,2) NOT NULL
);

-- 22. BẢNG CHI_TIET_TRU_THUE_TNCN (Chi tiết khấu trừ thuế TNCN lũy tiến - Bảng con 2)
CREATE TABLE IF NOT EXISTS CHI_TIET_TRU_THUE_TNCN (
    ma_chi_tiet_thue SERIAL PRIMARY KEY,
    ma_chi_tiet INT NOT NULL REFERENCES CHI_TIET_PHIEU_LUONG(ma_chi_tiet) ON DELETE CASCADE,
    thu_nhap_chiu_thue DECIMAL(15,2) NOT NULL CHECK (thu_nhap_chiu_thue >= 0),
    giam_tru_ban_than DECIMAL(15,2) NOT NULL DEFAULT 11000000.00,
    so_nguoi_phu_thuoc INT NOT NULL DEFAULT 0 CHECK (so_nguoi_phu_thuoc >= 0),
    giam_tru_phu_thuoc DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    thu_nhap_tinh_thue DECIMAL(15,2) NOT NULL CHECK (thu_nhap_tinh_thue >= 0),
    tien_thue DECIMAL(15,2) NOT NULL CHECK (tien_thue >= 0)
);

-- 23. BẢNG CHI_TIET_TRU_VI_PHAM_TAM_UNG (Chi tiết phạt kỷ luật & tạm ứng - Bảng con 3)
CREATE TABLE IF NOT EXISTS CHI_TIET_TRU_VI_PHAM_TAM_UNG (
    ma_chi_tiet_vp SERIAL PRIMARY KEY,
    ma_chi_tiet INT NOT NULL REFERENCES CHI_TIET_PHIEU_LUONG(ma_chi_tiet) ON DELETE CASCADE,
    loai_tru VARCHAR(30) NOT NULL CHECK (loai_tru IN ('DI_TRE', 'VE_SOM', 'KY_LUAT', 'LAM_HONG', 'TAM_UNG')),
    so_phut_vi_pham INT NOT NULL DEFAULT 0 CHECK (so_phut_vi_pham >= 0),
    so_tien_tru DECIMAL(15,2) NOT NULL CHECK (so_tien_tru >= 0),
    ly_do_chi_tiet VARCHAR(255) NOT NULL
);

-- 24. BẢNG YEU_CAU_TAI_KHOAN (Bảng trung gian: HR duyệt HĐ → IT cấp tài khoản)
-- Trạng thái: CHO_IT (chờ IT) → DANG_XU_LY (IT đã nhận) → DA_CAP / TU_CHOI
-- Ba CHECK dưới đây là bất biến nghiệp vụ: từ chối phải có lý do, "đã cấp"
-- thì phải có tài khoản, và mọi trạng thái ngoài CHO_IT đều phải biết ai xử lý.
CREATE TABLE IF NOT EXISTS YEU_CAU_TAI_KHOAN (
    ma_yeu_cau        SERIAL PRIMARY KEY,
    ma_nhan_vien      INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE CASCADE,
    ma_hop_dong       INT NULL REFERENCES HOP_DONG(ma_hop_dong) ON DELETE SET NULL,
    ten_dang_nhap     VARCHAR(50) NOT NULL,
    ma_vai_tro        INT NOT NULL REFERENCES VAI_TRO(ma_vai_tro) ON DELETE RESTRICT,
    trang_thai        VARCHAR(30) NOT NULL DEFAULT 'CHO_IT' CHECK (trang_thai IN ('CHO_IT', 'DANG_XU_LY', 'DA_CAP', 'TU_CHOI')),
    ngay_gui          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ma_nguoi_gui      INT NOT NULL REFERENCES NHAN_VIEN(ma_nhan_vien) ON DELETE RESTRICT,
    ma_nguoi_xu_ly    INT NULL REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE SET NULL,
    ngay_xu_ly        TIMESTAMPTZ NULL,
    ghi_chu_tu_choi   VARCHAR(500) NULL,
    ma_tai_khoan      INT NULL REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE SET NULL,
    CONSTRAINT chk_yeu_cau_tu_choi_can_co_ghi_chu
        CHECK (trang_thai <> 'TU_CHOI' OR ghi_chu_tu_choi IS NOT NULL),
    CONSTRAINT chk_yeu_cau_da_cap_can_co_tai_khoan
        CHECK ((trang_thai = 'DA_CAP') = (ma_tai_khoan IS NOT NULL)),
    CONSTRAINT chk_yeu_cau_da_xu_ly_can_co_nguoi
        CHECK (trang_thai = 'CHO_IT' OR ma_nguoi_xu_ly IS NOT NULL)
);

-- ======================================================================================
-- TẠO CHỈ MỤC (INDEXES) TỐI ƯU HIỆU NĂNG TRUY VẤN CSDL
-- ======================================================================================
CREATE INDEX IF NOT EXISTS idx_nhan_vien_phong_ban ON NHAN_VIEN(ma_phong_ban);
CREATE INDEX IF NOT EXISTS idx_nhan_vien_chuc_vu ON NHAN_VIEN(ma_chuc_vu);
CREATE INDEX IF NOT EXISTS idx_hop_dong_nhan_vien ON HOP_DONG(ma_nhan_vien);
CREATE INDEX IF NOT EXISTS idx_tai_khoan_vai_tro ON TAI_KHOAN(ma_vai_tro);
CREATE INDEX IF NOT EXISTS idx_nhat_ky_thoi_gian ON NHAT_KY_QUAN_TRI(thoi_gian DESC);
CREATE INDEX IF NOT EXISTS idx_phan_ca_nv_ngay ON PHAN_CA(ma_nhan_vien, ngay_lam_viec);
CREATE INDEX IF NOT EXISTS idx_don_tu_nv ON DON_TU(ma_nhan_vien);
CREATE INDEX IF NOT EXISTS idx_phieu_luong_nv_ky ON PHIEU_LUONG(ma_nhan_vien, ma_ky_luong);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_phieu ON CHI_TIET_PHIEU_LUONG(ma_phieu_luong);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_bh_ct ON CHI_TIET_TRU_BAO_HIEM(ma_chi_tiet);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_thue_ct ON CHI_TIET_TRU_THUE_TNCN(ma_chi_tiet);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_vp_ct ON CHI_TIET_TRU_VI_PHAM_TAM_UNG(ma_chi_tiet);
-- Chỉ khoá trùng tên đăng nhập khi yêu cầu còn mở: yêu cầu đã TU_CHOI rồi gửi
-- lại phải dùng được đúng tên cũ, nếu khoá cứng thì không sửa nổi lỗi đã bị từ chối.
CREATE UNIQUE INDEX IF NOT EXISTS uq_yeu_cau_ten_dang_nhap_dang_mo
    ON YEU_CAU_TAI_KHOAN (ten_dang_nhap)
    WHERE trang_thai IN ('CHO_IT', 'DANG_XU_LY');
