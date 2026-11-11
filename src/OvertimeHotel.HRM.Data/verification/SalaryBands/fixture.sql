-- ISOLATED DATABASE ONLY: owned by tests/SalaryMigration --local at port 55439.
DO $$ BEGIN
    IF inet_server_port() <> 55439 THEN RAISE EXCEPTION 'Synthetic fixture requires isolated port 55439'; END IF;
    IF EXISTS (SELECT FROM pg_tables WHERE schemaname='public') THEN RAISE EXCEPTION 'Synthetic fixture requires empty database'; END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon; END IF;
    IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated; END IF;
    IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='service_role') THEN CREATE ROLE service_role BYPASSRLS; END IF;
END $$;
CREATE TABLE public.phong_ban (ma_phong_ban integer PRIMARY KEY);
CREATE TABLE public.chuc_vu (ma_chuc_vu integer PRIMARY KEY, ten_chuc_vu text NOT NULL, ma_phong_ban integer);
CREATE TABLE public.nhan_vien (ma_nhan_vien integer PRIMARY KEY,ma_chuc_vu integer,ma_phong_ban integer,ngay_vao_lam date,ngay_thoi_viec date,trang_thai text);
CREATE TABLE public.hop_dong (ma_hop_dong integer PRIMARY KEY,ma_nhan_vien integer,ngay_bat_dau date,ngay_ket_thuc date,trang_thai text,luong_co_ban numeric(15,2));
CREATE TABLE public.ky_luong (ma_ky_luong integer PRIMARY KEY,tu_ngay date,den_ngay date,trang_thai text,ngay_duyet timestamptz,ngay_gui_duyet timestamptz,ly_do_tu_choi text);
INSERT INTO public.phong_ban VALUES (1),(2);
INSERT INTO public.chuc_vu (ma_chuc_vu,ten_chuc_vu) VALUES
(1,'Tổng Giám Đốc (GM)'),(2,'Trưởng Bộ Phận (Department Head)'),(4,'Nhân viên Lễ tân (Receptionist)'),
(5,'Nhân viên Buồng phòng (Housekeeper)'),(6,'Bếp Trưởng (Head Chef)'),(7,'Nhân viên Phục vụ (F&B Staff)'),
(8,'Kỹ sư Tòa nhà (Technician)'),(9,'Nhân viên An ninh (Security Guard)'),
(10,'Chuyên viên Nhân sự & Tiền lương (HR & Payroll)'),(11,'Quản trị hệ thống CNTT (IT Support)'),
(12,'Trưởng bộ phận CNTT (IT Manager)'),(13,'Nhân viên Bếp (Kitchen Staff)');
INSERT INTO public.nhan_vien VALUES (1,1,1,'2026-01-01',NULL,'DANG_LAM'),(2,4,1,'2026-01-01',NULL,'DANG_LAM');
INSERT INTO public.hop_dong VALUES (1,1,'2026-01-01','2026-12-31','HIEU_LUC',8000000), (2,2,'2026-01-01','2026-12-31','HIEU_LUC',8000000);
