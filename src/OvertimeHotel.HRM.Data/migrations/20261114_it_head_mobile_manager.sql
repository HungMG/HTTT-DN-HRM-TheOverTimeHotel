-- The IT department head uses Manager mobile grants; ordinary IT keeps Employee personal grants.
BEGIN;
GRANT SELECT (ma_chuc_vu) ON public.nhan_vien TO authenticated;
CREATE OR REPLACE FUNCTION public.hrm_is_it_head() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  JOIN public.nhan_vien n USING(ma_nhan_vien)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND v.ten_vai_tro='IT' AND n.ma_chuc_vu=12)
$$;
CREATE OR REPLACE FUNCTION public.hrm_it_head_employee_in_scope(p_employee integer) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_is_it_head() AND EXISTS (
  SELECT 1 FROM public.nhan_vien target JOIN public.nhan_vien me
    ON me.ma_nhan_vien=public.nhan_vien_hien_tai() AND target.ma_phong_ban=me.ma_phong_ban
  WHERE target.ma_nhan_vien=p_employee)
$$;
REVOKE ALL ON FUNCTION public.hrm_is_it_head(), public.hrm_it_head_employee_in_scope(integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_is_it_head(), public.hrm_it_head_employee_in_scope(integer) TO authenticated;

CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND CASE
     WHEN v.ten_vai_tro='IT' AND public.hrm_is_it_head() AND p_code IN
      ('CHAM_CONG_DI_DONG','NOP_DON_TU','XEM_CHAM_CONG_BO_PHAN','XEP_CA_LAM',
       'XEM_DON_NGHI','DUYET_DON_NGHI','DUYET_THOI_VIEC','REPORT_VIEW') THEN
      EXISTS (SELECT 1 FROM public.vai_tro ev JOIN public.vai_tro_quyen vq USING(ma_vai_tro)
       JOIN public.quyen q USING(ma_quyen) WHERE ev.ten_vai_tro='Manager' AND q.ma_quyen_code=p_code)
     WHEN v.ten_vai_tro='IT' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU') THEN
      EXISTS (SELECT 1 FROM public.vai_tro ev JOIN public.vai_tro_quyen vq USING(ma_vai_tro)
       JOIN public.quyen q USING(ma_quyen) WHERE ev.ten_vai_tro='Employee' AND q.ma_quyen_code=p_code)
     ELSE ((v.ten_vai_tro='IT' AND p_code IN
       ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL','CAU_HINH_VI_TRI_CHAM_CONG'))
      OR (v.ten_vai_tro='Admin' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU'))
      OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
       WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
     END)
$$;

-- Preserve the existing restrictive permission checks; add department-scoped
-- permissive policies for the IT head, whose account still has the IT role.
DROP POLICY IF EXISTS hrm_it_head_scope ON public.nhan_vien;
CREATE POLICY hrm_it_head_scope ON public.nhan_vien AS RESTRICTIVE FOR SELECT TO authenticated
 USING (NOT public.hrm_is_it_head() OR ma_nhan_vien=public.nhan_vien_hien_tai()
  OR public.hrm_it_head_employee_in_scope(ma_nhan_vien));
DROP POLICY IF EXISTS hrm_it_head_scope ON public.phan_ca;
CREATE POLICY hrm_it_head_scope ON public.phan_ca AS RESTRICTIVE FOR ALL TO authenticated
 USING (NOT public.hrm_is_it_head() OR ma_nhan_vien=public.nhan_vien_hien_tai()
  OR public.hrm_it_head_employee_in_scope(ma_nhan_vien))
 WITH CHECK (NOT public.hrm_is_it_head() OR ma_nhan_vien=public.nhan_vien_hien_tai()
  OR public.hrm_it_head_employee_in_scope(ma_nhan_vien));
DROP POLICY IF EXISTS hrm_it_head_scope ON public.cham_cong;
CREATE POLICY hrm_it_head_scope ON public.cham_cong AS RESTRICTIVE FOR ALL TO authenticated
 USING (NOT public.hrm_is_it_head() OR EXISTS (SELECT 1 FROM public.phan_ca p
  WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND (p.ma_nhan_vien=public.nhan_vien_hien_tai()
   OR public.hrm_it_head_employee_in_scope(p.ma_nhan_vien))))
 WITH CHECK (NOT public.hrm_is_it_head() OR EXISTS (SELECT 1 FROM public.phan_ca p
  WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND (p.ma_nhan_vien=public.nhan_vien_hien_tai()
   OR public.hrm_it_head_employee_in_scope(p.ma_nhan_vien))));
DROP POLICY IF EXISTS hrm_it_head_scope ON public.don_tu;
CREATE POLICY hrm_it_head_scope ON public.don_tu AS RESTRICTIVE FOR ALL TO authenticated
 USING (NOT public.hrm_is_it_head() OR ma_nhan_vien=public.nhan_vien_hien_tai()
  OR public.hrm_it_head_employee_in_scope(ma_nhan_vien))
 WITH CHECK (NOT public.hrm_is_it_head() OR ma_nhan_vien=public.nhan_vien_hien_tai()
  OR public.hrm_it_head_employee_in_scope(ma_nhan_vien));
DROP POLICY IF EXISTS hrm_it_head_read ON public.nhan_vien;
CREATE POLICY hrm_it_head_read ON public.nhan_vien FOR SELECT TO authenticated
 USING (public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND
  (public.hrm_has_permission('XEM_DON_NGHI') OR public.hrm_has_permission('DUYET_THOI_VIEC') OR
   public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN') OR public.hrm_has_permission('XEP_CA_LAM')));
DROP POLICY IF EXISTS hrm_it_head_read ON public.phan_ca;
CREATE POLICY hrm_it_head_read ON public.phan_ca FOR SELECT TO authenticated
 USING (public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND
  (public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN') OR public.hrm_has_permission('XEP_CA_LAM')));
DROP POLICY IF EXISTS hrm_it_head_write ON public.phan_ca;
CREATE POLICY hrm_it_head_write ON public.phan_ca FOR ALL TO authenticated
 USING (public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND public.hrm_has_permission('XEP_CA_LAM'))
 WITH CHECK (public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND public.hrm_has_permission('XEP_CA_LAM'));
DROP POLICY IF EXISTS hrm_it_head_read ON public.cham_cong;
CREATE POLICY hrm_it_head_read ON public.cham_cong FOR SELECT TO authenticated
 USING (public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN') AND EXISTS
  (SELECT 1 FROM public.phan_ca p WHERE p.ma_phan_ca=cham_cong.ma_phan_ca
   AND public.hrm_it_head_employee_in_scope(p.ma_nhan_vien)));

DROP POLICY IF EXISTS hrm_permission_read ON public.don_tu;
CREATE POLICY hrm_permission_read ON public.don_tu AS RESTRICTIVE FOR SELECT TO authenticated
 USING (ma_nhan_vien=public.nhan_vien_hien_tai() OR
  (((ma_loai_don BETWEEN 1 AND 4 AND public.hrm_has_permission('XEM_DON_NGHI')) OR
    (ma_loai_don=5 AND public.hrm_has_permission('DUYET_THOI_VIEC'))) AND
   (public.vai_tro_hien_tai() IN (1,2) OR
    (public.vai_tro_hien_tai()=3 AND public.phong_ban_cua_don(ma_don)=public.phong_ban_hien_tai()) OR
    public.hrm_it_head_employee_in_scope(ma_nhan_vien))));
DROP POLICY IF EXISTS hrm_it_head_read ON public.don_tu;
CREATE POLICY hrm_it_head_read ON public.don_tu FOR SELECT TO authenticated
 USING (public.hrm_it_head_employee_in_scope(ma_nhan_vien));
DROP POLICY IF EXISTS hrm_it_head_update ON public.don_tu;
CREATE POLICY hrm_it_head_update ON public.don_tu FOR UPDATE TO authenticated
 USING (ma_nhan_vien<>public.nhan_vien_hien_tai() AND public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND
   public.hrm_has_permission(CASE WHEN ma_loai_don=5 THEN 'DUYET_THOI_VIEC' ELSE 'DUYET_DON_NGHI' END))
 WITH CHECK (ma_nhan_vien<>public.nhan_vien_hien_tai() AND public.hrm_it_head_employee_in_scope(ma_nhan_vien) AND
   public.hrm_has_permission(CASE WHEN ma_loai_don=5 THEN 'DUYET_THOI_VIEC' ELSE 'DUYET_DON_NGHI' END));
NOTIFY pgrst, 'reload schema';
COMMIT;
