BEGIN;
INSERT INTO public.quyen(ma_quyen_code,ten_quyen,mo_ta) VALUES
 ('DUYET_BANG_LUONG','Duyệt bảng lương','Duyệt hoặc từ chối bảng lương đã gửi duyệt.')
ON CONFLICT (ma_quyen_code) DO NOTHING;
UPDATE public.quyen SET ten_quyen='Quản lý và phát hành bảng lương',
 mo_ta='Tính, điều chỉnh, phát hành lương nháp; gửi duyệt, khóa sổ, phát hành và xuất chuyển khoản.'
 WHERE ma_quyen_code='TINH_LUONG';
-- Role grants are managed in the permission screen.

-- This helper reads the persisted period state as the database owner; the caller
-- can never choose an employee ID and receives only a yes/no for their own slip.
CREATE OR REPLACE FUNCTION public.hrm_can_read_own_payslip(p_id integer) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.phieu_luong p JOIN public.ky_luong k USING(ma_ky_luong)
  WHERE p.ma_phieu_luong=p_id AND p.ma_nhan_vien=public.nhan_vien_hien_tai() AND (
   p.trang_thai='DA_THANH_TOAN'
   OR (p.trang_thai='DA_DUYET' AND k.ngay_duyet IS NOT NULL)
   OR (p.trang_thai IN ('NHAP','CHO_DUYET') AND k.ngay_phat_hanh_nhap IS NOT NULL
       AND k.ngay_duyet IS NULL AND coalesce(k.ly_do_tu_choi,'')='')
  )
 )
$$;
REVOKE ALL ON FUNCTION public.hrm_can_read_own_payslip(integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_can_read_own_payslip(integer) TO authenticated;
DROP POLICY IF EXISTS hrm_mobile_own_published ON public.phieu_luong;
CREATE POLICY hrm_mobile_own_published ON public.phieu_luong AS RESTRICTIVE FOR SELECT TO authenticated
 USING(public.hrm_can_read_own_payslip(ma_phieu_luong));
DROP POLICY IF EXISTS hrm_mobile_own_published ON public.chi_tiet_phieu_luong;
CREATE POLICY hrm_mobile_own_published ON public.chi_tiet_phieu_luong AS RESTRICTIVE FOR SELECT TO authenticated
 USING(public.hrm_can_read_own_payslip(ma_phieu_luong));

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

    IF NOT public.hrm_has_permission('XEM_BANG_LUONG') THEN
        RAISE EXCEPTION 'Bạn không có quyền xem bảng lương.' USING ERRCODE = '42501';
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
