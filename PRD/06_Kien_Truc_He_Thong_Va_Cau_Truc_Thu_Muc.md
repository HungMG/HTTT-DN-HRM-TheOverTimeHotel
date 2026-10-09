# PRD 06: Kiến Trúc Hệ Thống & Cấu Trúc Thư Mục Source Code

> **Trạng thái tài liệu:** mô tả **hiện trạng đã triển khai trong mã nguồn** (`src/`) tại tháng 10/2026.
> Mọi khẳng định về tồn tại / không tồn tại bên dưới đều đối chiếu được với file thật; phần nào suy ra từ
> hạ tầng bên ngoài repo mà chưa kiểm chứng được thì ghi rõ **"cần xác minh"**.

---

## 1. Mô Hình Kiến Trúc Phân Tán (Distributed Architecture)

Nhằm phân định rõ ràng cổng quản trị máy chủ và ứng dụng nghiệp vụ theo tiêu chí chấm đồ án môn học HTTTDN:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                            KIẾN TRÚC HỆ THỐNG                            │
└──────────────────────────────────────────────────────────────────────────┘

     [QUẢN TRỊ VIÊN]                             [QUẢN LÝ & NHÂN VIÊN]
     Trình duyệt Web                             Điện thoại Android / iOS
            │                                               │
            ▼                                               ▼
┌───────────────────────────────┐               ┌───────────────────────────────┐
│       WEB ADMIN PORTAL        │               │       MOBILE APP CLIENT       │
│     C# ASP.NET Core MVC       │               │       C# .NET MAUI App        │
│          (.NET 10)            │               │          (.NET 10)            │
└───────────────┬───────────────┘               └───────────────┬───────────────┘
                │                                               │
                │        ┌─────────────────────────────┐        │
                ├───────►│    OvertimeHotel.HRM.Core   │◄───────┤ (Dùng chung 24 Entities,
                │        │     Class Library .NET 10   │        │  DTOs, Enums & Policies)
                │        └─────────────────────────────┘        │
                ▼                                               ▼
┌───────────────────────────────┐               ┌───────────────────────────────┐
│  OvertimeHotel.HRM.Data       │               │  SupabaseRestClient           │
│  EF Core 10 + Npgsql          │               │  HttpClient tới PostgREST     │
│  (PostgreSQL trực tiếp)       │               │  anon key + RLS               │
│  KHÔNG dùng bởi MobileApp     │               │ (không dùng Supabase SDK)     │
└───────────────┬───────────────┘               └───────────────┬───────────────┘
                │                                               │
                └───────────────────────┬───────────────────────┘
                                        │ (Kết nối trực tuyến 24/7)
                                        ▼
                        ┌───────────────────────────────┐
                        │     SUPABASE CLOUD DATABASE   │
                        │       PostgreSQL Engine       │
                        │    25 Bảng Chuẩn Hóa 3NF      │
                        │    + RLS (Row Level Security) │
                        └───────────────────────────────┘
```

### 1.1. Hai đường truy cập dữ liệu độc lập (không có tầng trung gian chạy nghiệp vụ)

Đây là điểm **khác biệt quan trọng nhất** so với kiến trúc nhiều tầng kinh điển, cần nêu rõ khi thuyết trình:

| | **WebAdmin → Database** | **MobileApp → Database** |
| :--- | :--- | :--- |
| **Lớp truy cập** | `OvertimeHotel.HRM.Data` (EF Core) | `MobileApp/Services/SupabaseRestClient.cs` |
| **Công nghệ** | EF Core `10.0.4` + Npgsql `10.0.3`, `DbContext<AppDbContext>` | `HttpClient` gọi PostgREST (`/rest/v1/`), parse tay bằng `PostgrestJson` |
| **Đường đi** | `Controller → AppDbContext → Npgsql → PostgreSQL` | `ViewModel → Service → SupabaseRestClient → PostgREST → PostgreSQL` |
| **Quyền kết nối** | Chuỗi kết nối PostgreSQL trong `appsettings.json` | `apikey` = Anon Key (khoá công khai) |
| **Ranh giới bảo mật** | Kiểm tra trong code C# (Controller / Policy) | **RLS phía server** là lớp chặn duy nhất |

**Hệ quả trực tiếp — KHÔNG có tầng trung gian nào chạy logic nghiệp vụ:**

* `OvertimeHotel.HRM.Data` **không được MobileApp tham chiếu**. Đã kiểm tra: `.csproj` của MobileApp chỉ có
  `ProjectReference` tới `OvertimeHotel.HRM.Core`, và không có dòng `using OvertimeHotel.HRM.Data` nào.
* Vì vậy **cùng một quy tắc nghiệp vụ phải viết hai lần**: một bản cho Web (C# + EF Core trong Controller),
  một bản cho Mobile (C# trong ViewModel/Service, hoặc dựa vào policy của Postgres).
* Thay đổi công thức lương, quy tắc duyệt đơn hay validation phải sửa **song song hai nơi** — nếu lệch,
  Web và Mobile sẽ cho kết quả khác nhau trên cùng một dữ liệu.
* Điểm giảm nhẹ: `OvertimeHotel.HRM.Core` **có** tầng `Policies/` dùng chung
  (`LeaveApprovalPolicy.cs`, `AttendanceCalculator.cs`) — đây là phần logic đã thực sự được tách sẻ và
  **nên mở rộng thêm** để giảm phần trùng lặp.

---

## 2. Chi Tiết Cấu Trúc Thư Mục `src/`

```text
src/
├── OvertimeHotel.HRM.slnx                     # Solution file Visual Studio 2026
│
├── OvertimeHotel.HRM.Core/                    # [DỰ ÁN DÙNG CHUNG - CLASS LIBRARY .NET 10]
│   ├── Models/                                # 25 Class Entities ánh xạ 25 bảng CSDL
│   │   ├── PhongBan.cs            ├── NhatKyQuanTri.cs
│   │   ├── ChucVu.cs              ├── CauHinhKhoanLuong.cs
│   │   ├── NhanVien.cs            ├── CauHinhKhauTru.cs
│   │   ├── HopDong.cs             ├── CauHinhPhuCap.cs
│   │   ├── TaiKhoan.cs            ├── CauHinhTangCa.cs
│   │   ├── VaiTro.cs              ├── ChiTietPhieuLuong.cs
│   │   ├── Quyen.cs               ├── ChiTietTruBaoHiem.cs
│   │   ├── VaiTroQuyen.cs         ├── ChiTietTruThueTncn.cs
│   │   ├── CaLamViec.cs           ├── ChiTietTruViPhamTamUng.cs
│   │   ├── PhanCa.cs              ├── KyLuong.cs
│   │   ├── ChamCong.cs            ├── LoaiDon.cs
│   │   ├── DonTu.cs               └── PhieuLuong.cs
│   ├── DTOs/                                  # Data Transfer Objects (record bất biến)
│   │   ├── LoginDto.cs           # LoginRequest / LoginResponse
│   │   ├── CheckInDto.cs          # CheckInRequest / CheckInResponse + DTO chấm công & ca trực
│   │   ├── LeaveRequestDto.cs     # DTO nộp/duyệt đơn
│   │   └── PayslipDto.cs          # PayslipSummaryDto / PayslipDetailDto / PayslipItemDto
│   ├── Enums/                                 # Kiểu liệu kê (9 enum)
│   │   ├── RoleType.cs, ShiftType.cs, LeaveStatus.cs, ContractStatus.cs
│   │   ├── AttendanceStatus.cs, EmployeeStatus.cs, PayrollStatus.cs
│   │   └── PeriodStatus.cs, SalaryItemType.cs
│   ├── Policies/                              # Logic nghiệp vụ dùng chung 2 app
│   │   ├── LeaveApprovalPolicy.cs             # Quy tắc ai duyệt loại đơn nào
│   │   └── AttendanceCalculator.cs            # Chuẩn hoá check-in/check-out
│   └── Constants/
│       └── SalaryConstants.cs                 # 26 ngày công, tỷ lệ BH 10.5%, ...
│
├── OvertimeHotel.HRM.Data/                    # [TẦNG TRUY XUẤT DỮ LIỆU - CHỈ WEBADMIN DÙNG]
│   ├── Context/
│   │   ├── AppDbContext.cs                    # EF Core DbContext, cấu hình quan hệ PK/FK (25 DbSet)
│   │   └── DatabaseSchemaInitializer.cs        # Tự tạo bảng nhật ký admin nếu thiếu
│   ├── Supabase/
│   │   ├── SupabaseClientFactory.cs           # Tạo Supabase SDK client (Realtime/Storage)
│   │   └── SupabaseOptions.cs
│   ├── Services/
│   │   ├── IBackupService.cs                  # Interface sao lưu & phục hồi CSDL
│   │   └── BackupService.cs                   # Xuất/nhập JSON 25 bảng + thống kê
│   ├── migrations/                            # Script SQL áp dụng tay lên Supabase
│   ├── database_schema_3nf.sql                # DDL 25 bảng chuẩn 3NF
│   └── seed_data_24_tables.sql                # Dữ liệu danh mục & tài khoản mẫu
│
├── OvertimeHotel.HRM.WebAdmin/                # [CỔNG QUẢN TRỊ ADMIN - ASP.NET CORE MVC .NET 10]
│   ├── Controllers/                           # 12 Controller MVC + 4 API controller
│   │   ├── HomeController.cs                  # Dashboard & báo cáo
│   │   ├── NhanVienController.cs              # Hồ sơ nhân viên
│   │   ├── PhongBanController.cs, ChucVuController.cs, CaLamViecController.cs  # Danh mục
│   │   ├── HopDongController.cs, PhanCaController.cs                           # HĐ & xếp ca
│   │   ├── CauHinhLuongController.cs, CauHinhKhoanLuongController.cs           # Tham số lương
│   │   ├── AccountController.cs, UserAccountsController.cs                     # Tài khoản, nhật ký
│   │   ├── BackupController.cs                # Sao lưu & phục hồi CSDL
│   │   └── Api/                               # API cho các luồng cũ của Mobile
│   │       ├── AttendanceController.cs, LeaveRequestsController.cs
│   │       └── MobileAuthController.cs, MobileProfileController.cs
│   ├── Security/                              # Hạ tầng RBAC (policy, permission codes)
│   ├── Models/                                # ViewModel riêng cho từng màn hình Web
│   ├── Views/                                 # Razor (.cshtml) cho 12 nhóm chức năng
│   ├── wwwroot/                               # css / js / lib (Bootstrap, jQuery)
│   ├── appsettings.json                       # Chuỗi kết nối Supabase PostgreSQL + cấu hình Supabase
│   └── Program.cs                             # Cấu hình DI, Authentication & Authorization
│
└── OvertimeHotel.HRM.MobileApp/               # [ỨNG DỤNG DI ĐỘNG - .NET MAUI .NET 10]
    ├── App.xaml / AppShell.xaml               # Shell + điều hướng tab
    ├── MauiProgram.cs                         # Điểm khởi động & đăng ký DI
    ├── Platforms/                             # Android, iOS, MacCatalyst, Windows
    ├── Views/                                 # 11 màn hình XAML
    │   ├── IntroPage.xaml                     # Màn hình khởi động có thanh tiến trình 0%→100%
    │   ├── LoginPage.xaml                     # Đăng nhập
    │   ├── EmployeeHomePage.xaml              # Trang chủ Nhân viên
    │   ├── ManagerHomePage.xaml               # Trang chủ Quản lý
    │   ├── HrHomePage.xaml                    # Trang chủ Nhân sự (HR)
    │   ├── AttendancePage.xaml                # Chấm công quẹt thẻ check-in/out
    │   ├── LeaveApprovalPage.xaml             # Duyệt đơn
    │   ├── LeaveDetailPage.xaml               # Chi tiết đơn
    │   ├── ProfilePage.xaml                   # Hồ sơ cá nhân
    │   ├── ChangePasswordPage.xaml            # Đổi mật khẩu
    │   └── SettingsPage.xaml                  # Cài đặt
    ├── Views/Controls/                        # 6 component tái sử dụng
    │   ├── LuxuryPopup.xaml, ShiftHeroCard.xaml, LocationClockPanel.xaml
    │   └── FeatureTile.xaml, StatTile.xaml, ProfileHeaderBar.xaml
    ├── ViewModels/                            # 12 ViewModel - MVVM
    ├── Services/                              # Gọi PostgREST trực tiếp (KHÔNG dùng Supabase SDK)
    │   ├── SupabaseRestClient.cs              # HttpClient → PostgREST, kèm bearer token
    │   ├── PostgrestJson.cs                   # Đọc/ghi JSON & xử lý NoRowsMatched
    │   ├── AuthService.cs                     # Đăng nhập Supabase GoTrue
    │   ├── AttendanceService.cs, LeaveApprovalService.cs
    │   ├── GeoLocationService.cs
    │   └── ApiConfig.cs                       # URL + Anon Key
    ├── Animations/
    │   └── MobileMotion.cs                    # Thư viện chuyển động (SpringTap, Shimmer, BounceIn)
    └── Models/
```

> **Lưu ý:** thư mục `MobileApp` dùng cấu trúc **`Views/` phẳng** (không có `Views/Pages/` như
> khuôn mẫu MAUI mặc định). Ngoài ra có 3 service `Demo*` (`DemoAuthService`,
> `DemoLeaveApprovalService`, `DemoAttendanceService`) chỉ hoạt động khi build với cờ `-p:DemoMode=true`,
> phục vụ thuyết trình offline — **không dùng trong bản phát hành**.

---

## 3. Các Gói Thư Viện NuGet Cần Thiết

> **Phân biệt rõ:** phần này ghi *các gói NuGet đã cài thật trong `.csproj`*, không phải gói do tài liệu
> dự kiến. Mọi gói đều được đối chiếu trực tiếp với file `.csproj` của từng project.

### 3.1. `OvertimeHotel.HRM.Data` — đã cài thật

| Gói | Phiên bản |
| :--- | :--- |
| `Microsoft.EntityFrameworkCore` | `10.0.4` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` |
| `supabase` | `1.0.0` |

* ⚠️ `Microsoft.EntityFrameworkCore.Tools` **không có** trong `.csproj`. Cài riêng nó cũng không giúp gì vì
  dự án **không dùng lệnh `dotnet ef`**: toàn bộ DDL do script `database_schema_3nf.sql` và các file trong
  `migrations/` áp dụng tay lên Supabase. Có thể bỏ dòng "cần package này" trong danh mục trước.
* Gói `supabase` chỉ để dựng client Realtime/Storage; **hiện chưa có Controller nào tiêm client này**
  (`graft callers SupabaseClientFactory` không trả về caller nào ngoài chính dòng đăng ký trong `Program.cs`).
  Đăng ký DI đã có sẵn nhưng chưa được tiêu thụ.

### 3.2. `OvertimeHotel.HRM.MobileApp` — đã cài thật

| Gói | Phiên bản |
| :--- | :--- |
| `Microsoft.Maui.Controls` | `$(MauiVersion)` (theo SDK) |
| `Microsoft.Maui.Controls.Maps` | `$(MauiVersion)` — cần meta-data `com.google.android.geo.API_KEY` |
| `Microsoft.Extensions.Logging.Debug` | `10.0.0` |

* ⚠️ **`supabase-csharp` KHÔNG được cài** trong MobileApp, và không có dòng `using Supabase` nào trong project.
  Ứng dụng đi qua **PostgREST bằng `HttpClient`** với Anon Key. Không nên liệt kê `supabase-csharp` là
  gói của MobileApp — đó là mô tả kiến trúc đích, không phải hiện trạng.
* ⚠️ **`CommunityToolkit.Mvvm` KHÔNG được cài.** ViewModel tự cài đặt `INotifyPropertyChanged` thủ công
  (hàm `SetProperty` tự viết), không dùng attribute `[ObservableProperty]` / `[RelayCommand]`.

### 3.3. Xuất PDF phiếu lương — **CHƯA TRIỂN KHAI**

* ⚠️ **QuestPDF KHÔNG được tham chiếu** ở bất kỳ `.csproj` nào, và không có dòng `using QuestPDF` nào trong
  mã nguồn. Chuỗi "QuestPDF" chỉ xuất hiện ở hai chỗ **mô tả**, không phải mã chạy:
  * `seed_data_24_tables.sql:63` — nội dung quyền `XUAT_PHIEU_LUONG_PDF` ("Xuất phiếu lương QuestPDF").
  * `Views/Home/Index.cshtml:225` — dòng chữ *"Xuất phiếu lương PDF bằng QuestPDF đang được phát triển."*
* Khi thuyết trình phải nói rõ đây là **tính năng đang phát triển**, không phải đã hoàn thành. Danh mục
  gói NuGet ở mục này được giữ lại như **kế hoạch đã định**.

### 3.4. Tóm tắt sai lệch đã sửa

| Tài liệu cũ ghi | Hiện trạng thật |
| :--- | :--- |
| `Microsoft.EntityFrameworkCore.Tools` | Không cài, và không dùng `dotnet ef` |
| MobileApp dùng `supabase-csharp` | Không — dùng `HttpClient` → PostgREST |
| MobileApp dùng `CommunityToolkit.Mvvm` | Không — MVVM tự viết |
| QuestPDF đã dùng để xuất PDF | Chưa triển khai, chỉ có mô tả |

---

## 4. Cơ Chế Sao Lưu & Phục Hồi CSDL

> Phần này gộp lại để tránh mô tả trùng. Trước đây kiến trúc vẽ *"Tự Động Sao Lưu Hàng Ngày"* của
> Supabase, còn mục cấu trúc thư mục lại liệt kê `BackupController` — hai cách nói về hai thứ khác nhau.

**Hiện trạng đã triển khai — sao lưu thủ công từ trong Web Admin:**

* `BackupService` (`Data/Services/BackupService.cs`) nối thẳng PostgreSQL qua EF Core rồi:
  * `ExportDatabaseToJsonAsync()` — gom **toàn bộ 25 bảng** thành **một file JSON** và tải về máy người dùng.
  * `RestoreDatabaseFromJsonAsync(json)` — nạp ngược JSON, xóa & tạo lại dữ liệu theo thứ tự phụ thuộc FK.
  * `GetTableStatisticsAsync()` — đếm số bản ghi từng bảng.
* `BackupController` (`[Authorize(Policy = PermissionCodes.BackupDatabase)]`) là điểm vào duy nhất:
  `GET /Backup/Export` và `POST /Backup/Restore`.
* Quyền `SAO_LUU_CSDL` ("Sao lưu & Phục hồi CSDL") được gán cho nhóm Admin trong `seed_data_24_tables.sql`.

**Đặc điểm cần lưu ý khi trình bày:**

* Đây là **sao lưu thủ công theo yêu cầu**, không phải lịch tự động. Không có job, không có timer,
  không có Cron — không có bản sao lưu nào được tạo nếu người quản trị không bấm nút.
* Phạm vi là **toàn bộ CSDL trong một file JSON**, không phải sao lưu tăng dần (incremental).
* Bản sao lưu nằm trên máy người quản trị, **không nằm trong hạ tầng dự án**.

**Về backup nền tảng của Supabase:** sơ đồ kiến trúc cũ ghi Supabase *"Tự Động Sao Lưu Hàng Ngày"*.
Đây là tính năng của gói Supabase Cloud, **không nằm trong repo** nên không thể đối chiếu từ mã nguồn —
**cần xác minh** với tài khoản thực tế (gói Free có giới hạn thời gian giữ bản sao lưu tối đa 7 ngày).
Nếu không xác minh được thì **không nên khẳng định trong báo cáo**.

---

## 5. Đối Chiếu Luồng Gọi Hàm Thực Tế

**Kiến trúc đích mà tài liệu cũ mô tả:** `Controller → Service → Repository → Data`. **Hiện trạng: KHÔNG tồn tại.**

* Thư mục `WebAdmin` **không có** thư mục `Services/` hay `Repositories/` — xác nhận bằng cách liệt kê cây
  thư mục của project.
* **13 Controller MVC tiêm thẳng `AppDbContext`** và gọi thẳng LINQ. Chuỗi thực thi thật:
  ```
  Controller ──inject──▶ AppDbContext ──▶ Npgsql ──▶ PostgreSQL (Supabase)
  ```
* Điểm đáng lưu ý: chính tác giả cũng thừa nhận trong `MobileAuthController` rằng *"WebAdmin che được lỗ hổng
  này chỉ vì nó nối Postgres bằng quyền `postgres` và kiểm bằng code"* — tức **WebAdmin không đi qua RLS**,
  mọi kiểm tra quyền nằm trong code C#. Điều này giải thích vì sao một lỗ hổng policy yếu lọt qua WebAdmin
  mà lại thành lỗ hổng thật khi Mobile ghi thẳng.
* Ngoại lệ duy nhất trong Data là `IBackupService`/`BackupService` — có tách interface khỏi Controller, nhưng
  đây là **trường hợp riêng lẻ cho chức năng sao lưu**, không phải mẫu kiến trúc áp dụng chung.

**Luồng của Mobile:**

```
ViewModel ──▶ IAuthService / IAttendanceService / ILeaveApprovalService
           ──▶ SupabaseRestClient (HttpClient + apikey + bearer)
           ──▶ PostgREST /rest/v1/  ──▶ PostgreSQL (kiểm tra qua RLS)
```

**Nhận định:** phần lớn logic nghiệp vụ trong hệ thống hiện **nằm trực tiếp trong Controller và ViewModel**.
Đây là hiện trạng của một đồ án đang phát triển, chấp nhận được, nhưng khi trình bày **không nên gọi đó là
kiến trúc phân tầng 3 lớp** — hãy mô tả đúng là "Controller truy cập dữ liệu trực tiếp, tách lớp Services /
Repositories là hướng cải tiến dự kiến".

---

## 6. Điểm Rủi Ro Kiến Trúc

Các hệ quả cần thừa nhận khi bảo vệ đồ án:

1. **Logic nghiệp vụ trùng lặp ở 2 nơi.**
   `OvertimeHotel.HRM.Data` không phục vụ MobileApp, nên mỗi quy tắc (công thức lương, quyền duyệt đơn,
   validation chấm công) phải có hai bản mã. Sửa một bản mà quên bản kia sẽ khiến Web và Mobile báo kết
   quả khác nhau trên cùng một dữ liệu. *Giảm thiểu:* mở rộng `Core/Policies/` thành nguồn logic duy nhất.

2. **Phụ thuộc RLS phía server — điểm rủi ro lớn nhất.**
   MobileApp dùng Anon Key nên **RLS là lớp chặn DUY NHẤT**. Mọi thay đổi policy ảnh hưởng đồng thời
   cả hai ứng dụng. Đã có tiền lệ: migration `20261006_siet_rls_duyet_don_client.sql` phải *siết lại*
   chính vì policy cũ `duyet_don_tu_quan_ly` chỉ kiểm `ma_vai_tro IN (1,2,3)`, khiến mọi quản lý đều duyệt
   được đơn của phòng ban khác, đơn loại 3 và 5 vốn bị `LeaveApprovalPolicy` cấm. Khi RLS lọc dòng, PostgREST
   trả `204 No Content` chứ không báo lỗi — app phải tự diễn giải, và "không có quyền" với "đã bị người
   khác xử lý" hợp nhất về cùng một kết quả. Rất dễ tạo trải nghiệm lỗi khó hiểu.
   *Đối chiếu:* có 4 file migration trong `supabase/migrations/` chứa tổng cộng khoảng 50 câu lệnh
   `CREATE POLICY` / `ROW LEVEL SECURITY`.

3. **Không có lớp caching trung gian.**
   Không có Redis, không có MemoryCache dùng chung cho dữ liệu CSDL, không có lớp trung gian nào giữa hai
   app và database. Mỗi màn hình phải gọi mạng tới PostgREST/PostgreSQL. Hệ quả: độ trễ mạng hiển thị
   trực tiếp lên UI, phát sinh tải trùng giữa các màn hình, và mọi app đều phụ thuộc kết nối mạng ổn định.

4. **Hai cơ chế kiểm tra quyền song song, dễ lệch nhau.**
   Web kiểm tra bằng code (EF + `[Authorize]` + policy), Mobile kiểm tra bằng RLS. Hai bộ quy tắc phải được
   đồng bộ thủ công; khi lệch, Web "đúng" còn Mobile "sai" (hoặc ngược lại).

5. **Chưa có kiểm thử tự động phủ nghiệp vụ.**
   Thư mục `tests/` chỉ có 1 file script Node (`payroll-smoke.mjs`). Không có test project .NET, nên các
   công thức lương và quy tắc duyệt đơn chưa được bảo vệ bởi test hồi quy.

6. **Sao lưu thủ công, không có lịch tự động.**
   Không job, không Cron, không snapshot định kỳ. Bản sao lưu chỉ được tạo khi Admin bấm nút và nằm trên
   máy của người dùng. Nếu phụ thuộc vào backup nền tảng của Supabase thì phải xác minh riêng (mục 4).

7. **Tính năng PDF chưa hoàn thành.**
   QuestPDF chưa được cài, chưa có mã trên nhánh `main`. Nếu công việc này đang được thực hiện trên nhánh
   khác thì cần kiểm tra lại trước khi khẳng định trong báo cáo — **cần xác minh**.

---

## 7. Những Điểm Cần Xác Minh

| # | Nội dung | Vì sao chưa xác minh được |
| :--- | :--- | :--- |
| 1 | Supabase có bật **backup tự động hàng ngày** không, giữ lại bao lâu | Là tính năng của gói Supabase Cloud, **không nằm trong repo** — phải hỏi trực tiếp trên Dashboard |
| 2 | Trạng thái triển khai **QuestPDF / xuất phiếu lương PDF** hiện tại | Repo không có mã; có thể đang làm trên nhánh khác |
| 3 | `supabase` package (`1.0.0`) trong Data còn **dùng được** không | Đã đăng ký DI trong `Program.cs` nhưng không tìm thấy Controller nào tiêm client — có vẻ là đăng ký dư |