BEGIN;
DO $$ BEGIN
    IF (SELECT count(*) FROM public.khung_luong_cap_bac)<>13 THEN RAISE EXCEPTION 'Expected 13 bands'; END IF;
    IF (SELECT count(*) FROM public.khung_luong_cap_bac WHERE nhom_chuc_vu='TONG_GIAM_DOC')<>1 THEN RAISE EXCEPTION 'Expected one GM band'; END IF;
    IF has_table_privilege('anon','public.phu_luc_luong','SELECT')
       OR has_table_privilege('authenticated','public.khung_luong_cap_bac','SELECT')
       OR NOT has_table_privilege('service_role','public.phu_luc_luong','INSERT') THEN RAISE EXCEPTION 'Privilege boundary mismatch'; END IF;
    BEGIN
        INSERT INTO public.khung_luong_cap_bac(nhom_chuc_vu,trinh_do,tham_nien_tu_thang,tham_nien_den_thang,luong_toi_thieu,luong_toi_da)
        VALUES('NHAN_VIEN',NULL,0,12,1,2);
        RAISE EXCEPTION 'Nullable education escaped check';
    EXCEPTION WHEN check_violation THEN NULL; END;
    BEGIN
        INSERT INTO public.khung_luong_cap_bac(nhom_chuc_vu,trinh_do,tham_nien_tu_thang,tham_nien_den_thang,luong_toi_thieu,luong_toi_da)
        VALUES('QUAN_LY',NULL,0,NULL,1,2);
        RAISE EXCEPTION 'Nullable tenure escaped check';
    EXCEPTION WHEN check_violation THEN NULL; END;
    BEGIN
        INSERT INTO public.khung_luong_cap_bac(nhom_chuc_vu,luong_toi_thieu,luong_toi_da) VALUES('TONG_GIAM_DOC',1,2);
        RAISE EXCEPTION 'Duplicate null GM band escaped check';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
        VALUES(1,'2026-02-15',10000000,'raise','fixture');
        RAISE EXCEPTION 'Midmonth appendix escaped check';
    EXCEPTION WHEN check_violation THEN NULL; END;
    UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=4,ma_phong_ban_ky_ket=1,trinh_do_ky_ket='Đại học';
    INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
    VALUES(1,'2026-02-01',10000000,'raise','fixture');
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
        VALUES(1,'2026-02-01',11000000,'raise','fixture');
        RAISE EXCEPTION 'Duplicate effective date escaped check';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    INSERT INTO public.ky_luong(ma_ky_luong,tu_ngay,den_ngay,trang_thai) VALUES(1,'2026-03-01','2026-03-31','CHO_DUYET');
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
        VALUES(1,'2026-03-01',11000000,'raise','fixture');
        RAISE EXCEPTION 'Locked payroll escaped check';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'Salary amendment affects an approved or locked payroll period' THEN RAISE; END IF;
    END;
    BEGIN
        DELETE FROM public.phu_luc_luong WHERE ma_hop_dong=1;
        RAISE EXCEPTION 'Locked historical deletion escaped check';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'Salary amendment affects an approved or locked payroll period' THEN RAISE; END IF;
    END;
    -- A date flag protects a period even when its display status is stale.
    UPDATE public.ky_luong SET trang_thai='MO',ngay_duyet=now();
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
        VALUES(1,'2026-03-01',11000000,'raise','fixture');
        RAISE EXCEPTION 'Approved timestamp escaped check';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'Salary amendment affects an approved or locked payroll period' THEN RAISE; END IF;
    END;
    UPDATE public.ky_luong SET trang_thai='DA_THANH_TOAN',ngay_duyet=NULL;
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao)
        VALUES(1,'2026-03-01',11000000,'raise','fixture');
        RAISE EXCEPTION 'Paid payroll escaped check';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'Salary amendment affects an approved or locked payroll period' THEN RAISE; END IF;
    END;
    DELETE FROM public.ky_luong;
    -- A future contract cannot vacate the current legacy GM seat during a gap.
    UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=NULL,ma_phong_ban_ky_ket=NULL,trinh_do_ky_ket=NULL,
        ngay_ket_thuc='2026-02-28' WHERE ma_hop_dong=1;
    UPDATE public.nhan_vien SET ma_chuc_vu=1 WHERE ma_nhan_vien=1;
    INSERT INTO public.hop_dong(ma_hop_dong,ma_nhan_vien,ngay_bat_dau,ngay_ket_thuc,trang_thai,luong_co_ban,ma_chuc_vu_ky_ket,ma_phong_ban_ky_ket)
    VALUES(3,1,'2027-01-01','2027-12-31','HIEU_LUC',25000000,1,1);
    SET CONSTRAINTS ALL IMMEDIATE;
    BEGIN
        UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=1 WHERE ma_hop_dong=2;
        RAISE EXCEPTION 'Contract gap vacated current GM seat';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'General director appointments overlap; verify dated history' THEN RAISE; END IF;
    END;
    DELETE FROM public.hop_dong WHERE ma_hop_dong=3;
    UPDATE public.nhan_vien SET ma_chuc_vu=4 WHERE ma_nhan_vien=1;
    UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=4,ma_phong_ban_ky_ket=1,trinh_do_ky_ket='Đại học',ngay_ket_thuc='2026-12-31' WHERE ma_hop_dong=1;
    INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao,ma_chuc_vu_moi,ma_phong_ban_moi)
    VALUES(1,'2026-06-01',25000000,'promotion','fixture',1,1);
    SET CONSTRAINTS ALL IMMEDIATE;
    BEGIN
        INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao,ma_chuc_vu_moi,ma_phong_ban_moi)
        VALUES(2,'2026-07-01',25000000,'promotion','fixture',1,1);
        RAISE EXCEPTION 'Overlapping GM appointment escaped check';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'General director appointments overlap; verify dated history' THEN RAISE; END IF;
    END;
    INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao,ma_chuc_vu_moi,ma_phong_ban_moi)
    VALUES(1,'2026-08-01',14000000,'transfer','fixture',2,1),
          (2,'2026-08-01',25000000,'handover','fixture',1,1);
    -- Worker activation must not rewrite a new GM's unknown hire-time role backwards.
    DELETE FROM public.phu_luc_luong;
    UPDATE public.hop_dong SET ma_chuc_vu_ky_ket=4;
    UPDATE public.nhan_vien SET ngay_vao_lam='2020-01-01',ma_chuc_vu=4;
    INSERT INTO public.hop_dong(ma_hop_dong,ma_nhan_vien,ngay_bat_dau,ngay_ket_thuc,trang_thai,luong_co_ban,ma_chuc_vu_ky_ket,ma_phong_ban_ky_ket)
    VALUES(4,1,'2020-01-01','2025-12-31','HET_HAN',25000000,1,1);
    INSERT INTO public.phu_luc_luong(ma_hop_dong,ngay_hieu_luc,luong_co_ban_moi,ly_do,nguoi_tao,ma_chuc_vu_moi,ma_phong_ban_moi)
    VALUES(2,'2026-12-01',25000000,'future promotion','fixture',1,1);
    UPDATE public.nhan_vien SET ma_chuc_vu=1 WHERE ma_nhan_vien=2;
    -- Changing the current profile is a projection of the dated appendix. It must
    -- neither conflict with the old GM in 2020-2025 nor mutate the observed baseline.
    IF (SELECT ma_chuc_vu FROM public.salary_position_baseline WHERE ma_nhan_vien=2)<>4 THEN
        RAISE EXCEPTION 'Observed baseline was mutated by profile synchronization';
    END IF;
END $$;
ROLLBACK;
-- Sequences are intentionally nontransactional; restore fixture-only unused state to test rollback.
SELECT setval('public.phu_luc_luong_ma_phu_luc_seq',1,false);
