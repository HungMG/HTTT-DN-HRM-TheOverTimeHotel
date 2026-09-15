# Hệ Thống Thông Tin Quản Lý Nhân Sự & Tiền Lương (HRM)
### Khách Sạn The OverTime Hotel · Đồ Án Môn Học Hệ Thống Thông Tin Doanh Nghiệp (HTTTDN)

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Database](https://img.shields.io/badge/Database-Supabase%20PostgreSQL-3ECF8E.svg)](https://supabase.com/)
[![Web Admin](https://img.shields.io/badge/Web%20Admin-ASP.NET%20Core%20MVC-blue.svg)](https://dotnet.microsoft.com/apps/aspnet/mvc)
[![Mobile App](https://img.shields.io/badge/Mobile%20App-.NET%20MAUI-purple.svg)](https://dotnet.microsoft.com/apps/maui)
[![Tooling](https://img.shields.io/badge/IDE-Visual%20Studio%202026-c154c1.svg)](https://visualstudio.microsoft.com/)

---

## 🏨 1. Giới Thiệu Đề Tài

Dự án xây dựng giải pháp tin học hóa quản trị nhân sự và tính lương tự động cho doanh nghiệp kinh doanh lưu trú và nghỉ dưỡng quốc tế **The OverTime Hotel**. Đặc thù vận hành phục vụ khách hàng liên tục **24 giờ mỗi ngày (24/7)** đòi hỏi sự phối hợp nhịp nhàng giữa nhiều khối nghiệp vụ (Lễ tân tiền sảnh, Buồng phòng, Nhà hàng & Quầy bar, Kỹ thuật bảo trì, An ninh 24/7).

### Mục tiêu cốt lõi:
- **Tách biệt cổng quản trị máy chủ:** Cổng Web Admin dành riêng cho Quản trị viên (Admin) quản lý tài khoản, phân quyền RBAC và an toàn dữ liệu.
- **Ứng dụng di động tiện ích:** Ứng dụng .NET MAUI cho Quản lý xếp ca và Nhân viên tự phục vụ (quẹt thẻ chấm công, nộp 4 loại đơn điện tử, xem phiếu lương PDF).
- **Cơ sở dữ liệu đám mây trực tuyến 24/7:** Sử dụng Supabase PostgreSQL đảm bảo kết nối đồng bộ giữa Web và Mobile App từ mọi nơi.
- **Tính lương 2 tầng minh bạch (Gross → Net):** Tự động tính ngày công thực tế, phụ cấp ca đêm 30%, tiền tăng ca OT, khấu trừ bảo hiểm bắt buộc 10.5%.

---

## 👥 2. Thành Viên Nhóm Thực Hiện

| STT | Họ và Tên | Mã Số Sinh Viên (MSSV) | Lớp |
|:---:|:---|:---:|:---:|
| 1 | Nguyễn Đình Cường | 3123411043 | DCT123C4 |
| 2 | Châu Quốc Bảo | 3123411024 | DCT123C4 |
| 3 | Nguyễn Hoàng Long | 3123411179 | DCT123C4 |
| 4 | Võ Huỳnh Minh Sang | 3123411256 | DCT123C4 |

---

## 🛠️ 3. Kiến Trúc Kỹ Thuật & Công Nghệ

- **Ngôn ngữ lập trình:** C# .NET 10 (`net10.0`)
- **Web Admin Portal:** ASP.NET Core MVC .NET 10
- **Mobile Application:** .NET MAUI Cross-platform .NET 10 (Android, iOS, Windows)
- **Cơ sở dữ liệu:** Supabase Cloud Database (PostgreSQL Engine) với 17 bảng chuẩn hóa 3NF
- **ORM / Truy xuất CSDL:** Entity Framework Core 10 / Npgsql & Supabase C# SDK
- **Xuất tài liệu PDF:** Thư viện C# QuestPDF

---

## 📁 4. Cấu Trúc Repository

```text
HTTT-DN-HRM-TheOverTimeHotel/
│
├── src/                                       # Mã nguồn Solution C# .NET 10
│   ├── OvertimeHotel.HRM.slnx                 # Solution file Visual Studio 2026
│   ├── OvertimeHotel.HRM.Core/                # Domain Entities, DTOs, Enums dùng chung
│   ├── OvertimeHotel.HRM.Data/                # DbContext, Supabase connection
│   ├── OvertimeHotel.HRM.WebAdmin/            # ASP.NET Core MVC (Web Quản trị)
│   └── OvertimeHotel.HRM.MobileApp/           # .NET MAUI App (Android/iOS/Windows)
│
├── PRD/                                       # Bộ hồ sơ đặc tả yêu cầu hệ thống
│   ├── 01_Tong_Quan_He_Thong.md               # Mô tả khách sạn, cơ cấu tổ chức, 8 bước quy trình
│   ├── 02_Phan_Tich_Chuc_Nang_BFD.md          # 8 nhóm chức năng & Ma trận phân quyền RBAC
│   ├── 03_So_Do_Ngu_Canh_Va_DFD_Muc_1.md      # Sơ đồ ngữ cảnh & DFD Mức 1, ma trận CRUD
│   ├── 04_Thiet_Ke_CSDL_17_Bang.md            # Từ điển dữ liệu chi tiết 17 bảng chuẩn 3NF
│   ├── 05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md# Thuật toán tính lương 2 tầng Gross -> Net
│   └── 06_Kien_Truc_He_Thong_Va_Cau_Truc_Thu_Muc.md # Kiến trúc phân tán & hướng dẫn source code
│
├── .gitignore                                 # Loại trừ toàn bộ file build rác, cache, binary
└── README.md                                  # Tài liệu tổng quan dự án
```

---

## 🚀 5. Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu tiên quyết:
- Đã cài đặt **.NET 10 SDK** (`dotnet --version` >= 10.0.x).
- Đã cài đặt **Visual Studio 2026** (với workloads: *ASP.NET and web development* và *.NET Multi-platform App UI development*).

### Khởi chạy dự án:
1. Mở file Solution `src/OvertimeHotel.HRM.slnx` bằng **Visual Studio 2026**.
2. Để chạy **Cổng Web Admin**:
   * Chuột phải vào `OvertimeHotel.HRM.WebAdmin` -> Chọn **Set as Startup Project**.
   * Bấm `Ctrl + F5` để khởi chạy trên trình duyệt web.
3. Để chạy **Mobile App**:
   * Chuột phải vào `OvertimeHotel.HRM.MobileApp` -> Chọn **Set as Startup Project**.
   * Chọn thiết bị mục tiêu (*Windows Machine* để test nhanh trên máy tính, hoặc *Android Emulator*).
   * Bấm `F5` để biên dịch và chạy ứng dụng.

---

## 📜 6. Chi Tiết Tài Liệu Phân Tích (PRD)

Vui lòng tham khảo thư mục [`PRD/`](PRD/) để tra cứu chi tiết:
- [Tổng quan doanh nghiệp & Ca kíp 24/7](PRD/01_Tong_Quan_He_Thong.md)
- [Phân rã chức năng BFD & Phân quyền RBAC](PRD/02_Phan_Tich_Chuc_Nang_BFD.md)
- [Sơ đồ ngữ cảnh & DFD Mức 1](PRD/03_So_Do_Ngu_Canh_Va_DFD_Muc_1.md)
- [Thiết kế CSDL 17 bảng chuẩn 3NF](PRD/04_Thiet_Ke_CSDL_17_Bang.md)
- [Quy trình tính lương 2 tầng & Phiếu lương PDF](PRD/05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md)
- [Kiến trúc hệ thống & Cấu trúc thư mục](PRD/06_Kien_Truc_He_Thong_Va_Cau_Truc_Thu_Muc.md)
