\set ON_ERROR_STOP on
-- Run only on the isolated fixture. All changes are rolled back.
DO $$ BEGIN
    IF current_database() !~ '^hrm_swap_test_' THEN RAISE EXCEPTION 'Isolated hrm_swap_test_ database required'; END IF;
END $$;
BEGIN;
SET LOCAL TIME ZONE 'UTC';
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',true),set_config('test.employee','1',true);
DELETE FROM yeu_cau_doi_ca;
DELETE FROM cham_cong;
DELETE FROM phan_ca WHERE ma_phan_ca NOT IN (1,2);
UPDATE phan_ca SET ngay_lam_viec=(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
    bat_dau_du_kien=now()+interval '1 hour',ket_thuc_du_kien=now()+interval '9 hours';
SET LOCAL ROLE authenticated;
DO $$ BEGIN
    IF public.hrm_shift_swap('my-shifts')<>'[]'::jsonb THEN RAISE EXCEPTION 'Today sender shift must be hidden, even before it starts'; END IF;
    IF public.hrm_shift_swap('partner-shifts')<>'[]'::jsonb THEN RAISE EXCEPTION 'Today partner shift must be hidden'; END IF;
    IF public.hrm_shift_swap_partner_options()<>'[]'::jsonb THEN RAISE EXCEPTION 'Today must not appear in compatible options'; END IF;
END $$;
SELECT public.test_reject('create','{"senderShift":1,"receiverShift":2,"reason":"today"}');
RESET ROLE;
-- Test either side independently, then both tomorrow and the +30/+31 boundary.
DO $$ DECLARE pair record; rejected boolean; result jsonb; vn_today date:=(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date; BEGIN
    FOR pair IN SELECT * FROM (VALUES (0,1,false),(1,0,false),(1,1,true),(30,30,true),(31,1,false),(1,31,false)) AS cases(sender_day,receiver_day,allowed) LOOP
        DELETE FROM yeu_cau_doi_ca;
        UPDATE phan_ca SET ngay_lam_viec=vn_today+CASE ma_phan_ca WHEN 1 THEN pair.sender_day ELSE pair.receiver_day END;
        UPDATE phan_ca SET bat_dau_du_kien=(ngay_lam_viec+time '06:00') AT TIME ZONE 'Asia/Ho_Chi_Minh',
            ket_thuc_du_kien=(ngay_lam_viec+time '14:00') AT TIME ZONE 'Asia/Ho_Chi_Minh';
        -- A same-day shift that has not ended, regardless of when the test runs.
        UPDATE phan_ca SET bat_dau_du_kien=now()+interval '1 hour',ket_thuc_du_kien=now()+interval '9 hours' WHERE ngay_lam_viec=vn_today;
        IF jsonb_array_length(public.hrm_shift_swap('my-shifts'))<>(CASE WHEN pair.sender_day BETWEEN 1 AND 30 THEN 1 ELSE 0 END) THEN RAISE EXCEPTION 'Wrong sender date list boundary'; END IF;
        IF jsonb_array_length(public.hrm_shift_swap('partner-shifts'))<>(CASE WHEN pair.receiver_day BETWEEN 1 AND 30 THEN 1 ELSE 0 END) THEN RAISE EXCEPTION 'Wrong partner date list boundary'; END IF;
        IF jsonb_array_length(public.hrm_shift_swap_partner_options())<>(CASE WHEN pair.allowed THEN 1 ELSE 0 END) THEN RAISE EXCEPTION 'Wrong compatible options date boundary'; END IF;
        rejected:=false;
        BEGIN result:=public.hrm_shift_swap('create','{"senderShift":1,"receiverShift":2,"reason":"date boundary"}');
        EXCEPTION WHEN OTHERS THEN rejected:=true; END;
        IF rejected=pair.allowed THEN RAISE EXCEPTION 'Wrong create result for dates +%/+%, allowed %',pair.sender_day,pair.receiver_day,pair.allowed; END IF;
        IF NOT pair.allowed AND EXISTS(SELECT 1 FROM yeu_cau_doi_ca) THEN RAISE EXCEPTION 'Rejected create wrote a request'; END IF;
    END LOOP;
END $$;
-- A request created earlier must not be accepted once either shift is today.
DELETE FROM yeu_cau_doi_ca;
UPDATE phan_ca SET ngay_lam_viec=(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date+1,
    bat_dau_du_kien=now()+interval '2 days',ket_thuc_du_kien=now()+interval '2 days 8 hours';
SELECT public.hrm_shift_swap('create','{"senderShift":1,"receiverShift":2,"reason":"created before cutoff"}');
UPDATE phan_ca SET ngay_lam_viec=ngay_lam_viec-1 WHERE ma_phan_ca=1;
SELECT set_config('test.employee','2',true);
SET LOCAL ROLE authenticated;
SELECT public.test_reject('respond',jsonb_build_object('id',(public.hrm_shift_swap('list')->0->>'MaYeuCau')::int,'accept',true));
RESET ROLE;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM yeu_cau_doi_ca WHERE trang_thai<>'CHO_NGUOI_NHAN') THEN RAISE EXCEPTION 'Failed accept changed request state'; END IF; END $$;
UPDATE phan_ca SET ngay_lam_viec=ngay_lam_viec+CASE ma_phan_ca WHEN 1 THEN 1 ELSE -1 END;
SET LOCAL ROLE authenticated;
SELECT public.test_reject('respond',jsonb_build_object('id',(public.hrm_shift_swap('list')->0->>'MaYeuCau')::int,'accept',true));
RESET ROLE;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM yeu_cau_doi_ca WHERE trang_thai<>'CHO_NGUOI_NHAN') THEN RAISE EXCEPTION 'Failed accept of today receiver changed state'; END IF; END $$;
-- Refusal and withdrawal remain available for an old waiting request.
SELECT public.hrm_shift_swap('respond',jsonb_build_object('id',(public.hrm_shift_swap('list')->0->>'MaYeuCau')::int,'accept',false,'reason','Cannot exchange today'));
UPDATE yeu_cau_doi_ca SET trang_thai='CHO_DUYET';
SELECT set_config('test.employee','1',true);
SELECT public.hrm_shift_swap('withdraw',jsonb_build_object('id',(public.hrm_shift_swap('list')->0->>'MaYeuCau')::int));
DO $$ BEGIN IF EXISTS(SELECT 1 FROM yeu_cau_doi_ca WHERE trang_thai<>'DA_HUY') THEN RAISE EXCEPTION 'Old waiting request cannot be withdrawn'; END IF; END $$;
ROLLBACK;
SELECT 'PASS: Vietnam date cutoff, both sides, tomorrow/+30/+31, stale accept, refusal and withdrawal' AS result;
