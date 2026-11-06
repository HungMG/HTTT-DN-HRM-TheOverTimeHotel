-- Migration: 20261106_remove_giam_sat_ca_position.sql
-- Loại bỏ chức danh "Giám Sát Ca", chỉ giữ cấp "Trưởng Bộ Phận" (Quản lý) và "Nhân Viên"

-- 1. Thêm chức vụ Nhân viên Bếp nếu chưa có
INSERT INTO public.chuc_vu (ma_chuc_vu, ma_phong_ban, ten_chuc_vu, mo_ta)
VALUES (13, 5, 'Nhân viên Bếp (Kitchen Staff)', 'Sơ chế nguyên liệu, hỗ trợ nấu nướng và vệ sinh khu vực bếp')
ON CONFLICT (ma_chuc_vu) DO UPDATE
SET ten_chuc_vu = EXCLUDED.ten_chuc_vu, ma_phong_ban = EXCLUDED.ma_phong_ban, mo_ta = EXCLUDED.mo_ta;

SELECT setval('chuc_vu_ma_chuc_vu_seq', GREATEST((SELECT COALESCE(MAX(ma_chuc_vu), 1) FROM public.chuc_vu), 13));

-- 2. Chuyển đổi nhân viên đang giữ chức vụ Giám Sát Ca (ID = 3) về chức vụ Nhân viên của bộ phận
UPDATE public.nhan_vien SET ma_chuc_vu = 9 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 7; -- An ninh -> Nhân viên An ninh
UPDATE public.nhan_vien SET ma_chuc_vu = 5 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 3; -- Buồng phòng -> Nhân viên Buồng phòng
UPDATE public.nhan_vien SET ma_chuc_vu = 7 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 4; -- Ẩm thực -> Nhân viên Phục vụ
UPDATE public.nhan_vien SET ma_chuc_vu = 13 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 5; -- Bếp -> Nhân viên Bếp
UPDATE public.nhan_vien SET ma_chuc_vu = 4 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 2; -- Tiền sảnh -> Nhân viên Lễ tân
UPDATE public.nhan_vien SET ma_chuc_vu = 8 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 6; -- Kỹ thuật -> Kỹ sư Tòa nhà
UPDATE public.nhan_vien SET ma_chuc_vu = 10 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 8; -- Nhân sự -> Chuyên viên Nhân sự & Tiền lương
UPDATE public.nhan_vien SET ma_chuc_vu = 11 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 9; -- CNTT -> Quản trị hệ thống CNTT
UPDATE public.nhan_vien SET ma_chuc_vu = 4 WHERE ma_chuc_vu = 3; -- Fallback nếu còn

-- 3. Cập nhật tài khoản của nhân sự từng giữ Giám Sát Ca: chuyển từ Manager (3) sang Employee (4)
UPDATE public.tai_khoan SET ma_vai_tro = 4
WHERE ma_vai_tro = 3 AND ma_nhan_vien IN (
    SELECT ma_nhan_vien FROM public.nhan_vien
    WHERE ma_chuc_vu IN (4, 5, 7, 8, 9, 10, 11, 13)
      AND ma_nhan_vien IN (9, 12, 2281, 2289, 2303)
);

-- 4. Dọn dẹp các bảng phụ thuộc nếu có tham chiếu ma_chuc_vu = 3
UPDATE public.dinh_bien_ngay SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;
UPDATE public.dinh_bien_ca SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;
UPDATE public.hop_dong SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;

-- 5. Xóa bỏ hoàn toàn chức vụ Giám Sát Ca (ID = 3)
DELETE FROM public.chuc_vu WHERE ma_chuc_vu = 3;
