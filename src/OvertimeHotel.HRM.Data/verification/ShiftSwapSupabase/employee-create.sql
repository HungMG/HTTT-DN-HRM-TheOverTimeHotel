-- Run after fixture.sql, against an isolated fixture database only.
\set ON_ERROR_STOP on
CREATE TABLE public.vai_tro(ma_vai_tro int PRIMARY KEY, ten_vai_tro text);
ALTER TABLE public.tai_khoan ADD COLUMN ma_vai_tro int;
INSERT INTO public.vai_tro VALUES (1,'Employee'), (2,'Manager'), (3,'HR'), (4,'Admin'), (5,'IT');
UPDATE public.tai_khoan SET ma_vai_tro=1;
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',false),
       set_config('test.employee','1',false);
\ir ../../migrations/20261114_shift_swap_employee_create.sql
\ir ../../migrations/20261114_shift_swap_employee_create.sql
DO $$
DECLARE role_id int; denied boolean;
BEGIN
  FOR role_id IN 2..5 LOOP
    UPDATE public.tai_khoan SET ma_vai_tro=role_id WHERE ma_nhan_vien=1;
    denied:=false;
    BEGIN PERFORM public.hrm_shift_swap('create','{}');
    EXCEPTION WHEN insufficient_privilege THEN denied:=true; END;
    IF NOT denied THEN RAISE EXCEPTION 'Role % could create a swap', role_id; END IF;
    PERFORM public.hrm_shift_swap('list');
  END LOOP;
  UPDATE public.tai_khoan SET ma_vai_tro=1 WHERE ma_nhan_vien=1;
  denied:=false;
  BEGIN PERFORM public.hrm_shift_swap('create','{}');
  EXCEPTION WHEN insufficient_privilege THEN denied:=true;
            WHEN OTHERS THEN NULL; END;
  IF denied THEN RAISE EXCEPTION 'Employee rejected by role check'; END IF;
END $$;
