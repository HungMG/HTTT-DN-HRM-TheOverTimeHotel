CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE SCHEMA auth;
CREATE FUNCTION auth.uid() RETURNS uuid LANGUAGE sql AS $$ SELECT nullif(current_setting('test.uid',true),'')::uuid $$;
CREATE FUNCTION public.nhan_vien_hien_tai() RETURNS integer LANGUAGE sql AS $$ SELECT nullif(current_setting('test.employee',true),'')::integer $$;
CREATE TABLE public.nhan_vien (ma_nhan_vien integer PRIMARY KEY, dien_thoai varchar(20) NOT NULL, email_ca_nhan varchar(100), dia_chi varchar(255));
CREATE TABLE public.tai_khoan (ma_nhan_vien integer, trang_thai boolean);
INSERT INTO public.nhan_vien VALUES (1,'111',NULL,NULL),(2,'222',NULL,NULL);
INSERT INTO public.tai_khoan VALUES (1,true),(2,true),(3,false);
\ir ../../src/OvertimeHotel.HRM.Data/migrations/20261104_employee_contact_rpc.sql
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',false), set_config('test.employee','1',false);
SET ROLE authenticated;
SELECT public.hrm_my_contact('update','0901234567','a@example.com','HCM');
DO $$ BEGIN
 IF (public.hrm_my_contact('get')->>'Phone') <> '0901234567' THEN RAISE EXCEPTION 'own update failed'; END IF;
 BEGIN PERFORM public.hrm_my_contact('update','','a@example.com','HCM'); RAISE EXCEPTION 'invalid phone accepted'; EXCEPTION WHEN raise_exception THEN IF SQLERRM = 'invalid phone accepted' THEN RAISE; END IF; END;
 IF has_table_privilege('authenticated','public.nhan_vien','UPDATE') THEN RAISE EXCEPTION 'direct update permitted'; END IF;
 IF has_function_privilege('anon','public.hrm_my_contact(text,text,text,text)','EXECUTE') THEN RAISE EXCEPTION 'anonymous execute permitted'; END IF;
END $$;
RESET ROLE;
DO $$ BEGIN IF (SELECT dien_thoai FROM public.nhan_vien WHERE ma_nhan_vien=2) <> '222' THEN RAISE EXCEPTION 'other employee changed'; END IF; END $$;
SELECT set_config('test.uid','',false);
SET ROLE authenticated;
DO $$ BEGIN BEGIN PERFORM public.hrm_my_contact('get'); RAISE EXCEPTION 'missing auth accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END; END $$;
RESET ROLE;
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000003',false), set_config('test.employee','3',false);
SET ROLE authenticated;
DO $$ BEGIN BEGIN PERFORM public.hrm_my_contact('get'); RAISE EXCEPTION 'inactive account accepted'; EXCEPTION WHEN insufficient_privilege THEN NULL; END; END $$;
RESET ROLE;
