-- Retire unused permission codes after switching Web Admin to the current catalog.
-- Existing role assignments for all other codes, including Mobile permissions,
-- are left unchanged. Run in a transaction and retain an audit trail.
BEGIN;
DO $$
BEGIN
 IF EXISTS (
  SELECT 1 FROM public.vai_tro_quyen old_grant
  JOIN public.quyen old_permission ON old_permission.ma_quyen=old_grant.ma_quyen
  WHERE old_permission.ma_quyen_code='XUAT_PHIEU_LUONG_PDF'
    AND NOT EXISTS (
     SELECT 1 FROM public.vai_tro_quyen current_grant
     JOIN public.quyen current_permission ON current_permission.ma_quyen=current_grant.ma_quyen
     WHERE current_grant.ma_vai_tro=old_grant.ma_vai_tro
       AND current_permission.ma_quyen_code='XEM_BANG_LUONG')
 ) THEN
  RAISE EXCEPTION 'Có vai trò chỉ có quyền PDF cũ mà chưa có quyền xem lương; cần đối chiếu trước khi chuyển.';
 END IF;
END $$;

CREATE TEMP TABLE hrm_retired_permission_grants ON COMMIT DROP AS
SELECT v.ten_vai_tro, q.ma_quyen_code
FROM public.vai_tro_quyen vq
JOIN public.vai_tro v ON v.ma_vai_tro=vq.ma_vai_tro
JOIN public.quyen q ON q.ma_quyen=vq.ma_quyen
WHERE q.ma_quyen_code IN ('SYS_ADMIN','XUAT_PHIEU_LUONG_PDF','QL_TRI_THUC_AI','DUYET_TANG_CA_OT');

INSERT INTO public.nhat_ky_quan_tri
 (ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,hanh_dong,noi_dung,gia_tri_cu,gia_tri_moi,ly_do)
SELECT 'Hệ thống theo yêu cầu người dùng', 'Vai trò ' || ten_vai_tro, 'UPDATE_ROLE_PERMISSION',
 'Thu hồi các mã quyền cũ không còn trong danh mục.',
 string_agg(ma_quyen_code, ', ' ORDER BY ma_quyen_code), NULL,
 'Chuyển quyền PDF sang quyền xem lương; AI quản lý bằng code; chức năng duyệt OT chưa triển khai.'
FROM hrm_retired_permission_grants GROUP BY ten_vai_tro;

DELETE FROM public.vai_tro_quyen vq USING public.quyen q
WHERE vq.ma_quyen=q.ma_quyen
 AND q.ma_quyen_code IN ('SYS_ADMIN','XUAT_PHIEU_LUONG_PDF','QL_TRI_THUC_AI','DUYET_TANG_CA_OT');
DELETE FROM public.quyen
WHERE ma_quyen_code IN ('SYS_ADMIN','XUAT_PHIEU_LUONG_PDF','QL_TRI_THUC_AI','DUYET_TANG_CA_OT');

NOTIFY pgrst, 'reload schema';
COMMIT;
