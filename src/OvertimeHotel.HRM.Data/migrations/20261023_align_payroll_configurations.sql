-- ====================================================================
-- MIGRATION: 20261023_align_payroll_configurations.sql
-- Mục đích: Đồng bộ và làm sạch dữ liệu cấu hình lương cũ (cau_hinh_khoan_luong)
--           khớp với định mức thực tế của hệ thống và các bảng cấu hình chuyên biệt
-- ====================================================================

-- 1. Cập nhật Phụ cấp ăn ca theo ngày công thực tế (30.000 đ/ngày)
UPDATE public.cau_hinh_khoan_luong
SET gia_tri_mac_dinh = 30000.00,
    mo_ta = 'Phụ cấp ăn ca theo ngày công thực tế (30.000 đ/ngày, miễn thuế TNCN)'
WHERE ma_khoan = 1;

-- 2. Cập nhật Phụ cấp trách nhiệm quản lý (1.000.000 đ/tháng)
UPDATE public.cau_hinh_khoan_luong
SET gia_tri_mac_dinh = 1000000.00,
    mo_ta = 'Phụ cấp trách nhiệm quản lý dành cho cấp Trưởng bộ phận & Giám sát (1.000.000 đ/tháng)'
WHERE ma_khoan = 3;

-- 3. Cập nhật Định mức phạt vi phạm đi trễ (2.000 đ/phút)
UPDATE public.cau_hinh_khoan_luong
SET gia_tri_mac_dinh = 2000.00,
    mo_ta = 'Định mức phạt 2.000 đ cho mỗi phút vi phạm đi trễ ca làm việc'
WHERE ma_khoan = 7;
