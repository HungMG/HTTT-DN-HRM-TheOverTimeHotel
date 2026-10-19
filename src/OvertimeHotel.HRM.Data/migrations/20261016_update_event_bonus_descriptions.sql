-- Only update descriptions of the 27 event bonus configurations.
BEGIN;
SELECT pg_advisory_xact_lock(2026101602);
LOCK TABLE public.cau_hinh_khoan_luong IN SHARE ROW EXCLUSIVE MODE;

DO $$
BEGIN
    IF (SELECT count(*) FROM public.cau_hinh_khoan_luong
        WHERE loai_khoan = 'THUONG' AND loai_du_an IS NOT NULL AND ma_phong_ban IS NOT NULL) <> 27 THEN
        RAISE EXCEPTION 'Expected exactly 27 event bonus configurations; no descriptions changed.';
    END IF;
END $$;

UPDATE public.cau_hinh_khoan_luong
SET mo_ta = 'Nhân viên hoàn thành sự kiện'
WHERE loai_khoan = 'THUONG' AND loai_du_an IS NOT NULL AND ma_phong_ban IS NOT NULL
    AND mo_ta IS DISTINCT FROM 'Nhân viên hoàn thành sự kiện';

COMMIT;
