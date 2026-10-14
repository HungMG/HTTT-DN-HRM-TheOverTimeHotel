# PRD 06: Kiến Trúc Hệ Thống & Cấu Trúc Thư Mục Source Code

> **Trạng thái tài liệu:** mô tả **hiện trạng đã triển khai trong mã nguồn** (`src/`) tại tháng 10/2026 kèm **định hướng mở rộng kiến trúc** cho phân hệ Quản lý Dự án & Sự kiện Khách sạn (toàn hệ thống gồm **11 phân hệ chức năng nghiệp vụ** và **27 bảng CSDL chuẩn 3NF**).
> Mọi khẳng định về tồn tại / không tồn tại bên dưới đều đối chiếu được với file thật; phần nào suy ra từ hạ tầng bên ngoài repo mà chưa kiểm chứng được thì ghi rõ **"cần xác minh"**.

---

## 1. Mô Hình Kiến Trúc Phân Tán (Distributed Architecture)

Nhằm phân định rõ ràng cổng quản trị máy chủ và ứng dụng nghiệp vụ theo tiêu chí chấm đồ án môn học HTTTDN:

```text
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
                ├───────►│    OvertimeHotel.HRM.Core   │◄───────┤ (Dùng chung 27 Entities,
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
                        │    27 Bảng Chuẩn Hóa 3NF      │
                        │    + RLS (Row Level Security) │
                        └───────────────────────────────┘
```

### 1.1. Hai đường truy cập dữ liệu độc lập (không có tầng trung gian chạy nghiệp vụ)

Đây là điểm **khác biệt quan trọng nhất** so với kiến trúc nhiều tầng kinh điển, cần nêu rõ khi thuyết trình:

| Tiêu chí | **WebAdmin → Database** | **MobileApp → Database** |
| :--- | :--- | :--- |
| **Lớp truy cập** | `OvertimeHotel.HRM.Data` (EF Core) | `MobileApp/Services/SupabaseRestClient.cs` |
| **Công nghệ** | EF Core `10.0.4` + Npgsql `10.0.3`, `DbContext<AppDbContext>` | `HttpClient` gọi PostgREST (`/rest/v1/`), parse tay bằng `PostgrestJson` |
| **Đường đi** | `Controller → AppDbContext → Npgsql → PostgreSQL` | `ViewModel → Service → SupabaseRestClient → PostgREST → PostgreSQL` |
| **Quyền kết nối** | Chuỗi kết nối PostgreSQL trong `appsettings.json` | `apikey` = Anon Key (khoá công khai) |
| **Ranh giới bảo mật** | Kiểm tra trong code C# (Controller / Policy) | **RLS phía server** là lớp chặn duy nhất |

**Hệ quả trực tiếp — KHÔNG có tầng trung gian nào chạy logic nghiệp vụ:**

* `OvertimeHotel.HRM.Data` **không được MobileApp tham chiếu**. Đã kiểm tra: `.csproj` của MobileApp chỉ có `ProjectReference` tới `OvertimeHotel.HRM.Core`, và không có dòng `using OvertimeHotel.HRM.Data` nào.
* Vì vậy **cùng một quy tắc nghiệp vụ phải viết hai lần**: một bản cho Web (C# + EF Core trong Controller), một bản cho Mobile (C# trong ViewModel/Service, hoặc dựa vào policy của Postgres).
* Thay đổi công thức lương, quy tắc duyệt đơn hay validation phải sửa **song song hai nơi** — nếu lệch, Web và Mobile sẽ cho kết quả khác nhau trên cùng một dữ liệu.
* Điểm giảm nhẹ: `OvertimeHotel.HRM.Core` **có** tầng `Policies/` dùng chung (`LeaveApprovalPolicy.cs`, `AttendanceCalculator.cs`) — đây là phần logic đã thực sự được tách sẻ và **cần tiếp tục mở rộng** (như `ProjectAllowancePolicy.cs`) để giảm thiểu trùng lặp.

---

### 1.2. Kiến Trúc 11 Phân Hệ Nghiệp Vụ Chức Năng (Kèm Phân Hệ Quản Lý Dự Án & Sự Kiện)

Hệ thống Quản lý Nhân sự & Tiền lương The OverTime Hotel được chuẩn hóa thành **11 phân hệ nghiệp vụ cấp 1** (57 chức năng cấp 2 theo BFD), vận hành trên nền tảng **27 bảng CSDL chuẩn 3NF**:

| STT | Phân Hệ Nghiệp Vụ Cấp 1 | Phạm Vi Nghiệp Vụ Trong Khách Sạn | Bảng CSDL Tương Ứng (3NF) |
| :---: | :--- | :--- | :--- |
| **1.0** | **Quản Lý Nhân Sự & HĐLĐ** | Hồ sơ lý lịch, ký kết và theo dõi hạn HĐLĐ, bổ nhiệm và điều chuyển công tác. | `phong_ban`, `chuc_vu`, `nhan_vien`, `hop_dong` |
| **2.0** | **Tài Khoản & Phân Quyền (RBAC)** | Xác thực danh tính đa nền tảng, quản lý bảo mật tài khoản, phân nhóm 5 vai trò, quy trình HR yêu cầu -> IT cấp tài khoản Mobile. | `tai_khoan`, `vai_tro`, `quyen`, `vai_tro_quyen`, `yeu_cau_tai_khoan` |
| **3.0** | **Ca Làm & Lịch Phân Ca 24/7** | Quản lý mẫu ca 24/7, lập lịch phân ca tuần/tháng, kiểm soát khoảng nghỉ >= 8h, quản lý đổi ca và cảnh báo thiếu hụt định biên vận hành. | `ca_lam_viec`, `phan_ca` |
| **4.0** | **Bảng Chấm Công 24/7 & Chuyên Cần** | Quẹt thẻ di động GPS + Wi-Fi 2 lớp, tự động tính phút trễ/sớm, tính giờ OT làm tròn xuống bước 0.5h, tổng hợp công tháng và giải trình công. | `cham_cong` |
| **5.0** | **Quản Lý Đơn Từ Điện Tử** | Tạo và xử lý nộp trực tuyến 4 loại đơn: Phép năm, Nghỉ ốm BHXH, Thai sản, Thôi việc; phân luồng phê duyệt cấp Quản lý / HR. | `loai_don`, `don_tu` |
| **6.0** | **Tính Toán Lương Tự Động Hàng Tháng & Phiếu Lương PDF** | Mở kỳ lương, đối soát công, động cơ tính lương 2 tầng Gross -> Net (Điều 98 BLLĐ, BHXH 10.5%), khóa sổ kỳ lương, xem phiếu lương App, xuất PDF chuẩn QuestPDF (lộ trình phát triển) và quyết toán thuế năm. | `ky_luong`, `phieu_luong`, `chi_tiet_phieu_luong`, `chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn`, `chi_tiet_tru_vi_pham_tam_ung` |
| **7.0** | **Quản Lý Phụ Cấp** | Quản lý định mức phụ cấp ca đêm 30%, ăn ca 30k/ngày, phụ cấp trách nhiệm, phụ cấp độc hại/đồng phục và lịch sử điều chỉnh định mức. | `cau_hinh_phu_cap`, `cau_hinh_khoan_luong` |
| **8.0** | **Quản Lý Tiền Thưởng** | Quản lý thưởng hiệu quả KPI, thưởng doanh thu buồng phòng/tiệc, thưởng Lễ Tết chuyên cần, phê duyệt danh sách và báo cáo phân bổ quỹ thưởng. | `cau_hinh_khoan_luong` (khoản thưởng), `cau_hinh_tang_ca` |
| **9.0** | **Quản Lý Dự Án & Sự Kiện Khách Sạn (MỚI)** | Khởi tạo sự kiện/dự án (tiệc cưới VIP, hội nghị MICE, gala dinner), điều động nhân lực liên phòng ban (Task Force), thông báo điều động realtime lên Mobile App, nghiệm thu đánh giá và kết chuyển phụ cấp dự án vào kỳ lương tháng (Gross). | `du_an`, `phan_cong_du_an` |
| **10.0** | **Báo Cáo & Thống Kê Quản Trị** | Thống kê biến động nhân sự (Turnover Rate), cơ cấu bộ phận, học vấn thâm niên, đối soát ngân sách quỹ lương, chuyên cần vi phạm và báo cáo hiệu quả nhân sự sự kiện. | Tổng hợp liên bảng |
| **11.0** | **Quản Trị Hệ Thống** | Danh mục cơ cấu tổ chức (phòng ban, chức vụ), cấu hình khung giờ ca 24/7 (độc quyền Admin), thiết lập tham số tỷ lệ định mức, sao lưu và phục hồi CSDL Supabase. | `nhat_ky_quan_tri` (+ toàn bộ 27 bảng) |

---

## 2. Chi Tiết Cấu Trúc Thư Mục `src/`

```text
src/
├── OvertimeHotel.HRM.slnx                     # Solution file Visual Studio 2026
│
├── OvertimeHotel.HRM.Core/                    # [DỰ ÁN DÙNG CHUNG - CLASS LIBRARY .NET 10]
│   ├── Models/                                # 27 Class Entities ánh xạ 27 bảng CSDL chuẩn 3NF
│   │   ├── PhongBan.cs            ├── NhatKyQuanTri.cs
│   │   ├── ChucVu.cs              ├── CauHinhKhoanLuong.cs
│   │   ├── NhanVien.cs            ├── CauHinhKhauTru.cs
│   │   ├── HopDong.cs             ├── CauHinhPhuCap.cs
│   │   ├── TaiKhoan.cs            ├── CauHinhTangCa.cs
│   │   ├── VaiTro.cs              ├── ChiTietPhieuLuong.cs
│   │   ├── Quyen.cs               ├── ChiTietTruBaoHiem.cs
│   │   ├── VaiTroQuyen.cs         ├── ChiTietTruThueTncn.cs
│   │   ├── YeuCauTaiKhoan.cs      ├── ChiTietTruViPhamTamUng.cs
│   │   ├── CaLamViec.cs           ├── KyLuong.cs
│   │   ├── PhanCa.cs              ├── LoaiDon.cs
│   │   ├── ChamCong.cs            ├── PhieuLuong.cs
│   │   ├── DonTu.cs               ├── DuAn.cs                 # [MỚI] Entity sự kiện/dự án khách sạn
│   │   │                          └── PhanCongDuAn.cs         # [MỚI] Entity phân công & thù lao sự kiện
│   ├── DTOs/                                  # Data Transfer Objects (record bất biến)
│   │   ├── LoginDto.cs                        # LoginRequest / LoginResponse
│   │   ├── CheckInDto.cs                      # CheckInRequest / CheckInResponse
│   │   ├── AttendanceSummaryDto.cs            # Thống kê công & giờ làm
│   │   ├── LeaveRequestDto.cs                 # Nộp và duyệt đơn
│   │   ├── PayslipDto.cs                      # Bảng lương tóm tắt & chi tiết
│   │   ├── ProjectDto.cs                      # [MỚI] DTO danh sách, chi tiết, tạo/sửa sự kiện
│   │   └── ProjectAssignmentDto.cs            # [MỚI] DTO điều phối nhân sự, nghiệm thu & thù lao
│   ├── Enums/                                 # Kiểu liệt kê (13 enum)
│   │   ├── RoleType.cs, ShiftType.cs, LeaveStatus.cs, ContractStatus.cs
│   │   ├── AttendanceStatus.cs, EmployeeStatus.cs, PayrollStatus.cs
│   │   ├── PeriodStatus.cs, SalaryItemType.cs
│   │   ├── ProjectStatus.cs                   # [MỚI] ChuaBatDau, DangDienRa, DaHoanThanh, TamDung, HuyBo
│   │   ├── ProjectRoleType.cs                 # [MỚI] TruongNhom, DieuPhoi, PhucVuChinh, HoTro, KyThuat, HauCan
│   │   ├── ProjectEvaluation.cs               # [MỚI] ChuaDanhGia, XuatSac, Tot, DatYeuCau, KhongDat
│   │   └── ProjectPaymentStatus.cs            # [MỚI] ChuaQuyetToan, ChoPheDuyet, DaQuyetToan, DaKetChuyenLuong
│   ├── Policies/                              # Logic nghiệp vụ dùng chung 2 app
│   │   ├── LeaveApprovalPolicy.cs             # Quy tắc ai duyệt loại đơn nào
│   │   ├── AttendanceCalculator.cs            # Chuẩn hoá check-in/check-out
│   │   └── ProjectAllowancePolicy.cs          # [ĐỊNH HƯỚNG] Định mức thù lao dự án theo vai trò & đánh giá
│   └── Constants/
│       ├── SalaryConstants.cs                 # 26 ngày công, tỷ lệ BH 10.5%, hệ số OT 1.5/2.0/3.0
│       └── ProjectConstants.cs                # [ĐỊNH HƯỚNG] Mã định mức phụ cấp dự án, giới hạn ngân sách
│
├── OvertimeHotel.HRM.Data/                    # [TẦNG TRUY XUẤT DỮ LIỆU - CHỈ WEBADMIN DÙNG]
│   ├── Context/
│   │   ├── AppDbContext.cs                    # EF Core DbContext, cấu hình quan hệ PK/FK (27 DbSet)
│   │   └── DatabaseSchemaInitializer.cs       # Tự tạo bảng nhật ký admin nếu thiếu
│   ├── Supabase/
│   │   ├── SupabaseClientFactory.cs           # Tạo Supabase SDK client (Realtime/Storage)
│   │   └── SupabaseOptions.cs
│   ├── Services/
│   │   ├── IBackupService.cs                  # Interface sao lưu & phục hồi CSDL
│   │   └── BackupService.cs                   # Xuất/nhập JSON 27 bảng + thống kê
│   ├── migrations/                            # Script SQL áp dụng tay lên Supabase
│   ├── database_schema_3nf.sql                # DDL 27 bảng chuẩn 3NF
│   └── seed_data_24_tables.sql                # Dữ liệu danh mục & tài khoản mẫu
│
├── OvertimeHotel.HRM.WebAdmin/                # [CỔNG QUẢN TRỊ ADMIN - ASP.NET CORE MVC .NET 10]
│   ├── Controllers/                           # 14 Controller MVC + 4 API controller
│   │   ├── HomeController.cs                  # Dashboard & báo cáo KPI
│   │   ├── NhanVienController.cs              # Hồ sơ nhân viên
│   │   ├── PhongBanController.cs, ChucVuController.cs, CaLamViecController.cs  # Danh mục
│   │   ├── HopDongController.cs, PhanCaController.cs                           # HĐ & xếp ca
│   │   ├── CauHinhLuongController.cs, CauHinhKhoanLuongController.cs           # Tham số lương
│   │   ├── AccountController.cs, UserAccountsController.cs                     # Tài khoản, nhật ký
│   │   ├── YeuCauTaiKhoanController.cs        # Tiếp nhận & cấp tài khoản cho bộ phận IT
│   │   ├── BackupController.cs                # Sao lưu & phục hồi CSDL
│   │   ├── DuAnController.cs                  # [MỚI] Quản trị sự kiện, điều phối nhân sự, chốt thù lao
│   │   └── Api/                               # API cho các luồng dữ liệu
│   │       ├── AttendanceController.cs, LeaveRequestsController.cs
│   │       └── MobileAuthController.cs, MobileProfileController.cs
│   ├── Security/                              # Hạ tầng RBAC (policy, permission codes)
│   ├── Models/                                # ViewModel riêng cho từng màn hình Web
│   ├── Views/                                 # Razor (.cshtml) cho các nhóm chức năng
│   │   └── DuAn/                              # [MỚI] Index, Details, Create, Edit, AssignStaff, Settlement
│   ├── wwwroot/                               # css / js / lib (Bootstrap, jQuery, react-bits motion)
│   ├── appsettings.json                       # Chuỗi kết nối Supabase PostgreSQL + cấu hình Supabase
│   └── Program.cs                             # Cấu hình DI, Authentication & Authorization
│
└── OvertimeHotel.HRM.MobileApp/               # [ỨNG DỤNG DI ĐỘNG - .NET MAUI .NET 10]
    ├── App.xaml / AppShell.xaml               # Shell + điều hướng tab
    ├── MauiProgram.cs                         # Điểm khởi động & đăng ký DI
    ├── Platforms/                             # Android, iOS, MacCatalyst, Windows
    ├── Views/                                 # 15 màn hình XAML hiện có + định hướng phân hệ Dự án
    │   ├── IntroPage.xaml                     # Màn hình khởi động có thanh tiến trình 0%→100%
    │   ├── LoginPage.xaml                     # Đăng nhập
    │   ├── EmployeeHomePage.xaml              # Trang chủ Nhân viên
    │   ├── ManagerHomePage.xaml               # Trang chủ Quản lý
    │   ├── HrHomePage.xaml                    # Trang chủ Nhân sự (HR)
    │   ├── AttendancePage.xaml                # Chấm công quẹt thẻ check-in/out (GPS/WiFi)
    │   ├── AttendanceSummaryPage.xaml         # Bảng tổng hợp công cá nhân
    │   ├── SchedulePage.xaml                  # Xem lịch phân ca 24/7
    │   ├── LeaveSubmitPage.xaml               # Nộp đơn từ trực tuyến
    │   ├── LeaveApprovalPage.xaml             # Danh sách duyệt đơn
    │   ├── LeaveDetailPage.xaml               # Chi tiết đơn từ
    │   ├── PayslipPage.xaml                   # Xem bảng tổng hợp & chi tiết phiếu lương
    │   ├── ProfilePage.xaml                   # Hồ sơ cá nhân
    │   ├── ChangePasswordPage.xaml            # Đổi mật khẩu
    │   ├── SettingsPage.xaml                  # Cài đặt
    │   ├── ProjectSchedulePage.xaml           # [ĐỊNH HƯỚNG MỚI] Xem lịch phân công sự kiện khách sạn
    │   └── ProjectDetailPage.xaml             # [ĐỊNH HƯỚNG MỚI] Tra cứu chi tiết sự kiện & thù lao duyệt
    ├── Views/Controls/                        # Custom component tái sử dụng (Luxury UI 5★)
    │   ├── LuxuryPopup.xaml, ShiftHeroCard.xaml, LocationClockPanel.xaml
    │   └── FeatureTile.xaml, StatTile.xaml, ProfileHeaderBar.xaml
    ├── ViewModels/                            # Các ViewModel theo mô hình MVVM
    │   ├── ProjectScheduleViewModel.cs        # [ĐỊNH HƯỚNG MỚI] Lọc và hiển thị sự kiện được phân công
    │   └── ProjectDetailViewModel.cs          # [ĐỊNH HƯỚNG MỚI] Hiển thị chi tiết giờ công, đánh giá, thù lao
    ├── Services/                              # Gọi PostgREST trực tiếp (KHÔNG dùng Supabase SDK)
    │   ├── SupabaseRestClient.cs              # HttpClient → PostgREST, kèm bearer token & RLS
    │   ├── PostgrestJson.cs                   # Đọc/ghi JSON & xử lý NoRowsMatched
    │   ├── AuthService.cs                     # Đăng nhập Supabase GoTrue
    │   ├── AttendanceService.cs, LeaveApprovalService.cs
    │   ├── GeoLocationService.cs              # Kiểm tra tọa độ GPS & WiFi BSSID
    │   ├── ProjectService.cs                  # [ĐỊNH HƯỚNG MỚI] Lấy danh sách sự kiện & thù lao của nhân viên
    │   └── ApiConfig.cs                       # URL + Anon Key
    ├── Resources/                             # Tài nguyên ứng dụng
    │   ├── AppIcon/                           # appicon.png, appiconfg.png (PNG bắt buộc cho Linux build)
    │   └── Splash/                            # splash.png (PNG bắt buộc cho Linux build)
    └── Animations/
        └── MobileMotion.cs                    # Thư viện chuyển động (SpringTap, Shimmer, BounceIn)
```

---

## 3. Các Gói Thư Viện NuGet Cần Thiết & Hiện Trạng Thực Tế

> **Phân biệt rõ:** phần này ghi *các gói NuGet đã cài thật trong `.csproj`*, làm rõ các gói dự kiến và quy chuẩn tài nguyên bắt buộc trên môi trường đa nền tảng.

### 3.1. `OvertimeHotel.HRM.Data` — đã cài thật

| Gói | Phiên bản | Ghi chú kỹ thuật |
| :--- | :--- | :--- |
| `Microsoft.EntityFrameworkCore` | `10.0.4` | ORM truy xuất dữ liệu |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` | Driver PostgreSQL cho EF Core |
| `supabase` | `1.0.0` | Client Realtime/Storage (chưa tiêu thụ trong Controller) |

* ⚠️ `Microsoft.EntityFrameworkCore.Tools` **không có** trong `.csproj`. Cài riêng nó cũng không giúp gì vì dự án **không dùng lệnh `dotnet ef`**: toàn bộ DDL do script `database_schema_3nf.sql` và các file trong `migrations/` áp dụng tay lên Supabase.
* Gói `supabase` chỉ để dựng client Realtime/Storage; hiện chưa có Controller nào tiêm client này (`graft callers SupabaseClientFactory` không trả về caller nào ngoài chính dòng đăng ký trong `Program.cs`).

### 3.2. `OvertimeHotel.HRM.MobileApp` — đã cài thật

| Gói | Phiên bản | Ghi chú kỹ thuật |
| :--- | :--- | :--- |
| `Microsoft.Maui.Controls` | `$(MauiVersion)` | Nền tảng UI .NET MAUI |
| `Microsoft.Maui.Controls.Maps` | `$(MauiVersion)` | Cần meta-data `com.google.android.geo.API_KEY` |
| `Microsoft.Extensions.Logging.Debug` | `10.0.0` | Logging trong môi trường Debug |

* ⚠️ **`supabase-csharp` KHÔNG được cài** trong MobileApp, và không có dòng `using Supabase` nào trong project. Ứng dụng đi qua **PostgREST bằng `HttpClient`** với Anon Key.
* ⚠️ **`CommunityToolkit.Mvvm` KHÔNG được cài.** ViewModel tự cài đặt `INotifyPropertyChanged` thủ công (hàm `SetProperty` tự viết), không dùng attribute `[ObservableProperty]` / `[RelayCommand]`.

### 3.3. Quy chuẩn Định dạng Tài nguyên Icon & Splash Screen (PNG Build Gate)

> ⚠️ **Bắt buộc dùng PNG — KHÔNG dùng SVG cho build target:**

- `Microsoft.Maui.Resizetizer 10.0.110` ném lỗi **`MAUIR0004`** khi đọc file SVG (`Svg.Skia`) trên hệ điều hành Linux (môi trường CI/CD và máy trạm lập trình viên).
- Các biện pháp khắc phục như cờ `SkipValidateMauiIcon` không hỗ trợ cho SVG và không có bản vá Resizetizer nâng cấp tương thích.
- **Quy tắc bắt buộc:**
  1. File `.csproj` bắt buộc trỏ `MauiIcon` và `MauiSplashScreen` sang các file ảnh định dạng **`.png`** (`Resources/AppIcon/appicon.png`, `Resources/AppIcon/appiconfg.png`, `Resources/Splash/splash.png`).
  2. Các file `.svg` gốc được lưu giữ trong thư mục `Resources/` với vai trò là **nguồn thiết kế gốc (master design source)** — chỉ dùng để xuất lại file PNG khi cần điều chỉnh đồ họa, **tuyệt đối không đưa SVG vào ItemGroup build**.
  3. Bằng chứng kiểm chứng: commit `4ac7528` (`fix(mobileapp): chuyen app icon/splash sang PNG de build duoc tren Linux`).

### 3.4. Xuất PDF phiếu lương — Lộ trình phát triển (QuestPDF)

* ⚠️ **QuestPDF HIỆN CHƯA ĐƯỢC THAM CHIẾU** ở bất kỳ file `.csproj` nào trong solution và chưa có dòng `using QuestPDF` nào trong mã nguồn nhánh `main`.
* Chuỗi "QuestPDF" chỉ xuất hiện ở hai vị trí mang tính mô tả:
  * `seed_data_24_tables.sql:63` — tên quyền `XUAT_PHIEU_LUONG_PDF` ("Xuất phiếu lương QuestPDF").
  * `Views/Home/Index.cshtml:225` — thông báo *"Xuất phiếu lương PDF bằng QuestPDF đang được phát triển."*
* Khi thuyết minh đồ án phải nói rõ đây là **tính năng đang trong lộ trình hoàn thiện**, danh mục gói QuestPDF được lưu trong tài liệu với tư cách kế hoạch kỹ thuật.

### 3.5. Tóm tắt sai lệch giữa tài liệu cũ và hiện trạng

| Nội dung | Tài liệu cũ ghi | Hiện trạng mã nguồn thực tế |
| :--- | :--- | :--- |
| **EF Core Tools** | Yêu cầu cài đặt | Không cài, không dùng `dotnet ef` (chạy script SQL tay) |
| **Mobile SDK Supabase** | Dùng `supabase-csharp` | Không dùng — dùng `HttpClient` trực tiếp tới PostgREST |
| **Mobile MVVM** | Dùng `CommunityToolkit.Mvvm` | Không — Tự triển khai `INotifyPropertyChanged` thuần |
| **Icon/Splash Mobile** | Khai báo SVG | Bắt buộc dùng PNG để tránh lỗi `MAUIR0004` trên Linux |
| **Engine xuất PDF** | Đã xuất qua QuestPDF | Đang trong lộ trình phát triển, chưa cài package |

---

## 4. Cơ Chế Sao Lưu & Phục Hồi CSDL (27 Bảng Chuẩn 3NF)

**Hiện trạng đã triển khai — sao lưu thủ công từ trong Web Admin:**

* `BackupService` (`Data/Services/BackupService.cs`) nối thẳng PostgreSQL qua EF Core:
  * `ExportDatabaseToJsonAsync()` — trích xuất dữ liệu của **toàn bộ 27 bảng** (bao gồm danh mục, phân ca, chấm công, đơn từ, bảng lương, nhật ký admin và 2 bảng mới `du_an`, `phan_cong_du_an`) thành **một file JSON duy nhất** tải về máy quản trị viên.
  * `RestoreDatabaseFromJsonAsync(json)` — nạp ngược JSON, làm sạch và tái tạo dữ liệu theo thứ tự quan hệ phụ thuộc khóa ngoại (Foreign Key Dependency Graph), đảm bảo không vi phạm ràng buộc toàn vẹn.
  * `GetTableStatisticsAsync()` — thống kê số lượng bản ghi hiện hữu trên từng bảng trong số 27 bảng.
* `BackupController` (`[Authorize(Policy = PermissionCodes.BackupDatabase)]`) là điểm điều khiển duy nhất: `GET /Backup/Export` và `POST /Backup/Restore`.
* Quyền `SAO_LUU_CSDL` ("Sao lưu & Phục hồi CSDL") được cấp phát độc quyền cho nhóm Quản trị viên (Admin).

**Đặc điểm cần lưu ý khi trình bày:**

* Đây là **sao lưu thủ công theo yêu cầu (On-Demand Manual Backup)**, không phải cơ chế tự động theo lịch (không dùng Cron, không có Background Worker chạy ngầm).
* Bản sao lưu dạng snapshot JSON nằm trên máy trạm của Admin, không tự động đồng bộ lên bộ nhớ đám mây.
* Về cơ chế sao lưu hạ tầng Supabase: Gói Supabase Cloud có hỗ trợ backup vật lý hàng ngày (giữ 7 ngày đối với gói Free), tuy nhiên đây là dịch vụ hạ tầng đám mây bên ngoài repo — **cần xác minh trên Dashboard** trước khi khẳng định.

---

## 5. Đối Chiếu Luồng Gọi Hàm Thực Tế

### 5.1. Luồng thực thi Web Admin (C# Controller → EF Core Direct)

Kiến trúc triển khai trong `WebAdmin` hiện **không áp dụng mô hình Service/Repository trung gian**:

* Không có thư mục `Services/` hay `Repositories/` trong project `WebAdmin`.
* **14 Controller MVC tiêm trực tiếp `AppDbContext`** và thực thi truy vấn LINQ:
  ```text
  Controller (DuAnController, NhanVienController...)
     │
     ▼ (Dependency Injection)
  AppDbContext (27 DbSet)
     │
     ▼ (Npgsql Driver)
  PostgreSQL Engine (Supabase Cloud)
  ```
* **Đặc tính bảo mật:** WebAdmin kết nối PostgreSQL bằng chuỗi kết nối trực tiếp (quyền hạn mức database user), do đó **WebAdmin không chịu sự chi phối của RLS**, toàn bộ logic phân quyền phụ thuộc vào bộ lọc `[Authorize]` và mã quyền trong code C#.
* Ngoại lệ: `IBackupService`/`BackupService` là service độc lập duy nhất được tách lớp trong `Data`, phục vụ riêng nghiệp vụ JSON backup/restore.

### 5.2. Luồng thực thi Mobile Client (MVVM → PostgREST + RLS)

```text
View (XAML) 
   │ (Data Binding)
   ▼
ViewModel (MVVM)
   │ (Gọi hàm Async)
   ▼
Mobile Services (AuthService, AttendanceService, ProjectService...)
   │
   ▼
SupabaseRestClient (HttpClient + Anon Key + Bearer JWT Token)
   │ (HTTPS REST API /rest/v1/)
   ▼
Supabase PostgREST Gateway
   │
   ▼ (CHỐT CHẶN BẢO MẬT DUY NHẤT)
PostgreSQL Engine (RLS Policies lọc từng dòng dữ liệu)
```

**Nhận định kiến trúc:** Logic nghiệp vụ hiện nằm tập trung trong Controller (Web) và ViewModel/Service (Mobile). Khi thuyết minh, cần trình bày đây là mô hình "Controller/ViewModel trực tiếp truy xuất dữ liệu", việc chuẩn hóa tầng Service/Repository dùng chung là định hướng nâng cấp trong tương lai.

---

## 6. Các Nguyên Tắc Kiến Trúc & Thiết Kế Trọng Yếu

### 6.1. Cơ Chế Snapshot Bất Biến (Immutable Snapshot) Khi Kết Chuyển Thù Lao Dự Án

Một trong những thách thức nghiệp vụ lớn nhất của khách sạn 5 sao là **tính toàn vẹn của bảng lương lịch sử**: khi các sự kiện/tiệc cưới hoàn thành, nhân viên được chi trả thù lao hỗ trợ. Nếu bảng lương chỉ lưu khóa ngoại trỏ tới bảng dự án, mọi thao tác chỉnh sửa dự án trong tương lai sẽ làm sai lệch bảng lương kế toán đã chốt.

Hệ thống giải quyết bài toán này thông qua **Cơ chế Snapshot Bất Biến (Immutable Snapshot)**:

```text
┌─────────────────────────────────┐
│     BẢNG PHAN_CONG_DU_AN        │
├─────────────────────────────────┤
│ ma_phan_cong_da = 101           │
│ ma_du_an = 12 (Gala Dinner 5★)  │
│ ma_nhan_vien = 45               │
│ so_gio_cong_du_an = 8.5         │
│ phu_cap_du_an = 850,000 VNĐ     │
│ trang_thai_chi_tra =            │
│   'DA_QUYET_TOAN'               │
└────────────────┬────────────────┘
                 │
                 │ Thao tác "Kết chuyển lương" (TransferToPayroll)
                 │ tại DuAnController
                 ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      BẢNG CHI_TIET_PHIEU_LUONG                           │
├──────────────────────────────────────────────────────────────────────────┤
│ ma_chi_tiet = 5024                                                       │
│ ma_phieu_luong = 812 (Phiếu lương Kỳ Tháng 10/2026 của NV #45)           │
│ loai_khoan = 'PhuCap'                                                    │
│ ten_khoan = 'Thù lao sự kiện: Gala Dinner 5★ (Phục vụ tiệc)'             │
│ so_tien = 850,000 VNĐ  ◄── [BẢN SAO BẤT BIẾN - IMMUTABLE VALUE]          │
│ ma_khoan = NULL (Khoản thù lao sự kiện phát sinh ngoài định mức cố định) │
└────────────────┬─────────────────────────────────────────────────────────┘
                 │
                 │ Cập nhật tham chiếu đối soát 2 chiều
                 ▼
┌─────────────────────────────────┐
│     BẢNG PHAN_CONG_DU_AN        │
├─────────────────────────────────┤
│ ma_chi_tiet_phieu_luong = 5024  │
│ trang_thai_chi_tra =            │
│   'DA_KET_CHUYEN_LUONG' (Đã khóa)│
└─────────────────────────────────┘
```

**Các nguyên tắc thực thi bất biến:**

1. **Phân tách trạng thái thanh toán:** Cột `trang_thai_chi_tra` trên `phan_cong_du_an` trải qua các giai đoạn:
   `CHO_QUYET_TOAN` ➔ `CHO_PHE_DUYET` ➔ `DA_QUYET_TOAN` ➔ `DA_KET_CHUYEN_LUONG`.
2. **Khóa ghi sau kết chuyển:** Khi đã chuyển sang `DA_KET_CHUYEN_LUONG`, bản ghi phân công dự án **bị khóa sửa/xóa** ở cả tầng C# và trigger database, ngăn chặn việc thay đổi số tiền thù lao.
3. **Tính độc lập của phiếu lương:** `chi_tiet_phieu_luong` lưu bản sao giá trị số tiền (`so_tien = 850000`) và diễn giải chi tiết (`ten_khoan`). Khi kỳ lương đóng (`ky_luong.trang_thai = 'KhoaSo'`), dữ liệu lương là bất biến vĩnh viễn, phục vụ thanh tra thuế và kiểm toán độc lập.

---

### 6.2. Chính Sách Bảo Mật Hàng (Row Level Security - RLS) Cho Bảng Dự Án & Phân Công

Do Mobile App sử dụng Anon Key công khai, **RLS trên Supabase là chốt chặn bảo vệ dữ liệu duy nhất**. RLS cho 2 bảng mới `du_an` và `phan_cong_du_an` được thiết kế chặt chẽ theo ma trận thẩm quyền:

```text
               TÀI KHOẢN ĐĂNG NHẬP TRÊN MOBILE
                             │
            ┌────────────────┴────────────────┐
            ▼                                 ▼
   [NHÂN VIÊN (Employee)]           [QUẢN LÝ / HR / ADMIN]
            │                                 │
     RLS: CHỈ XEM                     RLS: TOÀN QUYỀN
 ┌──────────────────────┐         ┌──────────────────────┐
 │ - du_an: Chỉ thấy sự │         │ - du_an: Xem, tạo,   │
 │   kiện mình tham gia │         │   sửa, đóng sự kiện  │
 │ - phan_cong_du_an:   │         │ - phan_cong_du_an:   │
 │   Chỉ thấy dòng của  │         │   Điều phối, chấm    │
 │   chính mình         │         │   công, duyệt thù lao│
 └──────────────────────┘         └──────────────────────┘
```

#### Ma trận chính sách RLS chi tiết:

1. **Bảng `phan_cong_du_an`:**
   - **Policy xem cho Nhân viên (`phan_cong_du_an_select_nhan_vien`):**
     Nhân viên thông thường chỉ được xem dòng phân công của chính mình:
     ```sql
     CREATE POLICY "phan_cong_du_an_select_nhan_vien" ON public.phan_cong_du_an
     FOR SELECT TO anon, authenticated
     USING (
       ma_nhan_vien = (
         SELECT tk.ma_nhan_vien FROM public.tai_khoan tk
         WHERE tk.ten_dang_nhap = auth.email() AND tk.trang_thai = true
       )
     );
     ```
   - **Policy toàn quyền cho Quản trị/Quản lý (`phan_cong_du_an_all_quan_ly`):**
     Admin (#1), HR (#2), Manager (#3) có quyền xem, thêm, sửa, đánh giá thù lao nhân viên tham gia sự kiện:
     ```sql
     CREATE POLICY "phan_cong_du_an_all_quan_ly" ON public.phan_cong_du_an
     FOR ALL TO anon, authenticated
     USING (
       EXISTS (
         SELECT 1 FROM public.tai_khoan tk
         WHERE tk.ten_dang_nhap = auth.email()
           AND tk.ma_vai_tro IN (1, 2, 3)
           AND tk.trang_thai = true
       )
     );
     ```

2. **Bảng `du_an`:**
   - **Policy xem cho Nhân viên (`du_an_select_nhan_vien`):**
     Nhân viên chỉ xem được các sự kiện mà mình được ban quản lý phân công tham gia:
     ```sql
     CREATE POLICY "du_an_select_nhan_vien" ON public.du_an
     FOR SELECT TO anon, authenticated
     USING (
       EXISTS (
         SELECT 1 FROM public.phan_cong_du_an pc
         JOIN public.tai_khoan tk ON pc.ma_nhan_vien = tk.ma_nhan_vien
         WHERE pc.ma_du_an = du_an.ma_du_an
           AND tk.ten_dang_nhap = auth.email()
           AND tk.trang_thai = true
       )
     );
     ```
   - **Policy điều hành cho Quản lý/HR/Admin (`du_an_all_quan_ly`):**
     Ban quản lý có toàn quyền tạo mới, lập kế hoạch ngân sách, điều chỉnh tiến độ và nghiệm thu dự án.

---

### 6.3. Khai Báo Fluent API Trong `AppDbContext` (27 Bảng CSDL)

Để tầng ORM EF Core trong `OvertimeHotel.HRM.Data` nhận diện và đồng bộ hoàn hảo 27 bảng CSDL, `AppDbContext.cs` mở rộng danh mục 27 `DbSet` và khai báo cấu hình Fluent API chi tiết cho 2 bảng mới:

```csharp
// 1. Mở rộng 27 DbSet trong AppDbContext
public DbSet<DuAn> DuAns => Set<DuAn>();
public DbSet<PhanCongDuAn> PhanCongDuAns => Set<PhanCongDuAn>();

// 2. Cấu hình Fluent API trong OnModelCreating
modelBuilder.Entity<DuAn>(entity =>
{
    entity.ToTable("du_an");
    entity.HasKey(e => e.MaDuAn);
    entity.Property(e => e.MaDuAn).HasColumnName("ma_du_an");
    entity.Property(e => e.TenDuAn).HasColumnName("ten_du_an").HasMaxLength(200).IsRequired();
    entity.Property(e => e.LoaiDuAn).HasColumnName("loai_du_an").HasMaxLength(50).IsRequired();
    entity.Property(e => e.DiaDiem).HasColumnName("dia_diem").HasMaxLength(200).IsRequired();
    entity.Property(e => e.NgayBatDau).HasColumnName("ngay_bat_dau").IsRequired();
    entity.Property(e => e.NgayKetThuc).HasColumnName("ngay_ket_thuc").IsRequired();
    entity.Property(e => e.NganSachPhuCap).HasColumnName("ngan_sach_phu_cap").HasPrecision(15, 2).HasDefaultValue(0);
    entity.Property(e => e.MaTruongNhom).HasColumnName("ma_truong_nhom").IsRequired();
    entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(30).IsRequired().HasDefaultValue("KHOI_TAO");
    entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
    entity.Property(e => e.NgayTao).HasColumnName("ngay_tao").HasDefaultValueSql("CURRENT_TIMESTAMP");

    entity.HasOne(e => e.TruongNhom)
        .WithMany()
        .HasForeignKey(e => e.MaTruongNhom)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasIndex(e => new { e.NgayBatDau, e.NgayKetThuc, e.TrangThai })
        .HasDatabaseName("idx_du_an_thoi_gian_trang_thai");
});

modelBuilder.Entity<PhanCongDuAn>(entity =>
{
    entity.ToTable("phan_cong_du_an");
    entity.HasKey(e => e.MaPhanCongDa);
    entity.Property(e => e.MaPhanCongDa).HasColumnName("ma_phan_cong_da");
    entity.Property(e => e.MaDuAn).HasColumnName("ma_du_an").IsRequired();
    entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien").IsRequired();
    entity.Property(e => e.VaiTroDuAn).HasColumnName("vai_tro_du_an").HasMaxLength(50).IsRequired();
    entity.Property(e => e.TuNgay).HasColumnName("tu_ngay").IsRequired();
    entity.Property(e => e.DenNgay).HasColumnName("den_ngay").IsRequired();
    entity.Property(e => e.SoGioCongDuAn).HasColumnName("so_gio_cong_du_an").HasPrecision(5, 2).HasDefaultValue(0);
    entity.Property(e => e.PhuCapDuAn).HasColumnName("phu_cap_du_an").HasPrecision(15, 2).HasDefaultValue(0);
    entity.Property(e => e.DanhGiaHoanThanh).HasColumnName("danh_gia_hoan_thanh").HasMaxLength(30);
    entity.Property(e => e.TrangThaiChiTra).HasColumnName("trang_thai_chi_tra").HasMaxLength(30).IsRequired().HasDefaultValue("CHO_QUYET_TOAN");
    entity.Property(e => e.MaChiTietPhieuLuong).HasColumnName("ma_chi_tiet_phieu_luong");
    entity.Property(e => e.NgayPhanCong).HasColumnName("ngay_phan_cong").HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(e => e.NgayNghiemThu).HasColumnName("ngay_nghiem_thu");
    entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");

    // Chặn trùng lặp phân công 1 nhân viên nhiều lần trong cùng 1 sự kiện
    entity.HasIndex(e => new { e.MaDuAn, e.MaNhanVien })
        .IsUnique()
        .HasDatabaseName("uq_phan_cong_du_an_nhan_vien");

    entity.HasOne(e => e.DuAn)
        .WithMany(d => d.DanhSachPhanCong)
        .HasForeignKey(e => e.MaDuAn)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.NhanVien)
        .WithMany()
        .HasForeignKey(e => e.MaNhanVien)
        .OnDelete(DeleteBehavior.Restrict);

    // Liên kết đối soát sang dòng thu nhập trên phiếu lương (cho phép NULL trước khi kết chuyển)
    entity.HasOne(e => e.ChiTietPhieuLuong)
        .WithMany()
        .HasForeignKey(e => e.MaChiTietPhieuLuong)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.SetNull);
});
```

---

## 7. Điểm Rủi Ro Kiến Trúc

Các hệ quả kỹ thuật cần lưu ý và giải trình khi bảo vệ đồ án:

1. **Logic nghiệp vụ trùng lặp ở 2 nơi.**
   `OvertimeHotel.HRM.Data` không phục vụ MobileApp, do đó mọi quy tắc (công thức lương, thẩm quyền duyệt đơn, tính toán thù lao dự án, validation chấm công) phải duy trì song song hai bản mã. Cần tiếp tục đưa các quy tắc tính toán vào `OvertimeHotel.HRM.Core/Policies/` làm chuẩn duy nhất.

2. **Phụ thuộc hoàn toàn vào RLS phía server trên Mobile.**
   MobileApp dùng Anon Key nên **RLS là lớp chặn DUY NHẤT**. Nếu viết thiếu hoặc sai sót một điều kiện trong RLS policy của `du_an` hay `phan_cong_du_an`, toàn bộ dữ liệu sự kiện hoặc thù lao có thể bị lộ cho các tài khoản không phận sự. Khi RLS lọc dòng, PostgREST trả mã `204 No Content` hoặc mảng rỗng `[]` chứ không trả về mã lỗi HTTP 403, đòi hỏi ViewModel phải xử lý giao diện khéo léo để tránh gây hiểu nhầm cho người dùng.

3. **Nguy cơ sai lệch đối soát khi kết chuyển lương nếu không tuân thủ snapshot.**
   Nếu lập trình viên vi phạm nguyên tắc snapshot mà chỉ liên kết khóa ngoại động tới `phan_cong_du_an`, việc cập nhật thù lao dự án sau thời điểm khóa sổ lương sẽ làm vỡ báo cáo tài chính của khách sạn.

4. **Không có lớp caching trung gian.**
   Hệ thống không sử dụng Redis hay distributed cache; mọi truy vấn danh sách sự kiện, phân ca, phiếu lương đều gọi trực tiếp qua Internet tới Supabase Cloud (Singapore). Cần tối ưu hóa câu truy vấn LINQ và PostgREST filter để giảm tải băng thông.

5. **Hai cơ chế kiểm tra quyền song song, dễ lệch nhau.**
   Web kiểm tra bằng code C# (EF Core + `[Authorize]` + Policy), Mobile kiểm tra bằng RLS Postgres. Hai bộ quy tắc phải được đối soát đồng bộ; khi có sự thay đổi quyền hạn chức năng, phải cập nhật đồng thời cả hai nơi.

6. **Sao lưu thủ công, chưa có job định kỳ tự động.**
   Tính năng sao lưu CSDL hiện tại là dạng On-Demand JSON do Admin kích hoạt bằng tay. Để đảm bảo an toàn cấp doanh nghiệp, hệ thống cần bổ sung cron job định kỳ hoặc kết hợp dịch vụ backup vật lý tự động của cloud provider.

7. **Tính năng xuất PDF đang trong giai đoạn phát triển.**
   Gói QuestPDF chưa được cài đặt trong mã nguồn chính thức; tính năng xuất phiếu lương PDF cần được hoàn thiện theo đúng lộ trình kế hoạch.

---

## 8. Những Điểm Cần Xác Minh

| # | Nội dung | Hiện trạng & Lý do chưa xác minh được |
| :---: | :--- | :--- |
| **1** | Supabase có bật **backup tự động hàng ngày** không, thời gian lưu trữ | Là tính năng thuộc hạ tầng tài khoản Supabase Cloud bên ngoài repo — cần kiểm tra trực tiếp trên trang quản trị dự án Supabase. |
| **2** | Tiến độ hoàn thiện gói **QuestPDF** trên các nhánh tính năng | Mã nguồn nhánh `main` chưa có tham chiếu; cần kiểm tra tiến độ của thành viên phụ trách nhánh `feat/payroll-questpdf`. |
| **3** | Khả năng tiêu thụ gói **`supabase` SDK (`1.0.0`)** trong tầng Data | Đã đăng ký DI trong `Program.cs` nhưng các Controller hiện chưa tiêm sử dụng — cần đánh giá khả năng tận dụng cho tính năng Realtime thông báo sự kiện mới. |
| **4** | Đồng bộ triển khai DDL **2 bảng mới `du_an` và `phan_cong_du_an`** trên Cloud | Cần chạy script migration tương ứng lên Supabase trước khi kích hoạt các màn hình tương tác trên Web và Mobile. |
