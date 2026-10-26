-- Migration: Cập nhật dữ liệu mẫu số người phụ thuộc cho nhân viên
-- Phục vụ tính giảm trừ gia cảnh khi tính thuế TNCN (4.400.000 đ/người phụ thuộc/tháng)

UPDATE nhan_vien
SET so_nguoi_phu_thuoc = CASE ma_nhan_vien
    WHEN 1 THEN 2  -- Nguyễn Đình Cường (Tổng Giám đốc - Lương 35tr - 2 con)
    WHEN 2 THEN 1  -- Võ Huỳnh Minh Sang (Trưởng phòng Nhân sự - Lương 18tr - 1 con)
    WHEN 3 THEN 1  -- Nguyễn Hoàng Long (Trưởng phòng Lễ tân - Lương 20tr - 1 con)
    WHEN 4 THEN 0  -- Châu Quốc Bảo (Lễ tân - Lương 8.5tr)
    WHEN 5 THEN 2  -- Trần Thị Mai (Trưởng phòng Buồng phòng - Lương 19tr - 2 con)
    WHEN 6 THEN 0  -- Lê Văn Hùng (Buồng phòng - Lương 7.5tr)
    WHEN 7 THEN 1  -- Phạm Thị Lan (Buồng phòng - Lương 7.5tr - 1 con)
    WHEN 8 THEN 1  -- Vũ Minh Tuấn (Trưởng phòng F&B - Lương 21tr - 1 con)
    WHEN 9 THEN 0  -- Hoàng Thị Thảo (Giám sát F&B - Lương 12tr)
    WHEN 10 THEN 0 -- Đỗ Thành Đạt (Nhân viên phục vụ - Lương 7.8tr, Thử việc)
    WHEN 11 THEN 3 -- Bùi Văn Nam (Bếp trưởng - Lương 25tr - 2 con + 1 mẹ già)
    WHEN 12 THEN 1 -- Đặng Văn Kiên (Bếp phó - Lương 13tr - 1 con)
    WHEN 13 THEN 0 -- Ngô Thị Bích (Phụ bếp - Lương 8tr)
    WHEN 14 THEN 2 -- Trương Quốc Huy (Trưởng phòng Kỹ thuật - Lương 20tr - 2 con)
    WHEN 15 THEN 1 -- Đinh Văn Phong (Kỹ sư Điện lạnh - Lương 10.5tr - 1 con)
    WHEN 16 THEN 0 -- Phan Hồng Quân (Kỹ thuật - Lương 10tr)
    WHEN 17 THEN 2 -- Lâm Văn Tiến (Trưởng phòng An ninh - Lương 16tr - 2 con)
    WHEN 18 THEN 0 -- Hà Trọng Nghĩa (Nhân viên An ninh - Lương 8tr)
    WHEN 19 THEN 0 -- Đoàn Văn Thắng (Nhân viên An ninh - Lương 8tr, Thử việc)
    WHEN 20 THEN 0 -- Dương Thùy Linh (Lễ tân - Lương 8.5tr)
    ELSE 0
END
WHERE ma_nhan_vien BETWEEN 1 AND 20;
