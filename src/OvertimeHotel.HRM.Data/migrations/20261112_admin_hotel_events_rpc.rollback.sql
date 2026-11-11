BEGIN;

DROP FUNCTION IF EXISTS public.hrm_admin_hotel_events(integer);

NOTIFY pgrst, 'reload schema';

COMMIT;
