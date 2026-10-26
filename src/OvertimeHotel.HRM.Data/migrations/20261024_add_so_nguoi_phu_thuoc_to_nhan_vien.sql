-- Migration: 20261024_add_so_nguoi_phu_thuoc_to_nhan_vien.sql
-- Thêm cột số người phụ thuộc vào bảng nhan_vien để phục vụ giảm trừ gia cảnh thuế TNCN

ALTER TABLE nhan_vien
ADD COLUMN IF NOT EXISTS so_nguoi_phu_thuoc INT NOT NULL DEFAULT 0;

COMMENT ON COLUMN nhan_vien.so_nguoi_phu_thuoc IS 'Số lượng người phụ thuộc đăng ký giảm trừ gia cảnh thuế TNCN hợp lệ (mỗi người giảm 4.400.000đ/tháng)';
