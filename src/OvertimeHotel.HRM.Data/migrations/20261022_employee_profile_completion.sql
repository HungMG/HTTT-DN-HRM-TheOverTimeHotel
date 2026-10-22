-- Apply manually before deploying the employee profile completion flow.
-- Existing employee records retain their completed status.
BEGIN;
ALTER TABLE nhan_vien ADD COLUMN IF NOT EXISTS is_profile_complete boolean NOT NULL DEFAULT true;
ALTER TABLE nhan_vien ALTER COLUMN ngay_sinh DROP NOT NULL;
ALTER TABLE nhan_vien ALTER COLUMN gioi_tinh DROP NOT NULL;
ALTER TABLE nhan_vien DROP CONSTRAINT IF EXISTS nhan_vien_gioi_tinh_check;
ALTER TABLE nhan_vien ADD CONSTRAINT nhan_vien_gioi_tinh_check CHECK (gioi_tinh IN ('NAM', 'NU', 'KHAC'));
COMMIT;
