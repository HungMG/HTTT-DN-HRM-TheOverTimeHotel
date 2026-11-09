-- Live permissions supplement existing role, department and active-session policies.
BEGIN;
CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
   AND CASE
    WHEN p_code IN ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL') THEN v.ten_vai_tro='IT'
    WHEN v.ten_vai_tro='IT' THEN false
    WHEN p_code='QL_SU_KIEN' THEN v.ten_vai_tro IN ('HR','Manager','Admin') AND EXISTS(
     SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
     WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code)
    WHEN v.ten_vai_tro='Admin' THEN true
    ELSE EXISTS(SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
     WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code)
   END)
$$;
CREATE OR REPLACE FUNCTION public.hrm_my_permissions() RETURNS jsonb
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT coalesce(jsonb_agg(q.ma_quyen_code ORDER BY q.ma_quyen_code),'[]'::jsonb)
 FROM public.quyen q WHERE public.hrm_has_permission(q.ma_quyen_code)
$$;
REVOKE ALL ON FUNCTION public.hrm_has_permission(text), public.hrm_my_permissions() FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_has_permission(text), public.hrm_my_permissions() TO authenticated;

-- Restrictive policies are ANDed with the existing role/scope policies.
CREATE POLICY hrm_permission_read ON public.cham_cong AS RESTRICTIVE FOR SELECT TO authenticated
 USING (EXISTS(SELECT 1 FROM public.phan_ca p WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND
  (p.ma_nhan_vien=public.nhan_vien_hien_tai() OR public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN'))));
CREATE POLICY hrm_permission_insert ON public.cham_cong AS RESTRICTIVE FOR INSERT TO authenticated
 WITH CHECK((SELECT public.hrm_has_permission('CHAM_CONG_DI_DONG')) AND EXISTS(SELECT 1 FROM public.phan_ca p WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND p.ma_nhan_vien=(SELECT public.nhan_vien_hien_tai())));
CREATE POLICY hrm_permission_update ON public.cham_cong AS RESTRICTIVE FOR UPDATE TO authenticated
 USING((SELECT public.hrm_has_permission('CHAM_CONG_DI_DONG')) AND EXISTS(SELECT 1 FROM public.phan_ca p WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND p.ma_nhan_vien=(SELECT public.nhan_vien_hien_tai()))) WITH CHECK((SELECT public.hrm_has_permission('CHAM_CONG_DI_DONG')) AND EXISTS(SELECT 1 FROM public.phan_ca p WHERE p.ma_phan_ca=cham_cong.ma_phan_ca AND p.ma_nhan_vien=(SELECT public.nhan_vien_hien_tai())));
CREATE POLICY hrm_permission_read ON public.phan_ca AS RESTRICTIVE FOR SELECT TO authenticated
 USING(ma_nhan_vien=public.nhan_vien_hien_tai() OR public.hrm_has_permission('XEP_CA_LAM') OR public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN'));
CREATE POLICY hrm_permission_insert ON public.phan_ca AS RESTRICTIVE FOR INSERT TO authenticated
 WITH CHECK(public.hrm_has_permission('XEP_CA_LAM'));
CREATE POLICY hrm_permission_update ON public.phan_ca AS RESTRICTIVE FOR UPDATE TO authenticated
 USING(public.hrm_has_permission('XEP_CA_LAM')) WITH CHECK(public.hrm_has_permission('XEP_CA_LAM'));
CREATE POLICY hrm_permission_delete ON public.phan_ca AS RESTRICTIVE FOR DELETE TO authenticated
 USING(public.hrm_has_permission('XEP_CA_LAM'));
CREATE POLICY hrm_permission_read ON public.don_tu AS RESTRICTIVE FOR SELECT TO authenticated
 USING(ma_nhan_vien=public.nhan_vien_hien_tai() OR public.hrm_has_permission(CASE WHEN ma_loai_don=5 THEN 'DUYET_THOI_VIEC' ELSE 'DUYET_DON_NGHI' END));
CREATE POLICY hrm_permission_insert ON public.don_tu AS RESTRICTIVE FOR INSERT TO authenticated
 WITH CHECK(public.hrm_has_permission('NOP_DON_TU'));
CREATE POLICY hrm_permission_update ON public.don_tu AS RESTRICTIVE FOR UPDATE TO authenticated
 USING(public.hrm_has_permission(CASE WHEN ma_nhan_vien=public.nhan_vien_hien_tai() THEN 'NOP_DON_TU' WHEN ma_loai_don=5 THEN 'DUYET_THOI_VIEC' ELSE 'DUYET_DON_NGHI' END))
 WITH CHECK(public.hrm_has_permission(CASE WHEN ma_nhan_vien=public.nhan_vien_hien_tai() THEN 'NOP_DON_TU' WHEN ma_loai_don=5 THEN 'DUYET_THOI_VIEC' ELSE 'DUYET_DON_NGHI' END));

-- Definer RPCs bypass RLS: enforce the live permission before entering their bodies.
DO $$ DECLARE f record; definition text; BEGIN
 FOR f IN SELECT oid FROM pg_proc WHERE pronamespace='public'::regnamespace
  AND proname IN ('hrm_shift_swap','hrm_attendance_explanation') LOOP
  definition:=pg_get_functiondef(f.oid);
  IF definition !~* '\mBEGIN\M' THEN RAISE EXCEPTION 'Unsupported RPC definition'; END IF;
  definition:=regexp_replace(definition,'\mBEGIN\M',
   E'BEGIN\n IF NOT public.hrm_has_permission(''NOP_DON_TU'') THEN RAISE EXCEPTION ''Bạn không có quyền sử dụng đơn từ.'' USING ERRCODE=''42501''; END IF;', 'i');
  EXECUTE definition;
 END LOOP;
END $$;
NOTIFY pgrst,'reload schema';
COMMIT;
