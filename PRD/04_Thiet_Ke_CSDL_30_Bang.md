# PRD 04: Thiết Kế Cơ Sở Dữ Liệu 30 Bảng Chuẩn 3NF (Supabase PostgreSQL)

> **Tài liệu đặc tả kiến trúc dữ liệu cốt lõi:** Tài liệu này mô tả chi tiết toàn bộ **30 bảng CSDL chuẩn hóa dạng chuẩn 3 (3NF)** đang vận hành trực tiếp trên nền tảng Supabase Cloud (PostgreSQL 17.6), hệ thống bảo mật đa tầng **Row Level Security (RLS)**, các hàm **Stored Functions**, hệ thống **Triggers rào chắn** và cơ chế phân định luồng kiểm soát truy cập giữa **Web Admin Portal** và **Mobile App**.

> **Nguồn đối chiếu & Tiến trình chuẩn hóa mở rộng qua các giai đoạn:**
> 1. **17 Bảng gốc:** Thiết kế sơ bộ ban đầu cho các nghiệp vụ cốt lõi (Hồ sơ, Hợp đồng, Tài khoản, Phân ca, Chấm công, Đơn từ, Kỳ lương, Cấu hình lương, Phiếu lương).
> 2. **24 Bảng chuẩn 3NF (07/10/2026):** Dump từ CSDL vận hành Supabase Cloud (PostgreSQL 17.6). Phân rã chuẩn 3NF phân hệ tiền lương: tách 3 bảng cấu hình tham số (`cau_hinh_phu_cap`, `cau_hinh_khau_tru`, `cau_hinh_tang_ca`); tách 3 bảng chi tiết khấu trừ lương 3 tầng (`chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn`, `chi_tiet_tru_vi_pham_tam_ung`); bổ sung bảng kiểm toán an ninh `nhat_ky_quan_tri`; giữ bảng `cau_hinh_khoan_luong` làm bảng Legacy/Bonus.
> 3. **25 Bảng (09/10/2026):** Bổ sung bảng thứ 25 `yeu_cau_tai_khoan`, tách riêng bộ phận Công nghệ Thông tin (`ma_phong_ban = 9`) và bổ sung nhóm vai trò thứ 5 (`IT`), thiết lập quy trình kiểm soát cấp tài khoản Mobile App: *HR duyệt HĐLĐ -> IT cấp tài khoản* kèm 4 triggers rào chắn CSDL chống tự cấp và leo quyền.
> 4. **27 Bảng chính thức (14/10/2026):** Bổ sung 2 bảng `du_an` (Bảng 26) và `phan_cong_du_an` (Bảng 27) phục vụ Phân hệ 9 (Quản lý Dự án & Sự kiện Khách sạn: Tiệc cưới, Hội nghị MICE, Gala Dinner, Đoàn VIP...), hỗ trợ phân công nhân sự và kết chuyển thù lao sự kiện vào bảng lương tháng.
> 5. **30 Bảng chính thức (20/10/2026):** Bổ sung 3 bảng phục vụ nhóm chức năng 3.0 (Phân ca) và 4.0 (Chấm công) của PRD: `dinh_bien_ca` (Bảng 28 — định biên số người tối thiểu theo ca × chức vụ × bộ phận, phát hiện thiếu người khi xếp ca), `giai_trinh_cham_cong` (Bảng 29 — giải trình sai lệch giờ quẹt thẻ, duyệt 2 cấp Quản lý → HR), `yeu_cau_doi_ca` (Bảng 30 — nhân viên tự nguyện đổi ca trực với đồng nghiệp). Đồng thời mở rộng `cham_cong_trang_thai_check` thêm mã `TRE_VA_SOM` (nhân viên vừa đi trễ vừa về sớm trong cùng một ca).

---

## 1. Danh Mục Tổng Hợp 30 Bảng CSDL

Hệ thống CSDL HRM The OverTime Hotel được thiết kế đạt chuẩn hóa **dạng chuẩn 3 (3NF)** trên nền tảng Supabase Cloud (PostgreSQL Engine):

| STT | Tên Bảng | Ý Nghĩa Nghiệp Vụ Trong Khách Sạn | Khóa Chính (PK) | Khóa Ngoại (FK) |
|:---:|:---|:---|:---|:---|
| **1** | `phong_ban` | Danh mục các phòng ban, bộ phận khách sạn (FO, HK, F&B, Kỹ thuật, CNTT...) | `ma_phong_ban` | — |
| **2** | `chuc_vu` | Danh mục vị trí công tác (Bếp trưởng, Lễ tân, Bellman, Trưởng ca, IT Support...) | `ma_chuc_vu` | — |
| **3** | `nhan_vien` | Hồ sơ lý lịch, thông tin cá nhân, liên hệ (email công vụ + email cá nhân) | `ma_nhan_vien` | `ma_phong_ban`, `ma_chuc_vu` |
| **4** | `hop_dong` | Hợp đồng lao động pháp lý, lưu thời hạn và mức lương cơ bản | `ma_hop_dong` | `ma_nhan_vien` |
| **5** | `tai_khoan` | Tài khoản đăng nhập hệ thống, mật khẩu băm, trạng thái kích hoạt boolean | `ma_tai_khoan` | `ma_nhan_vien` (1-1), `ma_vai_tro` |
| **6** | `vai_tro` | 5 nhóm vai trò chuẩn: Admin, HR, Manager, Employee, IT | `ma_vai_tro` | — |
| **7** | `quyen` | Danh mục quyền truy cập với mã quyền dạng chuỗi (`ma_quyen_code`) | `ma_quyen` | — |
| **8** | `vai_tro_quyen` | Bảng trung gian giải quyết quan hệ N - N giữa Vai trò và Quyền (RBAC) | `(ma_vai_tro, ma_quyen)` | `ma_vai_tro`, `ma_quyen` |
| **9** | `yeu_cau_tai_khoan` | Hàng đợi cấp tài khoản: HR gửi sau khi duyệt HĐLĐ, bộ phận CNTT (IT) tiếp nhận và cấp | `ma_yeu_cau` | `ma_nhan_vien`, `ma_hop_dong`, `ma_vai_tro`, `ma_nguoi_gui`, `ma_nguoi_xu_ly`, `ma_tai_khoan` |
| **10** | `ca_lam_viec` | Danh mục 3 mẫu ca 24/7 (Sáng 6h-14h, Chiều 14h-22h, Đêm 22h-6h cờ qua đêm) | `ma_ca` | — |
| **11** | `phan_ca` | Bảng xếp lịch ca làm việc trước cho nhân viên theo tuần/tháng | `ma_phan_ca` | `ma_nhan_vien`, `ma_ca` |
| **12** | `cham_cong` | Bản ghi quẹt thẻ in/out thực tế, tự động tính phút trễ, sớm, giờ OT (GPS/Wifi) | `ma_cham_cong` | `ma_phan_ca` (1-1) |
| **13** | `loai_don` | Danh mục 4 loại đơn: Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc | `ma_loai_don` | — |
| **14** | `don_tu` | Quản lý nộp và xét duyệt đơn từ trực tuyến | `ma_don` | `ma_nhan_vien`, `ma_loai_don`, `ma_nguoi_duyet` |
| **15** | `ky_luong` | Chu kỳ khóa sổ tính lương theo tháng (Tháng/Năm, trạng thái MO/DANG_TINH/KHOA_SO) | `ma_ky_luong` | — |
| **16** | `cau_hinh_khoan_luong`| Tham số định mức tiền thưởng (Bonus) và tương thích ngược Legacy | `ma_khoan` | — |
| **17** | `cau_hinh_phu_cap` | Tham số phụ cấp (ăn ca, đêm, đi lại, trách nhiệm) kèm cờ chịu thuế TNCN | `ma_phu_cap` | — |
| **18** | `cau_hinh_khau_tru` | Tham số khoản khấu trừ (BHXH, BHYT, BHTN, thuế TNCN, vi phạm) | `ma_khoan_tru` | — |
| **19** | `cau_hinh_tang_ca` | Danh mục loại tăng ca kèm hệ số nhân tiền lương theo giờ OT | `ma_loai_ot` | — |
| **20** | `phieu_luong` | Bảng tổng hợp lương tháng: Lương công, OT, Phụ cấp, Thưởng, Khấu trừ, Net | `ma_phieu_luong` | `ma_nhan_vien`, `ma_ky_luong` |
| **21** | `chi_tiet_phieu_luong`| Bản ghi chi tiết từng dòng thu chi (Snapshot tên khoản và loại khoản bất biến) | `ma_chi_tiet` | `ma_phieu_luong`, `ma_khoan`, `ma_loai_ot`, `ma_phu_cap`, `ma_khoan_tru` |
| **22** | `chi_tiet_tru_bao_hiem` | Khai báo 3 khoản bảo hiểm bắt buộc BHXH 8% + BHYT 1.5% + BHTN 1% từng dòng | `ma_chi_tiet_bh` | `ma_chi_tiet` (1-1) |
| **23** | `chi_tiet_tru_thue_tncn` | Bảng tính thuế TNCN 5 bậc lũy tiến từng dòng chi tiết | `ma_chi_tiet_thue` | `ma_chi_tiet` (1-1) |
| **24** | `chi_tiet_tru_vi_pham_tam_ung` | Bảng ghi khấu trừ vi phạm kỷ luật (đi trễ, về sớm, bỏ ca) và khấu trừ khác (bỏ tạm ứng) | `ma_chi_tiet_vp` | `ma_chi_tiet` (1-1) |
| **25** | `nhat_ky_quan_tri` | Nhật ký thao tác quản trị (Audit Log: ai làm gì, lúc nào, giá trị cũ/mới, IP) | `ma_nhat_ky` | `ma_tai_khoan_thuc_hien`, `ma_tai_khoan_bi_tac_dong` |
| **26** | `du_an` | Danh mục Dự án / Sự kiện Khách sạn (Tiệc cưới, MICE, Gala, Hội nghị, Bảo trì...) | `ma_du_an` | `ma_nguoi_quan_ly` (FK -> `nhan_vien`) |
| **27** | `phan_cong_du_an` | Phân công nhân sự & Thù lao sự kiện, liên kết kết chuyển lương tháng | `ma_phan_cong` | `ma_du_an`, `ma_nhan_vien`, `ma_chi_tiet` (FK -> `chi_tiet_phieu_luong`) |
| **28** | `dinh_bien_ca` | Định biên nhân sự tối thiểu cho từng bộ ba Ca × Chức vụ × Bộ phận, làm cơ sở cảnh báo thiếu người khi xếp ca | `ma_dinh_bien` | `ma_ca`, `ma_chuc_vu`, `ma_phong_ban` (NULL = dùng chung) |
| **29** | `giai_trinh_cham_cong` | Giải trình sai lệch giờ vào/ra, duyệt 2 cấp Quản lý xác minh → HR xác nhận | `ma_giai_trinh` | `ma_phan_ca`, `ma_nhan_vien`, `ma_quan_ly_xac_minh`, `ma_hr_xac_nhan` |
| **30** | `yeu_cau_doi_ca` | Yêu cầu đổi ca trực giữa hai nhân viên, kèm trạng thái duyệt và phản hồi | `ma_yeu_cau` | `ma_phan_ca_gui`, `ma_phan_ca_nhan`, `ma_nhan_vien_gui`, `ma_nhan_vien_nhan`, `ma_nguoi_duyet` |

---

## 2. Từ Điển Dữ Liệu Chi Tiết (Data Dictionary)

### Bảng 1: `phong_ban` (Phòng ban / Bộ phận)
- `ma_phong_ban` (SERIAL, PK): Mã định danh phòng ban.
- `ten_phong_ban` (VARCHAR(150), NOT NULL, UNIQUE): Tên phòng ban (Tiền sảnh, Buồng phòng, Ẩm thực F&B, Kỹ thuật, Công nghệ Thông tin...).
- `mo_ta` (VARCHAR(255), NULL): Mô tả nhiệm vụ bộ phận.

### Bảng 2: `chuc_vu` (Chức vụ / Vị trí)
- `ma_chuc_vu` (SERIAL, PK): Mã định danh chức vụ.
- `ten_chuc_vu` (VARCHAR(100), NOT NULL, UNIQUE): Tên chức danh nghề nghiệp (Bếp trưởng, Lễ tân, Bellman, Trưởng ca, IT Support, IT Manager...).
- `mo_ta` (VARCHAR(255), NULL): Mô tả tiêu chuẩn chuyên môn.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NULL, ON DELETE SET NULL): Phòng ban trực thuộc (để `NULL` nếu là chức danh chung trải nhiều bộ phận như Trưởng Bộ Phận, Giám Sát Ca).
- `che_do_lam_viec` (VARCHAR(50), NOT NULL, DEFAULT 'CA_247'): Chế độ làm việc chuẩn (`CA_247` hoặc `HANH_CHINH`).

### Bảng 3: `nhan_vien` (Hồ sơ nhân viên)
- `ma_nhan_vien` (SERIAL, PK): Mã nhân viên duy nhất.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NOT NULL, ON DELETE RESTRICT): Phòng ban trực thuộc.
- `ma_chuc_vu` (INT, FK -> chuc_vu.ma_chuc_vu, NOT NULL, ON DELETE RESTRICT): Chức danh đảm nhiệm.
- `ho` (VARCHAR(100), NOT NULL): Họ và tên đệm của nhân viên.
- `ten` (VARCHAR(50), NOT NULL): Tên chính của nhân viên.
- `ngay_sinh` (DATE, NULL): Ngày tháng năm sinh.
- `gioi_tinh` (VARCHAR(10), NULL, CHECK in ('NAM', 'NU', 'KHAC')): Giới tính.
- `dien_thoai` (VARCHAR(20), NOT NULL): Số điện thoại liên hệ.
- `email` (VARCHAR(100), NOT NULL, UNIQUE): **Email công vụ** — định danh tài khoản đăng nhập Mobile App, đối chiếu 1-1 với `claims.email` trong `custom_access_token_hook`. Tuyệt đối không thay đổi tùy tiện vì làm gián đoạn phiên đăng nhập app.
- `email_ca_nhan` (VARCHAR(100), NULL): **Email cá nhân** — dùng lưu trữ hồ sơ liên lạc nhân sự, nhận thông báo cá nhân; tách biệt hoàn toàn với định danh công vụ để việc cập nhật không ảnh hưởng đến cơ chế xác thực.
- `dia_chi` (VARCHAR(255), NULL): Nơi ở hiện tại.
- `trinh_do` (VARCHAR(50), NOT NULL): Học vấn (Trung cấp, Cao đẳng, Đại học, Thạc sĩ...).
- `ngay_vao_lam` (DATE, NOT NULL): Ngày chính thức làm việc.
- `ngay_thoi_viec` (DATE, NULL): Ngày nghỉ việc thực tế (NULL nếu còn làm việc).
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DANG_LAM', CHECK in ('DANG_LAM', 'NGHI_PHEP', 'DA_THOI_VIEC')): Trạng thái công tác.
- `is_profile_complete` (BOOLEAN, NOT NULL, DEFAULT FALSE): Cờ đánh dấu nhân sự đã hoàn tất cập nhật hồ sơ cá nhân lần đầu.
- `so_nguoi_phu_thuoc` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Số lượng người phụ thuộc đăng ký hợp lệ dùng để tính giảm trừ gia cảnh thuế TNCN.

### Bảng 4: `hop_dong` (Hợp đồng lao động)
- `ma_hop_dong` (SERIAL, PK): Mã hợp đồng lao động.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên ký kết.
- `so_hop_dong` (VARCHAR(50), NOT NULL, UNIQUE): Số ký hiệu văn bản HĐLĐ.
- `loai_hop_dong` (VARCHAR(50), NOT NULL): Loại HĐ (Thử việc, 1 năm, 3 năm, Vô thời hạn).
- `ngay_bat_dau` (DATE, NOT NULL): Ngày có hiệu lực.
- `ngay_ket_thuc` (DATE, NULL): Ngày hết hạn (NULL nếu vô thời hạn).
- `luong_co_ban` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Mức lương thỏa thuận pháp lý làm căn cứ đóng BHXH và tính lương công.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'HIEU_LUC', CHECK in ('HIEU_LUC', 'HET_HAN', 'DA_CHAM_DUT')): Hiệu lực hợp đồng.
- `hinh_thuc_hop_dong` (VARCHAR(50), NULL): Hình thức làm việc theo hợp đồng (Toàn thời gian `FULL_TIME`, Bán thời gian `PART_TIME`).

### Bảng 5: `tai_khoan` (Tài khoản người dùng)
- `ma_tai_khoan` (SERIAL, PK): Mã tài khoản.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, UNIQUE, NOT NULL, ON DELETE CASCADE): Nhân viên sở hữu (Quan hệ 1-1).
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE RESTRICT): Nhóm vai trò được cấp.
- `ten_dang_nhap` (VARCHAR(50), NOT NULL, UNIQUE): Tên đăng nhập hệ thống.
- `mat_khau_bam` (VARCHAR(255), NOT NULL): Mật khẩu đã mã hóa an toàn (BCrypt/PBKDF2).
- `trang_thai` (BOOLEAN, NOT NULL, DEFAULT TRUE): Trạng thái tài khoản — `TRUE` (Đang hoạt động), `FALSE` (Bị khóa). Khớp chuẩn logic CSDL Supabase và điều kiện kiểm tra trong `custom_access_token_hook`.

### Bảng 6: `vai_tro` (Vai trò người dùng)
- `ma_vai_tro` (SERIAL, PK): Mã định danh vai trò (1: Admin, 2: HR, 3: Manager, 4: Employee, 5: IT).
- `ten_vai_tro` (VARCHAR(50), NOT NULL, UNIQUE): Tên vai trò (Admin, HR, Manager, Employee, IT).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải phạm vi trách nhiệm.

> **Ghi chú về Vai trò thứ 5 `IT` (mã = 5):** Bộ phận Công nghệ Thông tin được tách riêng khỏi Kỹ thuật (`ma_phong_ban = 9`). Vai trò `IT` là tác nhân quản trị chuyên trách xử lý các bản ghi `yeu_cau_tai_khoan` (Bảng 9) và sao lưu CSDL, sở hữu quyền `CAP_TAI_KHOAN_MOBILE`.

### Bảng 7: `quyen` (Danh mục quyền truy cập)
- `ma_quyen` (SERIAL, PK): Mã định danh quyền.
- `ma_quyen_code` (VARCHAR(50), NOT NULL, UNIQUE): Mã quyền chuẩn hóa dạng chuỗi viết hoa (VD: `CHAM_CONG_DI_DONG`, `NOP_DON_TU`, `QL_TAI_KHOAN`, `CAP_TAI_KHOAN_MOBILE`, `TINH_LUONG`...). Ánh xạ 1-1 với hằng chuỗi trong `Security/PermissionCodes.cs` của Web Admin và được gom vào mảng claim `app_metadata.quyen` trong JWT.
- `ten_quyen` (VARCHAR(100), NOT NULL, UNIQUE): Tên hiển thị quyền trên giao diện phân quyền.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải chức năng thao tác.

### Bảng 8: `vai_tro_quyen` (Phân quyền RBAC)
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE CASCADE): Vai trò.
- `ma_quyen` (INT, FK -> quyen.ma_quyen, NOT NULL, ON DELETE CASCADE): Quyền được cấp.
- *Khóa chính tổng hợp:* `PRIMARY KEY (ma_vai_tro, ma_quyen)`.
- *Rào chắn an ninh:* Bị kiểm soát bởi trigger `trg_chan_cap_quyen_tai_khoan_di_dong`, chặn gán quyền `CAP_TAI_KHOAN_MOBILE` cho vai trò ngoài Admin và IT.

### Bảng 9: `yeu_cau_tai_khoan` (Yêu cầu cấp tài khoản đăng nhập)
- `ma_yeu_cau` (SERIAL, PK): Mã yêu cầu cấp tài khoản.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên được cấp tài khoản (người thụ hưởng).
- `ma_hop_dong` (INT, FK -> hop_dong.ma_hop_dong, NULL, ON DELETE SET NULL): Hợp đồng lao động làm căn cứ phát sinh yêu cầu.
- `ten_dang_nhap` (VARCHAR(50), NOT NULL): Tên đăng nhập dự kiến cấp cho Mobile App.
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE RESTRICT): Vai trò sẽ được gán khi cấp tài khoản.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'CHO_IT', CHECK in ('CHO_IT', 'DANG_XU_LY', 'DA_CAP', 'TU_CHOI')): Trạng thái luồng cấp phát: HR gửi (`CHO_IT`) -> IT tiếp nhận (`DANG_XU_LY`) -> cấp xong (`DA_CAP`) hoặc từ chối (`TU_CHOI`).
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm HR gửi yêu cầu.
- `ma_nguoi_gui` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Nhân viên HR duyệt HĐLĐ và gửi yêu cầu.
- `ma_nguoi_xu_ly` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản IT tiếp nhận và xử lý yêu cầu.
- `ngay_xu_ly` (TIMESTAMPTZ, NULL): Thời điểm IT kết luận xử lý.
- `ghi_chu_tu_choi` (VARCHAR(500), NULL): Lý do từ chối (bắt buộc có khi `trang_thai = 'TU_CHOI'`).
- `ma_tai_khoan` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản kết quả sau khi cấp thành công (bắt buộc có khi `trang_thai = 'DA_CAP'`).
- *Ràng buộc:*
  - CHECK `chk_yeu_cau_tu_choi_can_co_ghi_chu`: `(trang_thai <> 'TU_CHOI' OR ghi_chu_tu_choi IS NOT NULL)`.
  - CHECK `chk_yeu_cau_da_cap_can_co_tai_khoan`: `((trang_thai = 'DA_CAP') = (ma_tai_khoan IS NOT NULL))`.
  - CHECK `chk_yeu_cau_da_xu_ly_can_co_nguoi`: `(trang_thai = 'CHO_IT' OR ma_nguoi_xu_ly IS NOT NULL)`.
  - UNIQUE INDEX partial: `uq_yeu_cau_ten_dang_nhap_dang_mo` trên `ten_dang_nhap` WHERE `trang_thai IN ('CHO_IT', 'DANG_XU_LY')`.

### Bảng 10: `ca_lam_viec` (Danh mục ca 24/7)
- `ma_ca` (SERIAL, PK): Mã ca.
- `ten_ca` (VARCHAR(50), NOT NULL, UNIQUE): Ca Sáng, Ca Chiều, Ca Đêm.
- `gio_bat_dau` (TIME, NOT NULL): Khung giờ mở ca (06:00, 14:00, 22:00).
- `gio_ket_thuc` (TIME, NOT NULL): Khung giờ đóng ca (14:00, 22:00, 06:00).
- `qua_dem` (BOOLEAN, NOT NULL, DEFAULT FALSE): Đánh dấu ca làm việc qua nửa đêm để kích hoạt phụ cấp ca đêm 30%.
- `gio_lam_chuan` (NUMERIC, NOT NULL, DEFAULT 8.00): Số giờ làm việc tiêu chuẩn trong ca (8.0 giờ).
- `loai_ca` (VARCHAR(50), NOT NULL, DEFAULT 'CA_CHINH'): Phân loại tính chất ca (`CA_CHINH`, `CA_PHU`, `CA_SU_KIEN`).

### Bảng 11: `phan_ca` (Lịch xếp ca làm việc)
- `ma_phan_ca` (SERIAL, PK): Mã bản ghi xếp ca.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên được xếp ca.
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL, ON DELETE RESTRICT): Ca làm việc được chỉ định.
- `ngay_lam_viec` (DATE, NOT NULL): Ngày thực hiện ca.
- `bat_dau_du_kien` (TIMESTAMPTZ, NOT NULL): Mốc thời gian dự kiến bắt đầu ca (làm căn cứ tính phút đi trễ).
- `ket_thuc_du_kien` (TIMESTAMPTZ, NOT NULL): Mốc thời gian dự kiến kết thúc ca (làm căn cứ tính phút về sớm).
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DA_PHAN', CHECK in ('DA_PHAN', 'HOAN_THANH', 'VANG_MAT', 'DA_HUY')): Trạng thái xử lý ca.
- `da_cong_bo` (BOOLEAN, NOT NULL, DEFAULT FALSE): Trạng thái công bố lịch ca cho nhân viên xem trên Mobile App.
- `gio_tang_ca_du_kien` (DECIMAL(4,2), NOT NULL, DEFAULT 0.00, CHECK (gio_tang_ca_du_kien >= 0 AND gio_tang_ca_du_kien <= 4.0)): Số giờ làm thêm (OT) dự kiến được phân công theo ca (tối đa 4h/ngày theo Điều 107 BLLĐ 2019).
- `ghi_chu_ot` (VARCHAR(255), NULL): Lý do / nội dung công việc làm thêm ngoài giờ.

### Bảng 12: `cham_cong` (Bản ghi quẹt thẻ in/out)
- `ma_cham_cong` (SERIAL, PK): Mã bản ghi chấm công.
- `ma_phan_ca` (INT, FK -> phan_ca.ma_phan_ca, NOT NULL, UNIQUE, ON DELETE CASCADE): Bản ghi phân ca được chấm công (quan hệ 1-1, thông tin nhân viên và ca suy ra từ `phan_ca`).
- `gio_check_in` (TIMESTAMPTZ, NULL): Thời điểm quẹt thẻ vào ca.
- `gio_check_out` (TIMESTAMPTZ, NULL): Thời điểm quẹt thẻ ra ca.
- `phut_di_tre` (INT, NOT NULL, DEFAULT 0): Số phút trễ so với `bat_dau_du_kien`.
- `phut_ve_som` (INT, NOT NULL, DEFAULT 0): Số phút về sớm so với `ket_thuc_du_kien`.
- `gio_tang_ca_ot` (DECIMAL(4,2), NOT NULL, DEFAULT 0.00): Số giờ làm thêm ngoài ca đã được phê duyệt.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DUNG_GIO', CHECK in ('DUNG_GIO', 'DI_TRE', 'VE_SOM', 'VANG_MAT', 'TRE_VA_SOM')): Kết luận chấm công.
- `phuong_thuc_checkin` (VARCHAR(20), NULL): Phương thức xác thực (`GPS`, `WIFI`, `THU_CONG`).
- `toa_do_checkin` (VARCHAR(50), NULL): Tọa độ GPS thực tế lúc quẹt thẻ.
- `thiet_bi_checkin` (VARCHAR(100), NULL): Định danh thiết bị phần cứng gửi lên từ Mobile App chống gian lận.

### Bảng 13: `loai_don` (Danh mục loại đơn)
- `ma_loai_don` (SERIAL, PK): Mã loại đơn.
- `ten_loai_don` (VARCHAR(100), NOT NULL, UNIQUE): Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc.
- `co_huong_luong` (BOOLEAN, NOT NULL, DEFAULT FALSE): Quy định loại nghỉ có được hưởng nguyên lương hay không.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải quyền lợi đi kèm.
- `la_nghi_phep` (BOOLEAN, NOT NULL, DEFAULT FALSE): Đánh dấu loại đơn có tính trừ vào số ngày nghỉ phép năm theo luật hay không.

### Bảng 14: `don_tu` (Đơn từ điện tử)
- `ma_don` (SERIAL, PK): Mã đơn từ.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Người nộp đơn.
- `ma_loai_don` (INT, FK -> loai_don.ma_loai_don, NOT NULL, ON DELETE RESTRICT): Loại đơn nộp.
- `ma_nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Cấp quản lý ký duyệt đơn.
- `tu_ngay` (DATE, NOT NULL): Ngày bắt đầu nghỉ / hiệu lực thôi việc.
- `den_ngay` (DATE, NOT NULL): Ngày kết thúc nghỉ.
- `ly_do` (TEXT, NOT NULL): Lý do nộp đơn.
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm gửi đơn.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'CHO_DUYET', CHECK in ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI')): Tiến trình xử lý.
- `phan_hoi_duyet` (VARCHAR(255), NULL): Ý kiến phê duyệt hoặc lý do từ chối.
- `ngay_duyet` (TIMESTAMPTZ, NULL): Thời điểm phê duyệt.

### Bảng 15: `ky_luong` (Kỳ tính lương)
- `ma_ky_luong` (SERIAL, PK): Mã kỳ lương.
- `thang` (INT, NOT NULL, CHECK (thang BETWEEN 1 AND 12)): Tháng lương.
- `nam` (INT, NOT NULL, CHECK (nam >= 2000)): Năm lương.
- `tu_ngay` (DATE, NOT NULL): Ngày đầu chu kỳ tính công.
- `den_ngay` (DATE, NOT NULL): Ngày cuối chu kỳ tính công.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'MO', CHECK in ('MO', 'NHAP', 'CHO_PHAN_HOI', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA_SO')): Trạng thái chu kỳ lương (`CHO_PHAN_HOI`: Đang phát hành nháp cho nhân viên kiểm tra đối soát).
- `ngay_phat_hanh_nhap` (TIMESTAMPTZ, NULL): Thời điểm HR phát hành phiếu lương nháp cho nhân viên kiểm tra, đối soát trước khi trình sếp.
- `nguoi_phat_hanh_nhap` (VARCHAR(100), NULL): Tên cán bộ HR thực hiện phát hành phiếu lương nháp.
- `han_phan_hoi_nhap` (TIMESTAMPTZ, NULL): Hạn chót nhân viên kiểm tra và gửi giải trình chấm công nếu có sai sót (mặc định 3 ngày làm việc).
- `ngay_gui_duyet` (TIMESTAMPTZ, NULL): Thời điểm HR gửi bảng lương lên Ban Giám đốc phê duyệt.
- `nguoi_gui_duyet` (VARCHAR(100), NULL): Tên cán bộ HR gửi duyệt bảng lương.
- `ngay_duyet` (TIMESTAMPTZ, NULL): Thời điểm Ban Giám đốc phê duyệt bảng lương.
- `nguoi_duyet` (VARCHAR(100), NULL): Tên người phê duyệt bảng lương.
- `ly_do_tu_choi` (TEXT, NULL): Ý kiến chỉ đạo hoặc lý do từ chối phê duyệt bảng lương.
- `ngay_khoa_so` (TIMESTAMPTZ, NULL): Thời điểm khóa sổ kỳ lương chính thức.
- `nguoi_khoa_so` (VARCHAR(100), NULL): Tên người thực hiện thao tác khóa sổ.
- *Ràng buộc:* `UNIQUE (thang, nam)` — Mỗi tháng/năm chỉ có duy nhất một kỳ tính lương.

### Bảng 16: `cau_hinh_khoan_luong` (Cấu hình khoản thu chi - Legacy & Khoản Thưởng)
- `ma_khoan` (SERIAL, PK): Mã khoản thu chi.
- `ten_khoan` (VARCHAR(150), NOT NULL, UNIQUE): Tên khoản (VD: Thưởng hiệu suất KPI, Thưởng tháng 13, Thưởng sự kiện...).
- `loai_khoan` (VARCHAR(30), NOT NULL, CHECK in ('PHU_CAP', 'THUONG', 'KHAU_TRU')): Phân loại thu chi.
- `gia_tri_mac_dinh` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Giá trị tiền cố định định mức.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải cách áp dụng.
- `loai_du_an` (VARCHAR(50), NULL): Liên kết loại hình sự kiện áp dụng (nếu là thưởng theo sự kiện).
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NULL, ON DELETE SET NULL): Bộ phận áp dụng khoản thưởng riêng (NULL = toàn khách sạn).

> **Vai trò bảng Legacy / Bonus trong kiến trúc:**
> Bảng này vốn là bảng tham số gốc trong thiết kế 17 bảng ban đầu. Khi hệ thống chuẩn hóa 3NF thành 24 bảng, các cấu hình phụ cấp, khấu trừ và tăng ca được bóc tách sang 3 bảng chuyên dụng (`cau_hinh_phu_cap`, `cau_hinh_khau_tru`, `cau_hinh_tang_ca`). Bảng `cau_hinh_khoan_luong` được giữ lại để **tương thích ngược hoàn toàn (Legacy support)** với các bản ghi cũ và hiện chuyên trách phục vụ quản lý **các khoản Thưởng (Bonus)** với `loai_khoan = 'THUONG'`. Cột `chi_tiet_phieu_luong.ma_khoan` trỏ về bảng này để liên kết các dòng tiền thưởng.

### Bảng 17: `cau_hinh_phu_cap` (Cấu hình phụ cấp)
- `ma_phu_cap` (SERIAL, PK): Mã khoản phụ cấp.
- `ten_phu_cap` (VARCHAR(150), NOT NULL, UNIQUE): Phụ cấp ăn ca, Phụ cấp ca đêm 30%, Phụ cấp đi lại, Phụ cấp trách nhiệm...
- `loai_tinh` (VARCHAR(30), NOT NULL, CHECK in ('THEO_NGAY_CONG', 'CO_DINH_THANG')): Cơ chế tính — nhân với số ngày công thực tế hoặc cộng cố định theo tháng.
- `muc_dinh_muc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Định mức tiền phụ cấp áp dụng.
- `chiu_thue_tncn` (BOOLEAN, NOT NULL, DEFAULT FALSE): Quy định khoản phụ cấp có tính vào thu nhập chịu thuế TNCN hay không.
- `mo_ta` (VARCHAR(255), NULL): Ghi chú chính sách phụ cấp khách sạn.

### Bảng 18: `cau_hinh_khau_tru` (Cấu hình khoản khấu trừ)
- `ma_khoan_tru` (SERIAL, PK): Mã khoản khấu trừ.
- `ten_khoan_tru` (VARCHAR(150), NOT NULL, UNIQUE): BHXH 8%, BHYT 1.5%, BHTN 1%, Thuế TNCN, Phạt đi trễ, Phạt về sớm, Bỏ ca... (không áp dụng tạm ứng lương).
- `loai_khoan_tru` (VARCHAR(30), NOT NULL, CHECK in ('BAO_HIEM', 'THUE', 'VI_PHAM', 'TAM_UNG')): Nhóm phân loại khấu trừ.
- `ty_le_hoac_dinh_muc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tỷ lệ phần trăm (%) áp dụng hoặc định mức tiền phạt/khấu trừ cố định.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải căn cứ pháp lý và nội quy khách sạn.

### Bảng 19: `cau_hinh_tang_ca` (Cấu hình tăng ca)
- `ma_loai_ot` (SERIAL, PK): Mã loại tăng ca.
- `ten_loai_ot` (VARCHAR(100), NOT NULL, UNIQUE): Tăng ca ngày thường, Tăng ca ngày nghỉ tuần, Tăng ca đêm, Tăng ca ngày lễ tết...
- `he_so` (DECIMAL(4,2), NOT NULL, CHECK > 0): Hệ số nhân tiền lương theo giờ OT (VD: ngày thường 1.5, ngày nghỉ 2.0, ngày lễ 3.0).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải quy định áp dụng.

### Bảng 20: `phieu_luong` (Phiếu lương tổng hợp)
- `ma_phieu_luong` (SERIAL, PK): Mã phiếu lương.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Nhân viên nhận lương.
- `ma_ky_luong` (INT, FK -> ky_luong.ma_ky_luong, NOT NULL, ON DELETE RESTRICT): Kỳ lương tương ứng.
- `luong_co_ban` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Lương cơ bản snapshot tại thời điểm tính lương.
- `ngay_cong_thuc_te` (DECIMAL(4,2), NOT NULL, DEFAULT 0.00): Số ngày công thực tế làm việc trong kỳ.
- `tien_luong_cong` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tiền lương theo công thức 2 tầng = Lương cơ bản × (Ngày công thực tế / 26).
- `tien_tang_ca` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng tiền làm thêm giờ đã duyệt trong kỳ.
- `tong_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng các khoản phụ cấp (ăn ca, đêm, đi lại, thù lao dự án...).
- `tong_thuong` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng tiền thưởng hiệu suất KPI / lễ tết trong kỳ.
- `tong_khau_tru` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng các khoản khấu trừ (Bảo hiểm 10.5%, Thuế TNCN, vi phạm kỷ luật).
- `thuc_nhan` (DECIMAL(15,2), NOT NULL): Lương Net thực chuyển khoản = (Tiền lương công + Tăng ca + Phụ cấp + Thưởng) - Khấu trừ.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'NHAP', CHECK in ('NHAP', 'DA_DUYET', 'DA_THANH_TOAN')): Trạng thái duyệt và chi trả.
- *Ràng buộc:* `UNIQUE (ma_nhan_vien, ma_ky_luong)` — Mỗi nhân viên chỉ có đúng một phiếu lương tổng hợp cho mỗi kỳ tính lương.

### Bảng 21: `chi_tiet_phieu_luong` (Chi tiết phiếu lương)
- `ma_chi_tiet` (SERIAL, PK): Mã bản ghi dòng chi tiết thu chi.
- `ma_phieu_luong` (INT, FK -> phieu_luong.ma_phieu_luong, NOT NULL, ON DELETE CASCADE): Phiếu lương chủ.
- `ten_khoan_luu` (VARCHAR(150), NOT NULL): **Snapshot tên khoản** tại thời điểm phát hành (bảo toàn lịch sử tài chính bất biến).
- `loai_khoan_luu` (VARCHAR(30), NOT NULL): **Snapshot loại khoản** (`PHU_CAP` / `THUONG` / `KHAU_TRU`).
- `so_tien` (DECIMAL(15,2), NOT NULL): Số tiền cụ thể cộng hoặc trừ.
- `ghi_chu` (VARCHAR(255), NULL): Diễn giải công thức hoặc nguồn phát sinh dòng tiền.
- `ma_khoan` (INT, FK -> cau_hinh_khoan_luong.ma_khoan, NULL, ON DELETE RESTRICT): Khoản thưởng hoặc khoản định mức Legacy tham chiếu.
- `ma_loai_ot` (INT, FK -> cau_hinh_tang_ca.ma_loai_ot, NULL, ON DELETE RESTRICT): Loại tăng ca phát sinh dòng này.
- `ma_phu_cap` (INT, FK -> cau_hinh_phu_cap.ma_phu_cap, NULL, ON DELETE RESTRICT): Khoản phụ cấp chuẩn phát sinh dòng này.
- `ma_khoan_tru` (INT, FK -> cau_hinh_khau_tru.ma_khoan_tru, NULL, ON DELETE RESTRICT): Khoản khấu trừ chuẩn phát sinh dòng này.
- `loai_khoan` (VARCHAR(30), NULL): Nhóm phân loại dòng chi tiết.

### Bảng 22: `chi_tiet_tru_bao_hiem` (Chi tiết khấu trừ bảo hiểm)
- `ma_chi_tiet_bh` (SERIAL, PK): Mã bản ghi bảo hiểm.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (Quan hệ 1-1).
- `luong_dong_bao_hiem` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Mức lương làm căn cứ trích đóng bảo hiểm.
- `ty_le_bhxh` (DECIMAL(5,2), NOT NULL, DEFAULT 8.00): Tỷ lệ đóng BHXH người lao động (8%).
- `tien_bhxh` (DECIMAL(15,2), NOT NULL): Số tiền BHXH trích trừ.
- `ty_le_bhyt` (DECIMAL(5,2), NOT NULL, DEFAULT 1.50): Tỷ lệ đóng BHYT người lao động (1.5%).
- `tien_bhyt` (DECIMAL(15,2), NOT NULL): Số tiền BHYT trích trừ.
- `ty_le_bhtn` (DECIMAL(5,2), NOT NULL, DEFAULT 1.00): Tỷ lệ đóng BHTN người lao động (1%).
- `tien_bhtn` (DECIMAL(15,2), NOT NULL): Số tiền BHTN trích trừ.
- `tong_bao_hiem` (DECIMAL(15,2), NOT NULL): Tổng cả 3 khoản bảo hiểm bắt buộc trích trừ trong dòng này (10.5%).

### Bảng 23: `chi_tiet_tru_thue_tncn` (Chi tiết khấu trừ thuế TNCN)
- `ma_chi_tiet_thue` (SERIAL, PK): Mã bản ghi thuế TNCN.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (Quan hệ 1-1).
- `thu_nhap_chiu_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Tổng thu nhập chịu thuế trong kỳ.
- `giam_tru_ban_than` (DECIMAL(15,2), NOT NULL, DEFAULT 15500000.00): Mức giảm trừ gia cảnh cho bản thân người nộp thuế (15.500.000 VNĐ/tháng).
- `so_nguoi_phu_thuoc` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Số lượng người phụ thuộc đăng ký hợp lệ.
- `giam_tru_phu_thuoc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng tiền giảm trừ người phụ thuộc (6.200.000 VNĐ/người/tháng theo quy định mới).
- `thu_nhap_tinh_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Thu nhập tính thuế sau giảm trừ.
- `tien_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Số tiền thuế TNCN trích nộp theo biểu lũy tiến từng phần.

### Bảng 24: `chi_tiet_tru_vi_pham_tam_ung` (Chi tiết khấu trừ vi phạm & khấu trừ khác - bỏ tạm ứng)
- `ma_chi_tiet_vp` (SERIAL, PK): Mã bản ghi vi phạm / tạm ứng.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (Quan hệ 1-1).
- `loai_tru` (VARCHAR(30), NOT NULL, CHECK in ('DI_TRE', 'VE_SOM', 'KY_LUAT', 'LAM_HONG', 'TAM_UNG')): Phân loại vi phạm kỷ luật hoặc khấu trừ khác (chính sách hiện tại không áp dụng tạm ứng).
- `so_phut_vi_pham` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Số phút vi phạm áp dụng đối với lỗi đi trễ / về sớm.
- `so_tien_tru` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Số tiền thực tế khấu trừ vào lương.
- `ly_do_chi_tiet` (VARCHAR(255), NOT NULL): Diễn giải cụ thể căn cứ trừ tiền.

### Bảng 25: `nhat_ky_quan_tri` (Nhật ký quản trị - Audit Log)
- `ma_nhat_ky` (BIGSERIAL, PK): Mã nhật ký tự tăng liên tục.
- `ma_tai_khoan_thuc_hien` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản thực hiện thao tác.
- `ma_tai_khoan_bi_tac_dong` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản bị tác động.
- `ten_nguoi_thuc_hien` (VARCHAR(100), NOT NULL): Snapshot họ tên người thực hiện (bảo lưu dấu vết kể cả khi tài khoản bị xóa).
- `ten_tai_khoan_bi_tac_dong` (VARCHAR(100), NOT NULL): Snapshot tên tài khoản bị tác động.
- `hanh_dong` (VARCHAR(40), NOT NULL): Mã thao tác (`TAO_MOI`, `CAP_NHAT`, `XOA`, `KHOA_TAI_KHOAN`, `DUYET_DON`, `CHOT_KY_LUONG`...).
- `noi_dung` (VARCHAR(500), NOT NULL): Mô tả chi tiết hành động quản trị.
- `gia_tri_cu` (VARCHAR(1000), NULL): Dữ liệu trước thay đổi (chuỗi JSON/Text).
- `gia_tri_moi` (VARCHAR(1000), NULL): Dữ liệu sau thay đổi (chuỗi JSON/Text).
- `ly_do` (VARCHAR(500), NULL): Lý do can thiệp hệ thống.
- `dia_chi_ip` (VARCHAR(64), NULL): Địa chỉ IP thiết bị phát sinh thao tác.
- `thoi_gian` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Mốc thời gian ghi nhận hành động.

### Bảng 26: `du_an` (Dự án / Sự kiện Khách sạn)
- `ma_du_an` (SERIAL, PK): Mã định danh duy nhất của dự án / sự kiện.
- `ten_du_an` (VARCHAR(200), NOT NULL, UNIQUE): Tên dự án, sự kiện tổ chức tại khách sạn (VD: "Tiệc cưới Hoàng Gia - Sảnh Diamond", "Hội nghị Thượng đỉnh Kinh tế MICE 2026", "Festival Ẩm thực Mùa Thu"...).
- `loai_du_an` (VARCHAR(50), NOT NULL, CHECK in ('TIEC_CUOI', 'HOI_NGHI_MICE', 'GALA_DINNER', 'FESTIVAL_AM_THUC', 'DOAN_VIP', 'BAO_TRI_NANG_CAP')): Phân loại tính chất sự kiện để điều phối nguồn lực phù hợp.
- `dia_diem` (VARCHAR(255), NOT NULL): Khu vực tổ chức trong khách sạn (Grand Ballroom, Lotus Hall, Rooftop Sky Bar, Hồ bơi tầng 5, Phòng VIP 888...).
- `ngay_bat_dau` (TIMESTAMPTZ, NOT NULL): Thời điểm bắt đầu triển khai chuẩn bị sự kiện.
- `ngay_ket_thuc` (TIMESTAMPTZ, NOT NULL): Thời điểm kết thúc, bàn giao hiện trường và nghiệm thu sự kiện.
- `ngan_sach_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00, CHECK >= 0): Tổng ngân sách phân bổ chi trả thù lao / phụ cấp tăng cường cho nhân sự tham gia sự kiện.
- `ma_nguoi_quan_ly` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Trưởng ban tổ chức / Quản lý điều hành dự án sự kiện (thường là Trưởng bộ phận Event/F&B/Sảnh).
- `mo_ta` (TEXT, NULL): Kịch bản chi tiết, yêu cầu phục vụ đặc biệt, quy mô số lượng khách, quy chuẩn an ninh và kỹ thuật.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'LEN_KE_HOACH', CHECK in ('LEN_KE_HOACH', 'DANG_DIEN_RA', 'HOAN_THANH', 'HUY')): Tiến độ vòng đời sự kiện.
- `ngay_tao` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm khởi tạo dự án sự kiện.
- `so_khach_du_kien` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Quy mô khách dự kiến tham gia.
- `ly_do_huy` (VARCHAR(255), NULL): Lý do hủy sự kiện (nếu trạng thái là `HUY`).
- `version` (BIGINT, NOT NULL, DEFAULT 1): Số hiệu phiên bản kiểm soát tương tranh lạc quan (Optimistic Concurrency Control).
- `tinh_phu_cap_khi_nghiem_thu` (BOOLEAN, NOT NULL, DEFAULT TRUE): Cờ chỉ định tự động kết chuyển phụ cấp sự kiện khi hoàn tất nghiệm thu.
- `tinh_theo_gio` (BOOLEAN, NOT NULL, DEFAULT FALSE): Cách tính thù lao theo giờ làm thực tế hay trọn gói sự kiện.
- `ket_thuc_thuc_te` (TIMESTAMPTZ, NULL): Thời điểm thực tế sự kiện kết thúc.
- `ly_do_dung` (VARCHAR(255), NULL): Lý do dừng sự kiện giữa chừng nếu có sự cố.
- `ma_nguoi_nghiem_thu` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Quản lý tiến hành nghiệm thu chất lượng sự kiện.
- `ngay_nghiem_thu` (TIMESTAMPTZ, NULL): Thời điểm ký duyệt nghiệm thu.
- `ma_dia_diem` (INT, NULL): Khóa ngoại tham chiếu bảng địa điểm tổ chức trong khách sạn.
- `phut_chuan_bi` (INT, NOT NULL, DEFAULT 0): Số phút chuẩn bị hiện trường trước sự kiện.
- `phut_thu_don` (INT, NOT NULL, DEFAULT 0): Số phút thu dọn và hoàn trả mặt bằng sau sự kiện.

### Bảng 27: `phan_cong_du_an` (Phân công nhân sự & Thù lao sự kiện)
- `ma_phan_cong` (SERIAL, PK): Mã bản ghi phân công nhân sự.
- `ma_du_an` (INT, FK -> du_an.ma_du_an, NOT NULL, ON DELETE CASCADE): Dự án / sự kiện tiếp nhận nhân sự.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên được điều động tham gia sự kiện.
- `vai_tro_du_an` (VARCHAR(100), NOT NULL): Vai trò chuyên môn được giao trong sự kiện (`DIEU_PHOI`, `KY_THUAT_AM_THANH_AS`, `PHUC_VU_TIEC`, `LE_TAN_DON_KHACH`, `AN_NINH_VIP`, `BEP_TIEC`...).
- `nhiem_vu_cu_the` (VARCHAR(255), NULL): Chi tiết công việc cụ thể tại vị trí trực chiến.
- `so_tien_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00, CHECK >= 0): Mức thù lao / phụ cấp sự kiện thực tế chi trả sau đánh giá.
- `danh_gia` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_DANH_GIA', CHECK in ('CHUA_DANH_GIA', 'HOAN_THANH', 'XUAT_SAC', 'KHONG_THAM_GIA')): Kết quả đánh giá hiệu suất của quản lý sau khi kết thúc sự kiện.
- `trang_thai_chi_tra` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_CHI_TRA', CHECK in ('CHUA_CHI_TRA', 'DA_KET_CHUYEN_LUONG', 'HUY_BO')): Trạng thái thanh toán thù lao. Khi tính lương tháng, các khoản đã nghiệm thu sẽ được chuyển sang `DA_KET_CHUYEN_LUONG`.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NULL, ON DELETE SET NULL): Khóa ngoại liên kết sang dòng chi tiết phiếu lương sau khi kết chuyển thù lao vào kỳ lương tháng (tạo liên kết 2 chiều minh bạch).
- `ngay_danh_gia` (TIMESTAMPTZ, NULL): Thời điểm quản lý nghiệm thu và chấm điểm đánh giá.
- `ngay_phan_cong` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm lập phiếu điều động nhân sự.
- `phu_cap_du_kien` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Mức thù lao dự kiến phân bổ ban đầu.
- `bat_dau` (TIMESTAMPTZ, NOT NULL): Thời điểm bắt đầu tham gia phục vụ sự kiện.
- `ket_thuc` (TIMESTAMPTZ, NOT NULL): Thời điểm kết thúc tham gia phục vụ sự kiện.
- `ma_phong_ban_su_kien` (VARCHAR(50), NULL): Nhóm bộ phận phân bổ tại sự kiện.
- `ma_phan_ca` (INT, FK -> phan_ca.ma_phan_ca, NULL, ON DELETE SET NULL): Ca trực chính thức liên kết phục vụ sự kiện.
- `trang_thai_tham_gia` (VARCHAR(30), NOT NULL, DEFAULT 'DA_PHAN', CHECK in ('DA_PHAN', 'DANG_THAM_GIA', 'HOAN_THANH', 'VANG_MAT', 'HUY_PHAN_CONG')): Trạng thái tham gia thực tế của nhân sự.
- `ly_do_ket_thuc` (VARCHAR(255), NULL): Ghi chú khi kết thúc nhiệm vụ sự kiện hoặc lý do rút lui.
- *Ràng buộc:* `UNIQUE (ma_du_an, ma_nhan_vien)` — Mỗi nhân viên chỉ được phân công một vị trí chính trong một dự án sự kiện để đảm bảo tập trung trách nhiệm và tránh phân bổ trùng lặp thù lao.

### Bảng 28: `dinh_bien_ca` (Định biên nhân sự theo ca, chức vụ & bộ phận)
- `ma_dinh_bien` (SERIAL, PK): Mã định danh duy nhất của dòng định biên.
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL, ON DELETE CASCADE): Ca làm việc được đặt định biên.
- `ma_chuc_vu` (INT, FK -> chuc_vu.ma_chuc_vu, NOT NULL, ON DELETE CASCADE): Chức vụ / vị trí công tác cần đếm số người.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NULL, ON DELETE CASCADE): Bộ phận áp dụng định biên. `NULL` nghĩa là **dùng chung mọi bộ phận**. Cột này cần thiết vì `chuc_vu` có những chức danh mang `ma_phong_ban = NULL` — "Trưởng Bộ Phận" (5 người / 5 bộ phận) và "Giám Sát Ca" (2 người / 2 bộ phận) — cùng một chức danh nhưng trải trên nhiều nơi. Thiếu chiều bộ phận thì không đặt được "Bếp cần 1 Giám Sát Ca ca đêm" mà chỉ đặt được một con số gộp cho toàn khách sạn.
- `so_nguoi_toi_thieu` (INT, NOT NULL, DEFAULT 1, CHECK >= 1): Số nhân sự tối thiểu phải có mặt trong ca để vận hành không bị gián đoạn (VD: ca Đêm cần 1 Lễ tân + 1 Bảo vệ + 1 Kỹ thuật trực).
- *Ràng buộc:* `UNIQUE NULLS NOT DISTINCT (ma_ca, ma_chuc_vu, ma_phong_ban)` — Mỗi bộ ba Ca × Chức vụ × Bộ phận chỉ có một dòng định biên. Mệnh đề `NULLS NOT DISTINCT` (PostgreSQL 15+) là bắt buộc: mặc định Postgres coi mọi `NULL` là khác nhau, nên nếu thiếu nó thì hai dòng "dùng chung" của cùng một cặp Ca × Chức vụ đều lọt qua ràng buộc.
- *Nghiệp vụ:* Màn **Phân ca** (PRD 3.5) đối chiếu số nhân sự đã xếp của mỗi ca với bảng này để gắn nhãn cảnh báo **thiếu người** ngay trên lịch, thay vì để Quản lý tự đếm thủ công. Cảnh báo nêu rõ tên bộ phận đang thiếu (VD: "Giám Sát Ca · Bộ phận Bếp 0/1").

### Bảng 29: `giai_trinh_cham_cong` (Giải trình sai lệch giờ chấm công)
- `ma_giai_trinh` (SERIAL, PK): Mã định danh hồ sơ giải trình.
- `ma_phan_ca` (INT, FK -> phan_ca.ma_phan_ca, NOT NULL, ON DELETE CASCADE): Lượt ca bị giải trình; xoá ca thì hồ sơ giải trình đi theo.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên đứng tên giải trình.
- `gio_de_xuat_vao` (TIMESTAMPTZ, NULL): Giờ vào thực tế đề nghị ghi nhận; để trống nếu chỉ điều chỉnh giờ ra.
- `gio_de_xuat_ra` (TIMESTAMPTZ, NULL): Giờ ra thực tế đề nghị ghi nhận; để trống nếu chỉ điều chỉnh giờ vào.
- `ly_do` (VARCHAR(500), NOT NULL): Nội dung giải trình (quên quẹt thẻ, hỏng đầu đọc, kẹt khách...). Trần 500 ký tự khớp đúng cột CSDL — vượt hạn mức sẽ bị PostgreSQL từ chối với mã lỗi `22001`.
- `minh_chung` (VARCHAR(500), NULL): Mã biên bản giao ca, số điện thoại tổ trưởng, đường dẫn ảnh chụp… làm căn cứ xác minh.
- `trang_thai` (VARCHAR(20), NOT NULL, DEFAULT 'CHO_QUAN_LY', CHECK in ('CHO_QUAN_LY', 'CHO_HR', 'DA_DUYET', 'TU_CHOI')): Tiến trình duyệt 2 cấp — Quản lý bộ phận xác minh, rồi HR xác nhận; chỉ khi `DA_DUYET` giờ công mới được ghi lại.
- `ma_quan_ly_xac_minh` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Quản lý cấp 1 đã xác minh có mặt.
- `ngay_quan_ly_xac_minh` (TIMESTAMPTZ, NULL): Thời điểm Quản lý xác minh.
- `y_kien_quan_ly` (VARCHAR(500), NULL): Ý kiến / lý do từ chối của cấp Quản lý.
- `ma_hr_xac_nhan` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Cán bộ HR đã xác nhận cấp 2.
- `ngay_hr_xac_nhan` (TIMESTAMPTZ, NULL): Thời điểm HR xác nhận.
- `y_kien_hr` (VARCHAR(500), NULL): Ý kiến / lý do từ chối của cấp HR.
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm nhân viên gửi hồ sơ.
- *Ràng buộc:*
  - `chk_giai_trinh_can_gio` — `CHECK (gio_de_xuat_vao IS NOT NULL OR gio_de_xuat_ra IS NOT NULL)`: không chấp nhận hồ sơ không đề xuất mốc giờ nào.
  - `chk_giai_trinh_tu_choi_can_y_kien` — từ chối bắt buộc kèm ý kiến của cấp ra quyết định, không để nhân viên bị từ chối mà không biết lý do.
  - `UNIQUE INDEX uq_giai_trinh_dang_mo ON giai_trinh_cham_cong(ma_phan_ca) WHERE trang_thai IN ('CHO_QUAN_LY','CHO_HR')` — mỗi lượt ca chỉ mở **một** hồ sơ đang chờ duyệt tại một thời điểm, chặn gửi trùng lặp.

### Bảng 30: `yeu_cau_doi_ca` (Yêu cầu đổi ca trực giữa nhân viên)
- `ma_yeu_cau` (SERIAL, PK): Mã định danh yêu cầu đổi ca.
- `ma_phan_ca_gui` (INT, FK -> phan_ca.ma_phan_ca, NOT NULL, ON DELETE CASCADE): Lượt ca nhân viên gửi nhường lại.
- `ma_phan_ca_nhan` (INT, FK -> phan_ca.ma_phan_ca, NOT NULL, ON DELETE CASCADE): Lượt ca nhân viên nhận làm thay.
- `ma_nhan_vien_gui` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Nhân viên gửi yêu cầu; chặn xoá hồ sơ nhân viên còn yêu cầu đang lưu vết.
- `ma_nhan_vien_nhan` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Nhân viên nhận ca.
- `ly_do` (VARCHAR(500), NOT NULL): Lý do đổi ca.
- `trang_thai` (VARCHAR(20), NOT NULL, DEFAULT 'CHO_DUYET', CHECK in ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'DA_HUY')): Vòng đời yêu cầu; `DA_HUY` dành cho trường hợp người gửi tự rút trước khi có quyết định.
- `ma_nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Quản lý / HR đã xử lý yêu cầu.
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm gửi yêu cầu.
- `ngay_duyet` (TIMESTAMPTZ, NULL): Thời điểm ra quyết định duyệt / từ chối.
- `phan_hoi_duyet` (VARCHAR(500), NULL): Phản hồi của người duyệt.
- `ben_tu_choi` (VARCHAR(50), NULL): Bên từ chối yêu cầu đổi ca (`NGUOI_NHAN` hoặc `QUAN_LY`).
- `phan_hoi_nguoi_nhan` (VARCHAR(500), NULL): Ý kiến đồng ý hoặc lý do từ chối từ đồng nghiệp được đề nghị đổi ca.
- `ngay_nguoi_nhan_phan_hoi` (TIMESTAMPTZ, NULL): Thời điểm đồng nghiệp phản hồi đồng ý hoặc từ chối.
- *Ràng buộc:*
  - `chk_doi_ca_khac_ca` — `CHECK (ma_phan_ca_gui <> ma_phan_ca_nhan)`: không cho "đổi" một ca với chính nó.
  - `chk_doi_ca_khac_nguoi` — `CHECK (ma_nhan_vien_gui <> ma_nhan_vien_nhan)`: không cho tự đổi ca với chính mình.
  - `chk_doi_ca_tu_choi_can_ly_do` — từ chối bắt buộc kèm phản hồi, đảm bảo nhân viên luôn biết lý do không được đổi.

---

### 2.1. Nhóm 7 Bảng Mở Rộng Chuyên Sâu (Phân Hệ Sự Kiện, Ngân Sách & Vận Hành)

Bên cạnh 30 bảng nghiệp vụ cốt lõi, CSDL Supabase Cloud của khách sạn còn mở rộng thêm **7 bảng chuyên sâu** phục vụ quản lý sự kiện khách sạn 5★ (Phân hệ 9), lập kế hoạch ngân sách quỹ lương (Phân hệ 10) và quản lý công bố lịch ca:

#### Bảng 31: `dia_diem_su_kien` (Khu vực / Sảnh tổ chức sự kiện)
- `ma_dia_diem` (SERIAL, PK): Mã định danh địa điểm.
- `ten_dia_diem` (VARCHAR(150), NOT NULL, UNIQUE): Tên sảnh tiệc / hội trường (Grand Ballroom, Lotus Hall, Rooftop Sky Bar...).
- `suc_chua` (INT, NOT NULL, CHECK >= 0): Sức chứa tối đa số lượng khách.
- `dang_su_dung` (BOOLEAN, NOT NULL, DEFAULT TRUE): Trạng thái sẵn sàng tiếp nhận sự kiện.

#### Bảng 32: `nhu_cau_su_kien` (Định mức nhân sự cần thiết theo sự kiện)
- `ma_nhu_cau` (SERIAL, PK): Mã dòng nhu cầu.
- `ma_du_an` (INT, FK -> du_an.ma_du_an, NOT NULL, ON DELETE CASCADE): Sự kiện cần nhân lực.
- `ma_phong_ban` (VARCHAR(50), NOT NULL): Bộ phận cung cấp nhân sự (F&B, HK, An ninh...).
- `vai_tro` (VARCHAR(100), NOT NULL): Vị trí công tác cần bổ sung.
- `so_nguoi` (INT, NOT NULL, CHECK >= 1): Số lượng nhân sự cần thiết.
- `la_mau` (BOOLEAN, NOT NULL, DEFAULT FALSE): Đánh dấu định mức mẫu cho các sự kiện cùng loại.

#### Bảng 33: `cong_su_kien` (Ghi nhận công sự kiện thực tế)
- `ma_cong` (SERIAL, PK): Mã bản ghi công sự kiện.
- `ma_phan_cong` (INT, FK -> phan_cong_du_an.ma_phan_cong, NOT NULL, ON DELETE CASCADE): Lượt phân công tương ứng.
- `bat_dau` (TIMESTAMPTZ, NOT NULL): Thời gian bắt đầu làm việc thực tế tại sự kiện.
- `ket_thuc` (TIMESTAMPTZ, NOT NULL): Thời gian kết thúc công việc thực tế.
- `ma_nguoi_xac_nhan` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Quản lý sự kiện xác nhận giờ công.
- `ngay_xac_nhan` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Mốc thời gian xác nhận.

#### Bảng 34: `quyet_toan_su_kien` (Quyết toán thù lao sự kiện vào kỳ lương)
- `ma_quyet_toan` (SERIAL, PK): Mã quyết toán.
- `ma_du_an` (INT, FK -> du_an.ma_du_an, NOT NULL, ON DELETE CASCADE): Dự án sự kiện đã hoàn tất.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân sự nhận thù lao.
- `thang_phat_sinh` (DATE, NOT NULL): Tháng phát sinh sự kiện.
- `ma_ky_chi_tra` (INT, FK -> ky_luong.ma_ky_luong, NOT NULL, ON DELETE RESTRICT): Kỳ lương chi trả thù lao.
- `so_phut` (NUMERIC, NOT NULL, CHECK >= 0): Tổng số phút phục vụ sự kiện thực tế.
- `so_tien` (NUMERIC, NOT NULL, CHECK >= 0): Số tiền thù lao quyết toán.
- `can_cu_tinh` (TEXT, NOT NULL): Diễn giải công thức và căn cứ tính thù lao.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NULL, ON DELETE SET NULL): Liên kết sang dòng phụ cấp trên phiếu lương.
- `ma_nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Người duyệt quyết toán.
- `ngay_duyet` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm duyệt quyết toán.

#### Bảng 35: `ngan_sach_luong` (Lập kế hoạch & kiểm soát ngân sách quỹ lương)
- `ma_ngan_sach` (UUID, PK, DEFAULT gen_random_uuid()): Mã định danh ngân sách.
- `version` (BIGINT, NOT NULL, DEFAULT 1): Phiên bản kiểm soát tương tranh.
- `nam` (INT, NOT NULL): Năm tài chính áp dụng.
- `thang` (INT, NOT NULL, CHECK (thang BETWEEN 1 AND 12)): Tháng áp dụng ngân sách.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NOT NULL, ON DELETE RESTRICT): Bộ phận được phân bổ ngân sách quỹ lương.
- `so_tien_ngan_sach` (NUMERIC, NOT NULL, CHECK >= 0): Hạn mức quỹ lương tối đa cho phép.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DU_THAO', CHECK in ('DU_THAO', 'CHO_DUYET', 'DA_DUYET', 'KHOA')): Trạng thái phê duyệt ngân sách.
- `nguoi_tao` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Người lập kế hoạch ngân sách.
- `ngay_tao` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm tạo ngân sách.
- `nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Ban Giám đốc phê duyệt hạn mức.
- `ngay_duyet` (TIMESTAMPTZ, NULL): Thời điểm phê duyệt.
- `ghi_chu` (TEXT, NULL): Thuyết minh ngân sách.

#### Bảng 36: `cong_bo_lich_tuan` (Lịch sử công bố lịch phân ca theo tuần)
- `dau_tuan` (DATE, PK): Ngày thứ Hai đầu tuần công bố lịch.
- `version` (BIGINT, NOT NULL, DEFAULT 1): Số phiên bản công bố lịch.
- `ma_nguoi_cong_bo` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Cán bộ HR hoặc Quản lý thực hiện công bố.
- `ngay_cong_bo` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm công bố lịch tuần cho Mobile App.
- `ly_do` (VARCHAR(255), NULL): Ghi chú hoặc lý do tái công bố lịch.

#### Bảng 37: `dinh_bien_ngay` (Định biên nhân sự linh hoạt theo ngày cụ thể)
- `ma_dinh_bien_ngay` (SERIAL, PK): Mã bản ghi định biên ngày.
- `ngay` (DATE, NOT NULL): Ngày cụ thể áp dụng định biên đặc biệt (ngày lễ, ngày sự kiện cao điểm).
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL, ON DELETE CASCADE): Ca làm việc áp dụng.
- `ma_chuc_vu` (INT, FK -> chuc_vu.ma_chuc_vu, NOT NULL, ON DELETE CASCADE): Chức vụ cần tăng cường nhân sự.
- `so_nguoi_toi_thieu` (INT, NOT NULL, DEFAULT 1, CHECK >= 1): Số nhân sự tối thiểu trong ngày đó.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NULL, ON DELETE CASCADE): Bộ phận áp dụng (NULL = dùng chung).

---

## 3. Kiến Trúc Bảo Mật Dữ Liệu & Row Level Security (RLS) trên Supabase

### 3.1. Bản Chất Kỹ Thuật: RLS (Row Level Security) là gì trên Supabase / PostgreSQL?

#### Khái niệm & Vị trí trong ngăn xếp CSDL
**Row Level Security (RLS)** — Bảo mật cấp độ hàng — là cơ chế kiểm soát truy cập dữ liệu chi tiết (fine-grained access control) được tích hợp trực tiếp vào **nhân (Kernel)** của hệ quản trị CSDL PostgreSQL (chính thức từ PostgreSQL 9.5+). 

Trong mô hình kiến trúc phần mềm truyền thống (3-Tier), việc bảo mật và cách ly dữ liệu phụ thuộc hoàn toàn vào tầng ứng dụng (Application Layer): lập trình viên phải nhớ gắn mệnh đề `WHERE user_id = @currentUserId` trong từng câu truy vấn SQL hoặc LINQ. Mô hình này tiềm ẩn rủi ro nghiêm trọng: chỉ cần một lập trình viên sơ suất bỏ quên mệnh đề `WHERE`, hoặc xảy ra lỗi SQL Injection, dữ liệu nhạy cảm của toàn bộ khách sạn (tiền lương, hợp đồng, hồ sơ cá nhân) sẽ bị rò rỉ (Data Leakage) hoặc bị sửa đổi trái phép.

Ngược lại, **RLS trên PostgreSQL chuyển dịch toàn bộ trách nhiệm bảo vệ dữ liệu xuống tầng CSDL (Zero-Trust Data Layer)**:
- Mỗi bảng dữ liệu được kích hoạt cờ `ENABLE ROW LEVEL SECURITY`.
- Khi người dùng gửi một câu truy vấn thông thường (ví dụ: `SELECT * FROM phieu_luong;`), bộ lập kế hoạch truy vấn của PostgreSQL (Query Planner / Query Rewriter) sẽ **tự động viết lại câu lệnh SQL một cách vô hình (Invisible Query Rewrite)** trước khi thực thi:
  ```sql
  -- Câu lệnh Client gửi lên:
  SELECT * FROM phieu_luong;

  -- Câu lệnh PostgreSQL thực tế biên dịch và thực thi dưới RLS:
  SELECT * FROM phieu_luong 
  WHERE (ma_nhan_vien = public.nhan_vien_hien_tai() OR public.vai_tro_hien_tai() IN (1, 2));
  ```
- Kết quả: Người dùng chỉ có thể "nhìn thấy" và thao tác đúng những hàng dữ liệu mà chính sách cho phép. Dù client có cố tình bỏ qua bộ lọc trên giao diện hay cố ý chèn payload độc hại qua API, CSDL vẫn áp đặt bộ lọc phần cứng không thể vượt qua.

#### Cơ chế hoạt động của RLS trên Supabase Cloud qua PostgREST
Khi ứng dụng di động **The OverTime Hotel Mobile App** (.NET MAUI) kết nối trực tiếp với Supabase Cloud theo mô hình BaaS (xem chi tiết tại `PRD/08_Kien_Truc_Ket_Noi_Mobile_Va_Danh_Gia_Uu_Nhuoc_Diem.md`), luồng kiểm tra RLS diễn ra như sau:
1. **Tiếp nhận HTTP Request:** Client gửi yêu cầu HTTPS tới endpoint PostgREST (ví dụ: `GET /rest/v1/cham_cong`) kèm header `Authorization: Bearer <JWT_ACCESS_TOKEN>`.
2. **Khởi tạo Transaction & Thiết lập Phiên:** PostgREST mở một PostgreSQL Transaction và giải mã JWT Token (được ký bởi GoTrue Auth bằng HMAC-SHA256 hoặc Asymmetric Key). PostgREST tự động chạy hai lệnh nội bộ:
   ```sql
   SET LOCAL request.jwt.claims = '{"sub":"...", "email":"...", "app_metadata":{"ma_nhan_vien": 12, "ma_vai_tro": 4, "quyen": [...]}}';
   SET LOCAL role = 'authenticated';
   ```
3. **Thực thi với Role `authenticated`:** Toàn bộ câu lệnh SQL trong transaction này chạy với danh tính của PostgreSQL Role `authenticated`.
4. **Áp đặt 2 mệnh đề then chốt của Policy:**
   - **`USING (expression)`:** Áp dụng cho các thao tác `SELECT`, `UPDATE`, `DELETE`. Mệnh đề này đóng vai trò như một bộ lọc `WHERE` tự động. Bất kỳ dòng dữ liệu nào trong bảng mà biểu thức `USING` trả về `FALSE` hoặc `NULL` sẽ bị loại bỏ hoàn toàn khỏi tập kết quả (với `SELECT`) hoặc bị xem như không tồn tại (với `UPDATE`, `DELETE`).
   - **`WITH CHECK (expression)`:** Áp dụng cho các thao tác `INSERT`, `UPDATE`. Mệnh đề này kiểm tra tính hợp lệ của dòng dữ liệu mới chuẩn bị được chèn vào hoặc trạng thái mới sau khi sửa đổi. Nếu biểu thức trả về `FALSE`, PostgreSQL sẽ hủy bỏ ngay transaction và ném mã lỗi `42501 (insufficient_privilege)`.

---

### 3.2. Ma Trận Toàn Bộ Chính Sách RLS Trên 30 Bảng CSDL

Trên Supabase Cloud của khách sạn The OverTime Hotel, **100% các bảng (30/30 bảng)** trong schema `public` đều đã được kích hoạt `ROW LEVEL SECURITY`. Không có bất kỳ bảng nào hoạt động ở trạng thái mở tự do (Unsecured).

#### Bảng Ma Trận Tổng Hợp Phân Quyền RLS 30 Bảng:

| STT | Tên Bảng CSDL | Tên Policy RLS | Lệnh Áp Dụng | Đối Tượng Áp Dụng | Điều Kiện Biểu Thức `USING` / `WITH CHECK` |
|:---:|:---|:---|:---:|:---:|:---|
| **1** | `phong_ban` | `doc_danh_muc_phong_ban` | SELECT | `authenticated` | `auth.uid() IS NOT NULL` (Đọc danh mục bộ phận). Ghi khóa chỉ dành cho `service_role`. |
| **2** | `chuc_vu` | `doc_danh_muc_chuc_vu` | SELECT | `authenticated` | `auth.uid() IS NOT NULL` (Đọc danh mục chức danh). Ghi khóa chỉ dành cho `service_role`. |
| **3** | `nhan_vien` | `doc_ho_so_nhan_vien` | SELECT | `authenticated` | `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()`. Nhân viên xem chính mình, Quản lý/HR/Admin xem theo thẩm quyền. |
| **4** | `hop_dong` | `doc_hop_dong` | SELECT | `authenticated` | `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.vai_tro_hien_tai() IN (1, 2)`. Nhân viên chỉ xem HĐ của mình, HR/Admin xem toàn viện. |
| **5** | `tai_khoan` | *(Không mở policy)* | ALL | `anon`, `authenticated` | **KHÓA TUYỆT ĐỐI KHỎI CLIENT DI ĐỘNG**. Chỉ `service_role` (Web Admin) mới có quyền truy cập để bảo vệ `mat_khau_bam`. |
| **6** | `vai_tro` | `doc_danh_muc_vai_tro` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc danh mục 5 vai trò. Ghi khóa chỉ dành cho `service_role`. |
| **7** | `quyen` | `doc_quyen` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc danh mục 17 mã quyền. |
| **8** | `vai_tro_quyen` | `doc_vai_tro_quyen` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc ma trận RBAC để Mobile App kiểm tra tính năng. |
| **9** | `yeu_cau_tai_khoan` | `doc_yeu_cau_tai_khoan`<br>`them_yeu_cau_tai_khoan`<br>`sua_yeu_cau_tai_khoan` | SELECT<br>INSERT<br>UPDATE | `authenticated` | • `doc_yeu_cau_tai_khoan`: `public.vai_tro_hien_tai() IN (1, 5) OR ma_nguoi_gui = public.nhan_vien_hien_tai()`<br>• `them_yeu_cau_tai_khoan`: `public.vai_tro_hien_tai() IN (1, 2)` (Chỉ HR/Admin gửi)<br>• `sua_yeu_cau_tai_khoan`: `public.vai_tro_hien_tai() IN (1, 5)` (Chỉ IT/Admin xử lý). |
| **10** | `ca_lam_viec` | `doc_danh_muc_ca_lam_viec` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Xem danh mục 3 ca chuẩn 24/7. |
| **11** | `phan_ca` | `doc_phan_ca`<br>`them_phan_ca_quan_ly`<br>`sua_phan_ca_quan_ly`<br>`sua_phan_ca_cua_toi` | SELECT<br>INSERT<br>UPDATE<br>UPDATE | `authenticated` | • `doc_phan_ca`: `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()`<br>• `them_phan_ca_quan_ly`: `WITH CHECK (public.la_quan_ly_hien_tai())`<br>• `sua_phan_ca_quan_ly`: `public.vai_tro_hien_tai() IN (1, 2) OR (public.vai_tro_hien_tai() = 3 AND EXISTS (SELECT 1 FROM nhan_vien nv WHERE nv.ma_nhan_vien = phan_ca.ma_nhan_vien AND nv.ma_phong_ban = public.phong_ban_hien_tai()))`<br>• `sua_phan_ca_cua_toi`: `ma_nhan_vien = public.nhan_vien_hien_tai()` (cho phép đổi trạng thái hoàn thành). |
| **12** | `cham_cong` | `doc_cham_cong`<br>`them_cham_cong_cua_toi`<br>`sua_cham_cong_quan_ly`<br>`sua_cham_cong_cua_toi` | SELECT<br>INSERT<br>UPDATE<br>UPDATE | `authenticated` | • `doc_cham_cong`: `EXISTS (SELECT 1 FROM phan_ca pc WHERE pc.ma_phan_ca = cham_cong.ma_phan_ca AND (pc.ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()))`<br>• `them_cham_cong_cua_toi`: `WITH CHECK (EXISTS (SELECT 1 FROM phan_ca pc WHERE pc.ma_phan_ca = cham_cong.ma_phan_ca AND pc.ma_nhan_vien = public.nhan_vien_hien_tai()))`<br>• `sua_cham_cong_quan_ly`: Quản lý phụ trách ca thuộc phòng ban hoặc HR/Admin<br>• `sua_cham_cong_cua_toi`: Nhân viên tự cập nhật giờ check-out ca của mình. |
| **13** | `loai_don` | `doc_danh_muc_loai_don` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc danh mục 4 loại đơn. |
| **14** | `don_tu` | `doc_don_tu`<br>`nop_don_tu_cua_toi`<br>`duyet_don_tu_quan_ly` | SELECT<br>INSERT<br>UPDATE | `authenticated` | • `doc_don_tu`: `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()`<br>• `nop_don_tu_cua_toi`: `WITH CHECK (ma_nhan_vien = public.nhan_vien_hien_tai())`<br>• `duyet_don_tu_quan_ly`: `(public.vai_tro_hien_tai() IN (1, 2) OR (public.vai_tro_hien_tai() = 3 AND public.phong_ban_cua_don(ma_don) = public.phong_ban_hien_tai())) AND ma_nhan_vien <> public.nhan_vien_hien_tai()` (**Chặn tuyệt đối tự duyệt đơn của mình!**). |
| **15** | `ky_luong` | `doc_ky_luong` | SELECT | `authenticated` | `public.vai_tro_hien_tai() IN (1, 2) OR EXISTS (SELECT 1 FROM phieu_luong pl WHERE pl.ma_ky_luong = ky_luong.ma_ky_luong AND pl.ma_nhan_vien = public.nhan_vien_hien_tai())`. Nhân viên chỉ thấy kỳ lương mà mình có phiếu lương phát hành. |
| **16** | `cau_hinh_khoan_luong`| `doc_cau_hinh_khoan_luong` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc cấu hình thưởng KPI/Bonus Legacy. |
| **17** | `cau_hinh_phu_cap` | `doc_cau_hinh_phu_cap` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc định mức phụ cấp (ăn ca, đêm 30%). |
| **18** | `cau_hinh_khau_tru` | `doc_cau_hinh_khau_tru` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc định mức khấu trừ BHXH, thuế TNCN. |
| **19** | `cau_hinh_tang_ca` | `doc_cau_hinh_tang_ca` | SELECT | `authenticated` | `auth.uid() IS NOT NULL`. Đọc hệ số lương OT (1.5, 2.0, 3.0). |
| **20** | `phieu_luong` | `doc_phieu_luong` | SELECT | `authenticated` | `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.vai_tro_hien_tai() IN (1, 2)`. Nhân viên chỉ xem phiếu lương của chính mình; HR/Admin xem toàn viện. Toàn bộ thao tác INSERT/UPDATE khóa chặt với `service_role`. |
| **21** | `chi_tiet_phieu_luong`| `doc_chi_tiet_phieu_luong` | SELECT | `authenticated` | `EXISTS (SELECT 1 FROM phieu_luong pl WHERE pl.ma_phieu_luong = chi_tiet_phieu_luong.ma_phieu_luong AND (pl.ma_nhan_vien = public.nhan_vien_hien_tai() OR public.vai_tro_hien_tai() IN (1, 2)))`. |
| **22** | `chi_tiet_tru_bao_hiem` | `doc_chi_tiet_tru_bao_hiem` | SELECT | `authenticated` | Khớp theo dòng chi tiết phiếu lương chủ của người dùng hoặc HR/Admin. |
| **23** | `chi_tiet_tru_thue_tncn` | `doc_chi_tiet_tru_thue_tncn` | SELECT | `authenticated` | Khớp theo dòng chi tiết phiếu lương chủ của người dùng hoặc HR/Admin. |
| **24** | `chi_tiet_tru_vi_pham_tam_ung` | `doc_chi_tiet_tru_vi_pham_tam_ung` | SELECT | `authenticated` | Khớp theo dòng chi tiết phiếu lương chủ của người dùng hoặc HR/Admin. |
| **25** | `nhat_ky_quan_tri` | `doc_nhat_ky_quan_tri` | SELECT | `authenticated` | `public.vai_tro_hien_tai() = 1` (**Chỉ duy nhất Quản trị viên Admin mới có quyền xem Audit Log**). Client hoàn toàn không có quyền sửa/xóa/chèn nhật ký. |
| **26** | `du_an` | `doc_du_an`<br>`them_du_an_quan_ly`<br>`sua_du_an_quan_ly`<br>`xoa_du_an_quan_ly` | SELECT<br>INSERT<br>UPDATE<br>DELETE | `authenticated` | • `doc_du_an`: `auth.uid() IS NOT NULL` (Mọi nhân sự xem lịch sự kiện khách sạn)<br>• Ghi/Sửa/Xóa: `public.vai_tro_hien_tai() IN (1, 2, 3)` (Quản lý sự kiện, HR, Admin). |
| **27** | `phan_cong_du_an` | `doc_phan_cong_du_an`<br>`them_phan_cong_du_an`<br>`sua_phan_cong_du_an`<br>`xoa_phan_cong_du_an` | SELECT<br>INSERT<br>UPDATE<br>DELETE | `authenticated` | • `doc_phan_cong_du_an`: `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()` (Nhân viên xem thù lao & vị trí của mình, Quản lý xem toàn bộ sự kiện)<br>• Thao tác điều động/đánh giá: Dành riêng cho cấp Quản lý (`public.la_quan_ly_hien_tai()`). |
| **28** | `dinh_bien_ca` | `doc_dinh_bien_ca`<br>`ghi_dinh_bien_ca` | SELECT<br>ALL | `authenticated` | • `doc_dinh_bien_ca`: `auth.uid() IS NOT NULL`<br>• `ghi_dinh_bien_ca`: `public.vai_tro_hien_tai() IN (1, 2, 3)` (Chỉ Quản lý/HR/Admin cấu hình định biên). |
| **29** | `giai_trinh_cham_cong` | `doc_giai_trinh_cham_cong`<br>`them_giai_trinh_cham_cong`<br>`sua_giai_trinh_cham_cong` | SELECT<br>INSERT<br>UPDATE | `authenticated` | • `doc_giai_trinh_cham_cong`: `ma_nhan_vien = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()`<br>• `them_giai_trinh_cham_cong`: `WITH CHECK (ma_nhan_vien = public.nhan_vien_hien_tai())`<br>• `sua_giai_trinh_cham_cong`: Quản lý xác minh cấp 1 và HR xác nhận cấp 2. |
| **30** | `yeu_cau_doi_ca` | `doc_yeu_cau_doi_ca`<br>`them_yeu_cau_doi_ca`<br>`sua_yeu_cau_doi_ca` | SELECT<br>INSERT<br>UPDATE | `authenticated` | • `doc_yeu_cau_doi_ca`: `ma_nhan_vien_gui = public.nhan_vien_hien_tai() OR ma_nhan_vien_nhan = public.nhan_vien_hien_tai() OR public.la_quan_ly_hien_tai()`<br>• `them_yeu_cau_doi_ca`: `WITH CHECK (ma_nhan_vien_gui = public.nhan_vien_hien_tai())`<br>• `sua_yeu_cau_doi_ca`: Người nhận đồng ý (`ma_nhan_vien_nhan = public.nhan_vien_hien_tai()`) hoặc Quản lý phê duyệt (`public.la_quan_ly_hien_tai()`). |

---

### 3.3. Hệ Thống Stored Functions Hỗ Trợ RLS & Toàn Vẹn CSDL (`SECURITY DEFINER`)

#### Khái niệm `SECURITY DEFINER` và giải pháp triệt tiêu đệ quy vô tận (Infinite Recursion)
Trong PostgreSQL, một hàm mặc định được định nghĩa là `SECURITY INVOKER` (thực thi dưới quyền hạn của người gọi hàm). Tuy nhiên, khi xây dựng Policy RLS:
- Nếu Policy trên bảng `phan_ca` thực hiện câu truy vấn `SELECT` sang bảng `nhan_vien` để kiểm tra phòng ban của nhân viên đó.
- Bảng `nhan_vien` cũng đang bật RLS và lại gọi Policy của chính nó.
- Hiện tượng này tạo thành **vòng lặp đệ quy vô tận (Infinite Recursion)**: Policy gọi Query -> Query kích hoạt Policy -> Policy lại gọi Query... khiến CSDL làm việc quá tải và ném lỗi `infinite recursion detected in policy for relation ...`.

**Giải pháp:** Toàn bộ các hàm tiện ích bảo mật được định nghĩa với từ khóa **`SECURITY DEFINER`**. Khi được gọi, hàm sẽ tạm thời nâng quyền thực thi lên cấp **Database Owner (Superuser `postgres`)**, hoàn toàn bỏ qua (Bypass) các bộ lọc RLS bên trong nội hàm để tra cứu thông tin định danh và phòng ban một cách nhanh chóng, trả về kết quả an toàn và ngăn chặn 100% lỗi đệ quy.

#### Danh mục chi tiết các Stored Functions cốt lõi trong hệ thống:

```sql
-- 1. Hàm trích xuất mã nhân viên hiện tại từ JWT claims
CREATE OR REPLACE FUNCTION public.nhan_vien_hien_tai()
RETURNS INT LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT NULLIF(auth.jwt() -> 'app_metadata' ->> 'ma_nhan_vien', '')::INT;
$$;

-- 2. Hàm trích xuất mã vai trò hiện tại từ JWT claims
CREATE OR REPLACE FUNCTION public.vai_tro_hien_tai()
RETURNS INT LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT NULLIF(auth.jwt() -> 'app_metadata' ->> 'ma_vai_tro', '')::INT;
$$;

-- 3. Hàm kiểm tra người dùng có thuộc nhóm quản lý (Admin, HR, Manager) không
CREATE OR REPLACE FUNCTION public.la_quan_ly_hien_tai()
RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT public.vai_tro_hien_tai() IN (1, 2, 3);
$$;

-- 4. Hàm lấy mã phòng ban của người dùng đang đăng nhập
CREATE OR REPLACE FUNCTION public.phong_ban_hien_tai()
RETURNS INT LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT nv.ma_phong_ban
    FROM public.nhan_vien nv
    WHERE nv.ma_nhan_vien = public.nhan_vien_hien_tai();
$$;

-- 5. Hàm lấy mã phòng ban của nhân viên nộp đơn
CREATE OR REPLACE FUNCTION public.phong_ban_cua_don(p_ma_don INT)
RETURNS INT LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT nv.ma_phong_ban
    FROM public.don_tu dt
    JOIN public.nhan_vien nv ON dt.ma_nhan_vien = nv.ma_nhan_vien
    WHERE dt.ma_don = p_ma_don;
$$;

-- 6. Hàm kiểm tra nhân viên có HĐLĐ còn hiệu lực tại một ngày cụ thể
CREATE OR REPLACE FUNCTION public.nhan_vien_co_hop_dong_hieu_luc(p_ma_nhan_vien INT, p_ngay DATE)
RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.hop_dong hd
        WHERE hd.ma_nhan_vien = p_ma_nhan_vien
          AND hd.trang_thai = 'HIEU_LUC'
          AND p_ngay >= hd.ngay_bat_dau
          AND (hd.ngay_ket_thuc IS NULL OR p_ngay <= hd.ngay_ket_thuc)
    );
$$;
```

#### Hook nhúng định danh & phân quyền vào JWT: `custom_access_token_hook`
Được đăng ký trực tiếp vào **Supabase GoTrue Authentication Engine**. Mỗi khi người dùng đăng nhập hoặc ứng dụng làm mới token (`Refresh Token`), GoTrue Auth tự động gọi hàm này:
1. Đọc địa chỉ email từ `event -> 'claims' ->> 'email'`.
2. Truy vấn đối chiếu trong CSDL: tìm bản ghi `nhan_vien` có email trùng khớp (không phân biệt hoa thường), nối sang `tai_khoan` để kiểm tra tài khoản có cờ `trang_thai = TRUE`.
3. Nếu tài khoản bị khóa hoặc không tìm thấy: Giữ nguyên claim gốc mà không bổ sung quyền hạn nào (khiến toàn bộ các lệnh truy vấn sau đó đều bị RLS chặn đứng).
4. Nếu tài khoản hợp lệ: Tập hợp danh sách các mã quyền dạng chuỗi `quyen.ma_quyen_code` từ bảng liên kết `vai_tro_quyen` (ví dụ: `["CHAM_CONG_DI_DONG", "NOP_DON_TU", "XEM_LICH_CA"]`).
5. Đóng gói và hợp nhất (`||`) `ma_nhan_vien`, `ma_vai_tro` cùng mảng quyền chuỗi `quyen` vào nhánh `app_metadata` của JWT. Nhờ đó, Mobile App và PostgreSQL RLS có thể kiểm tra thẩm quyền ngay lập tức mà không cần tốn thêm truy vấn SQL nào.

---

### 3.4. Cấu Trúc Kiểm Tra Khi Thao Tác: Phân Định Rạch Ròi Giữa Web Admin & Mobile App

Hệ thống Quản lý Nhân sự & Tiền lương The OverTime Hotel áp dụng mô hình phân tách kiến trúc chuyên biệt giữa cổng điều hành quản trị tập trung (Web Admin Portal) và ứng dụng di động phân tán (Mobile App):

```text
               ┌────────────────────────────────────────────────────────┐
               │              KIẾN TRÚC BẢO MẬT PHÂN TẦNG                │
               │          THE OVERTIME HOTEL ENTERPRISE HRM             │
               └────────────────────────────────────────────────────────┘

     [NHÂN VIÊN / QUẢN LÝ BỘ PHẬN]                 [QUẢN TRỊ VIÊN / HR / IT]
                   │                                           │
         .NET MAUI Mobile App                            Web Admin Portal
         (Android / iOS / Win)                        (ASP.NET Core MVC C#)
                   │                                           │
                   │ [HTTPS REST PostgREST]                    │ [Direct TCP ADO.NET Connection]
                   │ Bearer JWT (Access Token)                 │ Host: aws-0-ap-southeast-1.pooler
                   │ Port 443                                  │ Username: postgres (Superuser)
                   ▼                                           ▼
       ┌────────────────────────┐                  ┌────────────────────────┐
       │   SUPABASE POSTGREST   │                  │  ASP.NET CORE WEB CORE │
       │ - Giải mã claims JWT   │                  │ - Cookie Authentication│
       │ - SET LOCAL role =     │                  │ - [Authorize(Roles=..)]│
       │   'authenticated'      │                  │ - PermissionHandler    │
       └───────────┬────────────┘                  │   (17 PermissionCodes) │
                   │                               │ - IAdminAuditService   │
                   │                               └───────────┬────────────┘
                   │                                           │
                   ▼                                           ▼
       ┌────────────────────────┐                  ┌────────────────────────┐
       │ POSTGRESQL RLS ENGINE  │                  │  SUPERUSER CONNECTION  │
       │ (Row Level Security)   │                  │                        │
       │ • BẮT BUỘC & ÁP ĐẶT 100%│                  │ • BYPASS RLS HOÀN TOÀN │
       │ • Invisible Filter     │                  │ • Toàn quyền đọc/ghi   │
       │ • USING / WITH CHECK   │                  │   đáp ứng tính toán    │
       │ • Không thể vượt qua   │                  │   lương & quản trị hệ  │
       └───────────┬────────────┘                  │   thống phức tạp       │
                   │                               └───────────┬────────────┘
                   │                                           │
                   └─────────────────────┬─────────────────────┘
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │        POSTGRESQL DATABASE ENGINE         │
                   │  - 30 BẢNG CHUẨN 3NF                      │
                   │  - 8 DATABASE TRIGGERS RÀO CHẮN PHẦN CỨNG  │
                   │    (Chặn leo quyền, chặn đổi tên vai trò, │
                   │     chặn phân ca / quẹt thẻ không HĐLĐ,   │
                   │     khóa chống đua tương tranh nghiệp vụ) │
                   └───────────────────────────────────────────┘
```

#### Bảng Đối Chiếu Cơ Chế Kiểm Tra Khi Thao Tác Giữa Hai Phân Hệ:

| Tiêu Chí So Sánh | Web Admin Portal (Web Quản Trị) | Mobile Application (.NET MAUI) |
| :--- | :--- | :--- |
| **Đối tượng sử dụng** | Quản trị viên hệ thống (Admin), Trưởng phòng Nhân sự (HR), Chuyên viên Kỹ thuật & Hạ tầng (IT). | Nhân viên phục vụ khách sạn (Employee), Quản lý ca trực / Trưởng bộ phận (Manager). |
| **Giao thức mạng & Cổng** | TCP ADO.NET qua Npgsql / Entity Framework Core 10 (Cổng `5432` kết nối trực tiếp hoặc `6543` Supavisor Pooler). | HTTPS REST API qua Supabase PostgREST Engine (`https://<ref>.supabase.co/rest/v1/*`, Cổng `443`). |
| **Tài khoản CSDL kết nối** | `postgres` (Database Owner / Superuser có quyền cao nhất). | `authenticated` (phiên đã đăng nhập) hoặc `anon` (trước khi đăng nhập). |
| **Cơ chế Row Level Security (RLS)** | **BYPASS RLS HOÀN TOÀN** (Trong PostgreSQL, role Superuser tự động bỏ qua mọi Policy RLS). Lý do: Cần tổng hợp dữ liệu toàn khách sạn, chạy batch tính lương 2 tầng cho hàng ngàn nhân viên và sao lưu CSDL. | **BẮT BUỘC ÁP ĐẶT 100% TẠI NHÂN CSDL**. Mọi câu query đều tự động gắn thêm điều kiện bảo mật vô hình. Không người dùng nào xem được lương hoặc thông tin của đồng nghiệp. |
| **Xác thực danh tính (AuthN)** | Cookie-based Authentication (`CookieAuthenticationDefaults.AuthenticationScheme`), bảo mật bằng mã hóa AES phân tán trong phiên làm việc. | Supabase GoTrue Auth cấp cặp `access_token` (JWT có hạn sử dụng ngắn) và `refresh_token` lưu trong secure storage điện thoại. |
| **Phân quyền truy cập (AuthZ)** | • Phân quyền theo vai trò: `[Authorize(Roles = "Admin,HR,IT")]`<br>• Phân quyền theo chức năng chi tiết: `PermissionHandler` đối chiếu 17 mã quyền `PermissionCodes` lưu trong ClaimsPrincipal máy chủ. | • Phân quyền theo dữ liệu thực tế tại CSDL qua RLS Policies.<br>• Token mang `app_metadata.quyen`, client dùng để ẩn/hiện các nút bấm trên giao diện XAML di động. |
| **Ràng buộc toàn vẹn dữ liệu** | • Business Logic kiểm tra trong C# Controller/Service.<br>• **8 Triggers CSDL kiểm soát bắt buộc**. | • Client Policy (Kiểm tra GPS Geofence < 300m, kiểm tra khung giờ quẹt thẻ).<br>• **8 Triggers CSDL kiểm soát bắt buộc**. |
| **Ghi vết kiểm toán (Audit Log)** | Mọi hành động can thiệp (tạo mới, sửa quyền, khóa tài khoản, duyệt đơn, khóa kỳ lương) đều tự động ghi vết vào bảng `nhat_ky_quan_tri` qua `IAdminAuditService`. | Lưu vết nghiệp vụ trực tiếp trong các trường thời gian và người duyệt của bảng `don_tu`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`. |

---

## 4. Hệ Thống Toàn Bộ 8 Triggers CSDL (Rào Chắn Bảo Mật & Ràng Buộc Vận Hành)

Nhằm bảo đảm toàn vẹn dữ liệu ở cấp độ phần cứng CSDL (Database Engine Level), **8 triggers** được kích hoạt tự động. Các trigger này tạo thành một mạng lưới rào chắn vững chắc: dù truy cập từ Web Admin hay Mobile App, dù thực thi qua code C# hay script SQL gõ trực tiếp trên Dashboard, CSDL vẫn kiên quyết từ chối mọi thao tác vi phạm nghiệp vụ hoặc có nguy cơ tấn công leo quyền:

```text
[Mọi Thao Tác: Web Admin / Mobile App / SQL Console]
                     │
                     ├──> vai_tro_quyen ──────> [Trigger 1: trg_chan_cap_quyen_tai_khoan_di_dong]
                     ├──> vai_tro       ──────> [Trigger 2: trg_chan_doi_ten_vai_tro_quan_tri]
                     ├──> yeu_cau_tai_khoan ──> [Trigger 3: trg_chan_yeu_cau_tai_khoan_trung]
                     │                  ──────> [Trigger 4: trg_chan_yeu_cau_tai_khoan_dung_quyen]
                     ├──> phan_ca       ──────> [Trigger 5: trg_chan_phan_ca_khong_hop_dong]
                     │                  ──────> [Trigger 8: event_shift_changed]
                     ├──> cham_cong     ──────> [Trigger 6: trg_chan_cham_cong_khong_hop_dong]
                     └──> 10 Bảng cốt lõi ────> [Trigger 7: event_operations_lock]
```

### 4.1. Trigger 1: `trg_chan_cap_quyen_tai_khoan_di_dong`
- **Bảng áp dụng:** `public.vai_tro_quyen` (BEFORE INSERT OR UPDATE).
- **Hàm thực thi:** `public.chan_cap_quyen_tai_khoan_di_dong()`.
- **Cơ chế & Mục tiêu an ninh:**
  1. *Chặn cấp quyền sai đối tượng:* Quyền `CAP_TAI_KHOAN_MOBILE` (cấp phát user trong Supabase Auth) là chìa khóa mở vào toàn bộ RLS policy của Mobile App. Trigger nghiêm cấm tuyệt đối việc gán mã quyền này cho các vai trò ngoài Quản trị viên (`ma_vai_tro = 1`) và Công nghệ Thông tin (`ma_vai_tro = 5`). Bất kỳ thao tác thêm/sửa nào vi phạm đều bị CSDL ném ngoại lệ `integrity_constraint_violation`.
  2. *Chặn đường leo quyền (Privilege Escalation):* Ngăn chặn vai trò khác (kể cả IT) tự ý gán thêm quyền cho vai trò Admin (`ma_vai_tro = 1`). Chỉ có chính Admin mới được thao tác trên quyền hạn của vai trò Admin.
  3. *Tương thích script seed:* Tự động bỏ qua kiểm tra nếu cặp khóa `(ma_vai_tro, ma_quyen)` đã tồn tại trong CSDL, giúp câu lệnh `INSERT ... ON CONFLICT DO NOTHING` của các script khởi tạo dữ liệu không bị nghẽn.

### 4.2. Trigger 2: `trg_chan_doi_ten_vai_tro_quan_tri`
- **Bảng áp dụng:** `public.vai_tro` (BEFORE UPDATE).
- **Hàm thực thi:** `public.chan_doi_ten_vai_tro_quan_tri()`.
- **Cơ chế & Mục tiêu an ninh:**
  - Toàn bộ mã nguồn backend C# ASP.NET Core MVC và các chính sách phân quyền đều so sánh vai trò dựa trên chuỗi định danh chuẩn hóa: `Admin` và `IT` (ví dụ: `NormalizeRole`, `GetVietnameseRoleName`, kiểm tra giữ tài khoản Admin cuối cùng).
  - Nếu quản trị viên thực hiện câu lệnh `UPDATE` đổi tên vai trò của ID 1 hoặc ID 5 trên Dashboard, hệ thống phân quyền sẽ bị lỗi âm thầm (silent failure) dẫn tới mất quyền toàn bộ.
  - Trigger phát hiện thay đổi `NEW.ten_vai_tro IS DISTINCT FROM OLD.ten_vai_tro` trên các vai trò cốt lõi và ném lỗi từ chối ngay lập tức.

### 4.3. Trigger 3: `trg_chan_yeu_cau_tai_khoan_trung`
- **Bảng áp dụng:** `public.yeu_cau_tai_khoan` (BEFORE INSERT OR UPDATE).
- **Hàm thực thi:** `public.chan_yeu_cau_tai_khoan_trung()`.
- **Cơ chế & Mục tiêu an ninh:**
  - Chặn việc gửi yêu cầu cấp tài khoản cho nhân viên đã có tài khoản hoạt động trong hệ thống.
  - Quét 2 điều kiện trùng: `ten_dang_nhap` đã thuộc về một tài khoản đang tồn tại trong bảng `tai_khoan`, hoặc `ma_nhan_vien` đã được cấp một tài khoản khác.
  - *Ngoại lệ hợp lệ:* Khi trạng thái chuyển sang `DA_CAP`, chính yêu cầu đó sẽ trỏ `ma_tai_khoan` tới tài khoản mới được sinh ra. Trigger sử dụng điều kiện `t.ma_tai_khoan IS DISTINCT FROM NEW.ma_tai_khoan` để bỏ qua tài khoản kết quả của chính yêu cầu. Đồng thời, phiên bản nâng cấp (migration `20261019`) cho phép nhân viên thăng chức / đổi vai trò được tạo yêu cầu cấp lại quyền hợp lệ mà không bị chặn nhầm.

### 4.4. Trigger 4: `trg_chan_yeu_cau_tai_khoan_dung_quyen`
- **Bảng áp dụng:** `public.yeu_cau_tai_khoan` (BEFORE INSERT OR UPDATE).
- **Hàm thực thi:** `public.chan_yeu_cau_tai_khoan_dung_quyen()`.
- **Cơ chế & Mục tiêu an ninh:**
  - Bảo vệ tính toàn vẹn khi chốt cấp tài khoản: Khi yêu cầu được gán `ma_tai_khoan` (báo hiệu cấp thành công `DA_CAP`), trigger bắt buộc `ma_nguoi_xu_ly` không được phép NULL.
  - Trigger truy vấn ngược từ `tai_khoan` người xử lý -> `vai_tro_quyen` -> `quyen` để xác minh người xử lý bắt buộc phải sở hữu mã quyền `CAP_TAI_KHOAN_MOBILE` (vai trò Admin hoặc IT).
  - Đây là lớp phòng thủ chuyên sâu (Defense in Depth) ở tầng CSDL: kể cả khi kẻ tấn công vượt qua bộ lọc `[Authorize]` ở tầng ứng dụng hoặc thao tác trực tiếp trên SQL console, CSDL vẫn ném ngoại lệ `insufficient_privilege` từ chối thao tác.

### 4.5. Trigger 5: `trg_chan_phan_ca_khong_hop_dong`
- **Bảng áp dụng:** `public.phan_ca` (BEFORE INSERT OR UPDATE).
- **Hàm thực thi:** `public.chan_phan_ca_khong_hop_dong()` — `SECURITY DEFINER` vì `anon` không có quyền đọc `hop_dong`.
- **Cơ chế & Mục tiêu an ninh:**
  - Chặn xếp ca cho nhân viên không ở trạng thái `DANG_LAM`.
  - Bắt buộc ngày làm việc phải nằm trong khoảng hiệu lực của một hợp đồng lao động (`nhan_vien_co_hop_dong_hieu_luc`), tránh phát sinh công cho người đã hết hạn hợp đồng.
  - Bỏ qua khi dòng đã tồn tại trên INSERT (no-op của `ON CONFLICT DO NOTHING` lúc seed dữ liệu).

### 4.6. Trigger 6: `trg_chan_cham_cong_khong_hop_dong`
- **Bảng áp dụng:** `public.cham_cong` (BEFORE INSERT OR UPDATE).
- **Hàm thực thi:** `public.chan_cham_cong_khong_hop_dong()` — `SECURITY DEFINER`.
- **Cơ chế & Mục tiêu an ninh:**
  - Với bản ghi đã gắn `ma_phan_ca`, tra ngược ra nhân viên và ngày làm việc rồi kiểm tra hợp đồng lao động còn hiệu lực.
  - Ném lỗi `integrity_constraint_violation` kèm thông điệp nêu rõ mã ca, mã nhân viên và ngày sai, giúp người vận hành sửa đúng gốc thay vì dò log.
  - Đây là lớp chặn cuối cùng bảo vệ bảng lương khỏi việc ghi công cho người không còn hợp đồng.

### 4.7. Trigger 7: `event_operations_lock`
- **Bảng áp dụng:** 10 bảng nghiệp vụ nhạy cảm (`phan_ca`, `don_tu`, `hop_dong`, `ky_luong`, `phieu_luong`, `chi_tiet_phieu_luong`, `cham_cong`, `loai_don`, `dinh_bien_ca`, `dinh_bien_ngay`) — BEFORE INSERT OR UPDATE OR DELETE FOR EACH STATEMENT.
- **Hàm thực thi:** `public.event_operations_lock()`.
- **Cơ chế & Mục tiêu an ninh:**
  - Sử dụng cơ chế khóa cố vấn cấp giao dịch của PostgreSQL: `PERFORM pg_advisory_xact_lock(2026101509);`.
  - Triệt tiêu hoàn toàn hiện tượng tương tranh (Race Conditions) giữa các tiến trình chạy nền: tính toán và chốt sổ kỳ lương hàng loạt, nghiệm thu thù lao dự án sự kiện, và các thao tác điều chỉnh ca trực / đơn từ đồng thời. Khóa tự động giải phóng ngay khi giao dịch hoàn tất (Commit hoặc Rollback), bảo đảm tính nhất quán dữ liệu ở mức độ tuyệt đối.

### 4.8. Trigger 8: `event_shift_changed`
- **Bảng áp dụng:** `public.phan_ca` (BEFORE UPDATE OR DELETE FOR EACH ROW).
- **Hàm thực thi:** `public.event_shift_changed()`.
- **Cơ chế & Mục tiêu an ninh:**
  - Giữ vững tính toàn vẹn khi ca làm việc có liên kết với phân công dự án sự kiện (`phan_cong_du_an`):
    * Nếu ca làm việc đã phát sinh công sự kiện thực tế hoặc dự án sự kiện đã ở trạng thái `HOAN_THANH` / `CHO_NGHIEM_THU`, trigger **nghiêm cấm xóa hoặc sửa đổi ca nguồn** làm mất căn cứ tính công thù lao (ném ngoại lệ mã lỗi `23514`).
    * Nếu ca làm việc đang ở trạng thái phân công sơ bộ (`DA_PHAN`) mà bị sửa đổi thời gian hoặc chuyển trạng thái `DA_HUY`, trigger tự động cập nhật phân công sự kiện tương ứng sang `HUY_PHAN_CONG`, tăng số hiệu phiên bản dự án (`version = version + 1`) và tự động ghi một dòng lịch sử vào `nhat_ky_quan_tri`.

---

## 5. Mạng Lưới Quan Hệ Liên Kết & Sơ Đồ Thực Thể (ERD 30 Bảng 3NF)

### 5.1. Sơ đồ thực thể quan hệ hoàn chỉnh (Mermaid ERD)

```mermaid
erDiagram
    %% PHAN HE 1: TO CHUC & HO SO NHAN SU
    phong_ban ||--o{ nhan_vien : "thuoc phong ban"
    chuc_vu ||--o{ nhan_vien : "dam nhiem chuc vu"
    nhan_vien ||--o{ hop_dong : "ky ket hop dong"
    nhan_vien ||--o| tai_khoan : "so huu tai khoan 1-1"

    %% PHAN HE 2: TAI KHOAN, RBAC & AUDIT LOG
    vai_tro ||--o{ tai_khoan : "phan bo vai tro"
    vai_tro ||--o{ vai_tro_quyen : "chua danh sach quyen"
    quyen ||--o{ vai_tro_quyen : "thuoc ve vai tro"
    tai_khoan ||--o{ nhat_ky_quan_tri : "thuc hien hanh dong"
    tai_khoan ||--o{ nhat_ky_quan_tri : "bi tac dong boi"

    %% QUY TRINH CAP TAI KHOAN TRUNG GIAN (5 FKs)
    nhan_vien ||--o{ yeu_cau_tai_khoan : "nguoi duoc cap tai khoan"
    nhan_vien ||--o{ yeu_cau_tai_khoan : "HR gui yeu cau"
    hop_dong ||--o{ yeu_cau_tai_khoan : "can cu phap ly HDLD"
    tai_khoan ||--o{ yeu_cau_tai_khoan : "IT xu ly yeu cau"
    tai_khoan ||--o| yeu_cau_tai_khoan : "tai khoan ket qua cap xong"
    vai_tro ||--o{ yeu_cau_tai_khoan : "vai tro du kien gan"

    %% PHAN HE 3: CA TRUC & CHAM CONG 24/7
    ca_lam_viec ||--o{ phan_ca : "loai ca truc duoc phan"
    nhan_vien ||--o{ phan_ca : "nhan vien duoc xep ca"
    phan_ca ||--o| cham_cong : "quet the vao ra 1-1"

    %% DINH BIEN, GIAI TRINH & DOI CA (PRD 3.4 / 3.5 / 4.5)
    ca_lam_viec ||--o{ dinh_bien_ca : "dinh muc nguoi toi thieu"
    chuc_vu ||--o{ dinh_bien_ca : "theo tung chuc vu"
    phong_ban ||--o{ dinh_bien_ca : "theo tung bo phan (NULL = dung chung)"
    phan_ca ||--o{ giai_trinh_cham_cong : "ca bi giai trinh"
    nhan_vien ||--o{ giai_trinh_cham_cong : "nguoi gui giai trinh"
    nhan_vien ||--o{ giai_trinh_cham_cong : "quan ly xac minh cap 1"
    nhan_vien ||--o{ giai_trinh_cham_cong : "HR xac nhan cap 2"
    phan_ca ||--o{ yeu_cau_doi_ca : "ca nhuong di"
    phan_ca ||--o{ yeu_cau_doi_ca : "ca nhan lam thay"
    nhan_vien ||--o{ yeu_cau_doi_ca : "nguoi gui yeu cau"
    nhan_vien ||--o{ yeu_cau_doi_ca : "nguoi nhan ca"
    nhan_vien ||--o{ yeu_cau_doi_ca : "nguoi duyet doi ca"

    %% PHAN HE 4: DON TU TRUC TUYEN
    loai_don ||--o{ don_tu : "phan loai don"
    nhan_vien ||--o{ don_tu : "nhan vien nop don"
    nhan_vien ||--o{ don_tu : "quan ly xet duyet don"

    %% PHAN HE 5: TIEN LUONG & THU CHI 3 CAP
    ky_luong ||--o{ phieu_luong : "thuoc ky tinh luong"
    nhan_vien ||--o{ phieu_luong : "nhan phieu luong"
    phieu_luong ||--o{ chi_tiet_phieu_luong : "chi tiet dong thu chi"
    
    cau_hinh_khoan_luong ||--o{ chi_tiet_phieu_luong : "tham chieu thuong bonus"
    cau_hinh_phu_cap ||--o{ chi_tiet_phieu_luong : "tham chieu phu cap"
    cau_hinh_khau_tru ||--o{ chi_tiet_phieu_luong : "tham chieu khau tru"
    cau_hinh_tang_ca ||--o{ chi_tiet_phieu_luong : "tham chieu he so OT"

    chi_tiet_phieu_luong ||--o| chi_tiet_tru_bao_hiem : "tinh bao hiem 10.5% 1-1"
    chi_tiet_phieu_luong ||--o| chi_tiet_tru_thue_tncn : "tinh thue TNCN luy tien 1-1"
    chi_tiet_phieu_luong ||--o| chi_tiet_tru_vi_pham_tam_ung : "tinh vi pham tam ung 1-1"

    %% PHAN HE 9: QUAN LY DU AN & SU KIEN KHACH SAN
    nhan_vien ||--o{ du_an : "quan ly truong ban to chuc"
    du_an ||--o{ phan_cong_du_an : "phan bo vi tri su kien"
    nhan_vien ||--o{ phan_cong_du_an : "nhan vien tham gia su kien"
    chi_tiet_phieu_luong ||--o| phan_cong_du_an : "ket chuyen thu lao vao luong"
```

### 5.2. Ma trận liên kết khóa ngoại (Foreign Keys Matrix)

| Bảng Chứa Khóa Ngoại | Cột Khóa Ngoại | Bảng Tham Chiếu (PK) | Quan Hệ | Hành Vi Khi Xóa (ON DELETE) | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---:|:---:|:---|
| `nhan_vien` | `ma_phong_ban` | `phong_ban(ma_phong_ban)` | N - 1 | RESTRICT | Nhân viên trực thuộc một phòng ban. Không thể xóa phòng ban nếu còn nhân sự. |
| `nhan_vien` | `ma_chuc_vu` | `chuc_vu(ma_chuc_vu)` | N - 1 | RESTRICT | Nhân viên đảm nhiệm một chức vụ chuyên môn. |
| `hop_dong` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | HĐLĐ thuộc về nhân viên. Xóa nhân viên xóa hợp đồng liên quan. |
| `tai_khoan` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | 1 - 1 | CASCADE | Mỗi nhân viên có tối đa 1 tài khoản đăng nhập (UNIQUE). |
| `tai_khoan` | `ma_vai_tro` | `vai_tro(ma_vai_tro)` | N - 1 | RESTRICT | Tài khoản được phân vào một nhóm vai trò bảo mật. |
| `vai_tro_quyen` | `ma_vai_tro` | `vai_tro(ma_vai_tro)` | N - 1 | CASCADE | Khóa ghép RBAC: vai trò liên kết quyền. |
| `vai_tro_quyen` | `ma_quyen` | `quyen(ma_quyen)` | N - 1 | CASCADE | Khóa ghép RBAC: quyền liên kết vai trò. |
| `yeu_cau_tai_khoan` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | Nhân viên mới cần cấp tài khoản di động. |
| `yeu_cau_tai_khoan` | `ma_hop_dong` | `hop_dong(ma_hop_dong)` | N - 1 | SET NULL | Hợp đồng lao động làm căn cứ phát sinh yêu cầu. |
| `yeu_cau_tai_khoan` | `ma_vai_tro` | `vai_tro(ma_vai_tro)` | N - 1 | RESTRICT | Vai trò dự kiến gán cho tài khoản. |
| `yeu_cau_tai_khoan` | `ma_nguoi_gui` | `nhan_vien(ma_nhan_vien)` | N - 1 | RESTRICT | Nhân viên HR gửi yêu cầu sau khi duyệt HĐLĐ. |
| `yeu_cau_tai_khoan` | `ma_nguoi_xu_ly` | `tai_khoan(ma_tai_khoan)` | N - 1 | SET NULL | Tài khoản chuyên viên IT tiếp nhận và cấp phát. |
| `yeu_cau_tai_khoan` | `ma_tai_khoan` | `tai_khoan(ma_tai_khoan)` | 1 - 1 | SET NULL | Tài khoản kết quả sau khi IT cấp thành công. |
| `phan_ca` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | Lịch phân ca gán cho nhân viên. |
| `phan_ca` | `ma_ca` | `ca_lam_viec(ma_ca)` | N - 1 | RESTRICT | Ca làm việc được gán trong ngày. |
| `cham_cong` | `ma_phan_ca` | `phan_ca(ma_phan_ca)` | 1 - 1 | CASCADE | Bản ghi quẹt thẻ gắn trực tiếp với 1 ca làm việc đã xếp. |
| `don_tu` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | Nhân viên nộp đơn điện tử. |
| `don_tu` | `ma_loai_don` | `loai_don(ma_loai_don)` | N - 1 | RESTRICT | Phân loại đơn xin phép. |
| `don_tu` | `ma_nguoi_duyet` | `nhan_vien(ma_nhan_vien)` | N - 1 | SET NULL | Quản lý có thẩm quyền duyệt đơn. |
| `phieu_luong` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | RESTRICT | Nhân viên sở hữu phiếu lương tháng. |
| `phieu_luong` | `ma_ky_luong` | `ky_luong(ma_ky_luong)` | N - 1 | RESTRICT | Kỳ tính lương khóa sổ. |
| `chi_tiet_phieu_luong` | `ma_phieu_luong` | `phieu_luong(ma_phieu_luong)` | N - 1 | CASCADE | Dòng chi tiết thuộc phiếu lương tổng hợp. |
| `chi_tiet_phieu_luong` | `ma_khoan` | `cau_hinh_khoan_luong(ma_khoan)` | N - 1 | RESTRICT | Tham chiếu định mức thưởng Bonus (Legacy). |
| `chi_tiet_phieu_luong` | `ma_phu_cap` | `cau_hinh_phu_cap(ma_phu_cap)` | N - 1 | RESTRICT | Tham chiếu cấu hình phụ cấp chuẩn. |
| `chi_tiet_phieu_luong` | `ma_khoan_tru` | `cau_hinh_khau_tru(ma_khoan_tru)` | N - 1 | RESTRICT | Tham chiếu cấu hình khấu trừ chuẩn. |
| `chi_tiet_phieu_luong` | `ma_loai_ot` | `cau_hinh_tang_ca(ma_loai_ot)` | N - 1 | RESTRICT | Tham chiếu loại tăng ca và hệ số OT. |
| `chi_tiet_tru_bao_hiem` | `ma_chi_tiet` | `chi_tiet_phieu_luong(ma_chi_tiet)` | 1 - 1 | CASCADE | Tách dòng chi tiết bảo hiểm 10.5% (3NF). |
| `chi_tiet_tru_thue_tncn` | `ma_chi_tiet` | `chi_tiet_phieu_luong(ma_chi_tiet)` | 1 - 1 | CASCADE | Tách dòng chi tiết tính thuế lũy tiến (3NF). |
| `chi_tiet_tru_vi_pham_tam_ung` | `ma_chi_tiet` | `chi_tiet_phieu_luong(ma_chi_tiet)` | 1 - 1 | CASCADE | Tách dòng chi tiết phạt vi phạm và tạm ứng (3NF). |
| `nhat_ky_quan_tri` | `ma_tai_khoan_thuc_hien` | `tai_khoan(ma_tai_khoan)` | N - 1 | SET NULL | Tài khoản thực hiện hành vi can thiệp hệ thống. |
| `nhat_ky_quan_tri` | `ma_tai_khoan_bi_tac_dong` | `tai_khoan(ma_tai_khoan)` | N - 1 | SET NULL | Tài khoản bị tác động (khóa, đổi quyền, reset mật khẩu). |
| `du_an` | `ma_nguoi_quan_ly` | `nhan_vien(ma_nhan_vien)` | N - 1 | RESTRICT | Trưởng ban tổ chức / Quản lý điều hành dự án sự kiện. |
| `phan_cong_du_an` | `ma_du_an` | `du_an(ma_du_an)` | N - 1 | CASCADE | Dự án sự kiện tiếp nhận nhân sự. Xóa sự kiện xóa phân công. |
| `phan_cong_du_an` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | Nhân sự khách sạn được điều động phục vụ sự kiện. |
| `phan_cong_du_an` | `ma_chi_tiet` | `chi_tiet_phieu_luong(ma_chi_tiet)` | 1 - 1 | SET NULL | Kết chuyển thù lao sự kiện vào dòng phụ cấp trên phiếu lương tháng. |
| `dinh_bien_ca` | `ma_ca` | `ca_lam_viec(ma_ca)` | N - 1 | CASCADE | Định biên thuộc một ca mẫu. Xóa ca xóa luôn định biên của ca đó. |
| `dinh_bien_ca` | `ma_chuc_vu` | `chuc_vu(ma_chuc_vu)` | N - 1 | CASCADE | Định biên đếm số người theo chức vụ chuyên môn. |
| `dinh_bien_ca` | `ma_phong_ban` | `phong_ban(ma_phong_ban)` | N - 1 | CASCADE | Bộ phận áp dụng; `NULL` là dùng chung mọi bộ phận. Cần thiết cho chức danh trải nhiều bộ phận (Trưởng Bộ Phận, Giám Sát Ca). |
| `giai_trinh_cham_cong` | `ma_phan_ca` | `phan_ca(ma_phan_ca)` | N - 1 | CASCADE | Giải trình gắn với đúng lượt ca bị sai lệch giờ. |
| `giai_trinh_cham_cong` | `ma_nhan_vien` | `nhan_vien(ma_nhan_vien)` | N - 1 | CASCADE | Nhân viên đứng tên giải trình. |
| `giai_trinh_cham_cong` | `ma_quan_ly_xac_minh` | `nhan_vien(ma_nhan_vien)` | N - 1 | SET NULL | Quản lý cấp 1 xác minh. Xóa hồ sơ quản lý không làm mất giải trình. |
| `giai_trinh_cham_cong` | `ma_hr_xac_nhan` | `nhan_vien(ma_nhan_vien)` | N - 1 | SET NULL | Cán bộ HR xác nhận cấp 2. |
| `yeu_cau_doi_ca` | `ma_phan_ca_gui` | `phan_ca(ma_phan_ca)` | N - 1 | CASCADE | Lượt ca người gửi nhường lại. |
| `yeu_cau_doi_ca` | `ma_phan_ca_nhan` | `phan_ca(ma_phan_ca)` | N - 1 | CASCADE | Lượt ca người nhận làm thay. |
| `yeu_cau_doi_ca` | `ma_nhan_vien_gui` | `nhan_vien(ma_nhan_vien)` | N - 1 | RESTRICT | Người gửi yêu cầu; còn yêu cầu lưu vết thì không xóa được hồ sơ nhân viên. |
| `yeu_cau_doi_ca` | `ma_nhan_vien_nhan` | `nhan_vien(ma_nhan_vien)` | N - 1 | RESTRICT | Người nhận ca; cùng lý do bảo toàn lịch sử đổi ca. |
| `yeu_cau_doi_ca` | `ma_nguoi_duyet` | `nhan_vien(ma_nhan_vien)` | N - 1 | SET NULL | Quản lý / HR ra quyết định duyệt hoặc từ chối. |
