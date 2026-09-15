# PRD 03: Sơ Đồ Ngữ Cảnh & Sơ Đồ Luồng Dữ Liệu Mức 1 (DFD Level 1)

## 1. Sơ Đồ Ngữ Cảnh (Context Diagram / DFD Mức 0)

Sơ đồ ngữ cảnh định nghĩa ranh giới tương tác và các luồng thông tin 2 chiều giữa Hệ thống HRM Khách sạn The OverTime Hotel (Tiến trình trung tâm `0.0`) với 5 tác nhân chính:

```
                                 ┌────────────────────────────────────┐
                                 │           BAN GIÁM ĐỐC             │
                                 └────────────────┬──▲────────────────┘
                             Duyệt quỹ lương & NS │  │ Báo cáo nhân sự & quỹ lương
                                                  ▼  │
┌───────────────────────┐                      ┌─────┴────────────────┐                      ┌────────────────────────┐
│ QUẢN TRỊ VIÊN (ADMIN) │                      │ TIẾN TRÌNH TRUNG TÂM │                      │    QUẢN LÝ BỘ PHẬN     │
│   (Web Admin Server)  ├─────────────────────►│         0.0          │◄─────────────────────┤  (FO, HK, F&B, Maint)  │
│                       │ Account, RBAC, Backup│  HỆ THỐNG QUẢN LÝ    │ Xếp ca 24/7, Duyệt đơn│                        │
│                       │◄─────────────────────┤  NHÂN SỰ & TIỀN LƯƠNG├─────────────────────►│                        │
└───────────────────────┘ Log kiểm toán, Report│  THE OVERTIME HOTEL  │ Thông báo, Báo cáo ca└────────────────────────┘
                                               └─────┬────────────────┘
                                                     │  ▲
              Hồ sơ NV, HĐLĐ, Tham số lương tháng    │  │ Check-in/out quẹt thẻ, 4 Loại đơn
           Cảnh báo HĐLĐ hết hạn, Bảng lương sơ bộ   ▼  │ Lịch ca cá nhân, Kết quả đơn, Phiếu lương PDF
         ┌───────────────────────────────────────────┴──┴────────────────────────────────────┐
         │                                                                                   │
┌────────┴──────────────┐                                                           ┌────────┴───────────────┐
│ PHÒNG HÀNH CHÍNH - NS │                                                           │  NHÂN VIÊN KHÁCH SẠN   │
│    (HR Department)    │                                                           │   (Mobile App MAUI)    │
└───────────────────────┘                                                           └────────────────────────┘
```

### Bảng Từ Điển Luồng Dữ Liệu Mức Ngữ Cảnh:

| Mã Luồng | Tác Nhân | Hướng | Tên Luồng Dữ Liệu | Thuộc Tính Dữ Liệu Chính | Nghiệp Vụ Liên Quan |
|:---:|:---|:---:|:---|:---|:---|
| **F01_IN** | Quản trị viên (Admin) | Vào HT | Thông tin cấu hình & tài khoản | Username, PasswordHash, RoleId, Quyền RBAC, Danh mục phòng ban, chức vụ, ca làm, tham số phụ cấp/phạt, lệnh Backup/Restore | Quản trị hệ thống, an toàn dữ liệu |
| **F01_OUT** | Quản trị viên (Admin) | Ra HT | Nhật ký & trạng thái an toàn | Trạng thái cấp tài khoản, Log kiểm toán đăng nhập, Lịch sử cấp quyền, File backup nén (.sql.gz), Kết quả phục hồi | Giám sát vận hành máy chủ Web Admin |
| **F02_IN** | Ban Giám đốc (BoD) | Vào HT | Phê duyệt & Định mức quỹ lương | Hạn mức ngân sách quỹ lương tháng, Định biên nhân sự các bộ phận, Phê duyệt chính sách đãi ngộ mới | Định hướng chiến lược và kiểm soát ngân sách |
| **F02_OUT** | Ban Giám đốc (BoD) | Ra HT | Báo cáo quản trị chiến lược | Báo cáo biến động nhân sự, Báo cáo cơ cấu nhân lực, Tổng quỹ lương thực tế so với định mức | Ra quyết định điều hành cấp cao |
| **F03_IN** | Phòng Nhân sự (HR) | Vào HT | Hồ sơ nhân sự, HĐLĐ & Kỳ lương | Thông tin lý lịch NV, Hợp đồng lao động (ngày ký, ngày hết hạn, lương cơ bản), Thiết lập kỳ lương tháng, Duyệt đơn thai sản/thôi việc | Quản lý nhân viên, hợp đồng và tính lương |
| **F03_OUT** | Phòng Nhân sự (HR) | Ra HT | Cảnh báo HĐ & Bảng lương tháng | Danh sách HĐLĐ sắp hết hạn trước 30 ngày, Bảng tổng hợp công, Dự thảo bảng lương tháng sơ bộ, Báo cáo nhân sự | Chủ động tái ký HĐLĐ, đối soát tiền lương |
| **F04_IN** | Quản lý bộ phận (Manager) | Vào HT | Lịch phân ca & Quyết định duyệt đơn | Bảng xếp ca tuần/tháng theo từng NV (Ca sáng/chiều/đêm), Kết quả duyệt đơn (Đồng ý / Từ chối kèm lý do), Đề xuất thưởng/phạt | Điều phối vận hành ca kíp 24/7 của bộ phận |
| **F04_OUT** | Quản lý bộ phận (Manager) | Ra HT | Thông báo đơn & Báo cáo ca | Thông báo đơn nghỉ phép/đổi ca mới chờ duyệt, Danh sách nhân sự đang trực ca, Báo cáo vi phạm (đi trễ, về sớm) | Giám sát kỷ luật và đảm bảo quân số phục vụ |
| **F05_IN** | Nhân viên (Employee) | Vào HT | Chấm công & Đơn từ điện tử | Mã NV, Thời gian Check-in, Thời gian Check-out, 4 loại đơn trực tuyến, Yêu cầu in phiếu lương | Ghi nhận ngày công thực tế trên Mobile App |
| **F05_OUT** | Nhân viên (Employee) | Ra HT | Lịch ca, Duyệt đơn & Phiếu lương | Lịch làm việc ca kíp được phân công, Thông báo kết quả duyệt đơn từ, Phiếu lương chi tiết PDF (Gross, Net, BHXH, OT, Phụ cấp ca đêm) | Tự tra cứu trên điện thoại cá nhân |

---

## 2. Sơ Đồ Luồng Dữ Liệu Mức 1 (DFD Level 1)

Phân rã tiến trình `0.0` thành 8 tiến trình con kết nối cùng **5 kho dữ liệu Supabase (D1 đến D5)**:

### Danh sách 5 Kho Dữ liệu (Data Stores):
- **D1: Kho Nhân sự & HĐLĐ** (`NHAN_VIEN`, `HOP_DONG`, `PHONG_BAN`, `CHUC_VU`).
- **D2: Kho Tài khoản & Phân quyền** (`TAI_KHOAN`, `VAI_TRO`, `QUYEN`, `VAI_TRO_QUYEN`).
- **D3: Kho Ca làm & Chấm công** (`CA_LAM_VIEC`, `PHAN_CA`, `CHAM_CONG`).
- **D4: Kho Đơn từ điện tử** (`LOAI_DON`, `DON_TU`).
- **D5: Kho Tiền lương & Thu chi** (`KY_LUONG`, `CAU_HINH_KHOAN_LUONG`, `PHIEU_LUONG`, `CHI_TIET_PHIEU_LUONG`).

### Ma Trận Tương Tác CRUD (Tiến Trình ↔ Kho Dữ Liệu):

| Mã TT | Tên Tiến Trình Mức 1 | Tác Nhân | D1 (Nhân sự) | D2 (Account) | D3 (Chấm công) | D4 (Đơn từ) | D5 (Tiền lương) |
|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|
| **1.0** | Quản lý Nhân viên & HĐLĐ | Phòng HR | **C, U, R** | **R** | — | — | — |
| **2.0** | Tài khoản & Phân quyền (RBAC) | Quản trị viên (Admin) | **R** | **C, U, R** | — | — | — |
| **3.0** | Ca làm & Chấm công 24/7 | Manager & Employee | **R** | — | **C, U, R** | — | — |
| **4.0** | Quản lý Đơn từ điện tử | Employee, Manager, HR | **R** | — | **R** | **C, U, R** | — |
| **5.0** | Tính toán Lương tự động | HR & Ban Giám đốc | **R** | — | **R** | **R** | **C, U, R** |
| **6.0** | Phiếu lương & Xuất PDF | Nhân viên (Employee) | **R** | — | — | — | **R** |
| **7.0** | Báo cáo & Thống kê Quản trị | Ban Giám đốc & HR | **R** | **R** | **R** | **R** | **R** |
| **8.0** | Quản trị Danh mục Hệ thống | Quản trị viên (Admin) | **U** | — | **U** | **U** | **U** |
