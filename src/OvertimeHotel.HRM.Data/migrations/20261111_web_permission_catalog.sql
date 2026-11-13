-- Web permission catalog. Existing role grants are not changed.
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
INSERT INTO public.quyen (ma_quyen_code, ten_quyen, mo_ta) VALUES
 ('XEM_LICH_CA', 'Xem lịch làm việc', 'Xem lịch làm việc trong phạm vi được phép.'),
 ('XEM_YEU_CAU_DOI_CA', 'Xem yêu cầu đổi ca', 'Xem yêu cầu đổi ca trong phạm vi được phép.'),
 ('DUYET_DOI_CA', 'Duyệt yêu cầu đổi ca', 'Duyệt hoặc từ chối yêu cầu đổi ca.'),
 ('XEM_GIAI_TRINH_CHAM_CONG', 'Xem giải trình chấm công', 'Xem giải trình trong phạm vi được phép.'),
 ('XAC_MINH_GIAI_TRINH', 'Xử lý giải trình chấm công', 'Xử lý giải trình theo bước của quy trình.'),
 ('XEM_DON_NGHI', 'Xem đơn nghỉ', 'Xem đơn nghỉ trong phạm vi được phép.'),
 ('XEM_BANG_LUONG', 'Xem bảng lương', 'Xem bảng lương trong phạm vi được phép.'),
 ('REPORT_EXPORT', 'Xuất báo cáo', 'Xuất hoặc in báo cáo trong phạm vi được phép.')
ON CONFLICT (ma_quyen_code) DO NOTHING;

-- Role grants are managed in the permission screen.

-- Current permission checks are installed by the mobile alignment migration.
NOTIFY pgrst, 'reload schema';
COMMIT;
