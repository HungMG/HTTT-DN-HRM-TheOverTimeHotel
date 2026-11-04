BEGIN;
DO $$ BEGIN
 IF has_schema_privilege('authenticated','public','CREATE') OR has_schema_privilege('anon','public','CREATE') THEN
  RAISE EXCEPTION 'Cần kiểm kê quyền CREATE trong schema public trước triển khai.';
 END IF;
END $$;
CREATE FUNCTION public.hrm_my_contact(p_action text, p_phone text DEFAULT NULL, p_email text DEFAULT NULL, p_address text DEFAULT NULL)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public AS $$
DECLARE me integer; result jsonb;
BEGIN
 IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ.' USING ERRCODE='42501'; END IF;
 me := public.nhan_vien_hien_tai();
 IF me IS NULL OR NOT EXISTS (SELECT 1 FROM public.tai_khoan t WHERE t.ma_nhan_vien=me AND t.trang_thai=true) THEN
  RAISE EXCEPTION 'Tài khoản không hoạt động.' USING ERRCODE='42501';
 END IF;
 IF p_action = 'update' THEN
  p_phone := btrim(coalesce(p_phone,'')); p_email := nullif(btrim(coalesce(p_email,'')), ''); p_address := nullif(btrim(coalesce(p_address,'')), '');
  IF length(p_phone) NOT BETWEEN 1 AND 20 OR p_phone !~ '^[0-9+ ()-]+$' OR p_phone !~ '[0-9]' THEN RAISE EXCEPTION 'Số điện thoại không hợp lệ.'; END IF;
  IF p_email IS NOT NULL AND (length(p_email)>100 OR p_email !~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$') THEN RAISE EXCEPTION 'Email cá nhân không hợp lệ.'; END IF;
  IF length(p_address)>255 THEN RAISE EXCEPTION 'Địa chỉ quá dài.'; END IF;
  UPDATE public.nhan_vien SET dien_thoai=p_phone, email_ca_nhan=p_email, dia_chi=p_address WHERE ma_nhan_vien=me;
 ELSIF p_action <> 'get' OR p_action IS NULL THEN RAISE EXCEPTION 'Thao tác không hợp lệ.';
 END IF;
 SELECT jsonb_build_object('Phone',n.dien_thoai,'PersonalEmail',n.email_ca_nhan,'Address',n.dia_chi) INTO result FROM public.nhan_vien n WHERE n.ma_nhan_vien=me;
 IF result IS NULL THEN RAISE EXCEPTION 'Không tìm thấy hồ sơ.'; END IF;
 RETURN result;
END $$;
REVOKE ALL ON FUNCTION public.hrm_my_contact(text,text,text,text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_my_contact(text,text,text,text) TO authenticated;
COMMIT;
