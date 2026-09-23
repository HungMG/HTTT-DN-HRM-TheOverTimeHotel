# PRD 02: Phân Tích Yêu Cầu & Sơ Đồ Phân Rã Chức Năng (BFD)

**Dự án:** Hệ thống Quản lý Nhân sự & Tiền lương Khách sạn The OverTime Hotel  
**Chuyên ngành:** Hệ thống Thông tin Doanh nghiệp (HTTTDN) / Công nghệ Phần mềm  
**Nền tảng công nghệ:** C# .NET 10 (ASP.NET Core MVC & .NET MAUI) · Supabase Cloud Database (PostgreSQL 17 bảng)  
**Tài liệu trực quan kèm theo:** Xem sơ đồ đồ họa tương tác tại `docs project/diagrams/HRM_KhachSan_BFD_IPO.html`

---

## 1. Sơ Đồ Cây Phân Rã Chức Năng BFD Tổng Quan (Level 1 & 2)

Sơ đồ phân rã chức năng nghiệp vụ (Business Function Diagram - BFD) thể hiện cấu trúc phân cấp từ Tiến trình gốc trung tâm (`0.0`) xuống 8 phân hệ nghiệp vụ Cấp 1 và 39 chức năng thành phần Cấp 2:

```text
                                HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG
                                      THE OVERTIME HOTEL (0.0)
                                                 │
    ┌─────────────┬─────────────┬────────────────┼─────────────┬─────────────┬─────────────┬─────────────┐
    │             │             │                │             │             │             │             │
┌───▼───┐     ┌───▼───┐     ┌───▼───┐        ┌───▼───┐     ┌───▼───┐     ┌───▼───┐     ┌───▼───┐     ┌───▼───┐
│  1.0  │     │  2.0  │     │  3.0  │        │  4.0  │     │  5.0  │     │  6.0  │     │  7.0  │     │  8.0  │
│Quản lý│     │Tài    │     │Ca làm │        │Quản lý│     │Tính   │     │Phiếu  │     │Báo cáo│     │Quản   │
│Nhân sự│     │khoản &│     │& Chấm │        │Đơn từ │     │lương  │     │lương  │     │& Thống│     │trị Hệ │
│ & HĐLĐ│     │ RBAC  │     │ công  │        │điện tử│     │tự động│     │& PDF  │     │  kê   │     │ thống │
└───┬───┘     └───┬───┘     └───┬───┘        └───┬───┘     └───┬───┘     └───┬───┘     └───┬───┘     └───┬───┘
    ├──1.1 Hồ sơ  ├──2.1 Đăng   ├──3.1 Mẫu ca    ├──4.1 Nghỉ   ├──5.1 Lương  ├──6.1 Xem    ├──7.1 Biến   ├──8.1 Phòng
    │   mới       │   nhập/xuất │   24/7         │   phép năm  │   ngày công │   chi tiết  │   động NS   │   ban
    ├──1.2 Sửa hồ ├──2.2 Quản lý├──3.2 Lập lịch  ├──4.2 Nghỉ   ├──5.2 Tiền OT├──6.2 Lịch sử├──7.2 Cơ cấu ├──8.2 Chức
    │   sơ        │   tài khoản │   xếp ca       │   ốm (BHXH) │   (1.5-3.0) │   thu nhập  │   bộ phận   │   vụ
    ├──1.3 Quản lý├──2.3 Phân   ├──3.3 Quẹt thẻ  ├──4.3 Nghỉ   ├──5.3 Phụ cấp├──6.3 Xuất   ├──7.3 Học vấn├──8.3 Khung
    │   HĐLĐ      │   vai trò   │   Check-in/out │   thai sản  │   đêm 30%   │   QuestPDF  │   thâm niên │   giờ ca
    ├──1.4 Lọc/Tìm└──2.4 Phân   ├──3.4 Theo dõi  ├──4.4 Đơn xin├──5.4 Ăn ca  └──6.4 Quyết  ├──7.4 Quỹ    ├──8.4 Tham số
    │   kiếm NV       quyền RBAC│   trễ / sớm    │   thôi việc │   & thưởng      toán năm  │   lương/NS  │   lương/phạt
    └──1.5 Điều                 └──3.5 Tính giờ  └──4.5 Xét    ├──5.5 Khấu                 └──7.5 Vi phạm└──8.5 Sao lưu
        chuyển                       OT              duyệt đơn │   trừ 10.5%                   chấm công     & phục hồi
                                                               └──5.6 Phạt &
                                                                   Lương Net
```

---

## 2. Bảng Tổng Hợp 8 Phân Hệ Nghiệp Vụ Cấp 1

| Mã TT | Tên Phân Hệ Cấp 1 | Số Lượng Cấp 2 | Vai Trò Chịu Trách Nhiệm | Phạm Vi Nghiệp Vụ Trong Khách Sạn |
|:---:|:---|:---:|:---|:---|
| **1.0** | Quản lý Nhân sự & HĐLĐ | 5 chức năng | Phòng Hành chính - Nhân sự (HR) | Hồ sơ lý lịch, ký kết và theo dõi hạn HĐLĐ, điều động công tác. |
| **2.0** | Tài khoản & Phân quyền (RBAC) | 4 chức năng | Quản trị viên hệ thống (Admin) | Xác thực danh tính, bảo mật tài khoản, phân quyền 4 vai trò. |
| **3.0** | Ca làm & Chấm công 24/7 | 5 chức năng | Quản lý bộ phận & Nhân viên | Vận hành 3 ca liên tục, quẹt thẻ di động, tính phút trễ/sớm và giờ OT. |
| **4.0** | Quản lý Đơn từ điện tử | 5 chức năng | Nhân viên, Quản lý, Nhân sự | Nộp và phê duyệt 4 loại đơn: Phép năm, Nghỉ ốm, Thai sản, Thôi việc. |
| **5.0** | Tính toán Lương tự động | 6 chức năng | Phòng Hành chính - Nhân sự (HR) | Tự động hóa công thức 2 tầng: Gross (công, OT, đêm, thưởng) ➔ Net. |
| **6.0** | Phiếu lương & Xuất PDF | 4 chức năng | Nhân viên & Phòng Nhân sự | Tra cứu lương cá nhân, xuất phiếu lương QuestPDF, quyết toán năm. |
| **7.0** | Báo cáo & Thống kê Quản trị | 5 chức năng | Ban Giám đốc & Phòng Nhân sự | Biến động nhân lực, tỷ lệ chuyên cần, đối soát ngân sách quỹ lương. |
| **8.0** | Quản trị Hệ thống | 5 chức năng | Quản trị viên hệ thống (Admin) | Danh mục tổ chức, cấu hình tham số lương/phạt, sao lưu an toàn CSDL. |

---

## 3. Đặc Tả Chi Tiết 39 Chức Năng Cấp 2 Theo Mô Hình IPO (Input – Process – Output)

---

### Phân Hệ 1.0: Quản Lý Nhân Sự & Hợp Đồng Lao Động

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Thông tin lý lịch nhân viên: Họ tên, ngày sinh, giới tính, SĐT, email, địa chỉ, học vấn     │
│ • Thông tin đơn vị: Mã phòng ban (ma_phong_ban), Mã chức vụ (ma_chuc_vu), Ngày vào làm        │
│ • Thông tin hợp đồng: Số HĐLĐ, loại HĐ, ngày hiệu lực, ngày hết hạn, mức lương cơ bản (> 0)  │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ (PROCESS)                                                                  │
│ 1. Validate định dạng Email (RFC 5322), SĐT (10 chữ số) và kiểm tra tính duy nhất trong DB    │
│ 2. Kiểm tra điều kiện tuổi lao động: Tuổi = Năm hiện tại - Năm sinh >= 18 tuổi                │
│ 3. Tạo mã nhân viên tự động; gán trạng thái công tác mặc định: DangLamViec                     │
│ 4. Kiểm tra hợp đồng lao động: Ngày kết thúc > Ngày bắt đầu; Cập nhật HĐ cũ về HetHan         │
│ 5. Tự động quét cảnh báo hợp đồng: Lọc danh sách HĐ có (ngay_ket_thuc - CURRENT_DATE) <= 30   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Bản ghi mới tạo trong bảng nhan_vien với mã định danh ma_nhan_vien                          │
│ • Bản ghi hợp đồng mới trong bảng hop_dong mang trạng thái HieuLuc                            │
│ • Danh sách cảnh báo hợp đồng sắp hết hạn hiển thị trên giao diện Dashboard của HR            │
│ • Dữ liệu lưu trữ đồng bộ tại: nhan_vien, hop_dong, phong_ban, chuc_vu                        │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **1.1** | **Thêm hồ sơ nhân viên mới** | HR | • Họ tên, ngày sinh, giới tính<br>• SĐT, email, địa chỉ, học vấn<br>• Phòng ban, chức vụ, ngày vào | 1. Validate email RFC 5322 & SĐT 10 chữ số.<br>2. Kiểm tra độ tuổi lao động $\ge 18$.<br>3. Kiểm tra tính duy nhất của email và SĐT.<br>4. Tạo bản ghi mới, gán trạng thái `DangLamViec`. | • Bản ghi mới sinh tự động `ma_nhan_vien`.<br>• Thông báo UI thêm thành công.<br>• Điều hướng gợi ý ký HĐLĐ.<br>💾 `nhan_vien`, `phong_ban`, `chuc_vu` |
| **1.2** | **Sửa, cập nhật lý lịch** | HR | • `ma_nhan_vien`<br>• Các trường thông tin cần sửa<br>• Trạng thái (`DangLamViec`, `NghiPhep`, `NghiViec`) | 1. Xác minh `ma_nhan_vien` tồn tại trong hệ thống.<br>2. Check không trùng SĐT/email với nhân viên khác.<br>3. Kiểm tra ràng buộc khi đổi sang `NghiViec`.<br>4. Cập nhật các trường thông tin vào CSDL. | • Hồ sơ nhân viên cập nhật thông tin mới.<br>• Badge trạng thái đổi màu trên danh sách.<br>💾 `nhan_vien` |
| **1.3** | **Quản lý & Cảnh báo HĐLĐ** | HR | • `ma_nhan_vien`, `so_hop_dong`<br>• Loại HĐ, ngày bắt đầu/kết thúc<br>• Lương cơ bản thỏa thuận | 1. Kiểm tra tính duy nhất của `so_hop_dong`.<br>2. Kiểm tra: Ngày kết thúc > Ngày bắt đầu.<br>3. Kiểm tra: `luong_co_ban > 0`.<br>4. Chuyển HĐ cũ sang `HetHan`.<br>5. **Quét cảnh báo:** Lọc HĐ có hạn $\le 30$ ngày. | • Bản ghi mới trong `hop_dong` (`HieuLuc`).<br>• Danh sách cảnh báo tái ký HĐ trên HR Dashboard (Badge Vàng/Đỏ).<br>💾 `hop_dong`, `nhan_vien` |
| **1.4** | **Tìm kiếm, lọc nhân sự** | HR / Mgr / Admin | • Từ khóa (Tên, Mã NV, SĐT, Email)<br>• Bộ lọc: Phòng ban, Chức vụ, Trạng thái<br>• Tham số phân trang (page, pageSize) | 1. Phân quyền: Manager chỉ xem phòng ban mình; HR/Admin xem toàn khách sạn.<br>2. Tạo câu truy vấn động LINQ/SQL kết hợp nhiều điều kiện.<br>3. Đếm tổng bản ghi và thực thi phân trang. | • Danh sách nhân viên thỏa mãn điều kiện lọc.<br>• Thanh phân trang và nhãn tổng số kết quả.<br>💾 `nhan_vien`, `phong_ban`, `chuc_vu` |
| **1.5** | **Bổ nhiệm, điều chuyển** | HR | • `ma_nhan_vien`<br>• `ma_phong_ban_moi`, `ma_chuc_vu_moi`<br>• Quyết định điều chuyển, ngày hiệu lực | 1. Kiểm tra nhân viên đang ở trạng thái `DangLamViec`.<br>2. Kiểm tra vị trí mới hợp lệ và khác vị trí hiện tại.<br>3. Cập nhật mã phòng ban và chức vụ mới vào hồ sơ.<br>4. Ghi nhận lịch sử điều động công tác. | • Hồ sơ nhân viên hiển thị vị trí công tác mới.<br>• Thông báo quyết định bổ nhiệm thành công.<br>💾 `nhan_vien`, `phong_ban`, `chuc_vu` |

---

### Phân Hệ 2.0: Tài Khoản & Phân Quyền (RBAC)

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Tên đăng nhập (ten_dang_nhap), Mật khẩu người dùng nhập (mat_khau)                          │
│ • Mã tài khoản cần quản trị (ma_tai_khoan), Mật khẩu mới khi cấp lại                          │
│ • Mã vai trò mục tiêu: Admin (1), HR (2), Manager (3), Employee (4)                           │
│ • Danh sách mã quyền (ma_quyen) được tích chọn từ ma trận phân quyền                          │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ (PROCESS)                                                                  │
│ 1. Xác thực đăng nhập: Kiểm tra tài khoản tồn tại và trạng thái HoatDong                      │
│ 2. Đối soát mật khẩu: Dùng thuật toán băm an toàn (PBKDF2/BCrypt) so sánh với mat_khau_bam    │
│ 3. Nạp vai trò & quyền hạn: Đọc từ vai_tro, vai_tro_quyen, quyen để nạp Claims                │
│ 4. Khởi tạo phiên: Cấp Authentication Cookie (Web Admin) hoặc JWT Token (Mobile App)          │
│ 5. Quản trị tài khoản: Khóa/Mở account, cấp mật khẩu mới, ngăn chặn tự khóa Super Admin       │
│ 6. Đồng bộ quyền: Cập nhật các cặp (ma_vai_tro, ma_quyen) trong bảng vai_tro_quyen           │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Phiên làm việc xác thực bảo mật, điều hướng người dùng vào Dashboard đúng thẩm quyền         │
│ • Trạng thái tài khoản được cập nhật (HoatDong hoặc BiKhoa)                                   │
│ • Ma trận phân quyền RBAC được đồng bộ tức thì trên toàn hệ thống                             │
│ • Dữ liệu lưu trữ tại: tai_khoan, vai_tro, quyen, vai_tro_quyen                               │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **2.1** | **Đăng nhập / Đăng xuất** | Toàn bộ | • `ten_dang_nhap`, `mat_khau`<br>• Nền tảng truy cập (Web/Mobile) | 1. Tìm tài khoản; nếu `trang_thai = 'BiKhoa'` thì chặn.<br>2. So khớp mật khẩu băm PBKDF2/BCrypt.<br>3. Nạp danh sách quyền từ `vai_tro_quyen`.<br>4. Tạo Auth Cookie (Web) hoặc JWT Session (Mobile).<br>5. Đăng xuất: Xóa Cookie / hủy phiên làm việc. | • Đăng nhập thành công: Chuyển hướng Dashboard phù hợp.<br>• Thất bại: Thông báo lỗi tài khoản hoặc mật khẩu.<br>💾 `tai_khoan`, `vai_tro_quyen`, `quyen` |
| **2.2** | **Quản lý tài khoản (Khóa/Mở)** | Admin | • `ma_tai_khoan`<br>• Thao tác: Khóa / Mở / Reset pass<br>• Mật khẩu mới (khi reset) | 1. Chặn Admin tự khóa tài khoản của chính mình.<br>2. Cập nhật `trang_thai = 'BiKhoa'` hoặc `'HoatDong'`.<br>3. Reset: Băm mật khẩu mới $\rightarrow$ Lưu `mat_khau_bam`. | • Trạng thái tài khoản đổi màu trên giao diện.<br>• Mật khẩu mới có hiệu lực ngay lập tức.<br>💾 `tai_khoan` |
| **2.3** | **Phân 4 vai trò chuẩn** | Admin | • `ma_tai_khoan`<br>• `ma_vai_tro` mới chỉ định | 1. Kiểm tra tài khoản và vai trò hợp lệ trong DB.<br>2. Kiểm tra ràng buộc: Luôn duy trì tối thiểu 01 Admin.<br>3. Cập nhật `ma_vai_tro` vào bảng `tai_khoan`. | • Quyền hạn người dùng cập nhật ở lần login tiếp theo.<br>• Cập nhật vai trò hiển thị trên bảng quản lý.<br>💾 `tai_khoan`, `vai_tro` |
| **2.4** | **Quản lý danh mục quyền (RBAC)** | Admin | • `ma_vai_tro`<br>• Danh sách `ma_quyen` được chọn | 1. Bắt đầu Transaction trong CSDL.<br>2. Xóa các quyền cũ của vai trò trong `vai_tro_quyen`.<br>3. Chèn hàng loạt các quyền mới được tích chọn.<br>4. Làm mới bộ đệm phân quyền (Refresh Cache). | • Bảng liên kết `vai_tro_quyen` được cập nhật đồng bộ.<br>• Các tài khoản mang vai trò lập tức nhận quyền mới.<br>💾 `vai_tro_quyen`, `quyen`, `vai_tro` |

---

### Phân Hệ 3.0: Ca Làm & Chấm Công 24/7

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Danh mục 3 ca chuẩn 24/7: Ca Sáng (6h-14h), Ca Chiều (14h-22h), Ca Đêm (22h-6h)             │
│ • Lịch phân ca tuần: Mã nhân viên (ma_nhan_vien), Mã ca (ma_ca), Ngày làm việc (ngay_lam_viec)│
│ • Dữ liệu quẹt thẻ di động: ma_nhan_vien, Thời gian thực tế NOW()                              │
│ • Dữ liệu kiểm thực vị trí: Tọa độ GPS (toa_do_checkin), Tên WiFi nội bộ, Định danh thiết bị │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ (PROCESS)                                                                  │
│ 1. Xếp ca: Kiểm tra Manager chỉ xếp bộ phận mình; Chặn xếp trùng; Nghỉ giữa 2 ca >= 8 tiếng   │
│ 2. Xác thực vị trí thực tế 2 lớp (Hybrid): GPS bán kính <= 100m HOẶC kết nối đúng WiFi nội bộ  │
│ 3. Check-in: Tra cứu ca hôm đó trong phan_ca; Lưu gio_vao, phuong_thuc, toa_do vào cham_cong │
│ 4. Tính trễ tự động: Nếu gio_vao > gio_bat_dau ➔ so_phut_tre = gio_vao - gio_bat_dau          │
│ 5. Check-out: Lưu gio_ra; Tính về sớm nếu gio_ra < gio_ket_thuc; Tính giờ OT nếu làm thêm    │
│ 6. Đánh dấu trạng thái tự động: HopLe, DiTre, VeSom; Ghi nhận lưu vết kiểm toán chống gian lận│
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Bảng lịch phân ca tuần dạng lưới trên Web Admin và lịch cá nhân trên Mobile App             │
│ • Bản ghi cham_cong chứa đầy đủ gio_vao, gio_ra, số phút trễ/sớm, giờ OT, phương thức/tọa độ  │
│ • Bằng chứng đối soát vị trí (Audit Trail) phục vụ giải quyết khiếu nại kỷ luật lao động      │
│ • Dữ liệu lưu trữ tại: ca_lam_viec, phan_ca, cham_cong                                        │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **3.1** | **Quản lý danh mục mẫu ca** | Admin / HR | • Tên ca: Sáng, Chiều, Đêm<br>• `gio_bat_dau`, `gio_ket_thuc`<br>• Hệ số lương, cờ `la_ca_dem` | 1. Chuẩn hóa 3 khung giờ ca liên tục 24/7 (mỗi ca 8 tiếng).<br>2. Bật cờ `la_ca_dem = TRUE` cho ca 22h - 6h sáng.<br>3. Chặn xóa danh mục ca nếu đã có dữ liệu xếp ca. | • Bản ghi danh mục ca lưu trong `ca_lam_viec`.<br>• Khung giờ sẵn sàng cho lập lịch xếp ca.<br>💾 `ca_lam_viec` |
| **3.2** | **Lập lịch xếp ca tuần/tháng** | Manager (Bộ phận)<br>HR (Toàn KS) | • `ma_nhan_vien`, `ma_ca`<br>• `ngay_lam_viec`, ghi chú phân ca | 1. Manager chỉ được phân ca cho nhân viên bộ phận mình.<br>2. Chặn xếp 2 ca chồng chéo cho 1 nhân viên trong ngày.<br>3. Đảm bảo thời gian nghỉ giữa 2 ca liên tiếp $\ge 8$ tiếng.<br>4. Lưu các bản ghi phân ca vào CSDL. | • Bảng lịch ca tuần dạng lưới trên Web Admin.<br>• Lịch ca cá nhân hiển thị trên Mobile App nhân viên.<br>💾 `phan_ca`, `ca_lam_viec`, `nhan_vien` |
| **3.3** | **Quẹt thẻ Check-in / Check-out** | Employee (Mobile App) | • `ma_nhan_vien` phiên đăng nhập<br>• Thời gian thực tế `NOW()`<br>• Tọa độ GPS (`toa_do_checkin`)<br>• Tên WiFi, Mã máy (`thiet_bi`) | 1. **Xác thực vị trí (Chống gian lận):** Check GPS $\le 100m$ hoặc kết nối WiFi `OverTimeHotel_Staff`.<br>2. Tra cứu ca được phân công từ `phan_ca`.<br>3. **Check-in:** Tạo bản ghi `cham_cong` mới (`gio_vao`, `phuong_thuc_checkin`, `toa_do_checkin`, `thiet_bi_checkin`).<br>4. **Check-out:** Tìm bản ghi đang mở $\rightarrow$ Cập nhật `gio_ra`. | • Bản ghi chấm công lưu kèm bằng chứng kiểm toán vị trí.<br>• Thông báo UI Mobile: *"Quẹt thẻ thành công lúc HH:mm [GPS/WiFi]"*.<br>• Chặn quẹt thẻ nếu ở ngoài phạm vi khách sạn.<br>💾 `cham_cong`, `phan_ca` |
| **3.4** | **Tự động theo dõi trễ, sớm** | Hệ thống tự động | • `gio_vao`, `gio_ra` từ `cham_cong`<br>• `gio_bat_dau`, `gio_ket_thuc` từ ca | 1. Nếu `gio_vao > gio_bat_dau`: Tính `so_phut_tre` $\rightarrow$ Gán `trang_thai = 'DiTre'`.<br>2. Nếu `gio_ra < gio_ket_thuc`: Tính `so_phut_ve_som` $\rightarrow$ Gán `trang_thai = 'VeSom'`. | • Các cột `so_phut_tre`, `so_phut_ve_som` được tự động điền.<br>• Bảng giám sát chuyên cần cập nhật số liệu.<br>💾 `cham_cong`, `ca_lam_viec` |
| **3.5** | **Tính toán số giờ làm thêm (OT)** | Hệ thống tự động | • `gio_ra` thực tế từ `cham_cong`<br>• `gio_ket_thuc` quy định của ca | 1. Kiểm tra làm thêm sau giờ ca vượt ngưỡng $\ge 30$ phút.<br>2. Tính `so_gio_ot = (gio_ra - gio_ket_thuc) / 60`.<br>3. Quy đổi làm tròn theo bước 0.5 giờ (1.0h, 1.5h, 2.0h...). | • Trường `so_gio_ot` trong `cham_cong` được lưu dữ liệu.<br>• Số liệu sẵn sàng chuyển sang tính tiền OT ở 5.2.<br>💾 `cham_cong` |

---

### Phân Hệ 4.0: Quản Lý Đơn Từ Điện Tử

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Loại đơn: Nghỉ phép năm (1), Nghỉ ốm (2), Nghỉ thai sản (3), Đơn xin thôi việc (4)          │
│ • Thời gian xin nghỉ: Ngày bắt đầu (ngay_bat_dau), Ngày kết thúc (ngay_ket_thuc), Lý do xin nghỉ│
│ • Tài liệu đính kèm: Ảnh chụp giấy chứng nhận nghỉ việc hưởng BHXH, giấy khám thai/chứng sinh  │
│ • Thao tác xét duyệt: Quyết định (DaDuyet / TuChoi), Lý do từ chối (bắt buộc khi từ chối)     │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ (PROCESS)                                                                  │
│ 1. Validate: Kiểm tra ngày nghỉ trong tương lai; kiểm tra quỹ ngày phép năm còn lại          │
│ 2. Đơn thôi việc: Kiểm tra thời hạn báo trước theo luật lao động (30 ngày hoặc 45 ngày)       │
│ 3. Tạo đơn: Khởi tạo bản ghi don_tu mang trạng thái ChoDuyet; gửi Push Notification           │
│ 4. Phân luồng thẩm quyền: Manager duyệt nghỉ phép/ốm; HR duyệt chế độ thai sản và thôi việc   │
│ 5. Xét duyệt: Cập nhật DaDuyet/TuChoi; nếu duyệt phép năm thì ghi nhận công nghỉ phép có lương│
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Bản ghi đơn từ được lưu và cập nhật trạng thái trong bảng don_tu                            │
│ • Thông báo kết quả phê duyệt kèm lý do được gửi tức thì về ứng dụng Mobile App nhân viên     │
│ • Tự động trừ số ngày phép trong quỹ phép năm và cộng ngày công có lương cho kỳ tính lương     │
│ • Dữ liệu lưu trữ tại: don_tu, loai_don, nhan_vien                                            │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **4.1** | **Đơn xin nghỉ phép năm** | Employee (Mobile) | • `ma_loai_don = 1` (Phép năm)<br>• `ngay_bat_dau`, `ngay_ket_thuc`<br>• `ly_do` xin nghỉ | 1. Validate `ngay_bat_dau >= CURRENT_DATE`.<br>2. Tính tổng số ngày xin nghỉ.<br>3. Kiểm tra số ngày phép năm còn lại (tiêu chuẩn 12 ngày/năm).<br>4. Tạo bản ghi `don_tu` trạng thái `ChoDuyet`. | • Đơn lưu vào `don_tu` (Badge vàng `ChoDuyet`).<br>• Gửi thông báo đến Quản lý bộ phận trực tiếp.<br>💾 `don_tu`, `loai_don`, `nhan_vien` |
| **4.2** | **Đơn nghỉ ốm (hưởng BHXH)** | Employee (Mobile) | • `ma_loai_don = 2` (Nghỉ ốm)<br>• Ngày bắt đầu/kết thúc, lý do<br>• Ảnh chụp giấy chứng nhận y tế | 1. Kiểm tra thời gian nghỉ hợp lệ.<br>2. Kiểm tra có file đính kèm chứng từ y tế hợp lệ.<br>3. Tạo bản ghi `don_tu` trạng thái `ChoDuyet`.<br>4. Chuyển tiếp đơn đến HR tiếp nhận hồ sơ hưởng chế độ BHXH. | • Đơn lưu vào bảng `don_tu`.<br>• HR tiếp nhận hồ sơ y tế giải quyết trợ cấp.<br>💾 `don_tu`, `loai_don` |
| **4.3** | **Đơn nghỉ chế độ thai sản** | Employee (Mobile) | • `ma_loai_don = 3` (Thai sản)<br>• Ngày bắt đầu nghỉ, thời gian (6 tháng)<br>• Giấy khám thai / Giấy chứng sinh | 1. Validate thời gian nghỉ theo luật lao động (06 tháng).<br>2. Kiểm tra tính hợp lệ của giấy tờ y tế đính kèm.<br>3. Tạo bản ghi `don_tu` trạng thái `ChoDuyet`.<br>4. Phân luồng chuyển thẳng cấp duyệt lên Phòng HR. | • Đơn thai sản lưu trong `don_tu`.<br>• HR nhận thông báo tiếp nhận hồ sơ thai sản.<br>💾 `don_tu`, `loai_don` |
| **4.4** | **Đơn xin thôi việc / chấm dứt HĐ**| Employee (Mobile) | • `ma_loai_don = 4` (Thôi việc)<br>• Ngày thôi việc dự kiến<br>• Lý do & Cam kết bàn giao | 1. Tra cứu loại HĐLĐ của nhân viên trong bảng `hop_dong`.<br>2. Kiểm tra thời hạn báo trước theo Điều 35 BLLĐ (30 ngày với HĐ xác định thời hạn, 45 ngày với vô thời hạn).<br>3. Tạo bản ghi `don_tu` trạng thái `ChoDuyet`. | • Đơn thôi việc lưu vào bảng `don_tu`.<br>• Cảnh báo ưu tiên gửi Manager & Trưởng phòng HR.<br>💾 `don_tu`, `loai_don`, `hop_dong` |
| **4.5** | **Xét duyệt đơn từ trực tuyến** | Manager (Nghỉ phép)<br>HR (Thai sản/Thôi việc)| • `ma_don`<br>• Quyết định: `DaDuyet` hoặc `TuChoi`<br>• `ly_do_duyet` (bắt buộc khi từ chối) | 1. Kiểm tra thẩm quyền duyệt của vai trò.<br>2. Nếu từ chối: Bắt buộc nhập lý do giải thích.<br>3. Cập nhật `trang_thai`, `ngay_duyet`, `nguoi_duyet`.<br>4. Nếu duyệt phép năm: Ghi nhận công phép hưởng lương. | • Trạng thái đơn chuyển `DaDuyet` hoặc `TuChoi`.<br>• Thông báo kết quả gửi tức thời về Mobile App NV.<br>💾 `don_tu`, `nhan_vien`, `phan_ca` |

---

### Phân Hệ 5.0: Tính Toán Lương Tự Động (Gross → Net)

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Mã kỳ lương tháng cần tính (ma_ky_luong - Ví dụ: Tháng 03/2026)                             │
│ • Mức lương cơ bản thỏa thuận pháp lý (luong_co_ban) từ bảng hợp đồng lao động (hop_dong)     │
│ • Dữ liệu bảng chấm công (cham_cong): Số ca hợp lệ, Giờ làm thêm OT, Số ca làm việc ca đêm   │
│ • Đơn xin nghỉ phép năm đã được duyệt (don_tu) để cộng ngày công hưởng lương                  │
│ • Bảng tham số định mức (cau_hinh_khoan_luong): Ăn ca (30k/ngày), Tỷ lệ BH 10.5%, Mức phạt   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH TÍNH TOÁN 2 TẦNG (PROCESS)                                                        │
│ 1. TẦNG 1: TỔNG THU NHẬP (GROSS)                                                              │
│    • Lương ngày công = (Lương CB / 26) * (Số ca quẹt thẻ hợp lệ + Ngày phép năm hưởng lương)  │
│    • Tiền OT = (Lương CB / 208) * Số giờ OT * Hệ số (1.5 ngày thường, 2.0 cuối tuần, 3.0 Lễ) │
│    • Phụ cấp ca đêm = (Lương CB / 208) * Số giờ làm việc ca đêm * 0.30 (Điều 98 BLLĐ)         │
│    • Phụ cấp ăn ca = Số ngày công có mặt * 30.000 VNĐ + Tiền thưởng hiệu quả (KPI)            │
│ 2. TẦNG 2: TỔNG KHẤU TRỪ                                                                      │
│    • Bảo hiểm bắt buộc = Lương CB * 10.5% (BHXH 8%, BHYT 1.5%, BHTN 1%)                       │
│    • Tiền phạt kỷ luật lao động = Tổng tiền phạt đi trễ, về sớm, vắng không phép              │
│    • Thuế TNCN (nếu thu nhập chịu thuế vượt mức giảm trừ gia cảnh)                            │
│ 3. LƯƠNG THỰC NHẬN (NET) = GROSS - TỔNG KHẤU TRỪ                                              │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Bản ghi hoàn chỉnh trong bảng phieu_luong: tong_thu_nhap, tong_khau_tru, luong_thuc_nhan    │
│ • Bản ghi phân rã chi tiết từng dòng thu nhập và khấu trừ trong chi_tiet_phieu_luong          │
│ • Cập nhật trạng thái kỳ lương sang DaTinhLuong; Bảng lương sơ bộ hiển thị trên Web Admin     │
│ • Dữ liệu lưu trữ tại: ky_luong, phieu_luong, chi_tiet_phieu_luong, cau_hinh_khoan_luong     │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **5.1** | **Tính lương ngày công** | HR / Hệ thống | • `ma_ky_luong`<br>• Số ca hợp lệ từ `cham_cong`<br>• `luong_co_ban` từ `hop_dong`<br>• Đơn phép năm `DaDuyet` | 1. Chuẩn công tháng: 26 ngày.<br>2. $\text{Ngày công thực tế} = \text{Ca quẹt} + \text{Phép năm duyệt}$.<br>3. $\text{Lương công} = (\text{Lương CB} / 26) \times \text{Ngày công thực tế}$. | • Số tiền lương ngày công thực tế của NV.<br>• Lưu dòng `LuongCongThucTe` vào chi tiết lương.<br>💾 `ky_luong`, `hop_dong`, `cham_cong`, `chi_tiet_phieu_luong` |
| **5.2** | **Tính tiền làm thêm (OT)** | Hệ thống tự động | • `so_gio_ot` từ `cham_cong`<br>• Phân loại ngày: Thường, Nghỉ tuần, Lễ<br>• Lương cơ bản | 1. Đơn giá 1 giờ chuẩn $= \text{Lương CB} / (26 \times 8)$.<br>2. Áp dụng hệ số: Ngày thường (1.5), Nghỉ tuần (2.0), Lễ Tết (3.0).<br>3. $\text{Tiền OT} = \sum (\text{Đơn giá} \times \text{Giờ OT} \times \text{Hệ số})$. | • Tổng tiền làm thêm giờ trong tháng.<br>• Lưu dòng thu nhập `TienOT` vào bảng chi tiết.<br>💾 `cham_cong`, `hop_dong`, `chi_tiet_phieu_luong` |
| **5.3** | **Tính phụ cấp ca đêm 30%** | Hệ thống tự động | • Ca làm có `la_ca_dem = TRUE`<br>• Số ca đêm thực tế $\times 8$ tiếng<br>• Lương cơ bản | 1. Áp dụng Điều 98 Bộ luật Lao động (hưởng thêm ít nhất 30% lương).<br>2. $\text{Phụ cấp đêm} = (\text{Lương CB} / 208) \times \text{Giờ đêm} \times 0.30$. | • Tiền phụ cấp ca đêm minh bạch đúng luật.<br>• Lưu dòng `PhuCapCaDem` vào bảng chi tiết.<br>💾 `cham_cong`, `ca_lam_viec`, `chi_tiet_phieu_luong` |
| **5.4** | **Tính ăn ca & tiền thưởng** | HR / Hệ thống | • Số ngày công có mặt tại KS<br>• Định mức ăn ca (30.000đ/ngày)<br>• Tiền thưởng KPI, tiệc cưới | 1. $\text{Tiền ăn ca} = \text{Số ngày có mặt} \times 30.000\text{ VNĐ}$.<br>2. Nạp các khoản thưởng hiệu quả/KPI được nhập bổ sung trong kỳ. | • Các dòng phụ cấp `PhuCapAnCa` và `TienThuong` trong `chi_tiet_phieu_luong`.<br>💾 `cau_hinh_khoan_luong`, `chi_tiet_phieu_luong` |
| **5.5** | **Khấu trừ bảo hiểm 10.5%** | Hệ thống tự động | • Lương cơ bản đóng bảo hiểm<br>• Tỷ lệ: BHXH 8%, BHYT 1.5%, BHTN 1% | 1. $\text{Trừ BHXH} = \text{Lương CB} \times 8\%$.<br>2. $\text{Trừ BHYT} = \text{Lương CB} \times 1.5\%$.<br>3. $\text{Trừ BHTN} = \text{Lương CB} \times 1\%$.<br>4. $\text{Tổng trừ BH} = \text{Lương CB} \times 10.5\%$. | • 3 dòng khấu trừ chi tiết tương ứng trong bảng `chi_tiet_phieu_luong`.<br>💾 `hop_dong`, `cau_hinh_khoan_luong`, `chi_tiet_phieu_luong` |
| **5.6** | **Tính phạt & Lương thực nhận (Net)**| HR (Chốt kỳ lương) | • Thống kê số lần trễ/sớm từ `cham_cong`<br>• Mức phạt vi phạm (50k, 100k, 200k)<br>• Tổng Gross (5.1-5.4), Tổng BH (5.5) | 1. Tính tiền phạt vi phạm: $\sum (\text{Lần vi phạm} \times \text{Mức phạt})$.<br>2. $\text{Gross} = \text{Lương công} + \text{OT} + \text{Đêm} + \text{Ăn ca} + \text{Thưởng}$.<br>3. $\text{Khấu trừ} = \text{BH 10.5%} + \text{Phạt} + \text{Thuế TNCN}$.<br>4. $\text{Net} = \text{Gross} - \text{Khấu trừ}$. | • Bản ghi hoàn chỉnh trong bảng `phieu_luong` trạng thái `DaTinhLuong`.<br>• Bảng lương tháng hiển thị trên Web Admin.<br>💾 `phieu_luong`, `chi_tiet_phieu_luong`, `cham_cong` |

---

### Phân Hệ 6.0: Phiếu Lương & Xuất Báo Cáo PDF

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Mã phiếu lương cần truy vấn (ma_phieu_luong), Mã nhân viên (ma_nhan_vien) từ phiên làm việc │
│ • Dữ liệu tổng hợp từ phieu_luong và các dòng phân rã từ chi_tiet_phieu_luong                 │
│ • Template mẫu thiết kế chuẩn QuestPDF mang nhận diện thương hiệu The OverTime Hotel          │
│ • Dữ liệu 12 kỳ lương trong năm phục vụ báo cáo quyết toán thuế TNCN                          │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ (PROCESS)                                                                  │
│ 1. Kiểm tra thẩm quyền truy cập: Nhân viên chỉ được xem phiếu lương của chính mình            │
│ 2. Đọc CSDL: Nạp thông tin nhân sự, phòng ban, chức vụ, mức lương Gross, các khoản trừ, Net   │
│ 3. Khởi tạo QuestPDF Engine: Layout chuẩn A4/A5, bảng 2 cột thu nhập - khấu trừ, số tiền chữ  │
│ 4. Biên dịch nhị phân: Render luồng dữ liệu sang định dạng tệp PDF vector sắc nét             │
│ 5. Quyết toán thuế: Tổng hợp lũy kế 12 tháng lương, tính tổng thu nhập và thuế đã khấu trừ    │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Màn hình giao diện phiếu lương điện tử trực quan trên Web Admin và Mobile App MAUI          │
│ • Biểu đồ đường thể hiện lịch sử biến động thu nhập qua các tháng của nhân viên               │
│ • File PDF phiếu lương chuẩn: PhieuLuong_[MaNV]_[Thang].pdf tải về hoặc gửi lệnh in           │
│ • Văn bản chứng từ quyết toán thuế TNCN cả năm định dạng PDF                                  │
│ • Dữ liệu lưu trữ tại: phieu_luong, chi_tiet_phieu_luong, ky_luong, nhan_vien                │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **6.1** | **Xem chi tiết phiếu lương tháng**| Employee / HR | • `ma_nhan_vien`, `ma_ky_luong` | 1. Phân quyền: Nhân viên chỉ xem lương của mình.<br>2. Lấy số liệu tổng hợp từ `phieu_luong` và các dòng chi tiết từ `chi_tiet_phieu_luong`.<br>3. Render 3 khối: Tổng quan (Net), Thu nhập Gross, Khấu trừ. | • Giao diện chi tiết phiếu lương trên Web & Mobile.<br>• Phân định rõ ràng từng khoản thu - chi.<br>💾 `phieu_luong`, `chi_tiet_phieu_luong`, `ky_luong` |
| **6.2** | **Tra cứu lịch sử thu nhập** | Employee (Mobile) | • `ma_nhan_vien`<br>• Khoảng thời gian: 3 tháng, 6 tháng, 1 năm | 1. Query danh sách `phieu_luong` giảm dần theo tháng/năm.<br>2. Tính mức lương Net trung bình và tháng có thu nhập cao nhất. | • Danh sách thẻ lịch sử thu nhập trên App.<br>• Biểu đồ đường trực quan xu hướng thu nhập.<br>💾 `phieu_luong`, `ky_luong` |
| **6.3** | **Xuất file PDF QuestPDF** | Employee / HR | • `ma_phieu_luong` | 1. Nạp hồ sơ nhân viên, chức vụ, phòng ban và chi tiết lương.<br>2. Áp dụng template QuestPDF chuẩn nhận diện khách sạn.<br>3. Biên dịch tài liệu sang định dạng PDF vector sắc nét. | • File `PhieuLuong_[MaNV]_[Thang].pdf`.<br>• Hỗ trợ tải về máy hoặc in trực tiếp ra giấy.<br>💾 `phieu_luong`, `chi_tiet_phieu_luong`, `nhan_vien` |
| **6.4** | **Quyết toán thuế cả năm** | HR / Employee | • `ma_nhan_vien`, Năm tài chính quyết toán | 1. Tập hợp 12 kỳ lương trong năm tài chính của nhân viên.<br>2. Lũy kế Tổng thu nhập chịu thuế, tổng trừ BHXH và thuế TNCN đã tạm khấu trừ.<br>3. Dùng QuestPDF xuất văn bản chứng từ quyết toán thuế. | • Chứng từ khấu trừ thuế & bảng kê khai thu nhập năm dạng PDF phục vụ cơ quan Thuế.<br>💾 `phieu_luong`, `ky_luong`, `nhan_vien` |

---

### Phân Hệ 7.0: Báo Cáo & Thống Kê Quản Trị

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Khoảng thời gian báo cáo: Theo Tháng, theo Quý, hoặc theo Năm tài chính                     │
│ • Tiêu chí lọc phạm vi: Toàn khách sạn hoặc theo từng Phòng ban cụ thể                        │
│ • Hạn mức ngân sách quỹ lương được Ban Giám đốc phê duyệt từ đầu kỳ                          │
│ • Nguồn dữ liệu tổng hợp từ 17 bảng: Hồ sơ nhân sự, bảng chấm công, phiếu lương tháng          │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ TỔNG HỢP & PHÂN TÍCH (PROCESS)                                             │
│ 1. Biến động nhân lực: Turnover Rate = (Số nhân viên thôi việc / Tổng NS bình quân) * 100%    │
│ 2. Cơ cấu bộ phận: Gom nhóm GROUP BY phòng ban/chức vụ ➔ Tính tỷ trọng phần trăm quân số     │
│ 3. Học vấn & thâm niên: Phân nhóm trình độ (CĐ, ĐH); Thâm niên = Hiện tại - Ngày vào làm      │
│ 4. Đối soát quỹ lương: So sánh Tổng quỹ lương thực chi với Ngân sách; Tính tỷ lệ giải ngân    │
│ 5. Chuyên cần: Tỷ lệ đi làm đúng giờ; Lập danh sách xếp hạng nhân sự vi phạm kỷ luật nhiều    │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Biểu đồ Donut cơ cấu nhân sự & Biểu đồ cột so sánh Ngân sách vs Thực tế                     │
│ • Báo cáo biến động tăng/giảm nhân sự phục vụ kế hoạch tuyển dụng                             │
│ • Cảnh báo màu đỏ khi quỹ lương thực tế vượt quá định mức ngân sách cho phép                  │
│ • Tùy chọn xuất báo cáo thống kê định dạng Excel / PDF trình Ban Giám đốc                     │
│ • Nguồn dữ liệu tổng hợp từ: nhan_vien, hop_dong, phong_ban, cham_cong, phieu_luong           │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **7.1** | **Báo cáo biến động nhân sự** | BoD / HR | • Khoảng thời gian (Quý/Năm)<br>• Bộ lọc phòng ban | 1. Thống kê nhân sự đầu kỳ.<br>2. Đếm số tuyển mới (`ngay_vao_lam` trong kỳ).<br>3. Đếm số thôi việc (`trang_thai = 'NghiViec'`).<br>4. Tính tỷ lệ thôi việc (Turnover rate). | • Biểu đồ đường biến động nhân lực.<br>• Bảng số liệu chi tiết tuyển/nghỉ việc.<br>💾 `nhan_vien`, `hop_dong`, `phong_ban` |
| **7.2** | **Cơ cấu phòng ban, vị trí** | BoD / HR | • Tiêu chí gom nhóm: Phòng ban hoặc Chức danh | 1. Thực thi `GROUP BY` phòng ban và chức vụ.<br>2. Tính tỷ lệ % quân số từng bộ phận trên tổng quy mô khách sạn. | • Biểu đồ Donut cơ cấu nhân sự.<br>• Bảng thống kê định biên nhân sự các bộ phận.<br>💾 `nhan_vien`, `phong_ban`, `chuc_vu` |
| **7.3** | **Phân tích học vấn & thâm niên**| BoD / HR | • Bộ lọc: Bậc học vấn, Nhóm thâm niên | 1. Phân nhóm trình độ (Trung cấp, CĐ, ĐH, Chứng chỉ nghề).<br>2. Tính thâm niên $= \text{CURRENT\_DATE} - ngay\_vao\_lam$.<br>3. Gom nhóm: <1 năm, 1-3 năm, >3 năm. | • Biểu đồ chất lượng nhân sự phục vụ quy hoạch đào tạo và bổ nhiệm nội bộ.<br>💾 `nhan_vien` |
| **7.4** | **Quỹ lương vs Ngân sách** | BoD / HR / Kế toán | • `ma_ky_luong` hoặc Năm tài chính<br>• Hạn mức ngân sách quỹ lương được duyệt | 1. Tính $\sum \text{tong\_thu\_nhap}$ từ `phieu_luong`.<br>2. Tính chênh lệch so với ngân sách kế hoạch.<br>3. Phân rã quỹ lương chi tiết theo từng phòng ban. | • Biểu đồ cột Ngân sách vs Thực tế.<br>• Cảnh báo đỏ nếu quỹ lương vượt định mức.<br>💾 `phieu_luong`, `ky_luong`, `phong_ban` |
| **7.5** | **Tỷ lệ chuyên cần & vi phạm**| Manager / HR | • Tháng báo cáo, phòng ban | 1. Quét dữ liệu `cham_cong`: Đi đúng giờ, số lượt trễ/sớm, bỏ ca.<br>2. Tính tỷ lệ chuyên cần: $(\text{Ca đúng giờ} / \text{Tổng ca}) \times 100\%$.<br>3. Lập danh sách Top vi phạm kỷ luật. | • Bảng xếp hạng chuyên cần của các bộ phận.<br>• Danh sách vi phạm kỷ luật kèm giờ quẹt đối chứng.<br>💾 `cham_cong`, `phan_ca`, `nhan_vien` |

---

### Phân Hệ 8.0: Quản Trị Hệ Thống

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📥 ĐẦU VÀO (INPUT)                                                                            │
│ • Dữ liệu danh mục: Phòng ban, Chức danh công việc, Mẫu ca làm việc và khung giờ              │
│ • Tham số tài chính: Định mức ăn ca (30k), Tỷ lệ bảo hiểm bắt buộc 10.5%, Mức phạt vi phạm    │
│ • Lệnh sao lưu CSDL (Schema / Data / Full) hoặc Tệp tin phục hồi dữ liệu (.sql / .dump)       │
│ • Mật khẩu xác thực quyền Quản trị viên tối cao (Super Admin)                                 │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙️ QUY TRÌNH XỬ LÝ & BẢO MẬT (PROCESS)                                                         │
│ 1. Toàn vẹn dữ liệu: Kiểm tra khóa ngoại (FK) - Chặn xóa phòng ban/chức vụ nếu còn nhân viên  │
│ 2. Cấu hình tham số: Lưu định mức vào cau_hinh_khoan_luong để công thức tính lương tự nạp     │
│ 3. BackupService: Kết nối Supabase Cloud ➔ Trích xuất DDL 17 bảng + Dữ liệu ➔ Đóng gói tệp nén│
│ 4. Restore: Xác thực lại danh tính Admin ➔ Nạp lại kịch bản phục hồi an toàn                  │
│ 5. Ghi nhật ký kiểm toán quản trị an toàn thông tin                                           │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ 📤 ĐẦU RA (OUTPUT) & 💾 CƠ SỞ DỮ LIỆU                                                         │
│ • Danh mục tổ chức được cập nhật đồng bộ trong phong_ban, chuc_vu, ca_lam_viec                │
│ • Thông số định mức mới được áp dụng cho các kỳ tính lương tiếp theo                          │
│ • File sao lưu an toàn CSDL tải về máy hoặc lưu trữ đám mây                                   │
│ • Thông báo trạng thái và bản ghi kiểm toán ghi nhận trong nhật ký hệ thống                   │
│ • Phạm vi CSDL: Toàn bộ 17 bảng Supabase PostgreSQL                                           │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2:

| Mã | Tên Chức Năng | Vai Trò | 📥 Đầu Vào (Input) | ⚙️ Quy Trình Xử Lý (Process) | 📤 Đầu Ra (Output) & 💾 CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **8.1** | **Danh mục phòng ban** | Admin | • `ma_phong_ban`, `ten_phong_ban`, `mo_ta` | 1. Check không trùng tên phòng ban.<br>2. Chặn xóa nếu còn nhân viên đang trực thuộc phòng ban đó. | • Danh mục phòng ban cập nhật trong bảng `phong_ban`.<br>💾 `phong_ban`, `nhan_vien` |
| **8.2** | **Danh mục chức vụ** | Admin | • `ma_chuc_vu`, `ten_chuc_vu`, `mo_ta` | 1. Check không trùng tên chức danh công việc.<br>2. Chặn xóa nếu đang có nhân viên giữ chức vụ đó. | • Danh mục chức vụ chuẩn lưu trong bảng `chuc_vu`.<br>💾 `chuc_vu`, `nhan_vien` |
| **8.3** | **Cấu hình khung giờ ca** | Admin | • `ma_ca`, giờ mở ca, giờ đóng ca, hệ số, cờ đêm | 1. Validate tính hợp lệ khung giờ ca 24/7.<br>2. Cập nhật bảng `ca_lam_viec` để áp dụng cho quy trình xếp ca. | • Cập nhật cấu hình ca trong `ca_lam_viec`.<br>💾 `ca_lam_viec` |
| **8.4** | **Định mức phụ cấp & phạt** | Admin | • `ma_khoan`, tên khoản, loại (`ThuNhap`/`KhauTru`), định mức, tỷ lệ % | 1. Cho phép tùy chỉnh tham số (ăn ca 30k, phạt trễ 50k-100k, BHXH 8%...).<br>2. Lưu vào `cau_hinh_khoan_luong` để công thức lương tự áp dụng. | • Bảng `cau_hinh_khoan_luong` được cập nhật.<br>• Kỳ lương tiếp theo tự động lấy số mới.<br>💾 `cau_hinh_khoan_luong` |
| **8.5** | **Sao lưu & Phục hồi CSDL** | Admin | • Thao tác: Backup Schema/Data hoặc Restore<br>• File dự phòng `.sql.gz`, mật khẩu Admin | 1. Backup: Dump cấu trúc 17 bảng + Data từ Supabase $\rightarrow$ Nén file.<br>2. Restore: Xác thực quyền Admin $\rightarrow$ Nạp script phục hồi.<br>3. Ghi log kiểm toán thao tác hệ thống. | • File backup tải về máy hoặc lưu trữ an toàn.<br>• Thông báo kết quả và nhật ký an toàn.<br>💾 Toàn bộ 17 bảng Supabase |

---

## 4. Ma Trận Phân Quyền Vai Trò Người Dùng (RBAC Matrix)

Hệ thống phân định quyền hạn rõ ràng cho 4 vai trò chuẩn, tương ứng với các mã quyền (`PermissionCodes`) được cấu hình trong bảng `quyen` và bảng trung gian `vai_tro_quyen`:

| Phân Hệ Cấp 1 | Mã Quyền Chuẩn | Quản Trị Viên (Admin) | Phòng Nhân Sự (HR) | Quản Lý Bộ Phận (Manager) | Nhân Viên (Employee) |
|:---|:---:|:---:|:---:|:---:|:---:|
| **1.0 Quản lý Nhân sự & HĐLĐ** | `NV_MANAGE` | Chỉ xem | **Toàn quyền (CRUD)** | Xem bộ phận mình | Chỉ xem hồ sơ cá nhân |
| **2.0 Tài khoản & RBAC** | `USER_RBAC` | **Toàn quyền (CRUD)** | Không | Không | Đổi mật khẩu cá nhân |
| **3.0 Ca làm & Chấm công** | `ATT_MANAGE` | Xem log kiểm toán | Giám sát toàn KS | **Xếp ca & Giám sát bộ phận** | **Quẹt thẻ trên Mobile** |
| **4.0 Quản lý Đơn từ điện tử** | `LEAVE_FLOW` | Không | **Duyệt Thôi việc/Thai sản** | **Duyệt Nghỉ phép/Ốm** | **Tạo & nộp 4 loại đơn** |
| **5.0 Tính toán Lương tự động**| `PAYROLL_CALC`| Không | **Thực thi tính lương** | Không | Không |
| **6.0 Phiếu lương & Xuất PDF** | `PAYSLIP_VIEW` | Không | Tra cứu toàn KS & In | Tra cứu cá nhân | **Tra cứu & Tải PDF cá nhân**|
| **7.0 Báo cáo & Thống kê** | `REPORT_VIEW` | Báo cáo an toàn | **Báo cáo chuyên sâu** | Báo cáo bộ phận mình | Không |
| **8.0 Quản trị Hệ thống** | `SYS_CONFIG` | **Toàn quyền (CRUD)** | Không | Không | Không |

---

## 5. Ma Trận Đối Chiếu CRUD (39 Chức Năng ↔ 17 Bảng CSDL Supabase)

Bảng đối chiếu kiểm chứng mọi chức năng nghiệp vụ của hệ thống đều tương tác trực tiếp với 17 bảng đạt chuẩn 3NF:

```text
┌───────────────────────────┬──────────────────────────────────────────────────────────────────┐
│ Tên Bảng CSDL (Supabase)  │ Các Chức Năng Tương Tác Trực Tiếp                                │
├───────────────────────────┼──────────────────────────────────────────────────────────────────┤
│ phong_ban, chuc_vu        │ 1.1, 1.2, 1.4, 1.5, 7.1, 7.2, 8.1, 8.2                           │
│ nhan_vien                 │ 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 3.2, 4.1-4.5, 5.1-5.6, 6.1-6.4, 7.1│
│ hop_dong                  │ 1.3, 1.4, 4.4, 5.1, 5.2, 5.3, 5.5, 7.1                           │
│ tai_khoan                 │ 2.1 (Login), 2.2 (Khóa/Mở/Reset), 2.3 (Phân vai trò)             │
│ vai_tro, quyen, vt_quyen  │ 2.1, 2.3, 2.4 (Ma trận RBAC)                                     │
│ ca_lam_viec               │ 3.1, 3.2, 3.3, 3.4, 5.3, 8.3                                     │
│ phan_ca                   │ 3.2 (Xếp ca tuần), 3.3 (Kiểm tra ca quẹt thẻ), 7.5               │
│ cham_cong                 │ 3.3 (Check-in/out), 3.4 (Trễ/sớm), 3.5 (OT), 5.1, 5.2, 5.3, 7.5  │
│ loai_don, don_tu          │ 4.1, 4.2, 4.3, 4.4 (Nộp đơn), 4.5 (Xét duyệt), 5.1 (Công phép)   │
│ ky_luong                  │ 5.1, 5.6 (Khóa sổ), 6.1, 6.2, 6.4, 7.4                           │
│ cau_hinh_khoan_luong      │ 5.2, 5.4, 5.5, 5.6, 8.4 (Cấu hình định mức)                     │
│ phieu_luong               │ 5.6 (Tổng hợp lương), 6.1, 6.2, 6.3, 6.4, 7.4                    │
│ chi_tiet_phieu_luong      │ 5.1, 5.2, 5.3, 5.4, 5.5, 5.6 (Dòng thu chi), 6.1, 6.3, 6.4       │
└───────────────────────────┴──────────────────────────────────────────────────────────────────┘
```
