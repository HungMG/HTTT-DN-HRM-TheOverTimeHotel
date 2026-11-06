-- Read only the signed-in employee's event assignments linked to their own shifts.
BEGIN;
DO $$ BEGIN
 IF has_schema_privilege('authenticated','public','CREATE') OR has_schema_privilege('anon','public','CREATE') THEN
  RAISE EXCEPTION 'Cần kiểm kê quyền CREATE trong schema public trước triển khai.';
 END IF;
END $$;
CREATE FUNCTION public.hrm_my_schedule_events(p_from date, p_to date)
RETURNS TABLE(ma_phan_ca integer, ten_du_an text, bat_dau timestamptz, ket_thuc timestamptz)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public AS $$
DECLARE me integer;
BEGIN
 IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ.' USING ERRCODE='42501'; END IF;
 me := public.nhan_vien_hien_tai();
 IF me IS NULL OR NOT EXISTS (SELECT 1 FROM public.tai_khoan t WHERE t.ma_nhan_vien=me AND t.trang_thai=true) THEN
  RAISE EXCEPTION 'Tài khoản không hoạt động.' USING ERRCODE='42501';
 END IF;
 IF p_from IS NULL OR p_to IS NULL OR p_to < p_from OR p_to - p_from > 62 THEN
  RAISE EXCEPTION 'Khoảng ngày không hợp lệ.' USING ERRCODE='22023';
 END IF;
 RETURN QUERY
 SELECT pc.ma_phan_ca, d.ten_du_an::text, a.bat_dau, a.ket_thuc
 FROM public.phan_cong_du_an a
 JOIN public.phan_ca pc ON pc.ma_phan_ca=a.ma_phan_ca AND pc.ma_nhan_vien=me
 JOIN public.du_an d ON d.ma_du_an=a.ma_du_an
 WHERE a.ma_nhan_vien=me AND a.trang_thai_tham_gia <> 'HUY_PHAN_CONG'
   AND pc.ngay_lam_viec BETWEEN p_from AND p_to
 ORDER BY pc.ngay_lam_viec, a.bat_dau, a.ma_phan_cong;
END $$;
REVOKE ALL ON FUNCTION public.hrm_my_schedule_events(date,date) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_my_schedule_events(date,date) TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;
