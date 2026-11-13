-- Split existing shift-management access without granting new capabilities.
BEGIN;
INSERT INTO public.quyen (ma_quyen_code, ten_quyen, mo_ta) VALUES
 ('XEM_LICH_CA', 'Xem lịch ca', 'Xem lịch làm việc theo phạm vi vai trò.'),
 ('XEM_YEU_CAU_DOI_CA', 'Xem yêu cầu đổi ca', 'Xem yêu cầu đổi ca theo phạm vi vai trò.'),
 ('DUYET_DOI_CA', 'Duyệt đổi ca', 'Duyệt hoặc từ chối yêu cầu đổi ca.'),
 ('XEM_GIAI_TRINH_CHAM_CONG', 'Xem giải trình chấm công', 'Xem giải trình theo phạm vi vai trò.'),
 ('XAC_MINH_GIAI_TRINH', 'Xác minh giải trình chấm công', 'Quản lý xác minh giải trình của bộ phận.'),
 ('XEM_DON_NGHI', 'Xem đơn nghỉ', 'Xem đơn nghỉ theo phạm vi vai trò.'),
 ('REPORT_EXPORT', 'Xuất báo cáo', 'Xuất hoặc in báo cáo theo phạm vi vai trò.')
ON CONFLICT (ma_quyen_code) DO UPDATE SET ten_quyen = EXCLUDED.ten_quyen, mo_ta = EXCLUDED.mo_ta;

-- Preserve the existing behavior for roles already granted shift management.
INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT DISTINCT vq.ma_vai_tro, added.ma_quyen
FROM public.vai_tro_quyen vq
JOIN public.quyen old ON old.ma_quyen = vq.ma_quyen AND old.ma_quyen_code = 'XEP_CA_LAM'
JOIN public.quyen added ON added.ma_quyen_code IN
 ('XEM_LICH_CA', 'XEM_YEU_CAU_DOI_CA', 'DUYET_DOI_CA', 'XEM_GIAI_TRINH_CHAM_CONG', 'XAC_MINH_GIAI_TRINH')
JOIN public.vai_tro v ON v.ma_vai_tro = vq.ma_vai_tro AND v.ten_vai_tro NOT IN ('Admin', 'IT')
ON CONFLICT DO NOTHING;

INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT vq.ma_vai_tro, added.ma_quyen
FROM public.vai_tro_quyen vq
JOIN public.quyen old ON old.ma_quyen = vq.ma_quyen
JOIN public.quyen added ON (old.ma_quyen_code = 'DUYET_DON_NGHI' AND added.ma_quyen_code = 'XEM_DON_NGHI')
  OR (old.ma_quyen_code = 'REPORT_VIEW' AND added.ma_quyen_code = 'REPORT_EXPORT')
JOIN public.vai_tro v ON v.ma_vai_tro = vq.ma_vai_tro AND v.ten_vai_tro NOT IN ('Admin', 'IT')
ON CONFLICT DO NOTHING;
-- Manager is not involved in event workflows.
DELETE FROM public.vai_tro_quyen vq USING public.vai_tro v, public.quyen q
WHERE vq.ma_vai_tro = v.ma_vai_tro AND vq.ma_quyen = q.ma_quyen
  AND v.ten_vai_tro = 'Manager' AND q.ma_quyen_code = 'QL_SU_KIEN';

CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
   AND CASE
    WHEN p_code IN ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL') THEN v.ten_vai_tro='IT'
    WHEN v.ten_vai_tro='IT' THEN false
    WHEN p_code='QL_SU_KIEN' THEN v.ten_vai_tro IN ('HR','Admin') AND EXISTS(
     SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
     WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code)
    WHEN v.ten_vai_tro='Admin' THEN true
    ELSE EXISTS(SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
     WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code)
   END)
$$;
NOTIFY pgrst, 'reload schema';
COMMIT;
