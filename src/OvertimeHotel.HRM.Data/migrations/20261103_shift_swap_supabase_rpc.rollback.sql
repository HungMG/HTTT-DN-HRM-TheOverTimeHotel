-- Roll back client to WebAdmin API first. Keep historical requests unchanged.
-- Deliberately preserves the restriction on direct client table writes.
BEGIN;
DROP FUNCTION public.hrm_shift_swap(text,jsonb);
NOTIFY pgrst,'reload schema';
COMMIT;
