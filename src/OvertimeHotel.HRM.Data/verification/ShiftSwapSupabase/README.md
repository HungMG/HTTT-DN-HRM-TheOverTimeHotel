# Kiểm tra PostgreSQL cách ly

Chỉ chạy fixture trên database mới, rỗng, có tên `hrm_swap_test_*`. Fixture tạo các bảng/hàm giả lập Supabase Auth, không dùng dữ liệu thật. Cần PostgreSQL và user có quyền tạo role.

```sh
createdb hrm_swap_test_local
psql -X -v ON_ERROR_STOP=1 -d hrm_swap_test_local -f src/OvertimeHotel.HRM.Data/verification/ShiftSwapSupabase/fixture.sql
python3 src/OvertimeHotel.HRM.Data/verification/ShiftSwapSupabase/concurrency.py psql -d hrm_swap_test_local
```

Kiểm tra bộ lọc trên database fixture mới (sau fixture.sql, trước concurrency.py):

```sh
psql -X -v ON_ERROR_STOP=1 -d hrm_swap_test_local -f src/OvertimeHotel.HRM.Data/migrations/20261103_shift_swap_candidate_filter.sql -f src/OvertimeHotel.HRM.Data/verification/ShiftSwapSupabase/future-dates.sql -f src/OvertimeHotel.HRM.Data/verification/ShiftSwapSupabase/candidate-filter.sql
dotnet run --project verification/ShiftSwapViewModelChecks/ViewModelChecks.csproj
```

candidate-filter.sql thay đổi lịch fixture để kiểm tra cặp khác ngày; không chạy concurrency.py trên cùng DB sau file này. Dùng database fixture riêng cho mỗi nhóm.

`future-dates.sql` chạy trên fixture có migration bộ lọc, trước `candidate-filter.sql`/`concurrency.py`. Nó dùng transaction rollback để kiểm tra ca hôm nay bị ẩn/chặn ở cả hai phía, ngày mai và +30 được phép, +31 bị chặn, yêu cầu cũ không được đồng ý nhưng vẫn được từ chối/rút. Kiểm tra C# bổ sung mốc 16:59:59/17:00 UTC (nửa đêm Việt Nam).

Fixture kiểm tra quyền đọc theo nhân viên/phòng ban, người gửi/nhận, lý do, trạng thái, rút/từ chối/đồng ý, yêu cầu trùng, ca đã chấm công, nghỉ dưới 8 giờ, ca hết hạn, đổi chủ ca, phản hồi thất bại không đổi trạng thái, tài khoản khóa, phiên thiếu identity, role anon và quyền ghi trực tiếp. Concurrency chạy hai transaction đồng thời và yêu cầu đúng một yêu cầu được tạo.

Rollback được kiểm tra riêng trên cùng database fixture: chỉ gỡ hàm RPC, giữ toàn bộ yêu cầu cũ. Không chạy file rollback giữa fixture và concurrency.

Kiểm tra này không thay thế kiểm thử Supabase JWT/PostgREST và trigger thực tế. Xem SHIFT_SWAP_SUPABASE.md trong MobileApp cho điều kiện triển khai.
