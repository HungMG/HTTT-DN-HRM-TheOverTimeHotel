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
 QUẢN TRỊ VIÊN (ADMIN) │                      │ TIẾN│                │    QUẢN LÝ BỘ PHẬN   │                        │
│   (Web Admin Server)  ├─────────────────────►│         0.0          │◄─────────────────────┤  (FO, HK, F&B, Maint)  │
                       │Account, RBAC, Backup │  HỆ │ Xếp ca 24/7, Du│                      │                        │
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

> ### ⚠️ GHI CHÚ KỸ THUẬT — SƠ ĐỒ NGỮ CẢNH PHẢI ĐƯỢC VẼ LẠI BẰNG CÔNG CỤ VẼ
>
> Khối sơ đồ ASCII ở trên **đang bị lỗi căn chỉnh**: các hộp tác nhân lệch cột so với hộp tiến trình trung tâm, mũi tên không nối liền với mép hộp, và các đường nối bên dưới bị chồng lấn lên nhau. **Không sửa tay các ký tự người** — căn chỉnh ASCII bằng tay gần như không bao giờ đạt, và sẽ vỡ lại mỗi khi đổi nội dung.
>
> **Việc cần làm trước khi nộp báo cáo:** vẽ lại sơ đồ này bằng **draw.io (diagrams.net) / Visio / Lucidchart**, xuất ra PNG hoặc SVG rồi nhúng vào báo cáo Word. Toàn bộ dữ liệu đầu vào để dựng lại đã được liệt kê đầy đủ trong **Bảng Từ Điển Luồng Dữ Liệu Mức Ngữ Cảnh** ngay bên dưới và trong ma trận ánh xạ Mức 0 ↔ Mức 1 ở mục 2.7.
>
> **Mô tả chính xác cần dựng lại bằng công cụ vẽ:**
>
> - **Bố cục:** Một hộp trung tâm duy nhất đặt giữa bảng vẽ, nhãn `0.0 — HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG THE OVERTIME HOTEL`. Năm hộp tác nhân bố trí vòng quanh: **trên** = Ban Giám Đốc, **trái** = Quản Trị Viên (Admin), **phải** = Quản Lý Bộ Phận, **dưới-trái** = Phòng Hành Chính - Nhân Sự (HR), **dưới-phải** = Nhân Viên Khách Sạn.
> - **Số luồng:** Đúng **10 mũi tên** (5 tác nhân × 2 chiều), tương ứng F01_IN/OUT, F02_IN/OUT, F03_IN/OUT, F04_IN/OUT, F05_IN/OUT. Mũi tên vào hệ thống và mũi tên ra khỏi hệ thống phải là **hai mũi tên tách biệt** (không dùng mũi tên hai đầu `◄►`), mỗi mũi tên mang nhãn tên luồng in nghiêng.
> - **Nội dung nhãn mũi tên:** viết tắt gọn — F01_IN *"Cấu hình & tài khoản"*, F01_OUT *"Log kiểm toán & file backup"*, F02_IN *"Phê duyệt quỹ lương"*, F02_OUT *"Báo cáo quản trị"*, F03_IN *"Hồ sơ NV, HĐLĐ, kỳ lương"*, F03_OUT *"Cảnh báo HĐLĐ & bảng lương"*, F04_IN *"Xếp ca & duyệt đơn"*, F04_OUT *"Thông báo & báo cáo ca"*, F05_IN *"Chấm công & nộp đơn"*, F05_OUT *"Lịch ca, kết quả đơn, phiếu lương"*.
> - **Quy ước hình nét (giữ nhất quán với DFD Mức 1 ở mục 2):** tác nhân và tiến trình = hộp chữ nhật; kho dữ liệu = hai đường kẻ song song; luồng dữ liệu = mũi tên có nhãn.

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

Phân rã tiến trình `0.0` thành **12 tiến trình con** kết nối cùng **8 kho dữ liệu Supabase (D1 đến D8)**.

### Quy ước đánh số tiến trình Mức 1

Vì DFD Mức 1 vừa phải giữ lại 8 tiến trình đã định nghĩa trong PRD 02 (khớp với 8 phân hệ nghiệp vụ cấp 1) vừa phải bổ sung các tiến trình còn thiếu, hệ thống đánh số theo nguyên tắc:

1. **Số 1.0 – 8.0 (giữ nguyên):** 8 tiến trình nghiệp vụ cấp 1, mã y hệt Phân hệ 1.0 – 8.0 trong PRD 02 — thuận tiện đối chiếu chéo giữa hai tài liệu.
2. **Số 9.0 trở đi (tiến trình bổ sung):** đánh số **tăng dần theo thứ tự phụ thuộc nghiệp vụ**, tức tiến trình được đặt trước là tiến trình mà nó phụ thuộc hoặc sinh ra nó. Cách này tránh phải đánh số lại (và vi phạm tính "ổn định mã") toàn bộ 8 tiến trình cũ mỗi khi bổ sung một chức năng mới:
   - **9.0 — Xác thực & Quản lý Phiên làm việc**: đứng trước **mọi** tiến trình còn lại vì mọi tiến trình đều nằm sau cổng đăng nhập.
   - **10.0 — Sao lưu & Phục hồi CSDL**: tiến trình hệ thống độc lập, không nằm trên đường nghiệp vụ chính.
   - **11.0 — Duyệt & Chốt bảng lương**: đứng sau **5.0** (tính lương) và trước **6.0** (phát hành phiếu PDF), vì bảng lương chỉ được trả về cho nhân viên **sau khi đã được khóa sổ**.

### Quy ước ký hiệu tương tác với kho dữ liệu (Ma trận CRUD)

DFD kinh điển (Gane & Sarson) không đánh dấu thao tác trên mũi tên mà chỉ ghi **tên luồng dữ liệu**. Trong tài liệu đồ án này, để ma trận dạng bảng vẫn giữ được thông tin về thao tác, ta dùng bộ ký hiệu CRUD chuẩn — đây là **quy ước mở rộng hợp lệ** (dạng "bảng CRUD" / "CRUD chart" trong DFD mở rộng), được giải thích cụ thể như sau:

| Ký hiệu | Nghĩa tiếng Anh | Nghĩa tiếng Việt | Biểu hiện trên sơ đồ DFD |
|:---:|:---|:---|:---|
| **C** | Create | Tạo mới bản ghi | Mũi tên **ra** kho, dữ liệu được **ghi thêm** |
| **U** | Update | Cập nhật bản ghi đã có | Mũi tên **ra** kho, dữ liệu bị **ghi đè / thay đổi** |
| **R** | Read | Truy vấn, đọc bản ghi | Mũi tên **vào** kho |
| **—** | — | Không có tương tác | Không vẽ mũi tên |

> **Lưu ý học thuật:** C/U/R là **thao tác trên kho dữ liệu** (Data Store), **không phải** tên luồng dữ liệu. Trên sơ đồ vẽ, mỗi ô "C, U, R" của ma trận phải được thay bằng **mũi tên có ghi nhãn tên luồng tương ứng** (xem mục 2.5 — Quy ước khi dựng sơ đồ).

### 2.1. Danh sách 8 Kho Dữ Liệu (Data Stores)

Kho dữ liệu trong DFD là **gom nhóm nhiều bảng CSDL vùng trữ cùng chủ đề**, không phải "một kho = một bảng". Vì vậy 25 bảng CSDL được gom thành 8 kho:

| Mã | Tên Kho Dữ Liệu | Các Bảng CSDL Thành Phần (tên gốc trong Supabase) |
|:---:|:---|:---|
| **D1** | Kho Nhân sự & HĐLĐ | `phong_ban`, `chuc_vu`, `nhan_vien`, `hop_dong` |
| **D2** | Kho Tài khoản & Phân quyền | `tai_khoan`, `vai_tro`, `quyen`, `vai_tro_quyen`, `yeu_cau_tai_khoan` |
| **D3** | Kho Ca làm & Chấm công | `ca_lam_viec`, `phan_ca`, `cham_cong` |
| **D4** | Kho Đơn từ điện tử | `loai_don`, `don_tu` |
| **D5** | Kho Tiền lương & Thu chi | `ky_luong`, `phieu_luong`, `chi_tiet_phieu_luong`, `chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn`, `chi_tiet_tru_vi_pham_tam_ung`, `cau_hinh_khoan_luong`, `cau_hinh_tang_ca`, `cau_hinh_phu_cap`, `cau_hinh_khoan_tru` |
| **D6** | Kho Nhật ký & Thông báo | `nhat_ky_quan_tri` (+ bảng thông báo nếu bổ sung — xem ghi chú bên dưới) |
| **D7** | Kho Tham số Quỹ & Ngày nghỉ lễ | *(cần xác minh — chưa có bảng trong CSDL hiện tại)* |
| **D8** | Kho Tệp & Tài liệu đính kèm | *(cần xác minh — chưa có bảng trong CSDL hiện tại)* |

**Ghi chú về các kho mới bổ sung (D6, D7, D8) và các nhóm nghiệp vụ còn thiếu:**

- **D6 — Kho Nhật ký & Thông báo:** Luồng **F01_OUT** đã mô tả rõ *"Log kiểm toán đăng nhập, Lịch sử cấp quyền"* và các luồng F04/F05 đều có kết quả *"Thông báo…"*, nhưng trước đây **không kho nào lưu**. Trong CSDL hiện tại chỉ có bảng `nhat_ky_quan_tri` (ghi nhận thao tác quản trị: đổi mật khẩu, cấp quyền, khóa tài khoản…). **Cần xác minh:** hệ thống hiện **chưa có bảng thông báo riêng** (`thong_bao`); các thông báo trong F04/F05 đang được hiển thị trực tiếp từ trạng thái bản ghi `don_tu.trang_thai = 'CHO_DUYET'` mà không lưu lại lịch sử đã gửi. Nếu GV yêu cầu DFD phải thể hiện đầy đủ "luồng thông báo", cần bổ sung bảng `thong_bao` vào D6.
- **D7 — Kho Tham số Quỹ & Ngày nghỉ lễ:** gồm ba nhóm nghiệp vụ đang **có trong yêu cầu nghiệp vụ nhưng chưa có bảng riêng** trong CSDL:
  - *Quỹ phép năm* (ngân sách phép năm theo từng phòng ban, bộ phận) — dùng để đối soát khi phê duyệt đơn nghỉ phép ở tiến trình **4.0**.
  - *Ngân sách quỹ lương* (hạn mức quỹ lương theo từng kỳ/tháng do Ban Giám Đốc phê duyệt, luồng **F02_IN**) — dùng cho chức năng **7.4 Quỹ lương vs Ngân sách** (luồng **F02_OUT**).
  - *Lịch nghỉ lễ / ngày lễ Tết* — dùng để áp hệ số OT **3.0 cho ngày Lễ** và loại trừ ngày nghỉ lễ khỏi chuẩn công ở tiến trình **5.0**.
  - **Quy ước gộp kho (khi triển khai):** nhóm này **có thể gộp vào D1** (bảng `nhan_vien` mở rộng cột ngạch hạn mức phép, lịch nghỉ lễ) **hoặc gộp vào D5** (bảng tham số `cau_hinh_khoan_luong` mở rộng thành bảng tham số tổng quát). Với quy mô nghiệp vụ hiện tại, tách kho riêng giúp DFD thể hiện rõ hơn nhưng làm sơ đồ rối — **cần xác minh** ý kiến GV trước khi chốt.
- **D8 — Kho Tệp & Tài liệu đính kèm:** phục vụ **tệp đính kèm y tế của đơn từ** (ảnh chụp giấy nghỉ bệnh / thai sản), và tệp backup nén `.sql.gz` sinh ra ở luồng **F01_OUT**. **Cần xác minh:** hiện bảng `don_tu` **không có cột lưu đường dẫn tệp** (`LyDo` là kiểu `TEXT` thuần), và luồng backup hiện trả tệp về máy người dùng chứ chưa lưu trong Supabase Storage. Hai nhu cầu này về kỹ thuật khác nhau (ảnh y tế cần RLS riêng; file backup cần bảo mật tuyệt đối) nên **không nên gộp vào cùng một kho** nếu triển khai thật.
- **Lịch sử điều động công tác:** chưa có bảng riêng. Thông tin này hiện chỉ được phản ánh **gián tiếp** qua hai bảng lịch sử: đổi phòng / chức vụ thể hiện ở bảng `hop_dong` (phát sinh HĐLĐ mới) và quá trình làm việc thể hiện ở bảng `cham_cong`. **Cần xác minh:** nếu báo cáo cần truy vết *"nhân viên này từng qua các phòng ban nào, ai điều động, lý do"*, phải bổ sung bảng `lich_su_dieu_dong`; khi đó **gộp vào D1** (cùng nhóm hồ sơ nhân sự) là hợp lý nhất.
- **D1: Kho Nhân sự & HĐLĐ** (`NHAN_VIEN`, `HOP_DONG`, `PHONG_BAN`, `CHUC_VU`).
- **D2: Kho Tài khoản & Phân quyền** (`TAI_KHOAN`, `VAI_TRO`, `QUYEN`, `VAI_TRO_QUYEN`, `YEU_CAU_TAI_KHOAN`).
- **D3: Kho Ca làm & Chấm công** (`CA_LAM_VIEC`, `PHAN_CA`, `CHAM_CONG`).
- **D4: Kho Đơn từ điện tử** (`LOAI_DON`, `DON_TU`).
- **D5: Kho Tiền lương & Thu chi** (`KY_LUONG`, `CAU_HINH_KHOAN_LUONG`, `PHIEU_LUONG`, `CHI_TIET_PHIEU_LUONG`).

### 2.2. Ma Trận Tương Tác CRUD (Tiến Trình ↔ Kho Dữ Liệu)

| Mã TT | Tên Tiến Trình Mức 1 | Tác Nhân | D1 (Nhân sự) | D2 (Account) | D3 (Chấm công) | D4 (Đơn từ) | D5 (Tiền lương) | D6 (Nhật ký) | D7 (Quỹ & Lễ) | D8 (Tệp) |
|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **1.0** | Quản lý Nhân viên & HĐLĐ | Phòng HR | **C, U, R** | **R** | — | — | — | — | **R** | **R** |
| **2.0** | Tài khoản & Phân quyền (RBAC) | Quản trị viên (Admin) + Bộ phận CNTT (IT) | **R** | **C, U, R** | — | — | — | **C, R** | — | — |
| **3.0** | Ca làm & Chấm công 24/7 | Manager & Employee | **R** | — | **C, U, R** | — | — | — | **R** | — |
| **4.0** | Quản lý Đơn từ điện tử | Employee, Manager, HR | **R** | — | **R** | **C, U, R** | — | **R** | **R** | **R, C** |
| **5.0** | Tính toán Lương tự động | HR & Ban Giám đốc | **R** | — | **R** | **R** | **C, U, R** | — | **R** | — |
| **6.0** | Phiếu lương & Xuất PDF | Nhân viên (Employee) | **R** | — | — | — | **R** | **R** | — | — |
| **7.0** | Báo cáo & Thống kê Quản trị | Ban Giám đốc & HR | **R** | **R** | **R** | **R** | **R** | — | **R** | — |
| **8.0** | Quản trị Danh mục Hệ thống | Quản trị viên (Admin) | **U** | — | **U** | **U** | **U** | — | **U** | — |
| **9.0** | Xác thực & Quản lý Phiên làm việc | Tất cả 5 tác nhân | **R** | **R** | — | — | — | **C, R** | — | — |
| **10.0** | Sao lưu & Phục hồi CSDL | Quản trị viên (Admin) | — | — | — | — | — | **C, R** | **R** | **C, R** |
| **11.0** | Duyệt & Chốt bảng lương | HR & Ban Giám đốc | **R** | — | **R** | **R** | **C, U, R** | **C, R** | **R** | — |
| **12.0** | Cảnh báo tự động & Thông báo | Tất cả 5 tác nhân | **R** | — | **R** | **R** | **R** | **C, R** | **R** | — |

> **Ghi chú:** Các ô trong ma trận trên thể hiện **thao tác trên kho dữ liệu**, không phải tên luồng — xem lại bảng giải nghĩa C/U/R ở phần trên.
>
> **Bổ sung ngày 09/10/2026 — quy trình cấp tài khoản đi qua kho D2:** Với bảng `yeu_cau_tai_khoan` mới, tiến trình **2.0** không chỉ ghi vào nhóm RBAC (`vai_tro`, `quyen`, `vai_tro_quyen`) mà còn **tạo và cập nhật các yêu cầu cấp tài khoản**. Nguồn dữ liệu của yêu cầu là **kho D1** (HR đọc HĐLĐ đã duyệt ở tiến trình **1.0**), nên phép `R` của tiến trình 2.0 vào D1 là bắt buộc. Quy trình đầy đủ: **1.0** (duyệt HĐLĐ) → **2.0** (HR gửi yêu cầu) → **IT xử lý và cấp tài khoản** → kết quả ghi vào `yeu_cau_tai_khoan.trang_thai = 'DA_CAP'`.

### 2.3. Quy Tắc Phân Tách Quyền Ghi Danh Mục (Xóa Nhất Quán)

Phiên bản cũ của ma trận có mâu thuẫn: bảng `phong_ban`, `chuc_vu` (trong D1) và bảng `loai_don` (trong D4) bị **ghi (C/U) bởi cả tiến trình nghiệp vụ lẫn tiến trình 8.0**, tạo ra **hai chủ sở hữu cho cùng một nhóm danh mục** mà không có quy tắc phân tách — sau khi sửa:

| Nhóm Danh Mục | Kho | Chủ Sở Hữu Duy Nhất (được quyền Create/Update) | Tiến Trình Nghiệp Vụ Được Quyền Gì? |
|:---|:---:|:---|:---|
| Phòng ban, Chức vụ | D1 | **8.0 Quản trị Danh mục Hệ thống** (Admin) | Chỉ **R** — đọc để gán nhân viên, hiển thị bộ phận trong báo cáo |
| Loại đơn | D4 | **8.0 Quản trị Danh mục Hệ thống** (Admin) | Chỉ **R** — đọc để dựng danh sách 4 loại đơn trên Mobile App |
| Mẫu ca làm việc 24/7 | D3 | **8.0 Quản trị Danh mục Hệ thống** (Admin) | Chỉ **R** — đọc khung giờ để xếp ca và tính phụ cấp đêm 30% |
| Tham số thu chi | D5 | **8.0 Quản trị Danh mục Hệ thống** (Admin) | Chỉ **R** — đọc định mức để công thức lương tự áp dụng |

> **Quy tắc mang tính học thuật — Dữ liệu danh mục chỉ có một chủ sở hữu:** Trong DFD chuẩn, mỗi kho dữ liệu có **một chủ sở hữu chính** (đọc–ghi) và các tiến trình khác chỉ được **đọc**. Nếu cho phép hai tiến trình cùng ghi vào một kho danh mục, sẽ phát sinh khả năng **race condition** (hai màn hình cùng sửa một danh mục, giá trị cuối cùng bị ghi đè không kiểm soát) và vi phạm tính toàn vẹn dữ liệu. Quy tắc áp dụng cho toàn hệ thống: **tiến trình nghiệp vụ không bao giờ tạo mới hay sửa danh mục hệ thống** — chúng chỉ đọc danh mục rồi ghi vào kho nghiệp vụ riêng của mình (D3, D4, D5).

### 2.4. Danh Mục Luồng Nội Bộ Giữa Các Tiến Trình (Internal Data Flows)

> **⚠️ Đây là các luồng được SUY RA TỪ MA TRẬN CRUD, chưa phải luồng đã được khảo sát khả năng nghiệp vụ.**
> Cơ sở suy ra: nếu hai tiến trình cùng đọc một kho dữ liệu và tiến trình sau phụ thuộc vào kết quả tính toán của tiến trình trước, thì tồn tại một luồng nội bộ truyền dữ liệu giữa chúng. **Trước khi nộp báo cáo, cần đối chiếu lại từng luồng dưới đây với mã nguồn thực tế** để xác nhận cho chính xác tên và nội dung luồng.

| Mã Luồng | Từ Tiến Trình | Đến Tiến Trình | Tên Luồng Nội Bộ | Dữ Liệu Truyền | Cơ Sở Suy Ra |
|:---:|:---:|:---:|:---|:---|:---|
| **G01** | **3.0** Ca làm & Chấm công | **5.0** Tính toán Lương | *Bảng công đã đối soát theo kỳ* | Số ca quẹt thẻ hợp lệ, giờ OT, giờ ca đêm, số phút trễ/sớm, danh sách bỏ ca | Cả 3.0 và 5.0 đọc D3; PRD 02 mục 5.1–5.6 nêu rõ đầu vào lấy từ `cham_cong` |
| **G02** | **1.0** Quản lý NV & HĐLĐ | **5.0** Tính toán Lương | *Mức lương cơ bản pháp lý theo kỳ* | `luong_co_ban` từ `hop_dong` (ảnh chụp tại thời điểm ký) | PRD 02 mục 5.1: *"Mức lương cơ bản thỏa thuận pháp lý từ bảng `hop_dong`"* |
| **G03** | **4.0** Quản lý Đơn từ | **5.0** Tính toán Lương | *Ngày nghỉ hưởng lương đã duyệt* | Danh sách đơn `trang_thai = 'DA_DUYET'` có `co_huong_luong = TRUE`, nằm trong khoảng `tu_ngay` – `den_ngay` của kỳ lương | PRD 02 mục 5.1: *"Ngày công thực tế = Ca quẹt + Phép năm duyệt"* |
| **G04** | **5.0** Tính toán Lương | **11.0** Duyệt & Chốt bảng lương | *Bảng lương tháng dự thảo đã tính xong* | Danh sách `phieu_luong` trạng thái `NHAP` + các dòng `chi_tiet_phieu_luong` phân rã | Ma trận CRUD: 5.0 và 11.0 đều ghi (C, U) vào D5; 11.0 là bước xác nhận sau 5.0 |
| **G05** | **11.0** Duyệt & Chốt bảng lương | **6.0** Phiếu lương & Xuất PDF | *Bảng lương đã khóa sổ để phát hành* | `phieu_luong.trang_thai` chuyển `DA_DUYET` → `DA_THANH_TOAN`; `ky_luong.trang_thai = 'KHOA_SO'` | PRD 05 mục 2 — quy trình *"Đối soát công → Chạy tính toán → Duyệt bảng, khóa sổ kỳ lương → Sinh file PDF"* |
| **G06** | **1.0 / 3.0 / 4.0 / 5.0 / 11.0** | **7.0** Báo cáo & Thống kê | *Tập dữ liệu nghiệp vụ tổng hợp theo kỳ* | Dữ liệu đã chốt từ D1, D3, D4, D5 để tính biến động nhân sự, cơ cấu bộ phận, quỹ lương vs ngân sách, tỷ lệ chuyên cần | Ma trận CRUD: 7.0 đọc (R) **tất cả** kho nghiệp vụ |
| **G07** | **9.0** Xác thực | **Tất cả 11 tiến trình còn lại** | *Phiên làm việc đã xác thực & danh sách quyền được cấp* | JWT / token phiên, `ma_vai_tro`, tập mã quyền lấy từ `vai_tro_quyen` | Mọi tiến trình đều nằm sau cổng đăng nhập; tiến trình 6.0 cần nó để ràng buộc *"nhân viên chỉ xem phiếu lương của chính mình"* |
| **G08** | **9.0** Xác thực | **12.0** Cảnh báo tự động | *Sự kiện cần đẩy thông báo tới người dùng* | Đăng nhập thất bại nhiều lần, phiên hết hạn, HĐLĐ sắp hết hạn trước 30 ngày | Luồng F01_OUT *"Log kiểm toán đăng nhập"* và F03_OUT *"Cảnh báo HĐLĐ"* đều cần đẩy qua 12.0 |
| **G09** | **10.0** Sao lưu & Phục hồi | **9.0** Xác thực | *Kết quả phục hồi ảnh hưởng trạng thái tài khoản* | Trạng thái `tai_khoan.trang_thai`, danh sách phiên đang hoạt động cần vô hiệu hóa sau khi nạp lại dữ liệu | PRD 02 mục 8.5: *"Restore: Xác thực lại danh tính Admin → Nạp kịch bản phục hồi an toàn"* |
| **G10** | **1.0** Quản lý NV & HĐLĐ | **2.0** Tài khoản & Phân quyền (RBAC) | *Hợp đồng lao động đã được duyệt, cần cấp tài khoản đăng nhập* | Bản ghi `hop_dong` có `trang_thai = 'HIEU_LUC'` cùng hồ sơ `nhan_vien` tương ứng | Bổ sung ngày 09/10/2026 — tiến trình 2.0 đọc D1 để biết ai vừa ký HĐLĐ và cần tài khoản; HR tạo yêu cầu trong bảng `yeu_cau_tai_khoan` (kho D2) |
| **G11** | **2.0** Tài khoản & Phân quyền (RBAC) | **9.0** Xác thực & Quản lý Phiên làm việc | *Tài khoản vừa được bộ phận CNTT (IT) cấp* | Tên đăng nhập `ten_dang_nhap` và mã vai trò `ma_vai_tro` để IT cấp user trong `auth.users`; tiến trình 9.0 xác thực và gom quyền vào JWT | Bổ sung ngày 09/10/2026 — `yeu_cau_tai_khoan.trang_thai` chuyển `DA_CAP` là điều kiện tiên quyết để tiến trình 9.0 ghi nhận phiên làm việc |

**Tóm tắt:** tổng cộng **11 luồng nội bộ (G01–G11)** — trong đó G07 là luồng nhiều đích (1 tiến trình → 11 tiến trình), và G06 gộp 5 nguồn vào 1 đích. Khi dựng sơ đồ bằng draw.io, các luồng này vẽ bằng **đường nét đứt** để phân biệt với luồng ngoài (tiến trình ↔ tác nhân) vẽ bằng **đường liền**.

### 2.5. Bảng Ánh Xạ Cân Bằng Mức 0 ↔ Mức 1

DFD Mức 0 có **5 tác nhân × 2 chiều = 10 luồng (F01–F05)**. Bảng dưới đây khoanh vùng **mỗi luồng Mức 0 thuộc tiến trình nào ở Mức 1** — đảm bảo không có luồng nào của Mức 0 bị bỏ sót khi phân rã, và không có tiến trình Mức 1 nào thừa (không phục vụ luồng nào).

| Mã Luồng Mức 0 | Tên Luồng | Tiến Trình Mức 1 Xử Lý | Kho Dữ Liệu Mức 1 Liên Quan |
|:---:|:---|:---|:---|
| **F01_IN** | Thông tin cấu hình & tài khoản | **2.0** (RBAC) + **8.0** (Danh mục hệ thống) + **10.0** (lệnh backup) | D2, D1, D3, D4, D5, D7 |
| **F01_OUT** | Nhật ký & trạng thái an toàn | **9.0** (ghi log đăng nhập) + **10.0** (kết quả backup) + **12.0** (thông báo) | D6, D8 |
| **F02_IN** | Phê duyệt & Định mức quỹ lương | **11.0** (duyệt bảng lương) + **7.0** (đối soát ngân sách) | D5, D7 |
| **F02_OUT** | Báo cáo quản trị chiến lược | **7.0** | D1, D3, D4, D5, D7 |
| **F03_IN** | Hồ sơ nhân sự, HĐLĐ & Kỳ lương | **1.0** (hồ sơ, HĐLĐ) + **11.0** (thiết lập kỳ lương) | D1, D5 |
| **F03_OUT** | Cảnh báo HĐ & Bảng lương tháng | **7.0** (báo cáo) + **11.0** (bảng lương đã khóa) + **12.0** (cảnh báo HĐLĐ hết hạn) | D1, D5, D6 |
| **F04_IN** | Lịch phân ca & Quyết định duyệt đơn | **3.0** (xếp ca) + **4.0** (duyệt đơn) + **5.0** (đề xuất thưởng/phạt) | D3, D4, D5 |
| **F04_OUT** | Thông báo đơn & Báo cáo ca | **3.0** (danh sách đang trực ca) + **7.0** (báo cáo vi phạm) + **12.0** (thông báo) | D3, D6 |
| **F05_IN** | Chấm công & Đơn từ điện tử | **3.0** (quẹt thẻ) + **4.0** (nộp đơn) | D3, D4 |
| **F05_OUT** | Lịch ca, Duyệt đơn & Phiếu lương | **3.0** (lịch ca cá nhân) + **4.0** (kết quả duyệt đơn) + **6.0** (phiếu lương PDF) + **12.0** (thông báo) | D3, D4, D5, D6 |

> **Kiểm tra cân bằng:** cả 12 tiến trình Mức 1 đều xuất hiện trong bảng trên — không có tiến trình "mồ côi". Ngược lại, **9.0, 10.0, 11.0, 12.0 là các tiến trình mới bổ sung** và chúng chỉ xuất hiện trong luồng nội bộ (G07–G11) chứ không sinh ra luồng ngoài mới với tác nhân — đúng với bản chất kỹ thuật của chúng (xác thực / sao lưu / khóa sổ / thông báo đều là tiến trình dọc theo luồng đã có).

### 2.6. Quy Ước Khi Dựng Sơ Đồ Mức 1 Bằng Công Cụ Vẽ

| Thành phần | Ký hiệu chuẩn | Ghi chú cho người vẽ |
|:---|:---|:---|
| Tiến trình | Hộp chữ nhật, nhãn `Mã - Tên` | Ví dụ: `5.0 - Tính toán Lương tự động` |
| Kho dữ liệu | Hai đường kẻ song song, nhãn `D1 - Tên` | **Phải là 2 đường**, không dùng hộp chữ nhật |
| Luồng ngoài (↔ tác nhân) | Mũi tên nét liền, có nhãn tên luồng | Dùng lại đúng tên F01–F05 của Mức 0 để đối chiếu |
| Luồng nội bộ (↔ tiến trình) | Mũi tên nét đứt, có nhãn `G01–G11` | Danh mục tại mục 2.4 |
| Thao tác lên kho | Ghi C / U / R cạnh mũi tên | `C`, `U` trên mũi tên đi ra kho; `R` trên mũi tên đi vào kho |
| Thứ tự bố trí | Từ trái sang phải theo dòng nghiệp vụ | 9.0 ở ngoài cùng bên trái (cổng đăng nhập), 12.0 ở ngoài cùng bên phải (đẩy thông báo) |

### 2.7. Đối Chiếu Số Lượng Bảng CSDL

| Nơi khai báo | Số bảng | Tình trạng |
|:---|:---:|:---|
| PRD 03 (tài liệu này, mục 2.1) | 8 kho dữ liệu gom từ **25 bảng** | ✅ Đã cập nhật — khớp với mã nguồn |
| Mã nguồn thực tế `AppDbContext.cs` | **25 `DbSet`** | ✅ Đã cập nhật — đã có `DbSet<YeuCauTaiKhoan>` |
| PRD 04 (`04_Thiet_Ke_CSDL_17_Bang.md`) | 25 bảng | ✅ Đã cập nhật đầy đủ — **tiêu đề nội dung** đã sửa thành *"25 Bảng"*. *(Lưu ý: **tên file** vẫn là `..._17_Bang.md` vì được tham chiếu ở nhiều nơi — **không đổi tên file**, chỉ đọc con số trong tiêu đề nội dung.)* |
| PRD 02 (mục 5, ma trận đối chiếu CRUD) | **25 bảng CSDL Supabase** | ✅ Đã cập nhật — khớp với PRD 03 và mã nguồn |

**Chi tiết 8 bảng đã bổ sung sau 17 bảng ban đầu** (theo `migrations/20260918_add_admin_audit_log.sql`, `migrations/20260930_upgrade_to_23_tables.sql` và migration ngày 09/10/2026):

| STT | Bảng | Kho DFD | Nhóm nghiệp vụ |
|:---:|:---|:---:|:---|
| 18 | `nhat_ky_quan_tri` | D6 | Nhật ký kiểm toán quản trị |
| 19 | `cau_hinh_tang_ca` | D5 | Tham số hệ số tăng ca (OT) |
| 20 | `cau_hinh_phu_cap` | D5 | Tham số phụ cấp (ăn ca, ca đêm 30%) |
| 21 | `cau_hinh_khau_tru` | D5 | Tham số khấu trừ (BHXH, thuế TNCN, phạt) |
| 22 | `chi_tiet_tru_bao_hiem` | D5 | Phân rã BHXH 8% / BHYT 1.5% / BHTN 1% |
| 23 | `chi_tiet_tru_thue_tncn` | D5 | Phân rã thuế TNCN lũy tiến |
| 24 | `chi_tiet_tru_vi_pham_tam_ung` | D5 | Phân rã tiền phạt đi trễ / về sớm / bỏ ca |
| **25** | **`yeu_cau_tai_khoan`** | **D2** | **Quy trình HR gửi yêu cầu → bộ phận CNTT (IT) cấp tài khoản đăng nhập** |

> **Ghi chú dẫn chiếu sang PRD 04:** Nội dung chi tiết từng bảng (tên cột, kiểu dữ liệu, khóa chính / khóa ngoại) của đủ **25 bảng** đã được mô tả đầy đủ trong PRD 04 — bao gồm bảng mới `yeu_cau_tai_khoan` (Bảng 9 trong từ điển dữ liệu của PRD 04). Riêng tên cột thực tế của một số bảng đã được đối chiếu lại với CSDL (ví dụ bảng `phieu_luong` trong CSDL dùng `tien_luong_cong`, `tien_tang_ca`, `tong_phu_cap`, `tong_thuong`, `thuc_nhan` thay vì `tong_thu_nhap`, `tong_khau_tru`, `luong_thuc_nhan`).
