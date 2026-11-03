\set ON_ERROR_STOP on
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $$;
CREATE SCHEMA auth;
CREATE FUNCTION auth.uid() RETURNS uuid LANGUAGE sql AS $$ SELECT nullif(current_setting('test.uid',true),'')::uuid $$;
CREATE FUNCTION public.nhan_vien_hien_tai() RETURNS integer LANGUAGE sql AS $$ SELECT nullif(current_setting('test.employee',true),'')::integer $$;
CREATE TABLE nhan_vien(ma_nhan_vien int PRIMARY KEY,ho text,ten text,ma_phong_ban int);
CREATE TABLE tai_khoan(ma_nhan_vien int,trang_thai boolean);
CREATE TABLE ca_lam_viec(ma_ca int PRIMARY KEY,ten_ca text,gio_bat_dau time,gio_ket_thuc time);
CREATE TABLE phan_ca(ma_phan_ca int PRIMARY KEY,ma_nhan_vien int,ma_ca int,ngay_lam_viec date,bat_dau_du_kien timestamptz,ket_thuc_du_kien timestamptz);
CREATE TABLE cham_cong(ma_phan_ca int);
CREATE TABLE yeu_cau_doi_ca(ma_yeu_cau serial PRIMARY KEY,ma_phan_ca_gui int,ma_phan_ca_nhan int,ma_nhan_vien_gui int,ma_nhan_vien_nhan int,ly_do text,trang_thai text,ben_tu_choi text,phan_hoi_nguoi_nhan text,ngay_nguoi_nhan_phan_hoi timestamptz,ma_nguoi_duyet int,ngay_gui timestamptz DEFAULT now(),ngay_duyet timestamptz,phan_hoi_duyet text);
INSERT INTO nhan_vien VALUES(1,'A','A',1),(2,'B','B',1),(3,'C','C',2);
INSERT INTO tai_khoan VALUES(1,true),(2,true),(3,true);
INSERT INTO ca_lam_viec VALUES(1,'Sáng','06:00','14:00');
INSERT INTO phan_ca SELECT n,n,1,current_date+1,(current_date+1+time '06:00') AT TIME ZONE 'Asia/Ho_Chi_Minh',(current_date+1+time '14:00') AT TIME ZONE 'Asia/Ho_Chi_Minh' FROM generate_series(1,3)n;
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',false),set_config('test.employee','1',false);
-- This call must fail before the migration exists.
\ir ../../migrations/20261103_shift_swap_supabase_rpc.sql
SET ROLE authenticated;
SELECT public.hrm_shift_swap('my-shifts');
RESET ROLE;
\ir assertions.sql
