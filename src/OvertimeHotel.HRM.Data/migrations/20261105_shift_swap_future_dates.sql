-- Restrict both shifts to tomorrow through today +30, using Vietnam dates.
-- Preserve the existing function, owner, grants and all business data.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);
DO $$
DECLARE
    definition text := pg_get_functiondef('public.hrm_shift_swap(text,jsonb)'::regprocedure);
    old_text text;
    new_text text;
BEGIN
    FOR old_text, new_text IN SELECT * FROM (VALUES
        ('today date := (now() AT TIME ZONE ''Asia/Ho_Chi_Minh'')::date;', 'today date := (clock_timestamp() AT TIME ZONE ''Asia/Ho_Chi_Minh'')::date;'),
        ('IF g.ma_phan_ca IS NULL', E'today := (clock_timestamp() AT TIME ZONE ''Asia/Ho_Chi_Minh'')::date;\n        IF g.ma_phan_ca IS NULL'),
        ('p.ngay_lam_viec BETWEEN today AND today+30', 'p.ngay_lam_viec > today AND p.ngay_lam_viec <= today+30'),
        ('g.ngay_lam_viec NOT BETWEEN today AND today+30 OR n.ngay_lam_viec NOT BETWEEN today AND today+30', 'g.ngay_lam_viec <= today OR g.ngay_lam_viec > today+30 OR n.ngay_lam_viec <= today OR n.ngay_lam_viec > today+30'),
        ('Ca đã kết thúc hoặc ngoài khoảng 30 ngày được chọn.', 'Chỉ được đổi ca từ ngày mai đến 30 ngày tới; ca phải chưa kết thúc.')
    ) AS replacements(old_value,new_value) LOOP
        IF (length(definition)-length(replace(definition,old_text,'')))/length(old_text) <> 1 THEN
            RAISE EXCEPTION 'Định nghĩa RPC đổi ca không khớp bản đã kiểm kê; dừng migration.';
        END IF;
        definition := replace(definition,old_text,new_text);
    END LOOP;
    EXECUTE definition;
END $$;
NOTIFY pgrst,'reload schema';
COMMIT;
