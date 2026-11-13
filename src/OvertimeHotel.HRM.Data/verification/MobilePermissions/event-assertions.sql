-- Run in a transaction and ROLLBACK: exercises the explicit event grant without
-- assuming that any role must or must not already have it.
DO $$
DECLARE claims jsonb; role_id integer;
BEGIN
 SELECT jsonb_build_object('sub',s.user_id,'session_id',s.id,
  'iat',extract(epoch FROM clock_timestamp())::bigint,'role','authenticated',
  'app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro)),t.ma_vai_tro
 INTO claims,role_id FROM auth.sessions s JOIN auth.users u ON u.id=s.user_id
 JOIN public.nhan_vien nv ON lower(nv.email)=lower(u.email)
 JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE v.ten_vai_tro='Manager' AND t.trang_thai AND s.created_at>t.phien_hop_le_sau
 ORDER BY s.created_at DESC LIMIT 1;
 IF claims IS NULL THEN RAISE EXCEPTION 'Test requires an active Manager auth session'; END IF;
 PERFORM set_config('request.jwt.claims','{}',true);
 INSERT INTO public.vai_tro_quyen(ma_vai_tro,ma_quyen)
 SELECT role_id,q.ma_quyen FROM public.quyen q WHERE q.ma_quyen_code='QL_SU_KIEN' ON CONFLICT DO NOTHING;
 PERFORM set_config('request.jwt.claims',claims::text,true);
 IF NOT public.hrm_has_permission('QL_SU_KIEN') THEN RAISE EXCEPTION 'Explicit Manager event grant rejected'; END IF;
 DELETE FROM public.vai_tro_quyen vq USING public.quyen q
 WHERE vq.ma_quyen=q.ma_quyen AND vq.ma_vai_tro=role_id AND q.ma_quyen_code='QL_SU_KIEN';
 IF public.hrm_has_permission('QL_SU_KIEN') THEN RAISE EXCEPTION 'Revoked event grant accepted'; END IF;
END $$;
SELECT 'PASS: explicitly granted and revoked event access';
