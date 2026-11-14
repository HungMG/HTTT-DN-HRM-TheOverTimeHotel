-- Resolve waiting profiles first; rollback never deletes them or invents joining dates.
BEGIN;
DO $rollback$
DECLARE metadata jsonb; c jsonb;
BEGIN
    SELECT obj_description(oid,'pg_constraint')::jsonb INTO metadata FROM pg_constraint
        WHERE conrelid='public.nhan_vien'::regclass AND conname='ck_nhan_vien_onboarding_status';
    IF NOT FOUND THEN RETURN; END IF;
    IF metadata IS NULL OR NOT metadata ? 'previous_checks' THEN RAISE EXCEPTION 'Missing original constraint metadata'; END IF;
    IF EXISTS (SELECT FROM public.nhan_vien WHERE trang_thai='CHO_HOP_DONG' OR ngay_vao_lam IS NULL) THEN
        RAISE EXCEPTION 'Cannot rollback while unsigned intake profiles exist; resolve them first';
    END IF;
    ALTER TABLE public.nhan_vien DROP CONSTRAINT ck_nhan_vien_onboarding_dates;
    ALTER TABLE public.nhan_vien DROP CONSTRAINT ck_nhan_vien_onboarding_status;
    FOR c IN SELECT value FROM jsonb_array_elements(metadata->'previous_checks') LOOP
        EXECUTE format('ALTER TABLE public.nhan_vien ADD CONSTRAINT %I %s',c->>'name',c->>'definition');
    END LOOP;
    IF (metadata->>'joining_date_required')::boolean THEN
        ALTER TABLE public.nhan_vien ALTER COLUMN ngay_vao_lam SET NOT NULL;
    END IF;
END $rollback$;
COMMIT;
