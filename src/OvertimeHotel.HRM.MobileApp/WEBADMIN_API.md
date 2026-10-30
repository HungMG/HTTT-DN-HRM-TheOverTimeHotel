# Cấu hình API WebAdmin cho đơn từ

App dùng API WebAdmin để đọc/ghi đổi ca, giải trình và rút đơn nghỉ. Khi build, cấu hình URL gốc HTTPS của WebAdmin đã triển khai:

```bash
dotnet build src/OvertimeHotel.HRM.MobileApp/OvertimeHotel.HRM.MobileApp.csproj -c Release -p:WebAdminApiUrl=https://TEN_MAY_CHU_WEBADMIN
```

Thay `TEN_MAY_CHU_WEBADMIN` bằng địa chỉ thật có chứng chỉ HTTPS hợp lệ. Không đặt token hoặc mật khẩu trong URL. Nếu không cấu hình, các thao tác này báo thiếu cấu hình; app không tự ghi trực tiếp qua PostgREST. Chế độ demo dùng dữ liệu trong bộ nhớ.

Trước khi phát hành, cần kiểm tra cấu hình JWT, kết nối thực tế từ thiết bị, RLS/GRANT trên Supabase để chặn đường ghi trực tiếp vượt qua API, kiểm kê constraint theo `plan.md`, thử migration và 14 kịch bản trên dữ liệu cách ly. Đổi client không tự thay đổi quyền CSDL. Các script migration yêu cầu đặt tên CHECK đã kiểm kê trong cùng phiên PostgreSQL; rollback đổi ca khôi phục CHECK theo kết quả kiểm kê ngày 30/10/2026 và dừng nếu có dữ liệu người nhận cần bảo toàn; phải đối chiếu lại môi trường đích trước khi dùng.
