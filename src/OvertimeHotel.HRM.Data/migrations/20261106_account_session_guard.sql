BEGIN;

ALTER TABLE public.tai_khoan ADD COLUMN IF NOT EXISTS phien_hop_le_sau timestamptz NOT NULL DEFAULT '1970-01-01 00:00:00+00';

CREATE OR REPLACE FUNCTION public.hrm_account_session_valid() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT coalesce(auth.uid() IS NOT NULL AND EXISTS (
   SELECT 1 FROM public.tai_khoan t
   WHERE t.ma_nhan_vien::text = auth.jwt()->'app_metadata'->>'ma_nhan_vien'
     AND t.ma_vai_tro::text = auth.jwt()->'app_metadata'->>'ma_vai_tro'
     AND t.trang_thai
     AND EXISTS (SELECT 1 FROM auth.sessions s
       WHERE s.user_id = auth.uid()
         AND s.id = CASE WHEN coalesce(auth.jwt()->>'session_id','') ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
           THEN (auth.jwt()->>'session_id')::uuid ELSE NULL END
         AND s.created_at > t.phien_hop_le_sau)
     AND CASE WHEN coalesce(auth.jwt()->>'iat','') ~ '^[0-9]{1,12}$'
       THEN (auth.jwt()->>'iat')::numeric > extract(epoch FROM t.phien_hop_le_sau)
       ELSE false END
 ), false)
$$;

CREATE OR REPLACE FUNCTION public.nhan_vien_hien_tai() RETURNS integer
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT CASE WHEN public.hrm_account_session_valid() THEN nullif(auth.jwt()->'app_metadata'->>'ma_nhan_vien','')::integer END
$$;
CREATE OR REPLACE FUNCTION public.vai_tro_hien_tai() RETURNS integer
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT CASE WHEN public.hrm_account_session_valid() THEN nullif(auth.jwt()->'app_metadata'->>'ma_vai_tro','')::integer END
$$;
CREATE OR REPLACE FUNCTION public.la_quan_ly_hien_tai() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT coalesce(public.vai_tro_hien_tai() IN (1,2,3), false)
$$;

CREATE OR REPLACE FUNCTION public.phong_ban_cua_don(p_ma_don integer) RETURNS integer
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT nv.ma_phong_ban FROM public.don_tu dt JOIN public.nhan_vien nv ON nv.ma_nhan_vien=dt.ma_nhan_vien
 WHERE dt.ma_don=p_ma_don AND (coalesce(current_setting('role',true),'none') NOT IN ('authenticated','anon') OR public.hrm_account_session_valid())
$$;
CREATE OR REPLACE FUNCTION public.nhan_vien_co_hop_dong_hieu_luc(p_ma_nhan_vien integer, p_ngay date) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
 SELECT (coalesce(current_setting('role',true),'none') NOT IN ('authenticated','anon') OR public.hrm_account_session_valid())
 AND (p_ngay IS NULL OR (EXISTS (SELECT 1 FROM public.nhan_vien nv WHERE nv.ma_nhan_vien=p_ma_nhan_vien AND nv.trang_thai='DANG_LAM')
 AND EXISTS (SELECT 1 FROM public.hop_dong hd WHERE hd.ma_nhan_vien=p_ma_nhan_vien AND hd.trang_thai='HIEU_LUC'
   AND hd.ngay_bat_dau<=p_ngay AND (hd.ngay_ket_thuc IS NULL OR hd.ngay_ket_thuc>=p_ngay))))
$$;

-- A restrictive policy is ANDed with every existing permissive policy.
DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE n.nspname='public' AND c.relkind IN ('r','p') AND c.relrowsecurity LOOP
   EXECUTE format('DROP POLICY IF EXISTS hrm_active_account ON public.%I', t.relname);
   EXECUTE format('CREATE POLICY hrm_active_account ON public.%I AS RESTRICTIVE FOR ALL TO authenticated USING (public.hrm_account_session_valid()) WITH CHECK (public.hrm_account_session_valid())', t.relname);
 END LOOP;
END $$;

CREATE OR REPLACE FUNCTION public.hrm_invalidate_account_session() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
 IF NEW.trang_thai IS DISTINCT FROM OLD.trang_thai OR NEW.ma_vai_tro IS DISTINCT FROM OLD.ma_vai_tro THEN
  NEW.phien_hop_le_sau := clock_timestamp();
 END IF;
 RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS hrm_invalidate_account_session ON public.tai_khoan;
CREATE TRIGGER hrm_invalidate_account_session BEFORE UPDATE ON public.tai_khoan FOR EACH ROW EXECUTE FUNCTION public.hrm_invalidate_account_session();

COMMIT;
