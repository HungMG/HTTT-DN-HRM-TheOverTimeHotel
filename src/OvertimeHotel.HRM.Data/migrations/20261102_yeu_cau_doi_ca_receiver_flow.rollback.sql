-- Restore the constraints inventoried on the configured Supabase database on
-- 2026-10-30. Recheck names and definitions on the target before execution.
-- Stop writes, back up, and approve any data handling before running rollback.
BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public.yeu_cau_doi_ca
        WHERE trang_thai = 'CHO_NGUOI_NHAN' OR ben_tu_choi = 'NGUOI_NHAN'
           OR phan_hoi_nguoi_nhan IS NOT NULL OR ngay_nguoi_nhan_phan_hoi IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'Dừng rollback: có dữ liệu phản hồi người nhận cần phương án bảo toàn được duyệt';
    END IF;
END $$;

ALTER TABLE public.yeu_cau_doi_ca DROP CONSTRAINT chk_doi_ca_tu_choi_can_ly_do;
ALTER TABLE public.yeu_cau_doi_ca DROP CONSTRAINT chk_doi_ca_ben_tu_choi;
ALTER TABLE public.yeu_cau_doi_ca DROP CONSTRAINT chk_doi_ca_trang_thai;
ALTER TABLE public.yeu_cau_doi_ca
    DROP COLUMN ben_tu_choi,
    DROP COLUMN phan_hoi_nguoi_nhan,
    DROP COLUMN ngay_nguoi_nhan_phan_hoi;
ALTER TABLE public.yeu_cau_doi_ca ALTER COLUMN trang_thai SET DEFAULT 'CHO_DUYET';
ALTER TABLE public.yeu_cau_doi_ca ADD CONSTRAINT yeu_cau_doi_ca_trang_thai_check
    CHECK (trang_thai IN ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'DA_HUY'));
ALTER TABLE public.yeu_cau_doi_ca ADD CONSTRAINT chk_doi_ca_tu_choi_can_ly_do
    CHECK (trang_thai <> 'TU_CHOI' OR (phan_hoi_duyet IS NOT NULL AND length(trim(phan_hoi_duyet)) > 0));

COMMIT;
