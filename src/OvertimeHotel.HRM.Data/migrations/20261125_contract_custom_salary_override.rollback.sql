BEGIN;

ALTER TABLE public.phu_luc_luong
    DROP COLUMN IF EXISTS luong_ngoai_khung;

ALTER TABLE public.hop_dong
    DROP COLUMN IF EXISTS luong_ngoai_khung;

COMMIT;
