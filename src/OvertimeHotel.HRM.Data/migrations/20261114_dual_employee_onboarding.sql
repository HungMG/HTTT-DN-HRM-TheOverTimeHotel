-- Apply before deploying dual onboarding. Existing employees and dates are preserved.
BEGIN;
DO $migration$
DECLARE c record; old_checks jsonb := '[]'::jsonb; predicate text := ''; status_column smallint; required_date boolean;
BEGIN
    IF EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.nhan_vien'::regclass AND conname='ck_nhan_vien_onboarding_status') THEN
        IF NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.nhan_vien'::regclass AND conname='ck_nhan_vien_onboarding_dates') THEN
            RAISE EXCEPTION 'Incomplete onboarding migration; inspect constraints';
        END IF;
        RETURN;
    END IF;
    SELECT attnum INTO STRICT status_column FROM pg_attribute WHERE attrelid='public.nhan_vien'::regclass AND attname='trang_thai';
    IF EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.nhan_vien'::regclass AND contype='c'
        AND conkey @> ARRAY[status_column] AND conkey<>ARRAY[status_column]) THEN
        RAISE EXCEPTION 'Composite employee status constraint requires review before onboarding migration';
    END IF;
    SELECT attnotnull INTO STRICT required_date FROM pg_attribute WHERE attrelid='public.nhan_vien'::regclass AND attname='ngay_vao_lam';
    FOR c IN SELECT conname, pg_get_constraintdef(oid) AS definition, pg_get_expr(conbin,conrelid) AS expression
        FROM pg_constraint WHERE conrelid='public.nhan_vien'::regclass AND contype='c' AND conkey=ARRAY[status_column]
    LOOP
        old_checks := old_checks || jsonb_build_array(jsonb_build_object('name',c.conname,'definition',c.definition));
        predicate := predicate || CASE WHEN predicate='' THEN '' ELSE ' AND ' END || '(' || c.expression || ')';
        EXECUTE format('ALTER TABLE public.nhan_vien DROP CONSTRAINT %I',c.conname);
    END LOOP;
    IF predicate='' THEN predicate := $$trang_thai IN ('DANG_LAM','NGHI_PHEP','DA_THOI_VIEC','THOI_VIEC','TAM_NGHI','NGHI_VIEC')$$; END IF;
    EXECUTE 'ALTER TABLE public.nhan_vien ADD CONSTRAINT ck_nhan_vien_onboarding_status CHECK (trang_thai = ''CHO_HOP_DONG'' OR (' || predicate || '))';
    EXECUTE format('COMMENT ON CONSTRAINT ck_nhan_vien_onboarding_status ON public.nhan_vien IS %L',
        jsonb_build_object('previous_checks',old_checks,'joining_date_required',required_date)::text);
    ALTER TABLE public.nhan_vien ALTER COLUMN ngay_vao_lam DROP NOT NULL;
    ALTER TABLE public.nhan_vien ADD CONSTRAINT ck_nhan_vien_onboarding_dates CHECK (
        (trang_thai='CHO_HOP_DONG' AND ngay_vao_lam IS NULL AND ngay_thoi_viec IS NULL AND is_profile_complete)
        OR (trang_thai<>'CHO_HOP_DONG' AND ngay_vao_lam IS NOT NULL));
END $migration$;
COMMIT;
