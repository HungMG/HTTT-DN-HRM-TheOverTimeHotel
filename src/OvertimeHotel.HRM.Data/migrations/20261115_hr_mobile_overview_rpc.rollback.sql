BEGIN;
DROP FUNCTION IF EXISTS public.hrm_hr_overview(text);
NOTIFY pgrst,'reload schema';
COMMIT;
