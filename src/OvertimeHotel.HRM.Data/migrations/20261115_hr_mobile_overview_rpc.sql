-- Read-only HR dashboard and statistics. No broadening of direct table RLS.
BEGIN;
CREATE OR REPLACE FUNCTION public.hrm_hr_overview(p_view text DEFAULT 'statistics')
RETURNS jsonb LANGUAGE plpgsql STABLE SECURITY DEFINER
SET search_path = pg_catalog, public AS $$
DECLARE
 v_today date := (current_timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
 v_from date := date_trunc('month', current_timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
 v_role text;
 v_result jsonb;
BEGIN
 IF NOT public.hrm_account_session_valid() THEN
  RAISE EXCEPTION 'Phiên đăng nhập không còn hiệu lực.' USING ERRCODE='42501';
 END IF;
 SELECT v.ten_vai_tro INTO v_role FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai;
 IF v_role NOT IN ('HR','Admin') OR v_role IS NULL THEN
  RAISE EXCEPTION 'Chỉ nhân sự và quản trị viên được xem tổng hợp toàn khách sạn.' USING ERRCODE='42501';
 END IF;
 IF p_view NOT IN ('dashboard','statistics') OR p_view IS NULL THEN
  RAISE EXCEPTION 'Loại thống kê không hợp lệ.' USING ERRCODE='22023';
 END IF;
 IF NOT public.hrm_has_permission(CASE WHEN p_view='dashboard' THEN 'QL_NHAN_SU' ELSE 'REPORT_VIEW' END) THEN
  RAISE EXCEPTION 'Bạn không có quyền xem thống kê này.' USING ERRCODE='42501';
 END IF;
 IF p_view='statistics' THEN
  WITH planned AS (
   SELECT p.*,
    CASE WHEN isfinite(p.bat_dau_du_kien) THEN p.bat_dau_du_kien ELSE (p.ngay_lam_viec+cl.gio_bat_dau) AT TIME ZONE 'Asia/Ho_Chi_Minh' END starts_at,
    CASE WHEN isfinite(p.ket_thuc_du_kien) THEN p.ket_thuc_du_kien ELSE ((p.ngay_lam_viec+CASE WHEN cl.qua_dem OR cl.gio_ket_thuc<=cl.gio_bat_dau THEN 1 ELSE 0 END)+cl.gio_ket_thuc) AT TIME ZONE 'Asia/Ho_Chi_Minh' END ends_at
   FROM public.phan_ca p JOIN public.ca_lam_viec cl USING(ma_ca)
   WHERE p.ngay_lam_viec BETWEEN v_from-1 AND v_today
  ), employees AS (
   SELECT n.*, coalesce(pb.ten_phong_ban,'Chưa xếp phòng ban') department,
    CASE WHEN n.trinh_do ILIKE ANY(ARRAY['%tiến sĩ%','%thạc sĩ%','%sau đại học%']) THEN 'Sau đại học'
     WHEN n.trinh_do ILIKE ANY(ARRAY['%đại học%','%kỹ sư%']) THEN 'Đại học' WHEN n.trinh_do ILIKE '%cao đẳng%' THEN 'Cao đẳng'
     WHEN n.trinh_do ILIKE '%trung cấp%' THEN 'Trung cấp' WHEN n.trinh_do ILIKE '%chứng chỉ%' THEN 'Chứng chỉ nghề' WHEN n.trinh_do ILIKE ANY(ARRAY['%phổ thông%','%THPT%']) THEN 'Phổ thông' ELSE 'Khác' END education,
    CASE WHEN n.ngay_vao_lam > v_today - interval '1 year' THEN 'Dưới 1 năm'
     WHEN n.ngay_vao_lam > v_today - interval '3 years' THEN 'Từ 1 - 3 năm' ELSE 'Trên 3 năm' END tenure
   FROM public.nhan_vien n LEFT JOIN public.phong_ban pb USING(ma_phong_ban)
   WHERE n.is_profile_complete AND n.ngay_vao_lam<=v_today AND n.trang_thai<>'CHO_HOP_DONG'
    AND (n.trang_thai NOT IN ('THOI_VIEC','DA_THOI_VIEC','NGHI_VIEC') OR n.ngay_thoi_viec>v_today)
  ), assessed AS (
   SELECT CASE WHEN c.gio_check_in IS NULL AND c.gio_check_out IS NULL THEN
     CASE WHEN EXISTS(SELECT 1 FROM public.don_tu d JOIN public.loai_don l USING(ma_loai_don)
       WHERE d.ma_nhan_vien=p.ma_nhan_vien AND d.trang_thai='DA_DUYET' AND l.la_nghi_phep
        AND p.ngay_lam_viec BETWEEN d.tu_ngay AND d.den_ngay) THEN 'NGHI_DA_DUYET' ELSE 'VANG_MAT' END
    WHEN c.gio_check_in IS NULL OR c.gio_check_out IS NULL THEN 'THIEU_QUET'
    WHEN p.trang_thai='VANG_MAT' THEN 'VANG_MAT' ELSE c.trang_thai END status
   FROM planned p LEFT JOIN public.cham_cong c USING(ma_phan_ca)
   WHERE p.da_cong_bo AND p.trang_thai<>'DA_HUY' AND p.ngay_lam_viec BETWEEN v_from AND v_today
    AND p.ends_at<=current_timestamp
  ), payroll AS (
   SELECT p.trang_thai, p.tien_luong_cong+p.tien_tang_ca+p.tong_phu_cap+p.tong_thuong gross
   FROM public.phieu_luong p JOIN public.ky_luong k USING(ma_ky_luong)
   WHERE k.nam=extract(year FROM v_today) AND k.thang=extract(month FROM v_today)
    AND p.trang_thai IN ('NHAP','CHO_DUYET','DA_DUYET','DA_THANH_TOAN')
  )
  SELECT jsonb_build_object(
   'period',to_char(v_today,'"Tháng "MM/YYYY'), 'totalEmployees',(SELECT count(*) FROM employees),
   'departments',coalesce((SELECT jsonb_agg(x ORDER BY x.name) FROM (SELECT department name,count(*) count,
    round(100.0*count(*)/nullif((SELECT count(*) FROM employees),0),1) rate FROM employees GROUP BY department) x),'[]'::jsonb),
   'education',coalesce((SELECT jsonb_agg(x ORDER BY x.name) FROM (SELECT education name,count(*) count,
    round(100.0*count(*)/nullif((SELECT count(*) FROM employees),0),1) rate FROM employees GROUP BY education) x),'[]'::jsonb),
   'tenure',coalesce((SELECT jsonb_agg(x ORDER BY x.name) FROM (SELECT tenure name,count(*) count,
    round(100.0*count(*)/nullif((SELECT count(*) FROM employees),0),1) rate FROM employees GROUP BY tenure) x),'[]'::jsonb),
   'assessedShifts',(SELECT count(*) FROM assessed WHERE status NOT IN ('NGHI_DA_DUYET','THIEU_QUET')),
   'punctualRate',(SELECT coalesce(round(100.0*count(*) FILTER(WHERE status='DUNG_GIO')/
    nullif(count(*) FILTER(WHERE status NOT IN ('NGHI_DA_DUYET','THIEU_QUET')),0),1),0) FROM assessed),
   'violationCount',(SELECT count(*) FROM assessed WHERE status IN ('DI_TRE','VE_SOM','TRE_VA_SOM')),
   'leaveCount',(SELECT count(*) FROM assessed WHERE status='NGHI_DA_DUYET'),
   'missingPunchCount',(SELECT count(*) FROM assessed WHERE status='THIEU_QUET'),
   'payrollGross',CASE WHEN public.hrm_has_permission('XEM_BANG_LUONG') THEN (SELECT coalesce(sum(gross),0) FROM payroll) END,
   'payrollApproved',CASE WHEN public.hrm_has_permission('XEM_BANG_LUONG') THEN (SELECT coalesce(sum(gross),0) FROM payroll WHERE trang_thai IN ('DA_DUYET','DA_THANH_TOAN')) END,
   'payrollDraft',CASE WHEN public.hrm_has_permission('XEM_BANG_LUONG') THEN (SELECT coalesce(sum(gross),0) FROM payroll WHERE trang_thai IN ('NHAP','CHO_DUYET')) END
  ) INTO v_result;
 ELSE
  WITH planned AS (
   SELECT p.*,
    CASE WHEN isfinite(p.bat_dau_du_kien) THEN p.bat_dau_du_kien ELSE (p.ngay_lam_viec+cl.gio_bat_dau) AT TIME ZONE 'Asia/Ho_Chi_Minh' END starts_at,
    CASE WHEN isfinite(p.ket_thuc_du_kien) THEN p.ket_thuc_du_kien ELSE ((p.ngay_lam_viec+CASE WHEN cl.qua_dem OR cl.gio_ket_thuc<=cl.gio_bat_dau THEN 1 ELSE 0 END)+cl.gio_ket_thuc) AT TIME ZONE 'Asia/Ho_Chi_Minh' END ends_at
   FROM public.phan_ca p JOIN public.ca_lam_viec cl USING(ma_ca)
   WHERE p.ngay_lam_viec BETWEEN v_from-1 AND v_today
  ), employees AS (
   SELECT n.*, coalesce(pb.ten_phong_ban,'Chưa xếp phòng ban') department,coalesce(cv.ten_chuc_vu,'Chưa có chức vụ') position
   FROM public.nhan_vien n LEFT JOIN public.phong_ban pb USING(ma_phong_ban) LEFT JOIN public.chuc_vu cv USING(ma_chuc_vu)
   WHERE n.is_profile_complete AND n.ngay_vao_lam<=v_today AND n.trang_thai<>'CHO_HOP_DONG'
    AND (n.trang_thai NOT IN ('THOI_VIEC','DA_THOI_VIEC','NGHI_VIEC') OR n.ngay_thoi_viec>v_today)
  ), shifts AS (
   SELECT p.*, c.gio_check_in,c.gio_check_out,cl.ten_ca,
    CASE WHEN cl.loai_ca='HANH_CHINH' THEN 'office' WHEN cl.qua_dem OR cl.ten_ca ILIKE '%đêm%' THEN 'night'
     WHEN cl.ten_ca ILIKE '%chiều%' THEN 'afternoon' ELSE 'morning' END tone
   FROM planned p JOIN public.ca_lam_viec cl USING(ma_ca) LEFT JOIN public.cham_cong c USING(ma_phan_ca)
   WHERE public.hrm_has_permission('XEM_CHAM_CONG_BO_PHAN') AND p.da_cong_bo AND p.trang_thai<>'DA_HUY'
    AND (p.ngay_lam_viec=v_today OR (p.ngay_lam_viec=v_today-1 AND p.ends_at>current_timestamp))
  ), leaves AS (
   SELECT DISTINCT d.ma_nhan_vien FROM public.don_tu d JOIN public.loai_don l USING(ma_loai_don)
   WHERE public.hrm_has_permission('XEM_DON_NGHI') AND d.trang_thai='DA_DUYET' AND l.la_nghi_phep AND v_today BETWEEN d.tu_ngay AND d.den_ngay
  ), contracts AS (
   SELECT h.* FROM public.hop_dong h JOIN employees n USING(ma_nhan_vien)
   WHERE public.hrm_has_permission('QL_HOP_DONG') AND h.trang_thai='HIEU_LUC' AND h.ngay_bat_dau<=v_today AND h.ngay_ket_thuc BETWEEN v_today AND v_today+30
  ), events AS (
   SELECT '🎂' icon,concat(n.ho,' ',n.ten,' · Sinh nhật hôm nay') title,n.department subtitle FROM employees n
    WHERE to_char(n.ngay_sinh,'MM-DD')=to_char(v_today,'MM-DD')
   UNION ALL SELECT '🆕',concat(n.ho,' ',n.ten,' · Nhận việc hôm nay'),n.department FROM employees n WHERE n.ngay_vao_lam=v_today
   UNION ALL SELECT '📋',concat(n.ho,' ',n.ten,' · Sắp hết thử việc'),'Hết hạn '||to_char(h.ngay_ket_thuc,'DD/MM/YYYY')
    FROM contracts h JOIN employees n USING(ma_nhan_vien) WHERE h.hinh_thuc_hop_dong='THU_VIEC' AND h.ngay_ket_thuc<=v_today+7
  )
  SELECT jsonb_build_object('updatedAt',current_timestamp,
   'staff',coalesce((SELECT jsonb_agg(jsonb_build_object('staffCode',n.ma_nhan_vien::text,'fullName',concat_ws(' ',n.ho,n.ten),
    'department',n.department,'position',n.position,'phone',n.dien_thoai,
    'workStatus',CASE WHEN EXISTS(SELECT 1 FROM shifts p WHERE p.ma_nhan_vien=n.ma_nhan_vien AND p.gio_check_in IS NOT NULL AND p.gio_check_out IS NULL
      AND current_timestamp BETWEEN p.starts_at AND p.ends_at+p.gio_tang_ca_du_kien*interval '1 hour') THEN 'Đang trong ca'
      WHEN EXISTS(SELECT 1 FROM leaves l WHERE l.ma_nhan_vien=n.ma_nhan_vien) THEN 'Nghỉ được duyệt' ELSE 'Chưa xác nhận trong ca' END,
    'workStatusDetail',coalesce((SELECT string_agg(p.ten_ca,' · ') FROM shifts p WHERE p.ma_nhan_vien=n.ma_nhan_vien),'Chưa có lịch hôm nay')) ORDER BY n.ten,n.ma_nhan_vien) FROM employees n),'[]'::jsonb),
   'morning', (SELECT jsonb_build_object('checkedIn',count(*) FILTER(WHERE gio_check_in IS NOT NULL),'scheduled',count(*)) FROM shifts WHERE ngay_lam_viec=v_today AND tone='morning'),
   'afternoon',(SELECT jsonb_build_object('checkedIn',count(*) FILTER(WHERE gio_check_in IS NOT NULL),'scheduled',count(*)) FROM shifts WHERE ngay_lam_viec=v_today AND tone='afternoon'),
   'night',(SELECT jsonb_build_object('checkedIn',count(*) FILTER(WHERE gio_check_in IS NOT NULL),'scheduled',count(*)) FROM shifts WHERE ngay_lam_viec=v_today AND tone='night'),
   'onDutyCount',(SELECT count(DISTINCT ma_nhan_vien) FROM shifts WHERE gio_check_in IS NOT NULL AND gio_check_out IS NULL AND current_timestamp BETWEEN starts_at AND ends_at+gio_tang_ca_du_kien*interval '1 hour'),
   'leaveCount',(SELECT count(*) FROM leaves),
   'absentCount',(SELECT count(*) FROM shifts p WHERE ends_at<=current_timestamp AND gio_check_in IS NULL AND gio_check_out IS NULL AND NOT EXISTS(SELECT 1 FROM leaves l WHERE l.ma_nhan_vien=p.ma_nhan_vien)),
   'pendingCount',(SELECT count(*) FROM public.don_tu WHERE trang_thai='CHO_DUYET' AND ((ma_loai_don BETWEEN 1 AND 4 AND public.hrm_has_permission('XEM_DON_NGHI')) OR (ma_loai_don=5 AND public.hrm_has_permission('DUYET_THOI_VIEC')))),
   'reviewedCount',(SELECT count(*) FROM public.don_tu WHERE trang_thai IN ('DA_DUYET','TU_CHOI') AND ((ma_loai_don BETWEEN 1 AND 4 AND public.hrm_has_permission('XEM_DON_NGHI')) OR (ma_loai_don=5 AND public.hrm_has_permission('DUYET_THOI_VIEC')))),
   'expiringContractCount',(SELECT count(*) FROM contracts),
   'contractDescription',CASE WHEN NOT public.hrm_has_permission('QL_HOP_DONG') THEN 'Bạn không có quyền xem hợp đồng.' ELSE (SELECT count(*)::text||' hợp đồng hết hạn trong 30 ngày tới' FROM contracts) END,
   'events',coalesce((SELECT jsonb_agg(e) FROM events e),'[]'::jsonb)
  ) INTO v_result;
 END IF;
 RETURN v_result;
END $$;
REVOKE ALL ON FUNCTION public.hrm_hr_overview(text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_hr_overview(text) TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;
