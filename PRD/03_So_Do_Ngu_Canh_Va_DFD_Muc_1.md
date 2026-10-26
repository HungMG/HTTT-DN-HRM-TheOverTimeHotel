# PRD 03: Sơ Đồ Ngữ Cảnh & Sơ Đồ Luồng Dữ Liệu Mức 1 (DFD Level 1)

**Dự án:** Hệ thống Quản lý Nhân sự & Tiền lương Khách sạn The OverTime Hotel  
**Chuyên ngành:** Hệ thống Thông tin Doanh nghiệp (HTTTDN) / Công nghệ Phần mềm  
**Nền tảng công nghệ:** C# .NET 10 (ASP.NET Core MVC & .NET MAUI) · Supabase Cloud Database (PostgreSQL 30 bảng chuẩn 3NF)  
**Tài liệu trực quan kèm theo:** Xem sơ đồ đồ họa tương tác tại `docs project/diagrams/`

---

## 1. Sơ Đồ Ngữ Cảnh (Context Diagram / DFD Mức 0)

Sơ đồ ngữ cảnh định nghĩa ranh giới tương tác và các luồng thông tin hai chiều giữa Hệ thống HRM Khách sạn The OverTime Hotel (Tiến trình trung tâm `0.0`, mở rộng 11 phân hệ nghiệp vụ) với 5 tác nhân chính ngoài hệ thống:

```text
                                 ┌──────────────────────────────────────────────────┐
                                 │                   BAN GIÁM ĐỐC                   │
                                 └────────────────────────┬──▲──────────────────────┘
                          Duyệt hạn mức quỹ lương tháng,  │  │ Báo cáo quản trị chiến lược, hiệu
                          ngân sách sự kiện & định biên NS│  │ quả dự án & quỹ lương thực tế
                                                          ▼  │
┌───────────────────────┐                               ┌─┴──┴─────────────────────────────┐                               ┌────────────────────────┐
│ QUẢN TRỊ VIÊN (ADMIN) │                               │          TIẾN TRÌNH 0.0          │                               │    QUẢN LÝ BỘ PHẬN     │
│  (Web Admin Server)   ├──────────────────────────────►│                                  │◄──────────────────────────────┤ (FO, HK, F&B, Event...)│
│                       │ Cấu hình RBAC, phân quyền,    │  HỆ THỐNG QUẢN LÝ NHÂN SỰ        │ Lập lịch ca 24/7, duyệt đơn,  │                        │
│                       │ tham số danh mục, lệnh Backup │         & TIỀN LƯƠNG             │ kế hoạch sự kiện, phân công   │                        │
│                       │                               │      THE OVERTIME HOTEL          │ nhân sự & đề xuất thù lao DA  │                        │
│                       │                               │     (Mở rộng 11 Phân hệ)         │                               │                        │
│                       │◄──────────────────────────────┤                                  ├──────────────────────────────►│                        │
│                       │ Log kiểm toán, file backup,   └─┬──▲───────────────────────────┬─┘ Thông báo đơn từ, báo cáo ca, │                        │
└───────────────────────┘ kết quả phục hồi an toàn CSDL   │  │                           │   tiến độ sự kiện & nhân sự DA  └────────────────────────┘
                                                          │  │                           │
                               Hồ sơ NV, HĐLĐ, kỳ lương,  │  │ Check-in/out quẹt thẻ,    │
                               thù lao sự kiện đã chốt    ▼  │ 4 loại đơn trực tuyến,    │
                             ┌────────────────────────────┴──┤ xác nhận tham gia sự kiện │
                             │                               │                           │
                             │ Cảnh báo HĐLĐ hết hạn,        │ Lịch ca 24/7, duyệt đơn,  │
                             │ bảng lương sơ bộ, chi phí DA  │ thù lao DA, phiếu PDF     │
                           ┌─┴─────────────────────────────┐ └─────────────┬─────────────┘
                           │     PHÒNG HÀNH CHÍNH - NS     │               │
                           │        (HR Department)        │               │
                           └───────────────────────────────┘               ▼
                                                           ┌───────────────────────────────┐
                                                           │     NHÂN VIÊN KHÁCH SẠN       │
                                                           │      (Mobile App MAUI)        │
                                                           └───────────────────────────────┘
```

> ### GHI CHÚ KỸ THUẬT — SƠ ĐỒ NGỮ CẢNH CẦN ĐƯỢC XUẤT RA TỪ CÔNG CỤ VẼ
>
> Sơ đồ ngữ cảnh thể hiện sự tương tác của **5 tác nhân ngoài** với hệ thống trung tâm **0.0** (mở rộng 11 phân hệ nghiệp vụ cốt lõi và 4 tiến trình kỹ thuật). Khi đưa vào báo cáo chính thức (Word/PDF), sinh viên sử dụng công cụ **draw.io / Visio / Lucidchart** để xuất sơ đồ dưới dạng đồ họa vector SVG hoặc ảnh nét cao PNG.
>
> **Mô tả cấu trúc chuẩn của sơ đồ:**
> - **Tiến trình trung tâm:** Một hình chữ nhật bo góc đặt chính giữa bảng vẽ, nhãn `0.0 — HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG THE OVERTIME HOTEL (11 PHÂN HỆ)`.
> - **5 tác nhân ngoài:** Bố trí cân đối xung quanh: **Trên** = Ban Giám Đốc, **Trái** = Quản Trị Viên (Admin), **Phải** = Quản Lý Bộ Phận, **Dưới - Trái** = Phòng Hành Chính - Nhân Sự (HR), **Dưới - Phải** = Nhân Viên Khách Sạn (Mobile App).
> - **Số lượng luồng dữ liệu:** Đúng **10 luồng dữ liệu 2 chiều** (5 tác nhân × 2 chiều In/Out), mã hóa từ `F01_IN/OUT` đến `F05_IN/OUT`. Mũi tên vào và mũi tên ra là hai đường nét liền tách biệt, có nhãn diễn giải rõ ràng.
> - **Nghiệp vụ Dự án & Sự kiện:** Được tích hợp đồng bộ vào các luồng: Kế hoạch sự kiện & phân công nhân sự (F04_IN), Tiến độ & nhân sự tham gia sự kiện (F04_OUT), Ngân sách sự kiện (F02_IN), Báo cáo hiệu quả sự kiện (F02_OUT), Kết chuyển thù lao sự kiện vào kỳ lương (F03_IN), Báo cáo chi phí dự án (F03_OUT), Xác nhận tham gia dự án (F05_IN), Thông báo phân công sự kiện & thù lao sự kiện trong phiếu lương (F05_OUT).

### Bảng Từ Điển Luồng Dữ Liệu Mức Ngữ Cảnh:

| Mã Luồng | Tác Nhân | Hướng | Tên Luồng Dữ Liệu | Thuộc Tính Dữ Liệu Chính | Nghiệp Vụ Liên Quan |
|:---:|:---|:---:|:---|:---|:---|
| **F01_IN** | Quản trị viên (Admin) | Vào HT | Thông tin cấu hình, RBAC & sao lưu | `Username`, `PasswordHash`, `RoleId`, danh mục quyền RBAC, danh mục phòng ban, chức vụ, ca làm việc, tham số phụ cấp/phạt, lệnh Backup/Restore CSDL | Quản trị hệ thống, bảo mật tài khoản, an toàn dữ liệu |
| **F01_OUT** | Quản trị viên (Admin) | Ra HT | Nhật ký kiểm toán & trạng thái an toàn | Trạng thái cấp tài khoản, log kiểm toán đăng nhập/thao tác, lịch sử cấp quyền, file backup nén (.sql.gz), kết quả phục hồi dữ liệu | Giám sát vận hành máy chủ Web Admin |
| **F02_IN** | Ban Giám đốc (BoD) | Vào HT | Phê duyệt định mức quỹ lương & dự án | Hạn mức ngân sách quỹ lương tháng, định biên nhân sự các khối, kế hoạch ngân sách sự kiện lớn (tiệc cưới, hội nghị gala), chính sách đãi ngộ | Định hướng chiến lược, kiểm soát ngân sách quỹ lương & sự kiện |
| **F02_OUT** | Ban Giám đốc (BoD) | Ra HT | Báo cáo quản trị chiến lược & hiệu quả sự kiện | Báo cáo biến động nhân sự, cơ cấu nhân lực, tổng quỹ lương thực tế so với hạn mức, báo cáo doanh thu - chi phí thù lao sự kiện | Ra quyết định điều hành cấp cao |
| **F03_IN** | Phòng Nhân sự (HR) | Vào HT | Hồ sơ nhân sự, HĐLĐ, kỳ lương & thù lao sự kiện | Thông tin lý lịch NV, Hợp đồng lao động (ngày ký, hạn HĐ, lương cơ bản), thiết lập kỳ lương tháng, duyệt đơn dài hạn, dữ liệu thù lao dự án/sự kiện đã nghiệm thu | Quản lý vòng đời nhân sự, hợp đồng, kết chuyển lương & thù lao dự án |
| **F03_OUT** | Phòng Nhân sự (HR) | Ra HT | Cảnh báo HĐLĐ, bảng lương & chi phí dự án | Danh sách HĐLĐ sắp hết hạn trước 30 ngày, bảng tổng hợp công, dự thảo bảng lương tháng, báo cáo phân bổ chi phí thù lao sự kiện | Tái ký hợp đồng, thẩm định và khóa sổ bảng lương tháng |
| **F04_IN** | Quản lý bộ phận (Manager) | Vào HT | Lịch phân ca, duyệt đơn, kế hoạch sự kiện & phân công DA | Bảng xếp ca tuần/tháng (ca sáng/chiều/đêm), kết quả duyệt đơn (đồng ý/từ chối kèm lý do), kế hoạch sự kiện tiệc cưới/hội nghị, danh sách phân công nhân sự tham gia dự án, nghiệm thu thù lao sự kiện | Điều phối vận hành ca kíp 24/7, tổ chức sự kiện khách sạn, quản lý nhân sự dự án |
| **F04_OUT** | Quản lý bộ phận (Manager) | Ra HT | Thông báo đơn từ, báo cáo ca & tiến độ sự kiện | Thông báo đơn chờ duyệt, danh sách nhân sự đang trực ca thực tế, báo cáo vi phạm trễ/sớm, tiến độ thực hiện sự kiện và danh sách nhân sự tham gia dự án | Giám sát kỷ luật ca trực và điều hành tiến độ sự kiện |
| **F05_IN** | Nhân viên (Employee) | Vào HT | Chấm công, đơn từ & xác nhận sự kiện | Mã NV, thời gian Check-in, thời gian Check-out, 4 loại đơn trực tuyến (phép, ốm, thai sản, thôi việc), xác nhận tham gia dự án/sự kiện, yêu cầu tải phiếu lương | Ghi nhận ngày công thực tế, nộp đơn và phản hồi phân công dự án trên Mobile App |
| **F05_OUT** | Nhân viên (Employee) | Ra HT | Lịch ca, duyệt đơn, thông báo sự kiện & phiếu lương PDF | Lịch làm việc ca kíp cá nhân, thông báo kết quả duyệt đơn, thông báo phân công dự án/sự kiện, thông báo thù lao sự kiện, phiếu lương chi tiết PDF (Gross, Net, BHXH, OT, Phụ cấp đêm, Thù lao sự kiện) | Tự phục vụ (Employee Self-Service) trên điện thoại cá nhân |

---

## 2. Sơ Đồ Luồng Dữ Liệu Mức 1 (DFD Level 1)

Phân rã tiến trình `0.0` thành **11 tiến trình nghiệp vụ cốt lõi (1.0 đến 11.0)** và **4 tiến trình kỹ thuật & vận hành (12.0 đến 15.0)**, tương tác chặt chẽ với **9 kho dữ liệu Supabase (D1 đến D9)** bao phủ toàn bộ 30 bảng CSDL chuẩn 3NF.

### Quy ước đánh số tiến trình Mức 1

Cấu trúc phân rã DFD Mức 1 được chuẩn hóa theo đúng kiến trúc 11 phân hệ nghiệp vụ mở rộng và 4 tiến trình kỹ thuật hỗ trợ:

1. **Nhóm 11 Tiến trình Nghiệp vụ Cốt lõi (1.0 – 11.0):**
   - **1.0 — Nhân sự & Hợp đồng lao động:** Quản lý lý lịch nhân viên, chức danh, phòng ban và vòng đời hợp đồng lao động.
   - **2.0 — Tài khoản & Phân quyền (RBAC):** Quản lý định danh tài khoản, 5 vai trò (Admin, HR, Manager, Employee, IT), ma trận 17 mã quyền và quy trình cấp phát tài khoản.
   - **3.0 — Ca làm & Phân ca 24/7:** Quản lý mẫu ca trực xoay vòng (Sáng, Chiều, Đêm) và lập lịch xếp ca làm việc theo tuần/tháng.
   - **4.0 — Bảng Chấm công 24/7:** Ghi nhận quẹt thẻ Check-in/out di động, tính toán phút đi trễ, về sớm, giờ tăng ca (OT) và ngày công thực tế.
   - **5.0 — Quản lý Đơn từ:** Tiếp nhận và xử lý quy trình phê duyệt trực tuyến cho 4 loại đơn: Nghỉ phép năm, Nghỉ ốm (BHXH), Thai sản, Thôi việc.
   - **6.0 — Tính Lương & Phiếu Lương PDF:** Tự động hóa công thức tính lương 2 tầng (Gross ➔ Net), tổng hợp ngày công, OT, ca đêm, các khoản trừ và sinh phiếu lương điện tử QuestPDF.
   - **7.0 — Quản lý Phụ cấp:** Thiết lập và quản lý danh mục định mức phụ cấp: phụ cấp ca đêm 30%, ăn ca 30.000 VNĐ/ngày, phụ cấp trách nhiệm, độc hại, đi lại.
   - **8.0 — Quản lý Tiền thưởng:** Quản trị các khoản tiền thưởng: thưởng doanh thu buồng phòng/F&B, thưởng hiệu quả công việc (KPI), thưởng lễ tết và thưởng đột xuất.
   - **9.0 — Quản lý Dự án & Sự kiện Khách sạn:** Quản lý kế hoạch sự kiện (hội nghị gala, tiệc cưới, hội thảo quốc tế), phân công nhân sự các bộ phận tham gia, nghiệm thu và kết chuyển thù lao/phụ cấp sự kiện sang bảng lương.
   - **10.0 — Báo cáo & Thống kê:** Tổng hợp dữ liệu đa nguồn phục vụ Dashboard quản trị: biến động nhân sự, tỷ lệ chuyên cần, đối soát ngân sách quỹ lương và chi phí dự án/sự kiện.
   - **11.0 — Quản trị Hệ thống:** Quản lý danh mục tổ chức dùng chung (phòng ban, chức vụ, loại đơn, khung giờ ca) và tham số cấu hình toàn hệ thống.

2. **Nhóm 4 Tiến trình Kỹ thuật & Vận hành (12.0 – 15.0):**
   - **12.0 — Xác thực & Quản lý Phiên:** Cổng xác thực danh tính tập trung, kiểm tra băm mật khẩu, cấp phát token JWT/Claims và duy trì phiên an toàn cho cả Web Admin và Mobile App.
   - **13.0 — Sao lưu & Phục hồi CSDL:** Tác vụ kỹ thuật độc lập cho Quản trị viên (Admin) thực hiện sao lưu toàn vẹn 30 bảng CSDL Supabase ra tệp nén `.sql.gz` và phục hồi khẩn cấp.
   - **14.0 — Duyệt & Chốt khóa sổ lương:** Quy trình thẩm định bảng lương giữa HR và Ban Giám Đốc, chuyển trạng thái từ dự thảo `NHAP` sang `DA_DUYET`, thực hiện khóa sổ kỳ lương (`KHOA_SO`) trước khi phát hành.
   - **15.0 — Cảnh báo tự động & Thông báo:** Tiến trình nền quét và đẩy cảnh báo tự động: cảnh báo HĐLĐ sắp hết hạn trước 30 ngày, thông báo đơn từ mới, thông báo lịch ca trực và thông báo phân công dự án/sự kiện tới người dùng.

### Quy ước ký hiệu tương tác với kho dữ liệu (Ma trận CRUD)

Để sơ đồ luồng dữ liệu vừa tuân thủ mô hình Gane & Sarson vừa cung cấp đầy đủ thông tin về hành vi dữ liệu cho đội ngũ phát triển, bảng ma trận sử dụng các ký hiệu CRUD chuẩn:

| Ký hiệu | Nghĩa tiếng Anh | Nghĩa tiếng Việt | Biểu hiện trên sơ đồ DFD |
|:---:|:---|:---|:---|
| **C** | Create | Tạo mới bản ghi | Mũi tên từ tiến trình **ghi vào** kho dữ liệu |
| **U** | Update | Cập nhật bản ghi đã có | Mũi tên từ tiến trình **ghi đè / thay đổi** dữ liệu trong kho |
| **R** | Read | Truy vấn, đọc bản ghi | Mũi tên từ kho dữ liệu **đưa vào** tiến trình |
| **—** | — | Không có tương tác | Không vẽ mũi tên liên kết |

---

### 2.1. Danh Sách 9 Kho Dữ Liệu (Data Stores)

Kho dữ liệu trong DFD là tập hợp logic các bảng CSDL lưu trữ cùng chủ đề nghiệp vụ. Toàn bộ **30 bảng CSDL Supabase PostgreSQL** được chuẩn hóa và khớp nối hoàn chỉnh vào **9 kho dữ liệu (D1 đến D9)**:

| Mã Kho | Tên Kho Dữ Liệu | Số Bảng | Các Bảng CSDL Supabase Thành Phần | Chức Năng Nghiệp Vụ Cốt Lõi |
|:---:|:---|:---:|:---|:---|
| **D1** | Kho Nhân sự & HĐLĐ | 4 | `phong_ban`, `chuc_vu`, `nhan_vien`, `hop_dong` | Lưu trữ cơ cấu tổ chức, chức danh nghề nghiệp, hồ sơ lý lịch nhân sự và hợp đồng lao động pháp lý. |
| **D2** | Kho Tài khoản & Phân quyền | 5 | `tai_khoan`, `vai_tro`, `quyen`, `vai_tro_quyen`, `yeu_cau_tai_khoan` | Lưu trữ tài khoản đăng nhập, 5 nhóm vai trò RBAC, danh mục 17 mã quyền và yêu cầu cấp tài khoản từ HR chuyển sang IT. |
| **D3** | Kho Ca làm & Chấm công | 6 | `ca_lam_viec`, `phan_ca`, `cham_cong`, `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca` | Lưu trữ 3 mẫu ca chuẩn 24/7, lịch phân ca tuần/tháng, dữ liệu quẹt thẻ Check-in/out thực tế di động, định biên số người tối thiểu mỗi ca, hồ sơ giải trình sai lệch giờ công và yêu cầu đổi ca giữa nhân viên. |
| **D4** | Kho Đơn từ điện tử | 2 | `loai_don`, `don_tu` | Lưu trữ danh mục 4 loại đơn trực tuyến và lịch sử nộp/xét duyệt đơn từ của nhân sự. |
| **D5** | Kho Tiền lương & Thu chi | 10 | `ky_luong`, `phieu_luong`, `chi_tiet_phieu_luong`, `chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn`, `chi_tiet_tru_vi_pham_tam_ung`, `cau_hinh_khoan_luong`, `cau_hinh_tang_ca`, `cau_hinh_phu_cap`, `cau_hinh_khau_tru` | Lưu trữ chu kỳ tính lương, bảng tổng hợp lương tháng, chi tiết phân rã thu nhập/khấu trừ 2 tầng, và bảng cấu hình tham số lương/thuế/bảo hiểm. |
| **D6** | Kho Nhật ký & Kiểm toán | 1 | `nhat_ky_quan_tri` | Lưu trữ lịch sử kiểm toán thao tác quản trị: đổi mật khẩu, phân quyền, khóa tài khoản, can thiệp dữ liệu hệ thống. |
| **D7** | Kho Tham số Quỹ & Ngày nghỉ lễ | *(Tham số mở rộng)* | Lưu trữ hạn mức ngân sách quỹ lương tháng từ BoD, định mức quỹ phép năm các phòng ban, và danh mục ngày nghỉ Lễ/Tết phục vụ hệ số OT 3.0. |
| **D8** | Kho Tệp & Tài liệu đính kèm | *(Tệp mở rộng)* | Lưu trữ tệp minh chứng y tế/đơn từ (ảnh giấy khám, giấy nghỉ ốm), tệp phiếu lương PDF QuestPDF kết xuất tự động, và file sao lưu nén `.sql.gz`. |
| **D9** | Kho Dự án & Sự kiện Khách sạn | 2 | `du_an`, `phan_cong_du_an` | Lưu trữ thông tin các dự án/sự kiện đặc thù khách sạn (tiệc cưới, hội nghị gala, ẩm thực), phân công nhân sự tham gia, và mức thù lao/phụ cấp sự kiện được hưởng. |

#### Chi tiết 2 bảng CSDL mới trong Kho D9:

1. **Bảng `du_an` (Dự án & Sự kiện Khách sạn):**
   - `ma_du_an` (SERIAL, PK): Mã định danh duy nhất của dự án / sự kiện.
   - `ten_du_an` (VARCHAR(200), NOT NULL, UNIQUE): Tên sự kiện (ví dụ: *Tiệc cưới Hoàng Gia - Sảnh Diamond*, *Hội nghị Thượng đỉnh Kinh tế MICE 2026*, *Festival Ẩm thực Mùa Thu*).
   - `loai_du_an` (VARCHAR(50), NOT NULL, CHECK in ('TIEC_CUOI', 'HOI_NGHI_MICE', 'GALA_DINNER', 'FESTIVAL_AM_THUC', 'DOAN_VIP', 'BAO_TRI_NANG_CAP')): Phân loại tính chất sự kiện để điều phối nguồn lực phù hợp.
   - `dia_diem` (VARCHAR(255), NOT NULL): Khu vực tổ chức trong khách sạn (Grand Ballroom, Lotus Hall, Rooftop Sky Bar...).
   - `ngay_bat_dau` (TIMESTAMPTZ, NOT NULL): Thời điểm bắt đầu triển khai chuẩn bị sự kiện.
   - `ngay_ket_thuc` (TIMESTAMPTZ, NOT NULL): Thời điểm kết thúc, bàn giao hiện trường và nghiệm thu sự kiện.
   - `ngan_sach_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00, CHECK >= 0): Tổng ngân sách phân bổ chi trả thù lao / phụ cấp tăng cường cho nhân sự tham gia sự kiện.
   - `ma_nguoi_quan_ly` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE RESTRICT): Trưởng ban tổ chức / Quản lý điều hành dự án sự kiện (Trưởng bộ phận Event/F&B/Sảnh).
   - `mo_ta` (TEXT, NULL): Kịch bản chi tiết, yêu cầu phục vụ đặc biệt, quy mô khách và tiêu chuẩn kỹ thuật.
   - `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'LEN_KE_HOACH', CHECK in ('LEN_KE_HOACH', 'DANG_DIEN_RA', 'HOAN_THANH', 'HUY')): Tiến độ vòng đời sự kiện.

2. **Bảng `phan_cong_du_an` (Phân công nhân sự & Thù lao sự kiện):**
   - `ma_phan_cong` (SERIAL, PK): Mã bản ghi phân công nhân sự.
   - `ma_du_an` (INT, FK -> du_an.ma_du_an, NOT NULL, ON DELETE CASCADE): Dự án / sự kiện tiếp nhận nhân sự.
   - `ma_nhan_vien` (INT, FK -> nhan_vien.ma_nhan_vien, NOT NULL, ON DELETE CASCADE): Nhân viên được điều động tham gia sự kiện.
   - `vai_tro_du_an` (VARCHAR(100), NOT NULL): Vai trò chuyên môn được giao trong sự kiện (`DIEU_PHOI`, `KY_THUAT_AM_THANH_AS`, `PHUC_VU_TIEC`, `LE_TAN_DON_KHACH`, `AN_NINH_VIP`, `BEP_TIEC`...).
   - `nhiem_vu_cu_the` (VARCHAR(255), NULL): Chi tiết công việc cụ thể tại vị trí trực chiến.
   - `so_tien_phu_cap` (DECIMAL(15,2), NOT NULL, DEFAULT 0.00, CHECK >= 0): Mức thù lao / phụ cấp sự kiện dành riêng cho nhân sự này.
   - `danh_gia` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_DANH_GIA', CHECK in ('CHUA_DANH_GIA', 'HOAN_THANH', 'XUAT_SAC', 'KHONG_THAM_GIA')): Kết quả đánh giá hiệu suất của quản lý sau khi kết thúc sự kiện.
   - `trang_thai_chi_tra` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_CHI_TRA', CHECK in ('CHUA_CHI_TRA', 'DA_KET_CHUYEN_LUONG', 'HUY_BO')): Trạng thái thanh toán thù lao. Khi tính lương tháng (Tiến trình 6.0), các khoản đã hoàn thành sẽ được chuyển sang `DA_KET_CHUYEN_LUONG`.
   - `ma_chi_tiet` (INT, FK -> chi_tiet_phieu_luong.ma_chi_tiet, NULL, ON DELETE SET NULL): Khóa ngoại 1-1 liên kết kết chuyển thù lao sự kiện vào dòng phụ cấp trên phiếu lương tháng.

---

### 2.2. Ma Trận Tương Tác CRUD (Tiến Trình ↔ Kho Dữ Liệu)

Ma trận CRUD dưới đây thể hiện chính xác mối quan hệ tương tác dữ liệu giữa 15 tiến trình DFD Mức 1 và 9 kho dữ liệu. Đặc biệt, ma trận thể hiện rõ:
- **Tiến trình 9.0 (Quản lý Dự án & Sự kiện):** Thực hiện **Create, Update, Read (C, U, R)** vào **Kho D9**.
- **Tiến trình 6.0 (Tính Lương & Phiếu Lương PDF):** Thực hiện **Read (R)** từ **Kho D9** để lấy dữ liệu thù lao/phụ cấp sự kiện đã được nghiệm thu kết chuyển trực tiếp vào phiếu lương nhân viên.

| Mã TT | Tên Tiến Trình Mức 1 | Tác Nhân Chính | D1 (Nhân sự) | D2 (Account) | D3 (Ca & Công) | D4 (Đơn từ) | D5 (Tiền lương) | D6 (Nhật ký) | D7 (Quỹ & Lễ) | D8 (Tệp) | D9 (Dự án & SK) |
|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **1.0** | Nhân sự & HĐLĐ | Phòng HR | **C, U, R** | **R** | — | — | — | — | **R** | **R** | — |
| **2.0** | Tài khoản & Phân quyền (RBAC) | Admin + IT | **R** | **C, U, R** | — | — | — | **C, R** | — | — | — |
| **3.0** | Ca làm & Phân ca 24/7 | Quản lý bộ phận | **R** | — | **C, U, R** | — | — | — | **R** | — | — |
| **4.0** | Bảng Chấm công 24/7 | Employee & Manager | **R** | — | **C, U, R** | — | — | — | **R** | — | — |
| **5.0** | Quản lý Đơn từ | Employee, Manager, HR | **R** | — | **R** | **C, U, R** | — | **R** | **R** | **C, R** | — |
| **6.0** | Tính Lương & Phiếu Lương PDF | HR & Nhân viên | **R** | — | **R** | **R** | **C, U, R** | **R** | **R** | **C, R** | **R** |
| **7.0** | Quản lý Phụ cấp | HR & Quản lý bộ phận | **R** | — | **R** | — | **C, U, R** | — | — | — | — |
| **8.0** | Quản lý Tiền thưởng | Ban Giám đốc & HR | **R** | — | — | — | **C, U, R** | — | **R** | — | — |
| **9.0** | Quản lý Dự án & Sự kiện Khách sạn | BoD, Manager, HR, Employee | **R** | — | **R** | — | — | — | **R** | — | **C, U, R** |
| **10.0** | Báo cáo & Thống kê | Ban Giám đốc & HR | **R** | **R** | **R** | **R** | **R** | — | **R** | — | **R** |
| **11.0** | Quản trị Hệ thống | Quản trị viên (Admin) | **U** | — | **U** | **U** | **U** | — | **U** | — | — |
| **12.0** | Xác thực & Phiên | Tất cả 5 tác nhân | **R** | **R** | — | — | — | **C, R** | — | — | — |
| **13.0** | Sao lưu & Phục hồi | Quản trị viên (Admin) | — | — | — | — | — | **C, R** | **R** | **C, R** | **R** |
| **14.0** | Duyệt & Chốt khóa sổ lương | Ban Giám đốc & HR | **R** | — | **R** | **R** | **C, U, R** | **C, R** | **R** | — | **R** |
| **15.0** | Cảnh báo tự động & Thông báo | Tất cả 5 tác nhân | **R** | — | **R** | **R** | **R** | **C, R** | **R** | — | **R** |

> **Ghi chú phân tích ma trận:**
> 1. **Dòng 9.0 ➔ Cột D9 (`C, U, R`):** Tiến trình Quản lý Dự án & Sự kiện toàn quyền tạo mới kế hoạch sự kiện (`du_an`), cập nhật tiến độ, phân công nhân viên (`phan_cong_du_an`) và nghiệm thu số giờ, mức thù lao sự kiện.
> 2. **Dòng 6.0 ➔ Cột D9 (`R`):** Tiến trình Tính Lương & Phiếu Lương PDF đọc trực tiếp các bản ghi phân công dự án mang trạng thái `DA_NGHIEM_THU` trong kỳ lương. Thù lao này được cộng trực tiếp vào thu nhập Gross (Tầng 1 công thức lương) dưới dạng *Khoản phụ cấp / thù lao sự kiện*, đồng thời cập nhật trạng thái phân công sang `DA_KET_CHUYEN_LUONG`.
> 3. **Dòng 14.0 ➔ Cột D9 (`R`):** Tiến trình Duyệt & Chốt khóa sổ lương đọc D9 để đối soát, đảm bảo toàn bộ các sự kiện kết thúc trong tháng đã được nghiệm thu thù lao trước khi tiến hành khóa sổ kỳ lương (`ky_luong.trang_thai = 'KHOA_SO'`).
> 4. **Dòng 10.0 ➔ Cột D9 (`R`):** Tiến trình Báo cáo & Thống kê đọc D9 để kết xuất báo cáo hiệu quả doanh thu - chi phí nhân sự của các sự kiện tiệc cưới và hội nghị lớn cho Ban Giám Đốc.

---

### 2.3. Quy Tắc Phân Tách Quyền Ghi Danh Mục (Xóa Nhất Quán)

Nhằm đảm bảo tính toàn vẹn dữ liệu và triệt tiêu nguy cơ tranh chấp ghi (Race Condition), toàn bộ các bảng danh mục dùng chung được phân quyền sở hữu ghi duy nhất cho tiến trình **11.0 Quản trị Hệ thống**, trong khi các tiến trình nghiệp vụ khác chỉ có quyền đọc:

| Nhóm Danh Mục Hệ Thống | Kho | Chủ Sở Hữu Duy Nhất (Được Quyền Create / Update) | Các Tiến Trình Nghiệp Vụ Chỉ Được Quyền Đọc (Read) |
|:---|:---:|:---|:---|
| Phòng ban (`phong_ban`), Chức vụ (`chuc_vu`) | D1 | **11.0 Quản trị Hệ thống** (Admin) | **1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 9.0, 10.0** đọc để gán nhân sự, hiển thị cơ cấu và đối soát |
| Mẫu ca làm việc 24/7 (`ca_lam_viec`) | D3 | **11.0 Quản trị Hệ thống** (Admin) | **3.0** đọc để xếp ca tuần; **4.0** đọc khung giờ để tính phút trễ/sớm và ca đêm 30% |
| Danh mục loại đơn (`loai_don`) | D4 | **11.0 Quản trị Hệ thống** (Admin) | **5.0** đọc để dựng danh sách 4 loại đơn trực tuyến trên Mobile App và Web Admin |
| Cấu hình tham số lương, thuế, bảo hiểm (`cau_hinh_*`) | D5 | **11.0 Quản trị Hệ thống** (Admin) | **6.0, 7.0, 8.0** đọc để tự động áp dụng công thức tính lương 2 tầng chuẩn xác |
| Danh mục dự án & sự kiện (`du_an`, `phan_cong_du_an`) | D9 | **9.0 Quản lý Dự án & Sự kiện** (Manager/BoD) | **6.0** đọc thù lao đã nghiệm thu để đổ vào lương; **10.0** đọc để lập báo cáo sự kiện |

---

### 2.4. Danh Mục Luồng Nội Bộ Giữa Các Tiến Trình (Internal Data Flows)

Các luồng dữ liệu nội bộ (ký hiệu `G01` đến `G18`) thể hiện sự kết nối dữ liệu liên thông giữa các tiến trình Mức 1. Khi dựng sơ đồ DFD Mức 1 trên draw.io, các luồng này được thể hiện bằng **đường nét đứt có mũi tên** để phân biệt với luồng tương tác tác nhân (đường nét liền):

| Mã Luồng | Từ Tiến Trình | Đến Tiến Trình | Tên Luồng Dữ Liệu Nội Bộ | Nội Dung Dữ Liệu Truyền Tải | Ý Nghĩa Nghiệp Vụ Trong Khách Sạn |
|:---:|:---:|:---:|:---|:---|:---|
| **G01** | **4.0** Chấm công 24/7 | **6.0** Tính Lương & PDF | *Bảng công đã đối soát theo kỳ* | Số ca quẹt thẻ hợp lệ, tổng giờ OT (hệ số 1.5 - 3.0), tổng giờ ca đêm (+30%), phút trễ/sớm, số ca vắng | Đầu vào cơ sở để tính lương ngày công, tiền tăng ca, phụ cấp đêm và phạt vi phạm. |
| **G02** | **1.0** Nhân sự & HĐLĐ | **6.0** Tính Lương & PDF | *Mức lương cơ bản thỏa thuận* | `luong_co_ban` từ hợp đồng lao động đang có hiệu lực (`hop_dong.trang_thai = 'HIEU_LUC'`) | Làm căn cứ pháp lý tính đơn giá ngày công chuẩn (chia cho 26 ngày) và đóng BHXH 10.5%. |
| **G03** | **5.0** Quản lý Đơn từ | **6.0** Tính Lương & PDF | *Ngày nghỉ hưởng lương đã duyệt* | Danh sách đơn `DA_DUYET` có cờ `co_huong_luong = TRUE` nằm trong khoảng thời gian của kỳ lương | Quy đổi ngày nghỉ phép năm, thai sản thành ngày công hợp lệ khi tính lương tháng. |
| **G04** | **7.0** Quản lý Phụ cấp | **6.0** Tính Lương & PDF | *Định mức phụ cấp cố định & chức danh* | Danh mục phụ cấp ăn ca (30.000 VNĐ/công), phụ cấp trách nhiệm, độc hại, đi lại theo chức vụ | Cộng vào Tầng 1 (Gross) của công thức tính lương. |
| **G05** | **8.0** Quản lý Tiền thưởng | **6.0** Tính Lương & PDF | *Danh sách thưởng đã phê duyệt* | Các khoản thưởng doanh thu buồng/F&B, thưởng hiệu quả KPI, thưởng lễ tết đã duyệt | Cộng vào thu nhập chịu thuế của nhân viên trong kỳ lương. |
| **G06** | **9.0** Quản lý Dự án & Sự kiện | **6.0** Tính Lương & PDF | *Kết chuyển phụ cấp dự án đã chốt* | Danh sách nhân viên tham gia sự kiện, số giờ đóng góp, số tiền thù lao/phụ cấp sự kiện đã nghiệm thu (`trang_thai_nghiem_thu = 'DA_NGHIEM_THU'`) | **Bổ sung cốt lõi:** Kết chuyển thù lao dự án/sự kiện vào thu nhập Gross trên phiếu lương nhân viên trước khi khóa sổ. |
| **G07** | **9.0** Quản lý Dự án & Sự kiện | **15.0** Cảnh báo & Thông báo | *Thông báo phân công dự án mới* | Mã dự án, tên sự kiện, thời gian diễn ra, vai trò phân công, mức thù lao dự kiến và danh sách nhân sự tham gia | **Bổ sung cốt lõi:** Đẩy thông báo tức thời (Push Notification) tới Mobile App để nhân viên nắm bắt và xác nhận tham gia sự kiện. |
| **G08** | **6.0** Tính Lương & PDF | **14.0** Duyệt & Chốt khóa sổ | *Dự thảo bảng lương tháng hoàn tất* | Danh sách phiếu lương trạng thái `NHAP` kèm bảng phân rã chi tiết thu nhập, thuế, bảo hiểm | Chuyển bảng lương sang giao diện thẩm định của HR và Ban Giám Đốc. |
| **G09** | **14.0** Duyệt & Chốt khóa sổ | **6.0** Tính Lương & PDF | *Bảng lương đã khóa sổ chính thức* | Trạng thái kỳ lương chuyển `KHOA_SO`, phiếu lương chuyển `DA_DUYET` / `DA_THANH_TOAN` | Cho phép hệ thống phát hành phiếu lương cá nhân và sinh tệp QuestPDF tải về. |
| **G10** | **1.0, 3.0, 4.0, 5.0, 6.0, 9.0** | **10.0** Báo cáo & Thống kê | *Tập dữ liệu nghiệp vụ tổng hợp* | Dữ liệu nhân sự, công, ca trực, đơn từ, chi phí quỹ lương và chi phí dự án/sự kiện | Nguồn cấp cho Dashboard thống kê đa chiều phục vụ lãnh đạo khách sạn. |
| **G11** | **12.0** Xác thực & Phiên | **Tất cả các tiến trình nghiệp vụ** | *Phiên làm việc & Claims quyền RBAC* | JWT token, `ma_tai_khoan`, `ma_vai_tro`, tập mã quyền truy cập hợp lệ | Kiểm soát quyền truy cập tại cổng API và màn hình chức năng. |
| **G12** | **12.0** Xác thực & Phiên | **15.0** Cảnh báo & Thông báo | *Sự kiện an ninh đăng nhập* | Cảnh báo đăng nhập thất bại liên tiếp, đăng nhập từ thiết bị lạ, phiên hết hạn | Đẩy cảnh báo bảo mật tới Quản trị viên và người dùng. |
| **G13** | **13.0** Sao lưu & Phục hồi | **12.0** Xác thực & Phiên | *Trạng thái sau phục hồi dữ liệu* | Thông tin tài khoản được khôi phục, lệnh thu hồi toàn bộ phiên đăng nhập cũ | Đảm bảo tính nhất quán của phiên người dùng sau khi restore CSDL. |
| **G14** | **1.0** Nhân sự & HĐLĐ | **2.0** Tài khoản & RBAC | *HĐLĐ đã ký, kích hoạt cấp tài khoản* | Bản ghi `hop_dong` hiệu lực kèm thông tin lý lịch nhân sự (`nhan_vien`) | Khởi phát quy trình cấp tài khoản đăng nhập cho nhân viên mới. |
| **G15** | **2.0** Tài khoản & RBAC | **12.0** Xác thực & Phiên | *Tài khoản IT vừa cấp hoàn tất* | `ten_dang_nhap`, `mat_khau_bam`, `ma_vai_tro`, `auth.users` Supabase đồng bộ | Cho phép nhân sự mới đăng nhập vào hệ thống Web Admin hoặc Mobile App. |
| **G16** | **1.0** Nhân sự & HĐLĐ | **2.0** *(Chức năng 2.5)* | *Danh sách NV đủ điều kiện cấp tài khoản* | Bộ lọc 3 điều kiện: đang làm việc (`DANG_LAM`), chưa có tài khoản, có email công vụ | Đọc dữ liệu dựng dropdown chọn nhân viên trên màn hình `/YeuCauTaiKhoan`. |
| **G17** | **3.0** Ca làm & Phân ca 24/7 | **4.0** Chấm công 24/7 | *Khung giờ phân ca chuẩn theo ngày* | Danh sách phân ca chi tiết từng nhân viên theo ngày (`ma_phan_ca`, giờ bắt đầu, giờ kết thúc) | Làm mốc chuẩn đối chiếu giờ Check-in/out thực tế, tính phút trễ/sớm và giờ OT. |
| **G18** | **3.0** Ca làm & Phân ca 24/7 | **9.0** Quản lý Dự án & Sự kiện | *Lịch trực ca cố định của nhân viên* | Thời gian phân ca trực thường nhật của nhân sự trong tuần/tháng | Tránh xung đột lịch (Conflict Scheduling) khi điều động nhân sự vào các sự kiện lớn. |

---

### 2.5. Bảng Ánh Xạ Cân Bằng Giữa Mức 0 Và Mức 1

Bảng ánh xạ cân bằng chứng minh tính nhất quán giữa Sơ đồ Ngữ cảnh (Mức 0) và Sơ đồ Phân rã Mức 1. Mọi luồng dữ liệu vào/ra ở Mức 0 đều được tiếp nhận và xử lý đầy đủ bởi các tiến trình tương ứng ở Mức 1 mà không có luồng nào bị bỏ sót:

| Mã Luồng Mức 0 | Tên Luồng Dữ Liệu | Tiến Trình Mức 1 Đảm Nhiệm Xử Lý | Kho Dữ Liệu Mức 1 Liên Quan |
|:---:|:---|:---|:---|
| **F01_IN** | Thông tin cấu hình, RBAC & sao lưu | **2.0** (Tài khoản & RBAC) + **11.0** (Quản trị hệ thống) + **13.0** (Lệnh backup CSDL) | D1, D2, D3, D4, D5, D7, D9 |
| **F01_OUT** | Nhật ký kiểm toán & trạng thái an toàn | **12.0** (Log đăng nhập) + **13.0** (Kết quả backup) + **15.0** (Thông báo quản trị) | D6, D8 |
| **F02_IN** | Phê duyệt định mức quỹ lương & dự án | **8.0** (Định mức thưởng) + **9.0** (Ngân sách sự kiện) + **14.0** (Duyệt bảng lương) | D5, D7, D9 |
| **F02_OUT** | Báo cáo quản trị chiến lược & hiệu quả sự kiện | **10.0** (Báo cáo tổng hợp nhân sự, quỹ lương & hiệu quả chi phí sự kiện) | D1, D3, D4, D5, D7, D9 |
| **F03_IN** | Hồ sơ nhân sự, HĐLĐ, kỳ lương & thù lao sự kiện | **1.0** (Hồ sơ NV, HĐLĐ) + **6.0** (Đưa thù lao sự kiện vào lương) + **7.0** (Phụ cấp) + **14.0** (Thiết lập kỳ lương) | D1, D5, D7, D9 |
| **F03_OUT** | Cảnh báo HĐLĐ, bảng lương & chi phí dự án | **10.0** (Báo cáo nhân sự/chi phí DA) + **14.0** (Bảng lương đã chốt) + **15.0** (Cảnh báo HĐLĐ hết hạn trước 30 ngày) | D1, D5, D6 |
| **F04_IN** | Lịch phân ca, duyệt đơn, kế hoạch sự kiện & phân công DA | **3.0** (Xếp ca 24/7) + **5.0** (Duyệt đơn) + **8.0** (Đề xuất thưởng) + **9.0** (Kế hoạch sự kiện, phân công nhân sự & nghiệm thu DA) | D3, D4, D5, D9 |
| **F04_OUT** | Thông báo đơn từ, báo cáo ca & tiến độ sự kiện | **3.0** (Danh sách trực ca) + **9.0** (Tiến độ sự kiện & nhân sự tham gia) + **10.0** (Báo cáo chuyên cần/vi phạm) + **15.0** (Thông báo đơn mới) | D3, D6, D9 |
| **F05_IN** | Chấm công, đơn từ & xác nhận sự kiện | **4.0** (Quẹt thẻ Check-in/out) + **5.0** (Nộp 4 loại đơn trực tuyến) + **9.0** (Xác nhận tham gia sự kiện/dự án) | D3, D4, D9 |
| **F05_OUT** | Lịch ca, duyệt đơn, thông báo sự kiện & phiếu lương PDF | **3.0** (Lịch ca cá nhân) + **5.0** (Kết quả duyệt đơn) + **6.0** (Phiếu lương PDF QuestPDF) + **9.0** (Thông báo phân công sự kiện & thù lao sự kiện) + **15.0** (Thông báo đẩy) | D3, D4, D5, D6, D8, D9 |

---

### 2.6. Quy Ước Khi Dựng Sơ Đồ Mức 1 Bằng Công Cụ Vẽ

Khi triển khai bản vẽ kỹ thuật trên các công cụ chuyên dụng (draw.io, Microsoft Visio, Enterprise Architect), sinh viên tuân thủ các quy tắc chuẩn hóa hình họa sau:

| Thành Phần Sơ Đồ | Ký Hiệu Chuẩn Hình Họa | Quy Cách Nhãn & Đường Nét | Lưu Ý Kỹ Thuật Khi Trình Bày |
|:---|:---|:---|:---|
| **Tiến trình (Process)** | Hình chữ nhật bo góc mềm | `Mã_Số — Tên Tiến Trình`<br>*(Ví dụ: `6.0 — Tính Lương & Phiếu Lương PDF`)* | Kích thước đồng đều, màu nền trang nhã, phân biệt khối nghiệp vụ (1.0–11.0) và khối kỹ thuật (12.0–15.0). |
| **Kho dữ liệu (Data Store)** | Hai đường kẻ ngang song song (Open rectangle) | `Mã_Kho — Tên Kho Dữ Liệu`<br>*(Ví dụ: `D9 — Kho Dự án & Sự kiện`)* | **Tuyệt đối không vẽ hình hộp kín**. Đặt tại vị trí trung gian giữa các tiến trình đọc/ghi. |
| **Luồng tương tác tác nhân (External Flow)** | Mũi tên nét liền (Solid Arrow) | Nhãn tên luồng in nghiêng kèm mã luồng (`F01` đến `F05`) | Mũi tên một chiều dứt khoát, không dùng mũi tên hai đầu. |
| **Luồng nội bộ (Internal Flow)** | Mũi tên nét đứt (Dashed Arrow) | Nhãn tên luồng kèm mã hiệu nội bộ (`G01` đến `G18`) | Kết nối giữa các tiến trình phụ thuộc, thể hiện sự luân chuyển dữ liệu trung gian. |
| **Thao tác dữ liệu trên kho** | Chữ cái C, U, R đặt cạnh đầu mũi tên | `C` hoặc `U` trên mũi tên đi vào kho; `R` trên mũi tên đi ra khỏi kho | Định danh rõ hành vi tạo mới, cập nhật hay truy vấn dữ liệu. |
| **Bố cục tổng thể** | Phân tầng luồng nghiệp vụ từ trái sang phải | 12.0 (Xác thực) ở cổng vào bên trái, các phân hệ nghiệp vụ ở trung tâm, 10.0 (Báo cáo) và 15.0 (Thông báo) ở cánh phải | Giữ khoảng cách thoáng, các đường luồng hạn chế tối đa việc cắt chéo nhau. |

---

### 2.7. Đối Chiếu Số Lượng Bảng CSDL (Khớp Nối 30 Bảng Supabase)

Bảng đối chiếu khẳng định tính đồng bộ tuyệt đối giữa hồ sơ thiết kế DFD, tài liệu kiến trúc CSDL và mã nguồn hệ thống:

| Nguồn Tài Liệu Đối Chiếu | Số Lượng Khai Báo | Trạng Thái Đồng Bộ |
|:---|:---:|:---|
| **PRD 03 (Tài liệu này, Mục 2.1 & 2.2)** | **9 kho dữ liệu gom từ 30 bảng CSDL** | Hoàn thành khớp nối 100% (25 bảng nền tảng + 2 bảng dự án/sự kiện `du_an`, `phan_cong_du_an` + 3 bảng ca & chấm công `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`). |
| **Mã nguồn thực tế `AppDbContext.cs` & Schema DB** | **30 bảng chuẩn hóa 3NF** | Đã định nghĩa đầy đủ các thực thể và cấu hình quan hệ khóa ngoại trên Supabase Cloud. |
| **PRD 04 (`04_Thiet_Ke_CSDL_17_Bang.md`)** | **30 bảng CSDL Supabase PostgreSQL** | Đã cập nhật từ điển dữ liệu chi tiết cho toàn bộ 30 bảng (bao gồm 2 bảng sự kiện/dự án và 3 bảng ca & chấm công). |
| **PRD 02 (`02_Phan_Tich_Chuc_Nang_BFD.md`)** | **11 phân hệ nghiệp vụ Cấp 1** | Khớp nối đồng bộ với 11 tiến trình nghiệp vụ cốt lõi 1.0 đến 11.0 của DFD Mức 1. |

#### Danh mục chi tiết 30 bảng CSDL Supabase phân bổ theo 9 Kho Dữ Liệu DFD:

| STT | Tên Bảng CSDL (Supabase) | Thuộc Kho DFD | Nhóm Phân Hệ Nghiệp Vụ | Ý Nghĩa Thực Tế Trong Vận Hành Khách Sạn |
|:---:|:---|:---:|:---|:---|
| 1 | `phong_ban` | **D1** | Nhân sự & HĐLĐ | Danh mục cơ cấu phòng ban: Tiền sảnh (FO), Buồng phòng (HK), F&B, Kỹ thuật, An ninh, HR, IT. |
| 2 | `chuc_vu` | **D1** | Nhân sự & HĐLĐ | Danh mục chức danh chuyên môn: Lễ tân, Trưởng ca, Bếp trưởng, Giám sát tầng, Kỹ sư, Nhân viên. |
| 3 | `nhan_vien` | **D1** | Nhân sự & HĐLĐ | Hồ sơ lý lịch nhân sự, thông tin cá nhân, liên hệ, học vấn và trạng thái công tác (`DANG_LAM`, `DA_THOI_VIEC`). |
| 4 | `hop_dong` | **D1** | Nhân sự & HĐLĐ | Hợp đồng lao động pháp lý, lưu thời hạn hiệu lực và mức lương cơ bản thỏa thuận. |
| 5 | `tai_khoan` | **D2** | Tài khoản & Phân quyền | Tài khoản đăng nhập hệ thống, mật khẩu mã hóa BCrypt, khóa đối chiếu 1-1 với nhân viên. |
| 6 | `vai_tro` | **D2** | Tài khoản & Phân quyền | 5 nhóm vai trò chuẩn hóa hệ thống: Admin, HR, Manager, Employee, IT. |
| 7 | `quyen` | **D2** | Tài khoản & Phân quyền | Danh mục 17 mã quyền thao tác màn hình chức năng trên hệ thống Web và Mobile. |
| 8 | `vai_tro_quyen` | **D2** | Tài khoản & Phân quyền | Bảng trung gian giải quyết quan hệ N - N giữa Vai trò và Quyền hạn truy cập. |
| 9 | `yeu_cau_tai_khoan` | **D2** | Tài khoản & Phân quyền | Quy trình phê duyệt tài khoản: HR gửi yêu cầu sau khi ký HĐLĐ ➔ Bộ phận CNTT (IT) tiếp nhận và cấp phát. |
| 10 | `ca_lam_viec` | **D3** | Ca làm & Chấm công | Danh mục 3 ca trực xoay vòng 24/7: Ca Sáng (06:00 - 14:00), Ca Chiều (14:00 - 22:00), Ca Đêm (22:00 - 06:00). |
| 11 | `phan_ca` | **D3** | Ca làm & Chấm công | Bảng lập lịch xếp ca làm việc tuần/tháng cho từng nhân viên theo từng vị trí ca kíp. |
| 12 | `cham_cong` | **D3** | Ca làm & Chấm công | Bản ghi quẹt thẻ Check-in/out di động thực tế, tự động tính số phút trễ, về sớm, giờ OT và ca đêm. |
| 13 | `loai_don` | **D4** | Quản lý Đơn từ | Danh mục 4 loại đơn: Nghỉ phép năm, Nghỉ ốm (hưởng BHXH), Nghỉ thai sản, Đơn xin thôi việc. |
| 14 | `don_tu` | **D4** | Quản lý Đơn từ | Bản ghi đơn từ điện tử, trạng thái xét duyệt (`CHO_DUYET`, `DA_DUYET`, `TU_CHOI`) và lý do. |
| 15 | `ky_luong` | **D5** | Tiền lương & Thu chi | Chu kỳ tính lương theo tháng (Tháng/Năm) và trạng thái khóa sổ (`MO_SO`, `KHOA_SO`). |
| 16 | `cau_hinh_khoan_luong` | **D5** | Tiền lương & Thu chi | Tham số định mức tiền thưởng KPI, thưởng doanh thu, thưởng sự kiện và các khoản phụ cấp cố định. |
| 17 | `cau_hinh_phu_cap` | **D5** | Tiền lương & Thu chi | Tham số phụ cấp ca đêm (+30%), ăn ca (30.000 VNĐ/công), độc hại, đi lại kèm cờ chịu thuế TNCN. |
| 18 | `cau_hinh_khau_tru` | **D5** | Tiền lương & Thu chi | Tham số khấu trừ bảo hiểm bắt buộc (10.5%), giảm trừ gia cảnh thuế TNCN và biểu phí phạt trễ/sớm. |
| 19 | `cau_hinh_tang_ca` | **D5** | Tiền lương & Thu chi | Danh mục hệ số lương tăng ca (OT): ngày thường 1.5, ngày nghỉ tuần 2.0, ngày Lễ/Tết 3.0. |
| 20 | `phieu_luong` | **D5** | Tiền lương & Thu chi | Bảng tổng hợp thu nhập tháng của từng nhân viên: Lương công, OT, Phụ cấp đêm, Thù lao sự kiện, Thưởng, Khấu trừ, Net. |
| 21 | `chi_tiet_phieu_luong` | **D5** | Tiền lương & Thu chi | Chi tiết phân rã từng khoản mục thu nhập và khấu trừ cấu thành phiếu lương. |
| 22 | `chi_tiet_tru_bao_hiem` | **D5** | Tiền lương & Thu chi | Phân rã 3 khoản bảo hiểm trích từ lương người lao động: BHXH (8%), BHYT (1.5%), BHTN (1%). |
| 23 | `chi_tiet_tru_thue_tncn` | **D5** | Tiền lương & Thu chi | Bản ghi tính thuế TNCN theo biểu thuế lũy tiến từng phần sau khi trừ các khoản giảm trừ gia cảnh. |
| 24 | `chi_tiet_tru_vi_pham_tam_ung` | **D5** | Tiền lương & Thu chi | Khấu trừ tiền phạt kỷ luật (đi trễ, về sớm, bỏ ca trực) và các khoản khấu trừ khác (bỏ tạm ứng). |
| 25 | `nhat_ky_quan_tri` | **D6** | Nhật ký & Kiểm toán | Ghi vết kiểm toán quản trị hệ thống: thời điểm, tài khoản thao tác, loại hành động và dữ liệu thay đổi. |
| **26** | **`du_an`** | **D9** | **Quản lý Dự án & Sự kiện** | **Lưu trữ thông tin dự án/sự kiện đặc thù (tiệc cưới gala, hội nghị, lễ hội ẩm thực), ngân sách thù lao, thời gian và quản lý phụ trách.** |
| **27** | **`phan_cong_du_an`** | **D9** | **Quản lý Dự án & Sự kiện** | **Quản lý phân công nhân sự tham gia dự án, vai trò nhiệm vụ, số giờ làm việc, mức thù lao/phụ cấp sự kiện nghiệm thu để đổ vào lương.** |
| **28** | **`dinh_bien_ca`** | **D3** | **Ca làm & Chấm công** | **Định biên số nhân sự tối thiểu cho từng cặp Ca × Chức vụ, làm căn cứ cảnh báo thiếu người trên lịch phân ca.** |
| **29** | **`giai_trinh_cham_cong`** | **D3** | **Ca làm & Chấm công** | **Hồ sơ giải trình sai lệch giờ vào/ra, đi qua hai cấp xác minh: Quản lý bộ phận → HR xác nhận trước khi ghi lại giờ công.** |
| **30** | **`yeu_cau_doi_ca`** | **D3** | **Ca làm & Chấm công** | **Yêu cầu đổi ca trực giữa hai nhân viên, kèm trạng thái duyệt và phản hồi của Quản lý/HR.** |
