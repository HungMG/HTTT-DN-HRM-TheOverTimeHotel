-- Migration: Thêm cấu trúc giờ tăng ca (OT) vào bảng phan_ca
-- Mục đích: Hỗ trợ phân công ca chuẩn kèm số giờ OT kéo dài và lý do tăng ca theo Điều 107 Bộ luật Lao động 2019
BEGIN;

ALTER TABLE public.phan_ca
    ADD COLUMN IF NOT EXISTS gio_tang_ca_du_kien numeric(4,2) NOT NULL DEFAULT 0.00,
    ADD COLUMN IF NOT EXISTS ghi_chu_ot varchar(255);

-- Ràng buộc kiểm tra số giờ tăng ca hợp lệ (0 <= gio_tang_ca_du_kien <= 4.0)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_phan_ca_gio_tang_ca_du_kien'
    ) THEN
        ALTER TABLE public.phan_ca
            ADD CONSTRAINT chk_phan_ca_gio_tang_ca_du_kien
            CHECK (gio_tang_ca_du_kien >= 0.00 AND gio_tang_ca_du_kien <= 4.00);
    END IF;
END $$;

COMMIT;
