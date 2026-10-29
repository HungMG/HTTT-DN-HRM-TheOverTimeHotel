-- ======================================================================================
-- MIGRATION: BẢNG NGÂN SÁCH LƯƠNG ĐƯỢC DUYỆT (TSK-24 / PRD 10.4)
-- Quản lý kế hoạch ngân sách quỹ lương theo năm, tháng và phòng ban được phê duyệt
-- GHI CHÚ AN TOÀN: Script này được chuẩn bị riêng để Ban Quản trị / DBA kiểm duyệt,
-- KHÔNG tự ý chạy trực tiếp lên database production mà không có xác nhận.
-- ======================================================================================

-- Chuẩn hóa: một ngân sách theo THÁNG và PHÒNG BAN; tổng khách sạn là tổng các phòng.
-- Trạng thái KHOA không cho chỉnh trên ứng dụng. Cần hai nhân viên khác nhau để lập và duyệt.
-- Nếu bảng đã tồn tại từ phiên bản thử nghiệm, DBA phải đối chiếu schema và dữ liệu;
-- CREATE TABLE IF NOT EXISTS không tự nâng cấp các ràng buộc cũ.
BEGIN;

CREATE TABLE IF NOT EXISTS public.ngan_sach_luong (
    ma_ngan_sach UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    version BIGINT NOT NULL DEFAULT 1 CHECK (version > 0),
    nam INT NOT NULL CHECK (nam >= 2000 AND nam <= 2200),
    thang INT NOT NULL CHECK (thang >= 1 AND thang <= 12),
    ma_phong_ban INT NOT NULL REFERENCES public.phong_ban(ma_phong_ban) ON DELETE RESTRICT,
    so_tien_ngan_sach DECIMAL(15, 2) NOT NULL CHECK (so_tien_ngan_sach >= 0),
    trang_thai VARCHAR(30) NOT NULL DEFAULT 'DRAFT' CHECK (trang_thai IN ('DRAFT', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA')),
    nguoi_tao INT NULL REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE SET NULL,
    ngay_tao TIMESTAMPTZ NOT NULL DEFAULT now(),
    nguoi_duyet INT NULL REFERENCES public.nhan_vien(ma_nhan_vien) ON DELETE SET NULL,
    ngay_duyet TIMESTAMPTZ NULL,
    ghi_chu TEXT NULL CHECK (char_length(ghi_chu) <= 2000),
    CONSTRAINT ck_ngan_sach_luong_approval CHECK (trang_thai NOT IN ('DA_DUYET', 'KHOA')
        OR (nguoi_tao IS NOT NULL AND nguoi_duyet IS NOT NULL AND ngay_duyet IS NOT NULL AND nguoi_tao <> nguoi_duyet))
);

-- Upgrade an earlier draft of this same table without changing business data.
-- Existing annual/whole-hotel rows or inconsistent approvals cause a rollback;
-- DBA must reconcile them explicitly rather than silently reallocating money.
ALTER TABLE public.ngan_sach_luong ALTER COLUMN thang SET NOT NULL;
ALTER TABLE public.ngan_sach_luong ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE public.ngan_sach_luong ALTER COLUMN ma_phong_ban SET NOT NULL;
ALTER TABLE public.ngan_sach_luong ALTER COLUMN ngay_tao SET DEFAULT now();
ALTER TABLE public.ngan_sach_luong DROP CONSTRAINT IF EXISTS ngan_sach_luong_trang_thai_check;
ALTER TABLE public.ngan_sach_luong ADD CONSTRAINT ngan_sach_luong_trang_thai_check
    CHECK (trang_thai IN ('DRAFT', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA'));
ALTER TABLE public.ngan_sach_luong DROP CONSTRAINT IF EXISTS ngan_sach_luong_so_tien_ngan_sach_check;
ALTER TABLE public.ngan_sach_luong ADD CONSTRAINT ngan_sach_luong_so_tien_ngan_sach_check CHECK (so_tien_ngan_sach >= 0);
ALTER TABLE public.ngan_sach_luong DROP CONSTRAINT IF EXISTS ngan_sach_luong_nam_check;
ALTER TABLE public.ngan_sach_luong ADD CONSTRAINT ngan_sach_luong_nam_check CHECK (nam >= 2000 AND nam <= 2200);
ALTER TABLE public.ngan_sach_luong DROP CONSTRAINT IF EXISTS ck_ngan_sach_luong_approval;
ALTER TABLE public.ngan_sach_luong ADD CONSTRAINT ck_ngan_sach_luong_approval CHECK (trang_thai NOT IN ('DA_DUYET', 'KHOA')
    OR (nguoi_tao IS NOT NULL AND nguoi_duyet IS NOT NULL AND ngay_duyet IS NOT NULL AND nguoi_tao <> nguoi_duyet));

-- Ràng buộc duy nhất: Một phòng ban trong một kỳ (năm + tháng hoặc cả năm) chỉ có 1 bản ghi ngân sách
CREATE UNIQUE INDEX IF NOT EXISTS uq_ngan_sach_luong_ky_phongban
    ON public.ngan_sach_luong (nam, COALESCE(thang, 0), COALESCE(ma_phong_ban, 0));

-- Chỉ mục hỗ trợ truy vấn lọc theo năm, phòng ban, trạng thái
CREATE INDEX IF NOT EXISTS idx_ngan_sach_luong_nam_thang
    ON public.ngan_sach_luong(nam, thang);
CREATE INDEX IF NOT EXISTS idx_ngan_sach_luong_phong_ban
    ON public.ngan_sach_luong(ma_phong_ban);
CREATE INDEX IF NOT EXISTS idx_ngan_sach_luong_trang_thai
    ON public.ngan_sach_luong(trang_thai);

-- Kích hoạt RLS theo chuẩn dự án: Chặn truy cập trực tiếp từ client anon/authenticated
ALTER TABLE public.ngan_sach_luong ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format('REVOKE ALL ON public.ngan_sach_luong FROM %I', role_name);
        END IF;
    END LOOP;
END $$;

COMMIT;
