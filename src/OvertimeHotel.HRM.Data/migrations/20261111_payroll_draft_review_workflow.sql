-- Migration: 20261111_payroll_draft_review_workflow.sql
-- Thêm các trường hỗ trợ quy trình phát hành phiếu lương nháp cho nhân viên đối soát trước khi trình Ban Giám Đốc

ALTER TABLE ky_luong
    ADD COLUMN IF NOT EXISTS ngay_phat_hanh_nhap TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS nguoi_phat_hanh_nhap VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS han_phan_hoi_nhap TIMESTAMPTZ NULL;

COMMENT ON COLUMN ky_luong.ngay_phat_hanh_nhap IS 'Thời điểm HR phát hành phiếu lương nháp cho nhân viên kiểm tra đối soát';
COMMENT ON COLUMN ky_luong.nguoi_phat_hanh_nhap IS 'Tên chuyên viên HR thực hiện phát hành phiếu lương nháp';
COMMENT ON COLUMN ky_luong.han_phan_hoi_nhap IS 'Hạn chót nhân viên đối soát và gửi giải trình nếu có sai lệch';

-- Cập nhật ràng buộc trạng thái kỳ lương bổ sung CHO_PHAN_HOI
ALTER TABLE ky_luong DROP CONSTRAINT IF EXISTS ky_luong_trang_thai_check;
ALTER TABLE ky_luong ADD CONSTRAINT ky_luong_trang_thai_check
    CHECK (trang_thai IN ('MO', 'NHAP', 'CHO_PHAN_HOI', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA_SO'));
