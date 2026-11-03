# PRD 08: Phân Tích & So Sánh Kiến Trúc Kết Nối Mobile - Backend

> **Bộ tài liệu:** Đặc tả yêu cầu & Kiến trúc kỹ thuật Hệ thống Quản lý Nhân sự & Tiền lương The OverTime Hotel  
> **Trạng thái:** Tài liệu kiến trúc chính thức đối chiếu trực tiếp với mã nguồn Solution C# .NET 10 (`src/`)  
> **Chủ đề:** So sánh chuyên sâu giữa **Kiến trúc 3 tầng truyền thống (3-Tier qua WebAdmin API Localhost)** và **Kiến trúc BaaS hiện đại (Direct Supabase Cloud PostgREST & RLS)**

---

## 1. Đặt Vấn Đề & Bối Cảnh Thực Tế

Trong quá trình phát triển và kiểm thử dự án **The OverTime Hotel HRM**, một bài toán kỹ thuật thực tế nảy sinh xoay quanh cách ứng dụng di động **.NET MAUI Mobile App** giao tiếp với nguồn dữ liệu:

1. **Khởi nguyên ban đầu (Giai đoạn 1):** Hệ thống dự kiến áp dụng mô hình 3 tầng cổ điển (3-Tier Architecture): Mọi thao tác từ điện thoại di động đều phải gửi yêu cầu HTTP tới máy chủ Web Quản trị (**ASP.NET Core WebAdmin**) đang chạy trên `localhost`. WebAdmin thực hiện truy vấn qua Entity Framework Core 10 rồi trả dữ liệu ngược lại cho ứng dụng di động.
2. **Khó khăn thực tế tại môi trường kiểm thử của nhóm:**
   - **Gánh nặng tài nguyên phần cứng (Resource Contention):** Khi mở cùng lúc Visual Studio 2026/IDE, WebAdmin Backend (`dotnet run`), máy ảo Android Emulator (`qemu-system-x86_64` ngốn 3.5GB – 4GB RAM) và biên dịch mã nguồn C# .NET 10, máy tính của lập trình viên rơi vào tình trạng quá tải, CPU quạt rú liên tục, phản hồi giao diện bị giật khựng.
   - **Xung đột mạng nội bộ (Network Loopback & SSL Issues):** Máy ảo Android không nhận diện `localhost` là máy tính chủ mà phải ánh xạ qua địa chỉ loopback ảo `10.0.2.2`. Khi kiểm thử trên điện thoại thật qua Wi-Fi hoặc 4G, việc kết nối vào localhost đòi hỏi cấu hình mạng nội bộ phức tạp, chỉnh sửa `network_security_config.xml` để cho phép giao thức HTTP không mã hóa (Cleartext Traffic), hoặc đối mặt với lỗi từ chối chứng chỉ HTTPS tự ký (Self-signed Certificate).
3. **Bước chuyển hướng kiến trúc (Giai đoạn 2 - Hiện tại):** Nhóm đã quyết định chuyển dịch các phân hệ lõi của ứng dụng di động sang mô hình **BaaS (Backend-as-a-Service)**: Ứng dụng di động kết nối trực tiếp với **Supabase Cloud Database (Singapore)** thông qua cổng **PostgREST Engine** và cơ chế bảo mật hàng dữ liệu **Row Level Security (RLS)**.

Dưới đây là bản phân tích toàn diện, chi tiết từ mã nguồn đến ưu/nhược điểm của cả hai cấu trúc.

---

## 2. Cấu Trúc Cũ: Kiến Trúc 3 Tầng Truyền Thống (3-Tier via WebAdmin API)

### 2.1. Sơ Đồ Luồng Dữ Liệu & Các Chặng Mạng (Double-Hop Latency)

```text
┌──────────────────────────┐
│   MOBILE APP CLIENT      │ (Thiết bị di động / Máy ảo Android)
│    (.NET MAUI .NET 10)   │
└────────────┬─────────────┘
             │
             │ [Chặng 1: HTTP/REST qua IP 10.0.2.2:5065 hoặc LAN IP]
             │ Token: Bearer JWT (Cấp từ WebAdmin)
             ▼
┌──────────────────────────┐
│   WEB ADMIN API SERVER   │ (Máy tính cá nhân / Localhost)
│   (ASP.NET Core MVC 10)  │
│                          │
│  Controllers/Api:        │
│  - AttendanceController  │
│  - LeaveRequestsApi...   │
│  - ShiftSwapsApi...      │
│  - AppDbContext (EF Core)│
└────────────┬─────────────┘
             │
             │ [Chặng 2: TCP Connection Pool qua Internet]
             │ Giao thức: Npgsql 10.0.3 (PostgreSQL Wire Protocol)
             ▼
┌──────────────────────────┐
│  SUPABASE CLOUD DATABASE │ (Máy chủ Cloud Singapore)
│    PostgreSQL Engine     │
│   (30 Bảng Chuẩn 3NF)    │
└──────────────────────────┘
```

### 2.2. Vị Trí Mã Nguồn Cụ Thể Trong Codebase

1. **Client gọi API trên Mobile:**
   - `src/OvertimeHotel.HRM.MobileApp/Services/WebAdminApiClient.cs`: Đóng gói `HttpClient` trỏ tới địa chỉ WebAdmin.
   - `src/OvertimeHotel.HRM.MobileApp/OvertimeHotel.HRM.MobileApp.csproj` (dòng 50-57): Cấu hình cờ biên dịch `LocalApiServer=true` trỏ về `http://10.0.2.2:5065` và bật `cleartext` traffic cho Android.
2. **Server tiếp nhận và xử lý API trên WebAdmin:**
   - `src/OvertimeHotel.HRM.WebAdmin/Controllers/Api/AttendanceController.cs`: Cổng API `[Route("api/mobile/attendance")]`, tiếp nhận `[HttpPost("check-in")]`, `[HttpPost("check-out")]`, sử dụng `AppDbContext` để ghi nhận vào bảng `cham_cong`.
   - `src/OvertimeHotel.HRM.WebAdmin/Controllers/Api/LeaveRequestsApiController.cs`: Tiếp nhận nộp đơn và duyệt đơn nghỉ phép.
   - `src/OvertimeHotel.HRM.WebAdmin/Controllers/Api/ShiftSwapsApiController.cs`: Tiếp nhận yêu cầu đổi ca 24/7.
   - `src/OvertimeHotel.HRM.WebAdmin/Controllers/Api/AttendanceExplanationsApiController.cs`: Tiếp nhận giải trình công vi phạm.

### 2.3. Đánh Giá Ưu Điểm & Nhược Điểm Kỹ Thuật

####  Ưu điểm:
1. **Kiểm soát tập trung tuyệt đối (Single Backend Authority):** Toàn bộ nghiệp vụ thẩm định, đối soát ca và tính toán công nằm tập trung tại một nơi duy nhất trên máy chủ Web (C#). Nếu đổi công thức hay quy tắc, chỉ cần sửa code WebAdmin mà không cần cập nhật ứng dụng di động.
2. **Che giấu cấu trúc CSDL hoàn hảo (Data Abstraction):** Ứng dụng di động hoàn toàn không biết Supabase là gì, không biết tên bảng hay chuỗi kết nối. Mọi giao tiếp đều thông qua các DTO trung gian.
3. **Mô hình kinh điển, dễ giải trình lý thuyết môn học:** Rất quen thuộc với mô hình bài giảng Hệ thống thông tin doanh nghiệp (Presentation Layer ➔ Application Layer ➔ Data Layer).

#### ❌ Nhược điểm:
1. **Nặng máy phát triển cực hạn (Heavy Localhost Overhead):**
   - Lập trình viên bắt buộc phải chạy song song WebAdmin trên terminal (`dotnet run --project src/OvertimeHotel.HRM.WebAdmin`) trong khi máy ảo Android đang nuốt hàng gigabyte RAM.
   - Nếu WebAdmin gặp lỗi biên dịch hoặc bị crash, toàn bộ tính năng trên Mobile App lập tức tê liệt với lỗi `HttpRequestException: Connection Refused`.
2. **Trở ngại kết nối mạng nội bộ khi Test (The 10.0.2.2 & SSL Nightmare):**
   - Máy ảo dùng IP `10.0.2.2`, thiết bị Android thật dùng IP Wi-Fi (`192.168.1.x`), còn khi lên trường/quán cafe IP LAN bị đổi liên tục khiến app mất kết nối.
   - Giao thức HTTPS trên máy dev dùng chứng chỉ self-signed của .NET Development Certificate, hệ điều hành Android chặn mặc định vì lý do bảo mật.
3. **Độ trễ nhân đôi (Double-Hop Latency):** Dữ liệu phải đi 2 lần chặng mạng (Mobile ➔ Máy Dev ➔ Cloud Singapore ➔ Máy Dev ➔ Mobile). Tốc độ quẹt thẻ bị chậm đáng kể nếu mạng máy dev bị nghẽn.

---

## 3. Cấu Trúc Mới: Kiến Trúc BaaS Trực Tiếp (Direct Supabase Cloud + RLS)

### 3.1. Sơ Đồ Luồng Dữ Liệu & Cơ Chế PostgREST (Single-Hop Latency)

```text
┌────────────────────────────────────────────────────────┐
│               MOBILE APP CLIENT (.NET MAUI)            │
│  - Tự đối soát cục bộ: OvertimeHotel.HRM.Core/Policies  │
│  - Định vị phần cứng: IGeoLocationService (GPS)        │
│  - Đóng gói JSON: PostgrstJson / JsonContent           │
└───────────────────────────┬────────────────────────────┘
                            │
                            │ [1 Chặng Duy Nhất: HTTPS công khai trên Internet]
                            │ URL: https://soethtbttktnwtxyxjrj.supabase.co/rest/v1/...
                            │ Headers:
                            │   - apikey: AnonKey (Khoá công khai dự án)
                            │   - Authorization: Bearer <jwt_user_token>
                            ▼
┌────────────────────────────────────────────────────────┐
│             SUPABASE CLOUD (SINGAPORE)                 │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │  PostgREST Engine (Viết bằng Haskell siêu tốc)   │  │
│  │  - Chuyển HTTP JSON trực tiếp thành SQL JSON     │  │
│  │  - Tích hợp Supavisor / PgBouncer Pool           │  │
│  └──────────────────────────┬───────────────────────┘  │
│                             │                          │
│                             ▼                          │
│  ┌──────────────────────────────────────────────────┐  │
│  │  PostgreSQL Database Engine (30 Bảng Chuẩn 3NF)  │  │
│  │  - Lớp bảo vệ: Row Level Security (RLS Policies) │  │
│  │    (Chỉ cho phép sửa/xem dòng thuộc chính mình)  │  │
│  │  - Khóa B-Tree: ma_nhan_vien, ma_phan_ca         │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### 3.2. Vị Trí Mã Nguồn Cụ Thể Trong Codebase

1. **Client kết nối trực tiếp PostgREST:**
   - `src/OvertimeHotel.HRM.MobileApp/Services/SupabaseRestClient.cs`: Quản lý kết nối HTTP/2 duy nhất, tự động gắn `apikey` và `Bearer <Token>`, gửi các phương thức `GetAsync`, `PostAsync`, `PatchAsync`.
   - `src/OvertimeHotel.HRM.MobileApp/Services/ApiConfig.cs`: Định nghĩa URL Cloud chuẩn `https://soethtbttktnwtxyxjrj.supabase.co` và khóa công khai `SupabaseAnonKey`.
   - `src/OvertimeHotel.HRM.MobileApp/Services/PostgrstJson.cs`: Bộ phân tích JSON thích ứng tự động (hỗ trợ cả camelCase và snake_case).
2. **Dịch vụ nghiệp vụ độc lập:**
   - `src/OvertimeHotel.HRM.MobileApp/Services/AttendanceService.cs`: Chấm công trực tiếp lên bảng `cham_cong` và cập nhật `phan_ca` thành `HOAN_THANH`.
   - `src/OvertimeHotel.HRM.MobileApp/Services/MobileScheduleService.cs`: Nạp lịch trực 24/7 trực tiếp từ bảng `phan_ca` và `ca_lam_viec`.
   - `src/OvertimeHotel.HRM.MobileApp/Services/MobilePayslipService.cs`: Nạp phiếu lương cá nhân từ `phieu_luong` và `chi_tiet_phieu_luong`.
3. **Thư viện chính sách nghiệp vụ dùng chung (Clean Architecture):**
   - `src/OvertimeHotel.HRM.Core/Policies/AttendanceCalculator.cs`: Tính toán đi trễ, về sớm, giờ OT chuẩn xác từng phút.
   - `src/OvertimeHotel.HRM.Core/Policies/GeofencePolicy.cs`: Thuật toán Haversine đo khoảng cách tọa độ GPS thực tế.
   - `src/OvertimeHotel.HRM.Core/Policies/ContractAttendancePolicy.cs`: Kiểm tra hợp đồng lao động có hiệu lực.
4. **Tầng bảo vệ an ninh CSDL (Database Security):**
   - `supabase/migrations/20261005_hon_ho_so_jwt_quyen_theo_vai_tro.sql`: Thu hồi toàn bộ quyền của role `anon`, kích hoạt **RLS** trên toàn bộ 30 bảng, buộc mọi request phải có JWT hợp lệ.

### 3.3. Đánh Giá Ưu Điểm & Nhược Điểm Kỹ Thuật

####  Ưu điểm:
1. **Giải phóng hoàn toàn máy Dev (Zero Local Server Overhead):**
   - Khi phát triển và test ứng dụng di động, lập trình viên **KHÔNG CẦN CHẠY WebAdmin trên localhost**. Máy tính chỉ cần mở Android Emulator hoặc cắm dây điện thoại.
   - Máy chạy êm ái, mát mẻ, giải phóng 100% dung lượng RAM và luồng CPU dành cho việc giả lập thiết bị.
2. **Độc lập và cơ động tối đa khi Demo / Thuyết trình:**
   - Ứng dụng kết nối trực tiếp với Internet qua domain Supabase Cloud.
   - Thành viên nhóm có thể cài file APK vào điện thoại cá nhân, mang lên giảng đường, kết nối 4G/Wi-Fi của trường và mở app thao tác trực tiếp với dữ liệu sống mà không cần mang laptop theo làm "máy chủ".
3. **Tốc độ phản hồi cực nhanh (Single-Hop Latency < 100ms):**
   - Lược bỏ hoàn toàn máy chủ trung gian. Dữ liệu từ điện thoại gửi thẳng tới máy chủ Supabase Singapore qua HTTP/2. PostgREST trả về JSON trong tích tắc (~30ms – 60ms).
4. **Khả năng chịu tải vượt trội (High Concurrency & Scalability):**
   - Không bị nghẽn ở tầng Web Server. Dù 50 hay 200 nhân viên bấm quẹt thẻ cùng lúc vào giờ đổi ca, PostgREST và Supavisor Connection Pool phân bổ thẳng vào PostgreSQL song song.

#### ❌ Nhược điểm:
1. **Phân tán logic nghiệp vụ (Logic Duplication):**
   - Một số quy tắc nghiệp vụ phải tồn tại ở cả 2 nơi: trên Mobile (trong thư viện `OvertimeHotel.HRM.Core/Policies`) và trên WebAdmin (trong Controllers). Nếu có sự thay đổi quy tắc tính toán, lập trình viên phải cập nhật đồng bộ cả hai project.
2. **Đòi hỏi năng lực bảo mật CSDL cao (Heavy Reliance on RLS):**
   - Do client tương tác trực tiếp với CSDL qua REST, nếu CSDL bị quên bật RLS trên một bảng nào đó, người dùng có thể dùng token để đọc/sửa dữ liệu của người khác. Đội ngũ phát triển phải viết và kiểm tra các script SQL migration RLS rất cẩn trọng.

---

## 4. Bảng So Sánh Toàn Diện (Architectural Matrix)

| Tiêu chí so sánh | Kiến trúc cũ: 3-Tier qua WebAdmin API | Kiến trúc mới: BaaS Supabase Cloud |
| :--- | :--- | :--- |
| **Mô hình kiến trúc** | Client-Server 3 tầng truyền thống | Backend-as-a-Service (BaaS) / Serverless |
| **Đường dẫn truy cập** | `http://10.0.2.2:5065/api/mobile/...` | `https://soethtbttktnwtxyxjrj.supabase.co/rest/v1/...` |
| **Yêu cầu khi Test Mobile** | **Bắt buộc bật WebAdmin localhost** song song | **Chỉ cần mở App**, không cần bật WebAdmin |
| **Tài nguyên máy Dev** | Ngốn rất nhiều RAM (VS + Web + Emulator = 8–10GB) | Nhẹ nhàng, chỉ tốn RAM cho Emulator (~3GB) |
| **Khả năng Demo trên trường** | Khó khăn (cần ngrok, mở port, chung mạng LAN) | Cực kỳ dễ dàng (Cài APK vào máy thật, chạy 4G) |
| **Thời gian phản hồi (Latency)** | 2 chặng: `~180ms - 400ms` | 1 chặng: `~50ms - 120ms` |
| **Ranh giới bảo mật** | Tầng Controller C# trong WebAdmin | Tầng RLS (Row Level Security) trong PostgreSQL |
| **Điểm lỗi đơn lẻ (SPOF)** | WebAdmin localhost sập ➔ App sập theo | Độc lập, hạ tầng Cloud 99.9% uptime của Supabase |
| **Nơi thực thi nghiệp vụ** | 100% tại WebAdmin Controller | Tách sẻ qua `OvertimeHotel.HRM.Core.Policies` |

---

## 5. Kiến Trúc Lai (Hybrid Architecture) Đang Vận Hành Trong Dự Án

Nhằm dung hòa ưu điểm của cả hai mô hình và tối ưu hóa cho đồ án thực tế, hệ thống hiện tại đang vận hành theo mô hình **Hybrid (Kiến trúc lai thông minh)**:

```text
┌────────────────────────────────────────────────────────────────────────┐
│                        THE OVERTIME HOTEL HRM                          │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
          ┌─────────────────────────┴─────────────────────────┐
          │                                                   │
          ▼                                                   ▼
┌───────────────────────────────────┐       ┌───────────────────────────────────┐
│     NHÓM TÁC VỤ TẦN SUẤT CAO      │       │     NHÓM QUY TRÌNH DUYỆT ĐA BƯỚC  │
│     (Direct Supabase Cloud BaaS)  │       │     (WebAdmin API / Workflow)     │
├───────────────────────────────────┤       ├───────────────────────────────────┤
│ • Chấm công quẹt thẻ 24/7 (GPS)   │       │ • Nộp & Phê duyệt đơn nghỉ phép   │
│ • Theo dõi ca trực điều hành      │       │ • Quy trình đổi ca trực nhân sự   │
│ • Xem lịch phân ca tuần / tháng   │       │ • Nộp giải trình công vi phạm     │
│ • Tra cứu phiếu lương chi tiết    │       │ • Cấp phát tài khoản nhân sự (IT) │
├───────────────────────────────────┤       ├───────────────────────────────────┤
│ 👉 Đi thẳng PostgREST Cloud       │       │ 👉 Đi qua WebAdminApiClient       │
│ 👉 Tốc độ < 100ms, test không cần │       │ 👉 Có cơ chế Fallback Demo Mode   │
│    bật WebAdmin localhost.        │       │    không làm crash app khi offline│
└───────────────────────────────────┘       └───────────────────────────────────┘
```

---

## 6. Khảo Sát Doanh Nghiệp & Bài Toán Khi Deploy Lên Môi Trường Thực Tế (Production Deploy)

### 6.1. Thực Tế Các Doanh Nghiệp Phần Mềm Hiện Nay Đang Dùng Gì?

Khi khảo sát các nền tảng phần mềm quản trị doanh nghiệp (SaaS HRM/ERP) hiện nay trên thị trường (như **Base.vn, 1Office, Odoo, BambooHR**) cũng như các ứng dụng công nghệ lớn (**Grab, Shopee, Netflix**):

1. **Mô hình 3-Tier thuần túy (Truyền thống):**
   - *Đối tượng áp dụng:* Ngân hàng, các tổ chức tài chính (FinTech), khối cơ quan nhà nước với tiêu chuẩn an ninh thông tin ngặt nghèo (PCI-DSS, ISO 27001). CSDL được giấu hoàn toàn trong mạng nội bộ (On-premise Private Subnet), không có IP công khai.
   - *Hạn chế:* Chi phí duy trì hạ tầng cụm máy chủ Web rất lớn, đội ngũ vận hành phức tạp, khả năng mở rộng chậm khi có đột biến truy cập.
2. **Mô hình Pure BaaS / Serverless (Toàn bộ qua Firebase / Supabase):**
   - *Đối tượng áp dụng:* Các công ty khởi nghiệp (Startups), ứng dụng mạng xã hội, ứng dụng đặt lịch hoặc game di động.
   - *Hạn chế:* Dễ triển khai ban đầu nhưng khó quản lý các nghiệp vụ quản trị phức tạp (như tính thuế TNCN lũy tiến 7 bậc, trích đóng BHXH 10.5%, tổng hợp quỹ lương định kỳ, chạy tác vụ ngầm hàng đêm).
3. **Mô hình Lai (Hybrid Architecture / Modern BFF - Backend for Frontend):**
   - *Đối tượng áp dụng:* **Đây là cấu trúc tiêu chuẩn được hầu hết các doanh nghiệp công nghệ hiện đại áp dụng.**
   - *Nguyên tắc phân chia:*
     * Các tác vụ đọc/ghi tần suất cao của nhân viên di động (quẹt thẻ GPS, xem lịch ca, thông báo realtime): Đi trực tiếp qua hạ tầng Cloud (BaaS) với lớp bảo vệ RLS để đạt tốc độ tối đa và giảm tải cho máy chủ.
     * Các tác vụ quản trị cốt lõi, đối soát tài chính và xuất dữ liệu nặng (tính lương, duyệt đơn đa cấp, AI Assistant): Được đảm nhiệm bởi máy chủ nghiệp vụ trung tâm (ASP.NET Core).

---

### 6.2. Phân Tích Khi Deploy WebAdmin Lên Cloud: Có Nên Chuyển Hẳn Về 3-Tier?

Khi WebAdmin đã được Deploy lên máy chủ Cloud công khai (Azure App Service, AWS, Render hay VPS), vấn đề nghẽn mạng `localhost` và IP giả lập `10.0.2.2` không còn là rào cản. Tuy nhiên, việc **chuyển 100% về 3-Tier** hay **giữ vững Cấu Trúc Lai (Hybrid hiện tại)** cần được cân nhắc kỹ:

#### Phương án A: Chuyển 100% Mobile về qua WebAdmin API (3-Tier thuần)
- **Ưu điểm:** Quy tắc nghiệp vụ nằm đúng 1 nơi trong mã nguồn C# của WebAdmin.
- **Rủi ro lớn trong môi trường thực tế (Production Risks):**
  1. **Chi phí máy chủ Web tăng vọt (Infrastructure Cost Surge):** Máy chủ WebAdmin không chỉ phục vụ vài người quản trị/HR, mà phải chịu tải hàng trăm lượt quẹt thẻ dồn dập vào mỗi đầu giờ ca trực (06:00, 14:00, 22:00). Bạn bắt buộc phải nâng cấp gói máy chủ đắt tiền (nhiều vCPU và RAM hơn) để tránh bị nghẽn CPU.
  2. **Rủi ro "Sập chùm" (Single Point of Failure - SPOF):** Nếu máy chủ WebAdmin gặp sự cố treo tiến trình hoặc đang tạm dừng để bảo trì/cập nhật phiên bản mới, **toàn bộ nhân viên khách sạn sẽ bị chặn đứng tại cửa, không thể quẹt thẻ check-in được**!
  3. **Tăng độ trễ mạng (Double Latency):** Thời gian phản hồi bị kéo dài do phải đi qua 2 chặng mạng liên tiếp (Phone ➔ Server Web ➔ Supabase Singapore ➔ Server Web ➔ Phone).

#### Phương án B: Giữ Cấu Trúc Lai (Hybrid Architecture - Khuyến nghị lựa chọn)
- **Tính sẵn sàng và khả năng chịu lỗi độc lập (High Availability 99.9% & Fault Tolerance):** Dù máy chủ WebAdmin có đang khởi động lại hay bận chạy batch-job tính lương, nhân viên **vẫn quẹt thẻ chấm công, xem ca trực bình thường 24/7** vì Supabase Cloud không bị ảnh hưởng.
- **Tối ưu hóa chi phí hạ tầng (Cost Efficiency):** WebAdmin chỉ cần thuê cấu hình cơ bản (chi phí thấp) vì lưu lượng truy cập di động nặng nề đã được cụm máy chủ đám mây Supabase gánh trọn vẹn.
- **Trải nghiệm người dùng vượt trội (Ultra-low Latency):** Quẹt thẻ phản hồi tức thì dưới 100ms.

---

### 6.3. Cẩm Nang Bảo Vệ Đồ Án Trước Giảng Viên & Hội Đồng (Defense Pitch Guide)

Khi Giảng viên hoặc Hội đồng phản biện đặt câu hỏi:  
> *"Tại sao ở một số tính năng trên Mobile App, nhóm không gọi qua Web API trung gian mà lại kết nối thẳng vào CSDL Supabase?"*

**Nhóm tự tin trình bày theo 3 luận điểm chuyên nghiệp sau:**

1. **Luận điểm 1 - Bài toán thực tế giờ cao điểm (High-Concurrency Spike):**  
   *"Thưa Cô, khách sạn The OverTime Hotel vận hành 24/7 với hàng trăm nhân viên đổi ca đồng loạt vào các mốc giờ cố định (06:00, 14:00, 22:00). Nếu toàn bộ lượt quẹt thẻ dồn vào máy chủ WebAdmin trung gian sẽ tạo ra điểm nghẽn cổ chai (Bottleneck) và gây độ trễ lớn. Nhóm quyết định áp dụng mô hình **Backend-as-a-Service (BaaS)** qua cổng PostgREST để CSDL xử lý song song, giải tỏa áp lực cho máy chủ Web."*
2. **Luận điểm 2 - An ninh dữ liệu không bị suy giảm nhờ RLS (Zero-Trust Security):**  
   *"Việc gọi thẳng không làm mất đi tính bảo mật, vì nhóm đã triển khai **Row Level Security (RLS)** ngay tại tầng nhân PostgreSQL. Mọi request đều kèm JWT Token xác thực; nhân viên chỉ được quyền thêm/sửa đúng dòng chấm công thuộc phân ca của chính mình, hoàn toàn không thể xem hoặc sửa dữ liệu của nhân sự khác."*
3. **Luận điểm 3 - Phân tách tính sẵn sàng (High Availability & Separation of Concerns):**  
   *"Hệ thống được thiết kế theo **Kiến trúc Lai (Hybrid)** hiện đại: Các tác vụ sống còn 24/7 của nhân viên (Chấm công, Xem ca trực) hoàn toàn độc lập với máy chủ WebAdmin. Nhờ đó, ngay cả khi quản trị viên đang bảo trì Web, nâng cấp thuật toán lương hoặc xuất báo cáo nặng, hoạt động quẹt thẻ tại các cửa khách sạn vẫn diễn ra liên tục 100%, không bao giờ bị gián đoạn."*

---

## 7. Kết Luận & Định Hướng Nâng Cấp

1. **Đánh giá giải pháp:** Việc áp dụng **Kiến trúc Lai (Hybrid Architecture)** là một quyết định kỹ thuật sáng suốt, dung hòa hoàn hảo giữa khả năng chịu tải thực tế, tính ổn định cao và chi phí vận hành tối ưu chuẩn doanh nghiệp.
2. **Lộ trình hoàn thiện:**
   - Đóng gói các quy trình duyệt đơn phức tạp đa bảng thành các **PostgreSQL Stored Procedures (RPC)** trên Supabase.
   - Tiếp tục hoàn thiện bộ thư viện nghiệp vụ dùng chung `OvertimeHotel.HRM.Core.Policies` để đảm bảo quy tắc tính toán của Web và Mobile luôn đồng nhất 100%.

