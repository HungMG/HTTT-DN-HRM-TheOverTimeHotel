# Kiểm thử nghiệm thu ngân sách trên PostgreSQL thật

Chạy từ thư mục gốc repository. Runner không đọc `appsettings`, không truy cập
CSDL đang cấu hình của ứng dụng và từ chối CSDL không rỗng. Mỗi lượt chạy cần
một CSDL mới với tên bắt đầu bằng `hrm_budget_acceptance_`, ở
`127.0.0.1:55439`. PostgreSQL user cần quyền tạo schema và tạo hai role thử
`anon`, `authenticated` nếu chưa tồn tại. Chỉ dùng PostgreSQL cluster thử nghiệm.

```sh
export HRM_ACCEPTANCE_DB='Host=127.0.0.1;Port=55439;Database=hrm_budget_acceptance_YOUR_RUN;Username=YOUR_TEST_USER'
dotnet run --project tests/PayrollBudgetAcceptance -p:UseSharedCompilation=false
```

Runner tạo schema nền từ EF model hiện tại, bỏ riêng `NganSachLuong`, rồi chạy
chính script `20261028_approved_payroll_budget.sql` hai lần. Các ca kiểm thử gồm:

- Migration chạy lại, RLS và quyền của client; thử đọc thật bằng role `authenticated`.
- Mã tài khoản khác mã nhân viên; từ chối phiên thiếu claim nhân viên.
- Lưu, sửa, gửi duyệt, chặn tự duyệt, duyệt độc lập, khóa và chặn sửa sau khóa.
- Hai context/kết nối tranh chấp sửa cùng phiên bản và tranh chấp duyệt/từ chối.
- Từ chối, sửa số tiền, gửi lại; chặn trang duyệt cũ.
- Unique tháng/phòng ban và ràng buộc tự duyệt tại PostgreSQL.
- Đối soát phiếu lương Gross 9.000.000 với ngân sách duyệt 12.000.000,
  chênh lệch -3.000.000 và lựa chọn phòng ban ban đầu.

## Kiểm thử trình duyệt

Sau runner, chạy WebAdmin với `ConnectionStrings__DefaultConnection` trỏ tới
**đúng CSDL thử vừa tạo**, `JWT_KEY` thử dài ít nhất 32 ký tự, và
`Supabase__Url=http://127.0.0.1:55440` để cô lập dịch vụ bên ngoài.
Khởi động web sau khi runner hoàn tất, tại `http://127.0.0.1:5065`.
Các bảng sự kiện dùng SQL riêng ngoài EF model; để thử toàn ứng dụng cần triển
khai các migration sự kiện thích hợp vào CSDL thử. Runner này tập trung ngân sách.
Lượt kiểm thử trình duyệt ngày 29/10/2026 đã áp dụng thêm, theo thứ tự,
`20261015_hotel_events.sql`, `20261021_event_acceptance_budget.sql`,
`20261023_event_hourly_shift_planning.sql` vào CSDL fixture trước khi khởi động web;
worker sự kiện khởi động không còn báo thiếu bảng. Đây không phải xác nhận toàn
bộ chức năng sự kiện.

```sh
ConnectionStrings__DefaultConnection="$HRM_ACCEPTANCE_DB" \
JWT_KEY='acceptance-local-test-key-with-32-characters' \
Supabase__Url='http://127.0.0.1:55440' \
dotnet run --project src/OvertimeHotel.HRM.WebAdmin --no-build --no-launch-profile -- \
  --urls http://127.0.0.1:5065 --environment Development
```

Trong terminal khác, dùng Python có `playwright` và Chromium:

```sh
python3 tests/PayrollBudgetAcceptance/browser.py
```

Script dùng hai tài khoản **chỉ có trong CSDL thử**: `budget7101`, `budget7102`,
mật khẩu fixture `Acceptance-local-only-2026!`. Hai phiên trình duyệt độc lập
kiểm tra lưu/sửa, trang sửa cũ, gửi duyệt, chặn tự duyệt, duyệt và khóa;
kiểm tra bộ lọc tháng/quý/năm/phòng ban, Excel/CSV và trang in. Script tạo ngân
sách tháng 7 nên cũng cần fixture mới mỗi lượt chạy.

Ảnh, file tải và kết quả mặc định ở `/tmp/hrm-budget-acceptance`, có thể đổi bằng
`HRM_ACCEPTANCE_ARTIFACTS`. File tải được kiểm tra nội dung OpenXML/CSV và PDF
được tạo bằng Chromium. Không thay thế nghiệm thu Microsoft Excel hoặc máy in thật.

## Phạm vi xác nhận

Đây là PostgreSQL local với dữ liệu fixture, cookie login và hai phiên Admin.
Không xác nhận schema cũ/dữ liệu thật, Supabase gateway/JWT, các vai trò HR/Manager/IT,
hoặc việc triển khai production. Migration production vẫn cần xác định CSDL đích
và phê duyệt triển khai riêng. Runner không tự xóa CSDL thử sau khi chạy.

## Kết quả ngày 29/10/2026

- PostgreSQL 18.6 local: **11/11 đạt**. Migration tạo bảng mới và chạy lại thành công.
- Chromium / Playwright, hai phiên đăng nhập: **9/9 đạt**.
- Hồi quy: `ReportRules` **33/33**, `WebAdminReports` **17/17**,
  `node tests/report-ui-state.js`, kiểm tra cú pháp JS và `git diff --check` đạt.
- LibreOffice headless đọc và lưu lại workbook XLSX thành công; PDF A4 một trang
  đã render để kiểm tra bố cục. Chưa nghiệm thu Microsoft Excel hoặc máy in thật.
- Sửa hai lỗi được tái hiện trước khi sửa: controller nhầm account ID với employee ID;
  danh sách phòng ban chưa chọn đúng giá trị khi mở URL báo cáo trực tiếp.
- Web thử: `http://127.0.0.1:5065`; PostgreSQL thử: `127.0.0.1:55439`.
  PostgreSQL chạy từ gói giải nén trong `/tmp/hrm-budget-acceptance`, không cài service hệ thống.
- Không chạy migration lên Supabase đang cấu hình; không commit/push.
