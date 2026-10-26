-- ====================================================================================
-- MIGRATION: 20261026_remove_phone_allowance_and_salary_advance.sql
-- Mục đích: Xóa hẳn dòng Phụ cấp điện thoại & Tạm ứng lương khỏi hệ thống CSDL Supabase
-- ====================================================================================

-- 1. Xóa các dòng phụ cấp điện thoại trong chi_tiet_phieu_luong để giải phóng ràng buộc FK
DELETE FROM chi_tiet_phieu_luong WHERE ma_phu_cap = 3;

-- 2. Xóa hẳn row Phụ cấp điện thoại & liên lạc trong cau_hinh_phu_cap
DELETE FROM cau_hinh_phu_cap WHERE ma_phu_cap = 3 OR ten_phu_cap ILIKE '%điện thoại%';

-- 3. Xóa các dòng tạm ứng trong chi_tiet_phieu_luong (nếu có)
DELETE FROM chi_tiet_phieu_luong WHERE ma_khoan_tru = 6;

-- 4. Xóa hẳn row Tạm ứng lương định kỳ trong cau_hinh_khau_tru
DELETE FROM cau_hinh_khau_tru WHERE ma_khoan_tru = 6 OR loai_khoan_tru = 'TAM_UNG' OR ten_khoan_tru ILIKE '%tạm ứng%';
