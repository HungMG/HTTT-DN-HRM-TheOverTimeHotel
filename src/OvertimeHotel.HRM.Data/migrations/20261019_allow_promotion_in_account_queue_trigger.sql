-- Migration: Update trigger chan_yeu_cau_tai_khoan_trung to allow promotion requests
-- Context: Promotion requests ([THANG_CHUC]) are submitted for employees who already have an account.

CREATE OR REPLACE FUNCTION public.chan_yeu_cau_tai_khoan_trung()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    -- Bypass cho seed hoặc insert có sẵn ID đã tồn tại
    IF TG_OP = 'INSERT' AND NEW.ma_yeu_cau IS NOT NULL AND EXISTS (
        SELECT 1 FROM public.yeu_cau_tai_khoan WHERE ma_yeu_cau = NEW.ma_yeu_cau
    ) THEN
        RETURN NEW;
    END IF;

    -- Nếu là yêu cầu thăng chức ([THANG_CHUC]), nhân viên PHẢI có tài khoản sẵn
    IF NEW.ghi_chu_tu_choi IS NOT NULL AND NEW.ghi_chu_tu_choi LIKE '[THANG_CHUC]%' THEN
        IF TG_OP = 'INSERT' THEN
            IF NOT EXISTS (
                SELECT 1 FROM public.tai_khoan t
                 WHERE t.ma_nhan_vien = NEW.ma_nhan_vien
            ) THEN
                RAISE EXCEPTION
                    'Nhân viên #% chưa có tài khoản, không thể đề xuất thăng chức.',
                    NEW.ma_nhan_vien
                    USING ERRCODE = 'integrity_constraint_violation';
            END IF;

            IF EXISTS (
                SELECT 1 FROM public.yeu_cau_tai_khoan y
                 WHERE y.ma_nhan_vien = NEW.ma_nhan_vien
                   AND y.trang_thai IN ('CHO_IT', 'DANG_XU_LY')
                   AND y.ma_yeu_cau IS DISTINCT FROM NEW.ma_yeu_cau
            ) THEN
                RAISE EXCEPTION
                    'Nhân viên #% đang có yêu cầu chờ hoặc đang xử lý.',
                    NEW.ma_nhan_vien
                    USING ERRCODE = 'integrity_constraint_violation';
            END IF;
        END IF;

        RETURN NEW;
    END IF;

    -- Rào chắn 1: tên đăng nhập đã thuộc về một tài khoản đang tồn tại.
    IF EXISTS (
        SELECT 1 FROM public.tai_khoan t
         WHERE lower(t.ten_dang_nhap) = lower(NEW.ten_dang_nhap)
           AND t.ma_tai_khoan IS DISTINCT FROM NEW.ma_tai_khoan
    ) THEN
        RAISE EXCEPTION
            'Tên đăng nhập "%" đã thuộc về một tài khoản đang tồn tại. '
            'Nhân viên đã có tài khoản rồi thì không cần gửi yêu cầu cấp mới.',
            NEW.ten_dang_nhap
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    -- Rào chắn 2: chính nhân viên này đã có tài khoản (kể cả khi tên đăng nhập
    -- khác — có thể do cấp lại hoặc đổi tên). Cùng lý do: yêu cầu thừa.
    IF EXISTS (
        SELECT 1 FROM public.tai_khoan t
         WHERE t.ma_nhan_vien = NEW.ma_nhan_vien
           AND t.ma_tai_khoan IS DISTINCT FROM NEW.ma_tai_khoan
    ) THEN
        RAISE EXCEPTION
            'Nhân viên #% đã có tài khoản rồi, không thể gửi yêu cầu cấp tài khoản mới. '
            'Xem TAI_KHOAN của nhân viên trước khi tạo yêu cầu.',
            NEW.ma_nhan_vien
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    RETURN NEW;
END;
$function$;
