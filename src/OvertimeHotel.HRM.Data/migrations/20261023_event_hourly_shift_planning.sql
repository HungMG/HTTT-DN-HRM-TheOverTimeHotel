-- Additive event operations. Run explicitly after the existing event/shift migrations.
-- Existing events remain on their original compensation policy.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);

CREATE TABLE IF NOT EXISTS public.dia_diem_su_kien (
    ma_dia_diem serial PRIMARY KEY,
    ten_dia_diem varchar(255) NOT NULL UNIQUE,
    suc_chua integer NOT NULL CHECK (suc_chua > 0),
    dang_su_dung boolean NOT NULL DEFAULT true
);
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS tinh_theo_gio boolean NOT NULL DEFAULT false;
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ma_dia_diem integer REFERENCES public.dia_diem_su_kien ON DELETE RESTRICT;
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS phut_chuan_bi integer NOT NULL DEFAULT 0 CHECK (phut_chuan_bi BETWEEN 0 AND 10080);
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS phut_thu_don integer NOT NULL DEFAULT 0 CHECK (phut_thu_don BETWEEN 0 AND 10080);
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ket_thuc_thuc_te timestamptz;
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ly_do_dung varchar(500);
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ma_nguoi_nghiem_thu integer REFERENCES public.nhan_vien ON DELETE RESTRICT;
ALTER TABLE public.du_an ADD COLUMN IF NOT EXISTS ngay_nghiem_thu timestamptz;
ALTER TABLE public.phan_ca ADD COLUMN IF NOT EXISTS da_cong_bo boolean NOT NULL DEFAULT false;
ALTER TABLE public.phan_ca DROP CONSTRAINT IF EXISTS phan_ca_trang_thai_check;
ALTER TABLE public.phan_ca ADD CONSTRAINT phan_ca_trang_thai_check
    CHECK (trang_thai IN ('DA_PHAN','HOAN_THANH','VANG_MAT','DA_HUY'));
ALTER TABLE public.loai_don ADD COLUMN IF NOT EXISTS la_nghi_phep boolean NOT NULL DEFAULT false;
-- Only known leave labels are mapped. Other/custom types require HR classification.
UPDATE public.loai_don SET la_nghi_phep=true
WHERE lower(trim(ten_loai_don)) IN ('nghỉ phép','nghỉ phép năm','nghỉ không lương','nghỉ ốm','nghỉ thai sản','nghỉ việc riêng');

CREATE TABLE IF NOT EXISTS public.cong_bo_lich_tuan (
    dau_tuan date PRIMARY KEY CHECK (extract(isodow FROM dau_tuan)=1),
    version bigint NOT NULL DEFAULT 1,
    ma_nguoi_cong_bo integer NOT NULL REFERENCES public.nhan_vien ON DELETE RESTRICT,
    ngay_cong_bo timestamptz NOT NULL,
    ly_do varchar(500)
);
CREATE TABLE IF NOT EXISTS public.nhu_cau_su_kien (
    ma_nhu_cau serial PRIMARY KEY,
    ma_du_an integer NOT NULL REFERENCES public.du_an ON DELETE CASCADE,
    ten_khung varchar(100) NOT NULL,
    ma_phong_ban varchar(30) NOT NULL,
    vai_tro varchar(100) NOT NULL,
    so_nguoi integer NOT NULL CHECK (so_nguoi >= 0),
    bat_dau timestamptz NOT NULL,
    ket_thuc timestamptz NOT NULL CHECK (ket_thuc > bat_dau)
);
CREATE INDEX IF NOT EXISTS ix_nhu_cau_event ON public.nhu_cau_su_kien(ma_du_an);
ALTER TABLE public.nhu_cau_su_kien ADD COLUMN IF NOT EXISTS la_mau boolean NOT NULL DEFAULT false;
ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS ma_phan_ca integer REFERENCES public.phan_ca ON DELETE RESTRICT;
ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS trang_thai_tham_gia varchar(30) NOT NULL DEFAULT 'DA_PHAN'
    CHECK (trang_thai_tham_gia IN ('DA_PHAN','HUY_PHAN_CONG','DA_KET_THUC'));
ALTER TABLE public.phan_cong_du_an ADD COLUMN IF NOT EXISTS ly_do_ket_thuc varchar(500);
-- Multiple non-overlapping participation segments are permitted; settlement is unique below.
DO $$ DECLARE item record;
BEGIN
    FOR item IN SELECT conname FROM pg_constraint
        WHERE conrelid='public.phan_cong_du_an'::regclass AND contype='u'
          AND pg_get_constraintdef(oid) IN ('UNIQUE (ma_du_an, ma_nhan_vien)','UNIQUE (ma_nhan_vien, ma_du_an)')
    LOOP EXECUTE format('ALTER TABLE public.phan_cong_du_an DROP CONSTRAINT %I',item.conname); END LOOP;
END $$;
DROP INDEX IF EXISTS public.uq_phan_cong_du_an_nhan_vien;
DROP INDEX IF EXISTS public."IX_phan_cong_du_an_ma_du_an_ma_nhan_vien";
CREATE INDEX IF NOT EXISTS ix_event_employee_segments ON public.phan_cong_du_an(ma_du_an,ma_nhan_vien);
CREATE TABLE IF NOT EXISTS public.cong_su_kien (
    ma_cong serial PRIMARY KEY,
    ma_phan_cong integer NOT NULL REFERENCES public.phan_cong_du_an ON DELETE RESTRICT,
    bat_dau timestamptz NOT NULL,
    ket_thuc timestamptz NOT NULL CHECK (ket_thuc > bat_dau),
    giai_trinh varchar(500),
    ma_nguoi_xac_nhan integer NOT NULL REFERENCES public.nhan_vien ON DELETE RESTRICT,
    ngay_xac_nhan timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_event_work_assignment ON public.cong_su_kien(ma_phan_cong);
ALTER TABLE public.cong_su_kien ADD COLUMN IF NOT EXISTS xac_nhan_ngoai_le boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS public.quyet_toan_su_kien (
    ma_quyet_toan serial PRIMARY KEY,
    ma_du_an integer NOT NULL REFERENCES public.du_an ON DELETE RESTRICT,
    ma_nhan_vien integer NOT NULL REFERENCES public.nhan_vien ON DELETE RESTRICT,
    thang_phat_sinh date NOT NULL CHECK (extract(day FROM thang_phat_sinh)=1),
    ma_ky_chi_tra integer NOT NULL REFERENCES public.ky_luong ON DELETE RESTRICT,
    so_phut numeric(18,6) NOT NULL CHECK (so_phut >= 0),
    so_tien numeric(15,2) NOT NULL CHECK (so_tien >= 0),
    can_cu_tinh text NOT NULL,
    ma_chi_tiet integer UNIQUE REFERENCES public.chi_tiet_phieu_luong ON DELETE SET NULL,
    ma_nguoi_duyet integer NOT NULL REFERENCES public.nhan_vien ON DELETE RESTRICT,
    ngay_duyet timestamptz NOT NULL,
    UNIQUE(ma_du_an,ma_nhan_vien,thang_phat_sinh)
);
CREATE INDEX IF NOT EXISTS ix_event_settlement_period ON public.quyet_toan_su_kien(ma_ky_chi_tra,ma_nhan_vien);

-- Every entry point (including ExecuteUpdate and old shift controllers) shares the event/payroll lock.
CREATE OR REPLACE FUNCTION public.event_operations_lock() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN PERFORM pg_advisory_xact_lock(2026101509); RETURN NULL; END $$;
DO $$ DECLARE tab text;
BEGIN
    FOREACH tab IN ARRAY ARRAY['phan_ca','don_tu','hop_dong','ky_luong','phieu_luong','chi_tiet_phieu_luong','cham_cong','loai_don','dinh_bien_ca','dinh_bien_ngay'] LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS event_operations_lock ON public.%I',tab);
        EXECUTE format('CREATE TRIGGER event_operations_lock BEFORE INSERT OR UPDATE OR DELETE ON public.%I FOR EACH STATEMENT EXECUTE FUNCTION public.event_operations_lock()',tab);
    END LOOP;
END $$;

CREATE OR REPLACE FUNCTION public.event_shift_changed() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE changed boolean; e record;
BEGIN
    changed := TG_OP='DELETE';
    IF TG_OP='UPDATE' THEN
        changed := (NEW.ma_nhan_vien,NEW.ma_ca,NEW.ngay_lam_viec,NEW.bat_dau_du_kien,NEW.ket_thuc_du_kien,NEW.trang_thai='DA_HUY')
          IS DISTINCT FROM (OLD.ma_nhan_vien,OLD.ma_ca,OLD.ngay_lam_viec,OLD.bat_dau_du_kien,OLD.ket_thuc_du_kien,OLD.trang_thai='DA_HUY');
    END IF;
    IF NOT changed THEN RETURN NEW; END IF;
    IF EXISTS (SELECT 1 FROM public.phan_cong_du_an p JOIN public.du_an d USING(ma_du_an)
        WHERE p.ma_phan_ca=OLD.ma_phan_ca AND p.trang_thai_tham_gia <> 'HUY_PHAN_CONG'
          AND (p.bat_dau <= now() OR d.trang_thai IN ('HOAN_THANH','CHO_NGHIEM_THU')
               OR EXISTS(SELECT 1 FROM public.cong_su_kien c WHERE c.ma_phan_cong=p.ma_phan_cong))) THEN
        RAISE EXCEPTION 'Ca đã gắn công sự kiện thực hiện; không được sửa/xóa làm mất căn cứ công.' USING ERRCODE='23514';
    END IF;
    FOR e IN SELECT DISTINCT d.ma_du_an FROM public.phan_cong_du_an p JOIN public.du_an d USING(ma_du_an)
        WHERE p.ma_phan_ca=OLD.ma_phan_ca AND p.trang_thai_tham_gia='DA_PHAN' AND d.trang_thai NOT IN ('HOAN_THANH','HUY') LOOP
        UPDATE public.phan_cong_du_an SET trang_thai_tham_gia='HUY_PHAN_CONG',ly_do_ket_thuc='Ca nguồn thay đổi; cần xác nhận phân công lại.'
          WHERE ma_phan_ca=OLD.ma_phan_ca AND ma_du_an=e.ma_du_an AND trang_thai_tham_gia='DA_PHAN';
        UPDATE public.du_an SET version=version+1 WHERE ma_du_an=e.ma_du_an;
        INSERT INTO public.nhat_ky_quan_tri(ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,hanh_dong,noi_dung,ly_do)
          VALUES('Hệ thống', 'SK-'||e.ma_du_an, 'DONG_BO_CA_SU_KIEN', 'Ca nguồn #'||OLD.ma_phan_ca||' thay đổi; giữ lịch sử và yêu cầu phân công lại.',
            COALESCE(NULLIF(current_setting('app.shift_change_reason',true),''),'Thay đổi lịch ca nguồn.'));
    END LOOP;
    IF TG_OP='DELETE' THEN
        -- Keep source shifts as cancelled records when event history references them.
        IF EXISTS(SELECT 1 FROM public.phan_cong_du_an WHERE ma_phan_ca=OLD.ma_phan_ca) THEN
            RAISE EXCEPTION 'Ca có lịch sử sự kiện; hãy hủy ca thay vì xóa.' USING ERRCODE='23514';
        END IF;
        RETURN OLD;
    END IF;
    NEW.da_cong_bo := false;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS event_shift_changed ON public.phan_ca;
CREATE TRIGGER event_shift_changed BEFORE UPDATE OR DELETE ON public.phan_ca FOR EACH ROW EXECUTE FUNCTION public.event_shift_changed();

-- New tables fail closed for browser/mobile clients; only the server connection manages them.
DO $$ DECLARE tab text; role_name text;
BEGIN
    FOREACH tab IN ARRAY ARRAY['dia_diem_su_kien','cong_bo_lich_tuan','nhu_cau_su_kien','cong_su_kien','quyet_toan_su_kien'] LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',tab);
        FOREACH role_name IN ARRAY ARRAY['anon','authenticated'] LOOP
            IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname=role_name) THEN
                EXECUTE format('REVOKE ALL ON public.%I FROM %I',tab,role_name);
            END IF;
        END LOOP;
    END LOOP;
END $$;
COMMIT;
