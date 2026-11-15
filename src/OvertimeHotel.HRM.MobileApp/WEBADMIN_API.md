# Kết nối dữ liệu HR trên mobile

Trang chủ HR và màn hình Thống kê gọi trực tiếp Supabase qua `SupabaseRestClient`, khóa anon công khai và JWT của người đăng nhập. Không cần host WebAdmin hoặc cấu hình `WebAdminApiUrl` cho hai màn hình này.

- `rpc/hrm_hr_overview?p_view=dashboard`: nhân sự đang làm việc, quân số ca đã công bố, số đơn, hợp đồng sắp hết hạn và sự kiện nhân sự thật.
- `rpc/hrm_hr_overview?p_view=statistics`: số nhân sự hiện tại, cơ cấu phòng ban, trình độ, thâm niên, chuyên cần tháng hiện tại và tổng quỹ lương đã tính.

Hàm chỉ đọc dành cho HR/Admin có phiên hợp lệ. Dashboard yêu cầu `QL_NHAN_SU`, thống kê yêu cầu `REPORT_VIEW`; hợp đồng và số liệu lương còn kiểm tra quyền tương ứng. Không mở rộng quyền đọc trực tiếp phiếu lương của người khác. Các vai trò khác bị từ chối ngay tại Supabase.

Migration: `src/OvertimeHotel.HRM.Data/migrations/20261115_hr_mobile_overview_rpc.sql`. Có file rollback đi kèm. Kiểm tra CSDL nằm trong `src/OvertimeHotel.HRM.Data/verification/HrMobileOverview/assertions.sql` và được chạy trong transaction cách ly rồi rollback.

Quân số là số ca đã check-in / số ca được xếp hôm nay, không suy diễn thành đủ định biên. Số lương gộp không xác nhận tiền đã chuyển. Khi lỗi tải, xóa dữ liệu cũ và hiện thông báo; không có số liệu mẫu dự phòng.

Kiểm tra service RPC và hai ViewModel (stub nền tảng MAUI, không gọi production):

```bash
dotnet build verification/HrDashboard/HrDashboard.csproj -m:1
dotnet run --project verification/HrDashboard --no-build --no-restore
```
