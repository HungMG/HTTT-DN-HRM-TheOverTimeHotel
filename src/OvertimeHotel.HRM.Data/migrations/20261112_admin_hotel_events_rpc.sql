-- Read-only upcoming/active hotel events and Task Force headcount summary for Admin / BOD on Mobile.
BEGIN;

DO $$ BEGIN
    IF has_schema_privilege('authenticated', 'public', 'CREATE')
       OR has_schema_privilege('anon', 'public', 'CREATE') THEN
        RAISE EXCEPTION 'Cần kiểm kê quyền CREATE trong schema public trước triển khai.';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.hrm_admin_hotel_events(p_limit integer DEFAULT 4)
RETURNS TABLE(
    ma_du_an integer,
    ten_du_an text,
    loai_du_an text,
    dia_diem text,
    ngay_bat_dau timestamptz,
    ngay_ket_thuc timestamptz,
    so_khach_du_kien integer,
    trang_thai text,
    task_force_count integer,
    task_force_detail text
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE
    v_limit integer := GREATEST(1, LEAST(COALESCE(p_limit, 4), 12));
    v_emp_id integer;
    v_today_start timestamptz := date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh') AT TIME ZONE 'Asia/Ho_Chi_Minh';
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
        RAISE EXCEPTION 'Chỉ Ban Giám Đốc (Admin) mới được xem tổng hợp sự kiện trọng điểm.' USING ERRCODE = '42501';
    END IF;

    RETURN QUERY
    WITH upcoming AS (
        SELECT d.*
        FROM public.du_an d
        WHERE d.trang_thai IN ('LEN_KE_HOACH', 'DANG_DIEN_RA')
          AND d.ngay_ket_thuc >= v_today_start
        ORDER BY
            CASE WHEN d.trang_thai = 'DANG_DIEN_RA' THEN 0 ELSE 1 END,
            d.ngay_bat_dau ASC
        LIMIT v_limit
    ),
    dept_counts AS (
        SELECT
            p.ma_du_an,
            COALESCE(pb.ten_phong_ban, 'Khác') AS ten_phong_ban,
            COUNT(DISTINCT p.ma_nhan_vien)::integer AS dept_staff
        FROM public.phan_cong_du_an p
        JOIN upcoming u ON u.ma_du_an = p.ma_du_an
        JOIN public.nhan_vien nv ON nv.ma_nhan_vien = p.ma_nhan_vien
        LEFT JOIN public.phong_ban pb ON pb.ma_phong_ban = nv.ma_phong_ban
        WHERE COALESCE(p.trang_thai_tham_gia, '') <> 'HUY_PHAN_CONG'
        GROUP BY p.ma_du_an, pb.ten_phong_ban
    ),
    tf_agg AS (
        SELECT
            u.ma_du_an,
            COALESCE((
                SELECT COUNT(DISTINCT p2.ma_nhan_vien)::integer
                FROM public.phan_cong_du_an p2
                WHERE p2.ma_du_an = u.ma_du_an
                  AND COALESCE(p2.trang_thai_tham_gia, '') <> 'HUY_PHAN_CONG'
            ), 0) AS total_staff,
            (
                SELECT string_agg(dc.ten_phong_ban || ' (' || dc.dept_staff || ')', ' · ' ORDER BY dc.dept_staff DESC, dc.ten_phong_ban)
                FROM dept_counts dc
                WHERE dc.ma_du_an = u.ma_du_an
            ) AS dept_summary
        FROM upcoming u
    )
    SELECT
        u.ma_du_an,
        u.ten_du_an::text,
        u.loai_du_an::text,
        u.dia_diem::text,
        u.ngay_bat_dau,
        u.ngay_ket_thuc,
        COALESCE(u.so_khach_du_kien, 0)::integer,
        u.trang_thai::text,
        t.total_staff::integer AS task_force_count,
        COALESCE(t.dept_summary, 'Chưa phân công nhân sự Task Force')::text AS task_force_detail
    FROM upcoming u
    JOIN tf_agg t ON t.ma_du_an = u.ma_du_an
    ORDER BY
        CASE WHEN u.trang_thai = 'DANG_DIEN_RA' THEN 0 ELSE 1 END,
        u.ngay_bat_dau ASC;
END;
$$;

REVOKE ALL ON FUNCTION public.hrm_admin_hotel_events(integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.hrm_admin_hotel_events(integer) TO authenticated;

NOTIFY pgrst, 'reload schema';

COMMIT;
