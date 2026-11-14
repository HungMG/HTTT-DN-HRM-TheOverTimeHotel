-- Keep historical account requests when an account (recipient or handler) is deleted.
BEGIN;
ALTER TABLE public.yeu_cau_tai_khoan
    ADD COLUMN IF NOT EXISTS ma_tai_khoan_da_xoa integer,
    ADD COLUMN IF NOT EXISTS ma_nguoi_xu_ly_da_xoa integer,
    ADD COLUMN IF NOT EXISTS ten_nguoi_xu_ly_da_xoa varchar(50);
ALTER TABLE public.yeu_cau_tai_khoan DROP CONSTRAINT IF EXISTS chk_yeu_cau_da_cap_can_co_tai_khoan;
ALTER TABLE public.yeu_cau_tai_khoan ADD CONSTRAINT chk_yeu_cau_da_cap_can_co_tai_khoan
    CHECK ((trang_thai = 'DA_CAP') = (ma_tai_khoan IS NOT NULL OR ma_tai_khoan_da_xoa IS NOT NULL));
ALTER TABLE public.yeu_cau_tai_khoan DROP CONSTRAINT IF EXISTS chk_yeu_cau_da_xu_ly_can_co_nguoi;
ALTER TABLE public.yeu_cau_tai_khoan ADD CONSTRAINT chk_yeu_cau_da_xu_ly_can_co_nguoi
    CHECK (trang_thai = 'CHO_IT' OR ma_nguoi_xu_ly IS NOT NULL OR ma_nguoi_xu_ly_da_xoa IS NOT NULL);

-- Existing business triggers must skip only the narrowly scoped archival update below.
DO $migration$
DECLARE fn text; definition text; begin_position integer;
    guard text := $guard$
    IF TG_OP = 'UPDATE'
       AND current_setting('hrm.archiving_deleted_account', true) ~ '^[0-9]+$'
       AND (to_jsonb(NEW) - ARRAY['ma_tai_khoan','ma_tai_khoan_da_xoa','ma_nguoi_xu_ly','ma_nguoi_xu_ly_da_xoa','ten_nguoi_xu_ly_da_xoa']) =
           (to_jsonb(OLD) - ARRAY['ma_tai_khoan','ma_tai_khoan_da_xoa','ma_nguoi_xu_ly','ma_nguoi_xu_ly_da_xoa','ten_nguoi_xu_ly_da_xoa'])
       AND ((NEW.ma_tai_khoan IS NOT DISTINCT FROM OLD.ma_tai_khoan AND NEW.ma_tai_khoan_da_xoa IS NOT DISTINCT FROM OLD.ma_tai_khoan_da_xoa)
          OR (OLD.ma_tai_khoan::text = current_setting('hrm.archiving_deleted_account', true) AND NEW.ma_tai_khoan IS NULL AND NEW.ma_tai_khoan_da_xoa = OLD.ma_tai_khoan))
       AND ((NEW.ma_nguoi_xu_ly IS NOT DISTINCT FROM OLD.ma_nguoi_xu_ly AND NEW.ma_nguoi_xu_ly_da_xoa IS NOT DISTINCT FROM OLD.ma_nguoi_xu_ly_da_xoa)
          OR (OLD.ma_nguoi_xu_ly::text = current_setting('hrm.archiving_deleted_account', true) AND NEW.ma_nguoi_xu_ly IS NULL AND NEW.ma_nguoi_xu_ly_da_xoa = OLD.ma_nguoi_xu_ly)) THEN
        RETURN NEW;
    END IF;
$guard$;
BEGIN
    FOREACH fn IN ARRAY ARRAY['chan_yeu_cau_tai_khoan_trung', 'chan_yeu_cau_tai_khoan_dung_quyen'] LOOP
        IF to_regprocedure('public.' || fn || '()') IS NULL THEN CONTINUE; END IF;
        SELECT pg_get_functiondef(to_regprocedure('public.' || fn || '()')) INTO definition;
        IF strpos(definition, 'hrm.archiving_deleted_account') > 0 THEN CONTINUE; END IF;
        begin_position := strpos(definition, 'BEGIN');
        IF begin_position = 0 THEN RAISE EXCEPTION 'Unexpected trigger function %', fn; END IF;
        EXECUTE overlay(definition placing guard from begin_position + 5 for 0);
    END LOOP;
END $migration$;

CREATE OR REPLACE FUNCTION public.hrm_archive_deleted_account_requests() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE prior_setting text;
BEGIN
    prior_setting := current_setting('hrm.archiving_deleted_account', true);
    PERFORM set_config('hrm.archiving_deleted_account', OLD.ma_tai_khoan::text, true);
    UPDATE public.yeu_cau_tai_khoan
    SET ma_tai_khoan_da_xoa = CASE WHEN ma_tai_khoan = OLD.ma_tai_khoan THEN ma_tai_khoan ELSE ma_tai_khoan_da_xoa END,
        ma_tai_khoan = CASE WHEN ma_tai_khoan = OLD.ma_tai_khoan THEN NULL ELSE ma_tai_khoan END,
        ten_nguoi_xu_ly_da_xoa = CASE WHEN ma_nguoi_xu_ly = OLD.ma_tai_khoan THEN OLD.ten_dang_nhap ELSE ten_nguoi_xu_ly_da_xoa END,
        ma_nguoi_xu_ly_da_xoa = CASE WHEN ma_nguoi_xu_ly = OLD.ma_tai_khoan THEN ma_nguoi_xu_ly ELSE ma_nguoi_xu_ly_da_xoa END,
        ma_nguoi_xu_ly = CASE WHEN ma_nguoi_xu_ly = OLD.ma_tai_khoan THEN NULL ELSE ma_nguoi_xu_ly END
    WHERE ma_tai_khoan = OLD.ma_tai_khoan OR ma_nguoi_xu_ly = OLD.ma_tai_khoan;
    PERFORM set_config('hrm.archiving_deleted_account', coalesce(prior_setting, ''), true);
    RETURN OLD;
END $$;
DROP TRIGGER IF EXISTS hrm_archive_deleted_account_requests ON public.tai_khoan;
CREATE TRIGGER hrm_archive_deleted_account_requests BEFORE DELETE ON public.tai_khoan
FOR EACH ROW EXECUTE FUNCTION public.hrm_archive_deleted_account_requests();
COMMIT;
