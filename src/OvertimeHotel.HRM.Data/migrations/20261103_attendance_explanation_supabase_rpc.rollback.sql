-- Revert Android to WebAdmin service first; keep data and direct-write restrictions.
BEGIN;
DROP FUNCTION public.hrm_attendance_explanation(text,jsonb);
NOTIFY pgrst,'reload schema';
COMMIT;
