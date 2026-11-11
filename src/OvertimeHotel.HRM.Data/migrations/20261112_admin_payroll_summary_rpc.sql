-- Read-only aggregated payroll summary per period for Admin / BOD on Mobile.
-- Counts only finalized/locked payslips (trang_thai = 'DA_THANH_TOAN') inside locked periods.
BEGIN;

DO $$ BEGIN
    IF has_schema_privilege('authenticated', 'public', 'CREATE')
       OR has_schema_privilege('anon', 'public', 'CREATE') THEN
        RAISE EXCEPTION 'Cần kiểm kê quyền CREATE trong schema public trước triển khai.';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.hrm_admin_payroll_summary(p_limit integer DEFAULT 12)
RETURNS TABLE(
    ma_ky_luong integer,
    thang integer,
    nam integer,
    trang_thai text,
    so_phieu integer,
    tong_thuc_nhan numeric
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE
    v_limit integer := GREATEST(1, LEAST(COALESCE(p_limit, 12), 24));
    v_emp_id integer;
BEGIN
    IF NOT public.hrm_account_session_valid() THEN
        RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.' USING ERRCODE = '42501';
    END IF;

    v_emp_id := public.nhan_vien_hien_tai();
    IF v_emp_id IS NULL OR NOT EXISTS (
        SELECT 1
        FROM public.tai_khoan t
        JOIN public.vai_tro v ON v.ma_vai_tro = t.ma_vai_tro
        WHERE t.ma_nhan_vien = v_emp_id
          AND t.trang_thai = true
          AND v.ten_vai_tro = 'Admin'
    ) THEN
        RAISE EXCEPTION 'Chỉ Ban Giám Đốc (Admin) mới được xem tổng hợp quỹ lương toàn khách sạn.' USING ERRCODE = '42501';
    END IF;

    RETURN QUERY
    SELECT
        kl.ma_ky_luong,
        kl.thang,
        kl.nam,
        kl.trang_thai::text,
        COUNT(pl.ma_phieu_luong)::integer AS so_phieu,
        COALESCE(SUM(pl.thuc_nhan), 0)::numeric AS tong_thuc_nhan
    FROM public.ky_luong kl
    LEFT JOIN public.phieu_luong pl
           ON pl.ma_ky_luong = kl.ma_ky_luong
          AND kl.trang_thai IN ('KHOA_SO', 'DA_THANH_TOAN')
          AND pl.trang_thai = 'DA_THANH_TOAN'
    GROUP BY kl.ma_ky_luong, kl.thang, kl.nam, kl.trang_thai
    ORDER BY kl.nam DESC, kl.thang DESC
    LIMIT v_limit;
END;
$$;

REVOKE ALL ON FUNCTION public.hrm_admin_payroll_summary(integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_admin_payroll_summary(integer) TO authenticated;

NOTIFY pgrst, 'reload schema';

COMMIT;
