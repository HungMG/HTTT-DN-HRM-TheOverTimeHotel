# Kiểm thử đơn từ trên PostgreSQL cách ly

`acceptance.py` chỉ kết nối qua socket `~/.local/share/hrm-isolated-check`, cổng 55448. Nó tạo database riêng với các bảng tối thiểu, chạy migration và rollback, thử CHECK, kiểm tra privilege bằng role thử, rồi xóa database fixture. Script không dùng cấu hình Supabase.

`tests/RequestsApiAcceptance` tạo một database riêng trong cùng cluster, dùng schema do EF tạo và gọi trực tiếp controller với claim thử. Nó không thay thế kiểm thử JWT, PostgREST, HTTP hoặc schema thật.

Để khởi động lại cluster cục bộ đã tạo trong phiên này:

```bash
cd ~/.local/share/hrm-isolated-check
LD_LIBRARY_PATH="$PWD/pg/usr/lib/x86_64-linux-gnu" "$PWD/pg/usr/lib/postgresql/18/bin/pg_ctl" -D "$PWD/data" -l "$PWD/postgres.log" -o "-c listen_addresses='' -c unix_socket_directories='$PWD' -p 55448 -c shared_buffers=32MB -c max_connections=20" start
```

Kiểm kê chỉ đọc ngày 30/10/2026 trên database Supabase cấu hình cho WebAdmin tìm thấy:

- `don_tu`: CHECK trạng thái `don_tu_trang_thai_check`.
- `giai_trinh_cham_cong`: CHECK trạng thái `giai_trinh_cham_cong_trang_thai_check`; giữ nguyên `chk_giai_trinh_can_gio` và `chk_giai_trinh_tu_choi_can_y_kien`.
- `yeu_cau_doi_ca`: CHECK trạng thái `yeu_cau_doi_ca_trang_thai_check`, CHECK lý do `chk_doi_ca_tu_choi_can_ly_do`, DEFAULT `CHO_DUYET`. Số đơn `TU_CHOI` lúc kiểm kê: 0; phải kiểm tra lại trước migration.
- Hai bảng đổi ca và giải trình có RLS nhưng role `authenticated` có quyền INSERT/UPDATE và policy cho phép người tham gia ghi trực tiếp. Policy tạo đơn nghỉ chỉ kiểm tra chủ đơn, chưa bắt buộc trạng thái chờ duyệt hoặc thông tin duyệt rỗng. `20261103_requests_api_write_boundary.sql` là bản đề xuất ngăn đường ghi này và siết điều kiện tạo đơn nghỉ; cần đánh giá ảnh hưởng tới client cũ, quyền kế thừa, backup, và kiểm thử trên staging trước khi áp dụng.

Migration 3 và rollback khôi phục schema theo kết quả kiểm kê trên; không coi chúng là phổ quát cho các môi trường có constraint khác. Rollback dừng nếu có dữ liệu xác nhận của người nhận cần bảo toàn. Duyệt đơn nghỉ hiện vẫn ghi trực tiếp PostgREST và policy UPDATE hiện cho Admin cập nhật rộng; migration đề xuất không thu hồi quyền UPDATE bảng `don_tu` vì như vậy sẽ làm hỏng luồng duyệt đang dùng. Cần thiết kế và kiểm thử riêng trước khi tuyên bố toàn bộ đường ghi đã được bảo vệ. Quyền đã thu hồi không tự khôi phục khi rollback schema; chỉ khôi phục theo bản kiểm kê quyền và phương án cutover/rollback được duyệt.

Không chạy các script lên môi trường thật khi chưa hoàn tất backup, cutover, xác nhận URL/API và nghiệm thu. Các kiểm thử cục bộ không chứng minh an toàn trên Supabase hoặc hoàn thành tất cả 14 ca của plan.

## Thử liên thông USB ngày 30/10/2026

Một bản sao tạm của app (mã ứng dụng `.usbtest`) dùng tài khoản mẫu và HTTP loopback qua `adb reverse`; bản chính không thay đổi cấu hình TLS. Máy chủ fixture chỉ nghe `127.0.0.1`, dùng database PostgreSQL rỗng cách ly và xác thực giả lập, nhưng chạy các controller nghiệp vụ thật. Không dùng token Supabase thật.

- Qua `curl` chạy trên điện thoại: A tạo đổi ca, B đồng ý, A rút, trạng thái `DA_HUY`; phản hồi cũ trả 409.
- Trên giao diện app: A tạo yêu cầu #3; B xác nhận qua HTTP; controller quản lý duyệt; PostgreSQL ghi `DA_DUYET` và hoán đổi cả hai phân ca. App hiển thị trạng thái đã duyệt.
- Qua `curl` trên điện thoại: người khác rút đơn nghỉ bị 403, chủ đơn rút thành công, rút lại nhận 409.
- Chưa kiểm tra Supabase JWT thật, HTTPS, PostgREST/RLS thật, UI duyệt trên WebAdmin hoặc toàn bộ 14 ca đầu-cuối. Đây không phải kết quả nghiệm thu môi trường thật.
