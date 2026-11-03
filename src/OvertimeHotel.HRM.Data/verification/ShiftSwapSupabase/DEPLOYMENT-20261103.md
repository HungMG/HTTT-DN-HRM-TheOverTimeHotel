# Triển khai đổi ca Supabase ngày 03/11/2026

Đã được người dùng xác nhận triển khai trên project `soethtbttktnwtxyxjrj`.

- Sao lưu bảng `yeu_cau_doi_ca` (schema, dữ liệu, ACL/policy) bằng pg_dump custom. Trước triển khai bảng có 0 bản ghi. Thư mục backup riêng: `/home/bryan/.local/share/hrm-deployment-backups/20261103-shift-swap`, quyền thư mục 700, file 600.
- Khôi phục archive thành công vào PostgreSQL cách ly với các bảng phụ thuộc và helper identity giả lập. Kiểm thử RPC trên schema bảng vừa khôi phục đạt; không phải khôi phục toàn bộ Supabase.
- Áp dụng migration `20261103_shift_swap_supabase_rpc.sql` thành công, transaction đã commit.
- Kiểm tra hàm sau triển khai: owner postgres; SECURITY DEFINER; search_path `pg_catalog, public`; authenticated có EXECUTE; anon không có EXECUTE. Lời gọi PostgREST bằng anon bị từ chối HTTP 401/SQLSTATE 42501.
- Kiểm tra chỉ đọc tại DB thật với session claims giả lập của nhân viên 10: 5 ca của mình, 6 ca đồng nghiệp, 0 yêu cầu. Đây là kiểm tra nghiệp vụ/schema tại DB, không phải kiểm chứng JWT thực trên thiết bị.
- Android Debug arm64: build exit 0, 49 cảnh báo; APK có cùng SHA-256 chứng chỉ ký với bản cũ. Cập nhật tại chỗ bằng `adb install -r` thành công, thiết bị báo cập nhật lúc 20:55 ngày 03/11/2026; phiên Đỗ Thành Đạt được giữ.
- Chưa kiểm chứng màn hình đổi ca trên thiết bị sau cập nhật: người dùng đang dùng ứng dụng khác. Đã yêu cầu mở lại HRM để quan sát, tạm dừng thao tác GUI.
- Chưa tạo, phản hồi hoặc duyệt yêu cầu thật. Cần người dùng chọn ca test trước khi làm phát sinh giao dịch nghiệp vụ.

WebAdmin tạm tại localhost:5065 tiếp tục phục vụ bước quản lý duyệt. Nhân viên dùng đổi ca qua Supabase không cần URL WebAdmin hoặc USB trong lúc sử dụng. Giải trình chấm công chưa được chuyển sang RPC.
