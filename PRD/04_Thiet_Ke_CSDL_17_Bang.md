# PRD 04: Thiết Kế Cơ Sở Dữ Liệu 25 Bảng Chuẩn 3NF (Supabase PostgreSQL)

> **Lưu ý về tên file:** File này vẫn mang tên `04_Thiet_Ke_CSDL_17_Bang.md` (đã được tham chiếu ở nhiều nơi nên **không đổi tên**), nhưng **nội dung thực tế mô tả 25 bảng**. Hãy luôn đọc con số trong **tiêu đề nội dung bên dưới**, không đọc con số trong tên file.

> **Nguồn đối chiếu:** Toàn bộ nội dung dưới đây đã được đối chiếu với **schema thật ngày 07/10/2026** dump từ Supabase Cloud bằng lệnh `supabase db dump --linked --schema public` (PostgreSQL 17.6, schema `public`, 24 bảng). Bảng thứ **25** (`yeu_cau_tai_khoan`) được bổ sung ngày **09/10/2026** cho quy trình *HR duyệt HĐLĐ → bộ phận CNTT (IT) cấp tài khoản đăng nhập*.

## 1. Danh Mục Tổng Hợp 25 Bảng CSDL

Hệ thống CSDL HRM The OverTime Hotel được thiết kế đạt chuẩn hóa **dạng chuẩn 3 (3NF)** trên nền tảng Supabase Cloud (PostgreSQL Engine):

| STT | Tên Bảng | Ý Nghĩa Nghiệp Vụ Trong Khách Sạn | Khóa Chính (PK) | Khóa Ngoại (FK) |
|:---:|:---|:---|:---|:---|
| **1** | `phong_ban` | Danh mục các phòng ban, bộ phận khách sạn (FO, HK, F&B, Kỹ thuật...) | `ma_phong_ban` | — |
| **2** | `chuc_vu` | Danh mục vị trí công tác (Bếp trưởng, Lễ tân, Bellman, Trưởng ca...) | `ma_chuc_vu` | — |
| **3** | `nhan_vien` | Hồ sơ lý lịch, thông tin cá nhân, liên hệ, học vấn của nhân sự | `ma_nhan_vien` | `ma_phong_ban`, `ma_chuc_vu` |
| **4** | `hop_dong` | Hợp đồng lao động pháp lý, lưu thời hạn và mức lương cơ bản | `ma_hop_dong` | `ma_nhan_vien` |
| **5** | `tai_khoan` | Tài khoản đăng nhập hệ thống, mật khẩu băm bảo mật | `ma_tai_khoan` | `ma_nhan_vien` (1-1), `ma_vai_tro` |
| **6** | `vai_tro` | 5 nhóm vai trò chuẩn: Admin, HR, Manager, Employee, IT | `ma_vai_tro` | — |
| **7** | `quyen` | Danh mục quyền truy cập các màn hình chức năng | `ma_quyen` | — |
| **8** | `vai_tro_quyen` | Bảng trung gian giải quyết quan hệ N - N giữa Vai trò và Quyền (RBAC) | `(ma_vai_tro, ma_quyen)` | `ma_vai_tro`, `ma_quyen` |
| **9** | `yeu_cau_tai_khoan` | Yêu cầu cấp tài khoản đăng nhập: HR gửi sau khi duyệt HĐLĐ, bộ phận CNTT (IT) tiếp nhận và cấp | `ma_yeu_cau` | `ma_nhan_vien` (người được cấp), `ma_hop_dong`, `ma_vai_tro`, `ma_nguoi_gui`, `ma_nguoi_xu_ly`, `ma_tai_khoan` |
| **10** | `ca_lam_viec` | Danh mục 3 mẫu ca 24/7 (Sáng 6h-14h, Chiều 14h-22h, Đêm 22h-6h) | `ma_ca` | — |
| **11** | `phan_ca` | Bảng xếp lịch ca làm việc trước cho nhân viên theo tuần/tháng | `ma_phan_ca` | `ma_nhan_vien`, `ma_ca` |
| **12** | `cham_cong` | Bản ghi quẹt thẻ in/out thực tế, tự động tính phút trễ, sớm, giờ OT | `ma_cham_cong` | `ma_phan_ca` (1-1) |
| **13** | `loai_don` | Danh mục 4 loại đơn: Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc | `ma_loai_don` | — |
| **14** | `don_tu` | Quản lý nộp và xét duyệt đơn từ trực tuyến | `ma_don` | `ma_nhan_vien`, `ma_loai_don`, `ma_nguoi_duyet` |
| **15** | `ky_luong` | Chu kỳ khóa sổ tính lương theo tháng (Tháng/Năm) | `ma_ky_luong` | — |
| **16** | `cau_hinh_khoan_luong`| Tham số định mức phụ cấp, tiền thưởng, khoản khấu trừ định mức | `ma_khoan` | — |
| **17** | `cau_hinh_phu_cap` | Tham số phụ cấp (ăn ca, đêm, đi lại...) kèm cờ chịu thuế TNCN | `ma_phu_cap` | — |
| **18** | `cau_hinh_khau_tru` | Tham số khoản khấu trừ (BHXH, BHYT, thuế TNCN, vi phạm, tạm ứng) | `ma_khoan_tru` | — |
| **19** | `cau_hinh_tang_ca` | Danh mục loại tăng ca kèm hệ số nhân tiền lương theo giờ OT | `ma_loai_ot` | — |
| **20** | `phieu_luong` | Bảng tổng hợp lương tháng: Gross, Giảm trừ, Net thực nhận | `ma_phieu_luong` | `ma_nhan_vien`, `ma_ky_luong` |
| **21** | `chi_tiet_phieu_luong`| Bản ghi chi tiết từng dòng thu chi (Lương công, OT, Đêm 30%, BHXH 10.5%) | `ma_chi_tiet` | `ma_phieu_luong`, `ma_khoan`, `ma_loai_ot`, `ma_phu_cap`, `ma_khoan_tru` |
| **22** | `chi_tiet_tru_bao_hiem` | Khai báo 3 khoản bảo hiểm bắt buộc BHXH + BHYT + BHTN của từng dòng chi tiết | `ma_chi_tiet_bh` | `ma_chi_tiet` (1-1) |
| **23** | `chi_tiet_tru_thue_tncn` | Bảng tính thuế TNCN 5 mức bậc theo biểu lũy tiến từng dòng chi tiết | `ma_chi_tiet_thue` | `ma_chi_tiet` (1-1) |
| **24** | `chi_tiet_tru_vi_pham_tam_ung` | Bảng ghi khấu trừ vi phạm (đi trễ, về sớm, kỷ luật) và tạm ứng | `ma_chi_tiet_vp` | `ma_chi_tiet` (1-1) |
| **25** | `nhat_ky_quan_tri` | Nhật ký thao tác quản trị (ai làm gì, lúc nào, giá trị trước/sau) | `ma_nhat_ky` | `ma_tai_khoan_thuc_hien`, `ma_tai_khoan_bi_tac_dong` |

---

## 2. Từ Điển Dữ Liệu Chi Tiết (Data Dictionary)

### Bảng 1: `phong_ban` (Phòng ban / Bộ phận)
- `ma_phong_ban` (SERIAL, PK): Mã định danh phòng ban.
- `ten_phong_ban` (VARCHAR(150), NOT NULL, UNIQUE): Tên phòng ban (Tiền sảnh, Buồng phòng...).
- `mo_ta` (VARCHAR(255), NULL): Mô tả nhiệm vụ bộ phận.

### Bảng 2: `chuc_vu` (Chức vụ / Vị trí)
- `ma_chuc_vu` (SERIAL, PK): Mã định danh chức vụ.
- `ten_chuc_vu` (VARCHAR(100), NOT NULL, UNIQUE): Tên chức danh nghề nghiệp.
- `mo_ta` (VARCHAR(255), NULL): Mô tả tiêu chuẩn chuyên môn.

### Bảng 3: `nhan_vien` (Hồ sơ nhân viên)
- `ma_nhan_vien` (SERIAL, PK): Mã nhân viên duy nhất.
- `ma_phong_ban` (INT, FK -> phong_ban.ma_phong_ban, NOT NULL, ON DELETE RESTRICT): Phòng ban trực thuộc.
- `ma_chuc_vu` (INT, FK -> chuc_vu.ma_chuc_vu, NOT NULL, ON DELETE RESTRICT): Chức danh đảm nhiệm.
- `ho` (VARCHAR(100), NOT NULL): Họ và tên đệm của nhân viên.
- `ten` (VARCHAR(50), NOT NULL): Tên chính của nhân viên.
- `ngay_sinh` (DATE, NOT NULL): Ngày tháng năm sinh.
- `gioi_tinh` (VARCHAR(10), NOT NULL, CHECK in ('NAM', 'NU', 'KHAC')): Giới tính.
- `dien_thoai` (VARCHAR(20), NOT NULL): Số điện thoại liên hệ.
- `email` (VARCHAR(100), NOT NULL, UNIQUE): Thư điện tử — **khóa đối chiếu 1-1 với email của tài khoản Supabase Auth**.
- `dia_chi` (VARCHAR(255), NULL): Nơi ở hiện tại.
- `trinh_do` (VARCHAR(50), NOT NULL): Học vấn (Trung cấp, Cao đẳng, Đại học...).
- `ngay_vao_lam` (DATE, NOT NULL): Ngày chính thức làm việc.
- `ngay_thoi_viec` (DATE, NULL): Ngày nghỉ việc thực tế (NULL nếu còn làm).
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DANG_LAM', CHECK in ('DANG_LAM', 'NGHI_PHEP', 'DA_THOI_VIEC')): Trạng thái công tác.

### Bảng 4: `hop_dong` (Hợp đồng lao động)
- `ma_hop_dong` (SERIAL, PK): Mã hợp đồng lao động.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên ký kết.
- `so_hop_dong` (VARCHAR(50), NOT NULL, UNIQUE): Số ký hiệu văn bản HĐLĐ.
- `loai_hop_dong` (VARCHAR(50), NOT NULL): Loại HĐ (Thử việc, 1 năm, 3 năm, Vô thời hạn).
- `ngay_bat_dau` (DATE, NOT NULL): Ngày có hiệu lực.
- `ngay_ket_thuc` (DATE, NULL): Ngày hết hạn (NULL nếu vô thời hạn).
- `luong_co_ban` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Mức lương thỏa thuận pháp lý.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'HIEU_LUC', CHECK in ('HIEU_LUC', 'HET_HAN', 'DA_CHAM_DUT')): Hiệu lực hợp đồng.

### Bảng 5: `tai_khoan` (Tài khoản người dùng)
- `ma_tai_khoan` (SERIAL, PK): Mã tài khoản.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, UNIQUE, NOT NULL, ON DELETE CASCADE): Nhân viên sở hữu (Quan hệ 1-1).
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE RESTRICT): Nhóm vai trò được cấp.
- `ten_dang_nhap` (VARCHAR(50), NOT NULL, UNIQUE): Tên đăng nhập hệ thống.
- `mat_khau_bam` (VARCHAR(255), NOT NULL): Mật khẩu đã mã hóa an toàn (BCrypt/PBKDF2).
- `trang_thai` (VARCHAR(20), DEFAULT 'HoatDong'): HoatDong hoặc BiKhoa.

### Bảng 6: `vai_tro` (Vai trò người dùng)
- `ma_vai_tro` (SERIAL, PK): Mã định danh vai trò.
- `ten_vai_tro` (VARCHAR(50), NOT NULL, UNIQUE): Admin, HR, Manager, Employee, IT.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải phạm vi trách nhiệm.

> ⚠️ **Bổ sung ngày 09/10/2026 — vai trò thứ 5 `IT` (mã = 5):** Bộ phận **Công nghệ Thông tin** được tách riêng khỏi Kỹ thuật (`ma_phong_ban = 6`, kỹ sư tòa nhà) vì hai việc khác hẳn: kỹ thuật bảo trì hạ tầng vật lý, còn IT thì quản trị hệ thống và **cấp phát tài khoản đăng nhập** — điểm vào của toàn bộ quyền truy cập. Vai trò này là tác nhân chính xử lý các bản ghi `yeu_cau_tai_khoan` (Bảng 9).

### Bảng 7: `quyen` (Danh mục quyền truy cập)
- `ma_quyen` (SERIAL, PK): Mã quyền.
- `ten_quyen` (VARCHAR(100), NOT NULL, UNIQUE): Tên quyền (VD: NV_XEM, LUONG_TINH...).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải chức năng thao tác.

> ⚠️ **Bổ sung ngày 09/10/2026 — mã quyền thứ 17 `CAP_TAI_KHOAN_MOBILE`:** Danh mục quyền hiện có **17 mã quyền** (đối chiếu 1-1 với các hằng trong `Security/PermissionCodes.cs` của Web Admin). Mã mới được tách **có chủ đích** khỏi `QL_TAI_KHOAN`: quản lý hồ sơ tài khoản và cấp quyền đăng nhập Mobile là hai việc khác nhau. HR cần `QL_TAI_KHOAN` (đổi tên đăng nhập, khoá tài khoản) nhưng **KHÔNG** được cấp `CAP_TAI_KHOAN_MOBILE` — cửa vào của mọi RLS policy trên Supabase chỉ nên mở cho **Admin** và bộ phận **CNTT**. Ba trigger CSDL chặn tài khoản tự cấp quyền cho chính mình.

### Bảng 8: `vai_tro_quyen` (Phân quyền RBAC)
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE CASCADE): Vai trò.
- `ma_quyen` (INT, FK -> quyen.ma_quyen, NOT NULL, ON DELETE CASCADE): Quyền được cấp.
- *Khóa chính tổng hợp:* `PRIMARY KEY (ma_vai_tro, ma_quyen)`.

> ⚠️ **Nhóm RBAC sẽ được cập nhật sau khi chốt thiết kế đồng bộ tài khoản Supabase Auth.** Schema thật hiện đã có `quyen.ma_quyen_code VARCHAR(50) UNIQUE` (mã quyền dạng chuỗi, ví dụ `CHAM_CONG_DI_DONG`) và `tai_khoan.trang_thai` là **BOOLEAN** (đang bật / đang khoá) thay vì chuỗi `HoatDong` / `BiKhoa`. Các khác biệt này sẽ được chốt lại cùng kiến trúc đồng bộ tài khoản.

### Bảng 9: `yeu_cau_tai_khoan` (Yêu cầu cấp tài khoản đăng nhập)
- `ma_yeu_cau` (SERIAL, PK): Mã yêu cầu cấp tài khoản.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): **Người được cấp tài khoản** — nhân viên mới được HR duyệt HĐLĐ.
- `ma_hop_dong` (INT, FK -> hop_dong.ma_hop_dong, NULL, ON DELETE SET NULL): Hợp đồng lao động làm căn cứ phát sinh yêu cầu (NULL nếu yêu cầu phát sinh ngoài HĐLĐ).
- `ten_dang_nhap` (VARCHAR(50), NOT NULL): Tên đăng nhập dự kiến cấp cho Mobile App — **chỉ được duy nhất trong hàng đợi**.
- `ma_vai_tro` (INT, FK -> vai_tro.ma_vai_tro, NOT NULL, ON DELETE RESTRICT): Vai trò sẽ được gán khi cấp tài khoản.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'CHO_IT', CHECK in ('CHO_IT', 'DANG_XU_LY', 'DA_CAP', 'TU_CHOI')): Trạng thái luồng cấp phát — HR gửi → IT nhận xử lý → cấp hoặc từ chối.
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm HR gửi yêu cầu.
- `ma_nguoi_gui` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): **Nhân viên HR gửi yêu cầu** (người khởi tạo).
- `ma_nguoi_xu_ly` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): **Tài khoản IT xử lý** yêu cầu (NULL cho tới khi có người nhận việc).
- `ngay_xu_ly` (TIMESTAMPTZ, NULL): Thời điểm IT kết luận xử lý (cấp hoặc từ chối).
- `ghi_chu_tu_choi` (VARCHAR(500), NULL): Lý do từ chối — **bắt buộc có khi `trang_thai = 'TU_CHOI'`**.
- `ma_tai_khoan` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): **Tài khoản kết quả** sau khi cấp (chỉ có giá trị khi `trang_thai = 'DA_CAP'`).
- *Ràng buộc:* Unique index partial `uq_yeu_cau_ten_dang_nhap_dang_mo` trên `ten_dang_nhap` WHERE `trang_thai IN ('CHO_IT', 'DANG_XU_LY')` — chặn trùng tên đăng nhập trong hàng đợi, nhưng vẫn cho phép tái sử dụng tên đã cấp xong hoặc đã bị từ chối.

> **Bối cảnh nghiệp vụ — vì sao cần bảng trung gian này?** Hệ thống có **hai lớp tài khoản tách biệt**: bảng `tai_khoan` (lớp tài khoản PostgreSQL phục vụ **Web Admin**) và bảng `auth.users` của **Supabase Auth** phục vụ **Mobile App**. Hai lớp này được đồng bộ qua quy trình cấp phát chứ không tự sinh — nên có thể tồn tại **một dòng trong `tai_khoan` mà không có dòng tương ứng trong `auth.users`**: nhân viên đăng nhập được Web nhưng **không đăng nhập được app**. Bảng `yeu_cau_tai_khoan` ghi lại **ai đang chờ ai** (HR gửi → IT xử lý), biến việc cấp phát tài khoản từ thao tác thủ công, dễ thất lạc thành một **quy trình có vết, có trạng thái, có thời điểm xử lý**.

> **Chú ý khi vẽ ERD — bảng này tham chiếu 5 khóa ngoại vào 4 bảng:** `nhan_vien` bị tham chiếu **2 lần** (`ma_nhan_vien` là người được cấp tài khoản, `ma_nguoi_gui` là HR gửi yêu cầu) và `tai_khoan` cũng bị tham chiếu **2 lần** (`ma_nguoi_xu_ly` là IT xử lý, `ma_tai_khoan` là tài khoản kết quả). Khi dựng sơ đồ phải **ghi nhãn quan hệ khác nhau cho từng cặp**, tuyệt đối không vẽ hai đường nối trùng tên — nếu không sẽ không phân biệt được "tài khoản của người xử lý" với "tài khoản được cấp".

### Bảng 10: `ca_lam_viec` (Danh mục ca 24/7)
- `ma_ca` (SERIAL, PK): Mã ca.
- `ten_ca` (VARCHAR(50), NOT NULL, UNIQUE): Ca Sáng, Ca Chiều, Ca Đêm.
- `gio_bat_dau` (TIME, NOT NULL): Khung giờ mở ca (06:00, 14:00, 22:00).
- `gio_ket_thuc` (TIME, NOT NULL): Khung giờ đóng ca (14:00, 22:00, 06:00).
- `qua_dem` (BOOLEAN, NOT NULL, DEFAULT FALSE): Đánh dấu ca có làm qua nửa đêm để tính phụ cấp đêm 30%.

> ⚠️ Cột `he_so_luong` **không tồn tại** trong CSDL thật. Hệ số lương theo ca được lưu ở cấu hình tăng ca (`cau_hinh_tang_ca.he_so`) và phụ cấp đêm lấy theo cờ `qua_dem`.

### Bảng 11: `phan_ca` (Lịch xếp ca làm việc)
- `ma_phan_ca` (SERIAL, PK): Mã bản ghi xếp ca.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên được xếp ca.
- `ma_ca` (INT, FK -> ca_lam_viec.ma_ca, NOT NULL, ON DELETE RESTRICT): Ca làm việc được chỉ định.
- `ngay_lam_viec` (DATE, NOT NULL): Ngày thực hiện ca.
- `bat_dau_du_kien` (TIMESTAMPTZ, NOT NULL): Thời điểm dự kiến bắt đầu ca (quyết định mốc tính phút trễ).
- `ket_thuc_du_kien` (TIMESTAMPTZ, NOT NULL): Thời điểm dự kiến kết thúc ca (quyết định mốc tính phút về sớm).
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DA_PHAN', CHECK in ('DA_PHAN', 'HOAN_THANH', 'VANG_MAT')): Trạng thái xử lý ca.

> ⚠️ Cột `ghi_chu` **không tồn tại**. Mốc thời gian `bat_dau_du_kien` / `ket_thuc_du_kien` là **TIMESTAMPTZ** (không phải TIME) để hỗ trợ ca đêm vượt qua nửa đêm ngày hôm sau.

### Bảng 12: `cham_cong` (Bản ghi quẹt thẻ in/out)
- `ma_cham_cong` (SERIAL, PK): Mã bản ghi chấm công.
- `ma_phan_ca` (INT, FK -> phan_ca.ma_phan_ca, NOT NULL, UNIQUE, ON DELETE CASCADE): Bản ghi phân ca được chấm công — **quan hệ 1-1, không có `ma_nhan_vien` / `ma_ca` / `ngay_cham_cong` riêng** (nhân viên và ngày suy ra qua `phan_ca`).
- `gio_check_in` (TIMESTAMPTZ, NULL): Thời điểm quẹt thẻ vào ca.
- `gio_check_out` (TIMESTAMPTZ, NULL): Thời điểm quẹt thẻ ra ca.
- `phut_di_tre` (INT, NOT NULL, DEFAULT 0): Số phút trễ so với `bat_dau_du_kien`.
- `phut_ve_som` (INT, NOT NULL, DEFAULT 0): Số phút về sớm trước `ket_thuc_du_kien`.
- `gio_tang_ca_ot` (DECIMAL(4,2), NOT NULL, DEFAULT 0.00): Số giờ làm thêm ngoài ca đã được duyệt.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'DUNG_GIO', CHECK in ('DUNG_GIO', 'DI_TRE', 'VE_SOM', 'VANG_MAT')): Kết luận chấm công của ngày đó.
- `phuong_thuc_checkin` (VARCHAR(20), NULL): Phương thức xác thực vị trí: `GPS`, `WIFI`, hoặc `THU_CONG`.
- `toa_do_checkin` (VARCHAR(50), NULL): Kinh độ, vĩ độ GPS thực tế lúc bấm quẹt (VD: `10.776889, 106.700806`).
- `thiet_bi_checkin` (VARCHAR(100), NULL): Định danh thiết bị Mobile App gửi lên để chống mượn máy quẹt hộ.

### Bảng 13: `loai_don` (Danh mục loại đơn)
- `ma_loai_don` (SERIAL, PK): Mã loại đơn.
- `ten_loai_don` (VARCHAR(100), NOT NULL, UNIQUE): Nghỉ phép, Nghỉ bệnh, Thai sản, Thôi việc.
- `co_huong_luong` (BOOLEAN, NOT NULL, DEFAULT FALSE): Quy định có được hưởng lương hay không.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải quyền lợi đi kèm của từng loại đơn.

### Bảng 14: `don_tu` (Đơn từ điện tử)
- `ma_don` (SERIAL, PK): Mã đơn từ.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Người nộp đơn.
- `ma_loai_don` (INT, FK -> loai_don.ma_loai_don, NOT NULL, ON DELETE RESTRICT): Loại đơn nộp.
- `ma_nguoi_duyet` (INT, FK -> nhan_vien.ma_nhan_vien, NULL, ON DELETE SET NULL): Người ký duyệt đơn.
- `tu_ngay` (DATE, NOT NULL): Ngày bắt đầu nghỉ / thôi việc.
- `den_ngay` (DATE, NOT NULL): Ngày kết thúc nghỉ.
- `ly_do` (TEXT, NOT NULL): Lý do nộp đơn.
- `ngay_gui` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm nộp đơn.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'CHO_DUYET', CHECK in ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI')): Trạng thái xử lý.
- `phan_hoi_duyet` (VARCHAR(255), NULL): Ý kiến chỉ đạo khi duyệt hoặc lý do từ chối.
- `ngay_duyet` (TIMESTAMPTZ, NULL): Thời điểm phê duyệt.

### Bảng 15: `ky_luong` (Kỳ tính lương)
- `ma_ky_luong` (SERIAL, PK): Mã kỳ lương.
- `thang` (INT, NOT NULL, CHECK (thang BETWEEN 1 AND 12)): Tháng lương.
- `nam` (INT, NOT NULL, CHECK (nam >= 2000)): Năm lương.
- `tu_ngay` (DATE, NOT NULL): Ngày đầu chu kỳ tính công.
- `den_ngay` (DATE, NOT NULL): Ngày cuối chu kỳ tính công.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'MO', CHECK in ('MO', 'DANG_TINH', 'KHOA_SO')): Trạng thái chu kỳ (mở / đang tính / đã khoá sổ).
- *Ràng buộc:* `UNIQUE (thang, nam)` — mỗi tháng/năm chỉ có đúng một kỳ tính lương.

> ⚠️ Không có cột `ten_ky_luong` (nhãn hiển thị do ứng dụng tự ghép "Lương tháng 10/2026" từ `thang` + `nam`) và không có cột `da_khoa_so` — trạng thái khoá sổ nằm trong `trang_thai = 'KHOA_SO'`.

### Bảng 16: `cau_hinh_khoan_luong` (Cấu hình khoản thu chi)
- `ma_khoan` (SERIAL, PK): Mã khoản thu chi.
- `ten_khoan` (VARCHAR(150), NOT NULL, UNIQUE): Phụ cấp ăn ca, Phụ cấp đêm, Thưởng, Phạt trễ...
- `loai_khoan` (VARCHAR(30), NOT NULL, CHECK in ('PHU_CAP', 'THUONG', 'KHAU_TRU')): Phân loại cộng hoặc trừ.
- `gia_tri_mac_dinh` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Giá trị số tiền cố định (nếu có).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải cách áp dụng khoản thu chi này.

> ⚠️ Cột `ti_le_phan_tram` **không tồn tại** trong CSDL thật. Mọi tỷ lệ phần trăm được lưu riêng ở các bảng chuyên dụng: `cau_hinh_phu_cap.muc_dinh_muc`, `cau_hinh_khau_tru.ty_le_hoac_dinh_muc`, `cau_hinh_tang_ca.he_so` và tỷ lệ BHXH/BHYT/BHTN trong `chi_tiet_tru_bao_hiem`.

### Bảng 17: `cau_hinh_phu_cap` (Cấu hình phụ cấp)
- `ma_phu_cap` (SERIAL, PK): Mã khoản phụ cấp.
- `ten_phu_cap` (VARCHAR(150), NOT NULL, UNIQUE): Phụ cấp ăn ca, Phụ cấp ca đêm, Phụ cấp đi lại, Phụ cấp độc hạ...
- `loai_tinh` (VARCHAR(30), NOT NULL, CHECK in ('THEO_NGAY_CONG', 'CO_DINH_THANG')): Cơ chế tính — nhân với số ngày công hoặc cộng cố định một tháng.
- `muc_dinh_muc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Đơn giá / mức phụ cấp áp dụng.
- `chiu_thue_tncn` (BOOLEAN, NOT NULL, DEFAULT FALSE): Khoản phụ cấp có tính vào thu nhập chịu thuế TNCN hay không.
- `mo_ta` (VARCHAR(255), NULL): Ghi chú chính sách phụ cấp.

### Bảng 18: `cau_hinh_khau_tru` (Cấu hình khoản khấu trừ)
- `ma_khoan_tru` (SERIAL, PK): Mã khoản khấu trừ.
- `ten_khoan_tru` (VARCHAR(150), NOT NULL, UNIQUE): BHXH, BHYT, BHTN, Thuế TNCN, Phạt đi trễ, Tạm ứng...
- `loai_khoan_tru` (VARCHAR(30), NOT NULL, CHECK in ('BAO_HIEM', 'THUE', 'VI_PHAM', 'TAM_UNG')): Nhóm khoản khấu trừ.
- `ty_le_hoac_dinh_muc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tỷ lệ (%) áp dụng, hoặc mức tiền cố định nếu là khoản trừ theo hạn mức.
- `mo_ta` (VARCHAR(255), NULL): Diễn giải cách áp dụng khoản khấu trừ này.

### Bảng 19: `cau_hinh_tang_ca` (Cấu hình tăng ca)
- `ma_loai_ot` (SERIAL, PK): Mã loại tăng ca.
- `ten_loai_ot` (VARCHAR(100), NOT NULL, UNIQUE): Tăng ca ngày thường, Tăng ca ngày nghỉ, Tăng ca đêm, Tăng ca ngày lễ...
- `he_so` (DECIMAL(4,2), NOT NULL, CHECK > 0): Hệ số nhân tiền lương cho 1 giờ tăng ca (VD: ngày thường 1.5, ngày nghỉ 2.0, đêm lễ 3.0).
- `mo_ta` (VARCHAR(255), NULL): Diễn giải quy định áp dụng loại tăng ca này.

### Bảng 20: `phieu_luong` (Phiếu lương tổng hợp)
- `ma_phieu_luong` (SERIAL, PK): Mã phiếu lương.
- `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Nhân viên nhận lương.
- `ma_ky_luong` (INT, FK -> ky_luong.ma_ky_luong, NOT NULL, ON DELETE RESTRICT): Kỳ lương tương ứng.
- `luong_co_ban` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Lương cơ bản snapshot tại thời điểm tính.
- `ngay_cong_thuc_te` (DECIMAL(4,2), NOT NULL, DEFAULT 0.00): Số công làm việc thực tế trong kỳ (số ngày công quy đổi).
- `tien_luong_cong` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tiền lương theo công thức 2 tầng (lương công × ngày công thực tế / 26).
- `tien_tang_ca` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng tiền tăng ca đã được duyệt trong kỳ.
- `tong_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng các khoản phụ cấp cộng vào lương.
- `tong_thuong` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng tiền thưởng / điểm KPI trong kỳ.
- `tong_khau_tru` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng các khoản khấu trừ (BHXH, BHYT, BHTN, thuế TNCN, vi phạm, tạm ứng).
- `thuc_nhan` (DECIMAL(15,2), NOT NULL): Lương Net thực chuyển khoản = tổng thu − tổng khấu trừ.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'NHAP', CHECK in ('NHAP', 'DA_DUYET', 'DA_THANH_TOAN')): Trạng thái duyệt & chi trả.
- *Ràng buộc:* `UNIQUE (ma_nhan_vien, ma_ky_luong)` — mỗi nhân viên chỉ có một phiếu lương cho mỗi kỳ.

> ⚠️ CSDL thật **không có** `tong_thu_nhap` (Gross), `luong_thuc_nhan`, `so_ngay_cong_chuan` và `ngay_lap`. Gross được tính lúc xuất phiếu lương từ tổng các cột thu; thời điểm lập phiếu gắn với `ky_luong.tu_ngay` / `den_ngay`.

### Bảng 21: `chi_tiet_phieu_luong` (Chi tiết phiếu lương)
- `ma_chi_tiet` (SERIAL, PK): Mã bản ghi chi tiết.
- `ma_phieu_luong` (INT, FK -> phieu_luong.ma_phieu_luong, NOT NULL, ON DELETE CASCADE): Phiếu lương chủ.
- `ten_khoan_luu` (VARCHAR(150), NOT NULL): **Snapshot tên khoản** tại thời điểm tính lương.
- `loai_khoan_luu` (VARCHAR(30), NOT NULL): **Snapshot loại khoản** tại thời điểm tính lương (`PHU_CAP` / `THUONG` / `KHAU_TRU`).
- `so_tien` (DECIMAL(15,2), NOT NULL): Số tiền cụ thể được cộng hoặc trừ.
- `ghi_chu` (VARCHAR(255), NULL): Diễn giải công thức tính chi tiết.
- `ma_khoan` (INT, FK -> cau_hinh_khoan_luong.ma_khoan, NULL, ON DELETE RESTRICT): Khoản thu chi chuẩn mà dòng này tham chiếu.
- `ma_loai_ot` (INT, FK -> cau_hinh_tang_ca.ma_loai_ot, NULL, ON DELETE RESTRICT): Loại tăng ca sinh ra dòng chi tiết này.
- `ma_phu_cap` (INT, FK -> cau_hinh_phu_cap.ma_phu_cap, NULL, ON DELETE RESTRICT): Khoản phụ cấp sinh ra dòng chi tiết này.
- `ma_khoan_tru` (INT, FK -> cau_hinh_khau_tru.ma_khoan_tru, NULL, ON DELETE RESTRICT): Khoản khấu trừ sinh ra dòng chi tiết này.
- `loai_khoan` (VARCHAR(30), NULL): Nhóm khoản lưu trữ theo dòng (phụ cấp / tăng ca / khấu trừ...).

> **Vì sao bắt buộc phải có `ten_khoan_luu` + `loai_khoan_luu` (snapshot)?** Bảng chi tiết phiếu lương là **bản ghi bất biến theo thời gian** (immutable): nó phải in ra y hệt những gì nhân viên đã nhận vào tháng đó, kể cả khi HR sau này đổi tên khoản thu chi trong bảng cấu hình. Nếu chỉ lưu `ma_khoan` và hiển thị bằng `JOIN` sang bảng cấu hình thì phiếu lương cũ sẽ bị đổi tên ngoài ý muốn — sai lệch với giấy tờ đã phát hành và vi phạm nguyên tắc chuẩn hóa "lịch sử tài chính bất biến". Vì vậy `ten_khoan_luu` / `loai_khoan_luu` là cột **snapshot bắt buộc**, còn các cột `ma_*` chỉ là liên kết truy vết về cấu hình gốc.

### Bảng 22: `chi_tiet_tru_bao_hiem` (Chi tiết khấu trừ bảo hiểm)
- `ma_chi_tiet_bh` (SERIAL, PK): Mã bản ghi bảo hiểm.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (quan hệ 1-1).
- `luong_dong_bao_hiem` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Mức lương làm căn cứ đóng bảo hiểm của dòng này.
- `ty_le_bhxh` (DECIMAL(5,2), NOT NULL, DEFAULT 8.00): Tỷ lệ hưu trí - xã hội (%).
- `tien_bhxh` (DECIMAL(15,2), NOT NULL): Số tiền BHXH đã trừ.
- `ty_le_bhyt` (DECIMAL(5,2), NOT NULL, DEFAULT 1.50): Tỷ lệ bảo hiểm y tế (%).
- `tien_bhyt` (DECIMAL(15,2), NOT NULL): Số tiền BHYT đã trừ.
- `ty_le_bhtn` (DECIMAL(5,2), NOT NULL, DEFAULT 1.00): Tỷ lệ bảo hiểm thất nghiệp (%).
- `tien_bhtn` (DECIMAL(15,2), NOT NULL): Số tiền BHTN đã trừ.
- `tong_bao_hiem` (DECIMAL(15,2), NOT NULL): Tổng cả 3 khoản bảo hiểm đã trừ trong dòng này.

### Bảng 23: `chi_tiet_tru_thue_tncn` (Chi tiết khấu trừ thuế TNCN)
- `ma_chi_tiet_thue` (SERIAL, PK): Mã bản ghi thuế TNCN.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (quan hệ 1-1).
- `thu_nhap_chiu_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Tổng thu nhập chịu thuế của dòng này.
- `giam_tru_ban_than` (DECIMAL(15,2), NOT NULL, DEFAULT 11000000.00): Mức giảm trừ bản thân 11.000.000 đồng/tháng.
- `so_nguoi_phu_thuoc` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Số người phụ thuộc được đăng ký giảm trừ.
- `giam_tru_phu_thuoc` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00): Tổng mức giảm trừ người phụ thuộc (thường 6.220.000 đồng/người/tháng).
- `thu_nhap_tinh_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Thu nhập tính thuế sau khi trừ các khoản giảm trừ gia đình.
- `tien_thue` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Số thuế TNCN phải nộp theo biểu lũy tiến 5 mức bậc.

### Bảng 24: `chi_tiet_tru_vi_pham_tam_ung` (Chi tiết khấu trừ vi phạm & tạm ứng)
- `ma_chi_tiet_vp` (SERIAL, PK): Mã bản ghi vi phạm / tạm ứng.
- `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NOT NULL, ON DELETE CASCADE): Dòng chi tiết lương chủ (quan hệ 1-1).
- `loai_tru` (VARCHAR(30), NOT NULL, CHECK in ('DI_TRE', 'VE_SOM', 'KY_LUAT', 'LAM_HONG', 'TAM_UNG')): Loại vi phạm bị phạt hoặc khoản tạm ứng.
- `so_phut_vi_pham` (INT, NOT NULL, DEFAULT 0, CHECK >= 0): Số phút vi phạm (áp dụng cho `DI_TRE` / `VE_SOM`).
- `so_tien_tru` (DECIMAL(15,2), NOT NULL, CHECK >= 0): Số tiền thực bị trừ.
- `ly_do_chi_tiet` (VARCHAR(255), NOT NULL): Diễn giải cụ thể lý do khấu trừ (VD: "Trễ 45 phút ca Sáng 12/10").

### Bảng 25: `nhat_ky_quan_tri` (Nhật ký quản trị)
- `ma_nhat_ky` (BIGSERIAL, PK): Mã nhật ký (dùng BIGSERIAL vì bảng này tăng trưởng liên tục theo mọi thao tác quản trị).
- `ma_tai_khoan_thuc_hien` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản thực hiện thao tác.
- `ma_tai_khoan_bi_tac_dong` (INT, FK -> tai_khoan.ma_tai_khoan, NULL, ON DELETE SET NULL): Tài khoản bị tác động bởi thao tác (VD: khi Admin khoá tài khoản của nhân viên).
- `ten_nguoi_thuc_hien` (VARCHAR(100), NOT NULL): Họ tên người thực hiện — lưu chuỗi để nhật ký không bị mất dữ liệu khi tài khoản bị xoá.
- `ten_tai_khoan_bi_tac_dong` (VARCHAR(100), NOT NULL): Tên đăng nhập bị tác động — snapshot tương tự.
- `hanh_dong` (VARCHAR(40), NOT NULL): Mã hành động (VD: `TAO_MOI`, `SUA`, `XOA`, `KHOA_TAI_KHOAN`, `DUYET_DON`).
- `noi_dung` (VARCHAR(500), NOT NULL): Mô tả nội dung thao tác.
- `gia_tri_cu` (VARCHAR(1000), NULL): Giá trị trước khi thay đổi (dạng chuỗi JSON/text).
- `gia_tri_moi` (VARCHAR(1000), NULL): Giá trị sau khi thay đổi (dạng chuỗi JSON/text).
- `ly_do` (VARCHAR(500), NULL): Lý do thực hiện thao tác.
- `dia_chi_ip` (VARCHAR(64), NULL): Địa chỉ IP của thiết bị phát sinh thao tác.
- `thoi_gian` (TIMESTAMPTZ, NOT NULL, DEFAULT CURRENT_TIMESTAMP): Thời điểm thao tác.

---

## 3. Lớp Bảo Mật CSDL (Row Level Security)

Ngoài 25 bảng nghiệp vụ, CSDL còn có **lớp bảo mật cấp hàng (RLS)** được cấu hình thật trên Supabase Cloud. Tóm tắt chốt ngày 07/10/2026 (đã cập nhật thêm bảng thứ 25 ngày 09/10/2026):

- **Bật RLS: 25/25 bảng** — không bảng nào để trống (`ENABLE ROW LEVEL SECURITY` áp dụng cho toàn bộ bảng trong schema `public`).
- **Tổng cộng 31 policy RLS** (đếm trực tiếp từ dump `CREATE POLICY` ngày 07/10/2026), chia làm 2 nhóm theo thao tác:

| Nhóm policy | Số lượng | Thao tác | Ví dụ tên policy |
|:---|:---:|:---|:---|
| Đọc (`FOR SELECT`) | 23 | Xem danh sách, tự động giới hạn theo người đang đăng nhập | `doc_ho_so_nhan_vien`, `doc_phieu_luong`, `doc_don_tu` |
| Ghi (`FOR INSERT`) | 3 | Nhân viên tự nộp đơn / tự ghi chấm công; quản lý xếp ca | `nop_don_tu_cua_toi`, `them_cham_cong_cua_toi`, `them_phan_ca_quan_ly` |
| Ghi (`FOR UPDATE`) | 5 | Nhân viên tự sửa; quản lý duyệt và sửa theo phạm vi phòng ban | `duyet_don_tu_quan_ly`, `sua_cham_cong_quan_ly`, `sua_phan_ca_quan_ly` |

> **Ghi chú phạm vi:**
> - Các bảng danh mục dùng chung (`phong_ban`, `chuc_vu`, `loai_don`, `ca_lam_viec`, `vai_tro`, `quyen`, `vai_tro_quyen`) chỉ mở **đọc** cho mọi user đã đăng nhập (`auth.uid() IS NOT NULL`); mọi thao tác thêm/sửa/xoá danh mục thực hiện qua `service_role` phía server.
> - Bảng `tai_khoan` có RLS bật nhưng **không có policy nào**, kèm `GRANT ALL ... TO service_role` (không cấp cho `authenticated`) — tức là bảng này bị khoá hoàn toàn với client, chỉ truy cập được qua backend/`service_role`.
> - ⚠️ Bảng **`yeu_cau_tai_khoan`** (Bảng 9) được kế thừa cùng cách xử lý với `tai_khoan`: không mở policy cho client, mọi thao tác tạo yêu cầu (HR) và cấp tài khoản (IT) đều đi qua Web Admin với `service_role`. Lý do: bảng này chứa `ten_dang_nhap` — thông tin định danh đăng nhập — nên không được phép lộ bất kỳ cột nào cho `authenticated`.

### 3.1. Hàm bảo mật `SECURITY DEFINER`

Các policy **không** đọc trực tiếp bảng phân quyền mà thông qua các hàm `SECURITY DEFINER` (đã `REVOKE ALL ... FROM PUBLIC`, chỉ cấp quyền cho `anon` / `authenticated` / `service_role`), để tránh vòng lặp vô hạn khi policy truy vấn lại chính bảng đang bảo vệ:

| Hàm | Trả về | Công dụng trong policy |
|:---|:---|:---|
| `nhan_vien_hien_tai()` | `INT` | Mã nhân viên của user đang đăng nhập, đọc từ `auth.jwt() -> app_metadata ->> 'ma_nhan_vien'`. |
| `vai_tro_hien_tai()` | `INT` | Mã vai trò của user đang đăng nhập, đọc từ `app_metadata ->> 'ma_vai_tro'`. |
| `la_quan_ly_hien_tai()` | `BOOLEAN` | `TRUE` khi vai trò thuộc nhóm quản lý (mã vai trò ∈ {1, 2, 3} = Admin, HR, Manager); nhóm nhân viên bị chặn. |
| `phong_ban_hien_tai()` | `INT` | Mã phòng ban của user đang đăng nhập — dùng để Manager chỉ được sửa/xếp ca trong phòng ban mình. |
| `phong_ban_cua_don(p_ma_don)` | `INT` | Phòng ban sở hữu của một đơn từ — dùng cho policy duyệt đơn cấp quản lý. |
| `xac_thuc_dang_nhap(p_ten_dang_nhap, p_mat_khau)` | `TABLE` | Hàm xác thực cũ, **giữ lại để tương thích, KHÔNG phải đường xác thực chính** (đường chính là Supabase Auth / GoTrue). |

### 3.2. Cơ chế nhúng quyền vào JWT: `custom_access_token_hook`

Hàm `custom_access_token_hook(event jsonb)` (`SECURITY DEFINER`, chỉ `service_role` và `supabase_auth_admin` được gọi) được GoTrue gọi mỗi lần cấp access token:

1. Đối chiếu `claims.email` với `nhan_vien.email` (không phân biệt hoa thường), nối sang `tai_khoan` để lấy `ma_nhan_vien` và `ma_vai_tro`, chỉ nhận tài khoản đang `trang_thai = TRUE`.
2. Nếu không khớp tài khoản nào đang hoạt động → trả claim gốc, **không thêm quyền nào** → mọi policy sẽ chặn.
3. Gom danh sách quyền của vai trò từ `vai_tro_quyen` → `quyen`, lấy **mã quyền dạng chuỗi `ma_quyen_code`** (không phải mã số) — đây chính là các hằng trong `Security/PermissionCodes.cs` của Web Admin (`CHAM_CONG_DI_DONG`, `NOP_DON_TU`, `CAP_TAI_KHOAN_MOBILE`, ...).
4. **Merge** (không thay thế) `ma_nhan_vien`, `ma_vai_tro`, `quyen` vào `app_metadata` bằng `jsonb_set` để giữ nguyên các key GoTrue tự thêm (`provider`, `providers`).

Nhờ đó, các hàm `nhan_vien_hien_tai()` / `vai_tro_hien_tai()` / `la_quan_ly_hien_tai()` chỉ cần đọc claim trong JWT là đủ ra quyết định, không tốn một vòng truy vấn bảng nào cho mỗi lần đánh giá policy.

### 3.3. Bảng trung gian trong quy trình cấp tài khoản: `yeu_cau_tai_khoan`

Bảng `yeu_cau_tai_khoan` (Bảng 9) nằm **ngoài** phạm vi JWT claim: nó không phải bảng tra cứu quyền mà là **hàng đợi công việc** giữa hai bộ phận. Cách kiểm tra quyền trên bảng này vì vậy diễn ra ở tầng ứng dụng (Web Admin), không nằm trong policy:

| Quyền cần có (`ma_quyen_code`) | Ai được phép | Hành động trên `yeu_cau_tai_khoan` |
|:---|:---|:---|
| `CAP_TAI_KHOAN_MOBILE` | Vai trò **Admin** và **IT** | Xem danh sách yêu cầu đang chờ, tiếp nhận (`CHO_IT` → `DANG_XU_LY`), cấp (`DA_CAP`) hoặc từ chối (`TU_CHOI` kèm `ghi_chu_tu_choi`) |
| *(không cần quyền đặc biệt — chỉ cần chức danh HR)* | Phòng **HR** | Tạo yêu cầu sau khi duyệt HĐLĐ (`ngay_gui`, `ma_nguoi_gui`) |

> **Vì sao tách `CAP_TAI_KHOAN_MOBILE` khỏi `QL_TAI_KHOAN`?** Nếu gộp chung, HR sẽ theo đường `QL_TAI_KHOAN` mà lỡ tay cấp tài khoản Mobile — tức là tự mở cửa vào toàn bộ dữ liệu mà RLS policy bảo vệ. Tách quyền là để **phân định trách nhiệm**: HR quản lý hồ sơ tài khoản, IT là bộ phận duy nhất (cùng Admin) được cấp quyền sinh user trong `auth.users`. Ba trigger CSDL chặn tài khoản tự cấp quyền này cho chính mình.