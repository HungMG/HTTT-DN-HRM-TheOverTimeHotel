-- Stop the matching web release before rolling back. Restores archived values for surviving rows.
-- Needs created after cleanup inherit their event's current times; work created afterwards has no old exception data.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);
LOCK TABLE public.du_an,public.nhu_cau_su_kien,public.cong_su_kien IN ACCESS EXCLUSIVE MODE;
DROP INDEX IF EXISTS public.uq_event_need_role;
ALTER TABLE public.du_an
    ADD COLUMN IF NOT EXISTS ma_dia_diem integer REFERENCES public.dia_diem_su_kien ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS phut_chuan_bi integer NOT NULL DEFAULT 0 CHECK (phut_chuan_bi BETWEEN 0 AND 10080),
    ADD COLUMN IF NOT EXISTS phut_thu_don integer NOT NULL DEFAULT 0 CHECK (phut_thu_don BETWEEN 0 AND 10080);
ALTER TABLE public.nhu_cau_su_kien ADD COLUMN IF NOT EXISTS ten_khung varchar(100),
    ADD COLUMN IF NOT EXISTS bat_dau timestamptz,ADD COLUMN IF NOT EXISTS ket_thuc timestamptz;
ALTER TABLE public.cong_su_kien ADD COLUMN IF NOT EXISTS giai_trinh varchar(500),
    ADD COLUMN IF NOT EXISTS xac_nhan_ngoai_le boolean NOT NULL DEFAULT false;

WITH archive AS (
    SELECT DISTINCT ON ((gia_tri_cu::jsonb->>'id')::integer)
           (gia_tri_cu::jsonb->>'id')::integer AS id,gia_tri_cu::jsonb->'values' AS old
    FROM public.nhat_ky_quan_tri
    WHERE hanh_dong='TINH_GON_CAU_TRUC_SU_KIEN' AND gia_tri_moi='migration=20261026_event_schema_cleanup'
      AND gia_tri_cu::jsonb->>'table'='du_an'
    ORDER BY (gia_tri_cu::jsonb->>'id')::integer,ma_nhat_ky DESC
)
UPDATE public.du_an d SET ma_dia_diem=(a.old->>'ma_dia_diem')::integer,
    phut_chuan_bi=(a.old->>'phut_chuan_bi')::integer,phut_thu_don=(a.old->>'phut_thu_don')::integer
FROM archive a WHERE d.ma_du_an=a.id;

WITH archive AS (
    SELECT DISTINCT ON ((gia_tri_cu::jsonb->>'id')::integer)
           (gia_tri_cu::jsonb->>'id')::integer AS id,gia_tri_cu::jsonb->'values' AS old
    FROM public.nhat_ky_quan_tri
    WHERE hanh_dong='TINH_GON_CAU_TRUC_SU_KIEN' AND gia_tri_moi='migration=20261026_event_schema_cleanup'
      AND gia_tri_cu::jsonb->>'table'='nhu_cau_su_kien'
    ORDER BY (gia_tri_cu::jsonb->>'id')::integer,ma_nhat_ky DESC
)
UPDATE public.nhu_cau_su_kien n SET ten_khung=a.old->>'ten_khung',bat_dau=(a.old->>'bat_dau')::timestamptz,
    ket_thuc=(a.old->>'ket_thuc')::timestamptz FROM archive a WHERE n.ma_nhu_cau=a.id;
UPDATE public.nhu_cau_su_kien n SET ten_khung=COALESCE(n.ten_khung,'Phục vụ sự kiện'),
    bat_dau=COALESCE(n.bat_dau,d.ngay_bat_dau),ket_thuc=COALESCE(n.ket_thuc,d.ngay_ket_thuc)
FROM public.du_an d WHERE n.ma_du_an=d.ma_du_an;
ALTER TABLE public.nhu_cau_su_kien ALTER COLUMN ten_khung SET NOT NULL,
    ALTER COLUMN bat_dau SET NOT NULL,ALTER COLUMN ket_thuc SET NOT NULL;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.nhu_cau_su_kien'::regclass AND conname='nhu_cau_su_kien_ket_thuc_check') THEN
        ALTER TABLE public.nhu_cau_su_kien ADD CONSTRAINT nhu_cau_su_kien_ket_thuc_check CHECK(ket_thuc>bat_dau);
    END IF;
END $$;
WITH archive AS (
    SELECT DISTINCT ON ((gia_tri_cu::jsonb->>'id')::integer)
           (gia_tri_cu::jsonb->>'id')::integer AS id,gia_tri_cu::jsonb->'values' AS old
    FROM public.nhat_ky_quan_tri
    WHERE hanh_dong='TINH_GON_CAU_TRUC_SU_KIEN' AND gia_tri_moi='migration=20261026_event_schema_cleanup'
      AND gia_tri_cu::jsonb->>'table'='cong_su_kien'
    ORDER BY (gia_tri_cu::jsonb->>'id')::integer,ma_nhat_ky DESC
)
UPDATE public.cong_su_kien c SET giai_trinh=a.old->>'giai_trinh',
    xac_nhan_ngoai_le=(a.old->>'xac_nhan_ngoai_le')::boolean FROM archive a WHERE c.ma_cong=a.id;
COMMIT;
