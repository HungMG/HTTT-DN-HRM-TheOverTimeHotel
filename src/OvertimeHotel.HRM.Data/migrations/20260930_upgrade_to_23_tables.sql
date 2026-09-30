-- ======================================================================================
-- MIGRATION: NÂNG CẤP CƠ SỞ DỮ LIỆU TỪ 17 BẢNG LÊN 23 BẢNG CHUẨN 3NF
-- Phân hệ Tiền lương: Tách 3 bảng Cấu hình độc lập, Bảng trung gian và 3 bảng Khấu trừ chi tiết
-- Ngày tạo: 30/09/2026
-- ======================================================================================

-- 1. TẠO BẢNG CAU_HINH_TANG_CA (Cấu hình loại OT)
CREATE TABLE IF NOT EXISTS CAU_HINH_TANG_CA (
    ma_loai_ot SERIAL PRIMARY KEY,
    ten_loai_ot VARCHAR(100) NOT NULL UNIQUE,
    he_so DECIMAL(4,2) NOT NULL CHECK (he_so > 0),
    mo_ta VARCHAR(255)
);

-- 2. TẠO BẢNG CAU_HINH_PHU_CAP (Cấu hình danh mục phụ cấp)
CREATE TABLE IF NOT EXISTS CAU_HINH_PHU_CAP (
    ma_phu_cap SERIAL PRIMARY KEY,
    ten_phu_cap VARCHAR(150) NOT NULL UNIQUE,
    loai_tinh VARCHAR(30) NOT NULL CHECK (loai_tinh IN ('THEO_NGAY_CONG', 'CO_DINH_THANG')),
    muc_dinh_muc DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    chiu_thue_tncn BOOLEAN NOT NULL DEFAULT FALSE,
    mo_ta VARCHAR(255)
);

-- 3. TẠO BẢNG CAU_HINH_KHAU_TRU (Cấu hình danh mục khoản trừ)
CREATE TABLE IF NOT EXISTS CAU_HINH_KHAU_TRU (
    ma_khoan_tru SERIAL PRIMARY KEY,
    ten_khoan_tru VARCHAR(150) NOT NULL UNIQUE,
    loai_khoan_tru VARCHAR(30) NOT NULL CHECK (loai_khoan_tru IN ('BAO_HIEM', 'THUE', 'VI_PHAM', 'TAM_UNG')),
    ty_le_hoac_dinh_muc DECIMAL(15,2) NOT NULL DEFAULT 0.00,
    mo_ta VARCHAR(255)
);

-- 4. CẬP NHẬT BẢNG CHI_TIET_PHIEU_LUONG (BẢNG TRUNG GIAN ĐIỀU PHỐI)
ALTER TABLE CHI_TIET_PHIEU_LUONG ALTER COLUMN ma_khoan DROP NOT NULL;

ALTER TABLE CHI_TIET_PHIEU_LUONG 
    ADD COLUMN IF NOT EXISTS ma_loai_ot INT NULL REFERENCES CAU_HINH_TANG_CA(ma_loai_ot) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS ma_phu_cap INT NULL REFERENCES CAU_HINH_PHU_CAP(ma_phu_cap) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS ma_khoan_tru INT NULL REFERENCES CAU_HINH_KHAU_TRU(ma_khoan_tru) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS loai_khoan VARCHAR(30);

UPDATE CHI_TIET_PHIEU_LUONG SET loai_khoan = loai_khoan_luu WHERE loai_khoan IS NULL;

-- 5. TẠO BẢNG CON 1: CHI_TIET_TRU_BAO_HIEM (Trích đóng bảo hiểm bắt buộc)
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

-- 6. TẠO BẢNG CON 2: CHI_TIET_TRU_THUE_TNCN (Thuế TNCN lũy tiến)
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

-- 7. TẠO BẢNG CON 3: CHI_TIET_TRU_VI_PHAM_TAM_UNG (Kỷ luật, đi trễ, tạm ứng)
CREATE TABLE IF NOT EXISTS CHI_TIET_TRU_VI_PHAM_TAM_UNG (
    ma_chi_tiet_vp SERIAL PRIMARY KEY,
    ma_chi_tiet INT NOT NULL REFERENCES CHI_TIET_PHIEU_LUONG(ma_chi_tiet) ON DELETE CASCADE,
    loai_tru VARCHAR(30) NOT NULL CHECK (loai_tru IN ('DI_TRE', 'VE_SOM', 'KY_LUAT', 'LAM_HONG', 'TAM_UNG')),
    so_phut_vi_pham INT NOT NULL DEFAULT 0 CHECK (so_phut_vi_pham >= 0),
    so_tien_tru DECIMAL(15,2) NOT NULL CHECK (so_tien_tru >= 0),
    ly_do_chi_tiet VARCHAR(255) NOT NULL
);

-- 8. TẠO CHỈ MỤC CHO CÁC BẢNG MỚI
CREATE INDEX IF NOT EXISTS idx_chi_tiet_bh_ct ON CHI_TIET_TRU_BAO_HIEM(ma_chi_tiet);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_thue_ct ON CHI_TIET_TRU_THUE_TNCN(ma_chi_tiet);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_vp_ct ON CHI_TIET_TRU_VI_PHAM_TAM_UNG(ma_chi_tiet);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_ot_fk ON CHI_TIET_PHIEU_LUONG(ma_loai_ot);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_pc_fk ON CHI_TIET_PHIEU_LUONG(ma_phu_cap);
CREATE INDEX IF NOT EXISTS idx_chi_tiet_kt_fk ON CHI_TIET_PHIEU_LUONG(ma_khoan_tru);

-- 9. NẠP DỮ LIỆU SEED CHO CÁC BẢNG CẤU HÌNH MỚI
INSERT INTO CAU_HINH_TANG_CA (ma_loai_ot, ten_loai_ot, he_so, mo_ta) VALUES
(1, 'Làm thêm ngày thường (150%)', 1.50, 'Làm thêm ngoài giờ ca làm việc ngày thường theo Luật Lao động'),
(2, 'Làm thêm ngày nghỉ hàng tuần (200%)', 2.00, 'Làm thêm vào ngày nghỉ tuần của nhân sự khách sạn'),
(3, 'Làm thêm ngày Lễ, Tết (300%)', 3.00, 'Làm thêm vào ngày Lễ, Tết theo quy định Nhà nước'),
(4, 'Phụ cấp làm việc ca đêm (30%)', 0.30, 'Cộng thêm 30% lương cho ca làm việc từ 22h00 đến 06h00 sáng')
ON CONFLICT (ma_loai_ot) DO UPDATE SET he_so = EXCLUDED.he_so;

SELECT setval('cau_hinh_tang_ca_ma_loai_ot_seq', (SELECT MAX(ma_loai_ot) FROM CAU_HINH_TANG_CA));

INSERT INTO CAU_HINH_PHU_CAP (ma_phu_cap, ten_phu_cap, loai_tinh, muc_dinh_muc, chiu_thue_tncn, mo_ta) VALUES
(1, 'Phụ cấp ăn ca', 'THEO_NGAY_CONG', 30000.00, FALSE, 'Hỗ trợ suất ăn trưa/tối theo ngày công thực tế (miễn thuế TNCN)'),
(2, 'Phụ cấp trách nhiệm quản lý', 'CO_DINH_THANG', 1000000.00, TRUE, 'Dành cho cấp Giám sát và Trưởng bộ phận'),
(3, 'Phụ cấp điện thoại & liên lạc', 'CO_DINH_THANG', 300000.00, TRUE, 'Hỗ trợ cước liên lạc điều hành công việc khách sạn'),
(4, 'Phụ cấp độc hại buồng phòng', 'THEO_NGAY_CONG', 20000.00, FALSE, 'Phụ cấp tiếp xúc hóa chất tẩy rửa cho nhân viên HK')
ON CONFLICT (ma_phu_cap) DO UPDATE SET muc_dinh_muc = EXCLUDED.muc_dinh_muc;

SELECT setval('cau_hinh_phu_cap_ma_phu_cap_seq', (SELECT MAX(ma_phu_cap) FROM CAU_HINH_PHU_CAP));

INSERT INTO CAU_HINH_KHAU_TRU (ma_khoan_tru, ten_khoan_tru, loai_khoan_tru, ty_le_hoac_dinh_muc, mo_ta) VALUES
(1, 'Bảo hiểm xã hội (BHXH - 8%)', 'BAO_HIEM', 8.00, 'Trích nộp 8% lương cơ bản theo Luật BHXH'),
(2, 'Bảo hiểm y tế (BHYT - 1.5%)', 'BAO_HIEM', 1.50, 'Trích nộp 1.5% lương cơ bản theo Luật BHYT'),
(3, 'Bảo hiểm thất nghiệp (BHTN - 1%)', 'BAO_HIEM', 1.00, 'Trích nộp 1% lương cơ bản theo Luật Việc làm'),
(4, 'Thuế thu nhập cá nhân (Thuế TNCN)', 'THUE', 0.00, 'Khấu trừ thuế TNCN theo biểu thuế lũy tiến từng phần'),
(5, 'Phạt vi phạm đi trễ / về sớm', 'VI_PHAM', 2000.00, 'Định mức phạt 2.000 đ cho mỗi phút vi phạm ca làm việc'),
(6, 'Tạm ứng lương định kỳ', 'TAM_UNG', 0.00, 'Khấu trừ tiền nhân viên đã ứng trước trong kỳ lương')
ON CONFLICT (ma_khoan_tru) DO UPDATE SET ty_le_hoac_dinh_muc = EXCLUDED.ty_le_hoac_dinh_muc;

SELECT setval('cau_hinh_khau_tru_ma_khoan_tru_seq', (SELECT MAX(ma_khoan_tru) FROM CAU_HINH_KHAU_TRU));

-- 10. LIÊN KẾT LẠI DỮ LIỆU HIỆN CÓ TRONG CHI_TIET_PHIEU_LUONG VỚI CÁC BẢNG MỚI
UPDATE CHI_TIET_PHIEU_LUONG SET ma_phu_cap = 1, loai_khoan = 'PHU_CAP' WHERE ma_chi_tiet = 1;
UPDATE CHI_TIET_PHIEU_LUONG SET ma_loai_ot = 4, loai_khoan = 'TANG_CA' WHERE ma_chi_tiet = 2;
UPDATE CHI_TIET_PHIEU_LUONG SET loai_khoan = 'THUONG' WHERE ma_chi_tiet IN (3, 4);
UPDATE CHI_TIET_PHIEU_LUONG SET ma_khoan_tru = 1, loai_khoan = 'KHAU_TRU' WHERE ma_chi_tiet = 5;
UPDATE CHI_TIET_PHIEU_LUONG SET ma_khoan_tru = 5, loai_khoan = 'KHAU_TRU' WHERE ma_chi_tiet = 6;

-- 11. NẠP SEED DATA VÀO 3 BẢNG CON KHẤU TRỪ
-- Nạp chi tiết bảo hiểm cho dòng khấu trừ ma_chi_tiet = 5 (Tổng trừ: 892.500 đ)
INSERT INTO CHI_TIET_TRU_BAO_HIEM (ma_chi_tiet_bh, ma_chi_tiet, luong_dong_bao_hiem, ty_le_bhxh, tien_bhxh, ty_le_bhyt, tien_bhyt, ty_le_bhtn, tien_bhtn, tong_bao_hiem)
VALUES (1, 5, 8500000.00, 8.00, 680000.00, 1.50, 127500.00, 1.00, 85000.00, 892500.00)
ON CONFLICT (ma_chi_tiet_bh) DO NOTHING;

SELECT setval('chi_tiet_tru_bao_hiem_ma_chi_tiet_bh_seq', (SELECT MAX(ma_chi_tiet_bh) FROM CHI_TIET_TRU_BAO_HIEM));

-- Nạp chi tiết thuế TNCN cho dòng khấu trừ ma_chi_tiet = 5 (Thuế = 0 đ)
INSERT INTO CHI_TIET_TRU_THUE_TNCN (ma_chi_tiet_thue, ma_chi_tiet, thu_nhap_chiu_thue, giam_tru_ban_than, so_nguoi_phu_thuoc, giam_tru_phu_thuoc, thu_nhap_tinh_thue, tien_thue)
VALUES (1, 5, 8336538.46, 11000000.00, 0, 0.00, 0.00, 0.00)
ON CONFLICT (ma_chi_tiet_thue) DO NOTHING;

SELECT setval('chi_tiet_tru_thue_tncn_ma_chi_tiet_thue_seq', (SELECT MAX(ma_chi_tiet_thue) FROM CHI_TIET_TRU_THUE_TNCN));

-- Nạp chi tiết vi phạm cho dòng khấu trừ ma_chi_tiet = 6 (Phạt đi trễ 18 phút = 50.000 đ)
INSERT INTO CHI_TIET_TRU_VI_PHAM_TAM_UNG (ma_chi_tiet_vp, ma_chi_tiet, loai_tru, so_phut_vi_pham, so_tien_tru, ly_do_chi_tiet)
VALUES (1, 6, 'DI_TRE', 18, 50000.00, 'Phạt đi trễ 18 phút ca sáng ngày 16/09/2026')
ON CONFLICT (ma_chi_tiet_vp) DO NOTHING;

SELECT setval('chi_tiet_tru_vi_pham_tam_ung_ma_chi_tiet_vp_seq', (SELECT MAX(ma_chi_tiet_vp) FROM CHI_TIET_TRU_VI_PHAM_TAM_UNG));
