# Sự kiện khách sạn — giai đoạn 1

## Cập nhật luồng phụ cấp (21/10/2026)

Sự kiện tạo mới không nhập ngân sách phụ cấp. Trong chi tiết sự kiện, **Thêm người** mở popup chọn nhân viên theo phòng ban, sau đó chọn vai trò và thời gian tham gia. Nhân sự được đánh giá **Hoàn thành** hoặc **Xuất sắc** sẽ nhận mức thưởng theo cấu hình khoản lương của loại sự kiện và phòng ban tại lúc phân công (hoặc định mức chuẩn khi không có cấu hình); **Không tham gia** nhận 0 đ. HR/Admin nghiệm thu sau khi đánh giá toàn bộ nhân sự; lúc đó hệ thống chốt tiền từng người, cộng thành ngân sách trả nhân viên và khóa sự kiện. Kết chuyển lương lấy số tiền đã chốt. Sự kiện đã có trước bản cập nhật giữ cách nhập ngân sách/phụ cấp cũ.

Cần áp dụng `src/OvertimeHotel.HRM.Data/migrations/20261021_event_acceptance_budget.sql` trước khi chạy bản web mới; migration thêm cờ với mặc định `false` để giữ nguyên dữ liệu sự kiện cũ và cột lưu phòng ban tại lúc phân công. Migration không tự chạy khi khởi động.

Mở `/SuKien` hoặc mục **Sự kiện khách sạn** trên menu. Admin và HR tạo sự kiện; Manager chỉ thấy và điều hành sự kiện được giao phụ trách. IT không có quyền quản lý mặc định.

- Bấm tên sự kiện trên lịch hoặc danh sách ngày để mở popup chi tiết. Các nút **Thông tin sự kiện**, **Nhân sự**, **Đánh giá & nghiệm thu** chuyển nội dung trong popup; đóng popup giữ lựa chọn ngày và trả bàn phím về sự kiện vừa mở. Đường dẫn chi tiết trực tiếp vẫn dùng được.
- Trong **Nhân sự**, mỗi phòng ban có nút **Thêm người** mở popup đè lên phần nhân sự đang xem. Tích chọn một hoặc nhiều nhân viên rồi bấm **Tiếp tục**, chọn vai trò riêng cho từng người và thời gian tham gia chung. Sự kiện cũ vẫn có ô phụ cấp dự kiến **mỗi người**. Vai trò được lọc theo loại sự kiện và phòng ban; **Vai trò khác** cần nhập tên và chỉ là bổ sung. Hủy hoặc Escape đóng popup và trả bàn phím về nút thêm người; phần nhân sự bên dưới giữ nguyên. Lưu kiểm tra cả nhóm trong một giao dịch: có người không hợp lệ thì chưa lưu ai và giữ các lựa chọn để sửa. Thành công cập nhật danh sách và lịch ngay trên trang. Nhân viên đã phân công không xuất hiện lại trong bộ chọn. Hợp đồng và trùng lịch được kiểm tra khi lưu.
- Mỗi phòng ban có vai trò chính cần `ceil(số khách / định mức khách mỗi người)`, một vai trò phối hợp bắt buộc cần 1 người, và một vai trò bổ sung. Danh sách hiển thị số cần, đã phân công và còn thiếu theo vai trò. Người dư ở vai trò này không bù được vai trò khác. Nhân viên thay ca chỉ được tính trong khoảng thời gian thực sự được giao.
- Định mức khách/người của vai trò chính: Tiệc cưới — Tiền sảnh 100, F&B 20, Bếp 50, Buồng phòng 100, Kỹ thuật 200, An ninh 100; MICE — Tiền sảnh 75, F&B 40, Bếp 80, Kỹ thuật/An ninh/IT 100; Gala — Tiền sảnh 100, F&B 20, Bếp 50, Kỹ thuật/An ninh 100; Festival — F&B 25, Bếp 30, Buồng phòng 100, Kỹ thuật 150, An ninh 100; VIP — Tiền sảnh 20, F&B 10, Bếp 25, Buồng phòng 15, An ninh 20.
- Chưa có số khách hoặc còn thiếu vai trò ở bất kỳ khoảng thời gian nào thì chưa thể bắt đầu. Khi đang diễn ra, gỡ người phải có người cùng vai trò thay thế đủ thời gian còn lại. Vai trò cũ không khớp danh mục vẫn được hiển thị để quản lý phân công lại; dữ liệu đã nghiệm thu/hủy giữ nguyên, không bị áp định mức mới hồi tố. Vai trò mới dùng cột `vai_tro_du_an` hiện có, nhiệm vụ mới để trống và không thay đổi mức thưởng theo phòng ban.
- Đánh giá theo phòng ban, chọn **Hoàn thành** hoặc **Không tham gia**. Các khoản đã nghiệm thu được hiển thị chỉ đọc; HR/Admin thực hiện nghiệm thu theo điều kiện hiện có.

- Nhấp từng ô để chọn/bỏ chọn nhiều ngày. Nhấp đúp ô ngày hoặc bấm **Tạo sự kiện** để mở popup ngay trên lịch. Popup điền ngày bắt đầu sớm nhất và ngày kết thúc muộn nhất đã chọn; sự kiện kéo dài liên tục giữa hai mốc này và có thể chỉnh lại. Bàn phím dùng mũi tên để di chuyển, Enter/Space để chọn, rồi nút **Tạo sự kiện**. Hủy/Escape giữ lựa chọn và trả lại vị trí bàn phím. Mở popup chưa ghi dữ liệu.
- Thời gian nhập/hiển thị theo Việt Nam (UTC+7), lưu UTC. Lịch hỗ trợ năm 1900–2100. Sự kiện qua đêm/nhiều ngày hiện trên từng ngày liên quan; kết thúc đúng 00:00 không chiếm ngày kế tiếp. Hai sự kiện nối tiếp nhau được dùng cùng địa điểm.
- HR/Admin sửa thông tin ở bước lên kế hoạch. Cần gỡ phân công trước khi đổi thời gian để đối chiếu lại hợp đồng và lịch. Không sửa sự kiện đã hủy hoặc đã nghiệm thu.
- Phân công từ nhiều phòng ban, kiểm tra nhân viên đang làm việc, hợp đồng bao phủ thời gian tham gia và trùng nhân viên/trùng giờ. Với sự kiện cũ còn kiểm tra tổng phụ cấp dự kiến theo ngân sách đã nhập. Lịch ca chỉ để đối chiếu, không tự sinh tăng ca.
- Luồng: **Lên kế hoạch → Đang diễn ra → Chờ nghiệm thu → Hoàn thành**. Bắt đầu/chuyển chờ nghiệm thu khi đã đến giờ tương ứng. Hủy phải ghi lý do.
- Đánh giá sau khi kết thúc và chuyển chờ nghiệm thu. Không tham gia có phụ cấp bằng 0. HR/Admin chốt khi tất cả đã đánh giá; sự kiện cũ còn kiểm tra tổng thực tế không vượt ngân sách đã nhập, còn sự kiện mới tính tổng ngân sách tại bước này. Sau chốt, thông tin và phân công được khóa.
- Thao tác ghi có lịch sử cùng giao dịch. Khóa giao dịch chống xung đột giữa nhiều yêu cầu; phiên bản chống ghi đè dữ liệu mới. Khi báo dữ liệu đã thay đổi, tải lại trang trước khi thao tác tiếp.

## Database và kiểm thử

Migration: `src/OvertimeHotel.HRM.Data/migrations/20261015_hotel_events.sql`. Migration bổ sung vào hai bảng `du_an`, `phan_cong_du_an`, giữ các cột phụ cấp/lịch sử cũ và không chạy tự động khi khởi động web. Quyền truy cập trực tiếp hai bảng từ `anon`/`authenticated` được đóng; Web dùng kết nối máy chủ. Mobile và kết chuyển lương thuộc giai đoạn tiếp theo.

```powershell
dotnet run --project tests/HotelEvents/HotelEvents.csproj
node tests/hotel-events-ui.mjs
dotnet run --project tests/HotelEventsIntegration/HotelEventsIntegration.csproj
```

Kiểm thử tích hợp đọc `EVENT_TEST_CONNECTION`, hoặc cấu hình WebAdmin trong `appsettings.json`/`appsettings.Development.json`. Chỉ chạy với database đã áp dụng migration. Chương trình tạo dữ liệu mang tiền tố ngẫu nhiên, kiểm tra cả lưu đồng thời và dọn đúng dữ liệu của lần chạy trong `finally`.

Kiểm tra popup trên đúng bản web đang sử dụng: `python tests/hotel-events-popup.py http://localhost:5065` (sau khi tạo bộ dữ liệu thử bên dưới, cần Playwright). Khi sửa Controller/ViewModel C#, build và khởi động lại web; Razor runtime compilation chỉ tự cập nhật `.cshtml`, không cập nhật assembly C# đang chạy. Popup báo riêng lỗi máy chủ, phiên đăng nhập, quyền và phiên biểu mẫu.

Kiểm tra popup chi tiết bằng `python tests/hotel-events-details.py http://localhost:5065`, sau đó `python tests/hotel-events-details-lifecycle.py http://localhost:5065` trên cùng bộ preview fixture mới. Hai bài kiểm tra lần lượt kiểm tra danh sách vai trò/ô vai trò khác, phân công kỹ thuật, bổ sung đủ 12 vị trí bắt buộc của hội nghị MICE 40 khách, đánh giá và nghiệm thu sự kiện lịch sử của fixture. Tạo lại fixture trước khi chạy lại cặp bài này. Kiểm tra lịch/ngày bằng `python tests/hotel-events-dates.py http://localhost:5065`.

Để kiểm tra trình duyệt với tài khoản thử, runner có `--preview-fixture` và `--cleanup-preview`; thông tin đăng nhập được ghi vào `.probe/event-preview.json` đã được Git bỏ qua. Dọn bộ dữ liệu thử ngay sau khi kiểm tra. Không dùng tài khoản thật cho các phép thử ghi dữ liệu.
