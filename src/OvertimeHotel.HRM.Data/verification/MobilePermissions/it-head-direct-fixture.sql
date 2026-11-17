-- Run only in a new empty PostgreSQL database with psql -v ON_ERROR_STOP=1.
\ir it-head-fixture.sql
ALTER TABLE nhan_vien ADD COLUMN trang_thai text DEFAULT 'DANG_LAM';
ALTER TABLE tai_khoan ADD COLUMN ma_tai_khoan integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY;
CREATE TABLE hop_dong(ma_hop_dong integer PRIMARY KEY, ma_nhan_vien integer, trang_thai text, ngay_bat_dau date, ngay_ket_thuc date);
CREATE TABLE phu_luc_luong(ma_phu_luc bigint PRIMARY KEY, ma_hop_dong integer, ngay_hieu_luc date, ma_chuc_vu_moi integer);
UPDATE nhan_vien SET ma_chuc_vu=2 WHERE ma_nhan_vien=1;
INSERT INTO vai_tro_quyen VALUES(3,2);
\ir ../../migrations/20261117_it_head_direct_grant.sql
DO $$ BEGIN
 IF has_table_privilege('authenticated','public.it_head_grant','INSERT') OR
    has_function_privilege('authenticated','public.hrm_it_head_access(integer)','EXECUTE') THEN
  RAISE EXCEPTION 'Mobile sessions must not create grants or inspect another employee'; END IF;
END $$;
SELECT set_config('test.employee','1',false);
DO $$ BEGIN IF NOT hrm_is_it_head() OR NOT hrm_has_permission('XEM_DON_NGHI') THEN RAISE EXCEPTION 'Existing head access lost'; END IF; END $$;
INSERT INTO hop_dong VALUES(10,2,'HIEU_LUC',CURRENT_DATE-30,CURRENT_DATE+30);
INSERT INTO phu_luc_luong VALUES(10,10,CURRENT_DATE,2);
UPDATE nhan_vien SET ma_chuc_vu=2 WHERE ma_nhan_vien=2;
SELECT set_config('test.employee','2',false);
DO $$ BEGIN IF hrm_is_it_head() OR hrm_has_permission('XEM_DON_NGHI') OR NOT hrm_has_permission('NOP_DON_TU') THEN RAISE EXCEPTION 'Head access granted before direct grant'; END IF; END $$;
INSERT INTO it_head_grant(ma_nhan_vien,ma_phu_luc,ma_nguoi_cap) SELECT 2,10,ma_tai_khoan FROM tai_khoan WHERE ma_nhan_vien=1;
DO $$ BEGIN IF NOT hrm_is_it_head() OR NOT hrm_has_permission('XEM_DON_NGHI') THEN RAISE EXCEPTION 'Direct grant not effective'; END IF; END $$;
-- Renewing a contract without changing position must not revoke the grant.
UPDATE hop_dong SET trang_thai='HET_HAN',ngay_ket_thuc=CURRENT_DATE WHERE ma_hop_dong=10;
INSERT INTO hop_dong VALUES(11,2,'HIEU_LUC',CURRENT_DATE,CURRENT_DATE+365);
DO $$ BEGIN IF NOT hrm_is_it_head() THEN RAISE EXCEPTION 'Contract renewal revoked access'; END IF; END $$;
INSERT INTO phu_luc_luong VALUES(11,11,CURRENT_DATE,11);
UPDATE nhan_vien SET ma_chuc_vu=11 WHERE ma_nhan_vien=2;
DO $$ BEGIN IF hrm_is_it_head() OR hrm_has_permission('XEM_DON_NGHI') THEN RAISE EXCEPTION 'Access not revoked after position change'; END IF; END $$;
