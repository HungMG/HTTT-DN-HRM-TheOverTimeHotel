BEGIN;
DO $$ DECLARE c text; rid integer; BEGIN
 SELECT jsonb_build_object('sub',s.user_id,'session_id',s.id,'iat',extract(epoch FROM clock_timestamp())::bigint,
 'role','authenticated','app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro))::text,t.ma_vai_tro
 INTO c,rid FROM auth.sessions s JOIN auth.users u ON u.id=s.user_id
 JOIN public.nhan_vien nv ON lower(nv.email)=lower(u.email) JOIN public.tai_khoan t USING(ma_nhan_vien)
 JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE v.ten_vai_tro='Admin' AND t.trang_thai AND s.created_at>t.phien_hop_le_sau ORDER BY s.created_at DESC LIMIT 1;
 IF c IS NULL THEN RAISE EXCEPTION 'Need active Admin session'; END IF;
 PERFORM set_config('request.jwt.claims',c,true);
 IF NOT public.hrm_account_session_valid() THEN RAISE EXCEPTION 'Invalid Admin session'; END IF;
 IF NOT public.hrm_has_permission('XEM_BANG_LUONG') THEN RAISE EXCEPTION 'Test Admin missing view grant'; END IF;
 PERFORM public.hrm_admin_payroll_summary(1);
 PERFORM set_config('request.jwt.claims','{}',true);
 DELETE FROM public.vai_tro_quyen vq USING public.quyen q WHERE vq.ma_quyen=q.ma_quyen AND vq.ma_vai_tro=rid AND q.ma_quyen_code='XEM_BANG_LUONG';
 PERFORM set_config('request.jwt.claims',c,true);
 BEGIN
  PERFORM public.hrm_admin_payroll_summary(1);
  RAISE EXCEPTION 'RPC bypassed revoked payroll view';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
ROLLBACK;
