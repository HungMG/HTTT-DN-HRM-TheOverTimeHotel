-- Roll back the Android client first. No business data changes.
BEGIN;
DROP FUNCTION public.hrm_shift_swap_partner_options();
NOTIFY pgrst,'reload schema';
COMMIT;
