-- One-time correction: keep the current Admin grants, revoke report grants
-- from other roles. Re-running this migration does not undo later decisions.
BEGIN;
CREATE TABLE IF NOT EXISTS public.hrm_mobile_migration_history (migration_id text PRIMARY KEY);
REVOKE ALL ON public.hrm_mobile_migration_history FROM PUBLIC, anon, authenticated;
WITH first_run AS (
 INSERT INTO public.hrm_mobile_migration_history VALUES ('20261112_report_admin_grants')
 ON CONFLICT DO NOTHING RETURNING migration_id
)
DELETE FROM public.vai_tro_quyen vq USING public.vai_tro v, public.quyen q
WHERE vq.ma_vai_tro=v.ma_vai_tro AND vq.ma_quyen=q.ma_quyen
 AND v.ten_vai_tro <> 'Admin' AND q.ma_quyen_code IN ('REPORT_VIEW','REPORT_EXPORT')
 AND EXISTS (SELECT 1 FROM first_run);
COMMIT;
