-- Apply after 20261117_unify_it_head_position.sql and deploy matching application code.
-- Existing IT heads retain access; subsequent appointments require a direct grant.
BEGIN;
CREATE TABLE public.it_head_grant (
    ma_nhan_vien integer PRIMARY KEY REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE CASCADE,
    ma_phu_luc bigint REFERENCES public.phu_luc_luong(ma_phu_luc) ON DELETE RESTRICT,
    ma_nguoi_cap integer REFERENCES public.tai_khoan(ma_tai_khoan) ON DELETE SET NULL,
    thoi_gian_cap timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE public.it_head_grant ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.it_head_grant FROM PUBLIC, anon, authenticated;

-- The current position event changes whenever a new position amendment takes effect.
CREATE FUNCTION public.hrm_it_head_position_event(p_employee integer) RETURNS bigint
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT p.ma_phu_luc FROM public.phu_luc_luong p JOIN public.hop_dong h USING(ma_hop_dong)
 WHERE h.ma_nhan_vien=p_employee AND h.ngay_bat_dau<=CURRENT_DATE
   AND p.ngay_hieu_luc BETWEEN h.ngay_bat_dau AND COALESCE(h.ngay_ket_thuc, 'infinity'::date)
   AND p.ngay_hieu_luc<=CURRENT_DATE AND p.ma_chuc_vu_moi IS NOT NULL
 ORDER BY p.ngay_hieu_luc DESC,h.ngay_bat_dau DESC,h.ma_hop_dong DESC,p.ma_phu_luc DESC LIMIT 1
$$;
CREATE FUNCTION public.hrm_it_head_access(p_employee integer) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT EXISTS(SELECT 1 FROM public.it_head_grant g JOIN public.nhan_vien n USING(ma_nhan_vien)
  JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE n.ma_nhan_vien=p_employee AND n.trang_thai='DANG_LAM' AND n.ma_chuc_vu=2
   AND t.trang_thai AND v.ten_vai_tro='IT'
   AND g.ma_phu_luc IS NOT DISTINCT FROM public.hrm_it_head_position_event(p_employee))
$$;
REVOKE ALL ON FUNCTION public.hrm_it_head_position_event(integer), public.hrm_it_head_access(integer) FROM PUBLIC,anon,authenticated;
-- Preserve existing access only at rollout. New appointments are not inserted automatically.
INSERT INTO public.it_head_grant(ma_nhan_vien,ma_phu_luc)
 SELECT n.ma_nhan_vien,public.hrm_it_head_position_event(n.ma_nhan_vien)
 FROM public.nhan_vien n JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE n.trang_thai='DANG_LAM' AND n.ma_chuc_vu=2 AND t.trang_thai AND v.ten_vai_tro='IT';
CREATE OR REPLACE FUNCTION public.hrm_is_it_head() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND public.hrm_it_head_access(public.nhan_vien_hien_tai())
$$;
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
     WHEN v.ten_vai_tro='IT' AND p_code IN ('XEM_CHAM_CONG_BO_PHAN','XEP_CA_LAM','XEM_DON_NGHI','DUYET_DON_NGHI','DUYET_THOI_VIEC','REPORT_VIEW') THEN false
     ELSE ((v.ten_vai_tro='IT' AND p_code IN
       ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL','CAU_HINH_VI_TRI_CHAM_CONG'))
      OR (v.ten_vai_tro='Admin' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU'))
      OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
       WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
     END)
$$;

NOTIFY pgrst, 'reload schema';
COMMIT;
