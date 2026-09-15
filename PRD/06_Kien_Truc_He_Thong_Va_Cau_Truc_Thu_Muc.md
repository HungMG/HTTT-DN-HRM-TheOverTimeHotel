# PRD 06: Kiến Trúc Hệ Thống & Cấu Trúc Thư Mục Source Code

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
                ├───────►│    OvertimeHotel.HRM.Core   │◄───────┤ (Dùng chung 17 Entities,
                │        │     Class Library .NET 10   │        │  DTOs & Enums)
                │        └─────────────────────────────┘        │
                ▼                                               ▼
┌───────────────────────────────┐               ┌───────────────────────────────┐
│    OvertimeHotel.HRM.Data     │               │   Supabase C# Client SDK      │
│  Entity Framework Core 10     │               │  (REST API / Realtime Socket) │
│  PostgreSQL Connection Pool   │               │                               │
└───────────────┬───────────────┘               └───────────────┬───────────────┘
                │                                               │
                └───────────────────────┬───────────────────────┘
                                        │ (Kết nối trực tuyến 24/7)
                                        ▼
                        ┌───────────────────────────────┐
                        │     SUPABASE CLOUD DATABASE   │
                        │       PostgreSQL Engine       │
                        │    17 Bảng Chuẩn Hóa 3NF      │
                        │   Tự Động Sao Lưu Hàng Ngày   │
                        └───────────────────────────────┘
```

---

## 2. Chi Tiết Cấu Trúc Thư Mục `src/`

```text
src/
├── OvertimeHotel.HRM.slnx                     # Solution file Visual Studio 2026
│
├── OvertimeHotel.HRM.Core/                    # [DỰ ÁN DÙNG CHUNG - CLASS LIBRARY .NET 10]
│   ├── Models/                                # 17 Class Entities ánh xạ 17 bảng CSDL
│   │   ├── PhongBan.cs
│   │   ├── ChucVu.cs
│   │   ├── NhanVien.cs
│   │   ├── HopDong.cs
│   │   ├── TaiKhoan.cs
│   │   ├── VaiTro.cs
│   │   ├── Quyen.cs
│   │   ├── VaiTroQuyen.cs
│   │   ├── CaLamViec.cs
│   │   ├── PhanCa.cs
│   │   ├── ChamCong.cs
│   │   ├── LoaiDon.cs
│   │   ├── DonTu.cs
│   │   ├── KyLuong.cs
│   │   ├── CauHinhKhoanLuong.cs
│   │   ├── PhieuLuong.cs
│   │   └── ChiTietPhieuLuong.cs
│   ├── DTOs/                                  # Data Transfer Objects
│   │   ├── LoginDto.cs
│   │   ├── CheckInDto.cs
│   │   ├── LeaveRequestDto.cs
│   │   └── PayslipPdfDto.cs
│   ├── Enums/                                 # Kiểu liệt kê
│   │   ├── RoleType.cs
│   │   ├── ShiftType.cs
│   │   ├── LeaveStatus.cs
│   │   └── ContractStatus.cs
│   └── Constants/                             # Các hằng số hệ thống (26 ngày công, tỷ lệ BH 10.5%)
│
├── OvertimeHotel.HRM.Data/                    # [TẦNG TRUY XUẤT DỮ LIỆU - CLASS LIBRARY .NET 10]
│   ├── Context/
│   │   └── AppDbContext.cs                    # EF Core DbContext cấu hình quan hệ PK, FK
│   └── Supabase/
│       └── SupabaseConfig.cs                  # Cấu hình kết nối Supabase Client
│
├── OvertimeHotel.HRM.WebAdmin/                # [CỔNG QUẢN TRỊ ADMIN - ASP.NET CORE MVC .NET 10]
│   ├── Controllers/
│   │   ├── AccountController.cs               # Quản lý tài khoản và phân quyền RBAC
│   │   ├── DepartmentController.cs            # Quản lý danh mục phòng ban, chức vụ
│   │   ├── ShiftController.cs                 # Quản lý danh mục ca làm việc 24/7
│   │   └── BackupController.cs                # Tạo bản sao lưu tức thì & khôi phục CSDL
│   ├── Views/
│   │   ├── Account/
│   │   ├── Department/
│   │   └── Shared/_Layout.cshtml
│   ├── appsettings.json                       # Chuỗi kết nối Supabase PostgreSQL
│   └── Program.cs                             # Cấu hình Dependency Injection & Authentication
│
└── OvertimeHotel.HRM.MobileApp/               # [ỨNG DỤNG DI ĐỘNG - .NET MAUI .NET 10]
    ├── Platforms/                             # Android, iOS, Windows, MacCatalyst
    ├── Views/                                 # Màn hình XAML
    │   ├── LoginPage.xaml                     # Đăng nhập bằng tài khoản
    │   ├── AttendancePage.xaml                # Quẹt thẻ Check-in/out ca kíp
    │   ├── ShiftSchedulePage.xaml             # Xem và xếp lịch ca trực
    │   ├── LeaveFormPage.xaml                 # Nộp và duyệt 4 loại đơn
    │   └── PayslipPage.xaml                   # Xem và tải phiếu lương PDF
    ├── ViewModels/                            # Logic tương tác MVVM
    ├── Services/                              # Gọi Supabase API & QuestPDF
    └── MauiProgram.cs                         # Điểm khởi động ứng dụng di động
```

---

## 3. Các Gói Thư Viện NuGet Cần Thiết

1. **Cho `OvertimeHotel.HRM.Data`:**
   - `Npgsql.EntityFrameworkCore.PostgreSQL`
   - `Microsoft.EntityFrameworkCore.Tools`
2. **Cho `OvertimeHotel.HRM.MobileApp`:**
   - `supabase-csharp`
   - `CommunityToolkit.Mvvm`
3. **Cho xuất PDF Phiếu lương:**
   - `QuestPDF`
