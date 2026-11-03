# Đổi ca qua Supabase, quản lý duyệt trên WebAdmin

Android dùng JWT của nhân viên gọi `rest/v1/rpc/hrm_shift_swap` để đọc ca, đọc yêu cầu, gửi, phản hồi và rút. Quản lý duyệt/từ chối trên WebAdmin hiện có, cùng bảng `yeu_cau_doi_ca`. Android không cần WebAdminApiUrl cho đổi ca. Giải trình và các chức năng dùng WebAdmin khác vẫn cần cấu hình API như trước.

Danh sách ca đồng nghiệp dùng `hrm_shift_swap_partner_options()` sau migration `20261103_shift_swap_candidate_filter.sql`. Mỗi ca trả các mã ca nhường tương thích; Android lọc ngay khi người dùng đổi ca nhường và xóa lựa chọn đã mất hiệu lực. RPC gửi/nhận vẫn kiểm tra lại lịch hiện tại. Triển khai migration bộ lọc trước bản Android tương ứng; rollback client trước khi gỡ hàm bộ lọc. Hàm không thay đổi dữ liệu nghiệp vụ.

## Trước triển khai Supabase thật

1. Xác nhận đúng project, backup có khả năng khôi phục, kiểm kê schema/quyền bằng truy vấn bên dưới. Xem định nghĩa `nhan_vien_hien_tai()`: phải ánh xạ auth.uid/JWT app_metadata do server cấp, không tin user_metadata do người dùng tự sửa. Dừng nếu không bảo đảm điều này.
2. Bảng đổi ca phải có đầy đủ cột phản hồi người nhận và CHECK hỗ trợ `CHO_NGUOI_NHAN`, `CHO_DUYET`, `DA_DUYET`, `TU_CHOI`, `DA_HUY`. Nếu thiếu, kiểm kê và áp migration receiver-flow riêng trước; không đoán tên constraint.
3. Sau xác nhận triển khai, chạy `20261103_shift_swap_supabase_rpc.sql` bằng chủ sở hữu DB. Hàm SECURITY DEFINER phải thuộc role quản trị tin cậy; authenticated/anon không được CREATE trong schema public. Script thu hồi ghi trực tiếp bảng và dừng nếu còn quyền kế thừa/cột; kiểm kê trước vì các client cũ ghi trực tiếp sẽ bị chặn.
4. Không chạy lại migration cũ requests_api_write_boundary sau migration này: kiểm tra mọi GRANT/policy kế tiếp để giữ giới hạn quyền. Không cấp service_role hay mật khẩu database cho Android.
5. Build/cài APK sau khi RPC triển khai. Test hai tài khoản cùng phòng ban: A gửi → B đồng ý → quản lý duyệt trên WebAdmin → A/B tải lại lịch. Test rút ở cả hai trạng thái chờ, B từ chối cần lý do, tài khoản thứ ba không xem/sửa được, ca đã chấm công và ca tranh chấp bị chặn. Các thao tác này dùng dữ liệu thật, cần chỉ định tài khoản và ca test trước.

```sql
SELECT current_database(), current_user;
SELECT pg_get_functiondef('public.nhan_vien_hien_tai()'::regprocedure);
SELECT column_name,data_type FROM information_schema.columns
 WHERE table_schema='public' AND table_name='yeu_cau_doi_ca';
SELECT conname,pg_get_constraintdef(oid) FROM pg_constraint
 WHERE conrelid='public.yeu_cau_doi_ca'::regclass;
SELECT * FROM pg_policies WHERE schemaname='public'
 AND tablename IN ('yeu_cau_doi_ca','phan_ca','cham_cong','tai_khoan');
SELECT grantee,privilege_type FROM information_schema.role_table_grants
 WHERE table_schema='public' AND table_name='yeu_cau_doi_ca';
SELECT has_schema_privilege('authenticated','public','CREATE') AS authenticated_can_create,
       has_schema_privilege('anon','public','CREATE') AS anon_can_create;
```

## Rollback

Phát hành lại client dùng WebAdmin API trước, sau đó chạy file rollback tương ứng để gỡ đúng RPC này. Giữ yêu cầu và phản hồi đã phát sinh; giữ việc cấm ghi trực tiếp bảng. Không khôi phục GRANT không được kiểm kê.

## Phạm vi kiểm thử

Fixture PostgreSQL là dữ liệu cách ly với hàm identity giả lập. Nó kiểm tra nghiệp vụ và quyền EXECUTE/bảng; không chứng minh token Supabase thật, helper identity thật, các trigger thật hoặc PostgREST schema cache. Android build thành công cũng chưa chứng minh thiết bị hoạt động với production.
