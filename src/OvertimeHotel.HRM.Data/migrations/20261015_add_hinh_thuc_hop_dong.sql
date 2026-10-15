-- =========================================================================
-- BỔ SUNG CỘT HÌNH THỨC HỢP ĐỒNG (THỬ VIỆC / CHÍNH THỨC) VÀO BẢNG HOP_DONG
-- =========================================================================

ALTER TABLE hop_dong 
ADD COLUMN IF NOT EXISTS hinh_thuc_hop_dong VARCHAR(50) NOT NULL DEFAULT 'CHINH_THUC';

-- Đồng bộ dữ liệu hiện có
UPDATE hop_dong 
SET hinh_thuc_hop_dong = CASE 
    WHEN so_hop_dong LIKE 'HDTV%' OR loai_hop_dong ILIKE '%thu_viec%' THEN 'THU_VIEC' 
    ELSE 'CHINH_THUC' 
END;
