BEGIN;
DROP FUNCTION IF EXISTS public.hrm_my_schedule_events(date,date);
NOTIFY pgrst,'reload schema';
COMMIT;
