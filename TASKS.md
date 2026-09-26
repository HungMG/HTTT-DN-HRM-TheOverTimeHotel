# 📋 Bảng Phân Chia Nhiệm Vụ Đồ Án (Task Sheet)

### Hệ Thống Quản Lý Nhân Sự & Tiền Lương Khách Sạn The OverTime Hotel

**Môn học:** Hệ Thống Thông Tin Doanh Nghiệp (HTTTDN) · **Nhóm 4 sinh viên (DCT123C4)**  
**Nền tảng:** Web Admin (ASP.NET Core MVC .NET 10) · Mobile App (.NET MAUI .NET 10) · CSDL: Supabase (PostgreSQL Cloud)  
**Tài liệu gốc:** [Google Sheet Phân Chia Công Việc](https://docs.google.com/spreadsheets/d/16KZSB7seb6LT2DGE9hh6d--9ZpIarvO8t6yt4bBO91Q/edit?gid=1752931908#gid=1752931908)

---

## 📊 TIẾN ĐỘ TỔNG QUAN HỆ THỐNG

- **Tổng số nhiệm vụ:** 30 Tasks
- **Đã hoàn thành:** 15 / 30 Tasks (50.0%)
- **Đang thực hiện:** 0 Task
- **Chưa bắt đầu:** 15 Tasks
- **Mốc chấm đồ án chính thức:** **Tuần 10 (10/11/2026)**

```text
[████████████████░░░░░░░░░░░░░░] 50.0% Hoàn thành (15/30)
```

---

## 🚀 DANH SÁCH CHI TIẾT 30 TASKS (GITHUB CHECKLIST)

### GIAI ĐOẠN 1: KHỞI TẠO ĐỀ TÀI & THIẾT KẾ CƠ BẢN (TUẦN 1 - 2)

- [x] **TSK-01:** Mô tả doanh nghiệp khách sạn The OverTime Hotel
  - **Phân hệ:** Tài liệu đề tài
  - **Mô tả & Output:** Viết mô tả hoạt động 24/7, cơ cấu tổ chức, 8 bước quy trình quản lý nhân sự
  - **Người phụ trách:** Nguyễn Đình Cường | **Độ ưu tiên:** Cao | **Hạn chót:** 15/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-02:** Xác định 8 nhóm chức năng cốt lõi & Sơ đồ BFD
  - **Phân hệ:** Phân tích yêu cầu
  - **Mô tả & Output:** Chốt 8 chức năng, lập sơ đồ phân rã BFD từ cấp 1 đến cấp 2, vẽ hình BFD (`so_do_chuc_nang.jpg`)
  - **Người phụ trách:** Nguyễn Hoàng Long | **Độ ưu tiên:** Cao | **Hạn chót:** 15/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-03:** Thiết kế mô hình ERD & Lược đồ logic 3NF
  - **Phân hệ:** Thiết kế CSDL
  - **Mô tả & Output:** Thiết kế 17 thực thể (có HOP_DONG), vẽ ERD và xuất file `.erdplus`, `.drawio`, `.html`
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 15/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-04:** Lập Từ điển dữ liệu CSDL chi tiết (17 bảng)
  - **Phân hệ:** Thiết kế CSDL
  - **Mô tả & Output:** Mô tả 6 cột cho 17 bảng (Tên, Kiểu dữ liệu, Khóa, Null, Ràng buộc, Diễn giải)
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Cao | **Hạn chót:** 15/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-05:** Hoàn thiện Báo cáo khởi tạo đề tài nộp buổi học
  - **Phân hệ:** Tổng hợp tài liệu
  - **Mô tả & Output:** Gộp danh sách nhóm 4 người, BFD, ERD, Từ điển dữ liệu, phương án Backup CSDL (`do_an_nhom.docx`)
  - **Người phụ trách:** Cả nhóm | **Độ ưu tiên:** Cao | **Hạn chót:** 15/09/2026
  - **Trạng thái:** `Hoàn thành`

---

### GIAI ĐOẠN 2: PHÂN TÍCH HỆ THỐNG & NỀN TẢNG DỮ LIỆU ĐÁM MÂY (TUẦN 3 - 4)

- [x] **TSK-06:** Vẽ Sơ đồ ngữ cảnh (Context Diagram) & DFD Mức 0 / Mức 1
  - **Phân hệ:** Phân tích hệ thống
  - **Mô tả & Output:** Xác định luồng dữ liệu tác nhân Admin (Web), HR/Manager/Employee (Mobile) với hệ thống
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Cao | **Hạn chót:** 22/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-07:** Khởi tạo Project Supabase & Chạy Script SQL tạo 17 bảng
  - **Phân hệ:** Cơ sở dữ liệu
  - **Mô tả & Output:** Tạo database `OvertimeHotel-HRM-DB` trên Supabase Cloud (Singapore), chạy DDL script tạo 17 bảng (PostgreSQL) kèm PK, FK, Index (tách họ và tên)
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 22/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-08:** Tạo Visual Studio Solution: Web MVC + App .NET MAUI, tạo repo GitHub
  - **Phân hệ:** Kiến trúc C# .NET
  - **Mô tả & Output:** Khởi tạo Solution C# .NET 10 (`src/OvertimeHotel.HRM.slnx`) gồm: Web Admin (ASP.NET Core MVC), Mobile App (.NET MAUI), Core, Data
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 29/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-09:** Kết nối Supabase qua Npgsql / Entity Framework Core
  - **Phân hệ:** C# Data Access
  - **Mô tả & Output:** Cài đặt Npgsql EF Core & Supabase C# SDK; Xây dựng 17 Entities C#, Enums, Constants, DTOs và `AppDbContext.cs` cấu hình quan hệ bảng
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 29/09/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-10:** Thiết lập Sao lưu (Backup) & Phục hồi (Restore) Supabase
  - **Phân hệ:** An toàn CSDL
  - **Mô tả & Output:** Xây dựng `BackupService`, `BackupController` trên Web Admin cho phép xuất Snapshot JSON, khôi phục dữ liệu và thống kê 17 bảng
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Cao | **Hạn chót:** 29/09/2026
  - **Trạng thái:** `Hoàn thành`

---

### GIAI ĐOẠN 3: LẬP TRÌNH BACKEND & CỔNG WEB ADMIN (TUẦN 5 - 6)

- [x] **TSK-11:** Nạp dữ liệu mẫu (Seed Data) phong phú lên Supabase
  - **Phân hệ:** Cơ sở dữ liệu
  - **Mô tả & Output:** Tạo sẵn 8 phòng ban, 10 chức vụ, 4 vai trò RBAC, 3 mẫu ca 24/7, 20 nhân viên, 20 hợp đồng lao động
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 06/10/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-12:** Lập trình Xác thực đăng nhập & Phân quyền RBAC
  - **Phân hệ:** Web Admin (MVC)
  - **Mô tả & Output:** Trang đăng nhập Web Admin sang trọng, mã hóa SHA-256, kiểm tra quyền hạn 4 vai trò (Admin, HR, Manager, Employee), Cookie Authentication
  - **Người phụ trách:** Võ Huỳnh Minh Sang | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 06/10/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-13:** Xây dựng Web Admin: Quản trị tài khoản & phân quyền
  - **Phân hệ:** Web Admin (MVC)
  - **Mô tả & Output:** Giao diện Web Admin: Thêm, sửa, khóa tài khoản, cấp vai trò (Admin, HR, Manager, Employee)
  - **Người phụ trách:** Nhóm phát triển | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 13/10/2026
  - **Trạng thái:** `Hoàn thành`

- [x] **TSK-14:** Xây dựng Web Admin: Quản trị Danh mục hệ thống
  - **Phân hệ:** Web Admin (MVC)
  - **Mô tả & Output:** Màn hình Web quản lý phòng ban, chức vụ, ca làm việc, cấu hình khoản phụ cấp/thưởng/phạt
  - **Người phụ trách:** Nguyễn Đình Cường | **Độ ưu tiên:** Cao | **Hạn chót:** 13/10/2026
  - **Trạng thái:** `Hoàn thành`

- [ ] **TSK-15:** Lập trình chức năng Quản lý Nhân viên & HĐLĐ
  - **Phân hệ:** Web Admin / App
  - **Mô tả & Output:** Thêm/sửa/xóa hồ sơ nhân viên, tìm kiếm, lọc; Lập & theo dõi thời hạn hợp đồng lao động
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Cao | **Hạn chót:** 13/10/2026
  - **Trạng thái:** `Chưa bắt đầu`

---

### GIAI ĐOẠN 4: LẬP TRÌNH MOBILE APP CHẤM CÔNG & DUYỆT ĐƠN (TUẦN 7 - 8)

- [x] **TSK-16:** Lập trình Module Lập lịch phân ca làm việc 24/7
  - **Phân hệ:** Web Admin (MVC) & .NET MAUI
  - **Mô tả & Output:** Giao diện Ma trận Lịch Tuần 24/7 và Danh sách phân ca: Xem và xếp lịch ca sáng/chiều/đêm theo tuần/tháng cho nhân sự, tự động tính ca đêm 30%, kết nối Supabase Cloud
  - **Người phụ trách:** Nhóm phát triển | **Độ ưu tiên:** Cao | **Hạn chót:** 20/10/2026
  - **Trạng thái:** `Hoàn thành (Chờ duyệt)`

- [ ] **TSK-17:** Lập trình App: Module Chấm công Check-in/Check-out
  - **Phân hệ:** App .NET MAUI
  - **Mô tả & Output:** Nút bấm quẹt thẻ trên điện thoại, tự động đối soát ca, tính số phút đi trễ, về sớm, giờ OT
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 20/10/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-18:** Lập trình App: Cổng nộp đơn điện tử của Nhân viên
  - **Phân hệ:** App .NET MAUI
  - **Mô tả & Output:** Form gửi 4 loại đơn (nghỉ phép, bệnh, thai sản, thôi việc); Xem lịch sử và trạng thái duyệt
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Cao | **Hạn chót:** 27/10/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [x] **TSK-19:** Lập trình App: Màn hình Duyệt đơn cho Manager & HR
  - **Phân hệ:** App .NET MAUI
  - **Mô tả & Output:** Danh sách đơn chờ duyệt, thông báo đẩy (Notification), thao tác Phê duyệt / Từ chối kèm lý do
  - **Người phụ trách:** Nguyễn Đình Cường | **Độ ưu tiên:** Cao | **Hạn chót:** 27/10/2026
  - **Trạng thái:** `Hoàn thành`

- [ ] **TSK-20:** Liên kết tự động Đơn nghỉ phép đã duyệt vào Chấm công
  - **Phân hệ:** C# Logic
  - **Mô tả & Output:** Đơn nghỉ có hưởng lương tự động cộng vào ngày công tính lương của nhân viên trên Supabase
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Trung bình | **Hạn chót:** 27/10/2026
  - **Trạng thái:** `Chưa bắt đầu`

---

### GIAI ĐOẠN 5: TÍNH LƯƠNG 2 TẦNG & XUẤT PHIẾU LƯƠNG PDF (TUẦN 9)

- [ ] **TSK-21:** Lập trình C# thuật toán Tính lương tự động 2 tầng
  - **Phân hệ:** Nghiệp vụ Tiền lương
  - **Mô tả & Output:** Tính toán: Lương công + Tăng ca OT + Phụ cấp ca đêm 30% + Thưởng - Trừ BHXH 10.5% - Phạt
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 03/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-22:** Quản lý kỳ lương & Chốt khóa sổ bảng lương tháng
  - **Phân hệ:** Tiền lương
  - **Mô tả & Output:** Tạo kỳ lương tháng, duyệt bảng lương, lưu snapshot vào bảng `CHI_TIET_PHIEU_LUONG`
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Cao | **Hạn chót:** 03/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-23:** Lập trình chức năng Xuất phiếu lương PDF (QuestPDF)
  - **Phân hệ:** Xuất PDF
  - **Mô tả & Output:** Tích hợp QuestPDF: Sinh file PDF phiếu lương tháng và in bảng tổng hợp năm
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 03/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-24:** Lập trình Dashboard & Biểu đồ thống kê nhân sự
  - **Phân hệ:** Báo cáo Thống kê
  - **Mô tả & Output:** Thống kê số lượng NV, phòng ban, trình độ, thâm niên, quỹ lương, chấm công trên Web & App
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Cao | **Hạn chót:** 03/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

---

### GIAI ĐOẠN 6: KIỂM THỬ TOÀN DIỆN & CHẤM ĐỒ ÁN CHÍNH THỨC (TUẦN 10)

- [ ] **TSK-25:** Kiểm thử toàn diện luồng nghiệp vụ trên cả Web & Mobile
  - **Phân hệ:** Kiểm thử
  - **Mô tả & Output:** Chạy kịch bản từ tạo NV -> xếp ca -> chấm công -> gửi đơn -> duyệt -> tính lương -> in PDF
  - **Người phụ trách:** Cả nhóm | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 07/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-26:** Thiết lập sẵn sàng 4 tài khoản test đại diện
  - **Phân hệ:** Dữ liệu Demo
  - **Mô tả & Output:** Tạo sẵn tài khoản test cho Admin (Web), HR, Manager FO, Employee kèm dữ liệu thực tế
  - **Người phụ trách:** Cả nhóm | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 08/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-27:** Diễn tập kịch bản demo và Thuyết trình bảo vệ đồ án
  - **Phân hệ:** Thuyết trình
  - **Mô tả & Output:** Bảo vệ đồ án với Giảng viên: Demo Web Admin và Mobile App kết nối chung Supabase Cloud
  - **Người phụ trách:** Cả nhóm | **Độ ưu tiên:** Khẩn cấp | **Hạn chót:** 10/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

---

### GIAI ĐOẠN 7: TỐI ƯU HỆ THỐNG, ĐÓNG GÓI & BÁO CÁO (TUẦN 11 - 12)

- [ ] **TSK-28:** Fix bugs & Điều chỉnh hệ thống theo góp ý của GV
  - **Phân hệ:** Tối ưu phần mềm
  - **Mô tả & Output:** Tiếp thu ý kiến nhận xét của thầy cô ở buổi chấm Tuần 10, hoàn thiện hệ thống
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Trung bình | **Hạn chót:** 17/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-29:** Viết Tài liệu Hướng dẫn Cài đặt & Hướng dẫn sử dụng
  - **Phân hệ:** Tài liệu kỹ thuật
  - **Mô tả & Output:** Hướng dẫn cài đặt môi trường .NET, cấu hình Supabase và tài liệu HDSD cho Web & App
  - **Người phụ trách:** _Chưa phân công_ | **Độ ưu tiên:** Cao | **Hạn chót:** 24/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

- [ ] **TSK-30:** Đóng gói mã nguồn Visual Studio, SQL và in Báo cáo
  - **Phân hệ:** Tổng kết đề tài
  - **Mô tả & Output:** Gói toàn bộ Source Code, script SQL, file readme.txt và xuất bản in nộp khoa CNTT
  - **Người phụ trách:** Cả nhóm | **Độ ưu tiên:** Cao | **Hạn chót:** 30/11/2026
  - **Trạng thái:** `Chưa bắt đầu`

---

## 📌 HƯỚNG DẪN DÀNH CHO THÀNH VIÊN NHÓM KHI LÀM XONG TASK

1. Mở file `TASKS.md` trên GitHub hoặc Visual Studio Code.
2. Tìm đến mã Task vừa làm xong (ví dụ: `TSK-11`).
3. Chuyển `- [ ]` thành `- [x]`.
4. Cập nhật `Người phụ trách` và đổi `Trạng thái: Hoàn thành`.
5. Thực hiện commit theo cú pháp:
   ```bash
   git add TASKS.md
   git commit -m "docs: hoan thanh TSK-xx [ten cong viec]"
   git push origin main
   ```
