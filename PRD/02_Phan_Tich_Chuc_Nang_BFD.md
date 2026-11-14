# PRD 02: Phân Tích Yêu Cầu & Sơ Đồ Phân Rã Chức Năng (BFD)

**Dự án:** Hệ thống Quản lý Nhân sự & Tiền lương Khách sạn The OverTime Hotel  
**Chuyên ngành:** Hệ thống Thông tin Doanh nghiệp (HTTTDN) / Công nghệ Phần mềm  
**Nền tảng công nghệ:** C# .NET 10 (ASP.NET Core MVC & .NET MAUI) · Supabase Cloud Database (PostgreSQL 30 bảng chuẩn 3NF)  
**Tài liệu trực quan kèm theo:** Xem sơ đồ đồ họa tương tác tại `docs project/diagrams/HRM_KhachSan_BFD_IPO.html`

---

## 1. Sơ Đồ Cây Phân Rã Chức Năng BFD Tổng Quan (Level 1 & 2)

Sơ đồ phân rã chức năng nghiệp vụ (Business Function Diagram - BFD) thể hiện cấu trúc phân cấp từ Tiến trình gốc trung tâm (`0.0`) xuống 11 phân hệ nghiệp vụ Cấp 1 và 57 chức năng thành phần Cấp 2:

```text
                                        HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG
                                              THE OVERTIME HOTEL (0.0)
                                                          │
          ┌───────────────────────────────────────────────┴───────────────────────────────────────────────┐
          │                                                                                               │
    [NHÓM VẬN HÀNH NHÂN SỰ & CHẤM CÔNG]                                                [NHÓM ĐÃI NGỘ, DỰ ÁN & HỆ THỐNG]
    (Phân hệ 1.0 -> 6.0)                                                               (Phân hệ 7.0 -> 11.0)
          │                                                                                               │
  ┌───────┼───────┬───────┬───────┬───────┬───────┐                       ┌───────┬───────┬───────┬───────┴───────┐
  │       │       │       │       │       │       │                       │       │       │       │       │       │
┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐                   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐   ┌─▼─┐
│1.0│   │2.0│   │3.0│   │4.0│   │5.0│   │6.0│   │   │                   │7.0│   │8.0│   │9.0│   │10.0   │11.0   │   │
│QL │   │TK │   │Ca │   │Chấm│   │Đơn│   │Tính│   │   │                   │Phụ│   │Tiền   │Dự án  │Báo cáo│Quản   │   │
│NS │   │&  │   │làm│   │công│   │từ │   │lương│  │   │                   │cấp│   │thưởng │& Sự   │& Thống│trị Hệ │   │
│HĐ │   │RBAC│  │247│   │247│   │ĐT │   │& PDF│  │   │                   │   │   │       │kiện   │kê     │thống  │   │
└─┬─┘   └─┬─┘   └─┬─┘   └─┬─┘   └─┬─┘   └─┬─┘   └───┘                   └─┬─┘   └─┬─┘   └─┬─┘   └─┬─┘   └─┬─┘   └───┘
  │       │       │       │       │       │                               │       │       │       │       │
  ├──1.1  ├──2.1  ├──3.1  ├──4.1  ├──5.1  ├──6.1                          ├──7.1  ├──8.1  ├──9.1  ├──10.1 ├──11.1
  │  Mới  │  Login│  Mẫu  │  Quẹt │  Phép │  Mở kỳ                        │  Đêm  │  KPI  │  Khởi │  Biến │  Phòng
  ├──1.2  ├──2.2  ├──3.2  ├──4.2  ├──5.2  ├──6.2                          ├──7.2  ├──8.2  │  tạo  │  động │  ban
  │  Sửa  │  Khóa │  Lập  │  Trễ  │  Ốm   │  Động                         │  Ăn   │  Doanh│  DA   │  NS   ├──11.2
  ├──1.3  ├──2.3  │  lịch │  sớm  │  BHXH │  cơ 2T                        │  ca   │  thu  ├──9.2  ├──10.2 │  Chức
  │  HĐLĐ │  5 vai│├──3.3 ├──4.3  ├──5.3  ├──6.3                          ├──7.3  ├──8.3  │  Task │  Cơ   │  vụ
  ├──1.4  │  trò  │  Nghỉ │  Giờ  │  Thai │  Khóa                         │  Trách│  Lễ   │  Force│  cấu  ├──11.3
  │  Lọc  ├──2.4  │  >=8h │  OT   │  sản  │  sổ                           │  nhiệm│  Tết  ├──9.3  ├──10.3 │  Giờ ca
  └──1.5  │  RBAC ├──3.4  ├──4.4  ├──5.4  ├──6.4                          ├──7.4  ├──8.4  │  App  │  Học  ├──11.4
     Điều └──2.5  │  Đổi  │  Tổng │  Thôi │  Xem                          │  Độc  │  Duyệt│  Noti │  vấn  │  Tham
     chuyển  Cấp  │  ca   │  hợp  │  việc │  App                          │  hại  │  thưởng├──9.4 ├──10.4 │  số
             mới  └──3.5  └──4.5  └──5.5  ├──6.5                          └──7.5  └──8.5  │  Nghiệm│  Quỹ  └──11.5
             TK      Định    Giải    Phân │  PDF                             Lịch    Phân │  thu  │  lương    Backup
                     biên    trình   cấp  └──6.6                             sử      bổ   └──9.5  ├──10.5     Restore
                     24/7    công    duyệt   Thuế                                    quỹ     Kết  │  Chuyên
                                             năm                                             chuyển  cần
                                                                                             lương├──10.6
                                                                                                     Báo cáo
                                                                                                     sự kiện
```

---

## 2. Bảng Tổng Hợp 11 Phân Hệ Nghiệp Vụ Cấp 1

| Mã TT | Tên Phân Hệ Cấp 1 | Số Lượng Cấp 2 | Vai Trò Chịu Trách Nhiệm | Phạm Vi Nghiệp Vụ Trong Khách Sạn |
|:---:|:---|:---:|:---|:---|
| **1.0** | Quản lý Nhân sự & HĐLĐ | 5 chức năng | Phòng Hành chính - Nhân sự (HR) | Hồ sơ lý lịch, ký kết và theo dõi hạn HĐLĐ, bổ nhiệm và điều chuyển công tác. |
| **2.0** | Tài khoản & Phân quyền (RBAC) | 5 chức năng | Quản trị viên hệ thống (Admin) + Bộ phận CNTT (IT) | Xác thực danh tính đa nền tảng, quản lý bảo mật tài khoản, phân nhóm 5 vai trò, quy trình HR yêu cầu -> IT cấp tài khoản Mobile. |
| **3.0** | Ca làm & Lịch phân ca 24/7 | 5 chức năng | Quản lý bộ phận (Manager) & Phòng HR | Quản lý mẫu ca 24/7, lập lịch phân ca tuần/tháng, kiểm soát khoảng nghỉ >= 8h, quản lý đổi ca và cảnh báo thiếu hụt định biên vận hành. |
| **4.0** | Bảng Chấm công 24/7 & Chuyên cần | 5 chức năng | Nhân viên, Quản lý bộ phận & Phòng HR | Quẹt thẻ di động GPS + Wi-Fi 2 lớp, tự động tính phút trễ/sớm, tính giờ OT làm tròn xuống bước 0.5h, tổng hợp công tháng và giải trình công. |
| **5.0** | Quản lý Đơn từ điện tử | 5 chức năng | Toàn bộ Nhân viên, Quản lý bộ phận & HR | Tạo và xử lý nộp trực tuyến 4 loại đơn: Phép năm, Nghỉ ốm BHXH, Thai sản, Thôi việc; phân luồng phê duyệt cấp Quản lý / HR. |
| **6.0** | Tính toán Lương tự động hàng tháng & Phiếu lương PDF | 6 chức năng | Phòng Hành chính - Nhân sự (HR) & Kế toán | Mở kỳ lương, đối soát công, động cơ tính lương 2 tầng Gross -> Net (Điều 98 BLLĐ, BHXH 10.5%), khóa sổ kỳ lương, xem phiếu lương App, xuất PDF chuẩn QuestPDF (lộ trình phát triển) và quyết toán thuế năm. |
| **7.0** | Quản lý Phụ cấp | 5 chức năng | Phòng Hành chính - Nhân sự & Kế toán | Quản lý định mức phụ cấp ca đêm 30%, ăn ca 30k/ngày, phụ cấp trách nhiệm, phụ cấp độc hại/đồng phục và lịch sử điều chỉnh định mức. |
| **8.0** | Quản lý Tiền thưởng | 5 chức năng | Ban Giám đốc, Phòng HR & Quản lý bộ phận | Quản lý thưởng hiệu quả KPI, thưởng doanh thu buồng phòng/tiệc, thưởng Lễ Tết chuyên cần, phê duyệt danh sách và báo cáo phân bổ quỹ thưởng. |
| **9.0** | Quản lý Dự án & Sự kiện Khách sạn (Event & Project Management) | 5 chức năng | Ban Giám đốc, Quản lý Task Force & Phòng HR | Khởi tạo sự kiện/dự án, điều động nhân lực liên phòng ban (Task Force), thông báo điều động realtime lên Mobile App, nghiệm thu đánh giá và kết chuyển phụ cấp dự án vào kỳ lương tháng (Gross). |
| **10.0** | Báo cáo & Thống kê Quản trị | 6 chức năng | Ban Giám đốc, Phòng HR & Quản lý | Thống kê biến động nhân sự (Turnover Rate), cơ cấu bộ phận, học vấn thâm niên, đối soát ngân sách quỹ lương, chuyên cần vi phạm và báo cáo hiệu quả nhân sự sự kiện. |
| **11.0** | Quản trị Hệ thống | 5 chức năng | Quản trị viên hệ thống (Admin) | Danh mục cơ cấu tổ chức (phòng ban, chức vụ), cấu hình khung giờ ca 24/7 (độc quyền Admin), thiết lập tham số tỷ lệ định mức, sao lưu và phục hồi CSDL Supabase. |

**Tổng quy mô chức năng hệ thống:** 11 Phân hệ Cấp 1, 57 Chức năng Cấp 2, đáp ứng toàn diện chu trình quản trị nhân sự khách sạn 5 sao.

---

## 3. Đặc Tả Chi Tiết 57 Chức Năng Cấp 2 Theo Mô Hình IPO (Input – Process – Output)

---

### Phân Hệ 1.0: Quản Lý Nhân Sự & Hợp Đồng Lao Động

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Lý lịch nhân sự: Họ tên, ngày sinh, giới tính, SĐT, email công vụ, địa chỉ, trình độ học vấn│
│ • Vị trí công tác: Mã phòng ban (ma_phong_ban), Mã chức vụ (ma_chuc_vu), Ngày vào làm         │
│ • Dữ liệu hợp đồng: Số HĐLĐ, loại HĐ (thử việc/xác định/vô thời hạn), ngày bắt đầu/kết thúc   │
│ • Thỏa thuận tài chính: Mức lương cơ bản pháp lý (> 0), chế độ phụ cấp cam kết                │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Validate định dạng Email (RFC 5322), SĐT (10 số) và kiểm tra tính duy nhất trong hệ thống   │
│ 2. Kiểm tra độ tuổi lao động: Tuổi = Năm hiện tại - Năm sinh >= 18 tuổi                       │
│ 3. Khởi tạo mã nhân viên tự động; gán trạng thái công tác mặc định: DANG_LAM                  │
│ 4. Kiểm tra hợp đồng: Ngày kết thúc > Ngày bắt đầu; Cập nhật hợp đồng cũ về HET_HAN           │
│ 5. Quét cảnh báo hợp đồng tự động: Lọc danh sách HĐ có (ngay_ket_thuc - CURRENT_DATE) <= 30   │
│ 6. Điều chuyển bổ nhiệm: Kiểm tra trạng thái DANG_LAM, cập nhật phòng ban/chức vụ mới         │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bản ghi nhân sự mới trong nhan_vien với mã định danh ma_nhan_vien duy nhất                  │
│ • Bản ghi hợp đồng lao động mới trong hop_dong với trạng thái HIEU_LUC                        │
│ • Danh sách cảnh báo hợp đồng sắp hết hạn (<= 30 ngày) trên Dashboard của Phòng HR            │
│ • Lịch sử điều chuyển phòng ban, chức vụ được lưu vết đầy đủ                                  │
│ • Dữ liệu lưu trữ đồng bộ tại: nhan_vien, hop_dong, phong_ban, chuc_vu                        │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 1.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **1.1** | **Thêm hồ sơ nhân viên mới** | HR | • Họ tên, ngày sinh, giới tính<br>• SĐT, email công vụ, địa chỉ, học vấn<br>• Phòng ban, chức vụ, ngày vào làm | 1. Validate email RFC 5322 & SĐT 10 chữ số.<br>2. Kiểm tra độ tuổi lao động >= 18 tuổi.<br>3. Kiểm tra tính duy nhất của email và SĐT.<br>4. Tạo bản ghi mới, gán trạng thái `DANG_LAM`. | • Bản ghi mới sinh tự động `ma_nhan_vien`.<br>• Thông báo thêm thành công trên Web Admin.<br>• Điều hướng gợi ý tạo HĐLĐ.<br>CSDL: `nhan_vien`, `phong_ban`, `chuc_vu` |
| **1.2** | **Sửa, cập nhật lý lịch** | HR | • `ma_nhan_vien`<br>• Các trường thông tin cần sửa đổi<br>• Trạng thái (`DANG_LAM`, `NGHI_PHEP`, `DA_THOI_VIEC`) | 1. Xác minh `ma_nhan_vien` tồn tại.<br>2. Kiểm tra không trùng SĐT/email với nhân sự khác.<br>3. Kiểm tra ràng buộc khi chuyển sang `DA_THOI_VIEC`.<br>4. Cập nhật dữ liệu vào CSDL. | • Hồ sơ nhân sự hiển thị thông tin cập nhật.<br>• Huy hiệu trạng thái đổi màu trực quan.<br>CSDL: `nhan_vien` |
| **1.3** | **Quản lý & Cảnh báo HĐLĐ** | HR | • `ma_nhan_vien`, `so_hop_dong`<br>• Loại HĐ, ngày bắt đầu/kết thúc<br>• Lương cơ bản thỏa thuận | 1. Kiểm tra tính duy nhất của `so_hop_dong`.<br>2. Kiểm tra: Ngày kết thúc > Ngày bắt đầu.<br>3. Ràng buộc `luong_co_ban > 0`.<br>4. Chuyển HĐ cũ sang `HET_HAN`.<br>5. Quét cảnh báo: HĐ có thời hạn <= 30 ngày. | • Bản ghi mới trong `hop_dong` (`HIEU_LUC`).<br>• Danh sách cảnh báo tái ký trên Dashboard HR.<br>CSDL: `hop_dong`, `nhan_vien` |
| **1.4** | **Tìm kiếm, tra cứu & Lọc đa tiêu chí** | HR / Mgr / Admin | • Từ khóa (Tên, Mã NV, SĐT, Email)<br>• Bộ lọc: Phòng ban, Chức vụ, Trạng thái<br>• Tham số phân trang (page, pageSize) | 1. Phân quyền: Manager chỉ xem bộ phận mình; HR/Admin xem toàn khách sạn.<br>2. Tạo câu truy vấn động LINQ/SQL kết hợp nhiều điều kiện.<br>3. Đếm tổng bản ghi và thực thi phân trang tối ưu. | • Danh sách nhân sự thỏa điều kiện lọc.<br>• Thanh phân trang và nhãn tổng số kết quả.<br>CSDL: `nhan_vien`, `phong_ban`, `chuc_vu` |
| **1.5** | **Bổ nhiệm, điều chuyển** | HR | • `ma_nhan_vien`<br>• `ma_phong_ban_moi`, `ma_chuc_vu_moi`<br>• Quyết định điều chuyển, ngày hiệu lực | 1. Kiểm tra nhân viên đang ở trạng thái `DANG_LAM`.<br>2. Kiểm tra vị trí mới hợp lệ và khác vị trí hiện tại.<br>3. Cập nhật mã phòng ban và chức vụ mới vào hồ sơ.<br>4. Ghi nhận lịch sử điều động công tác. | • Hồ sơ nhân sự hiển thị chức vụ, phòng ban mới.<br>• Thông báo quyết định bổ nhiệm thành công.<br>CSDL: `nhan_vien`, `phong_ban`, `chuc_vu` |

---

### Phân Hệ 2.0: Tài Khoản & Phân Quyền RBAC

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Tên đăng nhập (ten_dang_nhap), Mật khẩu đăng nhập (mat_khau)                                │
│ • Mã tài khoản (ma_tai_khoan), Mật khẩu mới khi cấp lại                                       │
│ • Danh mục 5 vai trò: Admin (1), HR (2), Manager (3), Employee (4), IT (5)                    │
│ • Danh sách 17 mã quyền (ma_quyen) từ ma trận phân quyền                                      │
│ • Yêu cầu tài khoản: ma_nhan_vien, ma_hop_dong, email công vụ, vai trò đề xuất                │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Xác thực đăng nhập: Kiểm tra tài khoản tồn tại và trạng thái HoatDong                      │
│ 2. Đối soát mật khẩu băm an toàn (PBKDF2/BCrypt) với mat_khau_bam trong tai_khoan             │
│ 3. Nạp vai trò & quyền: Đọc từ vai_tro, vai_tro_quyen, quyen để thiết lập Claims              │
│ 4. Khởi tạo phiên: Cấp Authentication Cookie (Web Admin) hoặc JWT Token (Mobile App)          │
│ 5. Quản trị tài khoản: Khóa/Mở, reset mật khẩu, chặn Super Admin tự khóa chính mình           │
│ 6. Quy trình cấp tài khoản (2.5): HR gửi yêu cầu (CHO_IT) -> IT nhận việc (DANG_XU_LY) ->    │
│    Tạo user Supabase Auth + ghi tai_khoan -> Chốt DA_CAP hoặc TU_CHOI kèm lý do               │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Phiên làm việc xác thực bảo mật, điều hướng vào Dashboard theo đúng thẩm quyền              │
│ • Trạng thái tài khoản được cập nhật (HoatDong / BiKhoa)                                      │
│ • Ma trận phân quyền RBAC được cập nhật tức thì trên toàn hệ thống                            │
│ • Tài khoản nhân viên mới được cấp phát đầy đủ trên Web Admin và Supabase Auth Mobile         │
│ • Dữ liệu lưu trữ tại: tai_khoan, vai_tro, quyen, vai_tro_quyen, yeu_cau_tai_khoan, nhat_ky_quan_tri │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 2.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **2.1** | **Đăng nhập / Đăng xuất** | Toàn bộ | • `ten_dang_nhap`, `mat_khau`<br>• Nền tảng (Web Admin / Mobile App) | 1. Tìm tài khoản; nếu `trang_thai = 'BiKhoa'` thì chặn.<br>2. So khớp mật khẩu băm PBKDF2/BCrypt.<br>3. Nạp danh sách quyền từ `vai_tro_quyen`.<br>4. Tạo Auth Cookie (Web) hoặc JWT Session (Mobile).<br>5. Đăng xuất: Xóa Cookie / hủy phiên làm việc. | • Đăng nhập thành công: Chuyển hướng Dashboard phù hợp.<br>• Thất bại: Thông báo lỗi tài khoản hoặc mật khẩu.<br>CSDL: `tai_khoan`, `vai_tro_quyen`, `quyen` |
| **2.2** | **Quản lý tài khoản (Khóa/Mở/Xóa)** | IT | • `ma_tai_khoan`<br>• Thao tác: khóa, mở, sửa hoặc xóa<br>• Lý do thao tác | 1. Khóa/mở thay đổi trạng thái tài khoản và ghi nhật ký.<br>2. Xóa yêu cầu xác nhận và lý do; chặn tự xóa, xóa Admin và Trưởng bộ phận IT.<br>3. Đối soát Supabase Auth theo email công vụ, khóa tài khoản ứng dụng rồi xóa tài khoản xác thực (nếu có) và tài khoản ứng dụng.<br>4. Nếu chưa hoàn tất, thông báo lỗi; tài khoản đã khóa có thể được thử xóa lại. | • Tài khoản bị xóa không còn dùng để đăng nhập; hồ sơ nhân viên, hợp đồng và lịch sử làm việc được giữ.<br>• Lưu lịch sử yêu cầu cấp tài khoản và ghi nhật ký xóa.<br>CSDL: `tai_khoan`, `yeu_cau_tai_khoan`, `nhat_ky_quan_tri`; xác thực: Supabase Auth. |
| **2.3** | **Phân 5 vai trò chuẩn** | Admin | • `ma_tai_khoan`<br>• `ma_vai_tro` mới chỉ định (Admin, HR, Manager, Employee, IT) | 1. Kiểm tra tài khoản và vai trò hợp lệ trong CSDL.<br>2. Kiểm tra ràng buộc: Luôn duy trì tối thiểu 01 Admin.<br>3. Cập nhật `ma_vai_tro` vào bảng `tai_khoan`. | • Quyền hạn người dùng cập nhật ở phiên đăng nhập kế tiếp.<br>• Cập nhật vai trò hiển thị trên bảng quản lý.<br>CSDL: `tai_khoan`, `vai_tro` |
| **2.4** | **Phân quyền động RBAC** | Admin | • `ma_vai_tro`<br>• Danh sách `ma_quyen` được chọn trong 17 mã quyền | 1. Bắt đầu Transaction trong CSDL.<br>2. Xóa các quyền cũ của vai trò trong `vai_tro_quyen`.<br>3. Chèn hàng loạt các quyền mới được tích chọn.<br>4. Làm mới bộ đệm phân quyền (Refresh Cache). | • Bảng liên kết `vai_tro_quyen` được cập nhật đồng bộ.<br>• Các tài khoản mang vai trò lập tức nhận quyền mới.<br>CSDL: `vai_tro_quyen`, `quyen`, `vai_tro` |
| **2.5** | **Yêu cầu & cấp tài khoản mới** *(Đã hoàn thành CSDL, Core, Controller, View trên Web Admin)* | HR (gửi)<br>Admin + IT (cấp) | • `ma_nhan_vien` (chưa có tài khoản, đang `DANG_LAM`, có email công vụ)<br>• `ten_dang_nhap`, `ma_vai_tro`<br>• `ma_hop_dong` làm căn cứ<br>• Mật khẩu ban đầu do IT nhập | 1. Phía HR: Lọc nhân sự đủ điều kiện, tạo yêu cầu `CHO_IT`. Chặn HR chọn vai trò Admin.<br>2. Phía IT: Nhận việc (`CHO_IT` -> `DANG_XU_LY`).<br>3. Transaction ghi `tai_khoan` -> Gọi Supabase Admin API tạo user `auth.users`.<br>4. Chốt trạng thái `DA_CAP` hoặc `TU_CHOI` (kèm `ghi_chu_tu_choi`). | • Hộp thư yêu cầu cấp tài khoản trên Web Admin (`/YeuCauTaiKhoan`).<br>• Nhân sự đăng nhập được Mobile App bằng email công vụ.<br>• Lưu vết kiểm toán toàn diện.<br>CSDL: `yeu_cau_tai_khoan`, `tai_khoan`, `nhan_vien`, `hop_dong`, `vai_tro`, `nhat_ky_quan_tri` |

---

### Phân Hệ 3.0: Ca Làm & Lịch Phân Ca 24/7

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Danh mục 3 mẫu ca chuẩn 24/7: Ca Sáng (6h-14h), Ca Chiều (14h-22h), Ca Đêm (22h-6h)         │
│ • Dữ liệu xếp ca: Mã nhân viên (ma_nhan_vien), Mã ca (ma_ca), Ngày làm việc (ngay_lam_viec)   │
│ • Yêu cầu đổi ca: Mã phân ca gốc, Mã ca chuyển đổi, Nhân viên đổi chéo, Lý do                 │
│ • Định biên ca trực tối thiểu: Số lượng nhân sự yêu cầu cho từng vị trí trọng yếu              │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Xếp ca: Kiểm tra Manager chỉ xếp bộ phận mình; Chặn xếp 2 ca chồng chéo cùng ngày          │
│ 2. Kiểm tra ràng buộc khoảng nghỉ: Thời gian nghỉ giữa 2 ca liên tiếp phải >= 8 tiếng         │
│ 3. Quản lý đổi ca: Đối soát tính hợp lệ của ca đổi, kiểm tra không vi phạm khoảng nghỉ,       │
│    chuyển trạng thái yêu cầu đổi ca chờ Manager bộ phận phê duyệt                             │
│ 4. Giám sát định biên: So sánh tổng số nhân sự được xếp ca với định biên tối thiểu quy định,  │
│    tự động cảnh báo màu đỏ nếu ca trực bị thiếu nhân sự vận hành an toàn                      │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bảng lịch phân ca tuần/tháng dạng ma trận trực quan trên Web Admin                          │
│ • Lịch ca làm việc cá nhân hiển thị trên ứng dụng Mobile App của nhân viên                    │
│ • Lịch sử hoán đổi ca làm việc được lưu vết kiểm toán                                         │
│ • Báo cáo và cảnh báo thiếu định biên ca trực 24/7 theo thời gian thực                        │
│ • Dữ liệu lưu trữ tại: ca_lam_viec, phan_ca, nhan_vien                                        │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 3.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **3.1** | **Quản lý danh mục mẫu ca** | Admin (cấu hình)<br>HR (vận hành) | • Tên ca: Sáng, Chiều, Đêm<br>• `gio_bat_dau`, `gio_ket_thuc`<br>• Hệ số lương, cờ `qua_dem` | 1. Chuẩn hóa 3 khung giờ ca liên tục 24/7 (mỗi ca 8 tiếng).<br>2. Bật cờ `qua_dem = TRUE` cho ca Đêm 22h - 6h.<br>3. Chặn xóa danh mục ca nếu đã phát sinh dữ liệu xếp ca.<br>4. Quyền sở hữu dữ liệu ghi cấu hình thuộc về Admin (tại 11.3). | • Danh mục ca làm việc chuẩn hóa trong CSDL.<br>• Khung giờ sẵn sàng cho lập lịch xếp ca.<br>CSDL: `ca_lam_viec` |
| **3.2** | **Lập lịch xếp ca tuần/tháng** | Manager (Bộ phận)<br>HR (Toàn KS) | • `ma_nhan_vien`, `ma_ca`<br>• `ngay_lam_viec`, ghi chú phân ca | 1. Manager chỉ được phân ca cho nhân sự thuộc bộ phận mình.<br>2. Chặn xếp 2 ca chồng chéo cho 1 nhân sự trong cùng 1 ngày.<br>3. Kiểm tra tính hợp lệ và ghi nhận các bản ghi phân ca vào CSDL. | • Bảng lịch ca tuần dạng lưới trên Web Admin.<br>• Lịch ca cá nhân hiển thị trên Mobile App.<br>CSDL: `phan_ca`, `ca_lam_viec`, `nhan_vien` |
| **3.3** | **Ràng buộc khoảng nghỉ >= 8h** | Hệ thống tự động | • Ca làm việc vừa kết thúc<br>• Ca làm việc dự kiến xếp tiếp theo | 1. Tính khoảng cách thời gian giữa `gio_ket_thuc` của ca trước và `gio_bat_dau` của ca sau.<br>2. Nếu khoảng nghỉ < 8 tiếng: Chặn thao tác xếp ca và báo lỗi vi phạm quy định an toàn lao động (Điều 110 BLLĐ). | • Thông báo chặn xếp ca kèm cảnh báo vi phạm khoảng nghỉ.<br>• Đảm bảo sức khỏe và an toàn cho nhân viên trực 24/7.<br>CSDL: `phan_ca`, `ca_lam_viec` |
| **3.4** | **Quản lý điều chuyển & Đổi ca** | Employee (yêu cầu)<br>Manager (duyệt) | • `ma_phan_ca` cần đổi<br>• Nhân viên đổi chéo hoặc ca muốn chuyển<br>• Lý do đổi ca | 1. Kiểm tra tính hợp lệ của ca đổi (không trùng lịch, đảm bảo khoảng nghỉ >= 8h cho cả 2 nhân viên).<br>2. Tạo bản ghi yêu cầu đổi ca.<br>3. Manager bộ phận xem xét và phê duyệt trực tuyến.<br>4. Cập nhật lại lịch phân ca sau khi duyệt. | • Lịch phân ca được cập nhật lại tương ứng.<br>• Thông báo kết quả đổi ca gửi về Mobile App.<br>CSDL: `phan_ca`, `nhan_vien` |
| **3.5** | **Cảnh báo thiếu định biên ca** | Hệ thống tự động | • Lịch phân ca đã lập<br>• Định biên nhân sự tối thiểu theo quy định | 1. Quét toàn bộ lịch trực trong ngày theo từng khung ca và vị trí.<br>2. So sánh: Nếu Số nhân sự được xếp < Định biên tối thiểu (ví dụ: Lễ tân < 2 người, An ninh < 2 người, Buồng phòng < 3 người).<br>3. Kích hoạt cảnh báo màu đỏ trên Dashboard Manager và HR. | • Danh sách ca trực thiếu hụt nhân sự theo thời gian thực.<br>• Nhắc nhở Manager bộ phận kịp thời bổ sung nhân sự.<br>CSDL: `phan_ca`, `ca_lam_viec`, `phong_ban` |

---

### Phân Hệ 4.0: Bảng Chấm Công 24/7 & Chuyên Cần

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Dữ liệu quẹt thẻ di động: ma_phan_ca, Thời gian thực tế NOW()                               │
│ • Dữ liệu kiểm thực vị trí 2 lớp: Tọa độ GPS (toa_do_checkin), BSSID/Tên Wi-Fi nội bộ          │
│ • Thông tin thiết bị: thiet_bi_checkin (Device ID, OS)                                        │
│ • Dữ liệu giải trình: Mã chấm công cần giải trình, lý do (quên thẻ, sự cố), minh chứng kèm theo│
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Xác thực vị trí 2 lớp (Hybrid): GPS trong bán kính <= 100m HOẶC kết nối Wi-Fi OverTimeHotel │
│ 2. Check-in: Tra cứu ma_phan_ca; Ghi nhận gio_check_in, phuong_thuc, toa_do, thiet_bi         │
│ 3. Tính trễ tự động: Nếu gio_check_in > gio_bat_dau -> phut_di_tre = gio_check_in - gio_bat_dau│
│ 4. Check-out: Ghi nhận gio_check_out; tính phút về sớm nếu gio_check_out < gio_ket_thuc       │
│ 5. Tính giờ làm thêm (OT): Nếu làm thêm >= 30 phút, tính giờ OT và LÀM TRÒN XUỐNG bước 0.5h   │
│ 6. Giải trình chấm công: Tiếp nhận đơn giải trình, Quản lý/HR phê duyệt điều chỉnh công       │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bản ghi cham_cong hoàn chỉnh với đầy đủ giờ vào/ra, phút trễ/sớm, giờ OT và bằng chứng GPS  │
│ • Thông báo quẹt thẻ thành công gửi tức thì về giao diện Mobile App của nhân viên             │
│ • Bảng tổng hợp công tháng hiển thị số ca chuẩn, công thực tế, giờ OT và tỷ lệ chuyên cần     │
│ • Bản ghi giải trình công được cập nhật trạng thái phê duyệt minh bạch                        │
│ • Dữ liệu lưu trữ tại: cham_cong, phan_ca, ca_lam_viec                                        │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 4.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **4.1** | **Quẹt thẻ Check-in / Check-out GPS + Wi-Fi** | Employee (Mobile) | • `ma_phan_ca` từ lịch phân ca<br>• Thời gian thực tế `NOW()`<br>• Tọa độ GPS (`toa_do_checkin`)<br>• Tên Wi-Fi, Device ID | 1. Xác thực vị trí 2 lớp: Check GPS <= 100m hoặc kết nối Wi-Fi `OverTimeHotel_Staff`.<br>2. Check-in: Tạo bản ghi `cham_cong` mới (`gio_check_in`, `phuong_thuc_checkin`, `toa_do_checkin`).<br>3. Check-out: Tìm bản ghi đang mở -> Cập nhật `gio_check_out`. | • Bản ghi chấm công lưu kèm bằng chứng kiểm toán vị trí.<br>• Thông báo UI: *"Quẹt thẻ thành công lúc HH:mm [GPS/Wi-Fi]"*.<br>• Chặn quẹt thẻ nếu ngoài phạm vi khách sạn.<br>CSDL: `cham_cong`, `phan_ca` |
| **4.2** | **Tự động tính trễ, sớm** | Hệ thống tự động | • `gio_check_in`, `gio_check_out`<br>• Khung giờ ca chuẩn từ `ca_lam_viec` | 1. Nếu `gio_check_in > gio_bat_dau`: Tính `phut_di_tre` -> Gán `trang_thai = 'DI_TRE'`.<br>2. Nếu `gio_check_out < gio_ket_thuc`: Tính `phut_ve_som` -> Gán `trang_thai = 'VE_SOM'`. | • Các cột `phut_di_tre`, `phut_ve_som` được tự động điền.<br>• Cập nhật số liệu giám sát chuyên cần tức thì.<br>CSDL: `cham_cong`, `ca_lam_viec` |
| **4.3** | **Tính giờ OT làm tròn xuống bước 0.5h** | Hệ thống tự động | • `gio_check_out` thực tế<br>• `gio_ket_thuc` quy định của ca | 1. Kiểm tra làm thêm sau giờ ca vượt ngưỡng >= 30 phút.<br>2. Tính thời gian làm thêm = (gio_check_out - gio_ket_thuc).<br>3. Quy đổi và **LÀM TRÒN XUỐNG** theo bước 0.5 giờ: 45 phút -> 0.5h; 90 phút -> 1.5h; 105 phút -> 1.5h; 120 phút -> 2.0h (phần dư dưới 30 phút bị bỏ, không làm tròn lên để tránh chi trả vượt giờ thực tế). | • Cột `gio_tang_ca_ot` trong `cham_cong` được lưu dữ liệu.<br>• Sẵn sàng làm căn cứ tính tiền OT trong kỳ lương.<br>CSDL: `cham_cong` |
| **4.4** | **Bảng tổng hợp công tháng** | HR / Manager | • `ma_nhan_vien` (hoặc phòng ban)<br>• Tháng/Năm đối soát công | 1. Tổng hợp dữ liệu từ `cham_cong` trong tháng: Tổng ca hợp lệ, tổng số phút trễ/sớm, tổng giờ OT.<br>2. Tính tỷ lệ chuyên cần = (Số ca đúng giờ / Tổng số ca phân công) * 100%. | • Bảng tổng hợp công tháng chi tiết từng nhân viên.<br>• Xuất dữ liệu đối soát phục vụ tính lương tháng.<br>CSDL: `cham_cong`, `phan_ca`, `nhan_vien` |
| **4.5** | **Giải trình chấm công** | Employee (gửi)<br>Manager/HR (duyệt) | • `ma_cham_cong` (hoặc ngày bị thiếu)<br>• Lý do giải trình (quên thẻ, sự cố kỹ thuật, công tác đột xuất)<br>• Minh chứng đính kèm | 1. Nhân viên gửi đơn giải trình kèm giờ vào/ra đề xuất.<br>2. Quản lý trực tiếp kiểm tra và phê duyệt ý kiến.<br>3. Phòng HR xác nhận -> Hệ thống điều chỉnh lại dữ liệu công hợp lệ và lưu vết giải trình. | • Bản ghi công được cập nhật lại trạng thái hợp lệ.<br>• Thông báo kết quả giải trình gửi về Mobile App.<br>CSDL: `cham_cong`, `nhan_vien` |

---

### Phân Hệ 5.0: Quản Lý Đơn Từ Điện Tử

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Loại đơn điện tử: Nghỉ phép năm (1), Nghỉ ốm BHXH (2), Nghỉ thai sản (3), Đơn thôi việc (4) │
│ • Thời gian xin nghỉ: Ngày bắt đầu (tu_ngay), Ngày kết thúc (den_ngay), Lý do                 │
│ • Tài liệu minh chứng: Giấy chứng nhận nghỉ hưởng BHXH, Giấy khám thai/chứng sinh            │
│ • Thao tác phê duyệt: Quyết định (DA_DUYET / TU_CHOI), Lý do từ chối (bắt buộc khi từ chối)   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Validate: Kiểm tra thời gian nghỉ trong tương lai; kiểm tra số ngày phép năm còn lại       │
│ 2. Đơn thôi việc: Kiểm tra thời hạn báo trước theo Điều 35 BLLĐ (45/30/7/3 ngày theo loại HĐ) │
│ 3. Tạo đơn: Khởi tạo bản ghi don_tu mang trạng thái CHO_DUYET; gửi Push Notification          │
│ 4. Phân cấp thẩm quyền: Manager duyệt nghỉ phép/ốm; HR duyệt chế độ thai sản và thôi việc     │
│ 5. Xét duyệt: Cập nhật DA_DUYET/TU_CHOI; duyệt phép năm thì ghi nhận công nghỉ phép có lương  │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bản ghi đơn từ được lưu và cập nhật trạng thái trong bảng don_tu                            │
│ • Thông báo kết quả xét duyệt gửi tức thì về ứng dụng Mobile App của nhân viên                │
│ • Tự động trừ số ngày phép trong quỹ phép năm và cộng ngày công có lương cho kỳ tính lương    │
│ • Dữ liệu lưu trữ tại: don_tu, loai_don, nhan_vien, hop_dong                                  │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 5.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **5.1** | **Đơn xin nghỉ phép năm** | Employee (Mobile) | • `ma_loai_don = 1` (Phép năm, có hưởng lương)<br>• `tu_ngay`, `den_ngay`<br>• `ly_do` xin nghỉ | 1. Validate `tu_ngay >= CURRENT_DATE`.<br>2. Tính tổng số ngày xin nghỉ.<br>3. Kiểm tra số ngày phép năm còn lại (tiêu chuẩn 12 ngày/năm).<br>4. Tạo bản ghi `don_tu` trạng thái `CHO_DUYET`. | • Đơn lưu vào `don_tu` (trạng thái `CHO_DUYET`).<br>• Gửi thông báo đến Quản lý bộ phận trực tiếp.<br>CSDL: `don_tu`, `loai_don`, `nhan_vien` |
| **5.2** | **Đơn nghỉ ốm (hưởng BHXH)** | Employee (Mobile) | • `ma_loai_don = 2` (Nghỉ ốm)<br>• `tu_ngay`, `den_ngay`, `ly_do`<br>• Ảnh chụp giấy chứng nhận y tế | 1. Kiểm tra thời gian nghỉ hợp lệ.<br>2. Kiểm tra có file đính kèm chứng từ y tế hợp lệ.<br>3. Tạo bản ghi `don_tu` trạng thái `CHO_DUYET`.<br>4. Chuyển tiếp đơn đến HR tiếp nhận hồ sơ hưởng chế độ BHXH. | • Đơn lưu vào bảng `don_tu`.<br>• HR tiếp nhận hồ sơ y tế giải quyết trợ cấp BHXH.<br>CSDL: `don_tu`, `loai_don` |
| **5.3** | **Đơn nghỉ chế độ thai sản** | Employee (Mobile) | • `ma_loai_don = 3` (Thai sản)<br>• Ngày bắt đầu nghỉ, thời gian (6 tháng)<br>• Giấy khám thai / Giấy chứng sinh | 1. Validate thời gian nghỉ theo luật lao động (06 tháng).<br>2. Kiểm tra tính hợp lệ của giấy tờ y tế đính kèm.<br>3. Tạo bản ghi `don_tu` trạng thái `CHO_DUYET`.<br>4. Phân luồng chuyển thẳng cấp duyệt lên Phòng HR. | • Đơn thai sản lưu trong `don_tu`.<br>• HR nhận thông báo tiếp nhận hồ sơ thai sản.<br>CSDL: `don_tu`, `loai_don` |
| **5.4** | **Đơn xin thôi việc / chấm dứt HĐ** | Employee (Mobile) | • `ma_loai_don = 4` (Thôi việc)<br>• Ngày thôi việc dự kiến<br>• Lý do & Cam kết bàn giao | 1. Tra cứu loại HĐLĐ của nhân viên trong bảng `hop_dong`.<br>2. Kiểm tra thời hạn báo trước theo Điều 35 BLLĐ: 45 ngày (HĐ không xác định thời hạn); 30 ngày (HĐ xác định thời hạn từ 12-36 tháng); 7 ngày (HĐ dưới 6 tháng); 3 ngày (HĐ dưới 3 tháng).<br>3. Tạo bản ghi `don_tu` trạng thái `CHO_DUYET`. | • Đơn thôi việc lưu vào bảng `don_tu`.<br>• Cảnh báo ưu tiên gửi Manager & Trưởng phòng HR.<br>CSDL: `don_tu`, `loai_don`, `hop_dong` |
| **5.5** | **Xét duyệt phân cấp Quản lý / HR** | Manager (Phép/Ốm)<br>HR (Thai sản/Thôi việc) | • `ma_don`<br>• Quyết định: `DA_DUYET` hoặc `TU_CHOI`<br>• `phan_hoi_duyet` (bắt buộc khi từ chối) | 1. Kiểm tra thẩm quyền duyệt của vai trò.<br>2. Nếu từ chối: Bắt buộc nhập lý do giải thích.<br>3. Cập nhật `trang_thai`, `ngay_duyet`, `ma_nguoi_duyet`.<br>4. Nếu duyệt phép năm: Ghi nhận công phép hưởng lương. | • Trạng thái đơn chuyển `DA_DUYET` hoặc `TU_CHOI`.<br>• Thông báo kết quả gửi tức thời về Mobile App NV.<br>CSDL: `don_tu`, `nhan_vien`, `phan_ca` |

---

### Phân Hệ 6.0: Tính Toán Lương Tự Động Hàng Tháng & Phiếu Lương PDF

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Mã kỳ lương tháng cần tính (ma_ky_luong - Ví dụ: Tháng 03/2026)                             │
│ • Mức lương cơ bản thỏa thuận pháp lý (luong_co_ban) từ hợp đồng lao động (hop_dong)         │
│ • Dữ liệu bảng công (cham_cong): Số ca hợp lệ, Giờ OT, Số ca làm đêm 22h-6h                    │
│ • Đơn xin nghỉ phép năm đã được duyệt (don_tu) để cộng ngày công hưởng lương                  │
│ • Khoản phụ cấp dự án đã nghiệm thu từ phan_cong_du_an (kết chuyển sang Gross)                │
│ • Bảng tham số định mức: Ăn ca (30k/ngày), Tỷ lệ BH 10.5%, Biểu thuế TNCN lũy tiến             │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] ĐỘNG CƠ TÍNH LƯƠNG 2 TẦNG (GROSS -> NET)                                            │
│ 1. TẦNG 1: TỔNG THU NHẬP (GROSS)                                                              │
│    • Lương ngày công = (Lương CB / 26) * Số ngày công thực tế (gồm cả ngày phép năm duyệt)    │
│    • Tiền OT = (Lương CB / 208) * Số giờ OT * Hệ số (1.5 ngày thường, 2.0 cuối tuần, 3.0 Lễ)  │
│    • Phụ cấp ca đêm = (Lương CB / 208) * Số giờ làm ca đêm * 0.30 (Điều 98 BLLĐ)              │
│    • Phụ cấp ăn ca = Số ngày công có mặt * 30.000 VNĐ                                         │
│    • Phụ cấp dự án/sự kiện = Tổng phụ cấp nghiệm thu từ phan_cong_du_an                       │
│    • Tiền thưởng hiệu quả (KPI, doanh thu buồng phòng, Lễ Tết)                                │
│ 2. TẦNG 2: TỔNG KHẤU TRỪ                                                                      │
│    • Bảo hiểm bắt buộc = Lương CB * 10.5% (BHXH 8%, BHYT 1.5%, BHTN 1%)                       │
│    • Tiền phạt kỷ luật = Tổng tiền phạt đi trễ, về sớm, vắng không phép                       │
│    • Thuế TNCN lũy tiến 5 bậc sau khi giảm trừ gia cảnh                                       │
│ 3. LƯƠNG THỰC NHẬN (NET) = GROSS - TỔNG KHẤU TRỪ                                              │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bản ghi hoàn chỉnh trong phieu_luong: tien_luong_cong, tien_tang_ca, tong_phu_cap, net...    │
│ • Phân rã chi tiết từng dòng thu chi trong chi_tiet_phieu_luong                               │
│ • Phiếu lương điện tử trực quan trên Mobile App cho từng nhân viên                            │
│ • File PDF phiếu lương chuẩn nhận diện thương hiệu (QuestPDF - đang trong lộ trình phát triển)│
│ • Báo cáo quyết toán thuế TNCN cả năm                                                         │
│ • Dữ liệu lưu trữ tại: ky_luong, phieu_luong, chi_tiet_phieu_luong, cau_hinh_khoan_luong      │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 6.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **6.1** | **Mở kỳ lương & đối soát công** | HR | • Tháng/Năm tính lương<br>• Dữ liệu bảng chấm công tháng | 1. Khởi tạo bản ghi `ky_luong` mới (trạng thái `MO`).<br>2. Đối soát dữ liệu bảng công của toàn bộ nhân viên.<br>3. Kiểm tra các trường hợp giải trình chưa xử lý để chốt số liệu công. | • Kỳ lương được kích hoạt sẵn sàng tính toán.<br>• Bảng kiểm tra dữ liệu công trước khi chạy lương.<br>CSDL: `ky_luong`, `cham_cong` |
| **6.2** | **Động cơ tính lương 2 tầng Gross -> Net** | HR / Hệ thống | • `ma_ky_luong`<br>• Lương CB theo HĐLĐ<br>• Công chuẩn, OT, ca đêm<br>• Phụ cấp dự án đã nghiệm thu<br>• Thưởng KPI, ăn ca | 1. Tính Gross: Lương công + Tiền OT + Phụ cấp ca đêm 30% + Phụ cấp ăn ca + Phụ cấp dự án/sự kiện (kết chuyển từ 9.5) + Tiền thưởng.<br>2. Tính Khấu trừ: BHXH 10.5% + Phạt vi phạm + Thuế TNCN.<br>3. Tính Net = Gross - Khấu trừ.<br>4. Lưu các dòng phân rã chi tiết. | • Bản ghi hoàn chỉnh trong `phieu_luong` (trạng thái `NHAP`).<br>• Danh sách chi tiết các dòng thu chi trong `chi_tiet_phieu_luong`.<br>CSDL: `phieu_luong`, `chi_tiet_phieu_luong`, `ky_luong`, `phan_cong_du_an` |
| **6.3** | **Phê duyệt & Khóa sổ kỳ lương** | HR / Ban Giám đốc | • Bảng tổng hợp lương tháng<br>• Thao tác: Duyệt / Khóa sổ | 1. HR đối soát tổng thể và trình Ban Giám đốc phê duyệt.<br>2. Chuyển trạng thái `phieu_luong` sang `DA_DUYET`.<br>3. Khóa sổ `ky_luong` sang `KHOA`: Ngăn chặn mọi sửa đổi hồi tố dữ liệu công và lương của kỳ này. | • Kỳ lương chính thức khóa sổ an toàn.<br>• Mở quyền phát hành phiếu lương cho nhân viên.<br>CSDL: `ky_luong`, `phieu_luong` |
| **6.4** | **Xem phiếu lương trên Mobile** | Employee (Mobile) | • `ma_nhan_vien`<br>• `ma_ky_luong` đã duyệt | 1. Xác thực danh tính nhân viên qua phiên đăng nhập Mobile.<br>2. Phân quyền: Nhân viên chỉ được xem phiếu lương của chính mình.<br>3. Hiển thị 3 khối: Tổng quan Lương Net, Chi tiết Gross, Chi tiết Khấu trừ. | • Giao diện phiếu lương điện tử trực quan, sang trọng trên Mobile App.<br>CSDL: `phieu_luong`, `chi_tiet_phieu_luong`, `ky_luong` |
| **6.5** | **Xuất file PDF phiếu lương chuẩn QuestPDF** *(Lộ trình phát triển)* | HR / Employee | • `ma_phieu_luong`<br>• Template nhận diện 5★ | 1. Nạp hồ sơ nhân viên, chức danh, phòng ban và các dòng chi tiết lương.<br>2. Áp dụng template QuestPDF Engine (chuẩn layout A4/A5, bảng kê chi tiết thu nhập - khấu trừ, số tiền bằng chữ).<br>3. Render file PDF vector sắc nét phục vụ in ấn hoặc lưu trữ. *(Ghi chú: QuestPDF hiện đang trong lộ trình phát triển, sẽ được đóng gói NuGet vào solution).* | • File `PhieuLuong_[MaNV]_[Thang].pdf`.<br>• Hỗ trợ tải về máy hoặc in ấn trực tiếp.<br>CSDL: `phieu_luong`, `chi_tiet_phieu_luong`, `nhan_vien` |
| **6.6** | **Quyết toán thuế năm** | HR / Employee | • `ma_nhan_vien`, Năm tài chính quyết toán | 1. Tập hợp đủ 12 kỳ lương trong năm tài chính của nhân viên.<br>2. Lũy kế Tổng thu nhập chịu thuế, tổng trừ BHXH và thuế TNCN đã tạm khấu trừ.<br>3. Lập bảng kê khai thu nhập và chứng từ khấu trừ thuế TNCN cả năm. | • Văn bản chứng từ quyết toán thuế TNCN cả năm phục vụ cơ quan Thuế và người lao động.<br>CSDL: `phieu_luong`, `ky_luong`, `nhan_vien` |

---

### Phân Hệ 7.0: Quản Lý Phụ Cấp

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Danh mục các loại phụ cấp: Phụ cấp ca đêm 30%, Phụ cấp ăn ca, Phụ cấp trách nhiệm,         │
│   Phụ cấp độc hại & phụ cấp đồng phục, phụ cấp công việc đặc thù                              │
│ • Định mức chi trả: Tỷ lệ phần trăm hoặc số tiền cố định (VNĐ/ngày hoặc VNĐ/tháng)            │
│ • Điều kiện áp dụng: Chức danh công việc, môi trường làm việc, số ca làm việc thực tế         │
│ • Quyết định điều chỉnh định mức phụ cấp kèm ngày hiệu lực áp dụng                            │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Thiết lập định mức phụ cấp ca đêm: 30% lương giờ chuẩn theo Điều 98 BLLĐ                   │
│ 2. Cấu hình phụ cấp ăn ca: 30.000 VNĐ cho mỗi ngày công có mặt thực tế tại khách sạn          │
│ 3. Thiết lập phụ cấp trách nhiệm: Áp dụng cho cấp Trưởng ca, Giám sát, Trưởng bộ phận         │
│ 4. Thiết lập phụ cấp độc hại/đồng phục: Áp dụng cho vị trí giặt ủi hóa chất, bảo trì máy lạnh │
│ 5. Lưu vết lịch sử điều chỉnh: Ghi nhận ngày thay đổi, mức cũ, mức mới, người phê duyệt       │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bảng danh mục định mức phụ cấp hoàn chỉnh trong cau_hinh_phu_cap                            │
│ • Dữ liệu định mức tự động kết nối vào công thức tính lương của Phân hệ 6.0                   │
│ • Báo cáo lịch sử biến động định mức phụ cấp phục vụ thanh tra tài chính                       │
│ • Dữ liệu lưu trữ tại: cau_hinh_phu_cap, cau_hinh_khoan_luong, nhat_ky_quan_tri               │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 7.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **7.1** | **Quản lý phụ cấp ca đêm 30%** | HR / Kế toán | • Cấu hình tỷ lệ 30%<br>• Căn cứ pháp lý Điều 98 BLLĐ | 1. Chuẩn hóa công thức: (Lương CB / 208) * Số giờ ca đêm * 30%.<br>2. Đồng bộ tham số vào động cơ tính lương tự động.<br>3. Kiểm tra tính tuân thủ pháp luật lao động. | • Tham số phụ cấp ca đêm được kích hoạt đồng bộ.<br>CSDL: `cau_hinh_phu_cap`, `ca_lam_viec` |
| **7.2** | **Phụ cấp ăn ca 30k/ngày** | HR / Kế toán | • Định mức 30.000 VNĐ/ngày<br>• Điều kiện: Có mặt làm việc | 1. Xác định mức phụ cấp ăn ca tính trên ngày công thực tế.<br>2. Cấu hình cờ miễn thuế TNCN cho khoản phụ cấp ăn ca theo quy định hiện hành.<br>3. Cập nhật vào cấu hình khoản lương. | • Định mức phụ cấp ăn ca được lưu trong CSDL.<br>CSDL: `cau_hinh_phu_cap`, `cau_hinh_khoan_luong` |
| **7.3** | **Phụ cấp trách nhiệm** | HR / Kế toán | • Danh mục chức vụ hưởng phụ cấp<br>• Mức phụ cấp theo cấp bậc | 1. Xác định danh sách chức danh được hưởng phụ cấp trách nhiệm (Duty Manager, Bếp trưởng, Giám sát buồng...).<br>2. Thiết lập số tiền phụ cấp trách nhiệm hàng tháng.<br>3. Tự động cộng vào bảng lương theo chức vụ. | • Bảng định mức phụ cấp trách nhiệm theo vị trí công tác.<br>CSDL: `cau_hinh_phu_cap`, `chuc_vu` |
| **7.4** | **Phụ cấp độc hại / đồng phục** | HR / Kế toán | • Vị trí độc hại (giặt là đồ vải, xử lý hóa chất bể bơi, kỹ thuật điện lạnh)<br>• Định mức phụ cấp | 1. Xác định danh mục công việc nặng nhọc, độc hại trong khách sạn.<br>2. Thiết lập định mức trợ cấp độc hại và chi phí hỗ trợ giặt ủi đồng phục nhân viên.<br>3. Phê duyệt và áp dụng cho các nhân sự thuộc diện hưởng. | • Danh sách nhân sự và mức hưởng phụ cấp độc hại/đồng phục.<br>CSDL: `cau_hinh_phu_cap`, `nhan_vien` |
| **7.5** | **Lịch sử điều chỉnh phụ cấp** | HR / Admin | • Quyết định thay đổi định mức<br>• Mức phụ cấp mới, ngày áp dụng | 1. Ghi nhận giao dịch điều chỉnh định mức phụ cấp trong CSDL.<br>2. Lưu giữ giá trị cũ, giá trị mới, lý do điều chỉnh và người thực hiện.<br>3. Xuất bảng kê lịch sử khi có yêu cầu kiểm toán nội bộ. | • Bảng nhật ký biến động các khoản phụ cấp của khách sạn.<br>CSDL: `nhat_ky_quan_tri`, `cau_hinh_phu_cap` |

---

### Phân Hệ 8.0: Quản Lý Tiền Thưởng

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Danh mục các loại tiền thưởng: Thưởng KPI, Thưởng doanh thu buồng phòng/tiệc cưới,          │
│   Thưởng Lễ Tết, Thưởng chuyên cần tháng                                                      │
│ • Tiêu chí đánh giá: Điểm đánh giá KPI, doanh thu vượt chỉ tiêu, số ngày công chuẩn           │
│ • Ngân sách quỹ thưởng được Ban Giám đốc phê duyệt cho từng kỳ                                │
│ • Danh sách nhân sự được đề xuất khen thưởng kèm mức tiền thưởng dự kiến                      │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ                                                                     │
│ 1. Tính toán thưởng KPI: Áp dụng hệ số đánh giá tháng (Loại A: 100%, Loại B: 80%, Loại C: 50%) │
│ 2. Phân bổ thưởng doanh thu: Trích tỷ lệ % doanh thu tiệc cưới/buồng phòng chia cho nhân sự   │
│ 3. Tính thưởng chuyên cần: Thưởng cố định cho nhân viên đi làm đủ 26 công không trễ/sớm      │
│ 4. Xét duyệt danh sách thưởng: Trưởng bộ phận lập danh sách -> HR kiểm soát ngân sách ->      │
│    Ban Giám đốc phê duyệt chính thức                                                          │
│ 5. Kết chuyển dữ liệu thưởng vào bảng lương tháng tương ứng                                   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Quyết định khen thưởng và danh sách chi trả tiền thưởng đã được phê duyệt                   │
│ • Khoản tiền thưởng được tự động đưa vào thu nhập Gross của kỳ tính lương tháng               │
│ • Báo cáo phân bổ quỹ tiền thưởng theo từng phòng ban và khối vận hành                        │
│ • Dữ liệu lưu trữ tại: cau_hinh_khoan_luong, chi_tiet_phieu_luong, ky_luong                   │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 8.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **8.1** | **Thưởng hiệu quả KPI** | HR / Manager | • Bảng điểm đánh giá KPI tháng<br>• Khung tiền thưởng theo xếp loại | 1. Thu thập kết quả đánh giá KPI của nhân viên trong tháng.<br>2. Phân loại xếp hạng: Xuất sắc (Loại A), Tốt (Loại B), Đạt (Loại C).<br>3. Tính số tiền thưởng KPI tương ứng với từng mức xếp loại. | • Danh sách tiền thưởng KPI được lập tự động.<br>CSDL: `cau_hinh_khoan_luong`, `chi_tiet_phieu_luong` |
| **8.2** | **Thưởng doanh thu buồng & tiệc** | BoD / Kế toán | • Doanh thu dịch vụ buồng phòng<br>• Doanh thu tiệc cưới, hội nghị<br>• Tỷ lệ trích thưởng dịch vụ (Service Charge) | 1. Xác định doanh thu thực tế vượt định mức kế hoạch.<br>2. Trích quỹ thưởng theo tỷ lệ % quy định của khách sạn.<br>3. Phân bổ công bằng cho các bộ phận tham gia trực tiếp (F&B, Buồng phòng, Bếp, Lễ tân). | • Bảng phân bổ tiền thưởng doanh thu theo bộ phận.<br>CSDL: `cau_hinh_khoan_luong`, `phong_ban` |
| **8.3** | **Thưởng Lễ, Tết & Chuyên cần** | HR / BoD | • Dịp khen thưởng (30/4, Quốc khánh 2/9, Tết Dương/Âm lịch)<br>• Tiêu chí chuyên cần (đủ 26 công, 0 trễ/sớm) | 1. Thiết lập chính sách thưởng Lễ/Tết cho nhân sự toàn khách sạn.<br>2. Rà soát danh sách nhân sự đạt chuẩn chuyên cần tháng.<br>3. Tổng hợp mức thưởng theo thâm niên và đóng góp. | • Danh sách chi thưởng Lễ, Tết và chuyên cần toàn khách sạn.<br>CSDL: `cau_hinh_khoan_luong`, `cham_cong` |
| **8.4** | **Phê duyệt danh sách thưởng** | Ban Giám đốc | • Tờ trình đề xuất khen thưởng<br>• Tổng số tiền và danh sách nhân sự | 1. Ban Giám đốc xem xét đề xuất thưởng từ Phòng HR và các bộ phận.<br>2. Đối soát với hạn mức quỹ thưởng kế hoạch.<br>3. Ký duyệt trực tuyến: Chuyển trạng thái `DA_DUYET`. | • Quyết định khen thưởng chính thức có hiệu lực.<br>• Cho phép kết chuyển vào kỳ lương tháng.<br>CSDL: `cau_hinh_khoan_luong`, `ky_luong` |
| **8.5** | **Báo cáo phân bổ quỹ thưởng** | BoD / Kế toán | • Khoảng thời gian (Tháng, Quý, Năm)<br>• Tiêu chí phân nhóm bộ phận | 1. Thống kê tổng chi tiền thưởng trong kỳ báo cáo.<br>2. Phân rã theo loại thưởng (KPI, Doanh thu, Lễ Tết, Chuyên cần).<br>3. So sánh tỷ trọng chi thưởng giữa các phòng ban. | • Biểu đồ và bảng thống kê phân bổ quỹ thưởng khách sạn.<br>CSDL: `chi_tiet_phieu_luong`, `ky_luong` |

---

### Phân Hệ 9.0: Quản Lý Dự Án & Sự Kiện Khách Sạn (Event & Project Management)

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Thông tin dự án/sự kiện: Tên sự kiện (Hội nghị Quốc tế, Tiệc cưới VIP, Gala Dinner, MICE...) │
│ • Địa điểm tổ chức: Grand Ballroom, Trung tâm Hội nghị, Hồ bơi ngoài trời, Nhà hàng Á-Âu       │
│ • Khung thời gian: Ngày bắt đầu (tu_ngay), Ngày kết thúc (den_ngay), Số giờ dự kiến            │
│ • Ngân sách phụ cấp dự án (ngan_sach_phu_cap): Tổng kinh phí bồi dưỡng cho nhân lực tham gia   │
│ • Danh sách Task Force: Trưởng nhóm điều phối (Team Lead), Kỹ thuật viên AV, Nhân viên phục vụ │
│   tiệc (Banquet Staff), Lễ tân đón khách VIP, An ninh kiểm soát trật tự                       │
│ • Tiêu chí nghiệm thu: Tỷ lệ hoàn thành công việc, số giờ công tham gia, đánh giá chất lượng   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH QUẢN TRỊ DỰ ÁN & ĐIỀU ĐỘNG NHÂN LỰC LIÊN PHÒNG BAN                        │
│ 1. Khởi tạo dự án: Khai báo thông tin sự kiện, xác định quy mô khách, đăng ký địa điểm và     │
│    thiết lập hạn mức ngân sách phụ cấp dự án (Lưu vào bảng du_an)                             │
│ 2. Thành lập Task Force: Điều động nhân sự từ nhiều phòng ban khác nhau (F&B, HK, FO, IT, Kỹ  │
│    thuật, An ninh); Phân bổ vai trò nhiệm vụ cụ thể và định mức phụ cấp cho từng vị trí       │
│    (Lưu vào bảng phan_cong_du_an)                                                             │
│ 3. Thông báo tức thời (Realtime Push): Hệ thống tự động gửi thông báo điều động dự án đến     │
│    ứng dụng Mobile App của từng nhân viên được phân công; nhân viên xác nhận sẵn sàng         │
│ 4. Nghiệm thu & Đánh giá: Sau khi sự kiện kết thúc, Trưởng nhóm và HR đánh giá mức độ hoàn    │
│    thành (XUAT_SAC, DAT, KHONG_DAT), xác nhận số giờ công thực tế và chốt mức phụ cấp dự án   │
│ 5. Kết chuyển tự động vào lương: Hệ thống quét các bản ghi phan_cong_du_an đã nghiệm thu,     │
│    tự động tạo các dòng thu nhập phụ cấp dự án trong chi_tiet_phieu_luong của kỳ tính lương   │
│    hiện hành (tính vào Gross), cập nhật trạng thái chi trả DA_KET_CHUYEN_LUONG                │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Bản ghi dự án hoàn chỉnh trong du_an với trạng thái: KHOI_TAO -> DANG_TRIEN_KHAI ->         │
│   CHO_NGHIEM_THU -> HOAN_THANH                                                               │
│ • Danh sách điều động nhân sự chi tiết trong phan_cong_du_an                                  │
│ • Thông báo phân công nhiệm vụ và lịch trình sự kiện hiển thị trên Mobile App nhân viên       │
│ • Biên bản đánh giá nghiệm thu và bảng quyết toán kinh phí phụ cấp dự án                      │
│ • Các dòng phụ cấp dự án được kết chuyển tự động vào bảng chi_tiet_phieu_luong của kỳ lương  │
│ • Dữ liệu lưu trữ tại: du_an, phan_cong_du_an, nhan_vien, chi_tiet_phieu_luong, ky_luong      │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 9.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **9.1** | **Khởi tạo & Lập kế hoạch dự án/sự kiện** | Manager / HR / BoD | • `ten_du_an` (Tiệc cưới VIP, Hội nghị MICE...)<br>• `loai_du_an`, `dia_diem`<br>• `ngay_bat_dau`, `ngay_ket_thuc`<br>• `ngan_sach_phu_cap`<br>• `ma_nguoi_quan_ly` chỉ định | 1. Validate thời gian: `ngay_ket_thuc > ngay_bat_dau`.<br>2. Kiểm tra không xung đột lịch sử dụng địa điểm sảnh tiệc/hội nghị.<br>3. Kiểm tra hạn mức ngân sách phụ cấp dự án hợp lệ.<br>4. Tạo bản ghi mới trong bảng `du_an` với trạng thái `LEN_KE_HOACH`. | • Bản ghi dự án mới được tạo trong `du_an`.<br>• Dashboard hiển thị sự kiện sắp diễn ra kèm tiến độ chuẩn bị.<br>CSDL: `du_an`, `nhan_vien` |
| **9.2** | **Phân công & Điều động Task Force** | Manager (Trưởng nhóm)<br>HR (Giám sát) | • `ma_du_an`<br>• Danh sách `ma_nhan_vien` được điều động<br>• `vai_tro_du_an` (`DIEU_PHOI`, `KY_THUAT_AM_THANH_AS`, `PHUC_VU_TIEC`, `LE_TAN_DON_KHACH`, `AN_NINH_VIP`...)<br>• `nhiem_vu_cu_the`<br>• `so_tien_phu_cap` dự kiến | 1. Cho phép lựa chọn nhân sự liên phòng ban (F&B, Buồng phòng, Tiền sảnh, Kỹ thuật, Bảo vệ).<br>2. Đối soát lịch phân ca thông thường: Đảm bảo nhân sự không bị trùng ca trực chính hoặc đã có sắp xếp người thay thế.<br>3. Phân bổ vai trò nhiệm vụ cụ thể và gán định mức phụ cấp `so_tien_phu_cap` theo tính chất công việc.<br>4. Lưu các bản ghi vào `phan_cong_du_an`, chuyển trạng thái dự án sang `DANG_DIEN_RA`. | • Danh sách đội phản ứng nhanh (Task Force) tham gia sự kiện.<br>• Bản ghi phân công được lưu trong `phan_cong_du_an`.<br>CSDL: `phan_cong_du_an`, `du_an`, `nhan_vien`, `phan_ca` |
| **9.3** | **Thông báo phân công dự án lên Mobile** | Hệ thống tự động | • Dữ liệu bản ghi phân công vừa tạo từ `phan_cong_du_an`<br>• Thiết bị Mobile của nhân viên | 1. Kích hoạt dịch vụ thông báo đẩy (Push Notification) đến thiết bị di động của nhân viên được điều động.<br>2. Nội dung thông báo hiển thị: Tên sự kiện, địa điểm, thời gian tập trung, vai trò phụ trách và mức phụ cấp được hưởng.<br>3. Nhân viên xem chi tiết sự kiện trên Mobile App và bấm xác nhận tiếp nhận nhiệm vụ. | • Thông báo hiển thị tức thì trên Mobile App nhân viên.<br>• Trạng thái tiếp nhận nhiệm vụ được ghi nhận.<br>CSDL: `phan_cong_du_an`, `nhan_vien` |
| **9.4** | **Nghiệm thu & Quyết toán phụ cấp dự án** | Manager (Trưởng nhóm)<br>HR (Phê duyệt) | • `ma_du_an`<br>• Đánh giá chất lượng (`XUAT_SAC`, `HOAN_THANH`, `KHONG_THAM_GIA`)<br>• `so_tien_phu_cap` chốt quyết toán | 1. Khi sự kiện kết thúc, Quản lý cập nhật đánh giá mức độ đóng góp của từng thành viên (`danh_gia`).<br>2. Xác định và chốt số tiền thù lao thực tế: `so_tien_phu_cap`.<br>3. HR kiểm tra đối soát không vượt ngân sách `ngan_sach_phu_cap`.<br>4. Chốt nghiệm thu: Cập nhật `trang_thai_chi_tra = 'CHUA_CHI_TRA'`, ghi nhận `ngay_danh_gia` và đổi trạng thái dự án sang `HOAN_THANH`. | • Biên bản nghiệm thu dự án hoàn thành.<br>• Danh sách quyết toán phụ cấp dự án sẵn sàng chuyển sang kỳ lương.<br>CSDL: `du_an`, `phan_cong_du_an` |
| **9.5** | **Tự động kết chuyển phụ cấp vào kỳ lương** | Hệ thống tự động / HR | • `ma_ky_luong` đang mở (trạng thái `MO` hoặc `DANG_TINH`)<br>• Danh sách phân công có `trang_thai_chi_tra = 'CHUA_CHI_TRA'` và `danh_gia` hợp lệ | 1. Quét toàn bộ các bản ghi `phan_cong_du_an` đã nghiệm thu trong tháng tính lương.<br>2. Khởi tạo tự động dòng chi tiết trong `chi_tiet_phieu_luong` với loại khoản phụ cấp dự án/sự kiện, số tiền tương ứng và nội dung diễn giải: *"Thù lao sự kiện: [Tên dự án] - [Vai trò]".*<br>3. Cộng trực tiếp vào Tổng thu nhập Gross của nhân viên.<br>4. Cập nhật `trang_thai_chi_tra = 'DA_KET_CHUYEN_LUONG'` và liên kết khóa ngoại `ma_chi_tiet` để đảm bảo không bị kết chuyển trùng lặp. | • Dòng phụ cấp dự án xuất hiện trong phiếu lương tháng của nhân viên.<br>• Đảm bảo tính toán minh bạch, chính xác 100% không sót kinh phí.<br>CSDL: `phan_cong_du_an`, `chi_tiet_phieu_luong`, `phieu_luong`, `ky_luong` |

---

### Phân Hệ 10.0: Báo Cáo & Thống Kê Quản Trị

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Khoảng thời gian báo cáo: Theo Tháng, theo Quý, hoặc theo Năm tài chính                     │
│ • Phạm vi thống kê: Toàn bộ khách sạn hoặc phân rã theo từng Phòng ban/Khối nghiệp vụ         │
│ • Định mức ngân sách quỹ lương kế hoạch được Ban Giám đốc phê duyệt từ đầu kỳ                 │
│ • Dữ liệu tổng hợp từ 30 bảng CSDL: Hồ sơ nhân sự, bảng chấm công, phiếu lương, dự án sự kiện │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ PHÂN TÍCH & TRỰC QUAN HÓA SỐ LIỆU                                   │
│ 1. Biến động nhân sự: Tỷ lệ thôi việc = (Số nhân sự thôi việc / Tổng NS bình quân) * 100%     │
│ 2. Cơ cấu nhân lực: Gom nhóm GROUP BY phòng ban/chức vụ -> Tính tỷ trọng % quy mô quân số     │
│ 3. Học vấn & thâm niên: Phân tầng trình độ đào tạo; Thâm niên = CURRENT_DATE - ngay_vao_lam   │
│ 4. Đối soát quỹ lương: So sánh Tổng quỹ lương thực chi với Ngân sách; Tính tỷ lệ giải ngân    │
│ 5. Chuyên cần & kỷ luật: Tính tỷ lệ đúng giờ; Xếp hạng top vi phạm đi trễ, về sớm             │
│ 6. Hiệu quả dự án/sự kiện: Đánh giá chi phí nhân sự dự án, tỷ lệ giải ngân phụ cấp sự kiện   │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Biểu đồ Donut cơ cấu nhân sự & Biểu đồ cột so sánh Ngân sách vs Thực tế                     │
│ • Báo cáo biến động tăng giảm nhân sự hỗ trợ kế hoạch tuyển dụng                              │
│ • Cảnh báo màu đỏ trực quan khi quỹ lương thực tế vượt quá hạn mức ngân sách                  │
│ • Báo cáo đánh giá hiệu quả chi phí và điều động nhân sự dự án/sự kiện                        │
│ • Xuất báo cáo định dạng Excel / PDF phục vụ báo cáo Ban Giám đốc                             │
│ • Dữ liệu lưu trữ tại: nhan_vien, hop_dong, phong_ban, cham_cong, phieu_luong, du_an          │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 10.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **10.1** | **Báo cáo biến động nhân sự** | BoD / HR | • Khoảng thời gian (Tháng/Quý/Năm)<br>• Bộ lọc phòng ban | 1. Thống kê số lượng nhân sự đầu kỳ.<br>2. Đếm số nhân sự tuyển mới trong kỳ.<br>3. Đếm số nhân sự thôi việc trong kỳ.<br>4. Tính tỷ lệ thôi việc (Turnover Rate). | • Biểu đồ đường biến động nhân lực.<br>• Bảng số liệu chi tiết tuyển/nghỉ việc.<br>CSDL: `nhan_vien`, `hop_dong`, `phong_ban` |
| **10.2** | **Báo cáo cơ cấu nhân lực** | BoD / HR | • Tiêu chí gom nhóm: Phòng ban hoặc Chức danh | 1. Thực thi `GROUP BY` phòng ban và chức danh.<br>2. Tính tỷ lệ % quân số từng bộ phận trên tổng quy mô khách sạn. | • Biểu đồ Donut cơ cấu nhân sự.<br>• Bảng thống kê định biên nhân sự các bộ phận.<br>CSDL: `nhan_vien`, `phong_ban`, `chuc_vu` |
| **10.3** | **Phân tích học vấn & thâm niên** | BoD / HR | • Bộ lọc: Bậc học vấn, Nhóm thâm niên | 1. Phân nhóm trình độ đào tạo (Trung cấp, CĐ, ĐH, Chứng chỉ nghề quốc tế).<br>2. Tính thâm niên công tác = `CURRENT_DATE - ngay_vao_lam`.<br>3. Gom nhóm: < 1 năm, 1-3 năm, > 3 năm. | • Biểu đồ chất lượng nhân sự phục vụ quy hoạch đào tạo và bổ nhiệm nội bộ.<br>CSDL: `nhan_vien` |
| **10.4** | **Đối soát quỹ lương vs Ngân sách** | BoD / HR / Kế toán | • `ma_ky_luong` hoặc Năm tài chính<br>• Hạn mức ngân sách quỹ lương được duyệt | 1. Tính tổng quỹ lương thực chi từ `phieu_luong`.<br>2. Tính chênh lệch so với ngân sách kế hoạch.<br>3. Phân rã chi phí chi tiết theo từng phòng ban. | • Biểu đồ cột Ngân sách vs Thực tế.<br>• Cảnh báo đỏ nếu quỹ lương vượt định mức.<br>CSDL: `phieu_luong`, `ky_luong`, `phong_ban` |
| **10.5** | **Thống kê chuyên cần & vi phạm** | Manager / HR | • Tháng báo cáo, phòng ban | 1. Quét dữ liệu `cham_cong`: Đi đúng giờ, số lượt trễ/sớm, vắng mặt.<br>2. Tính tỷ lệ chuyên cần = (Ca đúng giờ / Tổng ca) * 100%.<br>3. Lập danh sách xếp hạng vi phạm kỷ luật. | • Bảng xếp hạng chuyên cần của các bộ phận.<br>• Danh sách vi phạm kỷ luật kèm giờ quẹt đối chứng.<br>CSDL: `cham_cong`, `phan_ca`, `nhan_vien` |
| **10.6** | **Báo cáo hiệu quả nhân sự dự án/sự kiện** | BoD / HR / Manager | • Năm/Tháng tổ chức sự kiện<br>• Danh mục các dự án/sự kiện | 1. Thống kê tổng số sự kiện đã tổ chức, quy mô Task Force điều động.<br>2. Tổng hợp tổng chi phí phụ cấp dự án đã chi trả từ `phan_cong_du_an`.<br>3. Đánh giá tỷ lệ hoàn thành xuất sắc/đạt của nhân sự tham gia.<br>4. Phân tích hiệu quả sử dụng nguồn nhân lực liên phòng ban. | • Báo cáo chi phí và hiệu quả nhân sự sự kiện.<br>• Đánh giá mức độ đóng góp của từng bộ phận cho các sự kiện lớn.<br>CSDL: `du_an`, `phan_cong_du_an`, `nhan_vien` |

---

### Phân Hệ 11.0: Quản Trị Hệ Thống

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [INPUT] ĐẦU VÀO                                                                               │
│ • Danh mục tổ chức: Phòng ban (phong_ban), Chức danh công việc (chuc_vu)                      │
│ • Cấu hình khung giờ ca 24/7: Mã ca, giờ mở ca, giờ đóng ca, hệ số lương, cờ ca đêm            │
│ • Tham số tài chính: Định mức ăn ca (30k), Tỷ lệ bảo hiểm bắt buộc 10.5%, Mức phạt vi phạm    │
│ • Lệnh sao lưu CSDL (Schema / Data / Full) hoặc Tệp tin phục hồi dữ liệu (.sql / .dump)       │
│ • Mật khẩu xác thực quyền Quản trị viên tối cao (Super Admin)                                 │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [PROCESS] QUY TRÌNH XỬ LÝ & BẢO MẬT HỆ THỐNG                                                  │
│ 1. Toàn vẹn dữ liệu: Kiểm tra khóa ngoại (FK) - Chặn xóa phòng ban/chức vụ nếu còn nhân viên  │
│ 2. Cấu hình ca làm việc: Admin độc quyền thiết lập khung giờ và hệ số lương ca trực 24/7      │
│ 3. Thiết lập tham số: Lưu định mức vào cau_hinh_khoan_luong để động cơ tính lương tự động nạp │
│ 4. Sao lưu CSDL: Kết nối Supabase Cloud -> Trích xuất DDL 30 bảng + Dữ liệu -> Đóng gói tệp nén│
│ 5. Phục hồi CSDL: Xác thực đa yếu tố quyền Admin -> Nạp lại kịch bản phục hồi an toàn         │
│ 6. Ghi nhật ký kiểm toán hệ thống an toàn thông tin (nhat_ky_quan_tri)                        │
└───────────────────────────────────────────────┬───────────────────────────────────────────────┘
                                                │
                                                ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────┐
│ [OUTPUT] ĐẦU RA & CƠ SỞ DỮ LIỆU                                                               │
│ • Danh mục cơ cấu tổ chức được cập nhật đồng bộ trong phong_ban, chuc_vu                      │
│ • Khung giờ ca làm việc 24/7 chuẩn hóa trong ca_lam_viec                                      │
│ • Thông số định mức mới được áp dụng tự động cho các kỳ tính lương tiếp theo                  │
│ • File sao lưu an toàn CSDL tải về máy hoặc lưu trữ đám mây an toàn                           │
│ • Bản ghi kiểm toán ghi nhận toàn bộ thao tác trong nhật ký quản trị                          │
│ • Phạm vi bảo vệ: Toàn bộ 30 bảng Supabase PostgreSQL chuẩn 3NF                               │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Bảng chi tiết chức năng Cấp 2 (Phân hệ 11.0):

| Mã | Tên Chức Năng | Vai Trò | Đầu Vào (Input) | Quy Trình Xử Lý (Process) | Đầu Ra (Output) & CSDL |
|:---:|:---|:---:|:---|:---|:---|
| **11.1** | **Quản lý danh mục phòng ban** | Admin | • `ma_phong_ban`, `ten_phong_ban`, `mo_ta` | 1. Kiểm tra tính duy nhất của tên phòng ban.<br>2. Chặn xóa nếu còn nhân viên đang trực thuộc phòng ban đó. | • Danh mục phòng ban được cập nhật đồng bộ.<br>CSDL: `phong_ban`, `nhan_vien` |
| **11.2** | **Quản lý danh mục chức vụ** | Admin | • `ma_chuc_vu`, `ten_chuc_vu`, `mo_ta` | 1. Kiểm tra không trùng tên chức danh công việc.<br>2. Chặn xóa nếu còn nhân viên đang đảm nhiệm chức vụ. | • Danh mục chức danh nghề nghiệp chuẩn hóa.<br>CSDL: `chuc_vu`, `nhan_vien` |
| **11.3** | **Cấu hình khung giờ ca 24/7** *(Độc quyền Admin)* | Admin | • `ma_ca`, giờ mở ca, giờ đóng ca, hệ số, cờ đêm | 1. Validate tính hợp lệ của khung giờ ca 24/7.<br>2. Cập nhật bảng `ca_lam_viec` để áp dụng cho quy trình xếp ca.<br>3. Admin là vai trò duy nhất có quyền tạo/sửa cấu hình ca; HR chỉ đối chiếu vận hành. | • Cấu hình ca làm việc được lưu trong `ca_lam_viec`.<br>CSDL: `ca_lam_viec` |
| **11.4** | **Thiết lập tham số hệ thống & định mức** | Admin | • `ma_khoan`, tên khoản, loại khoản (`PHU_CAP`/`THUONG`/`KHAU_TRU`), giá trị định mức | 1. Tùy chỉnh các tham số hệ thống (ăn ca 30k, phạt trễ 50k-100k, tỷ lệ bảo hiểm 10.5%...).<br>2. Lưu vào `cau_hinh_khoan_luong` để các công thức tự động áp dụng. | • Bảng tham số định mức được cập nhật.<br>• Kỳ lương kế tiếp tự động áp dụng số mới.<br>CSDL: `cau_hinh_khoan_luong` |
| **11.5** | **Sao lưu & Phục hồi CSDL** | Admin | • Lệnh Backup (Schema / Data / Full) hoặc File phục hồi `.sql.gz`<br>• Mật khẩu xác thực Super Admin | 1. Backup: Dump cấu trúc 30 bảng + Dữ liệu từ Supabase Cloud -> Nén file mã hóa.<br>2. Restore: Xác thực nghiêm ngặt quyền Admin -> Nạp script phục hồi an toàn.<br>3. Ghi log kiểm toán thao tác hệ thống. | • File backup tải về máy hoặc lưu trữ đám mây.<br>• Thông báo kết quả và nhật ký an toàn.<br>CSDL: Toàn bộ 30 bảng Supabase |

---

## 4. Ma Trận Phân Quyền Vai Trò Người Dùng (RBAC Matrix)

Hệ thống phân định quyền hạn rõ ràng cho 5 vai trò chuẩn:
1. **Quản trị viên (Admin):** Quản trị tối cao toàn bộ hệ thống, phân quyền, cấu hình khung giờ ca 24/7, tham số tài chính, sao lưu phục hồi CSDL.
2. **Phòng Nhân sự (HR):** Quản lý hồ sơ nhân viên, ký kết HĐLĐ, giám sát công toàn khách sạn, chạy động cơ tính lương tự động, xuất phiếu lương PDF, quản lý phụ cấp thưởng và gửi yêu cầu cấp tài khoản.
3. **Quản lý Bộ phận (Manager):** Lập lịch phân ca tuần/tháng cho nhân sự thuộc bộ phận mình, duyệt đơn nghỉ phép/nghỉ ốm, duyệt giải trình công, điều phối Task Force dự án sự kiện và đánh giá nghiệm thu.
4. **Nhân viên (Employee):** Quẹt thẻ Check-in/Check-out trên Mobile App, nộp đơn từ trực tuyến, nhận thông báo điều động dự án sự kiện, tra cứu lịch làm việc và phiếu lương cá nhân.
5. **Bộ phận CNTT (IT):** Tiếp nhận yêu cầu cấp tài khoản từ Phòng HR, thực hiện cấp phát tài khoản đăng nhập Mobile App trên Supabase Auth, hỗ trợ kỹ thuật và giám sát an toàn thông tin.

### Bảng Ma Trận Phân Quyền 11 Phân Hệ Cấp 1:

| Phân Hệ Cấp 1 | Mã Quyền Chuẩn | Quản Trị Viên (Admin) | Phòng Nhân Sự (HR) | Quản Lý Bộ Phận (Manager) | Nhân Viên (Employee) | Bộ Phận CNTT (IT) |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|
| **1.0 Quản lý Nhân sự & HĐLĐ** | `NV_MANAGE` | Chỉ xem | **Toàn quyền (CRUD)** | Xem bộ phận mình | Chỉ xem hồ sơ cá nhân | Không |
| **2.0 Tài khoản & Phân quyền RBAC** | `USER_RBAC` / `CAP_TAI_KHOAN_MOBILE` | **Toàn quyền (CRUD)** | Gửi yêu cầu cấp tài khoản (`CHO_IT`) | Không | Đổi mật khẩu cá nhân | **Cấp tài khoản Mobile (`CAP_TAI_KHOAN_MOBILE`)** |
| **3.0 Ca làm & Lịch phân ca 24/7** | `SHIFT_MANAGE` | Xem kiểm toán | Giám sát toàn KS | **Xếp ca & Quản lý đổi ca bộ phận** | Xem lịch ca cá nhân trên Mobile | Không |
| **4.0 Bảng Chấm công 24/7 & Chuyên cần** | `ATT_MANAGE` | Xem kiểm toán | Giám sát & Khóa công toàn KS | Duyệt giải trình công bộ phận | **Quẹt thẻ di động GPS/Wi-Fi & Gửi giải trình** | Quản trị thiết bị |
| **5.0 Quản lý Đơn từ điện tử** | `LEAVE_FLOW` | Không | **Duyệt Thôi việc / Thai sản** | **Duyệt Phép năm / Nghỉ ốm** | **Tạo & nộp 4 loại đơn trên Mobile** | Không |
| **6.0 Tính toán Lương & Phiếu lương PDF** | `PAYROLL_CALC` / `PAYSLIP_VIEW` | Xem đối soát | **Thực thi tính lương & Khóa sổ** | Không | **Xem phiếu lương & Tải PDF cá nhân** | Không |
| **7.0 Quản lý Phụ cấp** | `ALLOWANCE_MANAGE` | Cấu hình định mức | Quản lý & Áp dụng phụ cấp | Không | Xem chi tiết phụ cấp trên phiếu lương | Không |
| **8.0 Quản lý Tiền thưởng** | `BONUS_MANAGE` | Phê duyệt ngân sách | Quản lý & Phân bổ thưởng | Đề xuất thưởng bộ phận | Xem chi tiết thưởng trên phiếu lương | Không |
| **9.0 Quản lý Dự án & Sự kiện** | `PROJECT_MANAGE` | Phê duyệt chủ trương | Giám sát Task Force & Kết chuyển lương | **Khởi tạo, Điều động & Nghiệm thu** | **Nhận thông báo Mobile & Tham gia Task Force** | Đảm bảo hạ tầng thông báo |
| **10.0 Báo cáo & Thống kê Quản trị** | `REPORT_VIEW` | Báo cáo toàn diện & An toàn | **Báo cáo nhân sự & Quỹ lương** | Báo cáo chuyên cần & Bộ phận mình | Không | Báo cáo vận hành kỹ thuật |
| **11.0 Quản trị Hệ thống** | `SYS_CONFIG` | **Toàn quyền (CRUD)** | Không | Không | Không | Hỗ trợ vận hành kỹ thuật |

---

## 5. Ma Trận Đối Chiếu CRUD (57 Chức Năng ↔ 30 Bảng CSDL Supabase)

Cơ sở dữ liệu của hệ thống đạt chuẩn hóa **dạng chuẩn 3 (3NF)** với quy mô **30 bảng** hoàn chỉnh, đảm bảo tính toàn vẹn dữ liệu và hiệu năng truy vấn:

### Bảng Ánh Xạ Chức Năng và Bảng CSDL:

```text
┌─────────────────────────────────┬──────────────────────────────────────────────────────────────────┐
│ Tên Bảng CSDL (Supabase 30 Bảng)│ Các Chức Năng Nghiệp Vụ Tương Tác Trực Tiếp                      │
├─────────────────────────────────┼──────────────────────────────────────────────────────────────────┤
│ 1. phong_ban                    │ 1.1, 1.2, 1.4, 1.5, 3.5, 8.2, 10.1, 10.2, 10.4, 11.1             │
│ 2. chuc_vu                      │ 1.1, 1.2, 1.4, 1.5, 7.3, 10.2, 11.2                              │
│ 3. nhan_vien                    │ 1.1 - 1.5, 2.1, 2.5, 3.2, 4.4, 4.5, 5.1 - 5.5, 6.1 - 6.6,        │
│                                 │ 7.4, 8.1 - 8.3, 9.1 - 9.5, 10.1 - 10.6, 11.1, 11.2               │
│ 4. hop_dong                     │ 1.3, 1.4, 2.5, 5.4, 6.2, 10.1                                    │
│ 5. tai_khoan                    │ 2.1, 2.2, 2.3, 2.5                                               │
│ 6. vai_tro                      │ 2.1, 2.3, 2.4, 2.5                                               │
│ 7. quyen                        │ 2.1, 2.4                                                         │
│ 8. vai_tro_quyen                │ 2.1, 2.4                                                         │
│ 9. yeu_cau_tai_khoan            │ 2.5 (Quy trình HR gửi -> IT cấp tài khoản Mobile)                │
│ 10. ca_lam_viec                 │ 3.1, 3.2, 3.3, 3.5, 4.2, 7.1, 11.3                               │
│ 11. phan_ca                     │ 3.2, 3.3, 3.4, 3.5, 4.1, 4.4, 9.2, 10.5                          │
│ 12. cham_cong                   │ 4.1, 4.2, 4.3, 4.4, 4.5, 6.1, 6.2, 8.3, 10.5                     │
│ 13. loai_don                    │ 5.1, 5.2, 5.3, 5.4                                               │
│ 14. don_tu                      │ 5.1, 5.2, 5.3, 5.4, 5.5, 6.2                                     │
│ 15. ky_luong                    │ 6.1, 6.2, 6.3, 6.6, 8.4, 9.5, 10.4                               │
│ 16. cau_hinh_khoan_luong        │ 6.2, 7.2, 8.1 - 8.4, 11.4                                        │
│ 17. cau_hinh_phu_cap            │ 6.2, 7.1, 7.2, 7.3, 7.4, 7.5                                     │
│ 18. cau_hinh_khau_tru           │ 6.2, 11.4                                                        │
│ 19. cau_hinh_tang_ca            │ 4.3, 6.2, 11.4                                                   │
│ 20. phieu_luong                 │ 6.2, 6.3, 6.4, 6.5, 6.6, 10.4                                    │
│ 21. chi_tiet_phieu_luong        │ 6.2, 6.3, 6.4, 6.5, 7.1, 8.1, 8.5, 9.5                           │
│ 22. chi_tiet_tru_bao_hiem       │ 6.2, 6.4, 6.5                                                    │
│ 23. chi_tiet_tru_thue_tncn      │ 6.2, 6.4, 6.5, 6.6                                               │
│ 24. chi_tiet_tru_vi_pham_tam_ung│ 6.2, 6.4, 6.5                                                    │
│ 25. nhat_ky_quan_tri            │ 2.2, 2.5, 7.5, 11.5                                              │
│ 26. du_an                       │ 9.1, 9.2, 9.4, 10.6                                              │
│ 27. phan_cong_du_an             │ 9.2, 9.3, 9.4, 9.5, 6.2, 10.6                                    │
│ 28. dinh_bien_ca                │ 3.2, 3.5, 10.5                                                   │
│ 29. giai_trinh_cham_cong        │ 4.5, 6.1, 6.2, 10.5                                              │
│ 30. yeu_cau_doi_ca              │ 3.4, 3.5, 4.4, 10.5                                              │
└─────────────────────────────────┴──────────────────────────────────────────────────────────────────┘
```

---

### Đặc Tả Chi Tiết 2 Bảng Mới: `du_an` và `phan_cong_du_an`

Nhằm đáp ứng trọn vẹn nghiệp vụ quản lý dự án và các sự kiện lớn của khách sạn 5 sao (Phân hệ 9.0), hệ thống thiết kế bổ sung 2 bảng CSDL chuẩn 3NF:

#### Bảng 26: `du_an` (Dự án & Sự kiện Khách sạn)
Lưu trữ thông tin các dự án, sự kiện đặc biệt do khách sạn tổ chức hoặc phục vụ đối tác (Hội nghị thượng đỉnh, Tiệc cưới VIP, Gala Dinner, Triển lãm MICE...):

| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---|
| `ma_du_an` | SERIAL | PRIMARY KEY | Mã định danh duy nhất của dự án / sự kiện |
| `ten_du_an` | VARCHAR(200) | NOT NULL, UNIQUE | Tên sự kiện (VD: Tiệc cưới Hoàng Gia, Hội nghị Thượng đỉnh MICE 2026) |
| `loai_du_an` | VARCHAR(50) | NOT NULL | Phân loại sự kiện: `TIEC_CUOI`, `HOI_NGHI_MICE`, `GALA_DINNER`, `FESTIVAL_AM_THUC`, `DOAN_VIP`, `BAO_TRI_NANG_CAP` |
| `dia_diem` | VARCHAR(255) | NOT NULL | Địa điểm tổ chức (Grand Ballroom, Lotus Hall, Rooftop Sky Bar, Hồ bơi) |
| `ngay_bat_dau` | TIMESTAMPTZ | NOT NULL | Thời điểm bắt đầu triển khai chuẩn bị sự kiện |
| `ngay_ket_thuc` | TIMESTAMPTZ | NOT NULL | Thời điểm kết thúc, bàn giao hiện trường và nghiệm thu |
| `ngan_sach_phu_cap` | DECIMAL(15,2) | NOT NULL, DEFAULT 0.00, CHECK >= 0 | Tổng ngân sách phân bổ chi trả thù lao / phụ cấp sự kiện (VNĐ) |
| `ma_nguoi_quan_ly` | INT | FK -> `nhan_vien.ma_nhan_vien`, NOT NULL, ON DELETE RESTRICT | Trưởng ban tổ chức / Quản lý điều hành dự án sự kiện |
| `mo_ta` | TEXT | NULL | Kịch bản chi tiết, yêu cầu phục vụ, quy chuẩn an ninh và kỹ thuật |
| `trang_thai` | VARCHAR(30) | NOT NULL, DEFAULT 'LEN_KE_HOACH' | Tiến độ: `LEN_KE_HOACH`, `DANG_DIEN_RA`, `HOAN_THANH`, `HUY` |

#### Bảng 27: `phan_cong_du_an` (Phân công nhân sự & Thù lao sự kiện)
Bảng trung gian quản lý danh sách biệt đội Task Force được điều động từ nhiều phòng ban khác nhau tham gia dự án/sự kiện, xác định vai trò, mức thù lao và kết quả nghiệm thu:

| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---|
| `ma_phan_cong` | SERIAL | PRIMARY KEY | Mã bản ghi phân công nhân sự |
| `ma_du_an` | INT | FK -> `du_an.ma_du_an`, NOT NULL, ON DELETE CASCADE | Dự án / sự kiện tiếp nhận nhân sự |
| `ma_nhan_vien` | INT | FK -> `nhan_vien.ma_nhan_vien`, NOT NULL, ON DELETE CASCADE | Nhân viên được điều động tham gia sự kiện |
| `vai_tro_du_an` | VARCHAR(100) | NOT NULL | Vai trò: `DIEU_PHOI`, `KY_THUAT_AM_THANH_AS`, `PHUC_VU_TIEC`, `LE_TAN_DON_KHACH`, `AN_NINH_VIP`, `BEP_TIEC`... |
| `nhiem_vu_cu_the` | VARCHAR(255) | NULL | Chi tiết công việc cụ thể tại vị trí trực chiến |
| `so_tien_phu_cap` | DECIMAL(15,2) | NOT NULL, DEFAULT 0.00, CHECK >= 0 | Mức thù lao / phụ cấp sự kiện dành riêng cho nhân sự này (VNĐ) |
| `danh_gia` | VARCHAR(30) | NOT NULL, DEFAULT 'CHUA_DANH_GIA' | Kết quả nghiệm thu: `CHUA_DANH_GIA`, `HOAN_THANH`, `XUAT_SAC`, `KHONG_THAM_GIA` |
| `trang_thai_chi_tra` | VARCHAR(30) | NOT NULL, DEFAULT 'CHUA_CHI_TRA' | Trạng thái thanh toán: `CHUA_CHI_TRA`, `DA_KET_CHUYEN_LUONG`, `HUY_BO` |
| `ma_chi_tiet` | INT | FK -> `chi_tiet_phieu_luong.ma_chi_tiet`, NULL, ON DELETE SET NULL | Khóa ngoại liên kết dòng chi tiết phiếu lương sau khi kết chuyển vào kỳ lương tháng |
| `ngay_danh_gia` | TIMESTAMPTZ | NULL | Thời điểm quản lý nghiệm thu và chấm điểm đánh giá |
| *Ràng buộc duy nhất* | `UNIQUE (ma_du_an, ma_nhan_vien)` | UNIQUE | Mỗi nhân viên chỉ được phân công một vị trí chính trong một sự kiện để đảm bảo tập trung trách nhiệm |

---

### Đặc Tả Chi Tiết 3 Bảng Mới: `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`

Nhằm hoàn thiện nhóm chức năng **3.0 Quản lý Phân ca** và **4.0 Quản lý Chấm công** của PRD, hệ thống thiết kế bổ sung 3 bảng CSDL chuẩn 3NF:

#### Bảng 28: `dinh_bien_ca` (Định biên nhân sự theo ca & chức vụ)
Xác lập số nhân sự tối thiểu cần có cho từng cặp Ca × Chức vụ, làm căn cứ tự động cảnh báo thiếu người khi Quản lý xếp lịch (PRD 3.5):

| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---|
| `ma_dinh_bien` | SERIAL | PRIMARY KEY | Mã định danh dòng định biên |
| `ma_ca` | INT | FK -> `ca_lam_viec.ma_ca`, NOT NULL, ON DELETE CASCADE | Ca mẫu được đặt định biên |
| `ma_chuc_vu` | INT | FK -> `chuc_vu.ma_chuc_vu`, NOT NULL, ON DELETE CASCADE | Chức vụ cần đếm số người trong ca |
| `so_nguoi_toi_thieu` | INT | NOT NULL, DEFAULT 1, CHECK >= 1 | Số nhân sự tối thiểu phải có mặt để vận hành ca không bị gián đoạn |
| *Ràng buộc duy nhất* | `UNIQUE (ma_ca, ma_chuc_vu)` | UNIQUE | Mỗi cặp Ca × Chức vụ chỉ có một dòng định biên, tránh cộng dồn sai số người tối thiểu |

#### Bảng 29: `giai_trinh_cham_cong` (Giải trình sai lệch giờ chấm công)
Lưu hồ sơ nhân viên giải trình khi giờ vào/ra thực tế lệch so với ca đã xếp (quên quẹt thẻ, hỏng đầu đọc...). Hồ sơ đi qua **hai cấp duyệt**: Quản lý bộ phận xác minh → HR xác nhận (PRD 4.5):

| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---|
| `ma_giai_trinh` | SERIAL | PRIMARY KEY | Mã hồ sơ giải trình |
| `ma_phan_ca` | INT | FK -> `phan_ca.ma_phan_ca`, NOT NULL, ON DELETE CASCADE | Lượt ca bị giải trình |
| `ma_nhan_vien` | INT | FK -> `nhan_vien.ma_nhan_vien`, NOT NULL, ON DELETE CASCADE | Nhân viên đứng tên giải trình |
| `gio_de_xuat_vao` | TIMESTAMPTZ | NULL | Giờ vào thực tế đề nghị ghi nhận; để trống nếu chỉ điều chỉnh giờ ra |
| `gio_de_xuat_ra` | TIMESTAMPTZ | NULL | Giờ ra thực tế đề nghị ghi nhận; để trống nếu chỉ điều chỉnh giờ vào |
| `ly_do` | VARCHAR(500) | NOT NULL | Nội dung giải trình; trần 500 ký tự khớp đúng cột CSDL |
| `minh_chung` | VARCHAR(500) | NULL | Mã biên bản giao ca, số điện thoại tổ trưởng, đường dẫn ảnh… làm căn cứ xác minh |
| `trang_thai` | VARCHAR(20) | NOT NULL, DEFAULT 'CHO_QUAN_LY', CHECK in ('CHO_QUAN_LY', 'CHO_HR', 'DA_DUYET', 'TU_CHOI') | Tiến trình duyệt 2 cấp: Quản lý xác minh → HR xác nhận; chỉ `DA_DUYET` mới ghi lại giờ công |
| `ma_quan_ly_xac_minh` | INT | FK -> `nhan_vien.ma_nhan_vien`, NULL, ON DELETE SET NULL | Quản lý cấp 1 đã xác minh |
| `ngay_quan_ly_xac_minh` | TIMESTAMPTZ | NULL | Thời điểm Quản lý xác minh |
| `y_kien_quan_ly` | VARCHAR(500) | NULL | Ý kiến / lý do từ chối của cấp Quản lý |
| `ma_hr_xac_nhan` | INT | FK -> `nhan_vien.ma_nhan_vien`, NULL, ON DELETE SET NULL | Cán bộ HR xác nhận cấp 2 |
| `ngay_hr_xac_nhan` | TIMESTAMPTZ | NULL | Thời điểm HR xác nhận |
| `y_kien_hr` | VARCHAR(500) | NULL | Ý kiến / lý do từ chối của cấp HR |
| `ngay_gui` | TIMESTAMPTZ | NOT NULL, DEFAULT CURRENT_TIMESTAMP | Thời điểm nhân viên gửi hồ sơ |
| *Ràng buộc nghiệp vụ* | `chk_giai_trinh_can_gio` | CHECK | Bắt buộc có ít nhất một trong hai mốc giờ đề xuất |
| *Ràng buộc từ chối* | `chk_giai_trinh_tu_choi_can_y_kien` | CHECK | Từ chối bắt buộc kèm ý kiến của cấp ra quyết định |
| *Ràng buộc chống trùng* | `uq_giai_trinh_dang_mo` | PARTIAL UNIQUE INDEX trên `ma_phan_ca` WHERE `trang_thai IN ('CHO_QUAN_LY','CHO_HR')` | Mỗi lượt ca chỉ mở một hồ sơ đang chờ duyệt tại một thời điểm |

#### Bảng 30: `yeu_cau_doi_ca` (Yêu cầu đổi ca trực giữa nhân viên)
Cho phép nhân viên chủ động đổi ca với đồng nghiệp, có vết duyệt của Quản lý/HR (PRD 3.4):

| Tên Cột | Kiểu Dữ Liệu | Ràng Buộc | Ý Nghĩa Nghiệp Vụ |
|:---|:---|:---|:---|
| `ma_yeu_cau` | SERIAL | PRIMARY KEY | Mã yêu cầu đổi ca |
| `ma_phan_ca_gui` | INT | FK -> `phan_ca.ma_phan_ca`, NOT NULL, ON DELETE CASCADE | Lượt ca nhân viên gửi nhường lại |
| `ma_phan_ca_nhan` | INT | FK -> `phan_ca.ma_phan_ca`, NOT NULL, ON DELETE CASCADE | Lượt ca nhân viên nhận làm thay |
| `ma_nhan_vien_gui` | INT | FK -> `nhan_vien.ma_nhan_vien`, NOT NULL, ON DELETE RESTRICT | Nhân viên gửi yêu cầu |
| `ma_nhan_vien_nhan` | INT | FK -> `nhan_vien.ma_nhan_vien`, NOT NULL, ON DELETE RESTRICT | Nhân viên nhận ca |
| `ly_do` | VARCHAR(500) | NOT NULL | Lý do đổi ca |
| `trang_thai` | VARCHAR(20) | NOT NULL, DEFAULT 'CHO_DUYET', CHECK in ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'DA_HUY') | Vòng đời yêu cầu; `DA_HUY` dành cho trường hợp người gửi tự rút |
| `ma_nguoi_duyet` | INT | FK -> `nhan_vien.ma_nhan_vien`, NULL, ON DELETE SET NULL | Quản lý / HR ra quyết định |
| `ngay_gui` | TIMESTAMPTZ | NOT NULL, DEFAULT CURRENT_TIMESTAMP | Thời điểm gửi yêu cầu |
| `ngay_duyet` | TIMESTAMPTZ | NULL | Thời điểm duyệt / từ chối |
| `phan_hoi_duyet` | VARCHAR(500) | NULL | Phản hồi của người duyệt |
| *Ràng buộc khác ca* | `chk_doi_ca_khac_ca` | CHECK | Không cho đổi một ca với chính nó |
| *Ràng buộc khác người* | `chk_doi_ca_khac_nguoi` | CHECK | Không cho tự đổi ca với chính mình |
| *Ràng buộc từ chối* | `chk_doi_ca_tu_choi_can_ly_do` | CHECK | Từ chối bắt buộc kèm phản hồi để nhân viên biết lý do |

---

### Bảng Tổng Hợp Hiện Trạng Nghiệp Vụ và Quy Mô CSDL 30 Bảng:

| Nhóm Nghiệp Vụ | Số Bảng | Danh Sách Các Bảng CSDL | Trạng Thái Triển Khai Thực Tế |
|:---|:---:|:---|:---|
| **1. Nhân sự & Tổ chức** | 4 bảng | `phong_ban`, `chuc_vu`, `nhan_vien`, `hop_dong` | Đã hoàn thành 100% CSDL, Core, Controller và View Web Admin |
| **2. Tài khoản & Phân quyền** | 5 bảng | `tai_khoan`, `vai_tro`, `quyen`, `vai_tro_quyen`, `yeu_cau_tai_khoan` | Đã hoàn thành 100% CSDL, Core, Controller và View (`/YeuCauTaiKhoan`) |
| **3. Ca làm & Chấm công 24/7** | 6 bảng | `ca_lam_viec`, `phan_ca`, `cham_cong`, `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca` | Đã hoàn thành 100% Web Admin (lịch phân ca, định biên, giải trình 2 cấp, đổi ca) và Mobile App Check-in GPS/Wi-Fi |
| **4. Đơn từ điện tử** | 2 bảng | `loai_don`, `don_tu` | Đã hoàn thành 100% nộp đơn Mobile và xét duyệt Web Admin |
| **5. Kỳ lương & Cấu hình** | 5 bảng | `ky_luong`, `cau_hinh_khoan_luong`, `cau_hinh_phu_cap`, `cau_hinh_khau_tru`, `cau_hinh_tang_ca` | Đã hoàn thành 100% CSDL và cấu hình Web Admin |
| **6. Phiếu lương & Khấu trừ chi tiết** | 5 bảng | `phieu_luong`, `chi_tiet_phieu_luong`, `chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn`, `chi_tiet_tru_vi_pham_tam_ung` | Đã hoàn thành 100% động cơ tính lương 2 tầng; QuestPDF đang theo lộ trình phát triển |
| **7. Quản trị & Nhật ký** | 1 bảng | `nhat_ky_quan_tri` | Đã hoàn thành 100% lưu vết thao tác kiểm toán an toàn |
| **8. Dự án & Sự kiện Khách sạn** | 2 bảng | `du_an`, `phan_cong_du_an` | Thiết kế CSDL chuẩn hóa phục vụ phân hệ 9.0 và kết chuyển tự động vào lương |
| **TỔNG CỘNG** | **30 bảng** | **Hệ thống CSDL Supabase PostgreSQL chuẩn 3NF hoàn chỉnh** | **Đảm bảo tính nhất quán, toàn vẹn và khả năng mở rộng cao** |
