-- Migration 2 (Giai đoạn 3): Mở rộng trạng thái bảng giai_trinh_cham_cong thêm 'DA_HUY'
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
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.giai_trinh_cham_cong'::regclass AND contype = 'c' AND conname = v_name) THEN
            RAISE EXCEPTION 'Không tìm thấy CHECK đã chỉ định trên giai_trinh_cham_cong: %', v_name;
        END IF;
        EXECUTE format('ALTER TABLE public.giai_trinh_cham_cong DROP CONSTRAINT %I', v_name);
    END IF;
END $$;

ALTER TABLE public.giai_trinh_cham_cong ADD CONSTRAINT chk_giai_trinh_trang_thai
    CHECK (trang_thai IN ('CHO_QUAN_LY', 'CHO_HR', 'DA_DUYET', 'TU_CHOI', 'DA_HUY'));

COMMIT;
