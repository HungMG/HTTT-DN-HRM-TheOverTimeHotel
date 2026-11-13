-- Align Mobile with the live role grants without resetting existing grants.
BEGIN;
-- Role grants are managed in the permission screen.
CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND CASE
     WHEN v.ten_vai_tro='IT' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU') THEN
      EXISTS (SELECT 1 FROM public.vai_tro ev JOIN public.vai_tro_quyen vq USING(ma_vai_tro)
       JOIN public.quyen q USING(ma_quyen) WHERE ev.ten_vai_tro='Employee' AND q.ma_quyen_code=p_code)
     ELSE ((v.ten_vai_tro='IT' AND p_code IN
       ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL','CAU_HINH_VI_TRI_CHAM_CONG'))
      OR (v.ten_vai_tro='Admin' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU'))
      OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
       WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
     END
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
