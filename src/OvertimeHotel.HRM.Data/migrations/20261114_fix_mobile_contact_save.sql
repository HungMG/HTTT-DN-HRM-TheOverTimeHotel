BEGIN;
CREATE OR REPLACE FUNCTION public.hrm_my_contact(p_action text, p_phone text DEFAULT NULL, p_email text DEFAULT NULL, p_address text DEFAULT NULL)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public AS $$
DECLARE me integer; result jsonb;
BEGIN
 IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ.' USING ERRCODE='42501'; END IF;
 me := public.nhan_vien_hien_tai();
 IF me IS NULL OR NOT EXISTS (SELECT 1 FROM public.tai_khoan t WHERE t.ma_nhan_vien=me AND t.trang_thai=true) THEN
  RAISE EXCEPTION 'Tài khoản không hoạt động.' USING ERRCODE='42501';
 END IF;
 IF p_action = 'update' THEN
  p_phone := btrim(coalesce(p_phone,'')); p_email := btrim(coalesce(p_email,'')); p_address := btrim(coalesce(p_address,''));
  IF p_phone !~ '^(0|\+84)[35789][0-9]{8}$' THEN RAISE EXCEPTION 'Số điện thoại phải có dạng 0912345678 hoặc +84912345678.'; END IF;
  IF length(p_email)=0 OR length(p_email)>100 OR p_email !~ '^[^@[:space:]]+@[^@[:space:]]+$' THEN RAISE EXCEPTION 'Email cá nhân không hợp lệ.'; END IF;
  IF length(p_address)=0 OR length(p_address)>255 THEN RAISE EXCEPTION 'Địa chỉ bắt buộc và không được quá 255 ký tự.'; END IF;
  UPDATE public.nhan_vien SET dien_thoai=p_phone, email_ca_nhan=p_email, dia_chi=p_address WHERE ma_nhan_vien=me;
  -- Execute the deferred salary check before leaving this restricted definer function.
  SET CONSTRAINTS salary_check_nhan_vien IMMEDIATE;
  SET CONSTRAINTS salary_check_nhan_vien DEFERRED;
 ELSIF p_action <> 'get' OR p_action IS NULL THEN RAISE EXCEPTION 'Thao tác không hợp lệ.';
 END IF;
 SELECT jsonb_build_object('Phone',n.dien_thoai,'PersonalEmail',n.email_ca_nhan,'Address',n.dia_chi) INTO result FROM public.nhan_vien n WHERE n.ma_nhan_vien=me;
 IF result IS NULL THEN RAISE EXCEPTION 'Không tìm thấy hồ sơ.'; END IF;
 RETURN result;
END $$;
REVOKE ALL ON FUNCTION public.hrm_my_contact(text,text,text,text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_my_contact(text,text,text,text) TO authenticated;
COMMIT;
