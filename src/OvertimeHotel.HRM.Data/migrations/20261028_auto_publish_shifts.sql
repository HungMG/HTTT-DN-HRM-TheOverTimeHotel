-- Migration: Tự động công bố ca làm việc (Auto-publish shifts)
-- Mục đích: Loại bỏ cơ chế công bố lịch tuần thủ công gây cản trở điều phối nhân sự sự kiện.
-- Ca làm việc được tạo mới hoặc xếp lịch sẽ tự động có hiệu lực ngay (da_cong_bo = TRUE).

BEGIN;

-- 1. Cập nhật toàn bộ ca phân lịch hiện tại sang trạng thái đã công bố (da_cong_bo = TRUE)
UPDATE public.phan_ca
SET da_cong_bo = TRUE
WHERE da_cong_bo = FALSE;

-- 2. Đổi default của cột da_cong_bo thành TRUE
ALTER TABLE public.phan_ca
ALTER COLUMN da_cong_bo SET DEFAULT TRUE;

-- 3. Cập nhật trigger event_shift_changed để giữ nguyên da_cong_bo = TRUE khi ca thay đổi
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
    NEW.da_cong_bo := TRUE;
    RETURN NEW;
END $$;

COMMIT;
