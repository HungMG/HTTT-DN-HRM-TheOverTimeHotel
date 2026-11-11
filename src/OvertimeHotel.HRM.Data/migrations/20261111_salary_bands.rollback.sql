-- Guarded rollback only before feature use. No CASCADE and no historical-data repair.
BEGIN;
LOCK TABLE public.khung_luong_cap_bac, public.phu_luc_luong, public.salary_write_guard,
    public.salary_position_baseline, public.hop_dong, public.chuc_vu IN ACCESS EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT FROM (VALUES ('khung_luong_cap_bac'),('phu_luc_luong'),('salary_write_guard'),('salary_position_baseline')) t(name)
        WHERE obj_description(to_regclass('public.'||t.name),'pg_class') IS DISTINCT FROM 'salary_bands:20261111:v1') THEN
        RAISE EXCEPTION 'Rollback ownership marker mismatch; inventory and stop';
    END IF;
    IF EXISTS (SELECT FROM public.phu_luc_luong)
       OR EXISTS (SELECT FROM public.hop_dong WHERE ma_chuc_vu_ky_ket IS NOT NULL
            OR ma_phong_ban_ky_ket IS NOT NULL OR trinh_do_ky_ket IS NOT NULL)
       OR (SELECT is_called FROM public.phu_luc_luong_ma_phu_luc_seq) THEN
        RAISE EXCEPTION 'Salary feature has been used; rollback requires a separate preservation plan';
    END IF;
    IF EXISTS (
        SELECT nhom_chuc_vu,trinh_do,tham_nien_tu_thang,tham_nien_den_thang,luong_toi_thieu,luong_toi_da
        FROM public.khung_luong_cap_bac
        EXCEPT SELECT * FROM (VALUES
('NHAN_VIEN','Trung cấp',0,12,6000000,8000000), ('NHAN_VIEN','Trung cấp',12,36,7000000,9000000), ('NHAN_VIEN','Trung cấp',36,NULL,8000000,10000000),
('NHAN_VIEN','Cao đẳng',0,12,7000000,9000000), ('NHAN_VIEN','Cao đẳng',12,36,8000000,10000000), ('NHAN_VIEN','Cao đẳng',36,NULL,9000000,11000000),
('NHAN_VIEN','Đại học',0,12,8000000,10000000), ('NHAN_VIEN','Đại học',12,36,9000000,11000000), ('NHAN_VIEN','Đại học',36,NULL,10000000,12000000),
('QUAN_LY',NULL,0,12,12000000,16000000), ('QUAN_LY',NULL,12,36,14000000,18000000), ('QUAN_LY',NULL,36,NULL,16000000,22000000),
('TONG_GIAM_DOC',NULL,NULL,NULL,25000000,40000000)
        ) expected(nhom_chuc_vu,trinh_do,tham_nien_tu_thang,tham_nien_den_thang,luong_toi_thieu,luong_toi_da)
    ) THEN RAISE EXCEPTION 'Salary band seed changed; review before rollback'; END IF;
    IF (SELECT count(*) FROM public.khung_luong_cap_bac) <> 13
       OR (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgname IN (
            'salary_serialize_hop_dong','salary_serialize_nhan_vien','salary_serialize_phu_luc','salary_serialize_chuc_vu',
            'salary_guard_appendix','salary_check_hop_dong','salary_check_nhan_vien','salary_check_phu_luc','salary_check_chuc_vu'))<>9
       OR NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.chuc_vu'::regclass AND conname='chk_chuc_vu_nhom_luong')
       OR NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='public.hop_dong'::regclass AND conname='chk_hop_dong_salary_snapshot') THEN
        RAISE EXCEPTION 'Rollback object inventory mismatch; stop';
    END IF;
    IF EXISTS (SELECT FROM public.chuc_vu WHERE nhom_luong IS DISTINCT FROM
        CASE WHEN ma_chuc_vu=1 THEN 'TONG_GIAM_DOC'
             WHEN ma_chuc_vu IN (2,6,12) THEN 'QUAN_LY'
             WHEN ma_chuc_vu IN (4,5,7,8,9,10,11,13) THEN 'NHAN_VIEN' END) THEN
        RAISE EXCEPTION 'Position classification changed; preserve and review before rollback';
    END IF;
END $$;
DROP TRIGGER salary_check_hop_dong ON public.hop_dong;
DROP TRIGGER salary_check_nhan_vien ON public.nhan_vien;
DROP TRIGGER salary_check_phu_luc ON public.phu_luc_luong;
DROP TRIGGER salary_check_chuc_vu ON public.chuc_vu;
DROP TRIGGER salary_guard_appendix ON public.phu_luc_luong;
DROP TRIGGER salary_serialize_hop_dong ON public.hop_dong;
DROP TRIGGER salary_serialize_nhan_vien ON public.nhan_vien;
DROP TRIGGER salary_serialize_phu_luc ON public.phu_luc_luong;
DROP TRIGGER salary_serialize_chuc_vu ON public.chuc_vu;
DROP FUNCTION public.salary_guard_appendix();
DROP FUNCTION public.salary_check_gm_timeline();
DROP FUNCTION public.salary_serialize_write();
DROP TABLE public.phu_luc_luong;
DROP TABLE public.khung_luong_cap_bac;
DROP TABLE public.salary_write_guard;
DROP TABLE public.salary_position_baseline;
ALTER TABLE public.hop_dong DROP CONSTRAINT chk_hop_dong_salary_snapshot,
    DROP COLUMN ma_chuc_vu_ky_ket, DROP COLUMN ma_phong_ban_ky_ket, DROP COLUMN trinh_do_ky_ket;
ALTER TABLE public.chuc_vu DROP CONSTRAINT chk_chuc_vu_nhom_luong, DROP COLUMN nhom_luong;
COMMIT;
