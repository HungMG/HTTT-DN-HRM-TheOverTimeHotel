-- =========================================================================
-- CHẤM DỨT HỢP ĐỒNG: lưu loại và lý do chấm dứt ngay trên HOP_DONG.
-- Thay cho Sửa/Xóa hợp đồng đã ký (bản in A4 là chứng từ gốc).
-- =========================================================================

ALTER TABLE hop_dong
    ADD COLUMN IF NOT EXISTS loai_cham_dut VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS ly_do_cham_dut VARCHAR(500) NULL;

-- Dữ liệu cũ đã chấm dứt nhưng chưa có loại.
UPDATE hop_dong
SET loai_cham_dut = 'KHAC',
    ly_do_cham_dut = COALESCE(ly_do_cham_dut, 'Dữ liệu cũ')
WHERE trang_thai IN ('DA_CHAM_DUT', 'CHAM_DUT') AND loai_cham_dut IS NULL;

ALTER TABLE hop_dong DROP CONSTRAINT IF EXISTS chk_hop_dong_cham_dut;
ALTER TABLE hop_dong ADD CONSTRAINT chk_hop_dong_cham_dut CHECK (
    (trang_thai NOT IN ('DA_CHAM_DUT', 'CHAM_DUT') OR loai_cham_dut IS NOT NULL)
    AND (loai_cham_dut IS NULL OR loai_cham_dut IN ('HET_HAN_KHONG_TAI_KY', 'NHAN_VIEN_XIN_NGHI', 'SA_THAI_VI_PHAM', 'KHAC'))
    AND (loai_cham_dut IS DISTINCT FROM 'KHAC' OR NULLIF(BTRIM(ly_do_cham_dut), '') IS NOT NULL)
);
