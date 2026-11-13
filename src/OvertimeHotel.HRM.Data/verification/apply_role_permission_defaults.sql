-- Apply the agreed role defaults once. Re-running resets later permission changes.
-- Run only when intentionally resetting the five roles.
BEGIN;
CREATE TEMP TABLE requested_role_permissions (role text NOT NULL, code text NOT NULL, PRIMARY KEY(role, code)) ON COMMIT DROP;
INSERT INTO requested_role_permissions(role, code) VALUES
 ('Admin','XEM_BANG_LUONG'),('Admin','DUYET_BANG_LUONG'),('Admin','REPORT_VIEW'),('Admin','REPORT_EXPORT'),
 ('HR','QL_NHAN_SU'),('HR','QL_HOP_DONG'),('HR','QL_DANH_MUC'),
 ('HR','XEM_LICH_CA'),('HR','XEP_CA_LAM'),('HR','XEM_YEU_CAU_DOI_CA'),('HR','DUYET_DOI_CA'),
 ('HR','XEM_CHAM_CONG_BO_PHAN'),('HR','XEM_GIAI_TRINH_CHAM_CONG'),('HR','XAC_MINH_GIAI_TRINH'),
 ('HR','XEM_DON_NGHI'),('HR','DUYET_DON_NGHI'),('HR','DUYET_THOI_VIEC'),('HR','QL_SU_KIEN'),
 ('HR','XEM_BANG_LUONG'),('HR','TINH_LUONG'),
 ('HR','CHAM_CONG_DI_DONG'),('HR','NOP_DON_TU'),
 ('Manager','XEM_LICH_CA'),('Manager','XEP_CA_LAM'),('Manager','XEM_YEU_CAU_DOI_CA'),
 ('Manager','DUYET_DOI_CA'),('Manager','XEM_CHAM_CONG_BO_PHAN'),('Manager','XEM_GIAI_TRINH_CHAM_CONG'),
 ('Manager','XAC_MINH_GIAI_TRINH'),('Manager','XEM_DON_NGHI'),('Manager','DUYET_DON_NGHI'),
 ('Manager','XEM_BANG_LUONG'),('Manager','CHAM_CONG_DI_DONG'),('Manager','NOP_DON_TU'),
 ('Employee','CHAM_CONG_DI_DONG'),('Employee','NOP_DON_TU'),
 ('IT','QL_TAI_KHOAN'),('IT','CAP_TAI_KHOAN_MOBILE'),('IT','QL_PHAN_QUYEN'),('IT','SAO_LUU_CSDL');
DO $$
BEGIN
 IF (SELECT count(*) FROM public.vai_tro WHERE ten_vai_tro IN ('Admin','HR','Manager','Employee','IT')) <> 5 THEN
  RAISE EXCEPTION 'Thiếu vai trò mặc định; không thay đổi quyền.';
 END IF;
 IF EXISTS (SELECT 1 FROM requested_role_permissions d LEFT JOIN public.quyen q ON q.ma_quyen_code=d.code WHERE q.ma_quyen IS NULL) THEN
  RAISE EXCEPTION 'Thiếu mã quyền mặc định; không thay đổi quyền.';
 END IF;
END $$;
DELETE FROM public.vai_tro_quyen vq USING public.vai_tro v
WHERE vq.ma_vai_tro=v.ma_vai_tro AND v.ten_vai_tro IN ('Admin','HR','Manager','Employee','IT');
INSERT INTO public.vai_tro_quyen(ma_vai_tro, ma_quyen)
SELECT v.ma_vai_tro, q.ma_quyen FROM requested_role_permissions d
JOIN public.vai_tro v ON v.ten_vai_tro=d.role
JOIN public.quyen q ON q.ma_quyen_code=d.code;
DROP TABLE IF EXISTS public.hrm_mobile_migration_history;
COMMIT;
