-- Rollback Migration 1: Khôi phục ràng buộc trạng thái cũ của bảng don_tu
BEGIN;

ALTER TABLE public.don_tu DROP CONSTRAINT don_tu_trang_thai_check;

ALTER TABLE public.don_tu ADD CONSTRAINT don_tu_trang_thai_check
    CHECK (trang_thai IN ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI'));

COMMIT;
