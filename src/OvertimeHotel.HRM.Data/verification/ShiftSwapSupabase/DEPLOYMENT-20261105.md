# Chặn đổi ca trong ngày — 05/11/2026

Cả ca nhường và ca nhận phải có ngày làm việc sau hôm nay và không quá hôm nay +30 ngày, theo múi giờ Việt Nam. Áp dụng cho danh sách lựa chọn, gửi yêu cầu và đồng ý; mã nguồn WebAdmin/API cũng chặn khi tạo và duyệt yêu cầu cũ. Từ chối/rút yêu cầu vẫn được phép.

## Đã triển khai

- Project Supabase: `soethtbttktnwtxyxjrj`.
- Migration `20261105_shift_swap_future_dates.sql` đã commit. Chỉ cập nhật định nghĩa RPC, không thay dữ liệu nghiệp vụ. Bộ lọc đồng nghiệp kế thừa điều kiện từ RPC chính; Android không cần cài lại.
- Lấy ngày bằng `clock_timestamp()` và cập nhật lại ngày sau khi chờ khóa trước bước kiểm tra. Tránh dùng ngày bắt đầu transaction cho yêu cầu phải chờ qua nửa đêm.
- Backup riêng tại `/home/bryan/.local/share/hrm-deployment-backups/20261105-shift-swap-future-191003`, thư mục 700, file 600. Có định nghĩa RPC trước sửa và snapshot thuộc tính/quyền/hash dữ liệu trước/sau.
- Đã khôi phục đúng định nghĩa RPC thật vào fixture PostgreSQL cách ly, chạy migration/kiểm thử rồi rollback, xác nhận định nghĩa quay lại đúng bản sao lưu trước khi triển khai thật.
- Sau triển khai: định nghĩa hàm khớp bản đã kiểm thử; owner postgres, SECURITY DEFINER, search_path và ACL giữ nguyên; authenticated được EXECUTE, anon bị chặn. Hash bảng yêu cầu và phân ca giữ nguyên; số yêu cầu vẫn là 1.

## Kiểm chứng

- SQL trước sửa thất bại đúng tại `Today sender shift must be hidden, even before it starts`.
- SQL sau sửa đạt: ẩn/chặn ca hôm nay ở cả hai phía; ngày mai và +30 hợp lệ; +31 bị chặn; yêu cầu cũ không được đồng ý nếu ca gửi hoặc ca nhận đã là hôm nay; từ chối/rút vẫn hoạt động.
- Bộ lọc tương thích, khoảng nghỉ 8 giờ và đổi khác ngày đạt; hai client đồng thời chỉ tạo được một yêu cầu chờ.
- Migration/rollback giữ nguyên định nghĩa, dữ liệu yêu cầu và quyền khi chạy vòng đi/về; định nghĩa không khớp bị chặn và transaction không làm thay đổi trạng thái.
- C# đạt kiểm tra hai phía, mốc 16:59:59/17:00 UTC (nửa đêm Việt Nam), ngày mai, +30/+31 và lựa chọn trong ViewModel.
- WebAdmin build thành công: 3 projects, 0 lỗi, 0 cảnh báo. `git diff --check` đạt.
- DB thật, giao dịch chỉ đọc với claims nhân viên 10 giả lập: 3 ca của mình, 15 lựa chọn đồng nghiệp, 0 ngày vi phạm. Đây không phải kiểm chứng JWT thật hay giao diện Android.

## Giới hạn

WebAdmin đã sửa và build nhưng chưa khởi chạy bản mới trong lượt này. Chưa kiểm thử thao tác đăng nhập/gửi/đồng ý/duyệt trên thiết bị thật; không phát sinh yêu cầu nghiệp vụ thật. Trường hợp chờ khóa qua nửa đêm được rà soát mã và sửa bằng thời gian thực, chưa mô phỏng việc chờ qua nửa đêm trong SQL.

Rollback dùng `20261105_shift_swap_future_dates.rollback.sql`, chỉ hoàn nguyên quy tắc trong hàm, giữ nguyên quyền và dữ liệu. Đồng bộ bản WebAdmin cũ nếu muốn hoàn nguyên toàn bộ hành vi.
