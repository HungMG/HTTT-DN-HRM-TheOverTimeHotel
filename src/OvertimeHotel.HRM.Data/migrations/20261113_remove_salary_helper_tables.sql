-- Apply after 20261111_salary_bands.sql. Back up the two helper tables first.
-- Moves their information into existing tables while retaining the salary and GM guards.
-- To undo, run 20261113_remove_salary_helper_tables.rollback.sql before any older rollback.
BEGIN;
SET LOCAL lock_timeout='8s';
SET LOCAL statement_timeout='90s';
LOCK TABLE public.salary_write_guard, public.salary_position_baseline, public.nhan_vien, public.chuc_vu, public.hop_dong, public.phu_luc_luong, public.khung_luong_cap_bac IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
 IF (SELECT count(*) FROM public.salary_write_guard)<>1 OR (SELECT count(*) FROM public.khung_luong_cap_bac WHERE nhom_chuc_vu='TONG_GIAM_DOC')<>1
 OR (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('salary_serialize_hop_dong','salary_serialize_nhan_vien','salary_serialize_phu_luc','salary_serialize_chuc_vu','salary_guard_appendix','salary_check_hop_dong','salary_check_nhan_vien','salary_check_phu_luc','salary_check_chuc_vu'))<>9
 OR obj_description('public.salary_write_guard'::regclass,'pg_class') IS DISTINCT FROM 'salary_bands:20261111:v1'
 OR obj_description('public.salary_position_baseline'::regclass,'pg_class') IS DISTINCT FROM 'salary_bands:20261111:v1'
 OR EXISTS (SELECT FROM information_schema.columns WHERE table_schema='public' AND ((table_name='nhan_vien' AND column_name IN ('ngay_ghi_nhan_chuc_vu','ma_chuc_vu_ghi_nhan')) OR (table_name='khung_luong_cap_bac' AND column_name='phien_ban_bao_ve')))
 THEN RAISE EXCEPTION 'Salary schema inventory mismatch; stop'; END IF;
END $$;
ALTER TABLE public.nhan_vien ADD COLUMN ngay_ghi_nhan_chuc_vu date, ADD COLUMN ma_chuc_vu_ghi_nhan integer,
 ADD CONSTRAINT fk_nhan_vien_chuc_vu_ghi_nhan FOREIGN KEY (ma_chuc_vu_ghi_nhan) REFERENCES public.chuc_vu(ma_chuc_vu) ON DELETE RESTRICT;
ALTER TABLE public.khung_luong_cap_bac ADD COLUMN phien_ban_bao_ve bigint NOT NULL DEFAULT 0;
COMMENT ON COLUMN public.nhan_vien.ngay_ghi_nhan_chuc_vu IS 'Ngày ghi nhận mốc chức vụ cũ, không suy đoán ngày bổ nhiệm.';
COMMENT ON COLUMN public.nhan_vien.ma_chuc_vu_ghi_nhan IS 'Chức vụ được ghi nhận tại ngày mốc, không thay đổi khi điều chuyển.';
COMMENT ON COLUMN public.khung_luong_cap_bac.phien_ban_bao_ve IS 'Bộ đếm đồng bộ giao dịch, không phải dữ liệu mức lương.';
UPDATE public.nhan_vien n SET ngay_ghi_nhan_chuc_vu=b.observed_date,ma_chuc_vu_ghi_nhan=b.ma_chuc_vu FROM public.salary_position_baseline b WHERE n.ma_nhan_vien=b.ma_nhan_vien;
DO $$ BEGIN
 IF EXISTS(SELECT FROM public.salary_position_baseline b JOIN public.nhan_vien n USING(ma_nhan_vien) WHERE n.ngay_ghi_nhan_chuc_vu IS DISTINCT FROM b.observed_date OR n.ma_chuc_vu_ghi_nhan IS DISTINCT FROM b.ma_chuc_vu)
 OR (SELECT count(*) FROM public.nhan_vien WHERE ngay_ghi_nhan_chuc_vu IS NOT NULL)<>(SELECT count(*) FROM public.salary_position_baseline)
 THEN RAISE EXCEPTION 'Baseline copy mismatch'; END IF;
END $$;
UPDATE public.khung_luong_cap_bac SET phien_ban_bao_ve=(SELECT revision FROM public.salary_write_guard WHERE singleton) WHERE nhom_chuc_vu='TONG_GIAM_DOC';
CREATE OR REPLACE FUNCTION public.salary_serialize_write() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    UPDATE public.khung_luong_cap_bac SET phien_ban_bao_ve=phien_ban_bao_ve+1 WHERE nhom_chuc_vu='TONG_GIAM_DOC';
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
            SELECT n.ma_nhan_vien,greatest(n.ngay_ghi_nhan_chuc_vu,n.ngay_vao_lam) AS effective,n.ma_chuc_vu_ghi_nhan AS position,
                   0 AS priority,0::bigint AS source_id
            FROM public.nhan_vien n
            WHERE n.ngay_ghi_nhan_chuc_vu IS NOT NULL AND NOT EXISTS (SELECT FROM public.hop_dong h WHERE h.ma_nhan_vien=n.ma_nhan_vien
                AND h.ma_chuc_vu_ky_ket IS NOT NULL AND h.ngay_bat_dau<=n.ngay_ghi_nhan_chuc_vu)
              AND NOT EXISTS (SELECT FROM public.hop_dong h JOIN public.phu_luc_luong p USING (ma_hop_dong)
                WHERE h.ma_nhan_vien=n.ma_nhan_vien AND p.ma_chuc_vu_moi IS NOT NULL AND p.ngay_hieu_luc<=n.ngay_ghi_nhan_chuc_vu)
            UNION ALL
            -- New employees created after migration normally sign a snapshot at hire.
            SELECT n.ma_nhan_vien,n.ngay_vao_lam,n.ma_chuc_vu,0,0::bigint
            FROM public.nhan_vien n WHERE n.ngay_ghi_nhan_chuc_vu IS NULL
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
CREATE FUNCTION public.salary_baseline_immutable() RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
 IF TG_OP='INSERT' THEN
  IF NEW.ngay_ghi_nhan_chuc_vu IS NOT NULL OR NEW.ma_chuc_vu_ghi_nhan IS NOT NULL THEN RAISE EXCEPTION 'Observed salary position baseline is immutable'; END IF;
 ELSIF NEW.ngay_ghi_nhan_chuc_vu IS DISTINCT FROM OLD.ngay_ghi_nhan_chuc_vu OR NEW.ma_chuc_vu_ghi_nhan IS DISTINCT FROM OLD.ma_chuc_vu_ghi_nhan THEN RAISE EXCEPTION 'Observed salary position baseline is immutable'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER salary_baseline_immutable BEFORE UPDATE OF ngay_ghi_nhan_chuc_vu,ma_chuc_vu_ghi_nhan ON public.nhan_vien FOR EACH ROW EXECUTE FUNCTION public.salary_baseline_immutable();
CREATE TRIGGER salary_baseline_no_insert BEFORE INSERT ON public.nhan_vien FOR EACH ROW EXECUTE FUNCTION public.salary_baseline_immutable();
REVOKE ALL ON FUNCTION public.salary_baseline_immutable() FROM PUBLIC, anon, authenticated;
DROP TABLE public.salary_position_baseline;
DROP TABLE public.salary_write_guard;
SET CONSTRAINTS ALL IMMEDIATE;
COMMIT;
