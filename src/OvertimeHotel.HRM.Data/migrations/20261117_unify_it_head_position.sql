-- Run after the salary and IT mobile permission migrations. This transaction
-- replaces the duplicate IT head catalog ID without changing the IT role.
BEGIN;
DO $migration$
DECLARE ref record; guard_exists boolean; baseline_guard_exists boolean; still_used boolean;
BEGIN
    IF NOT EXISTS (SELECT FROM public.chuc_vu WHERE ma_chuc_vu=2
        AND ten_chuc_vu='Trưởng Bộ Phận (Department Head)' AND ma_phong_ban IS NULL
        AND nhom_luong='QUAN_LY') THEN
        RAISE EXCEPTION 'Shared department head position 2 is missing or has unexpected properties';
    END IF;
    IF EXISTS (SELECT FROM public.chuc_vu WHERE ma_chuc_vu=12
        AND (ten_chuc_vu <> 'Trưởng bộ phận CNTT (IT Manager)' OR nhom_luong IS DISTINCT FROM 'QUAN_LY')) THEN
        RAISE EXCEPTION 'Position 12 is not the expected legacy IT head';
    END IF;

    -- Staffing constraints can collide when the shared position already has
    -- an entry for the same shift/department. Stop for review, never discard it.
    IF to_regclass('public.dinh_bien_ca') IS NOT NULL AND EXISTS (
        SELECT FROM public.dinh_bien_ca a JOIN public.dinh_bien_ca b
          ON a.ma_ca=b.ma_ca AND a.ma_phong_ban IS NOT DISTINCT FROM b.ma_phong_ban
        WHERE a.ma_chuc_vu=12 AND b.ma_chuc_vu=2) THEN
        RAISE EXCEPTION 'Conflicting department head staffing configuration in dinh_bien_ca';
    END IF;
    IF to_regclass('public.dinh_bien_ngay') IS NOT NULL AND EXISTS (
        SELECT FROM public.dinh_bien_ngay a JOIN public.dinh_bien_ngay b
          ON a.ngay=b.ngay AND a.ma_ca=b.ma_ca AND a.ma_phong_ban IS NOT DISTINCT FROM b.ma_phong_ban
        WHERE a.ma_chuc_vu=12 AND b.ma_chuc_vu=2) THEN
        RAISE EXCEPTION 'Conflicting department head staffing configuration in dinh_bien_ngay';
    END IF;

    -- This is a catalog-ID correction, not a salary amendment. The normal
    -- guard blocks edits to past locked payroll; disable only that guard for
    -- this transaction and restore it before commit. Other triggers remain on.
    SELECT EXISTS (SELECT FROM pg_trigger WHERE tgrelid=to_regclass('public.phu_luc_luong')
        AND tgname='salary_guard_appendix' AND NOT tgisinternal) INTO guard_exists;
    SELECT EXISTS (SELECT FROM pg_trigger WHERE tgrelid=to_regclass('public.nhan_vien')
        AND tgname='salary_baseline_immutable' AND NOT tgisinternal) INTO baseline_guard_exists;
    IF EXISTS (SELECT FROM pg_trigger WHERE NOT tgisinternal AND tgenabled <> 'O' AND
        ((tgrelid=to_regclass('public.phu_luc_luong') AND tgname='salary_guard_appendix') OR
         (tgrelid=to_regclass('public.nhan_vien') AND tgname='salary_baseline_immutable'))) THEN
        RAISE EXCEPTION 'Salary guard has an unexpected state; review before migration';
    END IF;
    IF guard_exists THEN ALTER TABLE public.phu_luc_luong DISABLE TRIGGER salary_guard_appendix; END IF;
    IF baseline_guard_exists THEN ALTER TABLE public.nhan_vien DISABLE TRIGGER salary_baseline_immutable; END IF;

    FOR ref IN SELECT * FROM (VALUES
        ('nhan_vien','ma_chuc_vu'), ('nhan_vien','ma_chuc_vu_ghi_nhan'),
        ('hop_dong','ma_chuc_vu'), ('hop_dong','ma_chuc_vu_ky_ket'),
        ('phu_luc_luong','ma_chuc_vu_moi'), ('salary_position_baseline','ma_chuc_vu'),
        ('dinh_bien_ca','ma_chuc_vu'), ('dinh_bien_ngay','ma_chuc_vu')
    ) AS refs(table_name,column_name) LOOP
        IF EXISTS (SELECT FROM information_schema.columns c WHERE c.table_schema='public'
            AND c.table_name=ref.table_name AND c.column_name=ref.column_name) THEN
            EXECUTE format('UPDATE public.%I SET %I=2 WHERE %I=12',
                ref.table_name, ref.column_name, ref.column_name);
        END IF;
    END LOOP;
    -- Deferred salary integrity checks must finish before PostgreSQL permits
    -- altering the trigger state on tables touched above.
    SET CONSTRAINTS ALL IMMEDIATE;
    IF guard_exists THEN ALTER TABLE public.phu_luc_luong ENABLE TRIGGER salary_guard_appendix; END IF;
    IF baseline_guard_exists THEN ALTER TABLE public.nhan_vien ENABLE TRIGGER salary_baseline_immutable; END IF;
    -- Check even cascading foreign keys, which would otherwise delete data.
    FOR ref IN SELECT n.nspname AS schema_name, t.relname AS table_name, a.attname AS column_name
        FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
        JOIN pg_namespace n ON n.oid=t.relnamespace
        JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=c.conkey[1]
        WHERE c.contype='f' AND c.confrelid='public.chuc_vu'::regclass
          AND cardinality(c.conkey)=1 LOOP
        EXECUTE format('SELECT EXISTS (SELECT FROM %I.%I WHERE %I=12)',
            ref.schema_name, ref.table_name, ref.column_name) INTO still_used;
        IF still_used THEN
            RAISE EXCEPTION 'Position 12 is still referenced by %.%.%',
                ref.schema_name, ref.table_name, ref.column_name;
        END IF;
    END LOOP;
    DELETE FROM public.chuc_vu WHERE ma_chuc_vu=12;
END $migration$;

CREATE OR REPLACE FUNCTION public.hrm_is_it_head() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  JOIN public.nhan_vien n USING(ma_nhan_vien)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND v.ten_vai_tro='IT' AND n.ma_chuc_vu=2)
$$;
NOTIFY pgrst, 'reload schema';
COMMIT;
