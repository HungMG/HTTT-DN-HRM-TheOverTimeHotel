# Hệ Thống Thông Tin Quản Lý Nhân Sự & Tiền Lương (HRM)
### Khách Sạn The OverTime Hotel · Đồ Án Môn Học Hệ Thống Thông Tin Doanh Nghiệp (HTTTDN)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Database](https://img.shields.io/badge/Supabase-PostgreSQL%2030%20b%E1%BA%A3ng%203NF-3ECF8E.svg)](https://supabase.com/)
[![Web Admin](https://img.shields.io/badge/Web%20Admin-ASP.NET%20Core%20MVC-blue.svg)](https://dotnet.microsoft.com/apps/aspnet/mvc)
[![Mobile](https://img.shields.io/badge/Mobile-.NET%20MAUI-purple.svg)](https://dotnet.microsoft.com/apps/maui)
[![AI](https://img.shields.io/badge/Tr%E1%BB%A3%20l%C3%BD%20AI-LangGraph%20%2B%20Next.js-black.svg)](#-7-hướng-dẫn-cài-đặt--khởi-chạy)
[![IDE](https://img.shields.io/badge/IDE-Visual%20Studio%202026-c154c1.svg)](https://visualstudio.microsoft.com/)

---

## 🏨 1. Giới Thiệu Đề Tài

Tin học hóa toàn diện công tác quản trị nhân sự và tính lương cho **The OverTime Hotel** — khách sạn nghỉ dưỡng tiêu chuẩn quốc tế vận hành **24/7/365**, không ngừng nghỉ kể cả Lễ, Tết. Đặc thù đó đòi hỏi hệ thống phải quản lý nhịp nhàng 5 khối nghiệp vụ trực ca liên tục (Lễ tân tiền sảnh, Buồng phòng, Nhà hàng & Quầy bar, Kỹ thuật bảo trì, An ninh) cùng mảng sự kiện **Banquet / MICE** phát sinh ngoài giờ.

### Mục tiêu cốt lõi

| Mục tiêu | Cách hiện thực |
| :--- | :--- |
| **Tách biệt cổng quản trị** | Web Admin (ASP.NET Core MVC) dành riêng Quản trị viên: tài khoản, RBAC 5 vai trò, nhật ký kiểm toán |
| **Ứng dụng di động tiện ích** | .NET MAUI cho Quản lý xếp ca và Nhân viên tự phục vụ: quẹt thẻ GPS/Wi-Fi, nộp 4 loại đơn, xem phiếu lương |
| **Dữ liệu đám mây đồng bộ** | Supabase PostgreSQL — **30 bảng chuẩn 3NF**, RLS bật 30/30 bảng |
| **Tính lương 2 tầng minh bạch** | Gross → Net: ngày công thực tế, phụ cấp ca đêm 30%, OT theo hệ số, khấu trừ BH bắt buộc 10.5% |
| **Trợ lý AI nội bộ** | Chatbot LangGraph trả lời số liệu báo cáo theo phạm vi quyền của người hỏi |

---

## 👥 2. Thành Viên Nhóm Thực Hiện

**Chuyên ngành:** Hệ Thống Thông Tin Doanh Nghiệp (Mã HP: 841065) — Khoa Công Nghệ Thông Tin — Trường Đại học Sài Gòn (SGU)

| STT | Họ và Tên | MSSV | Lớp |
|:---:|:---|:---:|:---:|
| 1 | Nguyễn Đình Cường | 3123411043 | DCT123C4 |
| 2 | Châu Quốc Bảo | 3123411024 | DCT123C4 |
| 3 | Nguyễn Hoàng Long | 3123411179 | DCT123C4 |
| 4 | Võ Huỳnh Minh Sang | 3123411256 | DCT123C4 |

---

## 🛠️ 3. Kiến Trúc Kỹ Thuật & Công Nghệ

```text
     [QUẢN TRỊ VIÊN]                       [QUẢN LÝ & NHÂN VIÊN]
     Trình duyệt Web                       Điện thoại Android / iOS
            │                                       │
            ▼                                       ▼
  ┌─────────────────────┐               ┌─────────────────────┐
  │  WebAdmin (MVC)     │               │  MobileApp (MAUI)   │
  │  EF Core + Npgsql   │               │  HttpClient         │
  │                     │               │  → PostgREST        │
  └──────────┬──────────┘               └──────────┬──────────┘
             │           ┌─────────────────────┐   │
             └──────────►│  Supabase Cloud     │◄──┘
                         │  PostgreSQL · 30 bảng│
                         │  RLS 30/30 · 6 trig │
                         └──────────┬──────────┘
                                    │ HTTP + vé dữ liệu ngắn hạn
                         ┌──────────▼──────────┐
                         │ Trợ lý AI (LangGraph)│
                         │ Next.js iframe nhúng │
                         └─────────────────────┘
```

| Thành phần | Công nghệ | Ghi chú |
| :--- | :--- | :--- |
| Ngôn ngữ | C# .NET 10 (`net10.0`) | Nullable bật, file-scoped namespace, `async/await` toàn bộ I/O |
| Web Admin | ASP.NET Core MVC .NET 10 | **21 Controller MVC + 5 API controller**, Razor `.cshtml` |
| Mobile App | .NET MAUI .NET 10 | Android / iOS / Windows, MVVM tự triển khai (không `CommunityToolkit.Mvvm`) |
| CSDL | Supabase Cloud (PostgreSQL 17.6) | 30 bảng 3NF, RLS 30/30, 6 trigger rào chắn |
| Web ↔ CSDL | Entity Framework Core 10 + Npgsql | `AppDbContext` với 30 `DbSet`, Fluent API |
| Mobile ↔ CSDL | `HttpClient` → PostgREST + Anon Key | Không dùng `supabase-csharp` |
| Trợ lý AI | LangGraph (Python/TS backend) + Next.js 14 | Nhúng iframe trong Web Admin, gọi lại API `.NET` để lấy số liệu |
| Xuất PDF | QuestPDF | ⚠️ **Đang trong lộ trình phát triển** — chưa có trong `.csproj` nào |

---

## 🗂️ 4. Mười Một Phân Hệ Nghiệp Vụ

Hệ thống phân rã thành **11 phân hệ cấp 1 · 57 chức năng cấp 2** (chi tiết tại [PRD 02](PRD/02_Phan_Tich_Chuc_Nang_BFD.md)):

| Mã | Phân hệ | Vai trò chịu trách nhiệm chính |
|:---:|:---|:---|
| **1.0** | Quản lý Nhân sự & Hợp đồng lao động | Phòng HR |
| **2.0** | Tài khoản & Phân quyền (RBAC) | Admin + Bộ phận CNTT (IT) |
| **3.0** | Ca làm & Lịch phân ca 24/7 | Quản lý bộ phận & HR |
| **4.0** | Bảng chấm công 24/7 & Chuyên cần | Nhân viên, Quản lý & HR |
| **5.0** | Quản lý Đơn từ điện tử (4 loại đơn) | Toàn bộ nhân viên, Quản lý & HR |
| **6.0** | Tính lương tự động & Phiếu lương PDF | HR & Kế toán |
| **7.0** | Quản lý Phụ cấp | HR & Kế toán |
| **8.0** | Quản lý Tiền thưởng | Ban Giám đốc, HR & Quản lý |
| **9.0** | Quản lý Dự án & Sự kiện Khách sạn | Ban Giám đốc, Quản lý Task Force & HR |
| **10.0** | Báo cáo & Thống kê Quản trị | Ban Giám đốc, HR & Quản lý |
| **11.0** | Quản trị Hệ thống | Admin |

### Phân quyền RBAC — 5 vai trò

`Admin` · `HR` · `Manager` (Quản lý bộ phận) · `Employee` (Nhân viên) · `IT` (Công nghệ thông tin)

Quyền được gán theo mã chuỗi qua bảng trung gian `vai_tro_quyen` (N–N). Đặc biệt, quy trình cấp tài khoản Mobile tách riêng: **HR duyệt HĐLĐ → tạo yêu cầu → IT cấp tài khoản**, có 3 trigger CSDL chặn tự cấp quyền và leo thang đặc quyền.

---

## 🗄️ 5. Cơ Sở Dữ Liệu — 30 Bảng Chuẩn 3NF

Supabase PostgreSQL, chuẩn hóa 3NF, **RLS bật trên 30/30 bảng**, **6 trigger** bảo vệ toàn vẹn nghiệp vụ.

| Nhóm | Số bảng | Các bảng |
| :--- | :---: | :--- |
| **Tổ chức & Nhân sự** | 4 | `phong_ban`, `chuc_vu`, `nhan_vien`, `hop_dong` |
| **Tài khoản & Phân quyền** | 5 | `tai_khoan`, `vai_tro`, `quyen`, `vai_tro_quyen`, `yeu_cau_tai_khoan` |
| **Ca làm & Chấm công 24/7** | 6 | `ca_lam_viec`, `phan_ca`, `cham_cong`, `dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca` |
| **Đơn từ** | 2 | `loai_don`, `don_tu` |
| **Tiền lương & Cấu hình** | 9 | `ky_luong`, `cau_hinh_khoan_luong`, `cau_hinh_phu_cap`, `cau_hinh_khau_tru`, `cau_hinh_tang_ca`, `phieu_luong`, `chi_tiet_phieu_luong`, `chi_tiet_tru_bao_hiem`, `chi_tiet_tru_thue_tncn` |
| **Khấu trừ & Kiểm toán** | 2 | `chi_tiet_tru_vi_pham_tam_ung`, `nhat_ky_quan_tri` |
| **Dự án & Sự kiện** | 2 | `du_an`, `phan_cong_du_an` |
| **TỔNG CỘNG** | **30** | |

### Tiến trình tiến hóa lược đồ

`17 bảng` (thiết kế gốc) → `24 bảng` (chuẩn 3NF, tách phân hệ lương — 07/10/2026) → `25 bảng` (`yeu_cau_tai_khoan`, tách bộ phận IT — 09/10/2026) → `27 bảng` (`du_an`, `phan_cong_du_an` — 14/10/2026) → **`30 bảng`** (`dinh_bien_ca`, `giai_trinh_cham_cong`, `yeu_cau_doi_ca` — 20/10/2026)

> Từ điển dữ liệu đầy đủ từng cột, khóa ngoại, ràng buộc CHECK và policy RLS: xem [PRD 04](PRD/04_Thiet_Ke_CSDL_17_Bang.md).
> *Lưu ý:* tên file `04_Thiet_Ke_CSDL_17_Bang.md` được giữ nguyên để không đứt liên kết, nhưng nội dung mô tả đủ 30 bảng.

### 6 Trigger rào chắn

| Trigger | Bảo vệ điều gì |
| :--- | :--- |
| `trg_chan_cap_quyen_tai_khoan_di_dong` | Chặn gán quyền `CAP_TAI_KHOAN_MOBILE` cho vai trò ngoài Admin/IT, chặn leo quyền |
| `trg_chan_doi_ten_vai_tro_quan_tri` | Khóa bất biến tên vai trò Admin & IT |
| `trg_chan_yeu_cau_tai_khoan_trung` | Chặn gửi trùng yêu cầu cho người đã có tài khoản |
| `trg_chan_yeu_cau_tai_khoan_dung_quyen` | Chặn chốt cấp tài khoản nếu thiếu quyền IT/Admin |
| `trg_chan_phan_ca_khong_hop_dong` | Không xếp ca cho nhân viên không có HĐLĐ còn hiệu lực |
| `trg_chan_cham_cong_khong_hop_dong` | Không ghi nhận chấm công khi HĐLĐ đã hết hạn |

---

## 📁 6. Cấu Trúc Repository

```text
HTTT-DN/
│
├── src/                                          # Solution C# .NET 10 (4 project)
│   ├── OvertimeHotel.HRM.slnx                    # Solution file Visual Studio 2026
│   ├── OvertimeHotel.HRM.Core/                   # 30 Entity, DTOs, Enums, Policies dùng chung
│   ├── OvertimeHotel.HRM.Data/                   # AppDbContext (30 DbSet), Supabase client, DDL & seed
│   ├── OvertimeHotel.HRM.WebAdmin/               # ASP.NET Core MVC — cổng quản trị
│   ├── OvertimeHotel.HRM.MobileApp/              # .NET MAUI — Android / iOS / Windows
│   └── OvertimeHotel.HRM.AiChatbot/              # Trợ lý AI (npm monorepo, KHÔNG trong .slnx)
│       ├── backend/                              #   LangGraph: ingestion & retrieval graph
│       └── frontend/                             #   Next.js 14: giao diện chat 5★
│
├── PRD/                                          # Bộ hồ sơ đặc tả yêu cầu (7 tài liệu)
│   ├── 01_Tong_Quan_He_Thong.md                  # Doanh nghiệp, 11 phân hệ, 30 bảng
│   ├── 02_Phan_Tich_Chuc_Nang_BFD.md             # BFD 57 chức năng & ma trận RBAC
│   ├── 03_So_Do_Ngu_Canh_Va_DFD_Muc_1.md         # Context diagram, DFD Mức 1, CRUD matrix
│   ├── 04_Thiet_Ke_CSDL_17_Bang.md               # Từ điển dữ liệu 30 bảng
│   ├── 05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md   # Công thức lương 2 tầng Gross → Net
│   ├── 06_Kien_Truc_He_Thong_Va_Cau_Truc_Thu_Muc.md
│   └── 07_Tro_Ly_AI_Chatbot_Cau_Truc_Va_Logic.md
│
├── docs/                                         # Tài liệu kỹ thuật chuyên đề
│   ├── hotel-events.md                           #   Đặc tả Phân hệ 9 (Dự án & Sự kiện)
│   └── superpowers/plans/                        #   Kế hoạch triển khai theo từng đợt
│
├── tests/                                        # Kịch bản kiểm thử nghiệp vụ (.mjs, .py) & integration
├── TASKS.md                                      # Bảng phân chia nhiệm vụ đồ án
├── TESTCASES.md                                  # Danh mục ca kiểm thử
└── README.md
```

### Thư mục chỉ tồn tại trên máy lập trình viên (không đẩy lên GitHub)

`.git/info/exclude` ẩn các mục sau khỏi repo công khai vì chứa tài liệu nội bộ hoặc cấu hình riêng:

```text
docs project/     # Báo cáo Word chính thức & sơ đồ (.drawio/.html/.png)
supabase/         # Script migration áp tay lên Supabase Cloud
rules/            # coding-style, git-workflow, graft-rules, mobile-design-rules
memory/           # ADRs, business rules, nhật ký tiến độ
workflows/        # build-and-run, db-migration, ai-chatbot, quy-trinh-cap-tai-khoan
graft/            # Đồ thị quan hệ mã nguồn (AI navigation)
AGENTS.md         # Quy tắc cho AI coding agent
```

> Nếu clone repo về máy mới, các thư mục trên **sẽ không có sẵn** — cần xin từ thành viên nhóm hoặc tự tạo lại.

---

## 🚀 7. Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu tiên quyết

- **.NET SDK 10.0** — kiểm tra `dotnet --version`
- **Visual Studio 2026** với workloads *ASP.NET and web development* và *.NET Multi-platform App UI development*
- **Node.js + npm** (chỉ cần khi chạy Trợ lý AI)
- Tài khoản Supabase Cloud đã cấu hình trong `src/OvertimeHotel.HRM.WebAdmin/appsettings.json`

### 7.1. Biên dịch toàn bộ solution

```bash
dotnet build -t:Rebuild src/OvertimeHotel.HRM.slnx
```

> Yêu cầu nghiêm ngặt của dự án: `Build succeeded. 0 Warning(s), 0 Error(s)`.
> Phải dùng `-t:Rebuild` — `dotnet build` thường trả về "0 Warning" giả do kết quả incremental cache.

### 7.2. Chạy Web Admin

```bash
dotnet run --project src/OvertimeHotel.HRM.WebAdmin --launch-profile http
```

Mặc định lắng nghe tại **http://localhost:5065** (hoặc `https://localhost:7214` với profile `https`).

### 7.3. Chạy Mobile App

```bash
# Android (cần AVD đã khởi động)
dotnet build -f net10.0-android -c Release src/OvertimeHotel.HRM.MobileApp
```

Hoặc mở `src/OvertimeHotel.HRM.slnx` bằng Visual Studio 2026 → *Set as Startup Project* → chọn Android Emulator / Windows Machine → `F5`.

> ⚠️ **Build Mobile luôn dùng `-c Release`** và icon/splash **bắt buộc định dạng PNG**. `Microsoft.Maui.Resizetizer 10.0.110` ném lỗi `MAUIR0004` khi đọc SVG trên Linux.

### 7.4. Chạy Trợ lý AI

```bash
# Backend (LangGraph) — cần .env với AI_API_KEY
cd src/OvertimeHotel.HRM.AiChatbot/backend && npm install && npm run dev

# Frontend (Next.js) — chạy song song ở terminal khác
cd src/OvertimeHotel.HRM.AiChatbot/frontend && npm install && npm run dev
```

Chi tiết cấu hình khóa API và phân quyền kho tri thức: xem [PRD 07](PRD/07_Tro_Ly_AI_Chatbot_Cau_Truc_Va_Logic.md) (§10 Khởi chạy, §11 Ma trận Bảo mật).

### 7.5. Áp dụng migration CSDL

```bash
# KHÔNG dùng `supabase db push` — repo không có bảng supabase_migrations.schema_migrations
supabase db query --linked -f supabase/migrations/20261020_bo_sung_chuc_nang_phan_ca_cham_cong.sql
```

Hướng dẫn đầy đủ về kết nối & chạy script Supabase: xem `workflows/db-migration.md` (thư mục nội bộ, không có trên GitHub).

---

## 🧪 8. Kiểm Thử

Thư mục `tests/` chứa kịch bản kiểm thử nghiệp vụ và tích hợp:

| Nhóm | Nội dung |
| :--- | :--- |
| `AccountQueueRules`, `AccountQueueIntegration` | Ràng buộc quy trình HR → IT cấp tài khoản |
| `AttendanceRules`, `AttendanceFlow` | Tính phút trễ/sớm, OT làm tròn 0.5h, chống trùng ca |
| `PayrollRules` | Công thức lương 2 tầng, BH 10.5%, thuế TNCN lũy tiến |
| `ReportRules` | Phân quyền báo cáo theo vai trò & phòng ban |
| `HotelEvents`, `HotelEventsIntegration` | Vòng đời dự án/sự kiện, kết chuyển thù lao |
| `*.mjs` | Smoke test HTTP thật (payroll, account-queue, hotel-events) |

```bash
# Smoke test HTTP — cần biến môi trường mật khẩu tài khoản test
PAYROLL_TEST_PASSWORD='...' node tests/payroll-smoke.mjs
node tests/account-queue-smoke.mjs
```

---

## 📚 9. Tài Liệu PRD

| Tài liệu | Nội dung |
| :--- | :--- |
| [PRD 01 — Tổng quan](PRD/01_Tong_Quan_He_Thong.md) | Bối cảnh khách sạn 5★, cơ cấu tổ chức, 11 phân hệ, quy mô 30 bảng, ma trận NFR |
| [PRD 02 — BFD & RBAC](PRD/02_Phan_Tich_Chuc_Nang_BFD.md) | Sơ đồ phân rã chức năng 57 chức năng (IPO), ma trận phân quyền 5 vai trò |
| [PRD 03 — Context & DFD](PRD/03_So_Do_Ngu_Canh_Va_DFD_Muc_1.md) | 5 tác nhân ngoài, 4 kho dữ liệu, DFD Mức 1, ma trận CRUD |
| [PRD 04 — CSDL 30 bảng](PRD/04_Thiet_Ke_CSDL_17_Bang.md) | Từ điển dữ liệu, RLS, 6 trigger, ERD, ma trận khóa ngoại |
| [PRD 05 — Tính lương](PRD/05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md) | Công thức Gross → Net, khóa sổ kỳ lương, quy tắc làm tròn |
| [PRD 06 — Kiến trúc](PRD/06_Kien_Truc_He_Thong_Va_Cau_Truc_Thu_Muc.md) | Kiến trúc phân tán, cấu trúc thư mục, gói NuGet thực tế |
| [PRD 07 — Trợ lý AI](PRD/07_Tro_Ly_AI_Chatbot_Cau_Truc_Va_Logic.md) | LangGraph retrieval/ingestion, phân quyền kho tri thức, API cầu dữ liệu |

Sơ đồ đồ họa tương tác (BFD, ERD, Context, DFD) và báo cáo Word nằm trong `docs project/diagrams/` — thư mục nội bộ của nhóm, không có trên GitHub. Đặc tả Phân hệ 9 nằm tại [docs/hotel-events.md](docs/hotel-events.md).

---

## 💰 10. Công Thức Tính Lương Tóm Tắt

$$\text{Net} = \text{Gross} - \text{Tổng Khấu Trừ}$$

$$\text{Gross} = \text{Lương Ngày Công} + \text{Tiền OT} + \text{Phụ Cấp Ca Đêm} + \text{Phụ Cấp Ăn Ca} + \text{Phụ Cấp Khác} + \text{Phụ Cấp Dự Án} + \text{Thưởng}$$

- **Lương ngày công** = `(Lương cơ bản HĐLĐ / 26) × Số ngày công thực tế`
- **Tiền OT** = `(Lương cơ bản / (26 × 8)) × Số giờ OT × Hệ số OT`
- **Phụ cấp ca đêm** = `30%` lương theo giờ cho khung 22:00 – 06:00
- **Bảo hiểm bắt buộc** = `10.5%` (BHXH 8% + BHYT 1.5% + BHTN 1%)
- **Làm tròn OT** xuống bước **0.5 giờ**, chỉ áp dụng cho lần tính mới

Chi tiết đầy đủ gồm thuế TNCN lũy tiến 5 bậc: [PRD 05](PRD/05_Quy_Trinh_Va_Cong_Thuc_Tinh_Luong.md).

---

## ⚙️ 11. Quy Chuẩn Làm Việc Nhóm

| Quy chuẩn | Nội dung cốt lõi |
| :--- | :--- |
| **Coding style C# .NET 10** | PascalCase cho class/method, `_camelCase` cho private field, `decimal` cho tiền tệ, mọi I/O dùng `async/await` + hậu tố `Async` |
| **Git workflow** | Nhánh `feat/...` / `fix/...`, Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`), không commit trực tiếp vào `main` khi làm tính năng lớn |
| **Kiến trúc Graft** | Tra cứu `graft ask` / `graft skeleton` / `graft callers` trước khi đọc source thô, giúp tiết kiệm 10–20× token |
| **Thiết kế Mobile 5★** | Nền trắng `#F8FAFC`, nút navy `#1E3A8A`, điểm nhấn vàng kim `#D4AF37`; **cấm** `ActivityIndicator` (dùng Skeleton Shimmer) và **cấm** `DisplayAlert` (dùng Custom Modal Popup) |

**Hai cổng chất lượng bắt buộc trước khi commit:**

```bash
dotnet build -t:Rebuild src/OvertimeHotel.HRM.slnx   # phải 0 Error(s), 0 Warning(s)
graft build && graft check                            # phải "graph check: OK"
```

> Phải dùng `-t:Rebuild` chứ không phải `dotnet build` — bản build incremental trả về "0 Warning" giả do lấy từ cache.

---

## 📄 12. Giấy Phép & Ghi Chú

Đồ án môn học phục vụ mục đích học tập tại Trường Đại học Sài Gòn. Khóa bí mật, chuỗi kết nối và file `.env` **không được commit** — xem `.gitignore` trước khi đẩy code.
