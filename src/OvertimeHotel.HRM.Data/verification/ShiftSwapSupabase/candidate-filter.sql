\set ON_ERROR_STOP on
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',false),set_config('test.employee','1',false);
INSERT INTO phan_ca SELECT 4,1,1,ngay_lam_viec+1,bat_dau_du_kien+interval '1 day',ket_thuc_du_kien+interval '1 day' FROM phan_ca WHERE ma_phan_ca=1;
INSERT INTO phan_ca SELECT 5,2,1,ngay_lam_viec+1,bat_dau_du_kien+interval '1 day',ket_thuc_du_kien+interval '1 day' FROM phan_ca WHERE ma_phan_ca=2;
SET ROLE authenticated;
DO $$ DECLARE options jsonb; BEGIN
 options:=public.hrm_shift_swap_partner_options();
 IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(options) p WHERE (p->>'MaPhanCa')::int=2 AND p->'CompatibleSenderShiftIds'='[1]'::jsonb) THEN RAISE EXCEPTION 'Only compatible sender shift allowed'; END IF;
 IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(options) p WHERE (p->>'MaPhanCa')::int=5 AND p->'CompatibleSenderShiftIds'='[4]'::jsonb) THEN RAISE EXCEPTION 'Filter changes with sender shift'; END IF;
END $$;
RESET ROLE;
UPDATE phan_ca SET bat_dau_du_kien=bat_dau_du_kien+interval '8 hours',ket_thuc_du_kien=ket_thuc_du_kien+interval '8 hours' WHERE ma_phan_ca=2;
SET ROLE authenticated;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(public.hrm_shift_swap_partner_options()) p WHERE (p->>'MaPhanCa')::int=2 AND p->'CompatibleSenderShiftIds' @> '[1]'::jsonb) THEN RAISE EXCEPTION 'Exactly eight hours rest must be allowed'; END IF;
END $$;
RESET ROLE;
UPDATE phan_ca SET bat_dau_du_kien=bat_dau_du_kien-interval '1 minute' WHERE ma_phan_ca=4;
SET ROLE authenticated;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM jsonb_array_elements(public.hrm_shift_swap_partner_options()) p WHERE (p->>'MaPhanCa')::int=2 AND p->'CompatibleSenderShiftIds' @> '[1]'::jsonb) THEN RAISE EXCEPTION 'Rest below eight hours must be excluded'; END IF;
END $$;
RESET ROLE;
DELETE FROM phan_ca WHERE ma_phan_ca IN(4,5);
UPDATE phan_ca SET ngay_lam_viec=ngay_lam_viec+2,bat_dau_du_kien=bat_dau_du_kien+interval '2 days',ket_thuc_du_kien=ket_thuc_du_kien+interval '2 days' WHERE ma_phan_ca=2;
SET ROLE authenticated;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(public.hrm_shift_swap_partner_options()) p WHERE p->'CompatibleSenderShiftIds'='[1]'::jsonb) THEN RAISE EXCEPTION 'Valid different-day exchange must remain available'; END IF;
END $$;
RESET ROLE;
SELECT 'PASS: selected sender filters both schedules; valid different-day swap allowed' AS result;
