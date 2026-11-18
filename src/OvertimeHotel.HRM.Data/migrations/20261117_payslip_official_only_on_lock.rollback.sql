BEGIN;

CREATE OR REPLACE FUNCTION public.hrm_can_read_own_payslip(p_id integer) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.phieu_luong p JOIN public.ky_luong k USING(ma_ky_luong)
  WHERE p.ma_phieu_luong=p_id AND p.ma_nhan_vien=public.nhan_vien_hien_tai() AND (
   p.trang_thai='DA_THANH_TOAN'
   OR (p.trang_thai='DA_DUYET' AND k.ngay_duyet IS NOT NULL)
   OR (p.trang_thai IN ('NHAP','CHO_DUYET') AND k.ngay_phat_hanh_nhap IS NOT NULL
       AND k.ngay_duyet IS NULL AND coalesce(k.ly_do_tu_choi,'')='')
  )
 )
$$;

REVOKE ALL ON FUNCTION public.hrm_can_read_own_payslip(integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_can_read_own_payslip(integer) TO authenticated;

COMMIT;
