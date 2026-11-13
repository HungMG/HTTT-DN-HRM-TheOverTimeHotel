-- Đơn nghỉ hết hạn vào đầu ngày nghỉ theo giờ Việt Nam. Đơn thôi việc có quy tắc riêng.
BEGIN;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.don_tu'::regclass
                   AND conname='don_tu_trang_thai_check' AND contype='c'
                   AND pg_get_constraintdef(oid) LIKE '%DA_HUY%') THEN
        RAISE EXCEPTION 'Ràng buộc trạng thái đơn từ khác dự kiến; cần kiểm tra trước khi áp dụng';
    END IF;
END $$;
ALTER TABLE public.don_tu DROP CONSTRAINT don_tu_trang_thai_check;
ALTER TABLE public.don_tu ADD CONSTRAINT don_tu_trang_thai_check
    CHECK (trang_thai IN ('CHO_DUYET','DA_DUYET','TU_CHOI','DA_HUY','HET_HAN'));

CREATE OR REPLACE FUNCTION public.hrm_guard_leave_deadline() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE today_vn date := (statement_timestamp() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
BEGIN
    IF TG_OP = 'INSERT' AND NEW.ma_loai_don BETWEEN 1 AND 4 THEN
        IF NEW.tu_ngay <= today_vn OR NEW.tu_ngay > today_vn + 30 OR NEW.den_ngay < NEW.tu_ngay THEN
            RAISE EXCEPTION 'Đơn nghỉ phải gửi trước ngày nghỉ và không quá 30 ngày' USING ERRCODE='23514';
        END IF;
    ELSIF TG_OP = 'UPDATE' AND OLD.ma_loai_don BETWEEN 1 AND 4
          AND OLD.trang_thai = 'CHO_DUYET' AND NEW.trang_thai IN ('DA_DUYET', 'TU_CHOI')
          AND OLD.tu_ngay <= today_vn THEN
        RAISE EXCEPTION 'Đơn nghỉ đã hết hạn, không thể xử lý' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER hrm_guard_leave_deadline BEFORE INSERT OR UPDATE ON public.don_tu
    FOR EACH ROW EXECUTE FUNCTION public.hrm_guard_leave_deadline();
COMMIT;
