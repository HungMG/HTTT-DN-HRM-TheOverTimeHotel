-- Phase 1 events: additive, rerunnable, preserving event and allowance history.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);

CREATE TABLE IF NOT EXISTS public.du_an (
    ma_du_an SERIAL PRIMARY KEY,
    ten_du_an VARCHAR(200) NOT NULL UNIQUE,
    loai_du_an VARCHAR(50) NOT NULL,
    dia_diem VARCHAR(255) NOT NULL,
    ngay_bat_dau TIMESTAMPTZ NOT NULL,
    ngay_ket_thuc TIMESTAMPTZ NOT NULL,
    ngan_sach_phu_cap NUMERIC(15,2) NOT NULL DEFAULT 0 CHECK (ngan_sach_phu_cap >= 0),
    ma_nguoi_quan_ly INT NOT NULL REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE RESTRICT,
    mo_ta TEXT,
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'LEN_KE_HOACH',
    ngay_tao TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS public.phan_cong_du_an (
    ma_phan_cong SERIAL PRIMARY KEY,
    ma_du_an INT NOT NULL REFERENCES public.du_an(ma_du_an) ON DELETE CASCADE,
    ma_nhan_vien INT NOT NULL REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE RESTRICT,
    vai_tro_du_an VARCHAR(100) NOT NULL,
    nhiem_vu_cu_the VARCHAR(255),
    so_tien_phu_cap NUMERIC(15,2) NOT NULL DEFAULT 0 CHECK (so_tien_phu_cap >= 0),
    danh_gia VARCHAR(30) NOT NULL DEFAULT 'CHUA_DANH_GIA',
    trang_thai_chi_tra VARCHAR(30) NOT NULL DEFAULT 'CHUA_CHI_TRA',
    ma_chi_tiet INT REFERENCES public.chi_tiet_phieu_luong(ma_chi_tiet) ON DELETE SET NULL,
    ngay_danh_gia TIMESTAMPTZ,
    ngay_phan_cong TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS so_khach_du_kien INT NOT NULL DEFAULT 0;
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ly_do_huy VARCHAR(500);
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE public.du_an DROP CONSTRAINT IF EXISTS du_an_trang_thai_check;
ALTER TABLE public.du_an ADD CONSTRAINT du_an_trang_thai_check
    CHECK (trang_thai IN ('LEN_KE_HOACH','DANG_DIEN_RA','CHO_NGHIEM_THU','HOAN_THANH','HUY'));

ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS phu_cap_du_kien NUMERIC(15,2);
ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS bat_dau TIMESTAMPTZ;
ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS ket_thuc TIMESTAMPTZ;
UPDATE public.phan_cong_du_an p SET
    phu_cap_du_kien = COALESCE(p.phu_cap_du_kien, p.so_tien_phu_cap),
    bat_dau = COALESCE(p.bat_dau, d.ngay_bat_dau),
    ket_thuc = COALESCE(p.ket_thuc, d.ngay_ket_thuc)
FROM public.du_an d WHERE d.ma_du_an = p.ma_du_an
    AND (p.phu_cap_du_kien IS NULL OR p.bat_dau IS NULL OR p.ket_thuc IS NULL);
ALTER TABLE public.phan_cong_du_an ALTER COLUMN phu_cap_du_kien SET DEFAULT 0;
ALTER TABLE public.phan_cong_du_an ALTER COLUMN phu_cap_du_kien SET NOT NULL;
ALTER TABLE public.phan_cong_du_an ALTER COLUMN bat_dau SET NOT NULL;
ALTER TABLE public.phan_cong_du_an ALTER COLUMN ket_thuc SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.du_an'::regclass AND conname='chk_event_capacity_version') THEN
        ALTER TABLE public.du_an ADD CONSTRAINT chk_event_capacity_version CHECK (so_khach_du_kien >= 0 AND version > 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.phan_cong_du_an'::regclass AND conname='chk_event_planned_allowance') THEN
        ALTER TABLE public.phan_cong_du_an ADD CONSTRAINT chk_event_planned_allowance CHECK (phu_cap_du_kien >= 0);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_phan_cong_du_an_nhan_vien ON public.phan_cong_du_an(ma_du_an, ma_nhan_vien);
CREATE INDEX IF NOT EXISTS idx_event_time ON public.du_an(ngay_bat_dau, ngay_ket_thuc);
CREATE INDEX IF NOT EXISTS idx_event_manager ON public.du_an(ma_nguoi_quan_ly);
CREATE INDEX IF NOT EXISTS idx_event_staff_time ON public.phan_cong_du_an(ma_nhan_vien, bat_dau, ket_thuc);

-- Phase 2 mobile authorization is intentionally absent: direct client access fails closed.
ALTER TABLE public.du_an ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.phan_cong_du_an ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.du_an, public.phan_cong_du_an FROM anon, authenticated;
COMMIT;
