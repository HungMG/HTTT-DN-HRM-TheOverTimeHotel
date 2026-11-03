# Giải trình chấm công qua Supabase

Android gọi `rest/v1/rpc/hrm_attendance_explanation` bằng JWT người dùng để tải ca đủ điều kiện, xem danh sách/chi tiết, gửi và rút giải trình. Không cần WebAdminApiUrl cho các thao tác này. Quản lý xác minh và HR duyệt trên WebAdmin theo luồng hiện có; chỉ bước HR duyệt mới cập nhật chấm công.

RPC xác định nhân viên từ helper `nhan_vien_hien_tai()` sử dụng claim server cấp, kiểm tra tài khoản đang hoạt động. Chỉ nộp cho ca của mình đã kết thúc trong 45 ngày; ít nhất một giờ đề xuất, không ở tương lai, giờ ra sau giờ vào nếu có cả hai, lý do bắt buộc tối đa 500 ký tự, minh chứng tối đa 500 ký tự. Mốc giờ được chuẩn hóa UTC. Một ca chỉ có một đơn đang mở. Chỉ chủ đơn được rút khi CHO_QUAN_LY; đã chuyển CHO_HR hoặc kết thúc thì không được rút.

Danh sách riêng chỉ trả đơn của mình. Chi tiết giữ quyền tương đương API cũ: chủ đơn, quản lý cùng phòng ban hoặc Admin/HR/IT. Không cung cấp hành động duyệt trong RPC. Tham số client không được quyết định nhân viên, trạng thái hoặc người duyệt. Role anon không EXECUTE; authenticated không được ghi trực tiếp bảng. SECURITY DEFINER có search_path cố định và owner tin cậy.

## Triển khai 03/11/2026

- Project: `soethtbttktnwtxyxjrj`, được kiểm kê trước khi triển khai: bảng đủ cột/CHECK và index duy nhất `uq_giai_trinh_dang_mo`; authenticated chỉ SELECT; 0 đơn hiện có.
- Backup pg_dump custom của bảng và ACL/policy tại `/home/bryan/.local/share/hrm-deployment-backups/20261103-attendance-explanation` (thư mục 700, file 600). Restore schema/data vào DB cách ly với các bảng/helper giả lập phụ thuộc đã thành công; kiểm thử trên schema khôi phục đạt. Không phải bản backup toàn Supabase.
- Migration `20261103_attendance_explanation_supabase_rpc.sql` đã commit; không thay đổi các bản ghi nghiệp vụ. Kiểm tra owner postgres, SECURITY DEFINER, search_path pg_catalog/public, authenticated EXECUTE, anon không EXECUTE, không có quyền ghi trực tiếp bảng cho authenticated.
- APK Debug arm64 build 0 lỗi, 49 cảnh báo; cập nhật tại chỗ qua adb install -r thành công, giữ phiên đăng nhập. Màn hình thiết bị tải ca 30/10 và giờ chấm công thực tế từ Supabase thành công.
- Chưa tự gửi/rút hoặc duyệt giải trình thật. Cần người dùng chọn ca test và giờ đề xuất; sau đó đối chiếu các bước CHO_QUAN_LY → CHO_HR → DA_DUYET, và chỉ sau HR duyệt mới thay đổi công.

## Kiểm thử cách ly

Chỉ chạy trên DB mới, rỗng, tên bắt đầu `hrm_explanation_`. Fixture tạo bảng và role giả lập, dùng fixture đổi ca dùng chung. Không chạy trên production.

```sh
createdb hrm_explanation_local
psql -X -v ON_ERROR_STOP=1 -v skip_rpc=1 -d hrm_explanation_local -f src/OvertimeHotel.HRM.Data/verification/AttendanceExplanationSupabase/fixture.sql
psql -X -v ON_ERROR_STOP=1 -d hrm_explanation_local -f src/OvertimeHotel.HRM.Data/migrations/20261103_attendance_explanation_supabase_rpc.sql -f src/OvertimeHotel.HRM.Data/verification/AttendanceExplanationSupabase/assertions.sql
python3 src/OvertimeHotel.HRM.Data/verification/AttendanceExplanationSupabase/concurrency.py psql -d hrm_explanation_local
dotnet run --project verification/AttendanceExplanationChecks/AttendanceExplanationChecks.csproj
```

Đã kiểm tra scope ca, quyền chủ đơn/manager/HR, kiểm tra đầu vào, UTC +07, chống chèn trạng thái đã duyệt, không ghi công ở bước nhân viên, rút/không được rút sau xác minh, tài khoản khóa, truy cập anon, hai client gửi đồng thời chỉ tạo một đơn, rollback giữ lịch sử. Kiểm thử service liên kết code thật, chỉ stub HTTP: payload, DTO ngày/giờ, null detail, lỗi và cancellation.

Rollback: phát hành lại client gọi WebAdmin trước, sau đó chạy file rollback tương ứng để gỡ RPC. Giữ lịch sử đơn và quyền cấm ghi trực tiếp bảng. Mọi triển khai lại cần kiểm kê đúng project, backup và dừng nếu schema/quyền khác.
