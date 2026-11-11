-- ==============================================================================
-- Migration: 20261111_attendance_performance_indexes.sql
-- Mục đích: Tạo các chỉ mục B-Tree chuyên dụng nhằm tối ưu hóa triệt để tốc độ
--           truy vấn trên bảng phan_ca và phan_cong_du_an cho WebAdmin và Mobile App.
-- ==============================================================================

-- 1. Tối ưu tìm kiếm và lọc lịch theo ngày làm việc (Phân ca 42 ngày, Chấm công)
CREATE INDEX IF NOT EXISTS idx_phan_ca_ngay_lam_viec
    ON public.phan_ca(ngay_lam_viec);

-- 2. Tối ưu tìm kiếm phân ca theo nhân viên và ngày (Mobile check-in, PersonalAttendance)
CREATE INDEX IF NOT EXISTS idx_phan_ca_nhanvien_ngay
    ON public.phan_ca(ma_nhan_vien, ngay_lam_viec);

-- 3. Tối ưu lọc theo ca làm việc trong khoảng ngày
CREATE INDEX IF NOT EXISTS idx_phan_ca_ca_ngay
    ON public.phan_ca(ma_ca, ngay_lam_viec);

-- 4. Tối ưu lọc ca hợp lệ (loại bỏ DA_HUY) trong khoảng ngày
CREATE INDEX IF NOT EXISTS idx_phan_ca_trang_thai_ngay
    ON public.phan_ca(trang_thai, ngay_lam_viec);

-- 5. Tối ưu liên kết sự kiện dự án với phân ca
CREATE INDEX IF NOT EXISTS idx_phan_cong_du_an_phan_ca
    ON public.phan_cong_du_an(ma_phan_ca) WHERE ma_phan_ca IS NOT NULL;
