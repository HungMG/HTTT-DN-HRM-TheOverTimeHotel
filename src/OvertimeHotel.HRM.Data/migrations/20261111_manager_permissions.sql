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

-- Role grants are managed in the permission screen.

-- Current permission checks are installed by the mobile alignment migration.
NOTIFY pgrst, 'reload schema';
COMMIT;
