# PRD 06: Kiến Trúc Hệ Thống & Cấu Trúc Thư Mục Source Code

> **Trạng thái tài liệu:** mô tả **hiện trạng đã triển khai trong mã nguồn** (`src/`) tại tháng 10/2026 kèm **định hướng mở rộng kiến trúc** cho phân hệ Quản lý Dự án & Sự kiện Khách sạn (toàn hệ thống gồm **11 phân hệ chức năng nghiệp vụ** và **30 bảng CSDL chuẩn 3NF**).
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
                ├───────►│    OvertimeHotel.HRM.Core   │◄───────┤ (Dùng chung 30 Entities,
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
                        │    30 Bảng Chuẩn Hóa 3NF      │
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

Hệ thống Quản lý Nhân sự & Tiền lương The OverTime Hotel được chuẩn hóa thành **11 phân hệ nghiệp vụ cấp 1** (57 chức năng cấp 2 theo BFD), vận hành trên nền tảng **30 bảng CSDL chuẩn 3NF**:

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
| **11.0** | **Quản Trị Hệ Thống** | Danh mục cơ cấu tổ chức (phòng ban, chức vụ), cấu hình khung giờ ca 24/7 (độc quyền Admin), thiết lập tham số tỷ lệ định mức, sao lưu và phục hồi CSDL Supabase. | `nhat_ky_quan_tri` (+ toàn bộ 30 bảng) |

---

## 2. Chi Tiết Cấu Trúc Thư Mục `src/`

```text
src/
├── OvertimeHotel.HRM.slnx                     # Solution file Visual Studio 2026
│
├── OvertimeHotel.HRM.Core/                    # [DỰ ÁN DÙNG CHUNG - CLASS LIBRARY .NET 10]
│   ├── Models/                                # 30 Class Entities ánh xạ 30 bảng CSDL chuẩn 3NF
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
│   │   └── MonthlyAttendanceDto.cs            # Tổng hợp công tháng
│   ├── Enums/                                 # Kiểu liệt kê (9 enum)
│   │   ├── RoleType.cs, ShiftType.cs, LeaveStatus.cs, ContractStatus.cs
│   │   ├── AttendanceStatus.cs, EmployeeStatus.cs
│   │   └── PayrollStatus.cs, PeriodStatus.cs, SalaryItemType.cs
│   ├── Policies/                              # Logic nghiệp vụ dùng chung 2 app
│   │   ├── LeaveApprovalPolicy.cs             # Quy tắc ai duyệt loại đơn nào
│   │   ├── AttendanceCalculator.cs            # Chuẩn hoá check-in/check-out
│   │   ├── ContractAttendancePolicy.cs        # Ràng buộc HĐLĐ ↔ chấm công / phân ca
│   │   ├── ShiftSchedulingPolicy.cs           # Khoảng nghỉ ≥ 8h, chống trùng ca
│   │   ├── ShiftSwapPolicy.cs                 # Điều kiện đổi ca giữa hai nhân viên
│   │   ├── GeofencePolicy.cs                  # Bán kính GPS & Wi-Fi hợp lệ khi quẹt thẻ
│   │   ├── AttendanceAccessPolicy.cs          # Ai được xem công của ai
│   │   ├── ReportPolicy.cs                    # Phân quyền số liệu báo cáo theo vai trò
│   │   ├── PersonalIncomeTaxCalculator.cs     # Thuế TNCN lũy tiến 5 bậc
│   │   ├── HotelEventRules.cs                 # Vòng đời & trạng thái sự kiện
│   │   └── HotelEventRoleRules.cs             # Ai được thao tác trên sự kiện nào
│   └── Constants/
│       └── SalaryConstants.cs                 # 26 ngày công, tỷ lệ BH 10.5%, hệ số OT 1.5/2.0/3.0
│
├── OvertimeHotel.HRM.Data/                    # [TẦNG TRUY XUẤT DỮ LIỆU - CHỈ WEBADMIN DÙNG]
│   ├── Context/
│   │   ├── AppDbContext.cs                    # EF Core DbContext, cấu hình quan hệ PK/FK (30 DbSet)
│   │   └── DatabaseSchemaInitializer.cs       # Tự tạo bảng nhật ký admin nếu thiếu
│   ├── Supabase/
│   │   ├── SupabaseClientFactory.cs           # Tạo Supabase SDK client (Realtime/Storage)
│   │   └── SupabaseOptions.cs
│   ├── Services/
│   │   ├── IBackupService.cs                  # Interface sao lưu & phục hồi CSDL
│   │   └── BackupService.cs                   # Xuất/nhập JSON 30 bảng + thống kê
│   ├── migrations/                            # Script SQL áp dụng tay lên Supabase
│   ├── database_schema_3nf.sql                # DDL nền 24 bảng chuẩn 3NF (bản gốc)
│   └── seed_data_24_tables.sql                # Dữ liệu danh mục & tài khoản mẫu
│
│   # 6 bảng còn lại được tạo bằng script migration riêng, không nằm trong DDL nền:
│   #   du_an, phan_cong_du_an      -> migrations/20261015_hotel_events.sql
│   #   dinh_bien_ca, giai_trinh_cham_cong, yeu_cau_doi_ca
│   #                              -> supabase/migrations/20261020_bo_sung_chuc_nang_phan_ca_cham_cong.sql
│
├── OvertimeHotel.HRM.WebAdmin/                # [CỔNG QUẢN TRỊ ADMIN - ASP.NET CORE MVC .NET 10]
│   ├── Controllers/                           # 21 Controller MVC + 5 API controller
│   │   ├── HomeController.cs                  # Dashboard & báo cáo KPI
│   │   ├── NhanVienController.cs              # Hồ sơ nhân viên
│   │   ├── PhongBanController.cs, ChucVuController.cs, CaLamViecController.cs  # Danh mục
│   │   ├── HopDongController.cs, PhanCaController.cs                           # HĐ & xếp ca
│   │   ├── CauHinhLuongController.cs, CauHinhKhoanLuongController.cs           # Tham số lương
│   │   ├── AccountController.cs, UserAccountsController.cs                     # Tài khoản, nhật ký
│   │   ├── YeuCauTaiKhoanController.cs        # Tiếp nhận & cấp tài khoản cho bộ phận IT
│   │   ├── BackupController.cs                # Sao lưu & phục hồi CSDL
│   │   ├── ChamCongController.cs              # Nhật ký chấm công 24/7
│   │   ├── GiaiTrinhChamCongController.cs     # [MỚI] Duyệt giải trình sai lệch giờ quẹt thẻ
│   │   ├── YeuCauDoiCaController.cs           # [MỚI] Duyệt yêu cầu đổi ca
│   │   ├── DonTuController.cs                 # Duyệt 4 loại đơn từ
│   │   ├── BangLuongController.cs             # Bảng lương tháng
│   │   ├── BaoCaoController.cs                # Báo cáo & thống kê quản trị
│   │   ├── AiAssistantController.cs           # Nhúng Trợ lý AI (iframe + vé dữ liệu)
│   │   ├── SuKienController.cs                # [MỚI] Quản trị sự kiện, điều phối nhân sự, chốt thù lao
│   │   └── Api/                               # API cho các luồng dữ liệu
│   │       ├── AttendanceController.cs, LeaveRequestsController.cs
│   │       └── MobileAuthController.cs, MobileProfileController.cs
│   ├── Security/                              # Hạ tầng RBAC (policy, permission codes)
│   ├── Models/                                # ViewModel riêng cho từng màn hình Web
│   ├── Views/                                 # Razor (.cshtml) cho các nhóm chức năng
│   │   ├── SuKien/                            # [MỚI] Index, Details, Create, Edit, Assignment + 9 partial
│   │   ├── GiaiTrinhChamCong/                 # [MỚI] Giải trình sai lệch giờ quẹt thẻ (duyệt 2 cấp)
│   │   └── YeuCauDoiCa/                       # [MỚI] Yêu cầu đổi ca giữa hai nhân viên
│   ├── wwwroot/                               # css / js / lib (Bootstrap, jQuery, react-bits motion)
│   ├── appsettings.json                       # Chuỗi kết nối Supabase PostgreSQL + cấu hình Supabase
│   └── Program.cs                             # Cấu hình DI, Authentication & Authorization
│
└── OvertimeHotel.HRM.MobileApp/               # [ỨNG DỤNG DI ĐỘNG - .NET MAUI .NET 10]
    ├── App.xaml / AppShell.xaml               # Shell + điều hướng tab
    ├── MauiProgram.cs                         # Điểm khởi động & đăng ký DI
    ├── Platforms/                             # Android, iOS, MacCatalyst, Windows
    ├── Views/                                 # 18 màn hình XAML hiện có + định hướng phân hệ Dự án
    │   ├── IntroPage.xaml                     # Màn hình khởi động có thanh tiến trình 0%→100%
    │   ├── LoginPage.xaml                     # Đăng nhập
    │   ├── EmployeeHomePage.xaml              # Trang chủ Nhân viên
    │   ├── ManagerHomePage.xaml               # Trang chủ Quản lý
    │   ├── HrHomePage.xaml                    # Trang chủ Nhân sự (HR)
    │   ├── HrStatisticsPage.xaml              # Báo cáo thống kê dành cho HR
    │   ├── AdminHomePage.xaml                 # Trang chủ Quản trị viên
    │   ├── AdminAuditLogPage.xaml             # Nhật ký kiểm toán quản trị
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
    │   # [ĐỊNH HƯỚNG] ProjectSchedulePage.xaml / ProjectDetailPage.xaml — CHƯA tồn tại
    ├── Views/Controls/                        # Custom component tái sử dụng (Luxury UI 5★)
    │   ├── LuxuryPopup.xaml, ShiftHeroCard.xaml, LocationClockPanel.xaml
    │   └── FeatureTile.xaml, StatTile.xaml, ProfileHeaderBar.xaml
    ├── ViewModels/                            # 19 ViewModel theo mô hình MVVM (tự viết INotifyPropertyChanged)
    │   ├── LoginViewModel.cs, EmployeeHomeViewModel.cs, ManagerHomeViewModel.cs
    │   ├── HrHomeViewModel.cs, HrStatisticsViewModel.cs  # Trang chủ theo vai trò
    │   ├── AdminHomeViewModel.cs, AdminAuditLogViewModel.cs
    │   ├── AttendanceViewModel.cs, AttendanceSummaryViewModel.cs
    │   ├── ScheduleViewModel.cs, PayslipViewModel.cs, ProfileViewModel.cs
    │   ├── LeaveSubmitViewModel.cs, LeaveApprovalViewModel.cs, LeaveDetailViewModel.cs
    │   ├── ChangePasswordViewModel.cs, SettingsViewModel.cs
    │   ├── HomeViewModelBase.cs               # Base class dùng chung cho dashboard
    │   # [ĐỊNH HƯỚNG] ProjectScheduleViewModel.cs / ProjectDetailViewModel.cs — CHƯA tồn tại
    ├── Services/                              # Gọi PostgREST trực tiếp (KHÔNG dùng Supabase SDK)
    │   ├── SupabaseRestClient.cs              # HttpClient → PostgREST, kèm bearer token & RLS
    │   ├── PostgrstJson.cs                    # Đọc/ghi JSON & xử lý NoRowsMatched
    │   ├── AuthService.cs                     # Đăng nhập Supabase GoTrue
    │   ├── AttendanceService.cs, LeaveApprovalService.cs
    │   ├── MobileScheduleService.cs, MobilePayslipService.cs, MobileAttendanceSummaryService.cs
    │   ├── GeoLocationService.cs              # Kiểm tra tọa độ GPS & WiFi BSSID
    │   # [ĐỊNH HƯỚNG] ProjectService.cs — CHƯA tồn tại
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

## 4. Cơ Chế Sao Lưu & Phục Hồi CSDL (30 Bảng Chuẩn 3NF)

**Hiện trạng đã triển khai — sao lưu thủ công từ trong Web Admin:**

* `BackupService` (`Data/Services/BackupService.cs`) nối thẳng PostgreSQL qua EF Core:
  * `ExportDatabaseToJsonAsync()` — trích xuất dữ liệu của **toàn bộ 30 bảng** (bao gồm danh mục, phân ca, chấm công, đơn từ, bảng lương, nhật ký admin, 2 bảng dự án/sự kiện `du_an`, `phan_cong_du_an` và 3 bảng ca & chấm công mở rộng `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`) thành **một file JSON duy nhất** tải về máy quản trị viên.
  * `RestoreDatabaseFromJsonAsync(json)` — nạp ngược JSON, làm sạch và tái tạo dữ liệu theo thứ tự quan hệ phụ thuộc khóa ngoại (Foreign Key Dependency Graph), đảm bảo không vi phạm ràng buộc toàn vẹn.
  * `GetTableStatisticsAsync()` — thống kê số lượng bản ghi hiện hữu trên từng bảng trong số 30 bảng.
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
* **16 Controller MVC tiêm trực tiếp `AppDbContext`** và thực thi truy vấn LINQ:
  ```text
  Controller (SuKienController, NhanVienController...)
     │
     ▼ (Dependency Injection)
  AppDbContext (30 DbSet)
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
│ ma_phan_cong = 101              │
│ ma_du_an = 12 (Gala Dinner 5★)  │
│ ma_nhan_vien = 45               │
│ vai_tro_du_an = 'PhucVuChinh'   │
│ so_tien_phu_cap = 850,000 VNĐ   │
│ trang_thai_chi_tra =            │
│   'CHUA_CHI_TRA'                │
└────────────────┬────────────────┘
                 │
                 │ Thao tác "Kết chuyển lương" khi tính kỳ lương
                 │ tại PayrollCalculationService
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

1. **Phân tách trạng thái thanh toán:** Cột `trang_thai_chi_tra` trên `phan_cong_du_an` là `VARCHAR(30)` với ràng buộc CSDL
   `CHECK (trang_thai_chi_tra IN ('CHUA_CHI_TRA', 'DA_KET_CHUYEN_LUONG', 'HUY_BO'))`
   (`supabase/migrations/20261014_tao_bang_du_an_va_gan_rls.sql:72`). Đánh giá chất lượng công việc nằm ở cột riêng
   `danh_gia` (`CHUA_DANH_GIA`, `HOAN_THANH`, `XUAT_SAC`, `KHONG_THAM_GIA`) — tách khỏi trạng thái chi trả.
2. **Khóa ghi sau kết chuyển:** Khi đã chuyển sang `DA_KET_CHUYEN_LUONG`, bản ghi phân công dự án mang `ma_chi_tiet`
   trỏ ngược về dòng chi tiết phiếu lương, ngăn chặn việc thay đổi số tiền thù lao đã chốt.
3. **Tính độc lập của phiếu lương:** `chi_tiet_phieu_luong` lưu bản sao giá trị số tiền và diễn giải chi tiết (`ten_khoan`).
   Khi kỳ lương khóa sổ (`ky_luong.trang_thai = 'KHOA_SO'`), dữ liệu lương là bất biến vĩnh viễn, phục vụ thanh tra thuế và kiểm toán độc lập.
4. **Điểm thực thi thật trong mã nguồn:** thao tác kết chuyển **không** nằm ở controller sự kiện mà chạy bên trong
   `PayrollCalculationService` (`src/OvertimeHotel.HRM.Data/Services/PayrollCalculationService.cs:548,802`) — tức là
   gắn liền với bước tính kỳ lương, không phải một nút bấm riêng trên màn hình sự kiện.

---

### 6.2. Chính Sách Bảo Mật Hàng (Row Level Security - RLS) Cho Các Bảng Mở Rộng

Do Mobile App sử dụng Anon Key công khai, **RLS trên Supabase là chốt chặn bảo vệ dữ liệu duy nhất**. RLS cho các bảng mới (`du_an`, `phan_cong_du_an` và 3 bảng ca & chấm công) được thiết kế chặt chẽ theo ma trận thẩm quyền:

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

3. **Ba bảng ca & chấm công mở rộng (`dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`):**
   Chính sách dưới đây đã được áp dụng thực tế trên Supabase Cloud (tổng 8 policy), dùng các hàm `SECURITY DEFINER` `vai_tro_hien_tai()`, `nhan_vien_hien_tai()`, `phong_ban_hien_tai()`:

   - **`dinh_bien_ca`** — Danh mục cấu hình, mọi tài khoản đã đăng nhập đều đọc được để hiển thị cảnh báo thiếu người; chỉ Admin (1) và HR (2) được ghi:
     ```sql
     -- doc_dinh_bien_ca: FOR SELECT, USING (true)
     -- ghi_dinh_bien_ca: FOR ALL, USING (vai_tro_hien_tai() = ANY (ARRAY[1, 2]))
     ```

   - **`giai_trinh_cham_cong`** — Nhân viên xem/ghi hồ sơ của chính mình; Quản lý (3) xem hồ sơ của nhân sự cùng phòng ban; Admin/HR xem toàn bộ. Người nộp được sửa hồ sơ còn mở, cấp duyệt được cập nhật trạng thái:
     ```sql
     -- doc_giai_trinh_cham_cong: FOR SELECT
     --   USING (vai_tro_hien_tai() IN (1,2) OR ma_nhan_vien = nhan_vien_hien_tai()
     --          OR (vai_tro_hien_tai() = 3 AND EXISTS (SELECT 1 FROM nhan_vien nv
     --              WHERE nv.ma_nhan_vien = giai_trinh_cham_cong.ma_nhan_vien
     --                AND nv.ma_phong_ban = phong_ban_hien_tai())))
     -- them_giai_trinh_cham_cong: FOR INSERT
     -- sua_giai_trinh_cham_cong: FOR UPDATE
     --   USING (vai_tro_hien_tai() IN (1,2,3) OR ma_nhan_vien = nhan_vien_hien_tai())
     ```

   - **`yeu_cau_doi_ca`** — Hai bên tham gia đổi ca đều thấy yêu cầu; Quản lý (3) chỉ thấy yêu cầu do nhân sự phòng mình gửi; Admin/HR thấy toàn bộ:
     ```sql
     -- doc_yeu_cau_doi_ca: FOR SELECT
     --   USING (vai_tro_hien_tai() IN (1,2) OR ma_nhan_vien_gui = nhan_vien_hien_tai()
     --          OR ma_nhan_vien_nhan = nhan_vien_hien_tai()
     --          OR (vai_tro_hien_tai() = 3 AND EXISTS (SELECT 1 FROM nhan_vien nv
     --              WHERE nv.ma_nhan_vien = yeu_cau_doi_ca.ma_nhan_vien_gui
     --                AND nv.ma_phong_ban = phong_ban_hien_tai())))
     -- them_yeu_cau_doi_ca: FOR INSERT
     -- sua_yeu_cau_doi_ca: FOR UPDATE
     --   USING (vai_tro_hien_tai() IN (1,2,3) OR ma_nhan_vien_gui = nhan_vien_hien_tai()
     --          OR ma_nhan_vien_nhan = nhan_vien_hien_tai())
     ```

---

### 6.3. Khai Báo Fluent API Trong `AppDbContext` (30 Bảng CSDL)

Để tầng ORM EF Core trong `OvertimeHotel.HRM.Data` nhận diện và đồng bộ hoàn hảo 30 bảng CSDL, `AppDbContext.cs` khai báo danh mục 30 `DbSet` và cấu hình Fluent API chi tiết cho các bảng mới (2 bảng dự án/sự kiện và 3 bảng ca & chấm công):

```csharp
// 1. Mở rộng 30 DbSet trong AppDbContext
public DbSet<DuAn> DuAns => Set<DuAn>();
public DbSet<PhanCongDuAn> PhanCongDuAns => Set<PhanCongDuAn>();
public DbSet<YeuCauDoiCa> YeuCauDoiCas => Set<YeuCauDoiCa>();
public DbSet<DinhBienCa> DinhBienCas => Set<DinhBienCa>();
public DbSet<GiaiTrinhChamCong> GiaiTrinhChamCongs => Set<GiaiTrinhChamCong>();

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

// 3. Cấu hình Fluent API cho 3 bảng ca & chấm công mở rộng (PRD 3.4 / 3.5 / 4.5)

// YeuCauDoiCa: ba khoá ngoại cùng trỏ về NhanVien nên bắt buộc khai báo tường minh,
// để EF tự suy luận sẽ báo lỗi vì không biết navigation nào ứng với cột nào.
modelBuilder.Entity<YeuCauDoiCa>(entity =>
{
    entity.ToTable("yeu_cau_doi_ca");
    entity.HasKey(e => e.MaYeuCau);
    entity.Property(e => e.MaYeuCau).HasColumnName("ma_yeu_cau");
    entity.Property(e => e.MaPhanCaGui).HasColumnName("ma_phan_ca_gui").IsRequired();
    entity.Property(e => e.MaPhanCaNhan).HasColumnName("ma_phan_ca_nhan").IsRequired();
    entity.Property(e => e.MaNhanVienGui).HasColumnName("ma_nhan_vien_gui").IsRequired();
    entity.Property(e => e.MaNhanVienNhan).HasColumnName("ma_nhan_vien_nhan").IsRequired();
    entity.Property(e => e.LyDo).HasColumnName("ly_do").HasMaxLength(500).IsRequired();
    entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(20).HasDefaultValue("CHO_DUYET").IsRequired();
    entity.Property(e => e.MaNguoiDuyet).HasColumnName("ma_nguoi_duyet");
    entity.Property(e => e.NgayGui).HasColumnName("ngay_gui").HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(e => e.NgayDuyet).HasColumnName("ngay_duyet");
    entity.Property(e => e.PhanHoiDuyet).HasColumnName("phan_hoi_duyet").HasMaxLength(500);

    entity.HasOne(e => e.PhanCaGui).WithMany(p => p.YeuCauDoiCaGui)
        .HasForeignKey(e => e.MaPhanCaGui).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.PhanCaNhan).WithMany(p => p.YeuCauDoiCaNhan)
        .HasForeignKey(e => e.MaPhanCaNhan).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.NhanVienGui).WithMany(n => n.YeuCauDoiCaDaGui)
        .HasForeignKey(e => e.MaNhanVienGui).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(e => e.NhanVienNhan).WithMany(n => n.YeuCauDoiCaDaNhan)
        .HasForeignKey(e => e.MaNhanVienNhan).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(e => e.NguoiDuyet).WithMany(n => n.YeuCauDoiCaDaDuyet)
        .HasForeignKey(e => e.MaNguoiDuyet).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
});

modelBuilder.Entity<DinhBienCa>(entity =>
{
    entity.ToTable("dinh_bien_ca");
    entity.HasKey(e => e.MaDinhBien);
    entity.Property(e => e.MaDinhBien).HasColumnName("ma_dinh_bien");
    entity.Property(e => e.MaCa).HasColumnName("ma_ca").IsRequired();
    entity.Property(e => e.MaChucVu).HasColumnName("ma_chuc_vu").IsRequired();
    entity.Property(e => e.SoNguoiToiThieu).HasColumnName("so_nguoi_toi_thieu").HasDefaultValue(1).IsRequired();

    // Mỗi cặp (ca, chức vụ) chỉ có đúng một dòng định biên; hai dòng cho cùng một
    // cặp là dữ liệu mâu thuẫn không biết lấy số nào.
    entity.HasIndex(e => new { e.MaCa, e.MaChucVu }).IsUnique();

    entity.HasOne(e => e.CaLamViec).WithMany(c => c.DinhBienCas)
        .HasForeignKey(e => e.MaCa).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.ChucVu).WithMany(c => c.DinhBienCas)
        .HasForeignKey(e => e.MaChucVu).OnDelete(DeleteBehavior.Cascade);
});

// GiaiTrinhChamCong: ba khoá ngoại trỏ về NhanVien (người gửi, Quản lý xác minh, HR xác nhận).
modelBuilder.Entity<GiaiTrinhChamCong>(entity =>
{
    entity.ToTable("giai_trinh_cham_cong");
    entity.HasKey(e => e.MaGiaiTrinh);
    entity.Property(e => e.MaGiaiTrinh).HasColumnName("ma_giai_trinh");
    entity.Property(e => e.MaPhanCa).HasColumnName("ma_phan_ca").IsRequired();
    entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien").IsRequired();
    entity.Property(e => e.GioDeXuatVao).HasColumnName("gio_de_xuat_vao");
    entity.Property(e => e.GioDeXuatRa).HasColumnName("gio_de_xuat_ra");
    entity.Property(e => e.LyDo).HasColumnName("ly_do").HasMaxLength(500).IsRequired();
    entity.Property(e => e.MinhChung).HasColumnName("minh_chung").HasMaxLength(500);
    entity.Property(e => e.TrangThai).HasColumnName("trang_thai").HasMaxLength(20).HasDefaultValue("CHO_QUAN_LY").IsRequired();
    entity.Property(e => e.MaQuanLyXacMinh).HasColumnName("ma_quan_ly_xac_minh");
    entity.Property(e => e.NgayQuanLyXacMinh).HasColumnName("ngay_quan_ly_xac_minh");
    entity.Property(e => e.YKienQuanLy).HasColumnName("y_kien_quan_ly").HasMaxLength(500);
    entity.Property(e => e.MaHrXacNhan).HasColumnName("ma_hr_xac_nhan");
    entity.Property(e => e.NgayHrXacNhan).HasColumnName("ngay_hr_xac_nhan");
    entity.Property(e => e.YKienHr).HasColumnName("y_kien_hr").HasMaxLength(500);
    entity.Property(e => e.NgayGui).HasColumnName("ngay_gui").HasDefaultValueSql("CURRENT_TIMESTAMP");

    // Mỗi ca chỉ nộp một giải trình đang mở — sau khi HR duyệt hoặc từ chối
    // người dùng mới nộp lại được. Chặn bằng index có điều kiện để không
    // vướng các giải trình cũ đã đóng.
    entity.HasIndex(e => e.MaPhanCa)
        .HasDatabaseName("uq_giai_trinh_dang_mo")
        .IsUnique()
        .HasFilter("trang_thai IN ('CHO_QUAN_LY', 'CHO_HR')");

    entity.HasOne(e => e.PhanCa).WithMany()
        .HasForeignKey(e => e.MaPhanCa).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.NhanVien).WithMany(n => n.GiaiTrinhChamCongDaGui)
        .HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.QuanLyXacMinh).WithMany(n => n.GiaiTrinhQuanLyXacMinh)
        .HasForeignKey(e => e.MaQuanLyXacMinh).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    entity.HasOne(e => e.HrXacNhan).WithMany(n => n.GiaiTrinhHrXacNhan)
        .HasForeignKey(e => e.MaHrXacNhan).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
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
| **4** | Đồng bộ triển khai DDL **5 bảng mới** (`du_an`, `phan_cong_du_an`, `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca`) trên Cloud | **Đã xác minh xong:** cả 5 bảng đã hiện diện trên Supabase Cloud (30/30 bảng `public` có RLS bật). Lưu ý: thư mục `supabase/migrations/` đang bị `.git/info/exclude` bỏ qua — thành viên clone repo mới cần chạy tay script `20261020_bo_sung_chuc_nang_phan_ca_cham_cong.sql` bằng `supabase db query --linked -f`. |
