\set ON_ERROR_STOP on
\ir ../ShiftSwapSupabase/fixture.sql
CREATE TABLE phong_ban(ma_phong_ban int PRIMARY KEY,ten_phong_ban text);
INSERT INTO phong_ban VALUES(1,'A'),(2,'B');
CREATE TABLE vai_tro(ma_vai_tro int PRIMARY KEY,ten_vai_tro text);
INSERT INTO vai_tro VALUES(1,'Admin'),(2,'HR'),(3,'Manager'),(4,'Employee'),(5,'IT');
ALTER TABLE tai_khoan ADD COLUMN ma_vai_tro int DEFAULT 4;
ALTER TABLE cham_cong ADD COLUMN gio_check_in timestamptz,ADD COLUMN gio_check_out timestamptz,ADD COLUMN trang_thai text;
CREATE TABLE giai_trinh_cham_cong(ma_giai_trinh serial PRIMARY KEY,ma_phan_ca int,ma_nhan_vien int,gio_de_xuat_vao timestamptz,gio_de_xuat_ra timestamptz,ly_do varchar(500),minh_chung varchar(500),trang_thai varchar(20),ma_quan_ly_xac_minh int,ngay_quan_ly_xac_minh timestamptz,y_kien_quan_ly text,ma_hr_xac_nhan int,ngay_hr_xac_nhan timestamptz,y_kien_hr text,ngay_gui timestamptz DEFAULT now());
CREATE UNIQUE INDEX uq_giai_trinh_dang_mo ON giai_trinh_cham_cong(ma_phan_ca) WHERE trang_thai IN ('CHO_QUAN_LY','CHO_HR');
INSERT INTO phan_ca VALUES(11,1,1,current_date-1,now()-interval '1 day 8 hours',now()-interval '1 day'),(12,2,1,current_date-1,now()-interval '1 day 8 hours',now()-interval '1 day'),(13,1,1,current_date-46,now()-interval '46 days 8 hours',now()-interval '46 days');
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',false),set_config('test.employee','1',false);
-- Fails before the new RPC is implemented.
\if :{?skip_rpc}
\else
SELECT public.hrm_attendance_explanation('eligible-shifts');
\endif
