-- Migration 3 (Giai đoạn 4): Luồng đổi ca 3 bước có Người nhận xác nhận
-- Thêm CHO_NGUOI_NHAN, thêm các cột lưu vết bên từ chối và phản hồi của người nhận
BEGIN;

-- 1. Cập nhật constraint danh mục trạng thái
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
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.yeu_cau_doi_ca'::regclass AND contype = 'c' AND conname = v_name) THEN
            RAISE EXCEPTION 'Không tìm thấy CHECK đã chỉ định trên yeu_cau_doi_ca: %', v_name;
        END IF;
        EXECUTE format('ALTER TABLE public.yeu_cau_doi_ca DROP CONSTRAINT %I', v_name);
    END IF;
END $$;

ALTER TABLE public.yeu_cau_doi_ca ADD CONSTRAINT chk_doi_ca_trang_thai
    CHECK (trang_thai IN ('CHO_NGUOI_NHAN', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'DA_HUY'));

ALTER TABLE public.yeu_cau_doi_ca ALTER COLUMN trang_thai SET DEFAULT 'CHO_NGUOI_NHAN';

-- 2. Thêm 3 cột phục vụ lưu vết Người nhận B và bên từ chối
ALTER TABLE public.yeu_cau_doi_ca
    ADD COLUMN IF NOT EXISTS ben_tu_choi VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS phan_hoi_nguoi_nhan VARCHAR(500) NULL,
    ADD COLUMN IF NOT EXISTS ngay_nguoi_nhan_phan_hoi TIMESTAMPTZ NULL;

ALTER TABLE public.yeu_cau_doi_ca ADD CONSTRAINT chk_doi_ca_ben_tu_choi
    CHECK (ben_tu_choi IS NULL OR ben_tu_choi IN ('NGUOI_NHAN', 'QUAN_LY'));

-- Chỉ định CHECK từ chối cũ sau kiểm kê, hoặc '__NONE__' nếu không có.
-- SET hrm.rejection_constraint = 'tên_constraint_đã_kiểm_kê';
DO $$
DECLARE v_name text := current_setting('hrm.rejection_constraint', true);
BEGIN
    IF v_name IS NULL OR v_name = '' THEN
        RAISE EXCEPTION 'Cần kiểm kê và SET hrm.rejection_constraint trước khi chạy migration';
    ELSIF v_name <> '__NONE__' THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.yeu_cau_doi_ca'::regclass AND contype = 'c' AND conname = v_name) THEN
            RAISE EXCEPTION 'Không tìm thấy CHECK từ chối đã chỉ định: %', v_name;
        END IF;
        EXECUTE format('ALTER TABLE public.yeu_cau_doi_ca DROP CONSTRAINT %I', v_name);
    END IF;
END $$;

-- 3. Cập nhật constraint từ chối cần lý do (chặn triệt để SQL 3-valued logic bằng coalesce)
ALTER TABLE public.yeu_cau_doi_ca ADD CONSTRAINT chk_doi_ca_tu_choi_can_ly_do
    CHECK (
        trang_thai <> 'TU_CHOI' OR (ben_tu_choi IS NOT NULL AND (
            (ben_tu_choi = 'NGUOI_NHAN' AND coalesce(length(trim(phan_hoi_nguoi_nhan)), 0) > 0)
            OR
            (ben_tu_choi = 'QUAN_LY' AND coalesce(length(trim(phan_hoi_duyet)), 0) > 0)
        ))
    );

COMMIT;
