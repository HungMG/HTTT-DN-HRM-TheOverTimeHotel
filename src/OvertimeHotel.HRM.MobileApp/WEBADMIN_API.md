# Cấu hình API WebAdmin cho mobile

Trang thống kê HR lấy dữ liệu tháng hiện tại từ API WebAdmin và yêu cầu quyền xem báo cáo. Quyền xem báo cáo bao gồm số liệu lương trong thống kê. Khi build, cấu hình URL gốc HTTPS của WebAdmin đã triển khai:

```bash
dotnet build src/OvertimeHotel.HRM.MobileApp/OvertimeHotel.HRM.MobileApp.csproj -c Release -p:WebAdminApiUrl=https://TEN_MAY_CHU_WEBADMIN
```

Thay `TEN_MAY_CHU_WEBADMIN` bằng địa chỉ thật có chứng chỉ HTTPS hợp lệ. Không đặt token hoặc mật khẩu trong URL. Nếu không cấu hình, thống kê báo thiếu cấu hình thay vì hiển thị số mẫu. Trang thống kê cũng cần WebAdmin đang chạy và thiết bị truy cập được địa chỉ này; chế độ demo không có thống kê thật.

Trước khi phát hành, cần kiểm tra cấu hình JWT, kết nối thực tế từ thiết bị, RLS/GRANT trên Supabase để chặn đường ghi trực tiếp vượt qua API, kiểm kê constraint theo `plan.md`, thử migration và 14 kịch bản trên dữ liệu cách ly. Đổi client không tự thay đổi quyền CSDL. Các script migration yêu cầu đặt tên CHECK đã kiểm kê trong cùng phiên PostgreSQL; rollback đổi ca khôi phục CHECK theo kết quả kiểm kê ngày 30/10/2026 và dừng nếu có dữ liệu người nhận cần bảo toàn; phải đối chiếu lại môi trường đích trước khi dùng.
