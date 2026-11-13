-- Migration: 20261113_it_geofence_permission.sql
-- Thêm mã quyền CAU_HINH_VI_TRI_CHAM_CONG cho vai trò IT (ma_vai_tro = 5)
-- Quy tắc an ninh: Chỉ IT được phép cấu hình vị trí GPS / geofence, Admin tuyệt đối không can thiệp.

BEGIN;

-- 1. Thêm mã quyền vào bảng quyen
INSERT INTO public.quyen (ma_quyen_code, ten_quyen, mo_ta)
VALUES (
    'CAU_HINH_VI_TRI_CHAM_CONG',
    'Cấu hình vị trí chấm công',
    'Đặc quyền cấu hình toạ độ GPS, bán kính geofence và Wi-Fi chấm công (Chỉ IT được phép, Admin không can thiệp).'
)
ON CONFLICT (ma_quyen_code) DO NOTHING;

-- 2. Chỉ gán duy nhất cho vai trò IT (ma_vai_tro = 5), TUYỆT ĐỐI KHÔNG gán cho Admin (ma_vai_tro = 1)
INSERT INTO public.vai_tro_quyen (ma_vai_tro, ma_quyen)
SELECT v.ma_vai_tro, q.ma_quyen
FROM public.vai_tro v
CROSS JOIN public.quyen q
WHERE v.ten_vai_tro = 'IT' AND q.ma_quyen_code = 'CAU_HINH_VI_TRI_CHAM_CONG'
ON CONFLICT DO NOTHING;

-- 3. Cập nhật hàm RPC hrm_has_permission đồng bộ phía Supabase
CREATE OR REPLACE FUNCTION public.hrm_has_permission(p_code text) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.tai_khoan t JOIN public.vai_tro v USING(ma_vai_tro)
  WHERE t.ma_nhan_vien=public.nhan_vien_hien_tai() AND t.trang_thai
    AND CASE
     WHEN v.ten_vai_tro='IT' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU') THEN
      EXISTS (SELECT 1 FROM public.vai_tro ev JOIN public.vai_tro_quyen vq USING(ma_vai_tro)
       JOIN public.quyen q USING(ma_quyen) WHERE ev.ten_vai_tro='Employee' AND q.ma_quyen_code=p_code)
     ELSE ((v.ten_vai_tro='IT' AND p_code IN
       ('QL_TAI_KHOAN','QL_PHAN_QUYEN','CAP_TAI_KHOAN_MOBILE','SAO_LUU_CSDL','CAU_HINH_VI_TRI_CHAM_CONG'))
      OR (v.ten_vai_tro='Admin' AND p_code IN ('CHAM_CONG_DI_DONG','NOP_DON_TU'))
      OR EXISTS (SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen)
       WHERE vq.ma_vai_tro=t.ma_vai_tro AND q.ma_quyen_code=p_code))
     END
 )
$$;

-- 4. Bảng lưu trữ cấu hình toạ độ vị trí chấm công đồng bộ cho Web & Mobile
CREATE TABLE IF NOT EXISTS public.cau_hinh_cham_cong (
    id int PRIMARY KEY DEFAULT 1,
    ten_khach_san text NOT NULL DEFAULT 'The OverTime Hotel & Resort',
    dia_chi text NOT NULL DEFAULT '123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh',
    vi_do double precision NOT NULL DEFAULT 10.778848,
    kinh_do double precision NOT NULL DEFAULT 106.687421,
    ban_kinh_met double precision NOT NULL DEFAULT 250,
    wifi_ssid text NOT NULL DEFAULT 'OverTimeHotel_Staff',
    cap_nhat_luc timestamptz NOT NULL DEFAULT now(),
    nguoi_cap_nhat text NOT NULL DEFAULT 'IT'
);

INSERT INTO public.cau_hinh_cham_cong (id, ten_khach_san, dia_chi, vi_do, kinh_do, ban_kinh_met, wifi_ssid)
VALUES (1, 'The OverTime Hotel & Resort', '123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', 10.778848, 106.687421, 250, 'OverTimeHotel_Staff')
ON CONFLICT (id) DO NOTHING;

ALTER TABLE public.cau_hinh_cham_cong ENABLE ROW LEVEL SECURITY;

DO $$
BEGIN
    DROP POLICY IF EXISTS "cho_phep_doc_cau_hinh_cham_cong" ON public.cau_hinh_cham_cong;
    CREATE POLICY "cho_phep_doc_cau_hinh_cham_cong" ON public.cau_hinh_cham_cong
        FOR SELECT TO authenticated, anon USING (true);

    DROP POLICY IF EXISTS "chi_it_duoc_sua_cau_hinh_cham_cong" ON public.cau_hinh_cham_cong;
    CREATE POLICY "chi_it_duoc_sua_cau_hinh_cham_cong" ON public.cau_hinh_cham_cong
        FOR ALL TO authenticated USING (public.hrm_has_permission('CAU_HINH_VI_TRI_CHAM_CONG'));
END $$;

GRANT SELECT ON public.cau_hinh_cham_cong TO anon, authenticated, service_role;
GRANT INSERT, UPDATE, DELETE ON public.cau_hinh_cham_cong TO authenticated, service_role;

-- Ghi nhận lịch sử migration
CREATE TABLE IF NOT EXISTS public.hrm_mobile_migration_history (
    migration_id text PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO public.hrm_mobile_migration_history (migration_id)
VALUES ('20261113_it_geofence_permission')
ON CONFLICT (migration_id) DO NOTHING;

COMMIT;
