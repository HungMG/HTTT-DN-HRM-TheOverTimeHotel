-- Add three creation types while retaining historical event and payroll classifications.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);

ALTER TABLE public.du_an
    DROP CONSTRAINT IF EXISTS du_an_loai_du_an_check,
    DROP CONSTRAINT IF EXISTS chk_event_loai_du_an;
ALTER TABLE public.du_an ADD CONSTRAINT chk_event_loai_du_an CHECK (loai_du_an IN (
    'TIEC_CUOI','HOI_NGHI_MICE','TIEC_SINH_NHAT','TIEC_TAT_NIEN','HOP_BAO',
    'GALA_DINNER','FESTIVAL_AM_THUC','DOAN_VIP'
));

ALTER TABLE public.cau_hinh_khoan_luong DROP CONSTRAINT IF EXISTS chk_event_bonus_configuration;
ALTER TABLE public.cau_hinh_khoan_luong ADD CONSTRAINT chk_event_bonus_configuration CHECK (
    (loai_du_an IS NULL AND ma_phong_ban IS NULL)
    OR (loai_du_an IS NOT NULL AND ma_phong_ban IS NOT NULL AND loai_khoan = 'THUONG'
        AND loai_du_an IN ('TIEC_CUOI','HOI_NGHI_MICE','TIEC_SINH_NHAT','TIEC_TAT_NIEN','HOP_BAO',
                          'GALA_DINNER','FESTIVAL_AM_THUC','DOAN_VIP')
        AND gia_tri_mac_dinh >= 0)
);

NOTIFY pgrst, 'reload schema';
COMMIT;
