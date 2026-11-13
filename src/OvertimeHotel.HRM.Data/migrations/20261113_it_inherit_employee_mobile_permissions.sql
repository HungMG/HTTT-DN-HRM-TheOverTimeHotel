-- IT personal Mobile permissions follow Employee grants; fixed IT rights are unchanged.
BEGIN;
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
NOTIFY pgrst, 'reload schema';
COMMIT;
