-- Run on a test database after 20261112_payroll_permission_workflow.sql. All fixture changes are rolled back.
BEGIN;
CREATE TEMP TABLE payroll_test_fixture AS
SELECT p.ma_phieu_luong,p.ma_ky_luong,p.ma_nhan_vien,
 (SELECT p2.ma_phieu_luong FROM public.phieu_luong p2 WHERE p2.ma_nhan_vien<>p.ma_nhan_vien LIMIT 1) AS other_id,
 jsonb_build_object('sub',s.user_id,'session_id',s.id,'iat',extract(epoch FROM clock_timestamp())::bigint,
 'role','authenticated','app_metadata',jsonb_build_object('ma_nhan_vien',t.ma_nhan_vien,'ma_vai_tro',t.ma_vai_tro))::text claims
FROM auth.sessions s JOIN auth.users u ON u.id=s.user_id
JOIN public.nhan_vien nv ON lower(nv.email)=lower(u.email)
JOIN public.tai_khoan t USING(ma_nhan_vien) JOIN public.phieu_luong p USING(ma_nhan_vien)
WHERE t.trang_thai AND s.created_at>t.phien_hop_le_sau ORDER BY s.created_at DESC LIMIT 1;
GRANT SELECT ON payroll_test_fixture TO authenticated;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM payroll_test_fixture WHERE other_id IS NOT NULL) THEN RAISE EXCEPTION 'Need active session and two employee payslips'; END IF; END $$;
SELECT set_config('request.jwt.claims',claims,true) FROM payroll_test_fixture;
DO $$ BEGIN IF NOT public.hrm_account_session_valid() THEN RAISE EXCEPTION 'Test session invalid'; END IF; END $$;
UPDATE public.ky_luong k SET trang_thai='CHO_PHAN_HOI',ngay_phat_hanh_nhap=now(),ngay_duyet=null,ly_do_tu_choi=null FROM payroll_test_fixture f WHERE k.ma_ky_luong=f.ma_ky_luong;
UPDATE public.phieu_luong p SET trang_thai='NHAP' FROM payroll_test_fixture f WHERE p.ma_phieu_luong=f.ma_phieu_luong;
SET LOCAL ROLE authenticated;
DO $$ DECLARE f record; BEGIN
 SELECT * INTO f FROM payroll_test_fixture;
 IF NOT public.hrm_can_read_own_payslip(f.ma_phieu_luong) OR NOT EXISTS(SELECT 1 FROM public.phieu_luong WHERE ma_phieu_luong=f.ma_phieu_luong) THEN RAISE EXCEPTION 'Published own draft not visible'; END IF;
 IF public.hrm_can_read_own_payslip(f.other_id) OR EXISTS(SELECT 1 FROM public.phieu_luong WHERE ma_phieu_luong=f.other_id) THEN RAISE EXCEPTION 'Other employee payslip leaked'; END IF;
 IF EXISTS(SELECT 1 FROM public.chi_tiet_phieu_luong WHERE ma_phieu_luong=f.other_id) THEN RAISE EXCEPTION 'Other employee payslip details leaked'; END IF;
END $$;
RESET ROLE;
UPDATE public.ky_luong k SET ngay_phat_hanh_nhap=null FROM payroll_test_fixture f WHERE k.ma_ky_luong=f.ma_ky_luong;
SET LOCAL ROLE authenticated;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM public.phieu_luong p JOIN payroll_test_fixture f USING(ma_phieu_luong)) THEN RAISE EXCEPTION 'Unpublished draft leaked'; END IF; END $$;
RESET ROLE;
UPDATE public.ky_luong k SET trang_thai='MO',ngay_duyet=now() FROM payroll_test_fixture f WHERE k.ma_ky_luong=f.ma_ky_luong;
UPDATE public.phieu_luong p SET trang_thai='DA_DUYET' FROM payroll_test_fixture f WHERE p.ma_phieu_luong=f.ma_phieu_luong;
SET LOCAL ROLE authenticated;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM public.phieu_luong p JOIN payroll_test_fixture f USING(ma_phieu_luong)) THEN RAISE EXCEPTION 'Own approved official payslip not visible'; END IF; END $$;
RESET ROLE;
ROLLBACK;
