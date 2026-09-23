# PRD 04: Thiết Kế Cơ Sở Dữ Liệu 17 Bảng Chuẩn 3NF (Supabase PostgreSQL)

## 1. Danh Mục Tổng Hợp 17 Bảng CSDL

Hệ thống CSDL HRM The OverTime Hotel được thiết kế đạt chuẩn hóa **dạng chuẩn 3 (3NF)** trên nền tảng Supabase Cloud (PostgreSQL Engine):

| STT | Tên Bảng | Ý Nghĩa Nghiệp Vụ Trong Khách Sạn | Khóa Chính (PK) | Khóa Ngoại (FK) |
|:---:|:---|:---|:---|:---|
| **1** | `phong_ban` | Danh mục các phòng ban, bộ phận khách sạn (FO, HK, F&B, Kỹ thuật...) | `ma_phong_ban` | — |
| **2** | `chuc_vu` | Danh mục vị trí công tác (Bếp trưởng, Lễ tân, Bellman, Trưởng ca...) | `ma_chuc_vu` | — |
| **3** | `nhan_vien` | Hồ sơ lý lịch, thông tin cá nhân, liên hệ, học vấn của nhân sự | `ma_nhan_vien` | `ma_phong_ban`, `ma_chuc_vu` |
| **4** | `hop_dong` | Hợp đồng lao động pháp lý, lưu thời hạn và mức lương cơ bản | `ma_hop_dong` | `ma_nhan_vien` |
| **5** | `tai_khoan` | Tài khoản đăng nhập hệ thống, mật khẩu băm bảo mật | `ma_tai_khoan` | `ma_nhan_vien` (1-1), `ma_vai_tro` |
| **6** | `vai_tro` | 4 nhóm vai trò chuẩn: Admin, HR, Manager, Employee | `ma_vai_tro` | — |
| **7** | `quyen` | Danh mục quyền truy cập các màn hình chức năng | `ma_quyen` | — |
| **8** | `vai_tro_quyen` | Bảng trung gian giải quyết quan hệ N - N giữa Vai trò và Quyền (RBAC) | `(ma_vai_tro, ma_quyen)` | `ma_vai_tro`, `ma_quyen` |
| **9** | `ca_lam_viec` | Danh mục 3 mẫu ca 24/7 (Sáng 6h-14h, Chiều 14h-22h, Đêm 22h-6h) | `ma_ca` | — |
| **10** | `phan_ca` | Bảng xếp lịch ca làm việc trước cho nhân viên theo tuần/tháng | `ma_phan_ca` | `ma_nhan_vien`, `ma_ca` |
| **11** | `cham_cong` | Bản ghi quẹt thẻ in/out thực tế, tự động tính phút trễ, sớm, giờ OT | `ma_cham_cong` | `ma_nhan_vien`, `ma_ca` |
| **12** | `loai_don` | Danh mục 4 loại đơn: Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc | `ma_loai_don` | — |
| **13** | `don_tu` | Quản lý nộp và xét duyệt đơn từ trực tuyến | `ma_don` | `ma_nhan_vien`, `ma_loai_don`, `nguoi_duyet` |
| **14** | `ky_luong` | Chu kỳ khóa sổ tính lương theo tháng (Tháng/Năm) | `ma_ky_luong` | — |
| **15** | `cau_hinh_khoan_luong`| Tham số định mức phụ cấp, tiền thưởng, tỷ lệ phạt | `ma_khoan` | — |
| **16** | `phieu_luong` | Bảng tổng hợp lương tháng: Gross, Giảm trừ, Net thực nhận | `ma_phieu_luong` | `ma_nhan_vien`, `ma_ky_luong` |
| **17** | `chi_tiet_phieu_luong`| Bản ghi chi tiết từng dòng thu chi (Lương công, OT, Đêm 30%, BHXH 10.5%)| `ma_chi_tiet` | `ma_phieu_luong`, `ma_khoan` |

---

## 2. Từ Điển Dữ Liệu Chi Tiết (Data Dictionary)

### Bảng 1: `phong_ban` (Phòng ban / Bộ phận)
- `ma_phong_ban` (SERIAL, PK): Mã định danh phòng ban.
- `ten_phong_ban` (VARCHAR(100), NOT NULL, UNIQUE): Tên phòng ban (Tiền sảnh, Buồng phòng...).
- `mo_ta` (VARCHAR(255), NULL): Mô tả nhiệm vụ bộ phận.

### Bảng 2: `chuc_vu` (Chức vụ / Vị trí)
- `ma_chuc_vu` (SERIAL, PK): Mã định danh chức vụ.
- `ten_chuc_vu` (VARCHAR(100), NOT NULL, UNIQUE): Tên chức danh nghề nghiệp.
- `mo_ta` (VARCHAR(255), NULL): Mô tả tiêu chuẩn chuyên môn.

### Bảng 3: `nhan_vien` (Hồ sơ nhân viên)
- `ma_nhan_vien` (SERIAL, PK): Mã nhân viên duy nhất.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NOT NULL): Phòng ban trực thuộc.
- `ma_chuc_vu` (INT, FK -> chuc_vu.ma_chuc_vu, NOT NULL): Chức danh đảm nhiệm.
- `ho` (VARCHAR(100), NOT NULL): Họ và tên đệm của nhân viên.
- `ten` (VARCHAR(50), NOT NULL): Tên chính của nhân viên.
- `ngay_sinh` (DATE, NOT NULL): Ngày tháng năm sinh.
- `gioi_tinh` (VARCHAR(10), CHECK in ('Nam', 'Nữ', 'Khác')): Giới tính.
- `dien_thoai` (VARCHAR(15), NOT NULL, UNIQUE): Số điện thoại liên hệ.
- `email` (VARCHAR(100), NOT NULL, UNIQUE): Thư điện tử.
- `dia_chi` (VARCHAR(255), NULL): Nơi ở hiện tại.
- `trinh_do` (VARCHAR(50), NULL): Học vấn (Trung cấp, Cao đẳng, Đại học...).
- `ngay_vao_lam` (DATE, NOT NULL): Ngày chính thức làm việc.
- `trang_thai` (VARCHAR(20), DEFAULT 'DangLamViec', CHECK in ('DangLamViec', 'NghiPhep', 'NghiViec')): Trạng thái công tác.

### Bảng 4: `hop_dong` (Hợp đồng lao động)
- `ma_hop_dong` (SERIAL, PK): Mã hợp đồng lao động.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL): Nhân viên ký kết.
- `so_hop_dong` (VARCHAR(50), NOT NULL, UNIQUE): Số ký hiệu văn bản HĐLĐ.
- `loai_hop_dong` (VARCHAR(50), NOT NULL): Loại HĐ (Thử việc, 1 năm, 3 năm, Vô thời hạn).
- `ngay_bat_dau` (DATE, NOT NULL): Ngày có hiệu lực.
- `ngay_ket_thuc` (DATE, NULL): Ngày hết hạn (NULL nếu vô thời hạn).
- `luong_co_ban` (DECIMAL(15,2), NOT NULL, CHECK > 0): Mức lương thỏa thuận pháp lý.
- `trang_thai` (VARCHAR(20), DEFAULT 'HieuLuc'): Hiệu lực hợp đồng.

### Bảng 5: `tai_khoan` (Tài khoản người dùng)
- `ma_tai_khoan` (SERIAL, PK): Mã tài khoản.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, UNIQUE, NOT NULL): Nhân viên sở hữu (Quan hệ 1-1).
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL): Nhóm vai trò được cấp.
- `ten_dang_nhap` (VARCHAR(50), NOT NULL, UNIQUE): Tên đăng nhập hệ thống.
- `mat_khau_bam` (VARCHAR(255), NOT NULL): Mật khẩu đã mã hóa an toàn (BCrypt/PBKDF2).
- `trang_thai` (VARCHAR(20), DEFAULT 'HoatDong'): HoatDong hoặc BiKhoa.

### Bảng 6: `vai_tro` (Vai trò người dùng)
- `ma_vai_tro` (SERIAL, PK): Mã định danh vai trò.
- `ten_vai_tro` (VARCHAR(50), NOT NULL, UNIQUE): Admin, HR, Manager, Employee.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải phạm vi trách nhiệm.

### Bảng 7: `quyen` (Danh mục quyền truy cập)
- `ma_quyen` (SERIAL, PK): Mã quyền.
- `ten_quyen` (VARCHAR(100), NOT NULL, UNIQUE): Tên quyền (VD: NV_XEM, LUONG_TINH...).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải chức năng thao tác.

### Bảng 8: `vai_tro_quyen` (Phân quyền RBAC)
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL): Vai trò.
- `ma_quyen` (INT, FK -> quyen.ma_quyen, NOT NULL): Quyền được cấp.
- *Khóa chính tổng hợp:* `PRIMARY KEY (ma_vai_tro, ma_quyen)`.

### Bảng 9: `ca_lam_viec` (Danh mục ca 24/7)
- `ma_ca` (SERIAL, PK): Mã ca.
- `ten_ca` (VARCHAR(50), NOT NULL): Ca Sáng, Ca Chiều, Ca Đêm.
- `gio_bat_dau` (TIME, NOT NULL): Khung giờ mở ca (06:00, 14:00, 22:00).
- `gio_ket_thuc` (TIME, NOT NULL): Khung giờ đóng ca (14:00, 22:00, 06:00).
- `he_so_luong` (DECIMAL(4,2), DEFAULT 1.0): Hệ số lương cơ sở.
- `la_ca_dem` (BOOLEAN, DEFAULT FALSE): Đánh dấu ca đêm để tính phụ cấp 30%.

### Bảng 10: `phan_ca` (Lịch xếp ca làm việc)
- `ma_phan_ca` (SERIAL, PK): Mã bản ghi xếp ca.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL): Nhân viên được xếp ca.
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL): Ca làm việc được chỉ định.
- `ngay_lam_viec` (DATE, NOT NULL): Ngày thực hiện ca.
- `ghi_chu` (VARCHAR(255), NULL): Ghi chú phân công của quản lý.

### Bảng 11: `cham_cong` (Bản ghi quẹt thẻ in/out)
- `ma_cham_cong` (SERIAL, PK): Mã bản ghi chấm công.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL): Nhân viên quẹt thẻ (hoặc `ma_phan_ca` FK -> phan_ca).
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL): Ca làm việc đối soát.
- `ngay_cham_cong` (DATE, NOT NULL): Ngày giao dịch.
- `thoi_gian_checkin` (TIMESTAMP, NULL): Thời điểm quẹt thẻ vào ca (`gio_check_in`).
- `thoi_gian_checkout` (TIMESTAMP, NULL): Thời điểm quẹt thẻ ra ca (`gio_check_out`).
- `so_phut_di_tre` (INT, DEFAULT 0): Số phút trễ so với giờ mở ca.
- `so_phut_ve_som` (INT, DEFAULT 0): Số phút về sớm trước giờ đóng ca.
- `so_gio_ot` (DECIMAL(4,2), DEFAULT 0.0): Số giờ làm thêm ngoài ca được duyệt.
- `trang_thai` (VARCHAR(20), DEFAULT 'HopLe'): HopLe, DiTre, VeSom, VangMat.
- `phuong_thuc_checkin` (VARCHAR(20), NULL): Phương thức xác thực vị trí: 'GPS', 'WIFI', hoặc 'THU_CONG'.
- `toa_do_checkin` (VARCHAR(50), NULL): Kinh độ, vĩ độ GPS thực tế lúc bấm quẹt (VD: '10.776889, 106.700806').
- `thiet_bi_checkin` (VARCHAR(100), NULL): Định danh thiết bị Mobile App gửi lên để chống mượn máy quẹt hộ.

### Bảng 12: `loai_don` (Danh mục loại đơn)
- `ma_loai_don` (SERIAL, PK): Mã loại đơn.
- `ten_loai_don` (VARCHAR(50), NOT NULL): Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc.
- `huong_luong` (BOOLEAN, DEFAULT TRUE): Quy định có được hưởng lương hay không.

### Bảng 13: `don_tu` (Đơn từ điện tử)
- `ma_don` (SERIAL, PK): Mã đơn từ.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL): Người nộp đơn.
- `ma_loai_don` (INT, FK -> loai_don.ma_loai_don, NOT NULL): Loại đơn nộp.
- `ngay_nop` (TIMESTAMP, DEFAULT CURRENT_TIMESTAMP): Thời điểm nộp.
- `ngay_bat_dau` (DATE, NOT NULL): Ngày bắt đầu nghỉ/thôi việc.
- `ngay_ket_thuc` (DATE, NULL): Ngày kết thúc nghỉ.
- `ly_do` (TEXT, NOT NULL): Lý do nộp đơn.
- `trang_thai` (VARCHAR(20), DEFAULT 'ChoDuyet', CHECK in ('ChoDuyet', 'DaDuyet', 'TuChoi')): Trạng thái.
- `nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NULL): Người ký duyệt đơn.
- `ngay_duyet` (TIMESTAMP, NULL): Thời điểm phê duyệt.
- `phan_hoi` (TEXT, NULL): Ý kiến chỉ đạo khi duyệt hoặc lý do từ chối.

### Bảng 14: `ky_luong` (Kỳ tính lương)
- `ma_ky_luong` (SERIAL, PK): Mã kỳ lương.
- `ten_ky_luong` (VARCHAR(50), NOT NULL): Tên chu kỳ (VD: "Lương Tháng 10/2026").
- `thang` (INT, NOT NULL, CHECK (thang BETWEEN 1 AND 12)): Tháng lương.
- `nam` (INT, NOT NULL): Năm lương.
- `ngay_bat_dau` (DATE, NOT NULL): Ngày đầu chu kỳ tính công.
- `ngay_ket_thuc` (DATE, NOT NULL): Ngày cuối chu kỳ tính công.
- `da_khoa_so` (BOOLEAN, DEFAULT FALSE): Trạng thái đóng/mở chỉnh sửa bảng lương.

### Bảng 15: `cau_hinh_khoan_luong` (Cấu hình thu chi)
- `ma_khoan` (SERIAL, PK): Mã khoản thu chi.
- `ten_khoan` (VARCHAR(100), NOT NULL): Phụ cấp ăn ca, Phụ cấp đêm, BHXH, Thưởng, Phạt trễ...
- `loai_khoan` (VARCHAR(20), NOT NULL, CHECK in ('ThuNhap', 'KhauTru')): Phân loại cộng hoặc trừ.
- `gia_tri_mac_dinh` (DECIMAL(15,2), DEFAULT 0.0): Giá trị số tiền cố định (nếu có).
- `ti_le_phan_tram` (DECIMAL(5,2), DEFAULT 0.0): Tỷ lệ % tính theo lương cơ bản (VD: BHXH 10.5%).

### Bảng 16: `phieu_luong` (Phiếu lương tổng hợp)
- `ma_phieu_luong` (SERIAL, PK): Mã phiếu lương.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL): Nhân viên nhận lương.
- `ma_ky_luong` (INT, FK -> ky_luong.ma_ky_luong, NOT NULL): Kỳ lương tương ứng.
- `so_ngay_cong_chuan` (INT, DEFAULT 26): Ngày công định mức trong tháng.
- `so_ngay_cong_thuc_te` (DECIMAL(4,2), NOT NULL): Số công làm việc thực tế.
- `luong_co_ban` (DECIMAL(15,2), NOT NULL): Lương cơ bản snapshot tại thời điểm tính.
- `tong_thu_nhap` (DECIMAL(15,2), NOT NULL): Tổng thu nhập Gross.
- `tong_khau_tru` (DECIMAL(15,2), NOT NULL): Tổng các khoản trừ.
- `luong_thuc_nhan` (DECIMAL(15,2), NOT NULL): Lương Net thực chuyển khoản.
- `ngay_lap` (TIMESTAMP, DEFAULT CURRENT_TIMESTAMP): Thời điểm tính lương.

### Bảng 17: `chi_tiet_phieu_luong` (Chi tiết phiếu lương)
- `ma_chi_tiet` (SERIAL, PK): Mã bản ghi chi tiết.
- `ma_phieu_luong` (INT, FK -> phieu_luong.ma_phieu_luong, NOT NULL): Phiếu lương chủ.
- `ma_khoan` (INT, FK -> cau_hinh_khoan_luong.ma_khoan, NOT NULL): Khoản thu chi cụ thể.
- `so_tien` (DECIMAL(15,2), NOT NULL): Số tiền cụ thể được cộng hoặc trừ.
- `ghi_chu` (VARCHAR(255), NULL): Diễn giải công thức tính chi tiết.
