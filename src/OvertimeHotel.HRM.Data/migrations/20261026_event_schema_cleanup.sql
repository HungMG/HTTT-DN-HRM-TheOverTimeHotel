-- Run with the matching web release stopped. Keeps all five event tables and all historical payroll columns.
-- Removed values are archived in nhat_ky_quan_tri; rollback is in 20261026_event_schema_cleanup.rollback.sql.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);
LOCK TABLE public.du_an,public.nhu_cau_su_kien,public.cong_su_kien IN ACCESS EXCLUSIVE MODE;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.nhu_cau_su_kien GROUP BY ma_du_an,ma_phong_ban,vai_tro HAVING count(*)>1) THEN
        RAISE EXCEPTION 'Duplicate staffing roles must be reconciled before converting to whole-event needs.';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='nhu_cau_su_kien' AND column_name='bat_dau') THEN
        IF EXISTS (SELECT 1 FROM public.nhu_cau_su_kien n JOIN public.du_an d USING(ma_du_an)
                   WHERE n.bat_dau<>d.ngay_bat_dau OR n.ket_thuc<>d.ngay_ket_thuc) THEN
            RAISE EXCEPTION 'Staffing windows differ from event times; reconcile them before removing window columns.';
        END IF;
        INSERT INTO public.nhat_ky_quan_tri(ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,hanh_dong,noi_dung,gia_tri_cu,gia_tri_moi,ly_do)
        SELECT 'Hệ thống','SK-'||n.ma_du_an,'TINH_GON_CAU_TRUC_SU_KIEN','Lưu tên và giờ nhu cầu trước khi áp dụng cho toàn sự kiện.',
               jsonb_build_object('table','nhu_cau_su_kien','id',n.ma_nhu_cau,'values',
                   jsonb_build_object('ten_khung',n.ten_khung,'bat_dau',n.bat_dau,'ket_thuc',n.ket_thuc))::text,
               'migration=20261026_event_schema_cleanup','Giữ căn cứ cũ trước khi bỏ khung giờ riêng.'
        FROM public.nhu_cau_su_kien n;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='du_an' AND column_name='ma_dia_diem') THEN
        INSERT INTO public.nhat_ky_quan_tri(ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,hanh_dong,noi_dung,gia_tri_cu,gia_tri_moi,ly_do)
        SELECT 'Hệ thống','SK-'||d.ma_du_an,'TINH_GON_CAU_TRUC_SU_KIEN','Lưu địa điểm và giờ đệm lịch sử trước khi bỏ cột.',
               jsonb_build_object('table','du_an','id',d.ma_du_an,'values',
                   jsonb_build_object('ma_dia_diem',d.ma_dia_diem,'phut_chuan_bi',d.phut_chuan_bi,'phut_thu_don',d.phut_thu_don),
                   'venue',jsonb_build_object('ten_dia_diem',v.ten_dia_diem,'suc_chua',v.suc_chua))::text,
               'migration=20261026_event_schema_cleanup','Địa điểm hiện tại cố định tại khách sạn, không tính giờ chuẩn bị/thu dọn.'
        FROM public.du_an d LEFT JOIN public.dia_diem_su_kien v ON v.ma_dia_diem=d.ma_dia_diem;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='cong_su_kien' AND column_name='giai_trinh') THEN
        INSERT INTO public.nhat_ky_quan_tri(ten_nguoi_thuc_hien,ten_tai_khoan_bi_tac_dong,hanh_dong,noi_dung,gia_tri_cu,gia_tri_moi,ly_do)
        SELECT 'Hệ thống','SK-'||p.ma_du_an,'TINH_GON_CAU_TRUC_SU_KIEN','Lưu giải trình và xác nhận ngoại lệ lịch sử trước khi bỏ cột.',
               jsonb_build_object('table','cong_su_kien','id',c.ma_cong,'values',
                   jsonb_build_object('giai_trinh',c.giai_trinh,'xac_nhan_ngoai_le',c.xac_nhan_ngoai_le))::text,
               'migration=20261026_event_schema_cleanup','Chính sách mới chỉ ghi nhận thời gian tham gia.'
        FROM public.cong_su_kien c JOIN public.phan_cong_du_an p USING(ma_phan_cong);
    END IF;
END $$;

ALTER TABLE public.nhu_cau_su_kien DROP COLUMN IF EXISTS ten_khung,
    DROP COLUMN IF EXISTS bat_dau,DROP COLUMN IF EXISTS ket_thuc;
ALTER TABLE public.du_an DROP COLUMN IF EXISTS ma_dia_diem,
    DROP COLUMN IF EXISTS phut_chuan_bi,DROP COLUMN IF EXISTS phut_thu_don;
ALTER TABLE public.cong_su_kien DROP COLUMN IF EXISTS giai_trinh,
    DROP COLUMN IF EXISTS xac_nhan_ngoai_le;
CREATE UNIQUE INDEX IF NOT EXISTS uq_event_need_role ON public.nhu_cau_su_kien(ma_du_an,ma_phong_ban,vai_tro);
COMMIT;
