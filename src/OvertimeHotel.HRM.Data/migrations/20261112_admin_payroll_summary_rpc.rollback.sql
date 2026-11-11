BEGIN;

DROP FUNCTION IF EXISTS public.hrm_admin_payroll_summary(integer);

NOTIFY pgrst, 'reload schema';

COMMIT;
