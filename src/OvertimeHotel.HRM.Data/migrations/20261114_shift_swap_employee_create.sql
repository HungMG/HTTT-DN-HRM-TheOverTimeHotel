-- Restrict new mobile swap requests while preserving responses and existing requests.
BEGIN;
DO $migration$
DECLARE
    definition text := pg_get_functiondef('public.hrm_shift_swap(text,jsonb)'::regprocedure);
    marker text := '    me := public.nhan_vien_hien_tai();';
    guard text := $guard$
    IF p_action = 'create' AND NOT EXISTS (
        SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING (ma_vai_tro)
        WHERE t.ma_nhan_vien = me AND t.trang_thai
          AND upper(btrim(v.ten_vai_tro)) IN ('EMPLOYEE', 'NHANVIEN', 'NHAN_VIEN')
    ) THEN
        RAISE EXCEPTION 'Chỉ nhân viên được gửi yêu cầu đổi ca.' USING ERRCODE = '42501';
    END IF;
$guard$;
BEGIN
    IF position(guard IN definition) > 0 THEN RETURN; END IF;
    IF position(marker IN definition) = 0 OR
       length(definition) - length(replace(definition, marker, '')) <> length(marker) THEN
        RAISE EXCEPTION 'Định nghĩa hàm đổi ca không khớp; dừng migration.';
    END IF;
    EXECUTE replace(definition, marker, marker || guard);
END
$migration$;
NOTIFY pgrst, 'reload schema';
COMMIT;
