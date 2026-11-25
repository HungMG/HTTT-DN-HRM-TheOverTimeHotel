-- Migration: Allow official employee/manager contracts and salary amendments to
-- negotiate custom salaries >= position band minimum and <= sitting GM salary
-- (falling back to TONG_GIAM_DOC band maximum when no active GM contract exists).
BEGIN;

ALTER TABLE public.hop_dong
    ADD COLUMN IF NOT EXISTS luong_ngoai_khung boolean NOT NULL DEFAULT false;

ALTER TABLE public.phu_luc_luong
    ADD COLUMN IF NOT EXISTS luong_ngoai_khung boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN public.hop_dong.luong_ngoai_khung IS
    'Cho phép thỏa thuận lương chính thức ngoài khung cấp bậc, từ tối thiểu khung đến tối đa bằng lương Tổng Giám Đốc.';
COMMENT ON COLUMN public.phu_luc_luong.luong_ngoai_khung IS
    'Phụ lục điều chỉnh lương áp dụng thỏa thuận ngoài khung (chỉ hợp đồng chính thức Nhân viên/Quản lý).';

COMMIT;
