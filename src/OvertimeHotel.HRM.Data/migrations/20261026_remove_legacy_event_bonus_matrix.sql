-- ====================================================================
-- MIGRATION: 20261026_remove_legacy_event_bonus_matrix.sql
-- Mục đích: Xóa ma trận 27 định mức thưởng sự kiện (ma_khoan 8..34)
--           trong cau_hinh_khoan_luong.
--
-- Bối cảnh: sự kiện tạo mới trên Web tính thu nhập theo giờ:
--   Tiền sự kiện = Lương tháng / 26 / 8 × Số giờ tham gia × hệ số 2.0
--   (SalaryConstants.EVENT_HOURLY_RATE, áp dụng trong HourlyEventRules).
--   Ma trận [Loại dự án] × [Phòng ban] không còn được dùng để tính tiền.
--
-- An toàn dữ liệu:
--   * KHÔNG xóa bảng cau_hinh_khoan_luong.
--   * ma_khoan = 4 (thưởng chuyên cần) và ma_khoan = 5 (phí phục vụ)
--     không thuộc ma trận sự kiện, vẫn giữ nguyên.
--   * ma_khoan = 35 (hệ số thưởng sự kiện theo giờ) do migration
--     20261026_add_event_hourly_rate_config.sql nạp, không nằm trong
--     khoảng 8–34 nên không bị xóa.
--   * Các dòng chi_tiet_phieu_luong lịch sử giữ nguyên ten_khoan_luu,
--     so_tien và ghi_chu; chỉ đặt ma_khoan = NULL trước khi xóa để
--     không vi phạm khóa ngoại ON DELETE RESTRICT.
--   * Sao lưu trước khi chạy: /tmp/opencode/event_bonus_backup_20261026.sql
--
-- Chạy lại được (idempotent): lần hai không còn dòng nào khớp điều kiện.
-- ====================================================================
BEGIN;

-- 1. Gỡ liên kết khóa ngoại tới ma trận, giữ nguyên snapshot số tiền/tên khoản.
UPDATE public.chi_tiet_phieu_luong AS d
SET ma_khoan = NULL
FROM public.cau_hinh_khoan_luong AS c
WHERE d.ma_khoan = c.ma_khoan
  AND c.loai_khoan = 'THUONG'
  AND c.ma_khoan BETWEEN 8 AND 34;

-- 2. Xóa 27 định mức của ma trận cũ.
DELETE FROM public.cau_hinh_khoan_luong
WHERE loai_khoan = 'THUONG'
  AND ma_khoan BETWEEN 8 AND 34;

COMMIT;
