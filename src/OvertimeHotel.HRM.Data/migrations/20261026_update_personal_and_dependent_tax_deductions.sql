-- Migration: Cập nhật mức giảm trừ gia cảnh thuế TNCN
-- Bản thân người nộp thuế: 15.500.000 VNĐ / tháng
-- Mỗi người phụ thuộc: 6.200.000 VNĐ / tháng

ALTER TABLE public.chi_tiet_tru_thue_tncn 
    ALTER COLUMN giam_tru_ban_than SET DEFAULT 15500000.00;

COMMENT ON COLUMN public.nhan_vien.so_nguoi_phu_thuoc IS 'Số lượng người phụ thuộc đăng ký giảm trừ gia cảnh thuế TNCN hợp lệ (mỗi người giảm 6.200.000đ/tháng)';
