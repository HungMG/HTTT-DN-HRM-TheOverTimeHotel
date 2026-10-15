-- Remove maintenance/upgrade event type and clean up related data
BEGIN;
SELECT pg_advisory_xact_lock(2026101601);

-- Remove assignments belonging to BAO_TRI_NANG_CAP
DELETE FROM public.phan_cong_du_an WHERE ma_du_an IN (SELECT ma_du_an FROM public.du_an WHERE loai_du_an = 'BAO_TRI_NANG_CAP');

-- Remove admin audit logs for these events
DELETE FROM public.nhat_ky_quan_tri WHERE ten_tai_khoan_bi_tac_dong IN (SELECT 'SK-' || ma_du_an FROM public.du_an WHERE loai_du_an = 'BAO_TRI_NANG_CAP');

-- Remove events
DELETE FROM public.du_an WHERE loai_du_an = 'BAO_TRI_NANG_CAP';

-- Restrict event types to the 5 standard types
ALTER TABLE public.du_an DROP CONSTRAINT IF EXISTS chk_event_loai_du_an;
ALTER TABLE public.du_an ADD CONSTRAINT chk_event_loai_du_an
    CHECK (loai_du_an IN ('TIEC_CUOI', 'HOI_NGHI_MICE', 'GALA_DINNER', 'FESTIVAL_AM_THUC', 'DOAN_VIP'));

COMMIT;
