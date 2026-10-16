-- ======================================================================================
-- MIGRATION: QUY TRÌNH & BẢNG TÍNH LƯƠNG KHÁCH SẠN 2 TẦNG (GROSS -> NET)
-- Bổ sung các cột phê duyệt và nới lỏng CHECK constraint trạng thái bảng lương
-- ======================================================================================

-- 1. Bổ sung các trường quản lý quy trình phê duyệt & khóa sổ trên bảng KY_LUONG
ALTER TABLE KY_LUONG 
    ADD COLUMN IF NOT EXISTS ngay_gui_duyet TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS nguoi_gui_duyet VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS ngay_duyet TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS nguoi_duyet VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS ly_do_tu_choi TEXT NULL,
    ADD COLUMN IF NOT EXISTS ngay_khoa_so TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS nguoi_khoa_so VARCHAR(100) NULL;

-- 2. Cập nhật ràng buộc CHECK trên KY_LUONG.trang_thai
ALTER TABLE KY_LUONG DROP CONSTRAINT IF EXISTS ky_luong_trang_thai_check;
ALTER TABLE KY_LUONG DROP CONSTRAINT IF EXISTS uq_ky_luong_trang_thai;
ALTER TABLE KY_LUONG ADD CONSTRAINT ky_luong_trang_thai_check 
    CHECK (trang_thai IN ('MO', 'NHAP', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA_SO'));

-- 3. Cập nhật ràng buộc CHECK trên PHIEU_LUONG.trang_thai
ALTER TABLE PHIEU_LUONG DROP CONSTRAINT IF EXISTS phieu_luong_trang_thai_check;
ALTER TABLE PHIEU_LUONG ADD CONSTRAINT phieu_luong_trang_thai_check 
    CHECK (trang_thai IN ('NHAP', 'CHO_DUYET', 'DA_DUYET', 'DA_THANH_TOAN'));
