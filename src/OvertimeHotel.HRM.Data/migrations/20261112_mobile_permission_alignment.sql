-- Align Mobile with the live role grants without resetting existing grants.
BEGIN;
-- Admin previously inherited these two personal capabilities. Preserve them as
-- explicit grants so that subsequent changes on the permission page take effect.
CREATE TABLE IF NOT EXISTS public.hrm_mobile_migration_history (migration_id text PRIMARY KEY);
REVOKE ALL ON public.hrm_mobile_migration_history FROM PUBLIC, anon, authenticated;
WITH first_run AS (
 INSERT INTO public.hrm_mobile_migration_history VALUES ('20261112_mobile_permission_alignment')
 ON CONFLICT DO NOTHING RETURNING migration_id
)
INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT v.ma_vai_tro, q.ma_quyen FROM public.vai_tro v CROSS JOIN public.quyen q
WHERE EXISTS (SELECT 1 FROM first_run) AND v.ten_vai_tro='Admin' AND q.ma_quyen_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU')
AND NOT EXISTS (SELECT 1 FROM public.vai_tro_quyen x WHERE x.ma_vai_tro=v.ma_vai_tro AND x.ma_quyen=q.ma_quyen);

CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND ((v.ten_vai_tro='IT' AND p_code IN
      ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL','CHAM_CONG_DI_DONG','NOP_DON_TU'))
     OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
      WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
 )
$$;

DROP POLICY IF EXISTS hrm_permission_read ON public.don_tu;
CREATE POLICY hrm_permission_read ON public.don_tu AS RESTRICTIVE FOR SELECT TO authenticated
USING (
 ma_nhan_vien=public.nhan_vien_hien_tai()
 OR (
   ((ma_loai_don BETWEEN 1 AND 4 AND public.hrm_has_permission('XEM_DON_NGHI'))
     OR (ma_loai_don=5 AND public.hrm_has_permission('DUYET_THOI_VIEC')))
   AND (public.vai_tro_hien_tai() IN (1,2)
     OR (public.vai_tro_hien_tai()=3 AND public.phong_ban_cua_don(ma_don)=public.phong_ban_hien_tai()))
 )
);

-- A granted approval applies to the permitted department; a role name does not
-- silently exclude a request type. The restrictive permission policy remains in force.
DROP POLICY IF EXISTS duyet_don_tu_quan_ly ON public.don_tu;
CREATE POLICY duyet_don_tu_quan_ly ON public.don_tu FOR UPDATE TO authenticated
USING (ma_loai_don BETWEEN 1 AND 5 AND
 (public.vai_tro_hien_tai()=1 OR
  (ma_nhan_vien IS DISTINCT FROM public.nhan_vien_hien_tai() AND
   (public.vai_tro_hien_tai()=2 OR (public.vai_tro_hien_tai()=3 AND
    public.phong_ban_cua_don(ma_don)=public.phong_ban_hien_tai())))))
WITH CHECK (ma_loai_don BETWEEN 1 AND 5 AND
 (public.vai_tro_hien_tai()=1 OR
  (ma_nhan_vien IS DISTINCT FROM public.nhan_vien_hien_tai() AND
   (public.vai_tro_hien_tai()=2 OR (public.vai_tro_hien_tai()=3 AND
    public.phong_ban_cua_don(ma_don)=public.phong_ban_hien_tai())))));

-- Direct PostgREST reads are always limited to the signed-in person's published slip.
DROP POLICY IF EXISTS hrm_mobile_own_published ON public.phieu_luong;
CREATE POLICY hrm_mobile_own_published ON public.phieu_luong AS RESTRICTIVE FOR SELECT TO authenticated
USING (ma_nhan_vien=public.nhan_vien_hien_tai() AND trang_thai='DA_THANH_TOAN');
DROP POLICY IF EXISTS hrm_mobile_own_published ON public.chi_tiet_phieu_luong;
CREATE POLICY hrm_mobile_own_published ON public.chi_tiet_phieu_luong AS RESTRICTIVE FOR SELECT TO authenticated
USING (EXISTS (SELECT 1 FROM public.phieu_luong p WHERE p.ma_phieu_luong=chi_tiet_phieu_luong.ma_phieu_luong
 AND p.ma_nhan_vien=public.nhan_vien_hien_tai() AND p.trang_thai='DA_THANH_TOAN'));
NOTIFY pgrst, 'reload schema';
COMMIT;
