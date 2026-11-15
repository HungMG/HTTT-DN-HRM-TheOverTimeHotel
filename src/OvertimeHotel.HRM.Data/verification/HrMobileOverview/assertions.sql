-- Run inside the migration rehearsal transaction; does not alter user data.
DO $$
DECLARE
 claims jsonb; result jsonb; source_count bigint; role_name text;
BEGIN
 FOR role_name IN SELECT unnest(ARRAY['HR','Admin','Employee','Manager','IT']) LOOP
  SELECT jsonb_build_object('sub',u.id,'session_id',s.id,'iat',extract(epoch FROM now())::bigint,
   'app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro)) INTO claims
  FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro) JOIN public.nhan_vien n USING(ma_nhan_vien)
  JOIN auth.users u ON lower(u.email)=lower(n.email)
  JOIN auth.sessions s ON s.user_id=u.id AND s.created_at>t.phien_hop_le_sau
  WHERE v.ten_vai_tro=role_name AND t.trang_thai ORDER BY s.created_at DESC LIMIT 1;
  IF claims IS NULL THEN RAISE EXCEPTION 'Missing test session for %',role_name; END IF;
  PERFORM set_config('request.jwt.claims',claims::text,true);
  IF role_name IN ('HR','Admin') THEN
   IF role_name='Admin' AND NOT public.hrm_has_permission('REPORT_VIEW') THEN CONTINUE; END IF;
   result := public.hrm_hr_overview('statistics');
   SELECT count(*) INTO source_count FROM public.nhan_vien n
    WHERE n.is_profile_complete AND n.ngay_vao_lam<=(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AND n.trang_thai<>'CHO_HOP_DONG'
     AND (n.trang_thai NOT IN ('THOI_VIEC','DA_THOI_VIEC','NGHI_VIEC') OR n.ngay_thoi_viec>(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date);
   IF (result->>'totalEmployees')::bigint<>source_count THEN RAISE EXCEPTION 'Employee total mismatch'; END IF;
   IF (SELECT sum((x->>'count')::bigint) FROM jsonb_array_elements(result->'departments') x)<>source_count THEN RAISE EXCEPTION 'Department count mismatch'; END IF;
   IF coalesce((result->>'payrollGross')::numeric,0)<>coalesce((result->>'payrollApproved')::numeric,0)+coalesce((result->>'payrollDraft')::numeric,0) THEN RAISE EXCEPTION 'Payroll mismatch'; END IF;
   IF public.hrm_has_permission('QL_NHAN_SU') THEN
    result := public.hrm_hr_overview('dashboard');
    IF jsonb_array_length(result->'staff')<>source_count THEN RAISE EXCEPTION 'Dashboard directory mismatch'; END IF;
   END IF;
  ELSE
   BEGIN
    PERFORM public.hrm_hr_overview('statistics');
    RAISE EXCEPTION 'Role % was allowed unexpectedly',role_name;
   EXCEPTION WHEN insufficient_privilege THEN NULL;
   END;
  END IF;
 END LOOP;
 PERFORM set_config('request.jwt.claims','{}',true);
 BEGIN PERFORM public.hrm_hr_overview('statistics'); RAISE EXCEPTION 'Anonymous request was allowed';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT 'PASS: HR/Admin aggregates match source; Employee/Manager/IT and anonymous denied' result;
