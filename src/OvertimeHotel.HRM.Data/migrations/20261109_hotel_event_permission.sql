-- Add the event workflow permission. Only HR is selected initially.
BEGIN;
INSERT INTO public.quyen(ma_quyen_code,ten_quyen,mo_ta)
VALUES ('QL_SU_KIEN','Quản lý sự kiện khách sạn','Truy cập luồng sự kiện: lập kế hoạch, phân công, theo dõi và nghiệm thu theo phạm vi vai trò.')
ON CONFLICT(ma_quyen_code) DO UPDATE SET ten_quyen=excluded.ten_quyen,mo_ta=excluded.mo_ta;
DELETE FROM public.vai_tro_quyen vq USING public.quyen q,public.vai_tro v
WHERE vq.ma_quyen=q.ma_quyen AND vq.ma_vai_tro=v.ma_vai_tro
 AND q.ma_quyen_code='QL_SU_KIEN' AND v.ten_vai_tro<>'HR';
INSERT INTO public.vai_tro_quyen(ma_vai_tro,ma_quyen)
SELECT v.ma_vai_tro,q.ma_quyen FROM public.vai_tro v CROSS JOIN public.quyen q
WHERE v.ten_vai_tro='HR' AND q.ma_quyen_code='QL_SU_KIEN'
ON CONFLICT DO NOTHING;

-- Keep mobile effective permissions consistent with WebAdmin's explicit event grant.
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
NOTIFY pgrst,'reload schema';
COMMIT;
