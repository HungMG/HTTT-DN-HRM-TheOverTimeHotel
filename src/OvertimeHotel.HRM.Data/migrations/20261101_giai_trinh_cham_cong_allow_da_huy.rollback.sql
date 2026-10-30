-- Rollback Migration 2: Khôi phục danh mục trạng thái cũ của bảng giai_trinh_cham_cong
BEGIN;

ALTER TABLE public.giai_trinh_cham_cong DROP CONSTRAINT chk_giai_trinh_trang_thai;

ALTER TABLE public.giai_trinh_cham_cong ADD CONSTRAINT giai_trinh_cham_cong_trang_thai_check
    CHECK (trang_thai IN ('CHO_QUAN_LY', 'CHO_HR', 'DA_DUYET', 'TU_CHOI'));

COMMIT;
