-- Run inside a transaction and roll back the permission changes afterwards.
DO $$
DECLARE claims jsonb; employee_role integer; permission_code text;
BEGIN
 SELECT jsonb_build_object('sub',s.user_id,'session_id',s.id,'iat',extract(epoch FROM clock_timestamp())::bigint,
  'role','authenticated','app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro))
 INTO claims FROM auth.sessions s JOIN auth.users u ON u.id=s.user_id
 JOIN public.nhan_vien nv ON lower(nv.email)=lower(u.email)
 JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE v.ten_vai_tro='IT' AND t.trang_thai AND s.created_at>t.phien_hop_le_sau
 ORDER BY s.created_at DESC LIMIT 1;
 IF claims IS NULL THEN RAISE EXCEPTION 'Test requires an active IT session'; END IF;
 SELECT ma_vai_tro INTO STRICT employee_role FROM public.vai_tro WHERE ten_vai_tro='Employee';
 FOREACH permission_code IN ARRAY ARRAY['CHAM_CONG_DI_DONG','NOP_DON_TU'] LOOP
  PERFORM set_config('request.jwt.claims','{}',true);
  INSERT INTO public.vai_tro_quyen(ma_vai_tro,ma_quyen)
   SELECT employee_role,q.ma_quyen FROM public.quyen q WHERE q.ma_quyen_code=permission_code ON CONFLICT DO NOTHING;
  PERFORM set_config('request.jwt.claims',claims::text,true);
  IF NOT public.hrm_has_permission(permission_code) OR NOT public.hrm_my_permissions() ? permission_code THEN
   RAISE EXCEPTION 'IT did not inherit enabled permission %',permission_code; END IF;
  PERFORM set_config('request.jwt.claims','{}',true);
  DELETE FROM public.vai_tro_quyen vq USING public.quyen q
   WHERE vq.ma_quyen=q.ma_quyen AND vq.ma_vai_tro=employee_role AND q.ma_quyen_code=permission_code;
  PERFORM set_config('request.jwt.claims',claims::text,true);
  IF public.hrm_has_permission(permission_code) OR public.hrm_my_permissions() ? permission_code THEN
   RAISE EXCEPTION 'IT retained disabled permission %',permission_code; END IF;
 END LOOP;
 IF NOT public.hrm_has_permission('QL_PHAN_QUYEN') THEN RAISE EXCEPTION 'Fixed IT right lost'; END IF;
END $$;
