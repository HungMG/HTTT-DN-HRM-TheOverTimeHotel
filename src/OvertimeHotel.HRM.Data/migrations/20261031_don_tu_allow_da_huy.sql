-- Migration 1 (Giai đoạn 2): Cho phép rút đơn nghỉ phép (trạng thái 'DA_HUY')
-- Áp dụng cho bảng don_tu
BEGIN;

-- Sau kiểm kê mục 4.1 của plan.md, trong cùng phiên kết nối chạy:
-- SET hrm.status_constraint = 'tên_constraint_đã_kiểm_kê';
-- Nếu kiểm kê xác nhận không có CHECK trạng thái, đặt giá trị '__NONE__'.
DO $$
DECLARE
    v_name text := current_setting('hrm.status_constraint', true);
BEGIN
    IF v_name IS NULL OR v_name = '' THEN
        RAISE EXCEPTION 'Cần kiểm kê và SET hrm.status_constraint trước khi chạy migration';
    ELSIF v_name <> '__NONE__' THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.don_tu'::regclass AND contype = 'c' AND conname = v_name) THEN
            RAISE EXCEPTION 'Không tìm thấy CHECK đã chỉ định trên don_tu: %', v_name;
        END IF;
        EXECUTE format('ALTER TABLE public.don_tu DROP CONSTRAINT %I', v_name);
    END IF;
END $$;

ALTER TABLE public.don_tu ADD CONSTRAINT don_tu_trang_thai_check
    CHECK (trang_thai IN ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'DA_HUY'));

COMMIT;
