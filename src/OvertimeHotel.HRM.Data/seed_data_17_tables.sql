-- ======================================================================================
-- HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG KHÁCH SẠN THE OVERTIME HOTEL
-- SCRIPT NẠP DỮ LIỆU MẪU (SEED DATA) 17 BẢNG CHUẨN 3NF (TASK 11)
-- ======================================================================================

-- 1. NẠP BẢNG PHONG_BAN (8 Phòng ban khách sạn)
INSERT INTO PHONG_BAN (ma_phong_ban, ten_phong_ban, mo_ta) VALUES
(1, 'Ban Giám Đốc (BOD)', 'Quản trị chiến lược và điều hành toàn bộ khách sạn'),
(2, 'Bộ phận Tiền sảnh (FO - Front Office)', 'Đón tiếp khách, Check-in/Check-out, Dịch vụ hỗ trợ 24/7'),
(3, 'Bộ phận Buồng phòng (HK - Housekeeping)', 'Vệ sinh phòng khách sạn, giặt là, quản lý tiện nghi phòng'),
(4, 'Bộ phận Ẩm thực (F&B - Food & Beverage)', 'Phục vụ nhà hàng, quầy bar, phòng tiệc cưới hội nghị'),
(5, 'Bộ phận Bếp (Kitchen / Culinary)', 'Chế biến món ăn phục vụ bữa sáng buffet, trưa, tối'),
(6, 'Bộ phận Kỹ thuật (ENG - Engineering)', 'Bảo trì hệ thống điện lạnh, cấp thoát nước, thang máy, PCCC'),
(7, 'Bộ phận An ninh (Security)', 'Tuần tra bảo vệ tài sản, giám sát an ninh tòa nhà 24/7'),
(8, 'Bộ phận Nhân sự & Tài chính (HR & ACC)', 'Tuyển dụng, HĐLĐ, chấm công, tính lương và tài chính')
ON CONFLICT (ma_phong_ban) DO UPDATE 
SET ten_phong_ban = EXCLUDED.ten_phong_ban, mo_ta = EXCLUDED.mo_ta;

-- Cập nhật sequence của bảng phong_ban
SELECT setval('phong_ban_ma_phong_ban_seq', (SELECT MAX(ma_phong_ban) FROM PHONG_BAN));

-- 2. NẠP BẢNG CHUC_VU (10 Chức danh nghề nghiệp)
INSERT INTO CHUC_VU (ma_chuc_vu, ten_chuc_vu, mo_ta) VALUES
(1, 'Tổng Giám Đốc (GM)', 'Điều hành toàn diện các hoạt động khách sạn'),
(2, 'Trưởng Bộ Phận (Department Head)', 'Quản lý điều hành và phân công ca kíp bộ phận'),
(3, 'Giám Sát Ca (Shift Supervisor)', 'Giám sát chất lượng dịch vụ trong từng ca trực'),
(4, 'Nhân viên Lễ tân (Receptionist)', 'Tiếp nhận đặt phòng, làm thủ tục check-in/out'),
(5, 'Nhân viên Buồng phòng (Housekeeper)', 'Làm sạch và chuẩn bị phòng nghỉ đón khách'),
(6, 'Bếp Trưởng (Head Chef)', 'Quản lý bếp ăn và kiểm soát chất lượng món ăn'),
(7, 'Nhân viên Phục vụ (F&B Staff)', 'Phục vụ bàn tiệc, quầy bar và sự kiện'),
(8, 'Kỹ sư Tòa nhà (Technician)', 'Vận hành và sửa chữa sự cố kỹ thuật hạ tầng'),
(9, 'Nhân viên An ninh (Security Guard)', 'Trực cổng, tuần tra khuôn viên, phòng chống cháy nổ'),
(10, 'Chuyên viên Nhân sự & Tiền lương (HR & Payroll)', 'Quản lý hồ sơ, chấm công, tính lương và bảo hiểm')
ON CONFLICT (ma_chuc_vu) DO UPDATE 
SET ten_chuc_vu = EXCLUDED.ten_chuc_vu, mo_ta = EXCLUDED.mo_ta;

SELECT setval('chuc_vu_ma_chuc_vu_seq', (SELECT MAX(ma_chuc_vu) FROM CHUC_VU));

-- 3. NẠP BẢNG VAI_TRO (4 Vai trò RBAC)
INSERT INTO VAI_TRO (ma_vai_tro, ten_vai_tro, mo_ta) VALUES
(1, 'Admin', 'Quản trị viên hệ thống (Web Admin Portal: Tài khoản, Danh mục, Sao lưu)'),
(2, 'HR', 'Quản trị nhân sự (Web & App: Nhân viên, HĐLĐ, Tính lương, Duyệt thôi việc)'),
(3, 'Manager', 'Quản lý bộ phận (Mobile App: Xếp lịch ca 24/7, Duyệt đơn phép/đổi ca/OT)'),
(4, 'Employee', 'Nhân viên tự phục vụ (Mobile App: Quẹt thẻ chấm công, Nộp đơn, Xem lương)')
ON CONFLICT (ma_vai_tro) DO UPDATE 
SET ten_vai_tro = EXCLUDED.ten_vai_tro, mo_ta = EXCLUDED.mo_ta;

SELECT setval('vai_tro_ma_vai_tro_seq', (SELECT MAX(ma_vai_tro) FROM VAI_TRO));

-- 4. NẠP BẢNG QUYEN (16 Quyền chức năng)
INSERT INTO QUYEN (ma_quyen, ma_quyen_code, ten_quyen, mo_ta) VALUES
(1, 'SYS_ADMIN', 'Toàn quyền quản trị hệ thống', 'Truy cập mọi chức năng quản trị cấp cao'),
(2, 'QL_TAI_KHOAN', 'Quản lý tài khoản người dùng', 'Thêm, sửa, khóa tài khoản trên Web Admin'),
(3, 'QL_PHAN_QUYEN', 'Quản lý phân quyền vai trò', 'Gán quyền RBAC cho các nhóm vai trò'),
(4, 'QL_DANH_MUC', 'Quản trị danh mục hệ thống', 'Quản lý phòng ban, chức vụ, ca làm việc, khoản lương'),
(5, 'SAO_LUU_CSDL', 'Sao lưu & Phục hồi CSDL', 'Tạo snapshot và khôi phục cơ sở dữ liệu'),
(6, 'QL_NHAN_SU', 'Quản trị hồ sơ nhân sự', 'Thêm mới, cập nhật lý lịch và hồ sơ nhân viên'),
(7, 'QL_HOP_DONG', 'Quản lý hợp đồng lao động', 'Ký mới, gia hạn, theo dõi thời hạn HĐLĐ'),
(8, 'TINH_LUONG', 'Tính lương tháng tự động', 'Tổng hợp công, tính lương 2 tầng Gross -> Net'),
(9, 'XUAT_PHIEU_LUONG_PDF', 'Xuất phiếu lương QuestPDF', 'Tạo và in phiếu lương tháng định dạng PDF'),
(10, 'DUYET_THOI_VIEC', 'Phê duyệt đơn xin thôi việc', 'Xét duyệt quy trình chấm dứt HĐLĐ'),
(11, 'XEP_CA_LAM', 'Lập lịch phân ca làm việc 24/7', 'Xếp ca sáng, chiều, đêm cho nhân sự bộ phận'),
(12, 'DUYET_DON_NGHI', 'Phê duyệt đơn nghỉ phép', 'Duyệt đơn nghỉ phép năm, ốm đau, thai sản'),
(13, 'DUYET_TANG_CA_OT', 'Phê duyệt tăng ca làm thêm (OT)', 'Xác nhận số giờ làm thêm ngoài ca'),
(14, 'XEM_CHAM_CONG_BO_PHAN', 'Xem dữ liệu chấm công bộ phận', 'Theo dõi quẹt thẻ in/out của nhân viên cấp dưới'),
(15, 'CHAM_CONG_DI_DONG', 'Chấm công quẹt thẻ di động', 'Bấm nút Check-in / Check-out trên Mobile App'),
(16, 'NOP_DON_TU', 'Nộp đơn từ trực tuyến', 'Gửi đơn nghỉ phép, việc riêng, thôi việc trên App')
ON CONFLICT (ma_quyen) DO UPDATE 
SET ma_quyen_code = EXCLUDED.ma_quyen_code, ten_quyen = EXCLUDED.ten_quyen, mo_ta = EXCLUDED.mo_ta;

SELECT setval('quyen_ma_quyen_seq', (SELECT MAX(ma_quyen) FROM QUYEN));

-- 5. NẠP BẢNG VAI_TRO_QUYEN (Ánh xạ RBAC M:N)
INSERT INTO VAI_TRO_QUYEN (ma_vai_tro, ma_quyen) VALUES
-- Admin có toàn quyền hệ thống + danh mục + backup
(1, 1), (1, 2), (1, 3), (1, 4), (1, 5),
-- HR có quyền quản lý nhân sự, HĐLĐ, tính lương, xuất PDF, duyệt thôi việc
(2, 6), (2, 7), (2, 8), (2, 9), (2, 10), (2, 14), (2, 15), (2, 16),
-- Manager có quyền xếp ca, duyệt đơn nghỉ, duyệt OT, xem chấm công bộ phận
(3, 11), (3, 12), (3, 13), (3, 14), (3, 15), (3, 16),
-- Employee có quyền chấm công, nộp đơn từ
(4, 15), (4, 16)
ON CONFLICT (ma_vai_tro, ma_quyen) DO NOTHING;

-- 6. NẠP BẢNG CA_LAM_VIEC (3 Ca chuẩn 24/7)
INSERT INTO CA_LAM_VIEC (ma_ca, ten_ca, gio_bat_dau, gio_ket_thuc, qua_dem) VALUES
(1, 'Ca Sáng (Morning Shift)', '06:00:00', '14:00:00', FALSE),
(2, 'Ca Chiều (Afternoon Shift)', '14:00:00', '22:00:00', FALSE),
(3, 'Ca Đêm (Night Shift)', '22:00:00', '06:00:00', TRUE)
ON CONFLICT (ma_ca) DO UPDATE 
SET ten_ca = EXCLUDED.ten_ca, gio_bat_dau = EXCLUDED.gio_bat_dau, gio_ket_thuc = EXCLUDED.gio_ket_thuc, qua_dem = EXCLUDED.qua_dem;

SELECT setval('ca_lam_viec_ma_ca_seq', (SELECT MAX(ma_ca) FROM CA_LAM_VIEC));

-- 7. NẠP BẢNG LOAI_DON (5 Loại đơn từ)
INSERT INTO LOAI_DON (ma_loai_don, ten_loai_don, co_huong_luong, mo_ta) VALUES
(1, 'Nghỉ phép năm', TRUE, 'Nghỉ phép thường niên hưởng nguyên 100% lương theo chế độ'),
(2, 'Nghỉ ốm đau / BHXH', TRUE, 'Nghỉ theo chỉ định y tế, có giấy tờ BHXH chi trả'),
(3, 'Nghỉ thai sản', TRUE, 'Chế độ nghỉ thai sản dành cho lao động nữ (6 tháng)'),
(4, 'Nghỉ việc riêng không lương', FALSE, 'Nghỉ giải quyết việc gia đình, khấu trừ lương ngày công'),
(5, 'Đơn xin thôi việc', FALSE, 'Đơn nguyện vọng xin chấm dứt hợp đồng lao động trước hạn')
ON CONFLICT (ma_loai_don) DO UPDATE 
SET ten_loai_don = EXCLUDED.ten_loai_don, co_huong_luong = EXCLUDED.co_huong_luong, mo_ta = EXCLUDED.mo_ta;

SELECT setval('loai_don_ma_loai_don_seq', (SELECT MAX(ma_loai_don) FROM LOAI_DON));

-- 8. NẠP BẢNG CAU_HINH_KHOAN_LUONG (7 Khoản định mức thu chi)
INSERT INTO CAU_HINH_KHOAN_LUONG (ma_khoan, ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta) VALUES
(1, 'Phụ cấp ăn ca', 'PHU_CAP', 730000.00, 'Hỗ trợ ăn trưa/tối tại khách sạn (730.000 đ/tháng)'),
(2, 'Phụ cấp ca đêm (30%)', 'PHU_CAP', 0.00, 'Cộng thêm 30% lương theo số giờ làm ca đêm thực tế'),
(3, 'Phụ cấp trách nhiệm quản lý', 'PHU_CAP', 1500000.00, 'Phụ cấp điều hành dành cho cấp Trưởng bộ phận & Giám sát'),
(4, 'Tiền thưởng chuyên cần', 'THUONG', 500000.00, 'Thưởng nhân viên làm việc đủ công và không đi trễ/về sớm'),
(5, 'Tiền phí phục vụ (Service Charge)', 'THUONG', 1000000.00, 'Phí dịch vụ khách sạn chia đều hàng tháng cho nhân sự'),
(6, 'Khấu trừ BHXH, BHYT, BHTN (10.5%)', 'KHAU_TRU', 0.00, 'Trích nộp 10.5% lương cơ bản theo quy định bảo hiểm Nhà nước'),
(7, 'Phạt vi phạm đi trễ / về sớm', 'KHAU_TRU', 50000.00, 'Khấu trừ 50.000 đ cho mỗi lần vi phạm giờ giấc không phép')
ON CONFLICT (ma_khoan) DO UPDATE 
SET ten_khoan = EXCLUDED.ten_khoan, loai_khoan = EXCLUDED.loai_khoan, gia_tri_mac_dinh = EXCLUDED.gia_tri_mac_dinh, mo_ta = EXCLUDED.mo_ta;

SELECT setval('cau_hinh_khoan_luong_ma_khoan_seq', (SELECT MAX(ma_khoan) FROM CAU_HINH_KHOAN_LUONG));

-- 9. NẠP BẢNG KY_LUONG (Kỳ tháng 9 & tháng 10 năm 2026)
INSERT INTO KY_LUONG (ma_ky_luong, thang, nam, tu_ngay, den_ngay, trang_thai) VALUES
(1, 9, 2026, '2026-09-01', '2026-09-30', 'MO'),
(2, 10, 2026, '2026-10-01', '2026-10-31', 'MO')
ON CONFLICT (thang, nam) DO UPDATE 
SET tu_ngay = EXCLUDED.tu_ngay, den_ngay = EXCLUDED.den_ngay, trang_thai = EXCLUDED.trang_thai;

SELECT setval('ky_luong_ma_ky_luong_seq', (SELECT MAX(ma_ky_luong) FROM KY_LUONG));

-- 10. NẠP BẢNG NHAN_VIEN (20 Nhân viên mẫu - tách ho và ten)
INSERT INTO NHAN_VIEN (ma_nhan_vien, ma_phong_ban, ma_chuc_vu, ho, ten, ngay_sinh, gioi_tinh, dien_thoai, email, dia_chi, trinh_do, ngay_vao_lam, trang_thai) VALUES
(1, 1, 1, 'Nguyễn Đình', 'Cường', '1985-05-12', 'NAM', '0901234567', 'cuong.nguyen@overtimehotel.com', '123 Nguyễn Huệ, Q.1, TP.HCM', 'Thạc sĩ Quản trị Khách sạn', '2022-01-01', 'DANG_LAM'),
(2, 8, 10, 'Võ Huỳnh Minh', 'Sang', '1992-08-20', 'NAM', '0912345678', 'sang.vo@overtimehotel.com', '45 Lê Duẩn, Q.1, TP.HCM', 'Đại học Quản trị Nhân sự', '2024-03-01', 'DANG_LAM'),
(3, 2, 2, 'Nguyễn Hoàng', 'Long', '1990-11-15', 'NAM', '0923456789', 'long.nguyen@overtimehotel.com', '78 Hai Bà Trưng, Q.3, TP.HCM', 'Đại học Du lịch', '2023-06-15', 'DANG_LAM'),
(4, 2, 4, 'Châu Quốc', 'Bảo', '1996-03-25', 'NAM', '0934567890', 'bao.chau@overtimehotel.com', '12 Võ Văn Tần, Q.3, TP.HCM', 'Cao đẳng Lễ tân', '2025-01-01', 'DANG_LAM'),
(5, 3, 2, 'Trần Thị', 'Mai', '1988-09-10', 'NU', '0945678901', 'mai.tran@overtimehotel.com', '89 CMT8, Q.10, TP.HCM', 'Đại học Kinh tế', '2023-08-01', 'DANG_LAM'),
(6, 3, 5, 'Lê Văn', 'Hùng', '1997-04-18', 'NAM', '0956789012', 'hung.le@overtimehotel.com', '34 Hoàng Hoa Thám, Bình Thạnh', 'Trung cấp Nghề', '2025-02-01', 'DANG_LAM'),
(7, 3, 5, 'Phạm Thị', 'Lan', '1998-12-05', 'NU', '0967890123', 'lan.pham@overtimehotel.com', '56 Phan Đăng Lưu, Phú Nhuận', 'Trung cấp Nghề', '2025-02-15', 'DANG_LAM'),
(8, 4, 2, 'Vũ Minh', 'Tuấn', '1987-07-22', 'NAM', '0978901234', 'tuan.vu@overtimehotel.com', '102 Nguyễn Trãi, Q.5, TP.HCM', 'Đại học Du lịch', '2023-05-01', 'DANG_LAM'),
(9, 4, 3, 'Hoàng Thị', 'Thảo', '1994-01-30', 'NU', '0989012345', 'thao.hoang@overtimehotel.com', '215 Lý Thường Kiệt, Q.11', 'Cao đẳng F&B', '2024-10-01', 'DANG_LAM'),
(10, 4, 7, 'Đỗ Thành', 'Đạt', '2001-06-14', 'NAM', '0990123456', 'dat.do@overtimehotel.com', '77 Trường Chinh, Tân Bình', 'Trung cấp Phục vụ', '2026-08-01', 'DANG_LAM'),
(11, 5, 6, 'Bùi Văn', 'Nam', '1983-02-28', 'NAM', '0902345678', 'nam.bui@overtimehotel.com', '91 Nam Kỳ Khởi Nghĩa, Q.3', 'Chứng chỉ Bếp Trưởng Quốc tế', '2023-04-01', 'DANG_LAM'),
(12, 5, 3, 'Đặng Văn', 'Kiên', '1993-10-17', 'NAM', '0913456789', 'kien.dang@overtimehotel.com', '180 Ung Văn Khiêm, Bình Thạnh', 'Cao đẳng Chế biến Món ăn', '2024-11-01', 'DANG_LAM'),
(13, 5, 7, 'Ngô Thị', 'Bích', '1999-05-08', 'NU', '0924567890', 'bich.ngo@overtimehotel.com', '42 Đinh Bộ Lĩnh, Bình Thạnh', 'Trung cấp Bếp', '2025-03-01', 'DANG_LAM'),
(14, 6, 2, 'Trương Quốc', 'Huy', '1986-12-19', 'NAM', '0935678901', 'huy.truong@overtimehotel.com', '63 Sư Vạn Hạnh, Q.10', 'Kỹ sư Cơ điện Bách Khoa', '2023-07-01', 'DANG_LAM'),
(15, 6, 8, 'Đinh Văn', 'Phong', '1995-08-25', 'NAM', '0946789012', 'phong.dinh@overtimehotel.com', '118 Phạm Văn Đồng, Gò Vấp', 'Kỹ sư Điện lạnh', '2024-09-01', 'DANG_LAM'),
(16, 6, 8, 'Phan Hồng', 'Quân', '1997-03-11', 'NAM', '0957890123', 'quan.phan@overtimehotel.com', '29 Quang Trung, Gò Vấp', 'Cao đẳng Kỹ thuật', '2025-01-15', 'DANG_LAM'),
(17, 7, 2, 'Lâm Văn', 'Tiến', '1984-04-03', 'NAM', '0968901234', 'tien.lam@overtimehotel.com', '84 Bạch Đằng, Tân Bình', 'Sĩ quan An ninh giải ngũ', '2023-06-01', 'DANG_LAM'),
(18, 7, 9, 'Hà Trọng', 'Nghĩa', '1996-09-15', 'NAM', '0979012345', 'nghia.ha@overtimehotel.com', '52 Phan Huy Ích, Gò Vấp', 'Chứng chỉ Vệ sĩ Chuyên nghiệp', '2025-02-01', 'DANG_LAM'),
(19, 7, 9, 'Đoàn Văn', 'Thắng', '2000-11-20', 'NAM', '0980123456', 'thang.doan@overtimehotel.com', '135 Nguyễn Oanh, Gò Vấp', 'Bộ đội xuất ngũ', '2026-08-01', 'DANG_LAM'),
(20, 2, 4, 'Dương Thùy', 'Linh', '1998-07-07', 'NU', '0991234567', 'linh.duong@overtimehotel.com', '19 Bà Huyện Thanh Quan, Q.3', 'Đại học Ngoại ngữ', '2025-03-01', 'DANG_LAM')
ON CONFLICT (ma_nhan_vien) DO UPDATE 
SET ho = EXCLUDED.ho, ten = EXCLUDED.ten, dien_thoai = EXCLUDED.dien_thoai, email = EXCLUDED.email;

SELECT setval('nhan_vien_ma_nhan_vien_seq', (SELECT MAX(ma_nhan_vien) FROM NHAN_VIEN));

-- 11. NẠP BẢNG HOP_DONG (20 Hợp đồng lao động)
INSERT INTO HOP_DONG (ma_hop_dong, ma_nhan_vien, so_hop_dong, loai_hop_dong, ngay_bat_dau, ngay_ket_thuc, luong_co_ban, trang_thai) VALUES
(1, 1, 'HDLD-2022/001/OTH', 'HĐLĐ Không xác định thời hạn', '2022-01-01', NULL, 35000000.00, 'HIEU_LUC'),
(2, 2, 'HDLD-2024/002/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2024-03-01', '2027-03-01', 18000000.00, 'HIEU_LUC'),
(3, 3, 'HDLD-2023/003/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-06-15', '2026-06-15', 20000000.00, 'HIEU_LUC'),
(4, 4, 'HDLD-2025/004/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-01-01', '2026-01-01', 8500000.00, 'HIEU_LUC'),
(5, 5, 'HDLD-2023/005/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-08-01', '2026-08-01', 19000000.00, 'HIEU_LUC'),
(6, 6, 'HDLD-2025/006/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-02-01', '2026-02-01', 7500000.00, 'HIEU_LUC'),
(7, 7, 'HDLD-2025/007/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-02-15', '2026-02-15', 7500000.00, 'HIEU_LUC'),
(8, 8, 'HDLD-2023/008/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-05-01', '2026-05-01', 21000000.00, 'HIEU_LUC'),
(9, 9, 'HDLD-2024/009/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2024-10-01', '2025-10-01', 12000000.00, 'HIEU_LUC'),
(10, 10, 'HDTV-2026/010/OTH', 'Hợp đồng thử việc (2 tháng)', '2026-08-01', '2026-10-01', 7800000.00, 'HIEU_LUC'),
(11, 11, 'HDLD-2023/011/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-04-01', '2026-04-01', 25000000.00, 'HIEU_LUC'),
(12, 12, 'HDLD-2024/012/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2024-11-01', '2025-11-01', 13000000.00, 'HIEU_LUC'),
(13, 13, 'HDLD-2025/013/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-03-01', '2026-03-01', 8000000.00, 'HIEU_LUC'),
(14, 14, 'HDLD-2023/014/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-07-01', '2026-07-01', 20000000.00, 'HIEU_LUC'),
(15, 15, 'HDLD-2024/015/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2024-09-01', '2025-09-01', 10500000.00, 'HIEU_LUC'),
(16, 16, 'HDLD-2025/016/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-01-15', '2026-01-15', 10000000.00, 'HIEU_LUC'),
(17, 17, 'HDLD-2023/017/OTH', 'HĐLĐ Xác định thời hạn (3 năm)', '2023-06-01', '2026-06-01', 16000000.00, 'HIEU_LUC'),
(18, 18, 'HDLD-2025/018/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-02-01', '2026-02-01', 8000000.00, 'HIEU_LUC'),
(19, 19, 'HDTV-2026/019/OTH', 'Hợp đồng thử việc (2 tháng)', '2026-08-01', '2026-10-01', 8000000.00, 'HIEU_LUC'),
(20, 20, 'HDLD-2025/020/OTH', 'HĐLĐ Xác định thời hạn (1 năm)', '2025-03-01', '2026-03-01', 8500000.00, 'HIEU_LUC')
ON CONFLICT (so_hop_dong) DO UPDATE 
SET luong_co_ban = EXCLUDED.luong_co_ban, trang_thai = EXCLUDED.trang_thai;

SELECT setval('hop_dong_ma_hop_dong_seq', (SELECT MAX(ma_hop_dong) FROM HOP_DONG));

-- 12. NẠP BẢNG TAI_KHOAN (4 Tài khoản test đại diện)
-- Mật khẩu ban đầu được băm SHA-256:
-- admin / Admin@123 -> e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7
-- hr_sang / Hr@123 -> d2dcec9f7289f448fcf9f6e8c722961e61b60f8d540bbd633332431b57a7869c
-- mgr_long / Manager@123 -> e8392925a98c9c22795d1fc5d0dfee5b9a6943f6b768ec5a2a0c077e5ed119cf
-- emp_bao / Emp@123 -> ca35068eabfd1dc3ba09cbe6255036fb1e69e3e0b1e95a52f1494638bc759071
INSERT INTO TAI_KHOAN (ma_tai_khoan, ma_nhan_vien, ma_vai_tro, ten_dang_nhap, mat_khau_bam, trang_thai) VALUES
(1, 1, 1, 'admin', 'e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7', TRUE),
(2, 2, 2, 'hr_sang', 'd2dcec9f7289f448fcf9f6e8c722961e61b60f8d540bbd633332431b57a7869c', TRUE),
(3, 3, 3, 'mgr_long', 'e8392925a98c9c22795d1fc5d0dfee5b9a6943f6b768ec5a2a0c077e5ed119cf', TRUE),
(4, 4, 4, 'emp_bao', 'ca35068eabfd1dc3ba09cbe6255036fb1e69e3e0b1e95a52f1494638bc759071', TRUE)
ON CONFLICT (ten_dang_nhap) DO UPDATE 
SET mat_khau_bam = EXCLUDED.mat_khau_bam, ma_vai_tro = EXCLUDED.ma_vai_tro;

SELECT setval('tai_khoan_ma_tai_khoan_seq', (SELECT MAX(ma_tai_khoan) FROM TAI_KHOAN));

-- 13. NẠP BẢNG PHAN_CA (Lịch phân ca mẫu cho NV04 - Châu Quốc Bảo)
INSERT INTO PHAN_CA (ma_phan_ca, ma_nhan_vien, ma_ca, ngay_lam_viec, bat_dau_du_kien, ket_thuc_du_kien, trang_thai) VALUES
(1, 4, 1, '2026-09-15', '2026-09-15 06:00:00+07', '2026-09-15 14:00:00+07', 'HOAN_THANH'),
(2, 4, 1, '2026-09-16', '2026-09-16 06:00:00+07', '2026-09-16 14:00:00+07', 'HOAN_THANH'),
(3, 4, 2, '2026-09-17', '2026-09-17 14:00:00+07', '2026-09-17 22:00:00+07', 'HOAN_THANH'),
(4, 4, 3, '2026-09-18', '2026-09-18 22:00:00+07', '2026-09-19 06:00:00+07', 'HOAN_THANH')
ON CONFLICT (ma_phan_ca) DO UPDATE 
SET trang_thai = EXCLUDED.trang_thai;

SELECT setval('phan_ca_ma_phan_ca_seq', (SELECT MAX(ma_phan_ca) FROM PHAN_CA));

-- 14. NẠP BẢNG CHAM_CONG (4 Bản ghi quẹt thẻ in/out minh họa nghiệp vụ)
INSERT INTO CHAM_CONG (ma_cham_cong, ma_phan_ca, gio_check_in, gio_check_out, phut_di_tre, phut_ve_som, gio_tang_ca_ot, trang_thai) VALUES
-- Bản ghi 1: Đúng giờ
(1, 1, '2026-09-15 05:55:00+07', '2026-09-15 14:02:00+07', 0, 0, 0.00, 'DUNG_GIO'),
-- Bản ghi 2: Đi trễ 18 phút
(2, 2, '2026-09-16 06:18:00+07', '2026-09-16 14:00:00+07', 18, 0, 0.00, 'DI_TRE'),
-- Bản ghi 3: Tăng ca thêm 2.0 giờ OT
(3, 3, '2026-09-17 13:55:00+07', '2026-09-18 00:00:00+07', 0, 0, 2.00, 'DUNG_GIO'),
-- Bản ghi 4: Ca đêm (hưởng phụ cấp 30%)
(4, 4, '2026-09-18 21:58:00+07', '2026-09-19 06:05:00+07', 0, 0, 0.00, 'DUNG_GIO')
ON CONFLICT (ma_cham_cong) DO UPDATE 
SET gio_check_in = EXCLUDED.gio_check_in, gio_check_out = EXCLUDED.gio_check_out;

SELECT setval('cham_cong_ma_cham_cong_seq', (SELECT MAX(ma_cham_cong) FROM CHAM_CONG));

-- 15. NẠP BẢNG DON_TU (2 Đơn mẫu: 1 đã duyệt, 1 chờ duyệt)
INSERT INTO DON_TU (ma_don, ma_nhan_vien, ma_loai_don, ma_nguoi_duyet, tu_ngay, den_ngay, ly_do, ngay_gui, trang_thai, phan_hoi_duyet, ngay_duyet) VALUES
(1, 4, 1, 3, '2026-09-20', '2026-09-21', 'Em xin nghỉ phép 2 ngày đi việc gia đình đã báo trước ca', '2026-09-14 10:30:00+07', 'DA_DUYET', 'Đã sắp xếp bạn Linh trực thay, duyệt đơn cho em.', '2026-09-14 14:00:00+07'),
(2, 10, 4, 8, '2026-09-25', '2026-09-25', 'Xin nghỉ việc riêng 1 ngày không hưởng lương', '2026-09-16 08:00:00+07', 'CHO_DUYET', NULL, NULL)
ON CONFLICT (ma_don) DO UPDATE 
SET trang_thai = EXCLUDED.trang_thai, phan_hoi_duyet = EXCLUDED.phan_hoi_duyet;

SELECT setval('don_tu_ma_don_seq', (SELECT MAX(ma_don) FROM DON_TU));

-- 16. NẠP BẢNG PHIEU_LUONG (Phiếu lương mẫu Tháng 9/2026 cho NV04 - Châu Quốc Bảo)
INSERT INTO PHIEU_LUONG (
    ma_phieu_luong, ma_nhan_vien, ma_ky_luong, luong_co_ban, ngay_cong_thuc_te, 
    tien_luong_cong, tien_tang_ca, tong_phu_cap, tong_thuong, tong_khau_tru, thuc_nhan, trang_thai
) VALUES
(
    1, 4, 1, 8500000.00, 25.50,
    8336538.46, 122596.15, 1024230.77, 1500000.00, 942500.00, 10040865.38, 'DA_DUYET'
)
ON CONFLICT (ma_nhan_vien, ma_ky_luong) DO UPDATE 
SET thuc_nhan = EXCLUDED.thuc_nhan, trang_thai = EXCLUDED.trang_thai;

SELECT setval('phieu_luong_ma_phieu_luong_seq', (SELECT MAX(ma_phieu_luong) FROM PHIEU_LUONG));

-- 17. NẠP BẢNG CHI_TIET_PHIEU_LUONG (Bản chụp từng dòng thu chi cho Phiếu lương 1)
INSERT INTO CHI_TIET_PHIEU_LUONG (ma_chi_tiet, ma_phieu_luong, ma_khoan, ten_khoan_luu, loai_khoan_luu, so_tien, ghi_chu) VALUES
(1, 1, 1, 'Phụ cấp ăn ca', 'PHU_CAP', 730000.00, 'Định mức ăn trưa tại khách sạn'),
(2, 1, 2, 'Phụ cấp ca đêm (30%)', 'PHU_CAP', 294230.77, 'Cộng 30% cho 8 giờ làm việc ca đêm (22h-6h)'),
(3, 1, 4, 'Tiền thưởng chuyên cần', 'THUONG', 500000.00, 'Thưởng đạt tỷ lệ đi làm trên 95%'),
(4, 1, 5, 'Tiền phí phục vụ (Service Charge)', 'THUONG', 1000000.00, 'Chia đều quỹ phí phục vụ từ du khách'),
(5, 1, 6, 'Khấu trừ BHXH, BHYT, BHTN (10.5%)', 'KHAU_TRU', -892500.00, 'Trích nộp 10.5% mức lương cơ sở 8.500.000 đ'),
(6, 1, 7, 'Phạt vi phạm đi trễ / về sớm', 'KHAU_TRU', -50000.00, 'Phạt đi trễ 18 phút ngày 16/09/2026')
ON CONFLICT (ma_chi_tiet) DO UPDATE 
SET so_tien = EXCLUDED.so_tien;

SELECT setval('chi_tiet_phieu_luong_ma_chi_tiet_seq', (SELECT MAX(ma_chi_tiet) FROM CHI_TIET_PHIEU_LUONG));
