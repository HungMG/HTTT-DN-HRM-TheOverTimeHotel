-- Run after the migration in a transaction; always ROLLBACK test grant changes.
DO $$ DECLARE claims jsonb; role_id integer; code text; BEGIN
 SELECT jsonb_build_object('sub',s.user_id,'session_id',s.id,'iat',extract(epoch FROM clock_timestamp())::bigint,
  'role','authenticated','app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro,
  'quyen',jsonb_build_array('CHAM_CONG_DI_DONG','DUYET_DON_NGHI','XEP_CA_LAM','NOP_DON_TU'))),t.ma_vai_tro
 INTO claims,role_id FROM auth.sessions s JOIN auth.users u ON u.id=s.user_id
 JOIN public.nhan_vien nv ON lower(nv.email)=lower(u.email)
 JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE v.ten_vai_tro='Manager' AND t.trang_thai AND s.created_at>t.phien_hop_le_sau
 ORDER BY s.created_at DESC LIMIT 1;
 IF claims IS NULL THEN RAISE EXCEPTION 'Test requires an active Manager auth session'; END IF;
 PERFORM set_config('request.jwt.claims',claims::text,true);
 IF NOT public.hrm_account_session_valid() THEN RAISE EXCEPTION 'Invalid test session'; END IF;
 FOREACH code IN ARRAY ARRAY['CHAM_CONG_DI_DONG','DUYET_DON_NGHI','XEM_DON_NGHI','XEP_CA_LAM','XEM_CHAM_CONG_BO_PHAN','NOP_DON_TU'] LOOP
  -- Test setup runs as the database owner, not as the simulated Manager.
  PERFORM set_config('request.jwt.claims','{}',true);
  INSERT INTO public.vai_tro_quyen(ma_vai_tro,ma_quyen)
   SELECT role_id,q.ma_quyen FROM public.quyen q WHERE q.ma_quyen_code=code ON CONFLICT DO NOTHING;
  PERFORM set_config('request.jwt.claims',claims::text,true);
  IF NOT public.hrm_has_permission(code) OR NOT public.hrm_my_permissions() ? code THEN RAISE EXCEPTION 'Grant not visible: %',code; END IF;
  DELETE FROM public.vai_tro_quyen vq USING public.quyen q WHERE vq.ma_quyen=q.ma_quyen AND vq.ma_vai_tro=role_id AND q.ma_quyen_code=code;
  -- Same claims, including stale permission metadata: revocation must take effect immediately.
  IF public.hrm_has_permission(code) OR public.hrm_my_permissions() ? code THEN RAISE EXCEPTION 'Stale grant accepted: %',code; END IF;
  PERFORM set_config('request.jwt.claims','{}',true);
  INSERT INTO public.vai_tro_quyen(ma_vai_tro,ma_quyen) SELECT role_id,q.ma_quyen FROM public.quyen q WHERE q.ma_quyen_code=code;
  PERFORM set_config('request.jwt.claims',claims::text,true);
 END LOOP;
 DELETE FROM public.vai_tro_quyen WHERE ma_vai_tro=role_id;
 IF public.hrm_my_permissions()<>'[]'::jsonb THEN RAISE EXCEPTION 'Removed grants still visible'; END IF;
 BEGIN
  PERFORM public.hrm_shift_swap('create','{}'::jsonb);
  RAISE EXCEPTION 'Shift swap bypassed revoked permission';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  PERFORM public.hrm_attendance_explanation('create','{}'::jsonb);
  RAISE EXCEPTION 'Explanation bypassed revoked permission';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  PERFORM public.hrm_shift_swap_partner_options();
  RAISE EXCEPTION 'Partner RPC bypassed revoked permission';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 IF has_function_privilege('anon','public.hrm_my_permissions()','EXECUTE') THEN RAISE EXCEPTION 'Anonymous RPC allowed'; END IF;
END $$;
SET LOCAL ROLE authenticated;
DO $$ DECLARE affected integer; BEGIN
 IF EXISTS(SELECT 1 FROM public.phieu_luong WHERE ma_nhan_vien<>public.nhan_vien_hien_tai() OR NOT public.hrm_can_read_own_payslip(ma_phieu_luong)) THEN RAISE EXCEPTION 'Unpublished or other employee payslip leaked'; END IF;
 IF EXISTS(SELECT 1 FROM public.chi_tiet_phieu_luong c JOIN public.phieu_luong p USING(ma_phieu_luong) WHERE p.ma_nhan_vien<>public.nhan_vien_hien_tai() OR NOT public.hrm_can_read_own_payslip(p.ma_phieu_luong)) THEN RAISE EXCEPTION 'Payslip item leaked'; END IF;
 IF EXISTS(SELECT 1 FROM public.phan_ca WHERE ma_nhan_vien<>public.nhan_vien_hien_tai()) THEN RAISE EXCEPTION 'Department schedule leaked'; END IF;
 IF EXISTS(SELECT 1 FROM public.don_tu WHERE ma_nhan_vien<>public.nhan_vien_hien_tai()) THEN RAISE EXCEPTION 'Review list leaked'; END IF;
 IF EXISTS(SELECT 1 FROM public.cham_cong c JOIN public.phan_ca p USING(ma_phan_ca) WHERE p.ma_nhan_vien<>public.nhan_vien_hien_tai()) THEN RAISE EXCEPTION 'Department attendance leaked'; END IF;
 UPDATE public.cham_cong SET gio_check_in=gio_check_in;
 GET DIAGNOSTICS affected=ROW_COUNT;
 IF affected<>0 THEN RAISE EXCEPTION 'Attendance write bypassed revoked grant'; END IF;
 UPDATE public.don_tu SET trang_thai=trang_thai;
 GET DIAGNOSTICS affected=ROW_COUNT;
 IF affected<>0 THEN RAISE EXCEPTION 'Review write bypassed revoked grant'; END IF;
END $$;
RESET ROLE;
SELECT 'PASS: live grants, stale JWT revocation, RPC guards, anonymous access and RLS filtering';
