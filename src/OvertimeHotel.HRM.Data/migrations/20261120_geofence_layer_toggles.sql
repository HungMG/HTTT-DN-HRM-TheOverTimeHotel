-- Migration: 20261120_geofence_layer_toggles.sql
-- Bổ sung công tắc bật/tắt độc lập từng lớp xác thực chấm công (GPS và Wi-Fi)
-- Ràng buộc toàn vẹn: luôn phải bật ít nhất một lớp xác thực (bat_gps OR bat_wifi).

BEGIN;

ALTER TABLE IF EXISTS public.cau_hinh_cham_cong
    ADD COLUMN IF NOT EXISTS bat_gps boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS bat_wifi boolean NOT NULL DEFAULT true;

UPDATE public.cau_hinh_cham_cong
SET bat_gps = true
WHERE NOT bat_gps AND NOT bat_wifi;

DO $$
BEGIN
    IF to_regclass('public.cau_hinh_cham_cong') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint
           WHERE conname = 'chk_cau_hinh_cham_cong_at_least_one_layer'
             AND conrelid = 'public.cau_hinh_cham_cong'::regclass
       ) THEN
        ALTER TABLE public.cau_hinh_cham_cong
            ADD CONSTRAINT chk_cau_hinh_cham_cong_at_least_one_layer
            CHECK (bat_gps OR bat_wifi);
    END IF;
END $$;

INSERT INTO public.hrm_mobile_migration_history (migration_id)
VALUES ('20261120_geofence_layer_toggles')
ON CONFLICT (migration_id) DO NOTHING;

COMMIT;
