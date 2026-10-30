-- Apply only with the new WebAdmin API deployed and after inventory/review.
-- Mobile reads remain available through RLS; all writes to these two new
-- workflows must go through WebAdmin, where authorization and locks run.
BEGIN;

REVOKE ALL PRIVILEGES ON TABLE public.yeu_cau_doi_ca, public.giai_trinh_cham_cong FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.yeu_cau_doi_ca, public.giai_trinh_cham_cong TO authenticated;

-- PostgreSQL combines permissive policies with OR. An unexpected additional
-- INSERT policy must be reviewed rather than silently weakening this guard.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'don_tu'
               AND permissive = 'PERMISSIVE' AND cmd IN ('INSERT', 'ALL')
               AND policyname <> 'nop_don_tu_cua_toi') THEN
        RAISE EXCEPTION 'additional permissive leave INSERT policy requires review';
    END IF;
END $$;

-- Existing leave submission stays on PostgREST, but a client must not insert
-- a pre-approved/reviewed leave request by supplying extra JSON properties.
ALTER POLICY nop_don_tu_cua_toi ON public.don_tu
    WITH CHECK (
        ma_nhan_vien = public.nhan_vien_hien_tai()
        AND trang_thai = 'CHO_DUYET'
        AND ma_nguoi_duyet IS NULL
        AND ngay_duyet IS NULL
        AND phan_hoi_duyet IS NULL
    );

-- Fail closed if inherited or column-level privileges still permit writes.
DO $$
DECLARE r text; t text;
BEGIN
    FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        FOREACH t IN ARRAY ARRAY['public.yeu_cau_doi_ca', 'public.giai_trinh_cham_cong'] LOOP
            IF has_table_privilege(r, t, 'INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES')
               OR has_any_column_privilege(r, t, 'INSERT,UPDATE,REFERENCES') THEN
                RAISE EXCEPTION 'Direct write privilege remains for role % on %, inspect inherited and column grants', r, t;
            END IF;
        END LOOP;
    END LOOP;
END $$;

COMMIT;
