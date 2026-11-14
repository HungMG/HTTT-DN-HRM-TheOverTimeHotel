-- Isolated empty PostgreSQL database only.
\set ON_ERROR_STOP on
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF;
END $$;
CREATE TABLE vai_tro(ma_vai_tro int PRIMARY KEY, ten_vai_tro text);
CREATE TABLE quyen(ma_quyen int PRIMARY KEY, ma_quyen_code text);
CREATE TABLE vai_tro_quyen(ma_vai_tro int, ma_quyen int);
CREATE TABLE tai_khoan(ma_nhan_vien int,ma_vai_tro int,trang_thai boolean);
CREATE TABLE nhan_vien(ma_nhan_vien int PRIMARY KEY,ma_phong_ban int,ma_chuc_vu int);
CREATE TABLE phan_ca(ma_phan_ca int PRIMARY KEY,ma_nhan_vien int);
CREATE TABLE cham_cong(ma_phan_ca int);
CREATE TABLE don_tu(ma_don int PRIMARY KEY,ma_nhan_vien int,ma_loai_don int,trang_thai text);
CREATE FUNCTION hrm_account_session_valid() RETURNS boolean LANGUAGE sql STABLE AS $$ SELECT true $$;
CREATE FUNCTION nhan_vien_hien_tai() RETURNS integer LANGUAGE sql STABLE AS $$ SELECT current_setting('test.employee')::int $$;
CREATE FUNCTION vai_tro_hien_tai() RETURNS integer LANGUAGE sql STABLE AS $$ SELECT 5 $$;
CREATE FUNCTION phong_ban_hien_tai() RETURNS integer LANGUAGE sql STABLE AS $$ SELECT 9 $$;
CREATE FUNCTION phong_ban_cua_don(i integer) RETURNS integer LANGUAGE sql STABLE SECURITY DEFINER AS $$
 SELECT n.ma_phong_ban FROM don_tu d JOIN nhan_vien n USING(ma_nhan_vien) WHERE d.ma_don=i $$;
INSERT INTO vai_tro VALUES (1,'IT'),(2,'Employee'),(3,'Manager');
INSERT INTO quyen VALUES (1,'NOP_DON_TU'),(2,'XEM_DON_NGHI'),(3,'DUYET_DON_NGHI'),
 (4,'XEM_CHAM_CONG_BO_PHAN'),(5,'XEP_CA_LAM'),(6,'CHAM_CONG_DI_DONG');
INSERT INTO vai_tro_quyen VALUES (2,1),(3,2),(3,3),(3,4),(3,5);
INSERT INTO nhan_vien VALUES (1,9,12),(2,9,11),(3,1,11);
INSERT INTO tai_khoan VALUES (1,1,true),(2,1,true),(3,1,true);
INSERT INTO phan_ca VALUES (1,1),(2,2),(3,3);
INSERT INTO cham_cong VALUES (1),(2),(3);
INSERT INTO don_tu VALUES (1,2,1,'CHO_DUYET'),(2,3,1,'CHO_DUYET');
ALTER TABLE nhan_vien ENABLE ROW LEVEL SECURITY;
ALTER TABLE phan_ca ENABLE ROW LEVEL SECURITY;
ALTER TABLE cham_cong ENABLE ROW LEVEL SECURITY;
ALTER TABLE don_tu ENABLE ROW LEVEL SECURITY;
GRANT SELECT,INSERT,UPDATE,DELETE ON nhan_vien,phan_ca,cham_cong,don_tu TO authenticated;
-- Deliberately broad legacy policies test that the new restrictive boundaries hold.
CREATE POLICY old_nhan_vien ON nhan_vien FOR SELECT TO authenticated USING (true);
CREATE POLICY old_phan_ca ON phan_ca FOR ALL TO authenticated USING (true) WITH CHECK (true);
CREATE POLICY old_cham_cong ON cham_cong FOR SELECT TO authenticated USING (true);
CREATE POLICY old_don_tu ON don_tu FOR ALL TO authenticated USING (true) WITH CHECK (true);
\ir ../../migrations/20261114_it_head_mobile_manager.sql
SELECT set_config('test.employee','1',false);
DO $$ BEGIN
 IF NOT hrm_is_it_head() OR hrm_has_permission('NOP_DON_TU') OR NOT hrm_has_permission('XEM_DON_NGHI') THEN
  RAISE EXCEPTION 'IT head did not follow Manager grants'; END IF;
 IF NOT hrm_has_permission('QL_PHAN_QUYEN') THEN RAISE EXCEPTION 'IT admin rights lost'; END IF;
END $$;
SET ROLE authenticated;
DO $$ BEGIN
 IF (SELECT count(*) FROM nhan_vien)<>2 OR (SELECT count(*) FROM phan_ca)<>2 OR
    (SELECT count(*) FROM cham_cong)<>2 OR (SELECT count(*) FROM don_tu)<>1 THEN
  RAISE EXCEPTION 'IT head department scope incorrect'; END IF;
 UPDATE don_tu SET trang_thai='DA_DUYET' WHERE ma_don=1;
 IF NOT FOUND THEN RAISE EXCEPTION 'IT head could not approve department request'; END IF;
 UPDATE don_tu SET trang_thai='DA_DUYET' WHERE ma_don=2;
 IF FOUND THEN RAISE EXCEPTION 'IT head approved another department'; END IF;
END $$;
RESET ROLE;
SELECT set_config('test.employee','2',false);
DO $$ BEGIN
 IF hrm_is_it_head() OR NOT hrm_has_permission('NOP_DON_TU') OR hrm_has_permission('XEM_DON_NGHI') THEN
  RAISE EXCEPTION 'Ordinary IT did not follow Employee grants'; END IF;
END $$;
SELECT set_config('test.employee','1',false);
DELETE FROM vai_tro_quyen WHERE ma_vai_tro=3 AND ma_quyen=2;
DO $$ BEGIN IF hrm_has_permission('XEM_DON_NGHI') THEN RAISE EXCEPTION 'Revoked Manager grant persisted'; END IF; END $$;
