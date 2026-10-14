# PRD 01: Tổng Quan Doanh Nghiệp, Bài Toán Quản Trị Nhân Sự (HRM) & Kiến Trúc 11 Phân Hệ

## 1. Giới Thiệu Doanh Nghiệp Khách Sạn The OverTime Hotel

**The OverTime Hotel** là doanh nghiệp hoạt động trong lĩnh vực kinh doanh dịch vụ khách sạn và lưu trú tiêu chuẩn quốc tế 5 sao. Khách sạn cung cấp hệ sinh thái dịch vụ cao cấp đa dạng bao gồm:
- Dịch vụ lưu trú phòng khách sang trọng (Deluxe, Suite, Executive, Presidential Suite).
- Tiếp đón lễ tân tiền sảnh chuẩn mực quốc tế (Front Office).
- Buồng phòng và vệ sinh tiêu chuẩn nghiêm ngặt (Housekeeping).
- Ẩm thực nhà hàng & quầy Bar cao cấp (Food & Beverage - F&B).
- Trung tâm hội nghị, tiệc cưới và sự kiện quốc tế (Banquet, Events & MICE).
- Dịch vụ thư giãn, chăm sóc sức khỏe và phục hồi năng lượng (Spa, Gym, Bể bơi vô cực).
- Vận hành kỹ thuật tòa nhà và an ninh bảo vệ chuyên nghiệp 24/7 (Engineering & Security).

Do đặc thù ngành dịch vụ khách sạn nghỉ dưỡng cao cấp, hoạt động đón tiếp và phục vụ khách hàng diễn ra **liên tục 24 giờ mỗi ngày (24/7/365)**. Khách sạn không ngừng hoạt động vào dịp Lễ, Tết — mọi ca trực vẫn phải bố trí đầy đủ nhân lực thường trực. Nguồn nhân lực chuyên nghiệp, kỷ luật và tận tâm là tài sản cốt lõi quyết định chất lượng dịch vụ và uy tín thương hiệu của The OverTime Hotel.

> **Quy ước ngày Lễ, Tết trong hệ thống:** Nhân viên **đi làm** vào ngày Lễ, Tết thì giờ làm việc thực tế được ghi nhận tăng ca với **hệ số OT 3.0**; nhân viên **không đi làm** được hưởng chế độ **nghỉ Lễ, Tết có hưởng lương bình thường** theo quy định của Bộ luật Lao động, không phát sinh phụ trội OT. (Chi tiết quy định tại **PRD 05 — Quy Trình Và Công Thức Tính Lương**).

---

## 2. Bối Cảnh Đặc Thù Ngành Khách Sạn 5 Sao: Mảng Sự Kiện & Tiệc (Banquet, Events & MICE)

Bên cạnh hoạt động lưu trú và ẩm thực thường nhật, mảng kinh doanh **Hội nghị, Tiệc cưới & Sự kiện (Banquet, Events & MICE - Meetings, Incentives, Conferences, Exhibitions)** đóng vai trò trọng yếu, chiếm tỷ trọng lớn trong cơ cấu doanh thu và tạo nên danh tiếng đẳng cấp của khách sạn 5 sao The OverTime Hotel.

### 2.1. Tính Chất Đột Xuất & Khối Lượng Công Việc Đột Biến
Các sự kiện quy mô lớn diễn ra tại khách sạn rất đa dạng về hình thức và yêu cầu nghiệp vụ:
- Tiệc cưới hoàng gia cao cấp với hàng nghìn khách mời.
- Gala dinner doanh nghiệp, tiệc tri ân khách hàng cuối năm.
- Hội nghị thượng đỉnh quốc tế, diễn đàn kinh tế, hội thảo khoa học đa quốc gia.
- Lễ hội ẩm thực quốc tế, tuần lễ văn hóa, đón tiếp các đoàn khách VIP ngoại giao, chính khách và nghệ sĩ quốc tế.

Các sự kiện này diễn ra theo từng thời điểm, có thời hạn chuẩn bị gấp rút và đòi hỏi cường độ làm việc tập trung cao độ vượt xa năng lực xử lý của một ca trực thông thường.

### 2.2. Nhu Cầu Thành Lập Đội Đặc Nhiệm Liên Phòng Ban (Task Force Team)
Một sự kiện quy mô lớn không thể hoàn thành nếu chỉ giao cho một bộ phận đơn lẻ. Khách sạn bắt buộc phải kích hoạt cơ chế thành lập **Đội đặc nhiệm sự kiện (Task Force Team)**, huy động và điều động nhân sự linh hoạt từ nhiều phòng ban khác nhau cùng tham gia:
- **Bộ phận Tiền sảnh (Front Office):** Đón tiếp khách VIP tại sảnh chính, hướng dẫn luồng di chuyển, hỗ trợ thủ tục check-in sự kiện và quản lý hành lý.
- **Bộ phận Buồng phòng (Housekeeping):** Set-up khăn trải bàn, đồ vải cao cấp, trang trí hoa tươi, đảm bảo vệ sinh sảnh tiệc và khu vực công cộng trước, trong và sau sự kiện.
- **Bộ phận Nhà hàng & Bếp (F&B):** Bếp trưởng và phụ bếp chuẩn bị thực đơn tiệc số lượng lớn theo tiêu chuẩn an toàn thực phẩm khắt khe; nhân viên phục vụ bàn tiệc (banquet servers) phối hợp nhịp nhàng phục vụ hàng trăm bàn ăn cùng lúc.
- **Bộ phận Kỹ thuật - Âm thanh ánh sáng (Engineering):** Vận hành hệ thống màn hình LED, âm thanh sân khấu, chiếu sáng mỹ thuật, kiểm soát nhiệt độ điều hòa trung tâm và phòng ngừa sự cố mất điện.
- **Bộ phận An ninh - Bảo vệ (Security):** Thiết lập phương án phân luồng xe, kiểm soát an ninh cổng ra vào, bảo vệ yếu nhân (VIP escort) và túc trực phòng cháy chữa cháy xuyên suốt sự kiện.

### 2.3. Cơ Chế Thù Lao & Phụ Cấp Dự Án / Sự Kiện (Event Allowance / Project Remuneration)
Để khuyến khích tinh thần cống hiến, duy trì thái độ phục vụ tận tâm và bù đắp áp lực cường độ cao cho nhân sự tham gia Đội đặc nhiệm, khách sạn áp dụng cơ chế quản lý dự án/sự kiện minh bạch:
- Ghi nhận và phân định rõ vai trò của từng thành viên trong sự kiện (Trưởng ban điều phối, Trưởng nhóm kỹ thuật, Nhân sự phục vụ chính, Hỗ trợ hậu cần...).
- Theo dõi số giờ công đóng góp hoặc ngày công phục vụ thực tế cho từng dự án/sự kiện.
- Tính toán chi trả **Khoản thù lao sự kiện / Phụ cấp dự án (Event Allowance)** trực tiếp cho nhân viên dựa trên ngân sách sự kiện đã duyệt và mức độ hoàn thành nhiệm vụ.
- Toàn bộ khoản thù lao này được tự động kết chuyển vào bảng lương của kỳ tính lương tương ứng, đảm bảo quyền lợi thu nhập minh bạch, chuẩn xác cho người lao động.

---

## 3. Đặc Điểm Nhân Sự & Tổ Chức Ca Làm Việc 24/7

Khác với các doanh nghiệp văn phòng làm việc giờ hành chính cố định (08:00 – 17:00), nhân sự tại The OverTime Hotel làm việc xoay vòng theo **3 ca chính liên tục 24/7**:

| Ca Làm Việc | Khung Giờ Làm Việc | Đặc Thù Nghiệp Vụ Khách Sạn 5 Sao | Chế Độ Phụ Cấp Pháp Lý |
|:---:|:---:|:---|:---:|
| **Ca Sáng (Ca 1)** | 06:00 – 14:00 | Chuẩn bị và phục vụ buffet sáng, tiếp đón check-in sớm, dọn phòng buổi sáng, bàn giao kỹ thuật đầu ngày. | Hưởng lương theo ngày công chuẩn |
| **Ca Chiều (Ca 2)** | 14:00 – 22:00 | Cao điểm khách nhận/trả phòng, phục vụ bữa tối tại nhà hàng, tổ chức các buổi tiệc cưới, hội nghị và gala. | Hưởng lương theo ngày công chuẩn |
| **Ca Đêm (Ca 3)** | 22:00 – 06:00 (sáng hôm sau) | Trực quầy lễ tân đêm, kiểm toán đêm (night audit), tuần tra an ninh trật tự, giám sát hệ thống kỹ thuật cơ điện tòa nhà. | **Phụ cấp ca đêm thêm 30%** lương cơ bản theo đúng Bộ luật Lao động |

*Lưu ý:* Các bộ phận Tiền sảnh (FO), An ninh (Security) và Kỹ thuật (Engineering) bắt buộc bố trí nhân sự thường trực 24/24 để đảm bảo an toàn tuyệt đối và duy trì cam kết chất lượng dịch vụ 5 sao.

---

## 4. Cơ Cấu Tổ Chức Các Khối Nghiệp Vụ

Khách sạn tổ chức theo mô hình quản trị chuyên môn hóa phân cấp chặt chẽ:

1. **Ban Giám Đốc (General Management):** Cơ quan lãnh đạo cao nhất, định hướng chiến lược kinh doanh dịch vụ, phê duyệt định biên nhân sự toàn khách sạn và phê duyệt hạn mức ngân sách quỹ lương hàng tháng.
2. **Phòng Hành Chính - Nhân Sự (HR Department):** Chịu trách nhiệm tiếp nhận nhân sự, ký kết và quản lý HĐLĐ, giám sát chuyên cần, thiết lập bảng lương, thực thi công thức tính lương tự động, giải quyết chế độ phúc lợi và lập báo cáo quản trị nhân sự.
3. **Bộ Phận Tiền Sảnh (Front Office - FO):** Trực tiếp tạo ấn tượng đầu tiên và sau cùng cho khách lưu trú, gồm Lễ tân, Trưởng ca lễ tân (Duty Manager), Chăm sóc khách hàng (Concierge), Nhân viên đón tiếp hành lý (Bellman), Tổng đài viên (Operator).
4. **Bộ Phận Buồng Phòng (Housekeeping - HK):** Duy trì tiêu chuẩn sạch sẽ, vệ sinh và tiện nghi đẳng cấp, gồm Giám sát tầng, Nhân viên làm phòng, Nhân viên giặt ủi đồ vải, Nhân viên vệ sinh khu vực công cộng.
5. **Bộ Phận Nhà Hàng & Bếp (F&B):** Cung cấp trải nghiệm ẩm thực 5 sao, gồm Bếp trưởng, Bếp phó, Đầu bếp, Phụ bếp, Nhân viên pha chế (Bartender), Thu ngân và Đội ngũ nhân viên phục vụ bàn tiệc (Banquet Servers).
6. **Bộ Phận Kỹ Thuật - Bảo Trì (Engineering):** Bảo đảm vận hành thông suốt cơ sở hạ tầng vật lý, hệ thống điện lạnh trung tâm, hệ thống cấp thoát nước, thang máy, thiết bị âm thanh ánh sáng hội trường và phòng chống cháy nổ.
7. **Bộ Phận An Ninh - Bảo Vệ (Security):** Kiểm soát an ninh cổng ra vào, tuần tra bảo vệ tài sản, giám sát hệ thống camera an ninh 24/7 và sẵn sàng xử lý các tình huống khẩn cấp.
8. **Phòng Tài Chính - Kế Toán (Finance & Accounting):** Kiểm toán doanh thu, đối soát dữ liệu bảng lương từ phòng HR, hạch toán chi phí lương - thưởng - phụ cấp và thực hiện chi trả lương qua ngân hàng.
9. **Bộ Phận Công Nghệ Thông Tin (IT):** Quản trị hạ tầng mạng, máy chủ Web Admin, an toàn thông tin CSDL Supabase, và chịu trách nhiệm **cấp phát tài khoản đăng nhập Mobile App** cho nhân sự (tiếp nhận yêu cầu từ Phòng HR sau khi hợp đồng lao động được duyệt). Vai trò RBAC tương ứng là **IT** — tách biệt hoàn toàn khỏi Bộ phận Kỹ thuật tòa nhà nhằm đảm bảo tính toàn vẹn và bảo mật phân quyền.
10. **Ban Điều Phối Dự Án / Sự Kiện & Đội Đặc Nhiệm (Banquet & Event Task Force Committee):** Cơ chế phối hợp liên bộ phận nhằm điều hành các sự kiện MICE, tiệc cưới và gala quy mô lớn; quản lý tiến độ, phân công vai trò và nghiệm thu thù lao dự án.

---

## 5. Kiến Trúc 11 Phân Hệ Chức Năng Cốt Lõi

Nhằm đáp ứng toàn diện bài toán quản trị nhân lực đặc thù của khách sạn 5 sao, hệ thống HRM The OverTime Hotel được chuẩn hóa thành **11 phân hệ chức năng chính**:

```text
========================================================================================
            HỆ THỐNG QUẢN LÝ NHÂN SỰ & TIỀN LƯƠNG THE OVERTIME HOTEL
========================================================================================
  [01] Quản lý Nhân sự & Hợp đồng Lao động (HĐLĐ)
  [02] Tài khoản & Phân quyền RBAC (5 Vai trò, 17 Mã quyền, Quy trình HR -> IT)
  [03] Ca làm & Lịch phân ca 24/7 (Kế hoạch xếp ca Sáng/Chiều/Đêm xoay vòng)
  [04] Bảng Chấm công 24/7 & Chuyên cần (Dữ liệu quẹt thẻ thực tế, GPS/Wifi, Trễ/Sớm, OT)
  [05] Quản lý Đơn từ điện tử (4 loại đơn chuẩn hóa, xét duyệt 2 cấp)
  [06] Tính toán Lương tự động hàng tháng & Phiếu lương PDF (Gross -> Net, QuestPDF)
  [07] Quản lý Phụ cấp (Ca đêm +30%, ăn ca 30k/ngày, trách nhiệm, độc hại, đồng phục...)
  [08] Quản lý Tiền thưởng (KPI hiệu suất, buồng phòng vượt chỉ tiêu, tiệc cưới, Lễ Tết)
  [09] Quản lý Dự án / Sự kiện Khách sạn (Event & Project Management: MICE, Task Force)
  [10] Báo cáo & Thống kê Quản trị (Dashboard trực quan, biến động NS, quỹ lương)
  [11] Quản trị Hệ thống (Danh mục, tham số lương/phạt, Audit Log, sao lưu CSDL)
========================================================================================
```

### 5.1. Phân Hệ 1: Quản Lý Nhân Viên & Hợp Đồng Lao Động (HĐLĐ)
- **Hồ sơ lý lịch số hóa:** Quản lý toàn diện thông tin cá nhân (họ tên, ngày sinh, giới tính, CCCD/CMND, quê quán, địa chỉ thường trú, trình độ học vấn, số điện thoại, email liên hệ, tài khoản ngân hàng).
- **Phân loại tổ chức:** Gán nhân viên vào phòng ban trực thuộc và chức danh công việc tương ứng; quản lý trạng thái công tác (Đang làm việc, Tạm hoãn, Đã nghỉ việc).
- **Vòng đời hợp đồng lao động:** Quản lý các loại hợp đồng (Thử việc, 1 năm, 3 năm, Vô thời hạn), ghi nhận mức lương cơ bản pháp lý làm căn cứ đóng bảo hiểm và tính lương.
- **Cảnh báo hạn hợp đồng tự động:** Tự động lọc và hiển thị cảnh báo danh sách hợp đồng sắp hết hạn trước 30 ngày để phòng HR chủ động chuẩn bị thủ tục tái ký hoặc thanh lý.
- **Điều chuyển nội bộ:** Ghi nhận lịch sử luân chuyển phòng ban, bổ nhiệm chức vụ và thay đổi mức lương của nhân sự trong suốt quá trình công tác tại khách sạn.

### 5.2. Phân Hệ 2: Tài Khoản & Phân Quyền RBAC (5 Vai Trò, 17 Mã Quyền)
- **Xác thực danh tính hai lớp bảo mật:**
  * Lớp 1 (Web Admin): Đăng nhập bằng `ten_dang_nhap` và mật khẩu băm bảo mật tại bảng `tai_khoan`.
  * Lớp 2 (Mobile App): Đăng nhập bằng email công vụ qua hệ thống xác thực `auth.users` của Supabase.
- **5 nhóm vai trò chuẩn hóa trong ma trận RBAC:**
  1. *Quản trị viên (Admin):* Toàn quyền cấu hình hệ thống, quản lý tài khoản, danh mục tổ chức và xem nhật ký kiểm toán.
  2. *Phòng Nhân sự (HR):* Toàn quyền nghiệp vụ hồ sơ nhân viên, hợp đồng, gửi yêu cầu cấp tài khoản, duyệt đơn cấp cao, tính lương và phát hành phiếu lương.
  3. *Quản lý Bộ phận (Manager):* Xếp lịch phân ca, theo dõi chấm công bộ phận, duyệt đơn xin nghỉ phép/nghỉ ốm của nhân viên trực thuộc.
  4. *Nhân viên (Employee):* Quẹt thẻ chấm công di động, xem lịch phân ca 24/7, nộp đơn từ điện tử, tra cứu chi tiết phiếu lương cá nhân.
  5. *Bộ phận Công nghệ Thông tin (IT):* Quản trị kỹ thuật, hỗ trợ vận hành và độc quyền nắm giữ thẩm quyền tiếp nhận yêu cầu, cấp phát tài khoản Mobile App.
- **Tập 17 mã quyền chuẩn (`PermissionCodes`):**
  * Nhóm Quản trị hệ thống: `SYS_ADMIN`, `QL_TAI_KHOAN`, `CAP_TAI_KHOAN_MOBILE`, `QL_PHAN_QUYEN`, `QL_DANH_MUC`, `SAO_LUU_CSDL`.
  * Nhóm Quản trị nhân sự: `QL_NHAN_SU`, `QL_HOP_DONG`.
  * Nhóm Vận hành ca & chấm công: `XEP_CA_LAM`, `XEM_CHAM_CONG_BO_PHAN`, `CHAM_CONG_DI_DONG`.
  * Nhóm Lương & Phiếu lương: `TINH_LUONG`, `XUAT_PHIEU_LUONG_PDF`.
  * Nhóm Phê duyệt & Đơn từ: `DUYET_THOI_VIEC`, `DUYET_DON_NGHI`, `DUYET_TANG_CA_OT`, `NOP_DON_TU`.
- **Quy trình chuẩn hóa cấp tài khoản (HR gửi yêu cầu -> IT cấp phát):**
  * Sau khi ký kết hoặc gia hạn HĐLĐ, Phòng HR gửi yêu cầu cấp tài khoản qua màn hình `/YeuCauTaiKhoan` (ghi nhận tại bảng `yeu_cau_tai_khoan` với trạng thái `CHO_IT`).
  * Bộ phận IT tiếp nhận yêu cầu, kiểm tra tính hợp lệ và thực hiện lệnh cấp tài khoản Mobile App (`auth.users`).
  * Mã quyền `CAP_TAI_KHOAN_MOBILE` được tách biệt hoàn toàn khỏi `QL_TAI_KHOAN`: HR chỉ có quyền quản lý hồ sơ nhân sự, không có thẩm quyền tự kích hoạt tài khoản truy cập; chỉ Admin và IT mới có quyền mở cánh cổng kết nối Supabase RLS.

### 5.3. Phân Hệ 3: Ca Làm & Lịch Phân Ca 24/7 (Kế Hoạch Xếp Ca)
- **Danh mục mẫu ca chuẩn 24/7:** Định nghĩa khung thời gian ca Sáng (06:00 – 14:00), ca Chiều (14:00 – 22:00) và ca Đêm (22:00 – 06:00 sáng hôm sau).
- **Lập kế hoạch xếp ca linh hoạt:** Cho phép Quản lý bộ phận xếp ca trước theo chu kỳ tuần hoặc tháng cho từng nhân sự trực thuộc; hiển thị trực quan dưới dạng ma trận lịch biểu (Matrix Grid) và dòng thời gian (Timeline).
- **Cân đối định biên và phân bổ công bằng:** Hỗ trợ kiểm tra số ca đêm, số ngày nghỉ tuần (off-day) của từng nhân viên, đảm bảo tuân thủ quy định lao động và sự công bằng nội bộ.
- **Điều chỉnh và đổi ca trực:** Hỗ trợ quản lý ghi nhận điều chuyển ca làm việc khi có sự thay đổi đột xuất giữa các nhân viên trong bộ phận.

### 5.4. Phân Hệ 4: Bảng Chấm Công 24/7 & Chuyên Cần (Dữ Liệu Thực Tế)
- **Chấm công di động thông minh:** Nhân viên thực hiện Check-in / Check-out trực tiếp trên ứng dụng di động .NET MAUI.
- **Xác thực vị trí làm việc (Geofencing & Wifi công vụ):** Kiểm tra bán kính tọa độ GPS trong khuôn viên khách sạn và nhận diện BSSID mạng Wifi nội bộ được cấp phép để ngăn chặn gian lận chấm công hộ.
- **Đối soát tự động với ca phân công:** Hệ thống tự động khớp bản ghi quẹt thẻ với lịch phân ca đã lập để xác định ca làm việc tương ứng.
- **Tự động bắt trễ / về sớm:** Đo lường chính xác số phút đi trễ, về sớm so với giờ bắt đầu và kết thúc quy định của ca làm.
- **Tính toán giờ làm thêm (OT):** Tự động tính số giờ làm vượt ca và áp dụng hệ số làm thêm theo luật định: ngày thường (x1.5), ngày nghỉ tuần (x2.0), ngày Lễ, Tết (x3.0).

### 5.5. Phân Hệ 5: Quản Lý Đơn Từ Điện Tử
- **Số hóa 4 loại đơn chuẩn:**
  1. *Đơn xin nghỉ phép năm:* Trừ vào quỹ phép năm định mức (12 ngày/năm + thâm niên).
  2. *Đơn xin nghỉ ốm:* Hưởng chế độ trợ cấp ốm đau theo quy định Bảo hiểm Xã hội (BHXH).
  3. *Đơn xin nghỉ thai sản:* Hưởng chế độ thai sản theo quy định pháp lý hiện hành.
  4. *Đơn xin thôi việc:* Khởi tạo quy trình bàn giao công việc và chuẩn bị quyết toán hợp đồng.
- **Quy trình xét duyệt phân cấp chặt chẽ:**
  * Quản lý bộ phận duyệt đơn Nghỉ phép và Nghỉ ốm của nhân viên trong bộ phận mình.
  * Phòng HR duyệt đơn Thai sản, Thôi việc và các đơn vượt cấp trên toàn khách sạn.
  * Quy tắc bảo mật nghiệp vụ: Tuyệt đối không cá nhân nào có thể tự phê duyệt đơn từ do chính mình tạo ra.

### 5.6. Phân Hệ 6: Tính Toán Lương Tự Động Hàng Tháng & Phiếu Lương PDF
- **Công thức tính toán 2 tầng (Gross -> Net):**
  * Tầng 1: Tổng thu nhập (Gross) = Lương ngày công thực tế + Tiền làm thêm giờ (OT 1.5 - 3.0) + Phụ cấp ca đêm (+30%) + Phụ cấp ăn ca + Các khoản phụ cấp trách nhiệm/độc hại + Tiền thưởng KPI/buồng phòng/Lễ Tết + Thù lao dự án/sự kiện.
  * Tầng 2: Thu nhập thực nhận (Net) = Gross - Tổng các khoản khấu trừ (Bảo hiểm bắt buộc 10.5% gồm BHXH 8%, BHYT 1.5%, BHTN 1% + Tiền phạt vi phạm trễ/sớm + Tạm ứng + Thuế thu nhập cá nhân TNCN theo biểu lũy tiến từng phần).
- **Quy trình khóa sổ kỳ lương (Payroll Lock):** Đóng kỳ lương để bảo toàn dữ liệu lịch sử, ngăn chặn mọi sửa đổi trái phép sau khi đã đối soát hoàn tất.
- **Xuất phiếu lương tiêu chuẩn QuestPDF:** Sinh file PDF phiếu lương chi tiết từng tháng với layout sang trọng, đầy đủ chi tiết thu chi, mã vạch tra cứu; hỗ trợ xuất bảng tổng hợp thu nhập năm phục vụ quyết toán thuế TNCN.
- **Tra cứu di động:** Nhân viên tra cứu tức thì phiếu lương chi tiết của mình trên ứng dụng di động ngay khi kỳ lương được ban hành.

### 5.7. Phân Hệ 7: Quản Lý Phụ Cấp
- **Phụ cấp ca đêm pháp lý (+30%):** Tự động tính toán phụ trội 30% mức lương theo giờ cho toàn bộ các giờ làm việc nằm trong khung ca đêm (22:00 – 06:00).
- **Phụ cấp ăn ca định mức (30.000 VNĐ/ngày):** Tự động cộng dồn theo số ngày công làm việc thực tế của nhân viên trong tháng.
- **Các khoản phụ cấp cố định theo chức danh:** Phụ cấp trách nhiệm quản lý, phụ cấp độc hại (khu vực giặt ủi hóa chất, kho lạnh F&B), phụ cấp trang phục/đồng phục, phụ cấp xăng xe đi lại và điện thoại liên lạc.
- **Gắn cờ miễn/chịu thuế TNCN:** Tự động phân định khoản phụ cấp nào được miễn thuế (ăn ca trong định mức, trang phục trong định mức) và khoản nào tính vào thu nhập chịu thuế theo đúng Thông tư của Bộ Tài chính.

### 5.8. Phân Hệ 8: Quản Lý Tiền Thưởng
- **Thưởng hiệu suất công việc (KPI):** Ghi nhận mức đánh giá hoàn thành nhiệm vụ hàng tháng của từng nhân sự từ cấp quản lý để chi trả tiền thưởng năng suất.
- **Thưởng định mức buồng phòng (Housekeeping Incentive):** Thưởng vượt định mức số phòng làm sạch đạt tiêu chuẩn kiểm tra 5 sao trong tháng cao điểm.
- **Thưởng hoa hồng tiệc cưới & sự kiện (Banquet Sales & Service Commission):** Thưởng theo tỷ lệ phần trăm giá trị hợp đồng dịch vụ tiệc và sự kiện thành công.
- **Thưởng Lễ, Tết & Khen thưởng đột xuất:** Ghi nhận các quyết định khen thưởng nhân dịp Quốc khánh, Tết Dương lịch, Tết Nguyên đán hoặc thành tích đặc biệt trong phục vụ khách hàng VIP.

### 5.9. Phân Hệ 9: Quản Lý Dự Án / Sự Kiện Khách Sạn (Event & Project Management)
- **Quản lý danh mục sự kiện & tiệc MICE:** Khởi tạo và theo dõi toàn bộ vòng đời của các sự kiện tại khách sạn (Tên sự kiện, loại hình: Tiệc cưới, Hội nghị MICE, Gala Dinner, Lễ hội ẩm thực; thời gian bắt đầu, kết thúc; địa điểm sảnh tiệc; tổng ngân sách thù lao dự kiến; người phụ trách chính).
- **Thành lập và điều phối Đội đặc nhiệm (Task Force Team):** Cho phép ban tổ chức sự kiện lập danh sách điều động nhân sự liên phòng ban (FO, HK, F&B, Kỹ thuật, An ninh) tham gia hỗ trợ sự kiện mà không làm gián đoạn lịch phân ca thường nhật.
- **Phân công vai trò & Nhiệm vụ cụ thể:** Gán chức danh trong sự kiện cho từng nhân viên (Trưởng ban điều phối, Trưởng nhóm âm thanh ánh sáng, Nhân viên phục vụ bàn tiệc chính, Nhân viên dẫn khách, Giám sát bàn ăn).
- **Ghi nhận giờ công & Đánh giá mức độ đóng góp:** Ghi nhận số giờ làm việc thực tế hoặc ngày công tham gia sự kiện của từng nhân sự kèm đánh giá chất lượng hoàn thành.
- **Tính toán thù lao sự kiện & Kết chuyển tự động:** Tính toán mức thù lao/phụ cấp sự kiện xứng đáng cho từng nhân viên theo vai trò và giờ công đóng góp; tự động kết chuyển khoản tiền này vào bảng tính lương của kỳ tương ứng để thanh toán chung đợt lương tháng.

### 5.10. Phân Hệ 10: Báo Cáo & Thống Kê Quản Trị
- **Dashboard trực quan đa chiều:** Cung cấp cho Ban Giám đốc và Trưởng phòng HR bức tranh toàn cảnh về tình hình nhân sự và chi phí lao động thời gian thực.
- **Báo cáo biến động nhân lực:** Thống kê tỷ lệ tuyển mới, thôi việc, biến động nhân sự theo từng phòng ban và khối nghiệp vụ theo tháng/quý/năm.
- **Báo cáo chuyên cần & Kỷ luật lao động:** Tổng hợp tỷ lệ đi làm đúng giờ, số lần đi trễ, về sớm, số ngày nghỉ phép, tỷ lệ chấp hành ca trực 24/7.
- **Phân tích cơ cấu và quỹ tiền lương:** Báo cáo chi tiết tổng quỹ lương, cơ cấu lương cơ bản, chi phí tăng ca OT, chi phí phụ cấp ca đêm, thù lao sự kiện MICE và các khoản trích nộp bảo hiểm theo bộ phận.
- **Báo cáo hỗ trợ thanh tra & Quyết toán thuế:** Xuất dữ liệu bảng lương tổng hợp năm phục vụ kiểm toán tài chính và quyết toán thuế TNCN cho cơ quan chức năng.

### 5.11. Phân Hệ 11: Quản Trị Hệ Thống
- **Quản lý danh mục nền tảng:** Quản lý danh mục phòng ban, chức vụ, mẫu ca làm việc 24/7, danh mục loại đơn từ điện tử.
- **Cấu hình tham số tài chính:** Thiết lập định mức phụ cấp ăn ca, tỷ lệ phụ cấp ca đêm, hệ số lương tăng ca OT (1.5, 2.0, 3.0), mức phạt trễ/sớm, tỷ lệ đóng bảo hiểm bắt buộc và các bậc thuế TNCN lũy tiến.
- **Nhật ký kiểm toán quản trị (Audit Trail):** Ghi vết chi tiết 100% các hành động thay đổi dữ liệu nhạy cảm (thêm/sửa/xóa tài khoản, thay đổi quyền hạn, điều chỉnh lương, mở/khóa kỳ lương) kèm định danh người thực hiện, thời điểm và giá trị trước/sau.
- **Sao lưu & Phục hồi dữ liệu:** Cung cấp cơ chế sao lưu định kỳ và phục hồi cơ sở dữ liệu an toàn, đảm bảo tính liên tục trong hoạt động vận hành của khách sạn.

---

## 6. Quy Mô Cơ Sở Dữ Liệu: 27 Bảng Chuẩn Hóa 3NF

Cơ sở dữ liệu của hệ thống HRM The OverTime Hotel được thiết kế đạt chuẩn hóa **dạng chuẩn 3 (3NF)** trên nền tảng Supabase Cloud (PostgreSQL Engine), bao gồm **27 bảng** được phân nhóm chặt chẽ theo 11 phân hệ chức năng:

```text
+---------------------------------------------------------------------------------------+
|                 CSDL THE OVERTIME HOTEL HRM - 27 BẢNG CHUẨN 3NF                      |
+---------------------------------------------------------------------------------------+
| [1]  Nhóm Tổ chức & Nhân sự (4 bảng):                                                 |
|      - phong_ban, chuc_vu, nhan_vien, hop_dong                                        |
| [2]  Nhóm Tài khoản & Phân quyền RBAC (5 bảng):                                      |
|      - tai_khoan, vai_tro, quyen, vai_tro_quyen, yeu_cau_tai_khoan                    |
| [3]  Nhóm Ca làm & Chấm công 24/7 (3 bảng):                                          |
|      - ca_lam_viec, phan_ca, cham_cong                                                |
| [4]  Nhóm Đơn từ điện tử (2 bảng):                                                    |
|      - loai_don, don_tu                                                               |
| [5]  Nhóm Dự án / Sự kiện Khách sạn (2 bảng mới):                                     |
|      - du_an, phan_cong_du_an                                                         |
| [6]  Nhóm Cấu hình Lương, Phụ cấp & Khấu trừ (5 bảng):                               |
|      - ky_luong, cau_hinh_khoan_luong, cau_hinh_phu_cap, cau_hinh_khau_tru,          |
|        cau_hinh_tang_ca                                                               |
| [7]  Nhóm Phiếu lương & Chi tiết (5 bảng):                                           |
|      - phieu_luong, chi_tiet_phieu_luong, chi_tiet_tru_bao_hiem,                      |
|        chi_tiet_tru_thue_tncn, chi_tiet_tru_vi_pham_tam_ung                           |
| [8]  Nhóm Quản trị & Nhật ký (1 bảng):                                                |
|      - nhat_ky_quan_tri                                                               |
+---------------------------------------------------------------------------------------+
```

### 6.1. Danh Mục Tổng Hợp 27 Bảng CSDL

| STT | Tên Bảng | Phân Hệ Nghiệp Vụ Tương Ứng | Khóa Chính (PK) | Khóa Ngoại (FK) & Quan Hệ Nghiệp Vụ |
|:---:|:---|:---|:---|:---|
| **1** | `phong_ban` | Phân hệ 1 & 11 | `ma_phong_ban` | Danh mục đơn vị tổ chức trong khách sạn. |
| **2** | `chuc_vu` | Phân hệ 1 & 11 | `ma_chuc_vu` | Danh mục chức danh chuyên môn. |
| **3** | `nhan_vien` | Phân hệ 1 | `ma_nhan_vien` | FK -> `phong_ban`, `chuc_vu`. Hồ sơ lý lịch nhân sự. |
| **4** | `hop_dong` | Phân hệ 1 | `ma_hop_dong` | FK -> `nhan_vien`. Hợp đồng pháp lý và lương cơ bản. |
| **5** | `tai_khoan` | Phân hệ 2 | `ma_tai_khoan` | FK -> `nhan_vien` (1-1), `vai_tro`. Tài khoản Web Admin. |
| **6** | `vai_tro` | Phân hệ 2 | `ma_vai_tro` | 5 nhóm vai trò chuẩn (Admin, HR, Manager, Employee, IT). |
| **7** | `quyen` | Phân hệ 2 | `ma_quyen` | 17 mã quyền hệ thống (`PermissionCodes`). |
| **8** | `vai_tro_quyen` | Phân hệ 2 | `(ma_vai_tro, ma_quyen)` | FK -> `vai_tro`, `quyen`. Bảng gán quyền cho vai trò (RBAC). |
| **9** | `yeu_cau_tai_khoan` | Phân hệ 2 | `ma_yeu_cau` | FK -> `nhan_vien`, `hop_dong`, `vai_tro`, `tai_khoan`. Quy trình HR gửi -> IT cấp. |
| **10** | `ca_lam_viec` | Phân hệ 3 | `ma_ca` | Danh mục 3 ca chuẩn 24/7 (Sáng, Chiều, Đêm). |
| **11** | `phan_ca` | Phân hệ 3 | `ma_phan_ca` | FK -> `nhan_vien`, `ca_lam_viec`. Lịch xếp ca tuần/tháng. |
| **12** | `cham_cong` | Phân hệ 4 | `ma_cham_cong` | FK -> `phan_ca` (1-1). Quẹt thẻ check-in/out, GPS/Wifi, trễ/sớm, OT. |
| **13** | `loai_don` | Phân hệ 5 | `ma_loai_don` | 4 loại đơn: Phép năm, Nghỉ ốm, Thai sản, Thôi việc. |
| **14** | `don_tu` | Phân hệ 5 | `ma_don` | FK -> `nhan_vien`, `loai_don`, `nguoi_duyet`. Quản lý xét duyệt đơn. |
| **15** | `du_an` | Phân hệ 9 | `ma_du_an` | FK -> `nhan_vien` (người quản lý). Sự kiện tiệc cưới, MICE, Gala. |
| **16** | `phan_cong_du_an` | Phân hệ 9 | `ma_phan_cong` | FK -> `du_an`, `nhan_vien`. Điều động Đội đặc nhiệm Task Force. |
| **17** | `ky_luong` | Phân hệ 6 | `ma_ky_luong` | Chu kỳ khóa sổ tính lương theo tháng (Tháng/Năm). |
| **18** | `cau_hinh_khoan_luong` | Phân hệ 6, 7, 8 | `ma_khoan` | Định mức phụ cấp, tiền thưởng và các khoản khấu trừ. |
| **19** | `cau_hinh_phu_cap` | Phân hệ 7 | `ma_phu_cap` | Tham số phụ cấp ca đêm, ăn ca, kèm cờ chịu thuế TNCN. |
| **20** | `cau_hinh_khau_tru` | Phân hệ 6 | `ma_khoan_tru` | Tham số khoản khấu trừ (BHXH, BHYT, thuế, phạt). |
| **21** | `cau_hinh_tang_ca` | Phân hệ 4 & 6 | `ma_loai_ot` | Danh mục loại tăng ca và hệ số lương OT (1.5, 2.0, 3.0). |
| **22** | `phieu_luong` | Phân hệ 6 | `ma_phieu_luong` | FK -> `nhan_vien`, `ky_luong`. Tổng hợp Gross, Khấu trừ, Net. |
| **23** | `chi_tiet_phieu_luong` | Phân hệ 6 | `ma_chi_tiet` | FK -> `phieu_luong`, `cau_hinh_khoan_luong`. Bản ghi chi tiết thu chi. |
| **24** | `chi_tiet_tru_bao_hiem` | Phân hệ 6 | `ma_chi_tiet_bh` | FK -> `chi_tiet_phieu_luong` (1-1). Tách 3 khoản BHXH, BHYT, BHTN. |
| **25** | `chi_tiet_tru_thue_tncn` | Phân hệ 6 | `ma_chi_tiet_thue` | FK -> `chi_tiet_phieu_luong` (1-1). Tính thuế theo biểu lũy tiến từng phần. |
| **26** | `chi_tiet_tru_vi_pham_tam_ung` | Phân hệ 6 | `ma_chi_tiet_vp` | FK -> `chi_tiet_phieu_luong` (1-1). Khấu trừ vi phạm trễ/sớm và tạm ứng. |
| **27** | `nhat_ky_quan_tri` | Phân hệ 11 | `ma_nhat_ky` | FK -> `tai_khoan`. Nhật ký kiểm toán thao tác hệ thống (Audit Log). |

### 6.2. Đặc Tả 2 Bảng Mới Cho Phân Hệ Quản Lý Dự Án & Sự Kiện Khách Sạn

Nhằm hiện thực hóa phân hệ **9.0 Quản lý Dự án / Sự kiện Khách sạn (Event & Project Management)**, CSDL bổ sung 2 bảng chuyên trách:

#### Bảng `du_an` (Dự án & Sự kiện Khách sạn)
Lưu trữ thông tin chi tiết về các sự kiện tiệc cưới, gala, hội nghị quốc tế và MICE:
- `ma_du_an` (SERIAL, PRIMARY KEY): Mã định danh duy nhất của dự án/sự kiện.
- `ten_du_an` (VARCHAR(200), NOT NULL): Tên sự kiện (ví dụ: "Tiệc Cưới Hoàng Gia Kim Cương", "Hội Nghị Thượng Đỉnh Kinh Tế ASEAN 2026").
- `loai_su_kien` (VARCHAR(50), NOT NULL): Phân loại sự kiện (`TIEC_CUOI`, `GALA_DINNER`, `HOI_NGHI_MICE`, `LE_HOI_AM_THUC`, `DON_DOAN_VIP`).
- `ngay_bat_dau` (TIMESTAMP WITH TIME ZONE, NOT NULL): Thời điểm bắt đầu công tác set-up và đón khách.
- `ngay_ket_thuc` (TIMESTAMP WITH TIME ZONE, NOT NULL): Thời điểm kết thúc và thu dọn hậu cần.
- `dia_diem_to_chuc` (VARCHAR(150), NOT NULL): Khu vực tổ chức (Grand Ballroom, Sảnh Tiệc Sài Gòn, Poolside Terrace...).
- `ngan_sach_thu_lao` (DECIMAL(18,2), NOT NULL, DEFAULT 0): Tổng ngân sách quỹ thù lao/phụ cấp phê duyệt cho Đội đặc nhiệm sự kiện.
- `trang_thai` (VARCHAR(30), NOT NULL, DEFAULT 'KHOI_TAO'): Vòng đời dự án (`KHOI_TAO`, `DANG_TRIEN_KHAI`, `HOAN_TAT`, `QUYET_TOAN_LUONG`, `HUY_BO`).
- `ma_nguoi_quan_ly` (INT, NOT NULL, FK -> `nhan_vien.ma_nhan_vien`): Trưởng ban tổ chức / Giám đốc sự kiện chịu trách nhiệm chính.
- `mo_ta_yeu_cau` (TEXT, NULL): Yêu cầu kỹ thuật, tiêu chuẩn dịch vụ và lưu ý đặc biệt.
- `ngay_tao` (TIMESTAMP WITH TIME ZONE, DEFAULT CURRENT_TIMESTAMP): Thời điểm khởi tạo bản ghi.

#### Bảng `phan_cong_du_an` (Phân Công Nhân Sự Đội Đặc Nhiệm Sự Kiện)
Quản lý việc điều động nhân sự liên phòng ban và xác định mức thù lao dự án:
- `ma_phan_cong` (SERIAL, PRIMARY KEY): Mã định danh phân công công việc.
- `ma_du_an` (INT, NOT NULL, FK -> `du_an.ma_du_an` ON DELETE CASCADE): Sự kiện được phân công.
- `ma_nhan_vien` (INT, NOT NULL, FK -> `nhan_vien.ma_nhan_vien` ON DELETE RESTRICT): Nhân sự được điều động tham gia.
- `vai_tro_su_kien` (VARCHAR(100), NOT NULL): Vai trò trong Đội đặc nhiệm (ví dụ: `TRUONG_NHOM_DIEU_PHOI`, `PHU_TRACH_AM_THANH_ANH_SANG`, `PHUC_VU_BAN_TIEC_VIP`, `DIEU_PHOI_DON_KHACH_FO`, `HO_TRO_HAU_CAN_HK`, `AN_NINH_YEU_NHAN`).
- `so_gio_tham_gia` (DECIMAL(5,2), NOT NULL, DEFAULT 0): Số giờ làm việc thực tế đóng góp cho sự kiện.
- `thu_lao` (DECIMAL(18,2), NOT NULL, DEFAULT 0): Mức thù lao sự kiện / phụ cấp dự án định mức theo vai trò (VNĐ).
- `he_so_danh_gia` (DECIMAL(3,2), NOT NULL, DEFAULT 1.0): Hệ số đánh giá thưởng năng suất (1.0 khi `HOAN_THANH`, 1.1 - 1.2 khi `XUAT_SAC`).
- `danh_gia` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_DANH_GIA'): Kết quả đánh giá nhiệm vụ (`CHUA_DANH_GIA`, `HOAN_THANH`, `XUAT_SAC`, `KHONG_DAT`).
- `trang_thai_chi_tra` (VARCHAR(30), NOT NULL, DEFAULT 'CHUA_CHI_TRA'): Trạng thái thanh toán thù lao (`CHUA_CHI_TRA`, `DA_KET_CHUYEN_LUONG`).
- `ma_chi_tiet` (INT, NULL, FK -> `chi_tiet_phieu_luong.ma_chi_tiet`): Khóa ngoại liên kết hai chiều với dòng phụ cấp sự kiện trong chi tiết phiếu lương (Snapshot bất biến).
- `ghi_chu_danh_gia` (VARCHAR(255), NULL): Nhận xét chi tiết từ Trưởng ban sự kiện hoặc Quản lý bộ phận.
- `ngay_phan_cong` (TIMESTAMP WITH TIME ZONE, DEFAULT CURRENT_TIMESTAMP): Thời điểm lập phân công.

---

## 7. Ma Trận Định Lượng Yêu Cầu Phi Chức Năng (NFR Matrix)

Để đảm bảo hệ thống vận hành ổn định, an toàn và mang lại trải nghiệm chuẩn mực của một thương hiệu khách sạn 5 sao quốc tế, các yêu cầu phi chức năng (Non-Functional Requirements - NFR) được chuẩn hóa và lượng hóa cụ thể:

| Nhóm Yêu Cầu | Tiêu Chí Đo Lường | Chỉ Số Định Lượng Cam Kết | Giải Pháp Kỹ Thuật Đảm Bảo |
|:---|:---|:---:|:---|
| **Hiệu Năng (Performance)** | Thời gian phản hồi API Web & Mobile | **p95 < 500ms** (95% các yêu cầu phản hồi dưới 0.5 giây) | Tối ưu hóa truy vấn EF Core 10 / Npgsql; lập chỉ mục Index B-Tree trên các cột khóa ngoại và bộ lọc thường xuyên; kết nối Supabase qua Connection Pooling. |
| | Tốc độ tính lương tự động hàng loạt | **< 5 giây** cho toàn bộ 500+ nhân sự khách sạn | Thực thi thuật toán tính toán Gross -> Net tối ưu trên luồng xử lý nền (Background Task), xử lý dữ liệu theo lô (Batch Processing). |
| | Tốc độ xuất phiếu lương PDF | **< 1.5 giây / phiếu** | Sử dụng QuestPDF Engine với kiến trúc layout mã hóa tĩnh, tối ưu hóa bộ nhớ đệm và streaming trực tiếp. |
| **Độ Chính Xác Tài Chính (Financial Accuracy)** | Độ chính xác phép tính tiền tệ | **100% Tuyệt đối** (Sai số = 0) | Sử dụng kiểu dữ liệu `decimal` trong C# .NET 10 và `numeric(18,2)` trong PostgreSQL Supabase; **nghiêm cấm tuyệt đối** sử dụng `float` hoặc `double` để triệt tiêu hoàn toàn sai số làm tròn dấu phẩy động. |
| | Quy tắc làm tròn số tiền | Chuẩn kế toán Việt Nam | Làm tròn số tiền đến hàng đơn vị VNĐ theo quy tắc làm tròn tự nhiên (`MidpointRounding.AwayFromZero`). |
| **An Toàn & Bảo Mật (Security & Compliance)** | Kiểm soát truy cập CSDL | **100% bảng nhạy cảm** áp dụng Row Level Security (RLS) | Chính sách RLS trên Supabase Cloud bảo vệ dữ liệu theo vai trò và ID người dùng; người dùng chỉ truy cập đúng phạm vi quyền hạn được cấp. |
| | Bảo mật định danh & Xác thực | Chuẩn mã hóa quốc tế | Xác thực JWT (JSON Web Token); mật khẩu băm bảo mật bằng thuật toán an toàn; quản lý phiên đăng nhập chặt chẽ. |
| | Phân quyền vai trò người dùng | 5 vai trò & 17 mã quyền | Kiểm tra quyền RBAC tại tầng Middleware Web Admin và qua Policy claim trên Mobile App. |
| | Phân tách quyền nhạy cảm | Cấp tài khoản Mobile | Quyền `CAP_TAI_KHOAN_MOBILE` chỉ cấp cho vai trò Admin và IT; khóa `service_role` lưu trữ bảo mật tuyệt đối tại máy chủ Web Admin, **tuyệt đối không nhúng vào Mobile App client**. |
| | Lưu vết kiểm toán (Audit Trail) | **100% thao tác thay đổi** | Tự động ghi lại mọi thao tác thêm/sửa/xóa dữ liệu nhân sự, hợp đồng, bảng lương vào bảng `nhat_ky_quan_tri`. |
| **Khả Dụng & Mở Rộng (Availability & Scalability)** | Thời gian hoạt động liên tục | **Uptime >= 99.9%** (24/7/365) | Hạ tầng đám mây Supabase Cloud chịu lỗi cao; hỗ trợ vận hành liên tục cho các ca trực đêm của khách sạn. |
| | Sao lưu và phục hồi dữ liệu | Backup tự động định kỳ | Sao lưu tự động hàng ngày trên Supabase Cloud; khả năng phục hồi dữ liệu Point-in-Time Recovery khi xảy ra sự cố. |
| **Trải Nghiệm Web Admin (Luxury Web UI/UX)** | Quy tắc chuyển cảnh trạng thái | **Zero Full-Page Reload** | Áp dụng nguyên tắc không tải lại toàn trang khi đổi chế độ xem (Matrix / Timeline / List), chuyển tuần/ngày ca trực hoặc lọc dữ liệu; nạp cục bộ tại chỗ (Targeted Loading Isolation) bằng AJAX/Fetch kết hợp `history.pushState`. |
| | Hiệu ứng chuyển động giao diện | Chuẩn bộ khung react-bits | Thời lượng chuyển cảnh cục bộ 0.22s - 0.28s với đường cong gia tốc `cubic-bezier(0.16, 1, 0.3, 1)`; hiệu ứng đếm số động CountUp cho các chỉ số KPI tài chính; hiệu ứng ánh kim Gold Pulse khi cập nhật dữ liệu; sóng ánh sáng xúc giác Tactile Ripple trên mọi nút bấm. |
| **Trải Nghiệm Mobile App (Luxury Mobile UI/UX)** | Tông màu nhận diện thương hiệu | Đẳng cấp 5 sao quốc tế | Nền trắng ngà/slate (`#F8FAFC` / `#FFFFFF`), nút bấm xanh navy hoàng gia (`#1E3A8A`), viền và điểm nhấn vàng kim 5 sao (`#D4AF37`), chữ xanh đen sắc nét (`#0F172A`). |
| | Cơ chế hiển thị trạng thái chờ | **Tuyệt đối cấm vòng xoay ActivityIndicator** | 100% màn hình sử dụng **Skeleton Shimmer Loading** (khung xương giao diện nhấp nháy nhịp thở 0.45 sang 0.9 mượt mà 60 FPS), loại bỏ cảm giác chờ đợi. |
| | Cơ chế thông báo & Hộp thoại | **Tuyệt đối cấm DisplayAlert mặc định** | 100% cảnh báo và xác nhận sử dụng **Custom Luxury Modal Popup Component** (lớp phủ nền mờ backdrop, khung thẻ bo góc 24, viền vàng kim vương giả, icon trạng thái chuyên biệt). |
| | Typography & Định dạng tài nguyên | Chuẩn mực & Tương thích | Đồng bộ bộ phông `Segoe UI` / `SegoeUIBold`; toàn bộ App Icon và Splash Screen bắt buộc sử dụng định dạng **PNG** chuẩn nhận diện thương hiệu (tránh lỗi `MAUIR0004` Resizetizer trên Linux). |
| | Vật lý chuyển động Mobile | Thư viện `MobileMotion` | Tích hợp chuyển động nảy lò xo (SpringTap), lướt vào so le (StaggerCascade), chữ chuyển biến mượt mà (MorphText) và rung lắc báo lỗi (Shake). |

---

## 8. Thông Tin Nhóm Thực Hiện Đề Tài

- **Tên đề tài:** Hệ Thống Thông Tin Quản Lý Nhân Sự Và Tiền Lương (HRM) Tại Khách Sạn The OverTime Hotel.
- **Chuyên ngành:** Hệ Thống Thông Tin Doanh Nghiệp (Mã HP: 841065) — Khoa Công Nghệ Thông Tin — Trường Đại học Sài Gòn (SGU).
- **Giảng viên hướng dẫn:** ThS. [Tên Giảng Viên Hướng Dẫn]
- **Danh sách 4 thành viên sinh viên thực hiện:**
  1. **Nguyễn Đình Cường** — MSSV: 3123411043
  2. **Châu Quốc Bảo** — MSSV: 3123411024
  3. **Nguyễn Hoàng Long** — MSSV: 3123411179
  4. **Võ Huỳnh Minh Sang** — MSSV: 3123411256
