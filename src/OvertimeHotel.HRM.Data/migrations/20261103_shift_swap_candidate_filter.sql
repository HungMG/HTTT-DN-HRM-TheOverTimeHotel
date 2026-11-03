-- Read-only extension. Deploy after 20261103_shift_swap_supabase_rpc.sql.
BEGIN;
DO $$ BEGIN
    IF has_schema_privilege('authenticated','public','CREATE') OR has_schema_privilege('anon','public','CREATE') THEN
        RAISE EXCEPTION 'Schema public cho phép client CREATE; cần kiểm kê quyền trước.';
    END IF;
END $$;
CREATE FUNCTION public.hrm_shift_swap_partner_options()
RETURNS jsonb LANGUAGE sql SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
    WITH mine AS MATERIALIZED (
        SELECT value AS item FROM jsonb_array_elements(public.hrm_shift_swap('my-shifts'))
    ), partners AS MATERIALIZED (
        SELECT value AS item FROM jsonb_array_elements(public.hrm_shift_swap('partner-shifts'))
    ), options AS (
        SELECT partner.item,
            (SELECT jsonb_agg(g.ma_phan_ca ORDER BY g.ma_phan_ca)
             FROM mine JOIN public.phan_ca g ON g.ma_phan_ca=(mine.item->>'MaPhanCa')::integer
             WHERE NOT EXISTS (
                 SELECT 1 FROM public.phan_ca other
                 WHERE (other.ma_nhan_vien=g.ma_nhan_vien AND other.ma_phan_ca<>g.ma_phan_ca
                    AND other.bat_dau_du_kien<n.ket_thuc_du_kien+interval '8 hours'
                    AND other.ket_thuc_du_kien>n.bat_dau_du_kien-interval '8 hours')
                    OR (other.ma_nhan_vien=n.ma_nhan_vien AND other.ma_phan_ca<>n.ma_phan_ca
                    AND other.bat_dau_du_kien<g.ket_thuc_du_kien+interval '8 hours'
                    AND other.ket_thuc_du_kien>g.bat_dau_du_kien-interval '8 hours')
             )) AS compatible
        FROM partners partner JOIN public.phan_ca n ON n.ma_phan_ca=(partner.item->>'MaPhanCa')::integer
    )
    SELECT coalesce(jsonb_agg(item || jsonb_build_object('CompatibleSenderShiftIds',compatible)
        ORDER BY item->>'NgayLamViec',item->>'BatDauDuKien',item->>'MaPhanCa'),'[]'::jsonb)
    FROM options WHERE compatible IS NOT NULL;
$$;
REVOKE ALL ON FUNCTION public.hrm_shift_swap_partner_options() FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_shift_swap_partner_options() TO authenticated;
NOTIFY pgrst,'reload schema';
COMMIT;
