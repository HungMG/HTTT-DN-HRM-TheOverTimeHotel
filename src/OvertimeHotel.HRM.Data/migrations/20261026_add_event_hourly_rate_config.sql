-- ====================================================================
-- MIGRATION: 20261026_add_event_hourly_rate_config.sql
-- Mục đích: Thêm dòng cấu hình hệ số thưởng sự kiện theo giờ vào
--           cau_hinh_khoan_luong (ma_khoan = 35).
--
-- Bối cảnh: từ 26/10/2026 tiền sự kiện tính theo giờ:
--   Tiền sự kiện = Lương tháng / 26 / 8 × Số giờ tham gia × Hệ số
--   Hệ số trước đây là hằng số 2.0 trong SalaryConstants; nay HR/Admin
--   sửa được tại màn hình Cấu hình lương & Phụ cấp → Thưởng.
--   Code đọc dòng này qua EventRateConfig.ReadAsync; nếu thiếu dòng hoặc
--   giá trị không hợp lệ thì fallback về SalaryConstants.EVENT_HOURLY_RATE.
--
-- Thứ tự khi dựng database mới:
--   ... → 20261016_seed_event_bonus_configuration.sql
--       → 20261026_add_event_hourly_rate_config.sql  (file này)
--       → 20261026_remove_legacy_event_bonus_matrix.sql
--
-- Chạy lại được (idempotent): đã có dòng 35 thì giữ nguyên giá trị HR đã
-- sửa, chỉ đồng bộ lại tên/loại/mô tả nếu cần.
-- ====================================================================
BEGIN;

SELECT pg_advisory_xact_lock(2026102603);
LOCK TABLE public.cau_hinh_khoan_luong IN SHARE ROW EXCLUSIVE MODE;

INSERT INTO public.cau_hinh_khoan_luong (ma_khoan, ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta)
VALUES (35, 'Hệ số thưởng sự kiện theo giờ', 'THUONG', 2.00,
        'Hệ số nhân đơn giá giờ sự kiện: Lương tháng / 26 / 8 × số giờ × hệ số này')
ON CONFLICT (ma_khoan) DO UPDATE
SET loai_khoan = 'THUONG',
    mo_ta = EXCLUDED.mo_ta;

-- Đồng bộ sequence để các dòng thêm mới sau này không đụng ma_khoan 35.
SELECT setval(
    pg_get_serial_sequence('public.cau_hinh_khoan_luong', 'ma_khoan'),
    GREATEST((SELECT MAX(ma_khoan) FROM public.cau_hinh_khoan_luong), 35),
    true
);

COMMIT;
