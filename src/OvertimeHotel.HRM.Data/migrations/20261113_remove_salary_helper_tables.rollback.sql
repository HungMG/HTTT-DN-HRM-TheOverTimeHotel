-- Restore the helper tables from the preserved columns; take a fresh backup first.
-- Run before 20261111_salary_bands.rollback.sql; the older rollback has additional guards.
BEGIN;
SET LOCAL lock_timeout='8s';
SET LOCAL statement_timeout='90s';
LOCK TABLE public.nhan_vien, public.chuc_vu, public.hop_dong, public.phu_luc_luong, public.khung_luong_cap_bac IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
 IF to_regclass('public.salary_write_guard') IS NOT NULL OR to_regclass('public.salary_position_baseline') IS NOT NULL
 OR NOT EXISTS (SELECT FROM pg_trigger WHERE tgrelid='public.nhan_vien'::regclass AND tgname='salary_baseline_immutable' AND NOT tgisinternal)
 OR NOT EXISTS (SELECT FROM pg_trigger WHERE tgrelid='public.nhan_vien'::regclass AND tgname='salary_baseline_no_insert' AND NOT tgisinternal)
 OR NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.nhan_vien'::regclass AND conname='fk_nhan_vien_chuc_vu_ghi_nhan')
 OR (SELECT count(*) FROM public.khung_luong_cap_bac WHERE nhom_chuc_vu='TONG_GIAM_DOC')<>1 THEN RAISE EXCEPTION 'Rollback inventory mismatch; stop'; END IF;
END $$;
CREATE TABLE public.salary_write_guard (singleton boolean PRIMARY KEY CHECK(singleton), revision bigint NOT NULL);
INSERT INTO public.salary_write_guard SELECT true,phien_ban_bao_ve FROM public.khung_luong_cap_bac WHERE nhom_chuc_vu='TONG_GIAM_DOC';
CREATE TABLE public.salary_position_baseline (ma_nhan_vien integer PRIMARY KEY REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE CASCADE, observed_date date NOT NULL, ma_chuc_vu integer REFERENCES public.chuc_vu(ma_chuc_vu) ON DELETE RESTRICT);
INSERT INTO public.salary_position_baseline SELECT ma_nhan_vien,ngay_ghi_nhan_chuc_vu,ma_chuc_vu_ghi_nhan FROM public.nhan_vien WHERE ngay_ghi_nhan_chuc_vu IS NOT NULL;
ALTER TABLE public.salary_position_baseline ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.salary_write_guard ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.salary_position_baseline,public.salary_write_guard FROM PUBLIC,anon,authenticated;
GRANT SELECT ON public.salary_position_baseline TO service_role;
GRANT SELECT,INSERT,UPDATE,DELETE ON public.salary_write_guard TO service_role;
COMMENT ON TABLE public.salary_write_guard IS 'salary_bands:20261111:v1';
COMMENT ON TABLE public.salary_position_baseline IS 'salary_bands:20261111:v1';
CREATE OR REPLACE FUNCTION public.salary_serialize_write() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    UPDATE public.salary_write_guard SET revision=revision+1 WHERE singleton;
    IF NOT FOUND THEN RAISE EXCEPTION 'Salary serialization row is missing'; END IF;
    RETURN NULL;
END $$;
CREATE OR REPLACE FUNCTION public.salary_check_gm_timeline() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    -- Old rows have no historical snapshot. Treat the known current GM conservatively;
    -- never infer a past promotion date or silently rewrite business history.
    IF EXISTS (
        WITH events AS (
            -- A signed role history supersedes the observed projection. Retain the
            -- projection when the first signed event is still in the future.
            SELECT b.ma_nhan_vien,greatest(b.observed_date,n.ngay_vao_lam) AS effective,b.ma_chuc_vu AS position,
                   0 AS priority,0::bigint AS source_id
            FROM public.salary_position_baseline b JOIN public.nhan_vien n USING (ma_nhan_vien)
            WHERE NOT EXISTS (SELECT FROM public.hop_dong h WHERE h.ma_nhan_vien=b.ma_nhan_vien
                AND h.ma_chuc_vu_ky_ket IS NOT NULL AND h.ngay_bat_dau<=b.observed_date)
              AND NOT EXISTS (SELECT FROM public.hop_dong h JOIN public.phu_luc_luong p USING (ma_hop_dong)
                WHERE h.ma_nhan_vien=b.ma_nhan_vien AND p.ma_chuc_vu_moi IS NOT NULL AND p.ngay_hieu_luc<=b.observed_date)
            UNION ALL
            -- New employees created after migration normally sign a snapshot at hire.
            SELECT n.ma_nhan_vien,n.ngay_vao_lam,n.ma_chuc_vu,0,0::bigint
            FROM public.nhan_vien n WHERE NOT EXISTS (
                SELECT FROM public.salary_position_baseline b WHERE b.ma_nhan_vien=n.ma_nhan_vien)
              AND (n.trang_thai IN ('DANG_LAM','TAM_NGHI') OR n.ngay_thoi_viec IS NOT NULL)
            UNION ALL
            SELECT h.ma_nhan_vien,h.ngay_bat_dau,h.ma_chuc_vu_ky_ket,1,h.ma_hop_dong::bigint
            FROM public.hop_dong h WHERE h.ma_chuc_vu_ky_ket IS NOT NULL
            UNION ALL
            SELECT h.ma_nhan_vien,p.ngay_hieu_luc,p.ma_chuc_vu_moi,2,p.ma_phu_luc
            FROM public.hop_dong h JOIN public.phu_luc_luong p USING (ma_hop_dong)
            WHERE p.ma_chuc_vu_moi IS NOT NULL
        ), effective_events AS (
            SELECT DISTINCT ON (ma_nhan_vien,effective) * FROM events
            ORDER BY ma_nhan_vien,effective,priority DESC,source_id DESC
        ), ordered AS (
            SELECT *,lead(effective) OVER (PARTITION BY ma_nhan_vien ORDER BY effective) AS next_date
            FROM effective_events
        ), seats AS (
            SELECT o.ma_nhan_vien, daterange(o.effective,
                least(coalesce(o.next_date,'infinity'::date),
                      CASE WHEN n.ngay_thoi_viec IS NULL THEN 'infinity'::date ELSE n.ngay_thoi_viec+1 END),'[)') AS during
            FROM ordered o JOIN public.chuc_vu c ON c.ma_chuc_vu=o.position
            JOIN public.nhan_vien n ON n.ma_nhan_vien=o.ma_nhan_vien
            WHERE c.nhom_luong='TONG_GIAM_DOC'
              AND (n.ngay_thoi_viec IS NULL OR o.effective<=n.ngay_thoi_viec)
        )
        SELECT FROM seats a JOIN seats b ON a.ma_nhan_vien<b.ma_nhan_vien AND a.during && b.during
    ) THEN RAISE EXCEPTION 'General director appointments overlap; verify dated history'; END IF;
    RETURN NULL;
END $$;
DROP TRIGGER salary_baseline_no_insert ON public.nhan_vien;
DROP TRIGGER salary_baseline_immutable ON public.nhan_vien;
DROP FUNCTION public.salary_baseline_immutable();
ALTER TABLE public.nhan_vien DROP CONSTRAINT fk_nhan_vien_chuc_vu_ghi_nhan, DROP COLUMN ngay_ghi_nhan_chuc_vu, DROP COLUMN ma_chuc_vu_ghi_nhan;
ALTER TABLE public.khung_luong_cap_bac DROP COLUMN phien_ban_bao_ve;
SET CONSTRAINTS ALL IMMEDIATE;
COMMIT;
