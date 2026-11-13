# Đồng bộ quyền Mobile

Migration: `../../migrations/20261109_mobile_live_permissions.sql`.

App giữ kiến trúc Supabase trực tiếp. RPC `hrm_my_permissions()` trả quyền hiện tại từ
`vai_tro_quyen`, không lấy quyền cũ trong JWT. Các quy tắc riêng của Admin/IT khớp
`WebAdminPermissions`. Không tự cấp thêm quyền cho vai trò nào.

- `CHAM_CONG_DI_DONG`: tab Chấm Công và thao tác vào/ra ca của bản thân.
- `DUYET_DON_NGHI`: duyệt các loại đơn 1–4, vẫn giới hạn vai trò và phòng ban.
- `DUYET_THOI_VIEC`: duyệt loại đơn 5, vẫn giữ giới hạn vai trò.
- `NOP_DON_TU`: tab Đơn Từ, nộp/rút đơn, đổi ca và giải trình.
- `XEP_CA_LAM`: ô Lịch Ca Bộ Phận và quyền ghi phân ca trực tiếp.
- `XEM_CHAM_CONG_BO_PHAN`: xem công của người khác trong phạm vi vốn được phép.
- `REPORT_VIEW`: tab Thống Kê của HR.

`DUYET_TANG_CA_OT` là quyền của quy trình OT riêng; loại đơn 2 trong database là
nghỉ ốm, không phải tăng ca. Thay đổi quyền không tạo các chức năng chưa được triển khai.
Lịch cá nhân và lịch sử chấm công của bản thân vẫn đọc được khi tắt quyền quẹt thẻ.

App cập nhật mỗi 10 giây khi đang chạy và khi quay lại foreground. Nếu quyền đổi,
app đóng màn hình đang mở và dựng lại tab/trang chủ. Chấm công, duyệt/nộp đơn kiểm
tra lại quyền trước khi gửi; RLS và RPC kiểm tra quyền hiện tại ở database.
Không tải được quyền thì app tạm khóa các chức năng cần quyền đến lần tải thành công.
API MobileBearer cũng tải lại quyền mỗi request. Cần cài lại app bản mới để giao diện
đồng bộ; app cũ vẫn chịu chặn ở database/API.

Kiểm tra chạy được trên Linux, không cần Android SDK:

```sh
dotnet run --project verification/MobilePermissions
dotnet build -t:Rebuild src/OvertimeHotel.HRM.WebAdmin
```

Kiểm tra database: chạy migration và `assertions.sql` trong cùng giao dịch, bỏ
`COMMIT` của migration và kết thúc bằng `ROLLBACK`. Script dùng một phiên Manager
đang hoạt động, thay đổi quyền tạm thời và kiểm tra token giữ nguyên; không thay đổi
bản ghi nghiệp vụ. Không chạy assertions rồi COMMIT vì script xóa grant để kiểm thử.

Để build APK vẫn cần JDK và Android SDK. Biên dịch C#/XAML với MAUI target net10.0
trong bản sao tạm chỉ xác nhận mã dùng chung, không thay thế kiểm thử trên điện thoại.

IT dùng quyền chấm công và nộp đơn cá nhân theo cấu hình của vai trò Employee; các quyền quản trị IT cố định không thay đổi. Kiểm tra bật/tắt bằng `it-inheritance-assertions.sql` trong transaction có rollback.
