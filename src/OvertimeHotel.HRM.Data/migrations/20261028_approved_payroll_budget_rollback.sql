-- ======================================================================================
-- ROLLBACK MIGRATION: BẢNG NGÂN SÁCH LƯƠNG ĐƯỢC DUYỆT (TSK-24 / PRD 10.4)
-- Hoàn tác bảng ngan_sach_luong và các chỉ mục liên quan
-- ======================================================================================

BEGIN;

DROP INDEX IF EXISTS public.idx_ngan_sach_luong_trang_thai;
DROP INDEX IF EXISTS public.idx_ngan_sach_luong_phong_ban;
DROP INDEX IF EXISTS public.idx_ngan_sach_luong_nam_thang;
DROP INDEX IF EXISTS public.uq_ngan_sach_luong_ky_phongban;

DROP TABLE IF EXISTS public.ngan_sach_luong;

COMMIT;
