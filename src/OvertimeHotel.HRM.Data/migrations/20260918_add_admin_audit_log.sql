-- Nhật ký quản trị cho Task 13.
-- Có thể chạy độc lập trong Supabase SQL Editor nếu CSDL đã tồn tại.
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

CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_thoi_gian
    ON NHAT_KY_QUAN_TRI(thoi_gian DESC);

CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_tai_khoan
    ON NHAT_KY_QUAN_TRI(ma_tai_khoan_bi_tac_dong);
