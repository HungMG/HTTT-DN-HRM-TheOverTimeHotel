# 🧪 BỘ TEST CASE KIỂM THỬ HỆ THỐNG HRM THE OVERTIME HOTEL

> **Dự án:** Hệ thống Quản lý Nhân sự & Tiền lương Khách sạn The OverTime Hotel  
> **Môn học:** Hệ Thống Thông Tin Doanh Nghiệp (HTTTDN) · Nhóm 4 (DCT123C4)  
> **Nền tảng kiểm thử:** Web Admin Portal (ASP.NET Core MVC .NET 10) & Mobile App (.NET MAUI .NET 10)  
> **Cơ sở dữ liệu:** Supabase Cloud Database (PostgreSQL · 17 bảng chuẩn 3NF)  
> **Phiên bản tài liệu:** v1.0 (Cập nhật sau khi hoàn thành Task 15 & Task 16, chuẩn bị chuyển giao Mobile App)

---

## 📌 1. THÔNG TIN MÔI TRƯỜNG & TÀI KHOẢN KIỂM THỬ

### 1.1. Môi trường triển khai
- **Web Admin Portal:** Đang khởi chạy tại `http://localhost:5065`
- **Supabase Cloud Database:** Region Singapore (`ap-southeast-1`), kết nối qua Pooler IPv4 port `6543`.
- **Mobile Application:** .NET MAUI (`src/OvertimeHotel.HRM.MobileApp`) chạy trên Windows Desktop / Android Emulator / Thiết bị Android thật.

### 1.2. Danh sách 4 tài khoản kiểm thử đại diện (RBAC Demo Accounts)

| Tên Đăng Nhập | Mật Khẩu Mẫu | Vai Trò (Role) | Nhân Viên Đại Diện | Phạm Vi Thẩm Quyền |
|:---|:---|:---|:---|:---|
| **`admin`** | `Admin@123` | **Admin** (Quản trị viên) | Nguyễn Đình Cường (Tổng Giám Đốc) | Toàn quyền cấu hình hệ thống, quản lý tài khoản, danh mục, cấu hình lương, backup CSDL. |
| **`hr_sang`** | `Hr@123` | **HR** (Nhân sự) | Võ Huỳnh Minh Sang (Trưởng phòng HR) | Quản lý hồ sơ nhân viên, lập HĐLĐ, tính lương 2 tầng, xuất PDF, duyệt đơn thôi việc/thai sản. |
| **`mgr_long`** | `Manager@123` | **Manager** (Quản lý bộ phận)| Nguyễn Hoàng Long (Trưởng ca Tiền sảnh FO) | Lập lịch phân ca 24/7 bộ phận FO, duyệt đơn nghỉ phép/ốm, duyệt OT, xem chấm công bộ phận. |
| **`emp_bao`** | `Emp@123` | **Employee** (Nhân viên) | Châu Quốc Bảo (Nhân viên Lễ tân FO) | Quẹt thẻ Check-in/Check-out GPS/WiFi, nộp đơn điện tử, theo dõi ca trực, xem phiếu lương. |

---

## 📑 2. MA TRẬN PHÂN QUYỀN VÀ TRÁCH NHIỆM KIỂM THỬ

```text
[Quản trị viên (Admin)] ──► Web Admin: Tài khoản, Phân quyền RBAC, Danh mục, Cấu hình lương, Backup
[Phòng Nhân sự (HR)]   ──► Web Admin: Hồ sơ Nhân viên, HĐLĐ, Quản lý ca trực, Tính lương, In PDF
[Quản lý Bộ phận (Mgr)] ──► Web/Mobile: Lập lịch phân ca 24/7, Phê duyệt đơn từ, Xác nhận giờ tăng ca OT
[Nhân viên Khách sạn]   ──► Mobile App: Quẹt thẻ Check-in/out, Nộp 4 loại đơn, Xem lịch tuần & Phiếu lương
```

---

## 🌐 PHẦN A: BỘ TEST CASE CHO PHÂN HỆ WEB ADMIN (TSK-11 ➔ TSK-16)

### Nhóm 1: Xác Thực & Điều Hướng (Authentication & Authorization)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-AUTH-01** | Đăng nhập Admin thành công | Mở `http://localhost:5065` | 1. Nhập Username, Password<br>2. Bấm "Đăng nhập" | `admin` / `Admin@123` | Đăng nhập thành công, chuyển hướng đến Dashboard Admin, hiển thị đúng tên người dùng và vai trò Admin. | 🟢 Pass |
| **TC-WEB-AUTH-02** | Đăng nhập thất bại khi sai mật khẩu | Mở trang login | 1. Nhập Username đúng<br>2. Nhập Password sai<br>3. Bấm "Đăng nhập" | `admin` / `SaiPass999` | Thông báo lỗi đỏ: "Tên đăng nhập hoặc mật khẩu không chính xác", không cho phép truy cập. | 🟢 Pass |
| **TC-WEB-AUTH-03** | Chặn truy cập trái phép khi chưa đăng nhập | Đang ở chế độ ẩn danh | 1. Truy cập trực tiếp link `/UserAccounts`<br>2. Truy cập `/CauHinhKhoanLuong` | URL trực tiếp | Hệ thống chặn lại (HTTP 302) và tự động chuyển hướng về trang `/Account/Login?returnUrl=...`. | 🟢 Pass |
| **TC-WEB-AUTH-04** | Đăng xuất an toàn | Đang trong phiên đăng nhập | 1. Bấm nút "Đăng xuất" trên thanh header/sidebar | Thao tác click | Xóa sạch Authentication Cookie, chuyển hướng về trang Đăng nhập, bấm Back trình duyệt không vào lại được. | 🟢 Pass |

---

### Nhóm 2: Quản Trị Tài Khoản & Phân Quyền RBAC (TSK-13)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-ACC-01** | Xem danh sách tài khoản | Đăng nhập quyền Admin | 1. Vào menu "Quản trị tài khoản"<br>2. Xem danh sách | Không | Hiển thị bảng tài khoản kèm họ tên nhân viên, vai trò, trạng thái Hoạt động/Bị khóa, ngày tạo. | 🟢 Pass |
| **TC-WEB-ACC-02** | Khóa / Mở khóa tài khoản | Có tài khoản đang hoạt động | 1. Chọn tài khoản `emp_bao`<br>2. Bấm nút "Khóa tài khoản"<br>3. Nhập lý do khóa | Lý do: "Tạm đình chỉ công tác để kiểm tra" | Trạng thái chuyển sang "Bị khóa", tài khoản `emp_bao` không thể đăng nhập vào Web hay Mobile App nữa. | 🟢 Pass |
| **TC-WEB-ACC-03** | Đặt lại mật khẩu tài khoản | Tài khoản người dùng | 1. Chọn tài khoản cần reset<br>2. Nhập mật khẩu mới<br>3. Bấm xác nhận | Mật khẩu mới: `NewPass@2026` | Mật khẩu được băm SHA-256 lưu xuống Supabase, đăng nhập bằng mật khẩu mới thành công. | 🟢 Pass |
| **TC-WEB-ACC-04** | Điều chỉnh Ma trận phân quyền RolePermissions | Đăng nhập quyền Admin | 1. Vào màn hình "Phân quyền vai trò"<br>2. Bật/tắt checkbox quyền cho vai trò Manager | Tắt quyền `SAO_LUU_CSDL` của Manager | CSDL cập nhật bảng `vai_tro_quyen`, Manager không thể nhìn thấy hoặc bấm nút backup nữa. | 🟢 Pass |
| **TC-WEB-ACC-05** | Kiểm toán Nhật ký quản trị (`nhat_ky_quan_tri`) | Vừa thực hiện thao tác tài khoản | 1. Mở bảng `nhat_ky_quan_tri` hoặc xem tab Audit Log | Thao tác vừa làm | Ghi nhận chi tiết: Người thực hiện, Đối tượng bị tác động, Hành động (KHOA_TAI_KHOAN / CAP_QUYEN), IP, Thời gian. | 🟢 Pass |

---

### Nhóm 3: Quản Trị Danh Mục Hệ Thống & Cấu Hình Lương (TSK-14)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-CAT-01** | Thêm mới Phòng ban | Quyền Admin/HR | 1. Vào "Danh mục phòng ban"<br>2. Bấm "Thêm mới"<br>3. Nhập tên và mô tả | Tên: `Bộ phận Trị liệu Spa`<br>Mô tả: `Chăm sóc sức khỏe, xông hơi` | Bản ghi mới xuất hiện trong bảng `phong_ban`, hiển thị đúng trong danh sách phòng ban khi tạo nhân viên. | 🟢 Pass |
| **TC-WEB-CAT-02** | Chặn xóa phòng ban đang có nhân viên | Phòng ban có nhân viên | 1. Chọn phòng ban Tiền sảnh (FO)<br>2. Bấm "Xóa" | Thao tác click | Hệ thống cảnh báo lỗi toàn vẹn khóa ngoại (FK): Không được phép xóa phòng ban đang có nhân viên trực thuộc. | 🟢 Pass |
| **TC-WEB-CAT-03** | Quản lý danh mục 3 Ca 24/7 | Đăng nhập Admin | 1. Vào danh mục ca làm việc<br>2. Kiểm tra cờ ca đêm | Ca 3: 22h - 6h sáng | Cờ `qua_dem = TRUE`, ca đêm được gắn nhãn hưởng phụ cấp pháp lý thêm 30% lương. | 🟢 Pass |
| **TC-WEB-CAT-04** | CRUD ba danh mục mới | Admin/HR, Supabase có 24 bảng | Thêm/sửa/lọc/xóa bản ghi thử ở Tăng ca, Phụ cấp, Khấu trừ | OT 1.50; phụ cấp 35000.50 theo tháng, chịu thuế; bảo hiểm 1.50% | Ghi đúng ba bảng mới; giữ số thập phân, cách tính và checkbox thuế. | 🟢 Pass (HTTP 30/09/2026) |
| **TC-WEB-CAT-05** | Thưởng và tương thích cũ | Có bảng cau_hinh_khoan_luong | CRUD tab Thưởng; mở liên kết cũ | Thưởng 200000.50 | Chỉ quản lý dòng THUONG; liên kết cũ chuyển hướng; giữ các dòng phụ cấp/khấu trừ cũ. | 🟢 Pass (HTTP 30/09/2026) |
| **TC-WEB-CAT-06** | Validation và antiforgery | Admin/HR | Nhập âm, tên trùng, loại sai, quá hai số thập phân, thiếu định mức; gửi POST thiếu token | -1, OT 0, BH 101, tên trùng khác hoa/thường | Dữ liệu sai không được ghi; thiếu token bị từ chối. | 🟢 Pass (HTTP 30/09/2026) |
| **TC-WEB-CAT-07** | Chặn xóa cấu hình đã dùng | Phiếu lương tham chiếu cấu hình | Yêu cầu xóa | ID đang dùng | Chặn xóa, giữ nguyên cấu hình và phiếu lương. | 🟢 Pass (HTTP 30/09/2026) |
| **TC-WEB-CAT-08** | Dashboard cấu hình lương | Supabase hoạt động | Mở Home trước/sau CRUD | Bốn danh mục | Tổng = số loại OT + phụ cấp + khấu trừ mới + thưởng cũ; làm mới cache sau ghi. | 🟢 Pass (HTTP 30/09/2026) |
| **TC-WEB-CAT-09** | Phân quyền | Chưa đăng nhập | Mở /CauHinhLuong | Truy cập trực tiếp | Chuyển đến đăng nhập; CRUD chỉ dành cho Admin/HR. | 🟢 Pass (HTTP 30/09/2026) |

---

### Nhóm 4: Quản Lý Hồ Sơ Nhân Sự & Hợp Đồng Lao Động (TSK-15)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-EMP-01** | Thêm mới hồ sơ nhân sự hợp lệ | Quyền HR | 1. Bấm "Thêm nhân viên"<br>2. Điền đầy đủ Họ, Tên, Ngày sinh, CCCD, SĐT, Email, Phòng ban, Chức vụ | Họ: `Trần`, Tên: `Văn An`<br>Sinh: `1998-05-12` (>= 18t)<br>SĐT: `0912345678`, Email: `an.tran@overtime.com` | Tạo bản ghi thành công trong `nhan_vien`, trạng thái mặc định `DANG_LAM`, tự sinh `ma_nhan_vien`. | 🟢 Pass |
| **TC-WEB-EMP-02** | Validate tuổi lao động < 18 tuổi | Quyền HR | 1. Nhập ngày sinh năm 2012 (chưa đủ 18 tuổi)<br>2. Bấm Lưu | Ngày sinh: `2012-01-01` | Form báo lỗi: Nhân viên phải đủ từ 18 tuổi trở lên theo Bộ luật Lao động, chặn lưu vào CSDL. | 🟢 Pass |
| **TC-WEB-EMP-03** | Chặn trùng Email / Số điện thoại | Quyền HR | 1. Nhập Email hoặc SĐT đã tồn tại của nhân viên khác | SĐT trùng nhân viên cũ | Báo lỗi trùng lặp dữ liệu duy nhất (Unique Constraint), bảo toàn tính toàn vẹn hồ sơ. | 🟢 Pass |
| **TC-WEB-EMP-04** | Tìm kiếm & Phân trang nhân sự | Có >= 20 nhân viên mẫu | 1. Nhập từ khóa tên nhân viên<br>2. Chọn bộ lọc Phòng ban, Trạng thái | Từ khóa: `Bảo`<br>Phòng: `Tiền sảnh` | Danh sách lọc tức thời đúng kết quả, thanh phân trang hiển thị chính xác tổng số bản ghi. | 🟢 Pass |
| **TC-WEB-CON-01** | Lập Hợp đồng lao động mới | Nhân viên chưa có HĐ | 1. Vào chi tiết nhân viên<br>2. Bấm "Ký hợp đồng"<br>3. Điền Số HĐ, Loại HĐ, Lương cơ bản, Ngày hiệu lực | Số HĐ: `HD-2026-FO09`<br>Lương CB: `8.500.000 VNĐ`<br>Từ `2026-10-01` đến `2027-10-01` | Tạo bản ghi trong `hop_dong` trạng thái `HIEU_LUC`, lương cơ bản > 0 sẵn sàng làm căn cứ tính lương. | 🟢 Pass |
| **TC-WEB-CON-02** | Cảnh báo HĐLĐ sắp hết hạn $\le 30$ ngày | Có HĐ còn hạn < 30 ngày | 1. Mở trang Quản lý Hợp đồng / Dashboard HR | HĐ có `ngay_ket_thuc` trong vòng 30 ngày tới | Hiển thị huy hiệu cảnh báo (Badge màu Vàng/Đỏ), liệt kê danh sách nhân sự cần tái ký hoặc thanh lý HĐ. | 🟢 Pass |

---

### Nhóm 5: Lập Lịch Phân Ca Làm Việc 24/7 (TSK-16)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-SCH-01** | Xem Ma trận Lịch Tuần 24/7 | Quyền Manager/HR | 1. Vào chức năng "Phân ca làm việc"<br>2. Chọn tuần hiện tại<br>3. Bấm nút chuyển tuần trước/sau | Tuần W40 / 2026 | Ma trận 7 ngày x danh sách nhân viên hiển thị trực quan các ca Sáng (vàng), Chiều (xanh), Đêm (tím). | 🟢 Pass |
| **TC-WEB-SCH-02** | Xếp ca trực Sáng / Chiều / Đêm | Manager bộ phận | 1. Bấm vào ô ngày của nhân viên<br>2. Chọn Ca làm việc (Ví dụ: Ca Đêm)<br>3. Bấm Lưu phân ca | Nhân viên: `Châu Quốc Bảo`<br>Ngày: `Thứ Sáu`<br>Ca: `Ca Đêm (22h - 06h)` | Lưu bản ghi vào bảng `phan_ca`, ô lịch cập nhật màu sắc ca đêm, tự động tính ca đêm hưởng phụ cấp 30%. | 🟢 Pass |
| **TC-WEB-SCH-03** | Chặn xếp 2 ca chồng chéo trong 1 ngày | Đã có ca phân công | 1. Chọn nhân viên đã có Ca Sáng<br>2. Tiếp tục xếp thêm Ca Chiều trong cùng ngày | Cùng ngày `2026-09-18` | Hệ thống báo lỗi vi phạm ràng buộc: Một nhân sự không được xếp 2 ca trong cùng 1 ngày làm việc. | 🟢 Pass |
| **TC-WEB-SCH-04** | Đồng bộ phân ca theo ràng buộc DB | Xếp ca mới | 1. Chọn trạng thái phân ca ban đầu | Trạng thái: `DA_PHAN` / `CHO_LAM` | Đồng bộ chính xác với Check Constraint của PostgreSQL trên bảng `phan_ca`. | 🟢 Pass |

---

### Nhóm 6: Sao Lưu & Phục Hồi Dữ Liệu Supabase (TSK-10)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Trạng Thái |
|:---|:---|:---|:---|:---|:---|:---:|
| **TC-WEB-BAK-01** | Xuất Snapshot Sao lưu CSDL | Quyền Admin | 1. Vào menu "Sao lưu & Phục hồi"<br>2. Bấm "Tạo bản sao lưu ngay" | Thao tác click | Tải về file `.json` chứa toàn bộ dữ liệu 17 bảng của Supabase kèm timestamp định danh chuẩn. | 🟢 Pass |
| **TC-WEB-BAK-02** | Thống kê số lượng bản ghi 17 bảng | Đăng nhập Admin | 1. Mở trang Dashboard Backup | Tự động truy vấn | Hiển thị bảng số liệu trực quan: Số lượng bản ghi hiện hữu của cả 17 bảng trong CSDL Supabase. | 🟢 Pass |

---

## 📱 PHẦN B: BỘ TEST CASE CHUẨN BỊ CHO MOBILE APP (.NET MAUI) (TSK-17 ➔ TSK-20)

### Nhóm 1: Trải Nghiệm Mở Ứng Dụng & Đăng Nhập Modern Luxury

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi (Chuẩn Luxury UI) |
|:---|:---|:---|:---|:---|:---|
| **TC-MOB-AUTH-01** | Màn hình Intro Splash Screen | Mở app lần đầu | 1. Bật app trên điện thoại<br>2. Quan sát hiệu ứng | Khởi động app | Hiển thị Logo The OverTime Hotel vàng kim, thanh Progress Bar chạy êm từ 0% ➔ 100% đồng bộ Supabase. |
| **TC-MOB-AUTH-02** | Skeleton Shimmer Loading (Cấm Spinner) | Chuyển trang/tải data | 1. Đăng nhập hoặc nạp ca trực<br>2. Quan sát màn hình chờ | Nạp dữ liệu | **Tuyệt đối không có vòng xoay `ActivityIndicator`**; hiển thị các khối bo góc Shimmer gradient lướt sáng mượt mà. |
| **TC-MOB-AUTH-03** | Chuyển đổi vai trò Nhân Viên 🔁 Quản Lý | Trang Login | 1. Bấm tab Quản Lý / Nhân Viên<br>2. Quan sát hiệu ứng chữ & nút | Thao tác tap | Hiệu ứng Spring Bounce nảy nhẹ, chữ gợi ý trượt mờ Slide-Fade Morphing mượt mà, không giật cục 0ms. |
| **TC-MOB-AUTH-04** | Custom Luxury Modal Popup (Cấm Alert thô) | Nhập sai tài khoản | 1. Nhập sai mật khẩu<br>2. Bấm Đăng nhập | Sai password | **Tuyệt đối không dùng `DisplayAlert` thô của OS**; hiển thị Card trắng bo góc 24px, viền vàng kim, nền mờ tối sang trọng. |

---

### Nhóm 2: Chấm Công Di Động Check-in / Check-out & Kiểm Toán Vị Trí (TSK-17)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi |
|:---|:---|:---|:---|:---|:---|
| **TC-MOB-ATT-01** | Check-in thành công khi ở tại Khách sạn (GPS hợp lệ) | Đăng nhập tài khoản `emp_bao`, có ca phân công hôm nay | 1. Đứng tại khuôn viên khách sạn (khoảng cách $\le 100$m)<br>2. Bấm nút "QUẸT THẺ CHECK-IN" | Tọa độ GPS thực tế của khách sạn | Ghi nhận bản ghi `cham_cong` (`gio_check_in = NOW()`), lưu bằng chứng GPS/Thiết bị; Popup vàng kim: *"Quẹt thẻ thành công lúc HH:mm"*. |
| **TC-MOB-ATT-02** | Check-in thành công qua WiFi Khách sạn | Tắt GPS, kết nối WiFi nội bộ | 1. Kết nối WiFi SSID `OverTimeHotel_Staff`<br>2. Bấm "QUẸT THẺ CHECK-IN" | Tên WiFi `OverTimeHotel_Staff` | Nhận diện thành công mạng nội bộ khách sạn, cho phép Check-in hợp lệ mà không bị chặn. |
| **TC-MOB-ATT-03** | Chặn Check-in ngoài phạm vi khách sạn (Chống gian lận) | Nhân viên ở nhà / ngoài bán kính 100m | 1. Không kết nối WiFi khách sạn, GPS ở xa<br>2. Bấm "QUẸT THẺ CHECK-IN" | Khoảng cách GPS > 100m | Popup cảnh báo: *"Bạn đang ở ngoài phạm vi khách sạn (Cách xxx mét). Vui lòng có mặt tại nơi làm việc để chấm công!"*, chặn lưu CSDL. |
| **TC-MOB-ATT-04** | Tự động tính số phút Đi trễ | Nhân viên đến sau giờ ca | 1. Ca bắt đầu lúc 06:00<br>2. Quẹt Check-in lúc 06:18 | Giờ quẹt: `06:18` | Cột `phut_di_tre` tự động ghi nhận `= 18`, trạng thái ca chuyển sang `DI_TRE`, hiển thị badge vàng cam trên App. |
| **TC-MOB-ATT-05** | Tự động tính số phút Về sớm khi Check-out | Đã check-in trước đó | 1. Ca kết thúc lúc 14:00<br>2. Bấm Check-out lúc 13:45 | Giờ quẹt: `13:45` | Cập nhật `gio_check_out = 13:45`, tự động tính `phut_ve_som = 15`, trạng thái ca ghi nhận `VE_SOM`. |
| **TC-MOB-ATT-06** | Tự động tính Giờ Tăng Ca (Overtime - OT) | Nhân viên ở lại làm thêm | 1. Ca kết thúc 14:00<br>2. Làm thêm đến 16:30 mới Check-out | Giờ quẹt: `16:30` | Vượt ngưỡng $\ge 30$ phút, hệ thống tự động ghi nhận `gio_tang_ca_ot = 2.5` giờ, sẵn sàng cho công thức tính lương x1.5. |

---

### Nhóm 3: Cổng Nộp Đơn Từ Điện Tử Của Nhân Viên (TSK-18)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi |
|:---|:---|:---|:---|:---|:---|
| **TC-MOB-REQ-01** | Nộp Đơn xin nghỉ phép năm | Nhân viên còn ngày phép | 1. Chọn loại đơn "Nghỉ phép năm"<br>2. Chọn từ ngày đến ngày trong tương lai<br>3. Nhập lý do | Loại: Phép năm (1)<br>Từ `2026-10-15` đến `2026-10-16`<br>Lý do: `Giải quyết việc gia đình` | Tạo bản ghi mới trong bảng `don_tu` trạng thái `CHO_DUYET`, hiển thị trong danh sách đơn cá nhân kèm Badge vàng. |
| **TC-MOB-REQ-02** | Chặn nộp đơn phép khi đã hết quỹ 12 ngày | Quỹ phép năm = 0 | 1. Nhân viên đã nghỉ hết 12 ngày phép<br>2. Chọn nộp tiếp đơn phép năm | Đơn xin 2 ngày | Báo lỗi: Quỹ phép năm của bạn đã hết (12/12 ngày). Gợi ý chuyển sang nộp Đơn nghỉ việc riêng không hưởng lương. |
| **TC-MOB-REQ-03** | Nộp Đơn nghỉ ốm đau hưởng BHXH | Đơn có chứng từ y tế | 1. Chọn loại "Nghỉ ốm hưởng BHXH"<br>2. Chọn thời gian<br>3. Đính kèm ảnh giấy chứng nhận y tế | Ảnh chụp giấy nghỉ việc hưởng BHXH của bệnh viện | Bản ghi được tạo, đính kèm đường dẫn ảnh chứng từ, thông báo chuyển tiếp hồ sơ đến Phòng HR tiếp nhận chế độ. |
| **TC-MOB-REQ-04** | Nộp Đơn thôi việc & Check thời hạn báo trước | HĐLĐ có thời hạn (1 năm) | 1. Chọn loại "Đơn xin thôi việc"<br>2. Chọn ngày chấm dứt HĐ | Ngày nộp: Hôm nay<br>Ngày nghỉ: Sau 35 ngày | Hệ thống kiểm tra Điều 35 BLLĐ: Thời hạn báo trước $\ge 30$ ngày hợp lệ, tạo đơn và gửi thông báo ưu tiên đến HR & Manager. |
| **TC-MOB-REQ-05** | Cảnh báo vi phạm thời hạn báo trước thôi việc | HĐ xác định thời hạn | 1. Chọn ngày nghỉ chỉ sau 10 ngày (vi phạm luật) | Ngày nghỉ: Sau 10 ngày | Cảnh báo: HĐLĐ xác định thời hạn yêu cầu báo trước ít nhất 30 ngày. Vui lòng thỏa thuận trực tiếp với Trưởng phòng HR. |

---

### Nhóm 4: Màn Hình Phê Duyệt Đơn Từ Dành Cho Quản Lý & HR (TSK-19 & TSK-20)

| Mã TC | Tên Test Case | Tiền Điều Kiện | Các Bước Thực Hiện | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi |
|:---|:---|:---|:---|:---|:---|
| **TC-MOB-APP-01** | Quản lý xem danh sách đơn chờ duyệt | Đăng nhập tài khoản `mgr_long` | 1. Vào tab "Xét duyệt đơn"<br>2. Xem danh sách | Lọc theo phòng ban Tiền sảnh | Chỉ hiển thị các đơn từ của nhân viên thuộc bộ phận Tiền sảnh, kèm huy hiệu số lượng đơn đang chờ duyệt. |
| **TC-MOB-APP-02** | Phê duyệt đơn nghỉ phép (`DA_DUYET`) | Đơn đang ở `CHO_DUYET` | 1. Chọn đơn nghỉ của `emp_bao`<br>2. Nhập ghi chú duyệt<br>3. Bấm "Phê duyệt" | Ghi chú: `Đã bố trí nhân sự trực thay` | Trạng thái chuyển `DA_DUYET`, cập nhật `ngay_duyet` và `ma_nguoi_duyet`, đẩy thông báo kết quả tức thì về Mobile App nhân viên. |
| **TC-MOB-APP-03** | Từ chối đơn từ kèm lý do bắt buộc | Đơn đang ở `CHO_DUYET` | 1. Chọn đơn<br>2. Để trống lý do rồi bấm "Từ chối"<br>3. Nhập lý do và bấm lại | Lý do để trống ➔ Sau đó nhập: `Ngày này khách sạn kín phòng đón đoàn VIP` | Chặn từ chối nếu không có lý do; khi nhập lý do thành công, đơn chuyển sang `TU_CHOI` và gửi thông báo giải thích cho nhân viên. |
| **TC-MOB-INT-01** | Tự động liên kết Đơn phép vào Chấm công (TSK-20)| Đơn phép năm được duyệt | 1. Quản lý duyệt đơn phép năm 2 ngày<br>2. Kiểm tra dữ liệu ngày công kỳ lương | Đơn phép năm `DA_DUYET` | Hệ thống tự động ghi nhận 2 ngày phép hưởng nguyên lương vào cột `ngay_cong_thuc_te` khi tính lương tháng. |

---

## 🚀 PHẦN C: KỊCH BẢN KIỂM THỬ ĐẦU CUỐI (END-TO-END WORKFLOW)

Kịch bản chạy xuyên suốt 8 bước chuẩn hóa theo PRD 01 & 05:

1. **Bước 1 (HR trên Web):** Đăng nhập `hr_sang`, tạo hồ sơ nhân viên mới `Trần Văn An` và ký HĐLĐ 1 năm mức lương cơ bản `9.000.000 VNĐ`.
2. **Bước 2 (Admin trên Web):** Đăng nhập `admin`, cấp tài khoản `an_tran` với vai trò `Employee`.
3. **Bước 3 (Manager trên Web):** Đăng nhập `mgr_long`, vào Ma trận Phân ca 24/7 xếp cho `an_tran` lịch ca sáng và ca đêm trong tuần.
4. **Bước 4 (Nhân viên trên Mobile):** Đăng nhập `an_tran`, quẹt thẻ Check-in đúng giờ ca sáng tại khách sạn, sau đó quẹt Check-out trễ 1 giờ (ghi nhận 1.0h OT).
5. **Bước 5 (Nhân viên trên Mobile):** Nộp đơn xin nghỉ phép năm 1 ngày.
6. **Bước 6 (Manager trên Mobile):** Đăng nhập `mgr_long`, mở danh sách đơn chờ duyệt và bấm "Phê duyệt".
7. **Bước 7 (HR trên Web):** Mở kỳ tính lương tháng, hệ thống tự động tính lương Gross (công + OT + ca đêm 30% + phụ cấp ăn ca) trừ BHXH 10.5% ra lương Net.
8. **Bước 8 (Nhân viên & HR):** Xuất file phiếu lương QuestPDF và kiểm tra số tiền hiển thị trên điện thoại.

---

## 📋 3. HƯỚNG DẪN TIẾN HÀNH KIỂM THỬ VÀ ĐÁNH GIÁ KẾT QUẢ

1. Mở song song trình duyệt Web (`http://localhost:5065`) và ứng dụng Mobile App / Máy ảo.
2. Dùng đúng danh sách 4 tài khoản ở **Mục 1.2** để thực hiện từng Test Case theo thứ tự từ `TC-WEB-AUTH-01` đến `TC-WEB-BAK-02`.
3. Khi phát hiện lỗi hoặc sai lệch so với "Kết quả mong đợi", ghi lại Mã TC và mô tả hành vi thực tế để xử lý ngay.

### Xác nhận task 14 — 30/09/2026

Web Admin build: 0 lỗi, 0 cảnh báo. `tests/payroll-smoke.mjs`: 39 kiểm tra HTTP/CRUD qua tài khoản Admin mẫu trên Supabase; gồm số thập phân, checkbox thuế, bộ lọc, validation, antiforgery, chặn xóa FK và tổng dashboard/cache. Các cấu hình SMOKE đã được xóa sau kiểm tra. Kiểm tra HTTP không thay thế kiểm tra trực quan trên desktop/mobile.

Kiểm tra UI task 14 bằng Playwright: 21 kiểm tra đạt trên desktop 1440px và mobile 390px; bốn tab dùng navy, nút sửa/xóa đồng nhất, live search/lọc chỉ cập nhật kết quả, giữ loại khi hủy truy vấn cũ, trạng thái trống/reset và hộp thoại xóa. Phòng ban/Chức vụ vẫn live search; không có lỗi JavaScript và không thay đổi bản ghi nghiệp vụ.
