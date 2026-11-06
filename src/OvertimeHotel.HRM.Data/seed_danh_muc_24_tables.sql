-- ======================================================================================
-- HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG KHÁCH SẠN THE OVERTIME HOTEL
-- DỮ LIỆU SEED MẪU PHẦN DANH MỤC NỀN TẢNG (POSTGRESQL / SUPABASE)
-- Toàn hệ thống có 24 bảng chuẩn 3NF. File này nạp 9 bảng danh mục, phần còn
-- lại (nhân sự, chấm công, lương, RBAC) nằm ở seed_data_24_tables.sql.
-- Chạy TRƯỚC file kia: các bảng này là khoá ngoại mà file kia tham chiếu tới.
-- Chạy lại được nhiều lần: mọi INSERT đều có ON CONFLICT, không xoá dữ liệu cũ.
-- ======================================================================================

-- 1. NẠP PHONG_BAN
INSERT INTO PHONG_BAN (ma_phong_ban, ten_phong_ban, mo_ta) VALUES
(1, 'Ban Giám Đốc', 'Điều hành chiến lược toàn diện khách sạn The OverTime Hotel'),
(2, 'Bộ phận Tiền Sảnh (Front Office)', 'Đón tiếp, check-in/out, chăm sóc khách lưu trú 24/7'),
(3, 'Bộ phận Buồng Phòng (Housekeeping)', 'Vệ sinh phòng, giặt là, bảo đảm tiêu chuẩn buồng phòng cao cấp'),
(4, 'Bộ phận Ẩm Thực & Bếp (F&B)', 'Nhà hàng, quầy bar, phục vụ buffet sáng và yến tiệc sự kiện'),
(5, 'Bộ phận Kỹ Thuật & Bảo Trì (Engineering)', 'Vận hành hệ thống điện, nước, điều hòa, PCCC và thang máy 24/7'),
(6, 'Bộ phận An Ninh (Security)', 'Tuần tra an ninh, kiểm soát ra vào và quản lý camera giám sát'),
(7, 'Bộ phận Nhân Sự & Đào Tạo (HR)', 'Tuyển dụng, hợp đồng, chấm công, đào tạo và giải quyết chế độ'),
(8, 'Bộ phận Kế Toán & Tài Chính (Finance)', 'Quản trị dòng tiền, hạch toán chi phí, bảng lương và thuế')
ON CONFLICT (ma_phong_ban) DO UPDATE SET ten_phong_ban = EXCLUDED.ten_phong_ban;

SELECT setval('phong_ban_ma_phong_ban_seq', (SELECT MAX(ma_phong_ban) FROM PHONG_BAN));

-- 2. NẠP CHUC_VU
INSERT INTO CHUC_VU (ma_chuc_vu, ma_phong_ban, ten_chuc_vu, mo_ta) VALUES
(1, 1, 'Tổng Giám Đốc (General Manager)', 'Lãnh đạo tối cao, phê duyệt ngân sách và chính sách nhân sự'),
(2, NULL, 'Trưởng Bộ Phận (Department Head)', 'Quản lý vận hành phòng ban, duyệt lịch ca và duyệt phép'),
(4, 2, 'Nhân Viên Lễ Tân (Receptionist)', 'Tiếp đón khách, làm thủ tục buồng phòng, xử lý giao dịch'),
(5, 3, 'Nhân Viên Buồng Phòng (Room Attendant)', 'Làm vệ sinh phòng khách, thay drap giường, bổ sung minibar'),
(6, 4, 'Đầu Bếp Chính (Chef de Partie)', 'Chế biến món ăn theo thực đơn, bảo đảm vệ sinh ATTP'),
(7, 5, 'Nhân Viên Kỹ Thuật (Technician)', 'Trực kỹ thuật 24/7, khắc phục sự cố thiết bị khách sạn'),
(8, 6, 'Nhân Viên An Ninh (Security Officer)', 'Trực sảnh, bãi xe, tuần tra đêm và bảo đảm an toàn khách sạn'),
(9, 7, 'Chuyên Viên Nhân Sự (HR Specialist)', 'Quản trị hồ sơ nhân sự, lập bảng lương, bảo hiểm xã hội'),
(10, 8, 'Kế Toán Tiền Lương (Payroll Accountant)', 'Đối soát công, thẩm định phiếu lương, chi trả lương qua ngân hàng')
ON CONFLICT (ma_chuc_vu) DO UPDATE SET ten_chuc_vu = EXCLUDED.ten_chuc_vu, ma_phong_ban = EXCLUDED.ma_phong_ban;

SELECT setval('chuc_vu_ma_chuc_vu_seq', (SELECT MAX(ma_chuc_vu) FROM CHUC_VU));

-- 3. NẠP VAI_TRO
INSERT INTO VAI_TRO (ma_vai_tro, ten_vai_tro, mo_ta) VALUES
(1, 'ADMIN', 'Quản trị viên hệ thống - Toàn quyền Web Admin, quản trị tài khoản và sao lưu CSDL'),
(2, 'HR', 'Quản trị nhân sự - Quản lý nhân viên, hợp đồng, kỳ lương, duyệt đơn thôi việc'),
(3, 'MANAGER', 'Quản lý bộ phận - Lập lịch phân ca 24/7, duyệt đơn nghỉ phép/đổi ca nhân viên cấp dưới'),
(4, 'EMPLOYEE', 'Nhân viên khách sạn - Quẹt thẻ chấm công, nộp đơn trực tuyến, tra cứu phiếu lương cá nhân')
ON CONFLICT (ma_vai_tro) DO UPDATE SET ten_vai_tro = EXCLUDED.ten_vai_tro;

SELECT setval('vai_tro_ma_vai_tro_seq', (SELECT MAX(ma_vai_tro) FROM VAI_TRO));

-- 4. NẠP CA_LAM_VIEC
INSERT INTO CA_LAM_VIEC (ma_ca, ten_ca, gio_bat_dau, gio_ket_thuc, qua_dem) VALUES
(1, 'Ca sáng (S1)', '06:00:00', '14:00:00', FALSE),
(2, 'Ca chiều (S2)', '14:00:00', '22:00:00', FALSE),
(3, 'Ca đêm (S3)', '22:00:00', '06:00:00', TRUE)
ON CONFLICT (ma_ca) DO UPDATE SET qua_dem = EXCLUDED.qua_dem;

SELECT setval('ca_lam_viec_ma_ca_seq', (SELECT MAX(ma_ca) FROM CA_LAM_VIEC));

-- 5. NẠP LOAI_DON
INSERT INTO LOAI_DON (ma_loai_don, ten_loai_don, co_huong_luong, mo_ta) VALUES
(1, 'Nghỉ phép năm', TRUE, 'Nghỉ phép thường niên theo quy định HĐLĐ (hưởng 100% lương công)'),
(2, 'Nghỉ ốm đau', FALSE, 'Nghỉ theo chỉ định bác sĩ bệnh viện (hưởng trợ cấp BHXH chi trả)'),
(3, 'Nghỉ thai sản', FALSE, 'Nghỉ chế độ thai sản nữ nhân viên theo quy định Luật BHXH'),
(4, 'Đơn xin thôi việc', FALSE, 'Đơn thông báo chấm dứt hợp đồng lao động theo quy định pháp luật')
ON CONFLICT (ma_loai_don) DO UPDATE SET co_huong_luong = EXCLUDED.co_huong_luong;

SELECT setval('loai_don_ma_loai_don_seq', (SELECT MAX(ma_loai_don) FROM LOAI_DON));

-- 6. NẠP KY_LUONG
INSERT INTO KY_LUONG (ma_ky_luong, thang, nam, tu_ngay, den_ngay, trang_thai) VALUES
(1, 9, 2026, '2026-09-01', '2026-09-30', 'MO'),
(2, 10, 2026, '2026-10-01', '2026-10-31', 'MO')
ON CONFLICT (thang, nam) DO UPDATE SET trang_thai = EXCLUDED.trang_thai;

SELECT setval('ky_luong_ma_ky_luong_seq', (SELECT MAX(ma_ky_luong) FROM KY_LUONG));

-- 7. NẠP 3 BẢNG CẤU HÌNH ĐỘC LẬP
INSERT INTO CAU_HINH_TANG_CA (ma_loai_ot, ten_loai_ot, he_so, mo_ta) VALUES
(1, 'Làm thêm ngày thường (150%)', 1.50, 'Làm thêm ngoài giờ ca làm việc ngày thường theo Luật Lao động'),
(2, 'Làm thêm ngày nghỉ hàng tuần (200%)', 2.00, 'Làm thêm vào ngày nghỉ tuần của nhân sự khách sạn'),
(3, 'Làm thêm ngày Lễ, Tết (300%)', 3.00, 'Làm thêm vào ngày Lễ, Tết theo quy định Nhà nước'),
(4, 'Phụ cấp làm việc ca đêm (30%)', 0.30, 'Cộng thêm 30% lương cho ca làm việc từ 22h00 đến 06h00 sáng')
ON CONFLICT (ma_loai_ot) DO UPDATE SET he_so = EXCLUDED.he_so;

SELECT setval('cau_hinh_tang_ca_ma_loai_ot_seq', (SELECT MAX(ma_loai_ot) FROM CAU_HINH_TANG_CA));

INSERT INTO CAU_HINH_PHU_CAP (ma_phu_cap, ten_phu_cap, loai_tinh, muc_dinh_muc, chiu_thue_tncn, mo_ta) VALUES
(1, 'Phụ cấp ăn ca', 'THEO_NGAY_CONG', 30000.00, FALSE, 'Hỗ trợ suất ăn trưa/tối theo ngày công thực tế (miễn thuế TNCN)'),
(2, 'Phụ cấp trách nhiệm quản lý', 'CO_DINH_THANG', 1000000.00, TRUE, 'Dành cho cấp Giám sát và Trưởng bộ phận'),
(3, 'Phụ cấp điện thoại & liên lạc', 'CO_DINH_THANG', 300000.00, TRUE, 'Hỗ trợ cước liên lạc điều hành công việc khách sạn'),
(4, 'Phụ cấp độc hại buồng phòng', 'THEO_NGAY_CONG', 20000.00, FALSE, 'Phụ cấp tiếp xúc hóa chất tẩy rửa cho nhân viên HK')
ON CONFLICT (ma_phu_cap) DO UPDATE SET muc_dinh_muc = EXCLUDED.muc_dinh_muc;

SELECT setval('cau_hinh_phu_cap_ma_phu_cap_seq', (SELECT MAX(ma_phu_cap) FROM CAU_HINH_PHU_CAP));

INSERT INTO CAU_HINH_KHAU_TRU (ma_khoan_tru, ten_khoan_tru, loai_khoan_tru, ty_le_hoac_dinh_muc, mo_ta) VALUES
(1, 'Bảo hiểm xã hội (BHXH - 8%)', 'BAO_HIEM', 8.00, 'Trích nộp 8% lương cơ bản theo Luật BHXH'),
(2, 'Bảo hiểm y tế (BHYT - 1.5%)', 'BAO_HIEM', 1.50, 'Trích nộp 1.5% lương cơ bản theo Luật BHYT'),
(3, 'Bảo hiểm thất nghiệp (BHTN - 1%)', 'BAO_HIEM', 1.00, 'Trích nộp 1% lương cơ bản theo Luật Việc làm'),
(4, 'Thuế thu nhập cá nhân (Thuế TNCN)', 'THUE', 0.00, 'Khấu trừ thuế TNCN theo biểu thuế lũy tiến từng phần'),
(5, 'Phạt vi phạm đi trễ / về sớm', 'VI_PHAM', 2000.00, 'Định mức phạt 2.000 đ cho mỗi phút vi phạm ca làm việc'),
(6, 'Tạm ứng lương định kỳ', 'TAM_UNG', 0.00, 'Khấu trừ tiền nhân viên đã ứng trước trong kỳ lương')
ON CONFLICT (ma_khoan_tru) DO UPDATE SET ty_le_hoac_dinh_muc = EXCLUDED.ty_le_hoac_dinh_muc;

SELECT setval('cau_hinh_khau_tru_ma_khoan_tru_seq', (SELECT MAX(ma_khoan_tru) FROM CAU_HINH_KHAU_TRU));
