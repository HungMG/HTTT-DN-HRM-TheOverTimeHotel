-- Optional bootstrap for a new installation, after creating roles and running
-- 20261111_web_permission_catalog.sql, BEFORE creating any user accounts.
-- Never apply this file to an existing installation: it resets the web grants.
BEGIN;
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM public.tai_khoan) THEN
  RAISE EXCEPTION 'Chỉ được khởi tạo quyền mặc định trước khi tạo tài khoản.';
 END IF;
 IF EXISTS (
  SELECT 1 FROM (VALUES
   ('QL_NHAN_SU'),('QL_HOP_DONG'),('QL_DANH_MUC'),('XEM_LICH_CA'),('XEP_CA_LAM'),
   ('XEM_YEU_CAU_DOI_CA'),('DUYET_DOI_CA'),('XEM_CHAM_CONG_BO_PHAN'),
   ('XEM_GIAI_TRINH_CHAM_CONG'),('XAC_MINH_GIAI_TRINH'),('XEM_DON_NGHI'),
   ('DUYET_DON_NGHI'),('DUYET_THOI_VIEC'),('QL_SU_KIEN'),('XEM_BANG_LUONG'),
   ('TINH_LUONG'),('DUYET_BANG_LUONG'),('REPORT_VIEW'),('REPORT_EXPORT'),('QL_TAI_KHOAN'),
   ('CAP_TAI_KHOAN_MOBILE'),('QL_PHAN_QUYEN'),('SAO_LUU_CSDL')
  ) required(code) WHERE NOT EXISTS (SELECT 1 FROM public.quyen q WHERE q.ma_quyen_code=required.code)
 ) THEN RAISE EXCEPTION 'Danh mục quyền chưa đầy đủ; hãy chạy migration phân quyền trước.'; END IF;
END $$;

DELETE FROM public.vai_tro_quyen vq USING public.vai_tro v, public.quyen q
WHERE vq.ma_vai_tro=v.ma_vai_tro AND vq.ma_quyen=q.ma_quyen
 AND v.ten_vai_tro IN ('Admin','HR','Manager','IT','Employee')
 AND q.ma_quyen_code IN ('QL_NHAN_SU','QL_HOP_DONG','QL_DANH_MUC','XEM_LICH_CA','XEP_CA_LAM',
  'XEM_YEU_CAU_DOI_CA','DUYET_DOI_CA','XEM_CHAM_CONG_BO_PHAN',
  'XEM_GIAI_TRINH_CHAM_CONG','XAC_MINH_GIAI_TRINH','XEM_DON_NGHI','DUYET_DON_NGHI',
  'DUYET_THOI_VIEC','QL_SU_KIEN','XEM_BANG_LUONG','TINH_LUONG','DUYET_BANG_LUONG','REPORT_VIEW','REPORT_EXPORT',
  'QL_TAI_KHOAN','CAP_TAI_KHOAN_MOBILE','QL_PHAN_QUYEN','SAO_LUU_CSDL');

INSERT INTO public.vai_tro_quyen (ma_vai_tro,ma_quyen)
SELECT v.ma_vai_tro,q.ma_quyen FROM (VALUES
 ('Admin','XEM_BANG_LUONG'),('Admin','DUYET_BANG_LUONG'),('Admin','REPORT_VIEW'),('Admin','REPORT_EXPORT'),
 ('HR','QL_NHAN_SU'),('HR','QL_HOP_DONG'),('HR','QL_DANH_MUC'),
 ('HR','XEM_LICH_CA'),('HR','XEP_CA_LAM'),('HR','XEM_YEU_CAU_DOI_CA'),('HR','DUYET_DOI_CA'),
 ('HR','XEM_CHAM_CONG_BO_PHAN'),('HR','XEM_GIAI_TRINH_CHAM_CONG'),('HR','XAC_MINH_GIAI_TRINH'),
 ('HR','XEM_DON_NGHI'),('HR','DUYET_DON_NGHI'),('HR','DUYET_THOI_VIEC'),('HR','QL_SU_KIEN'),
 ('HR','XEM_BANG_LUONG'),('HR','TINH_LUONG'),
 ('Manager','XEM_LICH_CA'),('Manager','XEP_CA_LAM'),('Manager','XEM_YEU_CAU_DOI_CA'),
 ('Manager','DUYET_DOI_CA'),('Manager','XEM_CHAM_CONG_BO_PHAN'),('Manager','XEM_GIAI_TRINH_CHAM_CONG'),
 ('Manager','XAC_MINH_GIAI_TRINH'),('Manager','XEM_DON_NGHI'),('Manager','DUYET_DON_NGHI'),
 ('Manager','XEM_BANG_LUONG'),
 ('IT','QL_TAI_KHOAN'),('IT','CAP_TAI_KHOAN_MOBILE'),('IT','QL_PHAN_QUYEN'),('IT','SAO_LUU_CSDL')
) defaults(role,code)
JOIN public.vai_tro v ON v.ten_vai_tro=defaults.role
JOIN public.quyen q ON q.ma_quyen_code=defaults.code
ON CONFLICT DO NOTHING;
COMMIT;
