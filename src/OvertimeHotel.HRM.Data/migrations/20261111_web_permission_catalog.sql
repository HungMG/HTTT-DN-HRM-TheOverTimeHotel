-- Web permission catalog. Run after reviewing the existing role grants.
-- This migration never deletes role grants or applies the suggested defaults.
BEGIN;
-- Replace hard-coded target-role restrictions while retaining a permission
-- check for authenticated database callers. Existing RLS has no permissive
-- write policy for authenticated clients; the Web uses its database connection.
CREATE OR REPLACE FUNCTION public.chan_cap_quyen_tai_khoan_di_dong() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF auth.uid() IS NOT NULL AND NOT public.hrm_has_permission('QL_PHAN_QUYEN') THEN
  RAISE EXCEPTION 'Bạn không có quyền quản lý phân quyền.' USING ERRCODE='42501';
 END IF;
 RETURN NEW;
END
$$;
CREATE TEMP TABLE hrm_new_web_permissions (ma_quyen_code text PRIMARY KEY) ON COMMIT DROP;
WITH created AS (
INSERT INTO public.quyen (ma_quyen_code, ten_quyen, mo_ta) VALUES
 ('XEM_LICH_CA', 'Xem lịch làm việc', 'Xem lịch làm việc trong phạm vi được phép.'),
 ('XEM_YEU_CAU_DOI_CA', 'Xem yêu cầu đổi ca', 'Xem yêu cầu đổi ca trong phạm vi được phép.'),
 ('DUYET_DOI_CA', 'Duyệt yêu cầu đổi ca', 'Duyệt hoặc từ chối yêu cầu đổi ca.'),
 ('XEM_GIAI_TRINH_CHAM_CONG', 'Xem giải trình chấm công', 'Xem giải trình trong phạm vi được phép.'),
 ('XAC_MINH_GIAI_TRINH', 'Xử lý giải trình chấm công', 'Xử lý giải trình theo bước của quy trình.'),
 ('XEM_DON_NGHI', 'Xem đơn nghỉ', 'Xem đơn nghỉ trong phạm vi được phép.'),
 ('XEM_BANG_LUONG', 'Xem bảng lương', 'Xem bảng lương trong phạm vi được phép.'),
 ('REPORT_EXPORT', 'Xuất báo cáo', 'Xuất hoặc in báo cáo trong phạm vi được phép.')
ON CONFLICT (ma_quyen_code) DO NOTHING
RETURNING ma_quyen_code
)
INSERT INTO hrm_new_web_permissions SELECT ma_quyen_code FROM created;

-- The four fixed IT capabilities must also exist in the database because
-- account-provisioning triggers validate the stored grants.
INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT v.ma_vai_tro, q.ma_quyen FROM public.vai_tro v CROSS JOIN public.quyen q
WHERE v.ten_vai_tro = 'IT' AND q.ma_quyen_code IN
 ('QL_TAI_KHOAN','CAP_TAI_KHOAN_MOBILE','QL_PHAN_QUYEN','SAO_LUU_CSDL')
ON CONFLICT DO NOTHING;

-- Only map a directly dependent view permission from an existing action grant.
-- Do not infer swap approval, explanation handling, reporting or event grants.
INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT DISTINCT existing.ma_vai_tro, view_permission.ma_quyen
FROM public.vai_tro_quyen existing
JOIN public.quyen action_permission ON action_permission.ma_quyen = existing.ma_quyen
JOIN public.quyen view_permission ON (action_permission.ma_quyen_code, view_permission.ma_quyen_code) IN (
 ('XEP_CA_LAM', 'XEM_LICH_CA'),
 ('DUYET_DOI_CA', 'XEM_YEU_CAU_DOI_CA'),
 ('XAC_MINH_GIAI_TRINH', 'XEM_GIAI_TRINH_CHAM_CONG'),
 ('DUYET_DON_NGHI', 'XEM_DON_NGHI'),
 ('TINH_LUONG', 'XEM_BANG_LUONG'))
JOIN hrm_new_web_permissions newly_created ON newly_created.ma_quyen_code = view_permission.ma_quyen_code
ON CONFLICT DO NOTHING;

-- Keep direct Supabase calls consistent with the current grants and fixed IT permissions.
CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND ((v.ten_vai_tro='IT' AND p_code IN
      ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL'))
     -- Keep the two personal Mobile capabilities previously inherited by Admin.
     OR (v.ten_vai_tro='Admin' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU'))
     OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
      WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
 )
$$;

NOTIFY pgrst, 'reload schema';
COMMIT;
